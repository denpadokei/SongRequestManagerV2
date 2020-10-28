//using ChatCore.SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
//using StreamCore.Twitch;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using ChatCore.Utilities;
using Zenject;
using SongRequestManagerV2.Bot;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
using IPA.Loader;

namespace SongRequestManagerV2
{
    public partial class RequestBot// : MonoBehaviour
    {
        // BUG: This one needs to be cleaned up a lot imo
        // BUG: This file needs to be split up a little, but not just yet... Its easier for me to move around in one massive file, since I can see the whole thing at once. 

        public static StringBuilder AddSongToQueueText { get; } = new StringBuilder("Request %songName% %songSubName%/%authorName% %Rating% (%version%) added to queue.");
        public static StringBuilder LookupSongDetail { get; } = new StringBuilder("%songName% %songSubName%/%authorName% %Rating% (%version%)");
        public static StringBuilder BsrSongDetail { get; } = new StringBuilder("%songName% %songSubName%/%authorName% %Rating% (%version%)");
        public static StringBuilder LinkSonglink { get; } = new StringBuilder("%songName% %songSubName%/%authorName% %Rating% (%version%) %BeatsaverLink%");
        public static StringBuilder NextSonglink { get; } = new StringBuilder("%songName% %songSubName%/%authorName% %Rating% (%version%) requested by %user% is next.");
        public static StringBuilder SongHintText { get; } = new StringBuilder("Requested by %user%%LF%Status: %Status%%Info%%LF%%LF%<size=60%>Request Time: %RequestTime%</size>");
        public static StringBuilder QueueTextFileFormat { get; } = new StringBuilder("%songName%%LF%");         // Don't forget to include %LF% for these. 

        public static  StringBuilder QueueListRow2 { get; } = new StringBuilder("%authorName% (%id%) <color=white>%songlength%</color>");

        public static StringBuilder BanSongDetail { get; } = new StringBuilder("Blocking %songName%/%authorName% (%version%)");

        public static StringBuilder QueueListFormat { get; } = new StringBuilder("%songName% (%version%)");
        public static StringBuilder HistoryListFormat { get; } = new StringBuilder("%songName% (%version%)");

        public static StringBuilder AddSortOrder { get; } = new StringBuilder("-rating +id");
        public static StringBuilder LookupSortOrder { get; } = new StringBuilder("-rating +id");
        public static StringBuilder AddSongsSortOrder { get; } = new StringBuilder("-rating +id");

        

        #region Utility functions

        public string Variable(ParseState state) // Basically show the value of a variable without parsing
        {
            QueueChatMessage(state._botcmd.userParameter.ToString());
            return "";
        }

        public static int MaximumTwitchMessageLength
        {
            get
            {
                return 498 - RequestBotConfig.Instance.BotPrefix.Length;
            }
        }

        public string ChatMessage(ParseState state)
        {
            var dt = _textFactory.Create().AddUser(state._user);
            try {
                dt.AddSong((RequestManager.HistorySongs[0] as SongRequest)._song); // Exposing the current song 
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }

            dt.QueueMessage(state._parameter);
            return success;
        }

        public void RunScript(IChatUser requestor, string request)
        {
            listcollection.runscript(request);
        }

        public static TimeSpan GetFileAgeDifference(string filename)
        {
            DateTime lastModified = System.IO.File.GetLastWriteTime(filename);
            return DateTime.Now - lastModified;
        }
        #endregion

        #region Filter support functions

        internal bool DoesContainTerms(string request, ref string[] terms)
        {
            if (request == "") return false;
            request = request.ToLower();

            foreach (string term in terms)
                foreach (string word in request.Split(' '))
                    if (word.Length > 2 && term.ToLower().Contains(word)) return true;

            return false;
        }


        bool isNotModerator(IChatUser requestor, string message = "")
        {
            if (requestor.IsBroadcaster || requestor.IsModerator) return false;
            if (message != "") QueueChatMessage($"{message} is moderator only.");
            return true;
        }

