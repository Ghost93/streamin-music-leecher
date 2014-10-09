using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MusicServiceLeecher
{
    public interface IWorkspace
    {
        bool HandleTrack(Track track);
        bool HandleAlbum(IEnumerable<Track> album);
    }
}
