using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicServiceLeecher.Workspaces;

namespace MusicServiceLeecher
{
    public interface IMusicStreamingService
    {
        string Name { get; }
        Uri BaseUri { get; }
        bool DownloadSong(IWorkspace fileSystemWorkspace, Uri songUri);
        bool DownloadAlbum(IWorkspace fileSystemWorkspace, Uri albumUri);

    }
}
