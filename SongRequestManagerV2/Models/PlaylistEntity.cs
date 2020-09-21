using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Models
{
    public class PlaylistEntity
    {
        public string playlistTitle { get; set; } = "Request History";
        public string playlistAuthor { get; set; } = "SRM V2";
        public string image { get; set; } = "1";
        public List<PlaylistSongEntity> songs { get; set; } = new List<PlaylistSongEntity>();
    }
}
