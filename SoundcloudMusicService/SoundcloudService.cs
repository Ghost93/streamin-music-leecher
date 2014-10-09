using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using HtmlAgilityPack;
using MusicServiceLeecher.Utilities;
using Newtonsoft.Json.Linq;
using SoundCloud.NET;

namespace MusicServiceLeecher.MusicStreamingServices.SoundcloudMusicService
{
    public class SoundcloudService : IMusicStreamingService
    {
        #region Private Members

        private const string MP3_128_KBPS_URL = "http_mp3_128_url";

        //todo: get it out of here!
        private string m_ClintId = "143d391e891ba3f8853fe3ab63c68037";
        private string m_ClientSecret = "477d7ea6a65dcbe25aa6c4c60ccb9ef8";
        private string m_SoundCloudUsername = "gh0st93";
        private string m_SoundCloudPassword = "20245056";

        private SoundCloudAccessToken m_AccessToken;
        private SoundCloudClient m_SoundCloudClient;

        #endregion

        #region Properties

        public string Name { get; private set; }
        public Uri BaseUri { get; private set; }

        #endregion

        public SoundcloudService()
        {
            Name = "Soundcloud";
            BaseUri = new Uri("http://www.soundcloud.com");

            SoundCloudAccessToken accessToken;
            m_SoundCloudClient = GetAuthorizedSoundCloudClient(out accessToken);
            m_AccessToken = accessToken;
        }

        #region Public Functions

        public bool DownloadSong(IWorkspace workspace, Uri songUri)
        {
            throw new NotImplementedException();
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

        #endregion

        #region Private Functions

        private IEnumerable<Track> GetAlbumTracksByUri(Uri albumUri)
        {
            int playlistId = ExtractIdFromUri(albumUri);
            Playlist playlist = Playlist.GetPlaylist(playlistId);

            List<Track> res = GetPlaylist(playlist);

            if (!string.IsNullOrEmpty(playlist.ArtworkUrl))
            {
                Uri artworkUri = new Uri(playlist.ArtworkUrl);
                res.ForEach(track => track.AlbumArtUri = artworkUri);
            }

            return res;
        }

        private List<Track> GetPlaylist(Playlist playlist)
        {
            List<Track> res = new List<Track>();

            for (int i = 1; i <= playlist.Tracks.Count; i++)
            {
                SoundCloud.NET.Track trackToHandle = playlist.Tracks[i - 1];

                string trackName;
                string trackArtist = GetTrackArtist(trackToHandle, out trackName);

                Uri downloadUri = null;
                if (trackToHandle.Downloadable || trackToHandle.Streamable)
                {
                    downloadUri = CreateDownloadUriByTrackId(trackToHandle.Id);
                }

                if (downloadUri == null)
                {
                    Console.WriteLine("Track {0} is skipped. No download/stream uri at all...", trackToHandle.Title);
                }

                uint trackReleaseYear = GetTrackReleaseYear(playlist, trackToHandle);

                Track trackToAdd = new Track(trackArtist, trackName, trackReleaseYear, playlist.Title, (uint)(i),
                    downloadUri);
                if (trackToHandle.Artwork != null)
                {
                    trackToAdd.AlbumArtUri = new Uri(trackToHandle.Artwork);
                }

                res.Add(trackToAdd);
            }
            return res;
        }

        private static uint GetTrackReleaseYear(Playlist playlist, SoundCloud.NET.Track trackToHandle)
        {
            uint trackReleaseYear;
            if (!uint.TryParse(trackToHandle.ReleaseYear, out trackReleaseYear))
            {
                trackReleaseYear = playlist.ReleaseYear != null ? (uint)playlist.ReleaseYear : 0;
            }
            return trackReleaseYear;
        }

        private string GetTrackArtist(SoundCloud.NET.Track trackToHandle, out string trackName)
        {
            trackName = string.Empty;
            string trackArtist = string.Empty;
            string[] splittedTitle = trackToHandle.Title.Trim()
                .Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);

            if (splittedTitle.Length >= 2)
            {
                trackArtist = splittedTitle[0];
                trackName = splittedTitle[1];
            }
            else if (splittedTitle.Length <= 1)
            {
                trackName = trackToHandle.Title;
            }
            return trackArtist;
        }

        private Uri CreateDownloadUriByTrackId(int trackId)
        {
            //todo: use restsharp

            string streamUriString =
                string.Format("http://api.sndcdn.com/i1/tracks/{0}/streams?client_id={1}",
                    trackId, m_ClintId);

            HttpWebResponse response = MakeModifiedUserAgentRequest(new Uri(streamUriString), true);
            string jsonResponse = WebUtils.GetResponseText(response, Encoding.UTF8);
            JObject trackMetadata = JObject.Parse(jsonResponse);
            if (trackMetadata[MP3_128_KBPS_URL] == null)
            {
                return null;
            }
            return new Uri(trackMetadata[MP3_128_KBPS_URL].ToString());
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
                                att => att.Name == "property" && att.Value == "al:android:url"))
                    .Select(node => node.Attributes["content"].Value)
                    .FirstOrDefault();

            if (androidUrl == null)
            {
                throw new ArgumentNullException("androidUrl", "Could not get androidUrl");
            }

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
            {
                request.ContentType = "application/json";
            }
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
                client = new SoundCloudClient(
                             new SoundCloudCredentials(m_ClintId,
                                                       m_ClientSecret,
                                                       m_SoundCloudUsername,
                                                       m_SoundCloudPassword)
                                             );

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

        #endregion

    }
}
