using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicServiceLeecher
{
    public interface IMusicStreamingService
    {
        string Name { get; }
        Uri BaseUri { get; }
        bool DownloadSong(Workspace workspace, Uri songUri);
        bool DownloadAlbum(Workspace workspace, Uri albumUri);

    }
}
