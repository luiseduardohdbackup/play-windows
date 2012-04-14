﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Akavache;
using Ninject;
using ReactiveUI;
using RestSharp;
using RestSharp.Contrib;

namespace Play.Models
{
    public interface IPlayApi
    {
        IObservable<Song> NowPlaying();
        IObservable<BitmapImage> FetchImageForAlbum(Song song);
        IObservable<string> ListenUrl();
        IObservable<List<Song>> Queue();
        IObservable<Unit> Star(Song song);
        IObservable<Unit> Unstar(Song song);
        IObservable<List<Song>> Search(string query);
        IObservable<List<Song>> AllSongsForArtist(string name);
        IObservable<List<Song>> AllSongsOnAlbum(string artist, string album);
    }

    public class PlayApi : IPlayApi, IEnableLogger
    {
        readonly IRestClient client;
        readonly IBlobCache cache;

        [Inject]
        public PlayApi(IRestClient authedClient, [Named("LocalMachine")] IBlobCache blobCache)
        {
            client = authedClient;
            cache = blobCache;
        }

        public IObservable<Song> NowPlaying()
        {
            var rq = new RestRequest("now_playing");
            return client.RequestAsync<Song>(rq).Select(x => x.Data);
        }

        public IObservable<List<Song>> Queue()
        {
            var rq = new RestRequest("queue");
            return client.RequestAsync<SongQueue>(rq).Select(x => {
                return x.Data.songs.ToList();
            });
        }

        public IObservable<Unit> Star(Song song)
        {
            var rq = new RestRequest("star") {Method = Method.POST};
            rq.AddParameter("id", song.id);

            return client.RequestAsync(rq).Select(_ => Unit.Default);
        }

        public IObservable<Unit> Unstar(Song song)
        {
            var rq = new RestRequest("star") {Method = Method.DELETE};
            rq.AddParameter("id", song.id);

            return client.RequestAsync(rq).Select(_ => Unit.Default);
        }

        public IObservable<BitmapImage> FetchImageForAlbum(Song song)
        {
            var user = client.DefaultParameters.First(x => x.Name == "login").Value;
            var rq = new RestRequest(String.Format("images/art/{0}.png?login={1}", song.id, user));

            var fullUrl = client.BuildUri(rq).ToString();
            this.Log().Info("Fetching URL for image: {0}", fullUrl);
            return cache.LoadImageFromUrl(fullUrl);
        }

        public IObservable<List<Song>> Search(string query)
        {
            var rq = new RestRequest("search");
            rq.AddParameter("q", query);

            return client.RequestAsync<SongQueue>(rq).Select(x => x.Data.songs);
        }

        public IObservable<List<Song>> AllSongsForArtist(string name)
        {
            // NB: https://github.com/play/play/issues/135
            var rq = new RestRequest(String.Format("artist/{0}", 
                HttpUtility.UrlEncode(name).Replace("+", "%20")));

            return client.RequestAsync<SongQueue>(rq).Select(x => x.Data.songs);
        }

        public IObservable<List<Song>> AllSongsOnAlbum(string artist, string album)
        {
            // NB: https://github.com/play/play/issues/135
            var rq = new RestRequest(String.Format("artist/{0}/album/{1}", 
                HttpUtility.UrlEncode(artist).Replace("+", "%20"), 
                HttpUtility.UrlEncode(album).Replace("+", "%20")));

            return client.RequestAsync<SongQueue>(rq).Select(x => x.Data.songs);
        }

        public IObservable<string> ListenUrl()
        {
            var uri = new Uri(client.BaseUrl);
            return Observable.Return(
                String.Format("{0}:8000/listen", uri.GetLeftPart(UriPartial.Authority).Replace("https", "http")));
        }
    }
}
