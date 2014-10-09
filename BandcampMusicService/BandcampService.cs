using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using HtmlAgilityPack;
using Microsoft.Ajax.Utilities;
using MusicServiceLeecher.Utilities;
using Newtonsoft.Json.Linq;

namespace MusicServiceLeecher.MusicStreamingServices.BandcampMusicService
{
    public class BandcampService : IMusicStreamingService
    {
        private Minifier m_minifier;
        public string Name { get; private set; }
        public Uri BaseUri { get; private set; }

        public BandcampService()
        {
            Name = "Bandcamp";
            BaseUri = new Uri("http://www.bandcamp.com");
            m_minifier = new Minifier();
        }

        public bool DownloadSong(IWorkspace workspace, Uri songUri)
        {
            try
            {
                Track track = GetTrackByUri(songUri);
                return workspace.HandleTrack(track);
            }
            catch (Exception e)
            {
                //todo: log exception
                return false;
            }
        }

        public bool DownloadAlbum(IWorkspace workspace, Uri albumUri)
        {
            try
            {
                IEnumerable<Track> album = GetAlbumTracksByUri(albumUri);
                return workspace.HandleAlbum(album);
            }
            catch (Exception e)
            {
                //todo: log exception
                return false;
            }
        }

        private IEnumerable<Track> GetAlbumTracksByUri(Uri albumUri)
        {
            List<Track> result = new List<Track>();

            WebRequest request = WebUtils.CreateRequest(albumUri);
            HttpWebResponse httpResponse = WebUtils.GetResponse(request);

            string responseText;
            using (StreamReader sr = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8))
            {
                responseText = sr.ReadToEnd();
            }

            HtmlDocument responseHtml = WebUtils.GetResponseHtml(responseText);

            FillMetadata(ref result, responseText, responseHtml);

            return result;
        }

        private void FillMetadata(ref List<Track> result, string responseText, HtmlDocument responseHtml)
        {
            JObject embedData = GetEmbedData(responseText, "EmbedData");
            JObject stringBlock = GetEmbedData(responseText, "TralbumData");

            string albumTitle = embedData["album_title"].ToString();
            string albumArtist = stringBlock["artist"].ToString();
            DateTime albumDate = stringBlock["album_release_date"].ToObject<DateTime>();

            foreach (JToken trackInfo in stringBlock["trackinfo"])
            {
                string trackUri = string.Empty;

                if (trackInfo["file"] != null && trackInfo["file"]["mp3-128"] != null)
                    trackUri = trackInfo["file"]["mp3-128"].ToString();

                Uri uri = null;

                if (!string.IsNullOrEmpty(trackUri))
                    uri = new Uri(trackUri);

                Track track = new Track(albumArtist, trackInfo["title"].ToString(), (uint)albumDate.Year, albumTitle,
                    trackInfo["track_num"].ToObject<uint>(), uri);

                result.Add(track);
            }

            Uri albumArt;
            if (TryGetAlbumArt(responseHtml, out albumArt))
            {
                result.ForEach(track => track.AlbumArtUri = albumArt);
            }

        }

        private bool TryGetAlbumArt(HtmlDocument responseHtml, out Uri albumArt)
        {
            string albumArtUri;
            try
            {
                albumArtUri = responseHtml.GetElementbyId("tralbumArt").ChildNodes["a"].ChildNodes["img"].Attributes["src"].Value;
            }
            catch (Exception)
            {
                albumArt = null;
                return false;
            }

            albumArt = new Uri(albumArtUri);
            return true;
        }

        private JObject GetEmbedData(string responseText, string keyword)
        {
            string embedBlock = responseText.Split(new[] { string.Format("var {0} = ", keyword) }, StringSplitOptions.None)[1];
            string close = "};";
            embedBlock = string.Format("{0}{1}", embedBlock.Split(new[] { close }, StringSplitOptions.None)[0], close);
            string minifiedString = m_minifier.MinifyJavaScript(string.Format("var {0} = {1}", keyword, embedBlock));
            string metadataString = minifiedString.Split(new[] { string.Format("var {0}=", keyword) }, StringSplitOptions.None)[1];

            //Unfortunatly the minifier doesn't do the following code by itself
            metadataString = metadataString.Replace(":!0", ":1");
            metadataString = metadataString.Replace(":!1", ":0");

            return JObject.Parse(metadataString);
        }

        private Track GetTrackByUri(Uri trackUri)
        {
            return GetAlbumTracksByUri(trackUri).FirstOrDefault();
        }
    }
}
