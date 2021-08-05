using System.Text;

namespace SongRequestManagerV2.Statics
{
    public class StringFormat
    {
        public static StringBuilder AddSongToQueueText { get; } = new StringBuilder("Request %songName% %songSubName%/%levelAuthorName% %Rating% (%key%) added to queue.");
        public static StringBuilder LookupSongDetail { get; } = new StringBuilder("%songName% %songSubName%/%levelAuthorName% %Rating% (%key%)");
        public static StringBuilder BsrSongDetail { get; } = new StringBuilder("%songName% %songSubName%/%levelAuthorName% %Rating% (%key%)");
        public static StringBuilder LinkSonglink { get; } = new StringBuilder("%songName% %songSubName%/%levelAuthorName% %Rating% (%key%) %BeatsaverLink%");
        public static StringBuilder NextSonglink { get; } = new StringBuilder("%songName% %songSubName%/%levelAuthorName% %Rating% (%key%) requested by %user% is next.");
        public static StringBuilder SongHintText { get; } = new StringBuilder("Requested by %user%%LF%Status: %Status%%Info%%LF%%LF%<size=60%>Request Time: %RequestTime%</size>");
        public static StringBuilder QueueTextFileFormat { get; } = new StringBuilder("%songName%%LF%");         // Don't forget to include %LF% for these. 
        public static StringBuilder QueueListRow2 { get; } = new StringBuilder("%levelAuthorName% (%key%) <color=white>%songlength%</color>");
        public static StringBuilder BanSongDetail { get; } = new StringBuilder("Blocking %songName%/%levelAuthorName% (%key%)");
        public static StringBuilder QueueListFormat { get; } = new StringBuilder("%songName% (%key%)");
        public static StringBuilder HistoryListFormat { get; } = new StringBuilder("%songName% (%key%)");
        public static StringBuilder AddSortOrder { get; } = new StringBuilder("-rating +id");
        public static StringBuilder LookupSortOrder { get; } = new StringBuilder("-rating +id");
        public static StringBuilder AddSongsSortOrder { get; } = new StringBuilder("-rating +id");
    }
}