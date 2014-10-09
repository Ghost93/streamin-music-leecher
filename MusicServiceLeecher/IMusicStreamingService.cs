using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MusicServiceLeecher
{
    public interface IMusicStreamingService
    {
        string Name { get; }
        Uri BaseUri { get; }
        bool DownloadSong(IWorkspace workspace, Uri songUri);
        bool DownloadAlbum(IWorkspace workspace, Uri albumUri);

    }
}