        internal bool filtersong(JSONObject song)
        {
            string songid = song["id"].Value;
            if (IsInQueue(songid)) return true;
            if (listcollection.contains(ref banlist, songid)) return true;
            if (listcollection.contains(ref duplicatelist, songid)) return true;
            return false;
        }

        // Returns error text if filter triggers, or "" otherwise, "fast" version returns X if filter triggers

        

        internal string SongSearchFilter(JSONObject song, bool fast = false, SongFilter filter = SongFilter.All) // BUG: This could be nicer
        {
            string songid = song["id"].Value;
            if (filter.HasFlag(SongFilter.Queue) && RequestManager.RequestSongs.OfType<SongRequest>().Any(req => req._song["version"] == song["version"])) return fast ? "X" : $"Request {song["songName"].Value} by {song["authorName"].Value} already exists in queue!";

            if (filter.HasFlag(SongFilter.Blacklist) && listcollection.contains(ref banlist, songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is banned!";

            if (filter.HasFlag(SongFilter.Mapper) && mapperfiltered(song, _mapperWhitelist)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} does not have a permitted mapper!";

            if (filter.HasFlag(SongFilter.Duplicate) && listcollection.contains(ref duplicatelist, songid)) return fast ? "X" : $"{song["songName"].Value} by  {song["authorName"].Value} already requested this session!";

            if (listcollection.contains(ref _whitelist, songid)) return "";

            if (filter.HasFlag(SongFilter.Duration) && song["songduration"].AsFloat > RequestBotConfig.Instance.MaximumSongLength * 60) return fast ? "X" : $"{song["songName"].Value} ({song["songlength"].Value}) by {song["authorName"].Value} ({song["version"].Value}) is too long!";

            if (filter.HasFlag(SongFilter.NJS) && song["njs"].AsInt < RequestBotConfig.Instance.MinimumNJS) return fast ? "X" : $"{song["songName"].Value} ({song["songlength"].Value}) by {song["authorName"].Value} ({song["version"].Value}) NJS ({song["njs"].Value}) is too low!";

            if (filter.HasFlag(SongFilter.Remap) && songremap.ContainsKey(songid)) return fast ? "X" : $"no permitted results found!";

            if (filter.HasFlag(SongFilter.Rating) && song["rating"].AsFloat < RequestBotConfig.Instance.LowestAllowedRating && song["rating"] != 0) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} is below {RequestBotConfig.Instance.LowestAllowedRating}% rating!";

            return "";
        }

        // checks if request is in the RequestManager.RequestSongs - needs to improve interface
        internal string IsRequestInQueue(string request, bool fast = false)
        {
            string matchby = "";
            if (_beatSaverRegex.IsMatch(request)) matchby = "version";
            else if (_digitRegex.IsMatch(request)) matchby = "id";
            if (matchby == "") return fast ? "X" : $"Invalid song id {request} used in RequestInQueue check";

            foreach (SongRequest req in RequestManager.RequestSongs) {
                var song = req._song;
                if (song[matchby].Value == request) return fast ? "X" : $"Request {song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) already exists in queue!";
            }
            return ""; // Empty string: The request is not in the RequestManager.RequestSongs
        }

        bool IsInQueue(string request) // unhappy about naming here
        {
            return !(IsRequestInQueue(request) == "");
        }

        internal string ClearDuplicateList(ParseState state)
        {
            if (!state._botcmd.Flags.HasFlag(CmdFlags.SilentResult)) QueueChatMessage("Session duplicate list is now clear.");
            listcollection.ClearList(duplicatelist);
            return success;
        }
        #endregion

        #region Ban/Unban Song
        //public void Ban(IChatUser requestor, string request)
        //{
        //    Ban(requestor, request, false);
        //}

        internal async Task Ban(ParseState state)
        {
            var id = GetBeatSaverId(state._parameter.ToLower());

            if (listcollection.contains(ref banlist, id)) {
                QueueChatMessage($"{id} is already on the ban list.");
                return;
            }

            if (!MapDatabase.MapLibrary.TryGetValue(id, out SongMap song)) {
                JSONNode result = null;

                if (!RequestBotConfig.Instance.OfflineMode) {
                    var requestUrl = $"https://beatsaver.com/api/maps/detail/{id}";
                    var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                    if (resp.IsSuccessStatusCode) {
                        result = resp.ConvertToJsonNode();
                    }
                    else {
                        Plugin.Log($"Ban: Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    }
                }

                if (result != null) song = new SongMap(result.AsObject);
            }

            listcollection.add(banlist, id);

            if (song == null) {
                QueueChatMessage($"{id} is now on the ban list.");
            }
            else {
                state.msg(_textFactory.Create().AddSong(ref song.song).Parse(BanSongDetail), ", ");
            }
        }

        //public void Ban(IChatUser requestor, string request, bool silence)
        //{
        //    if (isNotModerator(requestor)) return;

        //    var songId = GetBeatSaverId(request);
        //    if (songId == "" && !silence)
        //    {
        //        QueueChatMessage($"usage: !block <songid>, omit <>'s.");
        //        return;
        //    }

        //    if (listcollection.contains(ref banlist,songId) && !silence)
        //    {
        //        QueueChatMessage($"{request} is already on the ban list.");
        //    }
        //    else
        //    {

        //        listcollection.add(banlist, songId);
        //        QueueChatMessage($"{request} is now on the ban list.");

        //    }
        //}

        internal void Unban(IChatUser requestor, string request)
        {
            var unbanvalue = GetBeatSaverId(request);

            if (listcollection.contains(ref banlist, unbanvalue)) {
                QueueChatMessage($"Removed {request} from the ban list.");
                listcollection.remove(banlist, unbanvalue);
            }
            else {
                QueueChatMessage($"{request} is not on the ban list.");
            }
        }
        #endregion

        #region Deck Commands
        internal string restoredeck(ParseState state)
        {
            return Readdeck(_stateFactory.Create().Setup(state, "savedqueue"));
        }

        internal void Writedeck(IChatUser requestor, string request)
        {
            try {
                int count = 0;
                if (RequestManager.RequestSongs.Count == 0) {
                    QueueChatMessage("Queue is empty  .");
                    return;
                }

                string queuefile = Path.Combine(Plugin.DataPath, request + ".deck");
                var sb = new StringBuilder();

                foreach (SongRequest req in RequestManager.RequestSongs.ToArray()) {
                    var song = req._song;
                    if (count > 0) sb.Append(",");
                    sb.Append(song["id"].Value);
                    count++;
                }
                File.WriteAllText(queuefile, sb.ToString());
                if (request != "savedqueue") QueueChatMessage($"wrote {count} entries to {request}");
            }
            catch {
                QueueChatMessage("Was unable to write {queuefile}.");
            }
        }

        internal string Readdeck(ParseState state)
        {
            try {
                string queuefile = Path.Combine(Plugin.DataPath, state._parameter + ".deck");
                if (!File.Exists(queuefile)) {
                    using (File.Create(queuefile)) { };
                }

                string fileContent = File.ReadAllText(queuefile);
                string[] integerStrings = fileContent.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                for (int n = 0; n < integerStrings.Length; n++) {
                    if (IsInQueue(integerStrings[n])) continue;

                    ParseState newstate = _stateFactory.Create().Setup(state); // Must use copies here, since these are all threads
                    newstate._parameter = integerStrings[n];
                    ProcessSongRequest(newstate);
                }
            }
            catch {
                QueueChatMessage("Unable to read deck {request}.");
            }

            return success;
        }
        #endregion

        #region Dequeue Song
        internal string DequeueSong(ParseState state)
        {

            var songId = GetBeatSaverId(state._parameter);
            for (int i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                bool dequeueSong = false;
                var song = (RequestManager.RequestSongs[i] as SongRequest)._song;

                if (songId == "") {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["version"].Value, (RequestManager.RequestSongs[i] as SongRequest)._requestor.UserName };

                    if (DoesContainTerms(state._parameter, ref terms))
                        dequeueSong = true;
                }
                else {
                    if (song["id"].Value == songId)
                        dequeueSong = true;
                }

                if (dequeueSong) {
                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");
                    Skip(i);
                    return success;
                }
            }
            return $"{state._parameter} was not found in the queue.";
        }
        #endregion


        // BUG: Will use a new interface to the list manager
        internal void MapperAllowList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            mapperwhitelist = listcollection.OpenList(key); // BUG: this is still not the final interface
            QueueChatMessage($"Mapper whitelist set to {request}.");
        }

        internal void MapperBanList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            mapperBanlist = listcollection.OpenList(key);
            //QueueChatMessage($"Mapper ban list set to {request}.");
        }

