using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicServiceLeecher.MusicStreamingServices;
using MusicServiceLeecher.Workspaces;

namespace MusicServiceLeecher.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            IWorkspace ws = new FileSystemWorkspace(string.Format("{0}\\Test\\", Environment.CurrentDirectory), true);
            //BandcampService bandcampService = new BandcampService();
            //bandcampService.DownloadAlbum(ws,
              //  new Uri("http://asafavidanmusic.bandcamp.com/album/avidan-in-a-box-live-acoustic-recordings"));

            //bandcampService.DownloadSong(ws, new Uri("http://bigpauper.bandcamp.com/track/planet-telex-loop"));

            SoundcloudService soundcloudService = new SoundcloudService();
            soundcloudService.DownloadAlbum(ws, new Uri("https://soundcloud.com/ethan-crystal/sets/fifa-15-soundtrack"));
        }
    }
}
