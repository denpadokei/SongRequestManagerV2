using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Statics
{
    public class StringFormat
    {
        public static StringBuilder AddSongToQueueText { get; } = new StringBuilder("Request %songName% %songSubName%/%authorName% %Rating% (%version%) added to queue.");
        public static StringBuilder LookupSongDetail { get; } = new StringBuilder("%songName% %songSubName%/%authorName% %Rating% (%version%)");
        public static StringBuilder BsrSongDetail { get; } = new StringBuilder("%songName% %songSubName%/%authorName% %Rating% (%version%)");
        public static StringBuilder LinkSonglink { get; } = new StringBuilder("%songName% %songSubName%/%authorName% %Rating% (%version%) %BeatsaverLink%");
        public static StringBuilder NextSonglink { get; } = new StringBuilder("%songName% %songSubName%/%authorName% %Rating% (%version%) requested by %user% is next.");
        public static StringBuilder SongHintText { get; } = new StringBuilder("Requested by %user%%LF%Status: %Status%%Info%%LF%%LF%<size=60%>Request Time: %RequestTime%</size>");
        public static StringBuilder QueueTextFileFormat { get; } = new StringBuilder("%songName%%LF%");         // Don't forget to include %LF% for these. 
        public static StringBuilder QueueListRow2 { get; } = new StringBuilder("%authorName% (%id%) <color=white>%songlength%</color>");
        public static StringBuilder BanSongDetail { get; } = new StringBuilder("Blocking %songName%/%authorName% (%version%)");
        public static StringBuilder QueueListFormat { get; } = new StringBuilder("%songName% (%version%)");
        public static StringBuilder HistoryListFormat { get; } = new StringBuilder("%songName% (%version%)");
        public static StringBuilder AddSortOrder { get; } = new StringBuilder("-rating +id");
        public static StringBuilder LookupSortOrder { get; } = new StringBuilder("-rating +id");
        public static StringBuilder AddSongsSortOrder { get; } = new StringBuilder("-rating +id");
    }
}