        internal void WhiteList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            Whitelist = listcollection.OpenList(key);
        }

        internal void BlockedUserList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            BlockedUser = listcollection.OpenList(key);
        }

        // Not super efficient, but what can you do
        internal bool mapperfiltered(JSONObject song, bool white)
        {
            string normalizedauthor = song["metadata"]["levelAuthorName"].Value.ToLower();
            if (white && mapperwhitelist.list.Count > 0) {
                foreach (var mapper in mapperwhitelist.list) {
                    if (normalizedauthor.Contains(mapper)) return false;
                }
                return true;
            }

            foreach (var mapper in mapperBanlist.list) {
                if (normalizedauthor.Contains(mapper)) return true;
            }

            return false;
        }

        // return a songrequest match in a SongRequest list. Good for scanning Queue or History
        SongRequest FindMatch(IEnumerable<SongRequest> queue, string request, QueueLongMessage qm)
        {
            var songId = GetBeatSaverId(request);

            SongRequest result = null;

            string lastuser = "";
            foreach (var entry in queue) {
                var song = entry._song;

                if (songId == "") {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"].Value, song["version"].Value, entry._requestor.UserName };

                    if (DoesContainTerms(request, ref terms)) {
                        result = entry;

                        if (lastuser != result._requestor.UserName) qm.Add($"{result._requestor.UserName}: ");
                        qm.Add($"{result._song["songName"].Value} ({result._song["version"].Value})", ",");
                        lastuser = result._requestor.UserName;
                    }
                }
                else {
                    if (song["id"].Value == songId) {
                        result = entry;
                        qm.Add($"{result._requestor.UserName}: {result._song["songName"].Value} ({result._song["version"].Value})");
                        return entry;
                    }
                }
            }
            return result;
        }

        public string ClearEvents(ParseState state)
        {
            BotEvent.Clear();
            return success;
        }

        public string Every(ParseState state)
        {
            float period;

            string[] parts = state._parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out period)) return state.error($"You must specify a time in minutes after {state._command}.");
            if (period < 1) return state.error($"You must specify a period of at least 1 minute");
            new BotEvent(TimeSpan.FromMinutes(period), parts[1], true);
            return success;
        }

        public string EventIn(ParseState state)
        {
            float period;
            string[] parts = state._parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out period)) return state.error($"You must specify a time in minutes after {state._command}.");
            if (period < 0) return state.error($"You must specify a period of at least 0 minutes");
            new BotEvent(TimeSpan.FromMinutes(period), parts[1], false);
            return success;
        }
        public string Who(ParseState state)
        {

            var qm = this._messageFactroy.Create();
            SongRequest result = null;
            result = FindMatch(RequestManager.RequestSongs.OfType<SongRequest>(), state._parameter, qm);
            if (result == null) result = FindMatch(RequestManager.HistorySongs.OfType<SongRequest>(), state._parameter, qm);

            //if (result != null) QueueChatMessage($"{result.song["songName"].Value} requested by {result.requestor.displayName}.");
            if (result != null) qm.end("...");
            return "";
        }

        public string SongMsg(ParseState state)
        {
            string[] parts = state._parameter.Split(new char[] { ' ', ',' }, 2);
            var songId = GetBeatSaverId(parts[0]);
            if (songId == "") return state.helptext(true);

            foreach (var entry in RequestManager.RequestSongs.OfType<SongRequest>()) {
                var song = entry._song;

                if (song["id"].Value == songId) {
                    entry._requestInfo = "!" + parts[1];
                    QueueChatMessage($"{song["songName"].Value} : {parts[1]}");
                    return success;
                }
            }
            QueueChatMessage($"Unable to find {songId}");
            return success;
        }

        internal IEnumerator SetBombState(ParseState state)
        {
            state._parameter = state._parameter.ToLower();

            if (state._parameter == "on") state._parameter = "enable";
            if (state._parameter == "off") state._parameter = "disable";

            if (state._parameter != "enable" && state._parameter != "disable") {
                state.msg(state._botcmd.ShortHelp);
                yield break;
            }

            //System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state.parameter}"));

            System.Diagnostics.Process.Start($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state._parameter}");

            if (PluginManager.GetPlugin("WobbleSaber") != null) {
                string wobblestate = "off";
                if (state._parameter == "enable") wobblestate = "on";
                SendChatMessage($"!wadmin toggle {wobblestate} ");
            }

            state.msg($"The !bomb command is now {state._parameter}d.");

            yield break;
        }


        internal async Task addsongsFromnewest(ParseState state)
        {
            int totalSongs = 0;

            string requestUrl = "https://beatsaver.com/api/maps/latest";

            //if (RequestBotConfig.Instance.OfflineMode) return;

            int offset = 0;

            listcollection.ClearList("latest.deck");

            //state.msg($"Flags: {state.flags}");

            while (offset < RequestBotConfig.Instance.MaxiumScanRange) // MaxiumAddScanRange
            {
                var resp = await WebClient.GetAsync($"{requestUrl}/{offset}", System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode) {
                    var result = resp.ConvertToJsonNode();
                    if (result["docs"].IsArray && result["totalDocs"].AsInt == 0) {
                        return;
                    }

                    if (result["docs"].IsArray) {
                        foreach (JSONObject entry in result["docs"]) {
                            JSONObject song = entry;
                            new SongMap(song);

                            if (mapperfiltered(song, true)) continue; // This forces the mapper filter
                            if (filtersong(song)) continue;

                            if (state._flags.HasFlag(CmdFlags.Local)) QueueSong(state, song);
                            listcollection.add("latest.deck", song["id"].Value);
                            totalSongs++;
                        }
                    }

                }
                else {
                    Plugin.Log($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    return;
                }

                offset += 1; // Magic beatsaver.com skip constant.
            }

            if (totalSongs == 0) {
                //QueueChatMessage($"No new songs found.");
            }
            else {
#if UNRELEASED
                COMMAND.Parse(TwitchWebSocketClient.OurIChatUser, "!deck latest",state.flags);
#endif

                if (state._flags.HasFlag(CmdFlags.Local)) {
                    UpdateRequestUI();
                    RefreshSongQuere();
                    _refreshQueue = true;
                }
            }
        }

        internal async Task makelistfromsearch(ParseState state)
        {
            int totalSongs = 0;

            var id = GetBeatSaverId(state._parameter);

            string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{normalize.RemoveSymbols(ref state._parameter, normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text";

            if (RequestBotConfig.Instance.OfflineMode) return;

            int offset = 0;

            listcollection.ClearList("search.deck");

            //state.msg($"Flags: {state.flags}");

            while (offset < RequestBotConfig.Instance.MaxiumScanRange) // MaxiumAddScanRange
            {
                var resp = await WebClient.GetAsync($"{requestUrl}/{offset}?q={state._parameter}", System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode) {
                    var result = resp.ConvertToJsonNode();
                    if (result["docs"].IsArray && result["totalDocs"].AsInt == 0) {
                        return;
                    }

                    if (result["docs"].IsArray) {
                        foreach (JSONObject entry in result["docs"]) {
                            JSONObject song = entry;
                            new SongMap(song);

                            if (mapperfiltered(song, true)) continue; // This forces the mapper filter
                            if (filtersong(song)) continue;

                            if (state._flags.HasFlag(CmdFlags.Local)) QueueSong(state, song);
                            listcollection.add("search.deck", song["id"].Value);
                            totalSongs++;
                        }
                    }
                }
                else {
                    Plugin.Log($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    return;
                }
                offset += 1;
            }

            if (totalSongs == 0) {
                //QueueChatMessage($"No new songs found.");
            }
            else {
#if UNRELEASED
                COMMAND.Parse(TwitchWebSocketClient.OurIChatUser, "!deck search", state.flags);
#endif

                if (state._flags.HasFlag(CmdFlags.Local)) {
                    UpdateRequestUI();
                    RefreshSongQuere();
                    _refreshQueue = true;
                }
            }
        }

        // General search version
        internal async Task addsongs(ParseState state)
        {

            var id = GetBeatSaverId(state._parameter);
            string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{normalize.RemoveSymbols(ref state._parameter, normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text/0?q={state._request}";

            string errorMessage = "";

            if (RequestBotConfig.Instance.OfflineMode) requestUrl = "";

            JSONNode result = null;

            if (!RequestBotConfig.Instance.OfflineMode) {
                var resp = await WebClient.GetAsync($"{requestUrl}/{normalize.NormalizeBeatSaverString(state._parameter)}", System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode) {
                    result = resp.ConvertToJsonNode();

                }
                else {
                    Plugin.Log($"Error {resp.ReasonPhrase} occured when trying to request song {state._parameter}!");
                    errorMessage = $"Invalid BeatSaver ID \"{state._parameter}\" specified.";
                }
            }

            SongFilter filter = SongFilter.All;
            if (state._flags.HasFlag(CmdFlags.NoFilter)) filter = SongFilter.Queue;
            List<JSONObject> songs = GetSongListFromResults(result, state._parameter, ref errorMessage, filter, state._sort != "" ? state._sort : LookupSortOrder.ToString(), -1);

            foreach (JSONObject entry in songs) {
                QueueSong(state, entry);
            }

            UpdateRequestUI();
            RefreshSongQuere();
            _refreshQueue = true;
        }

        public void QueueSong(ParseState state, JSONObject song)
        {
            var req = _songRequestFactory.Create();
            req.Init(song, state._user, DateTime.UtcNow, RequestStatus.SongSearch, "search result");

            if ((state._flags.HasFlag(CmdFlags.MoveToTop)))
                RequestManager.RequestSongs.Insert(0, req);
            else
                RequestManager.RequestSongs.Add(req);
        }

        #region Move Request To Top/Bottom

        internal void MoveRequestToTop(IChatUser requestor, string request)
        {
            MoveRequestPositionInQueue(requestor, request, true);
        }

        internal void MoveRequestToBottom(IChatUser requestor, string request)
        {
            MoveRequestPositionInQueue(requestor, request, false);
        }

        internal void MoveRequestPositionInQueue(IChatUser requestor, string request, bool top)
        {

            string moveId = GetBeatSaverId(request);
            for (int i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                var req = RequestManager.RequestSongs.ElementAt(i) as SongRequest;
                var song = req._song;

                bool moveRequest = false;
                if (moveId == "") {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"].Value, song["version"].Value, (RequestManager.RequestSongs[i] as SongRequest)._requestor.UserName };
                    if (DoesContainTerms(request, ref terms))
                        moveRequest = true;
                }
                else {
                    if (song["id"].Value == moveId)
                        moveRequest = true;
                }

                if (moveRequest) {
                    // Remove the request from the queue
                    RequestManager.RequestSongs.RemoveAt(i);

                    // Then readd it at the appropriate position
                    if (top)
                        RequestManager.RequestSongs.Insert(0, req);
                    else
                        RequestManager.RequestSongs.Add(req);

                    // Write the modified request queue to file
                    _requestManager.WriteRequest();

                    // Refresh the queue ui
                    RefreshSongQuere();
                    _refreshQueue = true;

                    // And write a summary to file
                    WriteQueueSummaryToFile();

                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) {(top ? "promoted" : "demoted")}.");
                    return;
                }
            }
            QueueChatMessage($"{request} was not found in the queue.");
        }
        #endregion

        

        #region Queue Related

        // This function existing to unify the queue message strings, and to allow user configurable QueueMessages in the future
        public static string QueueMessage(bool QueueState)
        {
            return QueueState ? "Queue is open" : "Queue is closed";
        }
        public string OpenQueue(ParseState state)
        {
            ToggleQueue(state._user, state._parameter, true);
            return success;
        }

        public string CloseQueue(ParseState state)
        {
            ToggleQueue(state._user, state._parameter, false);
            return success;
        }

        internal void ToggleQueue(IChatUser requestor, string request, bool state)
        {
            RequestBotConfig.Instance.RequestQueueOpen = state;
            RequestBotConfig.Instance.Save();

            QueueChatMessage(state ? "Queue is now open." : "Queue is now closed.");
            WriteQueueStatusToFile(QueueMessage(state));
            RefreshSongQuere();
            _refreshQueue = true;
        }
        internal void WriteQueueSummaryToFile()
        {

            if (!RequestBotConfig.Instance.UpdateQueueStatusFiles) return;

            try {
                string statusfile = Path.Combine(Plugin.DataPath, "queuelist.txt");
                var queuesummary = new StringBuilder();
                int count = 0;

                foreach (SongRequest req in RequestManager.RequestSongs.ToArray()) {
                    var song = req._song;
                    queuesummary.Append(_textFactory.Create().AddSong(song).Parse(QueueTextFileFormat));  // Format of Queue is now user configurable

                    if (++count > RequestBotConfig.Instance.MaximumQueueTextEntries) {
                        queuesummary.Append("...\n");
                        break;
                    }
                }
                File.WriteAllText(statusfile, count > 0 ? queuesummary.ToString() : "Queue is empty.");
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }
        }

        public static void WriteQueueStatusToFile(string status)
        {
            try {
                string statusfile = Path.Combine(Plugin.DataPath, "queuestatus.txt");
                File.WriteAllText(statusfile, status);
            }

            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }
        }

        public static void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1) {
                n--;
                int k = generator.Next(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        internal string QueueLottery(ParseState state)
        {
            Int32.TryParse(state._parameter, out int entrycount);

            Shuffle(RequestManager.RequestSongs);

            var list = RequestManager.RequestSongs.OfType<SongRequest>().ToList();
            for (int i = entrycount; i < list.Count; i++) {
                try {
                    if (RequestTracker.ContainsKey(list[i]._requestor.Id)) RequestTracker[list[i]._requestor.Id].numRequests--;
                    listcollection.remove(duplicatelist, list[i]._song["id"]);
                }
                catch { }
            }

            if (entrycount > 0) {
                try {
                    Writedeck(state._user, "prelotto");
                    RequestManager.RequestSongs.RemoveRange(entrycount, RequestManager.RequestSongs.Count - entrycount);
                }
                catch { }
            }

            _requestManager.WriteRequest();

            // Notify the chat that the queue was cleared
            QueueChatMessage($"Queue lottery complete!");

            ToggleQueue(state._user, state._parameter, false); // Close the queue.
            // Reload the queue
            UpdateRequestUI();
            RefreshSongQuere();
            _refreshQueue = true;
            return success;
        }

        internal void Clearqueue(IChatUser requestor, string request)
        {
            // Write our current queue to file so we can restore it if needed
            Writedeck(requestor, "justcleared");

            // Cycle through each song in the final request queue, adding them to the song history

            while (RequestManager.RequestSongs.Count > 0) DequeueRequest(0, false); // More correct now, previous version did not keep track of user requests 

            _requestManager.WriteRequest();

            // Update the request button ui accordingly
            UpdateRequestUI();

            // Notify the chat that the queue was cleared
            QueueChatMessage($"Queue is now empty.");

            // Reload the queue
            RefreshSongQuere();
            _refreshQueue = true;
        }

        #endregion

        #region Unmap/Remap Commands
        internal void Remap(IChatUser requestor, string request)
        {
            string[] parts = request.Split(',', ' ');

            if (parts.Length < 2) {
                QueueChatMessage("usage: !remap <songid>,<songid>, omit the <>'s");
                return;
            }

            if (songremap.ContainsKey(parts[0])) songremap.Remove(parts[0]);
            songremap.Add(parts[0], parts[1]);
            QueueChatMessage($"Song {parts[0]} remapped to {parts[1]}");
            WriteRemapList();
        }

        internal void Unmap(IChatUser requestor, string request)
        {

            if (songremap.ContainsKey(request)) {
                QueueChatMessage($"Remap entry {request} removed.");
                songremap.Remove(request);
            }
            WriteRemapList();
        }

        internal void WriteRemapList()
        {

            // BUG: Its more efficient to write it in one call

            try {
                string remapfile = Path.Combine(Plugin.DataPath, "remap.list");

                var sb = new StringBuilder();

                foreach (var entry in songremap) {
                    sb.Append($"{entry.Key},{entry.Value}\n");
                }
                File.WriteAllText(remapfile, sb.ToString());
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }
        }

        internal void ReadRemapList()
        {
            string remapfile = Path.Combine(Plugin.DataPath, "remap.list");

            if (!File.Exists(remapfile)) {
                using (var file = File.Create(remapfile)) { };
            }

            try {
                var fileContent = File.ReadAllText(remapfile);

                var maps = fileContent.Split('\r', '\n');
                foreach (var map in maps) {
                    var parts = map.Split(',', ' ');
                    if (parts.Length > 1) songremap.Add(parts[0], parts[1]);
                }
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }
        }
        #endregion

        #region Wrong Song
        internal void WrongSong(IChatUser requestor, string request)
        {
            // Note: Scanning backwards to remove LastIn, for loop is best known way.
            for (int i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                var song = (RequestManager.RequestSongs[i] as SongRequest)._song;
                if ((RequestManager.RequestSongs[i] as SongRequest)._requestor.Id == requestor.Id) {
                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");

                    listcollection.remove(duplicatelist, song["id"].Value);
                    Skip(i, RequestStatus.Wrongsong);
                    return;
                }
            }
            QueueChatMessage($"You have no requests in the queue.");
        }
        #endregion

        // BUG: This requires a switch, or should be disabled for those who don't allow links
        public string ShowSongLink(ParseState state)
        {
            try  // We're accessing an element across threads, and currentsong doesn't need to be defined
            {
                var song = (RequestManager.RequestSongs[0] as SongRequest)._song;
                if (!song.IsNull) _textFactory.Create().AddSong(ref song).QueueMessage(LinkSonglink.ToString());
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }

            return success;
        }

        public string queueduration()
        {
            int total = 0;
            try {
                foreach (var songrequest in RequestManager.RequestSongs.OfType<SongRequest>()) {
                    total += songrequest._song["songduration"];
                }
            }
            catch {


            }

            return $"{total / 60}:{ total % 60:00}";
        }

        internal string QueueStatus(ParseState state)
        {
            string queuestate = RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open. " : "Queue is closed. ";
            QueueChatMessage($"{queuestate} There are {RequestManager.RequestSongs.Count} maps ({queueduration()}) in the queue.");
            return success;
        }

        public string GetStarRating(ref JSONObject song, bool mode = true)
        {
            if (!mode) return "";

            string stars = "******";
            float rating = song["rating"].AsFloat;
            if (rating < 0 || rating > 100) rating = 0;
            string starrating = stars.Substring(0, (int)(rating / 17)); // 17 is used to produce a 5 star rating from 80ish to 100.
            return starrating;
        }

        public string GetRating(ref JSONObject song, bool mode = true)
        {
            if (!mode) return "";

            string rating = song["rating"].AsInt.ToString();
            if (rating == "0") return "";
            return rating + '%';
        }

        #region DynamicText class and support functions.

        

        #endregion
    }
}