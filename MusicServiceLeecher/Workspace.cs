using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using MusicServiceLeecher.Utilities;
using TagLib;
using TagLib.Id3v2;
using File = TagLib.File;

namespace MusicServiceLeecher
{
    public class Workspace
    {
        #region Constants

        private const string DEFAULT_PATTERN = "%tracknum - %trackname";
        private const string DEFAULT_FILE_EXTENSION = ".mp3";

        #endregion

        #region Properties

        public DirectoryInfo WorkingDirectory { get; private set; }

        public string Pattern { get; private set; }

        #endregion

        #region Constructors

        public Workspace(string workingDirectory, bool createIfNotExists)
            : this(workingDirectory, createIfNotExists, DEFAULT_PATTERN)
        {
        }

        public Workspace(DirectoryInfo workingDirectory, bool createIfNotExists)
            : this(workingDirectory, createIfNotExists, DEFAULT_PATTERN)
        {
        }

        public Workspace(string workingDirectory, bool createIfNotExists, string pattern)
            : this(new DirectoryInfo(workingDirectory), createIfNotExists, pattern)
        {
        }

        public Workspace(DirectoryInfo workingDirectory, bool createIfNotExists, string pattern)
        {
            if (workingDirectory == null)
            {
                throw new ArgumentNullException("workingDirectory");
            }

            if (workingDirectory.Exists)
            {
                WorkingDirectory = workingDirectory;
            }
            else
            {
                if (!createIfNotExists)
                {
                    throw new DirectoryNotFoundException(string.Format("No such directory! Path was {0}",
                        workingDirectory.FullName));
                }
                workingDirectory.Create();
                WorkingDirectory = workingDirectory;
            }

            Pattern = pattern;
        }

        #endregion

        #region Public Methods

        public bool HandleTrack(Track track)
        {
            if (track == null) throw new ArgumentNullException("track");

            if (track.DownloadUri == null)
            {
                Console.WriteLine("Could not fetch download url for track {0}. Skipping download...", track.Name);
                return false;
            }

            return HandleAlbum(new[] { track });
        }

        public bool HandleAlbum(IEnumerable<Track> album)
        {
            try
            {
                ChangePattern(album.Count());

                Dictionary<Track, FileInfo> downloadedTracks = new Dictionary<Track, FileInfo>();

                foreach (Track track in album)
                {
                    if (track.DownloadUri == null)
                    {
                        Console.WriteLine("Could not fetch download url for track {0}. Skipping download...", track.Name);
                        downloadedTracks.Add(track, null);
                    }
                    else
                    {
                        string downloadFilePath = DownloadTrack(track);

                        downloadedTracks.Add(track, new FileInfo(downloadFilePath));
                    }
                }

                AddId3Tags(downloadedTracks);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
            return true;
        }

        #endregion

        #region Private Methods

        private void ChangePattern(int trackCount)
        {
            int numOfLeadingZeros = (int)(Math.Floor(Math.Log10(trackCount) + 1)) - 1;
            if (numOfLeadingZeros == 0) return;

            string prefix = CalculatePrefix(numOfLeadingZeros);
            Pattern = Pattern.Replace("%tracknum", string.Format("{0}%tracknum", prefix));
        }

        private void AddId3Tags(Dictionary<Track, FileInfo> downloadedFilesInfo)
        {
            AttachedPictureFrame albumArt = GetAttachedPictureFrame(downloadedFilesInfo);
            uint totalTracksCount = (uint)downloadedFilesInfo.Count();

            foreach (KeyValuePair<Track, FileInfo> downloadedFile in downloadedFilesInfo.Where(x => x.Value != null))
            {
                File downloadedTrack = File.Create(downloadedFile.Value.FullName);
                downloadedTrack.Tag.Album = downloadedFile.Key.Album;
                downloadedTrack.Tag.AlbumArtists = new[] { downloadedFile.Key.Artist };
                downloadedTrack.Tag.Title = downloadedFile.Key.Name;
                downloadedTrack.Tag.Track = downloadedFile.Key.TrackNumber;
                downloadedTrack.Tag.TrackCount = totalTracksCount;
                downloadedTrack.Tag.Year = downloadedFile.Key.Year;
                downloadedTrack.Tag.Comment = string.Format("MusicServiceLeecher @ {0}", DateTime.Now.ToString("dd.MM.yy HH:mm:ss"));
                if (albumArt != null)
                {
                    downloadedTrack.Tag.Pictures = new IPicture[] { albumArt };
                }
                else if (downloadedFile.Key.AlbumArtUri != null)
                {
                    AttachedPictureFrame attachedPicFrame = new AttachedPictureFrame();
                    AddAlbumArtToAttachedPictureFrame(attachedPicFrame, downloadedFile.Key.AlbumArtUri);
                    downloadedTrack.Tag.Pictures = new IPicture[] { attachedPicFrame };
                }

                downloadedTrack.Save();
            }
        }

        private AttachedPictureFrame GetAttachedPictureFrame(Dictionary<Track, FileInfo> downloadedFilesInfo)
        {
            if (downloadedFilesInfo == null) throw new ArgumentNullException("downloadedFilesInfo");

            AttachedPictureFrame res = new AttachedPictureFrame();
            List<Uri> albumArtUris= downloadedFilesInfo.Keys.Select(x => x.AlbumArtUri).Distinct().ToList();
            if (albumArtUris.Count!=1)
            {
                return null;
            }

            AddAlbumArtToAttachedPictureFrame(res, albumArtUris.First());

            return res;
        }

        private void AddAlbumArtToAttachedPictureFrame(AttachedPictureFrame res, Uri albumArtUri)
        {
            using (TempFile tempFile = new TempFile())
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFile(albumArtUri, tempFile.Path);
                }
                res.TextEncoding = StringType.Latin1;
                res.MimeType = MediaTypeNames.Image.Jpeg;
                res.Type = PictureType.FrontCover;
                res.Data = ByteVector.FromPath(tempFile.Path);
            }
        }

        private string DownloadTrack(Track track)
        {
            using (WebClient webClient = new WebClient())
            {
                string filename = CreateFilePath(track);
                string shortfilename = filename.Substring(filename.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                Console.WriteLine("About to download file {0}", shortfilename);
                webClient.DownloadFile(track.DownloadUri, filename);
                Console.WriteLine("Completed download file {0}", shortfilename);
                return filename;
            }
        }

        private string CreateFilePath(Track track)
        {
            string filename = string.Format("{0}{1}", Pattern, DEFAULT_FILE_EXTENSION);
            filename = filename.Replace("%trackname", track.Name);
            filename = filename.Replace("%album", track.Album);
            filename = filename.Replace("%artist", track.Artist);
            filename = filename.Replace("%year", track.Year.ToString());

            int numOfLeadingZeros = (int)(Math.Floor(Math.Log10(track.TrackNumber) + 1)) - 1;
            if (numOfLeadingZeros == 0)
            {
                filename = filename.Replace("%tracknum", track.TrackNumber.ToString());
            }
            else
            {
                string prefix = CalculatePrefix(numOfLeadingZeros);

                filename = filename.Replace(string.Format("{0}%tracknum", prefix), track.TrackNumber.ToString());
            }

            return Path.Combine(WorkingDirectory.FullName, filename);
        }

        private string CalculatePrefix(int numOfLeadingZeros)
        {
            string prefix = string.Empty;

            for (int i = 0; i < numOfLeadingZeros; i++)
            {
                prefix += "0";
            }

            return prefix;
        }

        #endregion

    }
}
