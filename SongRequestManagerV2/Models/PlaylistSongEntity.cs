using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Models
{
    public class PlaylistSongEntity
    {
        public string songName { get; set; }
        public string levelAuthorName { get; set; }
        public string key { get; set; }
        public string hash { get; set; }
        public string levelid { get; set; }
        public DateTime dateAdded { get; set; }
    }
}
