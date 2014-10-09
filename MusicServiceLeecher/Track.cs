using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MusicServiceLeecher
{
    public class Track
    {
        public Track(string artistName, string trackName, uint year,string album,uint trackNumber, Uri downloadUri)
        {
            Artist = artistName;
            Name = trackName;
            Year = year;
            Album = album;
            TrackNumber = trackNumber;
            DownloadUri = downloadUri;
            AlbumArtUri = null;
        }

        public Uri DownloadUri { get; private set; }

        public uint Year { get; set; }

        public string Name { get; private set; }

        public string Artist { get; private set; }

        public string Album { get; set; }

        public uint TrackNumber { get; set; }
        public Uri AlbumArtUri { get; set; }
    }
}
