using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using HtmlAgilityPack;
using MusicServiceLeecher.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoundCloud.NET;

namespace MusicServiceLeecher.MusicStreamingServices
{
    public class SoundcloudService : IMusicStreamingService
    {

        //todo: get it out of here!
        private string clientId = "143d391e891ba3f8853fe3ab63c68037";
        private string clientSecret = "477d7ea6a65dcbe25aa6c4c60ccb9ef8";
        private string username = "gh0st93";
        private string password = "20245056";
        private SoundCloudAccessToken m_AccessToken;
        private SoundCloudClient m_SoundCloudClient;

        public string Name { get; private set; }
        public Uri BaseUri { get; private set; }

        public SoundcloudService()
        {
            Name = "Soundcloud";
            BaseUri = new Uri("http://www.soundcloud.com");

            SoundCloudAccessToken accessToken;
            m_SoundCloudClient = GetAuthorizedSoundCloudClient(out accessToken);
            m_AccessToken = accessToken;
        }


        public bool DownloadSong(Workspace workspace, Uri songUri)
        {
            throw new NotImplementedException();
        }

        public bool DownloadAlbum(Workspace workspace, Uri albumUri)
        {
            IEnumerable<Track> album;

            try
            {
                album = GetAlbumTracksByUri(albumUri);
            }
            catch (Exception e)
            {
                //todo: log exception
                return false;
            }

            try
            {
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
            int playlistId = ExtractIdFromUri(albumUri);
            Playlist playlist = Playlist.GetPlaylist(playlistId);

            List<Track> res = GetPlaylist(playlist);

            if (!string.IsNullOrEmpty(playlist.ArtworkUrl))
            {
                Uri artworkUri = new Uri(playlist.ArtworkUrl);
                res.ForEach(track=>track.AlbumArtUri = artworkUri);
            }

            return res;
        }

        private List<Track> GetPlaylist(Playlist playlist)
        {
            List<Track> res = new List<Track>();

            for (int i = 1; i <= playlist.Tracks.Count; i++)
            {
                SoundCloud.NET.Track trackToHandle = playlist.Tracks[i - 1];

                string trackArtist = string.Empty, trackName = string.Empty;
                string[] splittedTitle = trackToHandle.Title.Trim().Split(new[] {" - "}, StringSplitOptions.RemoveEmptyEntries);

                if (splittedTitle.Length >= 2)
                {
                    trackArtist = splittedTitle[0];
                    trackName = splittedTitle[1];
                }
                else if (splittedTitle.Length <= 1)
                {
                    trackName = trackToHandle.Title;
                }


                Uri downloadUri = null;
                if (trackToHandle.Downloadable || trackToHandle.Streamable)
                {
                    downloadUri = CreateDownloadUriByTrackId(trackToHandle.Id);
                }

                if(downloadUri==null)
                {
                    Console.WriteLine("Track " + trackToHandle.Title + " is skipped. No download/stream uri at all...");
                }

                uint trackReleaseYear;
                if (!uint.TryParse(trackToHandle.ReleaseYear, out trackReleaseYear))
                {
                    trackReleaseYear = playlist.ReleaseYear != null ? (uint) playlist.ReleaseYear : 0;
                }

                Track trackToAdd = new Track(trackArtist, trackName, trackReleaseYear, playlist.Title, (uint) (i), downloadUri);
                if (trackToHandle.Artwork != null)
                {
                    trackToAdd.AlbumArtUri = new Uri(trackToHandle.Artwork);
                }

                res.Add(trackToAdd);
            }
            return res;
        }

        private Uri CreateDownloadUriByTrackId(int trackId)
        {
            string streamUriString = string.Format("http://api.sndcdn.com/i1/tracks/{0}/streams?client_id={1}", trackId, clientId);
            HttpWebResponse response = MakeModifiedUserAgentRequest(new Uri(streamUriString), true);
            string jsonResponse = WebUtils.GetResponseText(response, Encoding.UTF8);
            JObject metadata = JObject.Parse(jsonResponse);
            if (metadata["http_mp3_128_url"] == null)
            {
                return null;
            }
            return new Uri(metadata["http_mp3_128_url"].ToString());
        }

        private int ExtractIdFromUri(Uri uri)
        {
            HtmlDocument responseHtml = GetResponseHtml(uri);
            HtmlNode headNode = responseHtml.DocumentNode.ChildNodes["html"].ChildNodes["head"];
            if (headNode == null)
            {
                throw new WebException("Could not find HEAD node", WebExceptionStatus.UnknownError);
            }
            string androidUrl =
                headNode.ChildNodes
                    .Where(
                        node =>
                            node.Name == "meta" && node.HasAttributes &&
                            node.Attributes.Any(
                                attribute => attribute.Name == "property" && attribute.Value == "al:android:url"))
                    .Select(node => node.Attributes["content"].Value)
                    .FirstOrDefault();

            if (androidUrl == null) throw new ArgumentNullException("androidUrl", "Could not get androidUrl");

            int res = int.Parse(androidUrl.Split(':').Last());
            return res;
        }

        private HtmlDocument GetResponseHtml(Uri uri)
        {
            HttpWebResponse response = MakeModifiedUserAgentRequest(uri);

            return WebUtils.GetResponseHtml(response);
        }

        private HttpWebResponse MakeModifiedUserAgentRequest(Uri uri, bool json = false)
        {
            if (!m_SoundCloudClient.IsAuthenticated)
            {
                ReAuthenticate();
            }
            HttpWebRequest request = WebUtils.CreateRequest(uri) as HttpWebRequest;
            if (request == null)
            {
                throw new InvalidCastException("Could not cast web request to HttpWebRequest");
            }

            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64)";
            if (json)
                request.ContentType = "application/json";
            return WebUtils.GetResponse(request);
        }

        private void ReAuthenticate()
        {
            m_AccessToken = m_SoundCloudClient.Authenticate();
        }

        private SoundCloudClient GetAuthorizedSoundCloudClient(out SoundCloudAccessToken accessToken)
        {
            SoundCloudClient client;
            try
            {
                client = new SoundCloudClient(new SoundCloudCredentials(clientId, clientSecret, username, password));

                accessToken = client.Authenticate();
                if (!client.IsAuthenticated || accessToken == null)
                {
                    throw new AuthenticationException("Could not authenticate soundcloud client!");
                }
            }
            catch (WebException webException)
            {
                throw new AuthenticationException("Could not authenticate soundcloud client!", webException);
            }
            return client;
        }
    }
}
