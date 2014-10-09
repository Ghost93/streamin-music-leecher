using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using HtmlAgilityPack;
using MusicServiceLeecher.Utilities;
using Newtonsoft.Json.Linq;
using RestSharp;
using SoundCloud.NET;

namespace MusicServiceLeecher.MusicStreamingServices.SoundcloudMusicService
{
    public class SoundcloudService : IMusicStreamingService
    {
        #region Private Members

        private const string MP3_128_KBPS_URL = "http_mp3_128_url";

        //todo: get it out of here!
        private string m_ClientId = "143d391e891ba3f8853fe3ab63c68037";
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
            try
            {
                Track track = GetSongByUri(songUri);
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

        #endregion

        #region Private Functions

        private Track GetSongByUri(Uri songUri)
        {
            int songId = ExtractIdFromUri(songUri);

            SoundCloud.NET.Track soundcloudTrack = SoundCloud.NET.Track.GetTrack(songId);

            return GetTrackBySoundcloudTrack(soundcloudTrack);
        }

        private IEnumerable<Track> GetAlbumTracksByUri(Uri albumUri)
        {
            int playlistId = ExtractIdFromUri(albumUri);

            Playlist playlist = Playlist.GetPlaylist(playlistId);

            return GetPlaylist(playlist);
        }

        private List<Track> GetPlaylist(Playlist playlist)
        {
            List<Track> res = new List<Track>();

            for (int i = 1; i <= playlist.Tracks.Count; i++)
            {
                SoundCloud.NET.Track trackToHandle = playlist.Tracks[i - 1];

                Track trackToAdd = GetTrackBySoundcloudTrack(trackToHandle);
                trackToAdd.TrackNumber = (uint)i;
                if (playlist.ReleaseYear != null)
                {
                    trackToAdd.Year = (uint)playlist.ReleaseYear;
                }
                trackToAdd.Album = playlist.Title;

                res.Add(trackToAdd);
            }

            if (!string.IsNullOrEmpty(playlist.ArtworkUrl))
            {
                Uri artworkUri = new Uri(playlist.ArtworkUrl);
                res.ForEach(track => track.AlbumArtUri = artworkUri);
            }

            return res;
        }

        private Track GetTrackBySoundcloudTrack(SoundCloud.NET.Track trackToHandle)
        {
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

            uint trackReleaseYear = 0;
            uint.TryParse(trackToHandle.ReleaseYear, out trackReleaseYear);

            Track res = new Track(trackArtist, trackName, trackReleaseYear, string.Empty, 0, downloadUri);
            if (trackToHandle.Artwork != null)
            {
                res.AlbumArtUri = new Uri(trackToHandle.Artwork);
            }
            return res;
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
            if (!m_SoundCloudClient.IsAuthenticated)
            {
                ReAuthenticate();
            }

            RestClient client = new RestClient("http://api.sndcdn.com");
            client.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64)";
            RestRequest request = new RestRequest("i1/tracks/{trackid}/streams", Method.GET);
            request.AddParameter("client_id", m_ClientId);
            request.AddUrlSegment("trackid", trackId.ToString());
            request.AddHeader("content-type", "application/json");

            IRestResponse response = client.Execute(request);
            JObject trackMetadata = JObject.Parse(response.Content);

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
                             new SoundCloudCredentials(m_ClientId,
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
