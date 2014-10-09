using System;
using MusicServiceLeecher.MusicStreamingServices.BandcampMusicService;
using MusicServiceLeecher.MusicStreamingServices.SoundcloudMusicService;
using MusicServiceLeecher.Workspaces.FileSystemWorkspace;

namespace MusicServiceLeecher.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            IWorkspace ws = new FSWorkspace(string.Format("{0}\\Test\\", Environment.CurrentDirectory), true);
            IMusicStreamingService bandcampService = new BandcampService();
            bandcampService.DownloadAlbum(ws,
                new Uri("http://asafavidanmusic.bandcamp.com/album/avidan-in-a-box-live-acoustic-recordings"));

            bandcampService.DownloadSong(ws, new Uri("http://bigpauper.bandcamp.com/track/planet-telex-loop"));

            IMusicStreamingService soundcloudService = new SoundcloudService();
            soundcloudService.DownloadAlbum(ws, new Uri("https://soundcloud.com/ethan-crystal/sets/fifa-15-soundtrack"));
        }
    }
}
