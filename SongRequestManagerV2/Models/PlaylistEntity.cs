using System.Collections.Generic;

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
