using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

#if OLDVERSION
using TMPro;
#endif

using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using SongCore;
using IPA.Utilities;
using SongRequestManagerV2.UI;
using BeatSaberMarkupLanguage;
using JSONObject = ChatCore.SimpleJSON.JSONObject;
using System.Threading.Tasks;
using System.IO.Compression;
using ChatCore.Models.Twitch;
using ChatCore.Interfaces;
using ChatCore.Services;
using ChatCore;
using ChatCore.Services.Twitch;
using System.Runtime.CompilerServices;
using System.Reflection;
using ChatCore.SimpleJSON;
using SongRequestManagerV2.Extentions;
using SongRequestManagerV2.Utils;
using System.Text;
using System.Text.RegularExpressions;
using HMUI;
using SongRequestManagerV2.Views;
using Zenject;
using UnityEngine.PlayerLoop;
using System.Threading;

namespace SongRequestManagerV2
{
    public partial class RequestBot : MonoBehaviour
    {
        [Flags]
        public enum RequestStatus
        {
            Invalid,
            Queued,
            Blacklisted,
            Skipped,
            Played,
            Wrongsong,
            SongSearch,
        }

        public static RequestBot Instance { get; private set; }
        //private RequestBot()
        //{

        //}

        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue { get; } = new ConcurrentQueue<RequestInfo>();
        public static Dictionary<string, RequestUserTracker> RequestTracker = new Dictionary<string, RequestUserTracker>();
        //private ChatServiceMultiplexer _chatService { get; set; }

        //SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        //synthesizer.Volume = 100;  // 0...100
        //    synthesizer.Rate = -2;     // -10...10
        public static bool _refreshQueue = false;

        private static Queue<string> _botMessageQueue = new Queue<string>();

        bool _mapperWhitelist = false; // BUG: Need to clean these up a bit.
        bool _configChanged = false;

        private static System.Random generator = new System.Random(); // BUG: Should at least seed from unity?

        public static List<JSONObject> played = new List<JSONObject>(); // Played list

        private static StringListManager mapperwhitelist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static StringListManager mapperBanlist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static StringListManager Whitelist = new StringListManager();
        private static StringListManager BlockedUser = new StringListManager();

        private static string duplicatelist = "duplicate.list"; // BUG: Name of the list, needs to use a different interface for this.
        private static string banlist = "banlist.unique"; // BUG: Name of the list, needs to use a different interface for this.
        private static string _whitelist = "whitelist.unique"; // BUG: Name of the list, needs to use a different interface for this.
        private static string _blockeduser = "blockeduser.unique";

        private static Dictionary<string, string> songremap = new Dictionary<string, string>();
        public static Dictionary<string, string> deck = new Dictionary<string, string>(); // deck name/content

        [Inject]
        public SRMButton button { get; set; }
        [Inject]
        public LevelFilteringNavigationController _levelFilteringNavigationController;
        public static string playedfilename = "";

        public event Action RecevieRequest;
        public event Action DismissRequest;

        private static readonly object _lockObject = new object();

        [Inject]
        public async void OnLoad()
        {
            Plugin.Log("Awake start!");
            
#if UNRELEASED
            var startingmem = GC.GetTotalMemory(true);

            //var folder = Path.Combine(Environment.CurrentDirectory, "userdata","streamcore");

           //List<FileInfo> files = new List<FileInfo>();  // List that will hold the files and subfiles in path
            //List<DirectoryInfo> folders = new List<DirectoryInfo>(); // List that hold direcotries that cannot be accessed

            //DirectoryInfo di = new DirectoryInfo(folder);

            //Dictionary<string, string> remap = new Dictionary<string, string>();
        
            //foreach (var entry in listcollection.OpenList("all.list").list) 
            //    {
            //    //Instance.QueueChatMessage($"Map {entry}");

            //    string[] remapparts = entry.Split('-');
            //    if (remapparts.Length == 2)
            //    {
            //        int o;
            //        if (Int32.TryParse(remapparts[1], out o))
            //        {
            //            try
            //            {
            //                remap.Add(remapparts[0], o.ToString("x"));
            //            }
            //            catch
            //            { }
            //            //Instance.QueueChatMessage($"Map {remapparts[0]} : {o.ToString("x")}");
            //        }
            //    }
            //}

            //Instance.QueueChatMessage($"Scanning lists");

            //FullDirList(di, "*.deck");
            //void FullDirList(DirectoryInfo dir, string searchPattern)
            //{
            //    try
            //    {
            //        foreach (FileInfo f in dir.GetFiles(searchPattern))
            //        {
            //            var List = listcollection.OpenList(f.UserName).list;
            //            for (int i=0;i<List.Count;i++)
            //                {
            //                if (remap.ContainsKey(List[i]))
            //                {
            //                    //Instance.QueueChatMessage($"{List[i]} : {remap[List[i]]}");
            //                    List[i] = remap[List[i]];
            //                }    
            //                }
            //            listcollection.OpenList(f.UserName).Writefile(f.UserName);
            //        }
            //    }
            //    catch
            //    {
            //        Console.WriteLine("Directory {0}  \n could not be accessed!!!!", dir.FullName);
            //        return;
            //    }
            //}

            //NOTJSON.UNITTEST();
#endif

            playedfilename = Path.Combine(Plugin.DataPath, "played.dat"); // Record of all the songs played in the current session
            Plugin.Log("create playd path");
            try {
                string filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
                if (Directory.Exists(filesToDelete)) {
                    Plugin.Log("files delete");
                    Utility.EmptyDirectory(filesToDelete);
                }

                try {
                    DateTime LastBackup;
                    if (!DateTime.TryParse(RequestBotConfig.Instance.LastBackup, out LastBackup)) LastBackup = DateTime.MinValue;
                    TimeSpan TimeSinceBackup = DateTime.Now - LastBackup;
                    if (TimeSinceBackup > TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) {
                        Plugin.Log("try buck up");
                        Backup();
                        Plugin.Log("end buck up");
                    }
                }
                catch (Exception ex) {
                    Plugin.Log(ex.ToString());
                    Instance.QueueChatMessage("Failed to run Backup");

                }

                try {
                    TimeSpan PlayedAge = GetFileAgeDifference(playedfilename);
                    if (PlayedAge < TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) played = ReadJSON(playedfilename); // Read the songsplayed file if less than x hours have passed 
                }
                catch (Exception ex) {
                    Plugin.Log(ex.ToString());
                    Instance.QueueChatMessage("Failed to clear played file");

                }

                if (RequestBotConfig.Instance.PPSearch) await GetPPData(); // Start loading PP data

                Plugin.Log("try load database");
                MapDatabase.LoadDatabase();
                Plugin.Log("end load database");

                if (RequestBotConfig.Instance.LocalSearch) await MapDatabase.LoadCustomSongs(); // This is a background process

                RequestQueue.Read(); // Might added the timespan check for this too. To be decided later.
                RequestHistory.Read();
                listcollection.OpenList("banlist.unique");

#if UNRELEASED
            //GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            //GC.Collect();
            //Instance.QueueChatMessage($"hashentries: {SongMap.hashcount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");
#endif

                listcollection.ClearOldList("duplicate.list", TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours));

                UpdateRequestUI();
                InitializeCommands();

                //EnhancedStreamChat.ChatHandler.ChatMessageFilters += MyChatMessageHandler; // TODO: Reimplement this filter maybe? Or maybe we put it directly into EnhancedStreamChat

                try {
                    COMMAND.CommandConfiguration();
                    this.RunStartupScripts();
                }
                catch (Exception e) {
                    Plugin.Log(e.ToString());
                }

                //TwitchMessageHandlers.PRIVMSG += PRIVMSG;

                RequestBotConfig.Instance.ConfigChangedEvent += OnConfigChangedEvent;

                //IsPluginReady = true;
                Plugin.Log("Awake finished!");
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
                QueueChatMessage(ex.ToString());
            }

            Plugin.Logger.Debug("OnLoad()");
            
            SongListUtils.Initialize();

            WriteQueueSummaryToFile();
            WriteQueueStatusToFile(QueueMessage(RequestBotConfig.Instance.RequestQueueOpen));
        }

        public static void AddKeyboard(KEYBOARD keyboard, string keyboardname, float scale = 0.5f)
        {
            try
            {
                string fileContent = File.ReadAllText(Path.Combine(Plugin.DataPath, keyboardname));
                if (fileContent.Length > 0) keyboard.AddKeys(fileContent, scale);
            }
            catch
            {
                // This is a silent fail since custom keyboards are optional
            }
        }

        public static void Newest(KEYBOARD.KEY key)
        {
            ClearSearches();
            RequestBot.COMMAND.Parse(SerchCreateChatUser(), $"!addnew/top", CmdFlags.Local);
        }

        public static void Search(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(SerchCreateChatUser(), $"!addsongs/top {key.kb.KeyboardText.text}",CmdFlags.Local);
            key.kb.Clear(key);
        }

        public static void MSD(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(SerchCreateChatUser(), $"!makesearchdeck {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public static void UnfilteredSearch(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(SerchCreateChatUser(), $"!addsongs/top/mod {key.kb.KeyboardText.text}",CmdFlags.Local);
            key.kb.Clear(key);
        }

        public static void ClearSearches()
        {
            for (int i = 0; i < RequestQueue.Songs.Count; i++)
            {
                var entry = RequestQueue.Songs[i];
                if (entry.status == RequestBot.RequestStatus.SongSearch)
                {
                    RequestBot.DequeueRequest(i, false);
                    i--;
                }
            }
        }

        public void ClearSearch(KEYBOARD.KEY key)
        {
            ClearSearches();
            UpdateUI?.Invoke(false);
            RefreshSongQuere();
            RequestBot._refreshQueue = true;
        }
        public void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
            Instance = this;
        }

        public bool MyChatMessageHandler(IChatMessage msg)
        {
            string excludefilename = "chatexclude.users";
            return listcollection.contains(ref excludefilename, msg.Sender.UserName.ToLower(), RequestBot.ListFlags.Uncached);
        }

        internal void RecievedMessages(IChatService _, IChatMessage msg)
        {
            Plugin.Log($"Received Message : {msg.Message}");
            RequestBot.COMMAND.Parse(msg.Sender, msg.Message.Replace("！", "!"));
        }

        private void OnConfigChangedEvent(RequestBotConfig config)
        {
            _configChanged = true;
        }

        private void OnConfigChanged()
        {
            UpdateRequestUI();

            if (RequestBotListViewController.Instance.isActivated)
            {
                RequestBotListViewController.Instance.UpdateRequestUI(true);
                RequestBotListViewController.Instance.SetUIInteractivity();
            }

            _configChanged = false;
        }
        
        // BUG: Prototype code, used for testing.
        class BotEvent
        {
            public static List<BotEvent> events = new List<BotEvent>();

            public DateTime time;
            public string command;
            public bool repeat;
            System.Timers.Timer timeq;

            public static void Clear()
            {                
                foreach (var ev in events) ev.timeq.Stop();
            }
            public BotEvent(DateTime time,string command,bool repeat)
            {
                this.time = time;
                this.command = command;
                this.repeat = repeat;
                timeq = new System.Timers.Timer(1000);
                timeq.Elapsed += (s, args) => ScheduledCommand(command, args);
                timeq.AutoReset = true;
                timeq.Enabled = true;
            }

            public BotEvent(TimeSpan delta, string command, bool repeat=false)
            {
                this.command = command;
                this.repeat = repeat;
                timeq = new System.Timers.Timer(delta.TotalMilliseconds);
                timeq.Elapsed += (s, args) => ScheduledCommand(command, args);
                timeq.AutoReset = repeat;

                events.Add(this);

                timeq.Enabled = true;
            }
        }

        public static void ScheduledCommand(string command, System.Timers.ElapsedEventArgs e)
        {
            COMMAND.Parse(SerchCreateChatUser(), command);
        }

        private void RunStartupScripts()
        {
            ReadRemapList(); // BUG: This should use list manager

            MapperBanList(SerchCreateChatUser(), "mapperban.list");
            WhiteList(SerchCreateChatUser(), "whitelist.unique");
            BlockedUserList(SerchCreateChatUser(), "blockeduser.unique");
            accesslist("whitelist.unique");
            accesslist("blockeduser.unique");
            accesslist("mapperban.list");

#if UNRELEASED
            OpenList(SerchCreateChatUser(), "mapper.list"); // Open mapper list so we can get new songs filtered by our favorite mappers.
            MapperAllowList(SerchCreateChatUser(), "mapper.list");
            accesslist("mapper.list");

            loaddecks(SerchCreateChatUser(), ""); // Load our default deck collection
            // BUG: Command failure observed once, no permission to use /chatcommand. Possible cause: OurIChatUser isn't authenticated yet.

            RunScript(SerchCreateChatUser(), "startup.script"); // Run startup script. This can include any bot commands.
#endif
        }

        private void FixedUpdate()
        {
            if (_configChanged)
                OnConfigChanged();

            //if (_botMessageQueue.Count > 0)
              //  SendChatMessage(_botMessageQueue.Dequeue());

            if (_refreshQueue)
            {
                if (RequestBotListViewController.Instance.isActivated)
                {
                    RequestBotListViewController.Instance.UpdateRequestUI(true);
                    RequestBotListViewController.Instance.SetUIInteractivity();
                }
                _refreshQueue = false;
            }
        }

// if (!silence) QueueChatMessage($"{request.Key.song["songName"].Value}/{request.Key.song["authorName"].Value} ({songId}) added to the blacklist.");
        private void SendChatMessage(string message)
        {
            Task.Run(() =>
            {
                Plugin.Log($"Sending message: \"{message}\"");
                
                if (Plugin.Instance.TwitchService != null) {
                    foreach (var channel in Plugin.Instance.TwitchService.Channels) {
                        Plugin.Instance.TwitchService.SendTextMessage($"{message}", channel.Value);
                    }
                }
            }).Await(() => { Plugin.Log("Finish send chat message"); },
            e =>
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e}");
            }, null);
        }

        public void QueueChatMessage(string message)
        {
            Task.Run(() =>
            {
                if (Plugin.Instance.TwitchService != null) {
                    foreach (var channel in Plugin.Instance.TwitchService.Channels) {
                        Plugin.Instance.TwitchService.SendTextMessage($"{RequestBotConfig.Instance.BotPrefix}\uFEFF{message}", channel.Value);
                    }
                }
            }).Await(() => { Plugin.Log("Finish Quere chat message"); },
            e =>
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e}");
            }, null);
        }

        private void ProcessRequestQueue()
        {
            lock (_lockObject) {
                while (!UnverifiedRequestQueue.IsEmpty) {
                    try {
                        if (UnverifiedRequestQueue.TryDequeue(out var requestInfo)) {
                            CheckRequest(requestInfo).Await(null, e => { Plugin.Log($"{e}"); }, null);
                        }
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                }
            }
        }

        int CompareSong(JSONObject song2, JSONObject song1, ref string [] sortorder)            
            {
            int result=0;

            foreach (string s in sortorder)
            {
                string sortby = s.Substring(1);
                switch (sortby)
                {
                    case "rating":
                    case "pp":

                        //QueueChatMessage($"{song2[sortby].AsFloat} < {song1[sortby].AsFloat}");
                        result = song2[sortby].AsFloat.CompareTo(song1[sortby].AsFloat);
                        break;

                    case "id":
                    case "version":
                        // BUG: This hack makes sorting by version and ID sort of work. In reality, we're comparing 1-2 numbers
                        result=GetBeatSaverId(song2[sortby].Value).PadLeft(6).CompareTo(GetBeatSaverId(song1[sortby].Value).PadLeft(6));
                        break;

                    default:
                        result= song2[sortby].Value.CompareTo(song1[sortby].Value);
                        break;
                }
                if (result == 0) continue;

                if (s[0] == '-') return -result;
                
                return result;
            }
            return result;
        }

        private async void UpdateSongMap(JSONObject song)
        {
            var resp = await WebClient.GetAsync($"https://beatsaver.com/api/maps/detail/{song["id"].Value.ToString()}", System.Threading.CancellationToken.None);

            if (resp.IsSuccessStatusCode) {
                var result = resp.ConvertToJsonNode();

                QueueChatMessage($"{result.AsObject}");

                if (result != null && result["id"].Value != "") {
                    song = result.AsObject;
                    new SongMap(result.AsObject);
                }
            }
        }

        // BUG: Testing major changes. This will get seriously refactored soon.
        private async Task CheckRequest(RequestInfo requestInfo)
        {
            IChatUser requestor = requestInfo.requestor;
            string request = requestInfo.request;

            string normalrequest= normalize.NormalizeBeatSaverString(requestInfo.request);

            var id = GetBeatSaverId(normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash));

            if (id!="")
            {
                // Remap song id if entry present. This is one time, and not correct as a result. No recursion right now, could be confusing to the end user.
                if (songremap.ContainsKey(id) && !requestInfo.flags.HasFlag(CmdFlags.NoFilter))
                {
                    request = songremap[id];
                    QueueChatMessage($"Remapping request {requestInfo.request} to {request}");
                }

                string requestcheckmessage = IsRequestInQueue(normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash));               // Check if requested ID is in Queue  
                if (requestcheckmessage != "")
                {
                    QueueChatMessage(requestcheckmessage);
                    return;
                }

                if (RequestBotConfig.Instance.OfflineMode && RequestBotConfig.Instance.offlinepath!="" && !MapDatabase.MapLibrary.ContainsKey(id))
                {
                    Dispatcher.RunCoroutine(this.LoadOfflineDataBase(id));
                }
            }

            JSONNode result = null;

            string errorMessage = "";

            // Get song query results from beatsaver.com
            if (!RequestBotConfig.Instance.OfflineMode)
            {
                string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text/0?q={normalrequest}";

                var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);
                if (resp == null) {
                    errorMessage = $"BeatSaver is down now.";
                }
                else if (resp.IsSuccessStatusCode) {
                    result = resp.ConvertToJsonNode();
                }
                else {
                    errorMessage = $"Invalid BeatSaver ID \"{request}\" specified. {requestUrl}";
                }
            }

            SongFilter filter = SongFilter.All;
            if (requestInfo.flags.HasFlag(CmdFlags.NoFilter)) filter = SongFilter.Queue;
            List<JSONObject> songs = GetSongListFromResults(result, request, ref errorMessage, filter, requestInfo.state.sort != "" ? requestInfo.state.sort : AddSortOrder.ToString());

            bool autopick = RequestBotConfig.Instance.AutopickFirstSong || requestInfo.flags.HasFlag(CmdFlags.Autopick);

            // Filter out too many or too few results
            if (songs.Count == 0) {
                if (errorMessage == "") {
                    errorMessage = $"No results found for request \"{request}\"";
                }   
            }
            else if (!autopick && songs.Count >= 4) {
                errorMessage = $"Request for '{request}' produces {songs.Count} results, narrow your search by adding a mapper name, or use https://beatsaver.com to look it up.";
            }
            else if (!autopick && songs.Count > 1 && songs.Count < 4) {
                var msg = new QueueLongMessage(1, 5);
                //ToDo: Support Mixer whisper
                if (requestor is TwitchUser) {
                    msg.Header($"@{requestor.UserName}, please choose: ");
                }
                else {
                    msg.Header($"@{requestor.UserName}, please choose: ");
                }
                foreach (var eachsong in songs) {
                    msg.Add(new DynamicText().AddSong(eachsong).Parse(BsrSongDetail), ", ");
                }
                msg.end("...", $"No matching songs for for {request}");
                return;
            }
            else {
                if (!requestInfo.flags.HasFlag(CmdFlags.NoFilter)) errorMessage = SongSearchFilter(songs[0], false);
            }

            // Display reason why chosen song was rejected, if filter is triggered. Do not add filtered songs
            if (errorMessage != "") {
                QueueChatMessage(errorMessage);
                return;
            }

            JSONObject song = songs[0];

            // Song requests should try to be current. If the song was local, we double check for a newer version

            //if ((song["downloadUrl"].Value == "") && !RequestBotConfig.Instance.OfflineMode )
            //{
            //    //QueueChatMessage($"song:  {song["id"].Value.ToString()} ,{song["songName"].Value}");

            //    yield return Utilities.Download($"https://beatsaver.com/api/maps/detail/{song["id"].Value.ToString()}", Utilities.DownloadType.Raw, null,
            //     // Download success
            //     (web) =>
            //     {
            //         result = JSON.Parse(web.downloadHandler.text);
            //         var newsong = result["song"].AsObject;

            //         if (result != null && newsong["version"].Value != "")
            //         {
            //             new SongMap(newsong);
            //             song = newsong;
            //         }
            //     },
            //     // Download failed,  song probably doesn't exist on beatsaver
            //     (web) =>
            //     {
            //         // Let player know that the song is not current on BeatSaver
            //         requestInfo.requestInfo += " *LOCAL ONLY*";
            //         ; //errorMessage = $"Invalid BeatSaver ID \"{request}\" specified. {requestUrl}";
            //     });

            //}

            RequestTracker[requestor.Id].numRequests++;
            listcollection.add(duplicatelist, song["id"].Value);
            if ((requestInfo.flags.HasFlag(CmdFlags.MoveToTop))) {
                RequestQueue.Songs.Insert(0, new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo));
            }
            else {
                RequestQueue.Songs.Add(new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo));
            }
            RequestQueue.Write();

            Writedeck(requestor, "savedqueue"); // This can be used as a backup if persistent Queue is turned off.

            if (!requestInfo.flags.HasFlag(CmdFlags.SilentResult)) {
                new DynamicText().AddSong(ref song).QueueMessage(AddSongToQueueText.ToString());
            }

            UpdateRequestUI();
            RefreshSongQuere();
            _refreshQueue = true;
        }

        private IEnumerator LoadOfflineDataBase(string id)
        {
            foreach (string directory in Directory.GetDirectories(RequestBotConfig.Instance.offlinepath, id + "*")) {
                MapDatabase.LoadCustomSongs(directory, id).Await(null, e => { Plugin.Log($"{e}"); }, null);
                Task.Delay(25).Wait();
                yield return new WaitWhile(() => MapDatabase.DatabaseLoading);
                // break;
            }
        }

        private async Task ProcessSongRequest(int index, bool fromHistory = false)
        {
            if ((RequestQueue.Songs.Count > 0 && !fromHistory) || (RequestHistory.Songs.Count > 0 && fromHistory))
            {
                SongRequest request = null;
                if (!fromHistory)
                {
                    Plugin.Log("Set status to request");
                    SetRequestStatus(index, RequestStatus.Played);
                    request = DequeueRequest(index);
                }
                else
                {
                    request = RequestHistory.Songs.ElementAt(index);
                }

                if (request == null)
                {
                    Plugin.Log("Can't process a null request! Aborting!");
                    return;
                }
                else
                    Plugin.Log($"Processing song request {request.song["songName"].Value}");

 
                string songName = request.song["songName"].Value;
                string songIndex = Regex.Replace($"{request.song["id"].Value} ({request.song["songName"].Value} - {request.song["levelAuthor"].Value})", "[\\\\:*/?\"<>|]", "_");
                songIndex = normalize.RemoveDirectorySymbols(ref songIndex); // Remove invalid characters.

                string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data\\CustomLevels", songIndex);
                string songHash = request.song["hash"].Value.ToUpper();


                // Check to see if level exists, download if not.

                // Replace with level check.
                //CustomLevel[] levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                //if (levels.Length == 0)

                //var rat = SongCore.Collections.levelIDsForHash(songHash);
                //bool mapexists = (rat.Count>0) && (rat[0] != "");

                if (Loader.GetLevelByHash(songHash) == null)
                {
                    Utility.EmptyDirectory(".requestcache", false);
                    //SongMap map;
                    //if (MapDatabase.MapLibrary.TryGetValue(songIndex, out map))
                    //{
                    //    if (map.path != "")
                    //    {
                    //        songIndex = map.song["version"].Value;
                    //        songName = map.song["songName"].Value;
                    //        currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "CustomSongs", songIndex);
                    //        songHash = map.song["hashMd5"].Value.ToUpper();
                    //        Directory.CreateDirectory(currentSongDirectory);
                    //        // HACK to allow playing alternate songs not in custom song directory
                    //        CopyFilesRecursively(new DirectoryInfo(map.path),new DirectoryInfo( currentSongDirectory));
                    //        goto here;
                    //    }
                    //}
                    //Plugin.Log("Downloading");

                    if (Directory.Exists(currentSongDirectory)) {
                        Utility.EmptyDirectory(currentSongDirectory, true);
                        Plugin.Log($"Deleting {currentSongDirectory}");
                    }
                    string localPath = Path.Combine(Environment.CurrentDirectory, ".requestcache", $"{request.song["id"].Value}.zip");
                    //string dl = $"https://beatsaver.com {request.song["downloadURL"].Value}";
                    //Instance.QueueChatMessage($"Download url: {dl}, {request.song}");
                    // Insert code to replace local path with ZIP path here
                    //SongMap map;
                    //if (MapDatabase.MapLibrary.TryGetValue(songIndex, out map))
                    //{
                    //    if (map.path != "")
                    //    {
                    //        songIndex = map.song["version"].Value;
                    //        songName = map.song["songName"].Value;
                    //        currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "CustomSongs", songIndex);
                    //        songHash = map.song["hashMd5"].Value.ToUpper();

                    //        Directory.CreateDirectory(currentSongDirectory);
                    //        // HACK to allow playing alternate songs not in custom song directory
                    //        CopyFilesRecursively(new DirectoryInfo(map.path),new DirectoryInfo( currentSongDirectory));                           

                    //        goto here;
                    //    }
                    //}


#if UNRELEASED
                    // Direct download hack
                    var ext = Path.GetExtension(request.song["coverURL"].Value);
                    var k = request.song["coverURL"].Value.Replace(ext, ".zip");

                    var songZip = await Plugin.WebClient.DownloadSong($"https://beatsaver.com{k}", System.Threading.CancellationToken.None);
#else
                    var result = await WebClient.DownloadSong($"https://beatsaver.com{request.song["downloadURL"].Value}", System.Threading.CancellationToken.None, RequestBotListViewController.Instance._progress);
                    if (result == null) {
                        Instance.QueueChatMessage("BeatSaver is down now.");
                    }
                    using (var zipStream = new MemoryStream(result))
                    using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
                        try {
                            // open zip archive from memory stream
                            archive.ExtractToDirectory(currentSongDirectory);
                        }
                        catch (Exception e) {
                            Plugin.Log($"Unable to extract ZIP! Exception: {e}");
                            return;
                        }
                        zipStream.Close();
                    }
                    Dispatcher.RunOnMainThread(() =>
                    {
                        Dispatcher.RunCoroutine(WaitForRefreshAndSchroll(request));
                    });
#if UNRELEASED
                        //if (!request.song.IsNull) // Experimental!
                        //{
                        //TwitchWebSocketClient.SendCommand("/marker "+ new DynamicText().AddUser(ref request.requestor).AddSong(request.song).Parse(NextSonglink.ToString()));
                        //}
#endif
#endif
                }
                else
                {
                    //Instance.QueueChatMessage($"Directory exists: {currentSongDirectory}");
                    Plugin.Log($"Song {songName} already exists!");
                    DismissRequest?.Invoke();
                    bool success = false;
                    Dispatcher.RunCoroutine(SongListUtils.ScrollToLevel(songHash, (s) => success = s, false));
                    if (!request.song.IsNull) {
                        // Display next song message
                        new DynamicText().AddUser(ref request.requestor).AddSong(request.song).QueueMessage(NextSonglink.ToString());
                    }
                }
            }
        }

        private IEnumerator WaitForRefreshAndSchroll(SongRequest request)
        {
            _levelFilteringNavigationController.UpdateCustomSongs();
            yield return null;
            //yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
            //Loader.Instance.RefreshSongs(false);
            //yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
            Utility.EmptyDirectory(".requestcache", true);
            button.BackButtonPressed();
            DismissRequest?.Invoke();
            bool success = false;
            Dispatcher.RunCoroutine(SongListUtils.ScrollToLevel(request.song["hash"].Value.ToUpper(), (s) => success = s, false));
            RequestBotListViewController.Instance?.ChangeProgressText(0f);
            if (!request.song.IsNull) {
                // Display next song message
                new DynamicText().AddUser(ref request.requestor).AddSong(request.song).QueueMessage(NextSonglink.ToString());
            }
        }
        public event Action<bool> UpdateUI;

        public void UpdateRequestUI(bool writeSummary = true)
        {
            Plugin.Log("start updateUI");
            try {
                if (writeSummary)
                    WriteQueueSummaryToFile(); // Write out queue status to file, do it first


                Dispatcher.RunOnMainThread(() =>
                {
                    if (button != null) {
                        button.SetButtonIntaractive(true);

                        if (RequestQueue.Songs.Count == 0) {
                            button.SetButtonColor(Color.red);
                        }
                        else {
                            button.SetButtonColor(Color.green);
                        }
                    }
                });
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }
            finally {
                Plugin.Log("end update UI");
            }
            UpdateUI?.Invoke(writeSummary);
        }

        public static void RefreshSongQuere()
        {
            if (RequestBotListViewController.Instance) {
                Dispatcher.RunOnMainThread(() =>
                {
                    RequestBotListViewController.Instance.RefreshSongQueueList(false);
                });
            }
        }

        public void DequeueRequest(SongRequest request, bool updateUI = true)
        {
            Plugin.Log("start to deque request");
            try {
                if (request.status != RequestStatus.Wrongsong && request.status != RequestStatus.SongSearch) RequestHistory.Songs.Insert(0, request); // Wrong song requests are not logged into history, is it possible that other status states shouldn't be moved either?

                if (RequestHistory.Songs.Count > RequestBotConfig.Instance.RequestHistoryLimit) {
                    int diff = RequestHistory.Songs.Count - RequestBotConfig.Instance.RequestHistoryLimit;
                    RequestHistory.Songs.RemoveRange(RequestHistory.Songs.Count - diff - 1, diff);
                }
                RequestQueue.Songs.Remove(request);
                RequestHistory.Write();
                HistoryManager.AddSong(request);
                RequestQueue.Write();

                // Decrement the requestors request count, since their request is now out of the queue

                if (!RequestBotConfig.Instance.LimitUserRequestsToSession) {
                    if (RequestTracker.ContainsKey(request.requestor.Id)) RequestTracker[request.requestor.Id].numRequests--;
                }

                if (updateUI == false) return;
            }
            catch (Exception e) {
                Plugin.Log($"{e}");
            }
            finally {
                UpdateUI?.Invoke(false);
                _refreshQueue = true;
                Plugin.Log("end Deque");
            }
        }

        public static SongRequest DequeueRequest(int index, bool updateUI = true)
        {
            SongRequest request = RequestQueue.Songs.ElementAt(index);

            if (request != null)
                Instance?.DequeueRequest(request, updateUI);

#if UNRELEASED
            // If the queue is empty, Execute a custom command, the could be a chat message, a deck request, or nothing
            try
            {
                if (RequestBotConfig.Instance.RequestQueueOpen && updateUI == true && RequestQueue.Songs.Count == 0) RequestBot.listcollection.runscript("emptyqueue.script");
            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); }
#endif
            return request;
        }

        public static void SetRequestStatus(int index, RequestStatus status, bool fromHistory = false)
        {
            if (!fromHistory)
                RequestQueue.Songs[index].status = status;
            else
                RequestHistory.Songs[index].status = status;
        }

        public static void Blacklist(int index, bool fromHistory, bool skip)
        {
            // Add the song to the blacklist
            SongRequest request = fromHistory ? RequestHistory.Songs.ElementAt(index) : RequestQueue.Songs.ElementAt(index);

            listcollection.add(banlist, request.song["id"].Value);
 
            Instance.QueueChatMessage($"{request.song["songName"].Value} by {request.song["authorName"].Value} ({request.song["id"].Value}) added to the blacklist.");

            if (!fromHistory)
            {
                if (skip)
                    Skip(index, RequestStatus.Blacklisted);
            }
            else
                SetRequestStatus(index, RequestStatus.Blacklisted, fromHistory);
        }

        public static void Skip(int index, RequestStatus status = RequestStatus.Skipped)
        {
            // Set the final status of the request
            SetRequestStatus(index, status);

            // Then dequeue it
            DequeueRequest(index);

            RequestBotListViewController.Instance.UpdateRequestUI();
        }

        public void Process(int index, bool fromHistory)
        {
            ProcessSongRequest(index, fromHistory).Await(null, null, null);
        }

        public void Next()
        {
            ProcessSongRequest(0).Await(null, null, null); ;
        }


        private string GetBeatSaverId(string request)
        {
            request=normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash);
            if (request!="360" && _digitRegex.IsMatch(request)) return request;
            if (_beatSaverRegex.IsMatch(request))
            {
                string[] requestparts = request.Split(new char[] { '-' }, 2);
                //return requestparts[0];
           
                int o;
                Int32.TryParse(requestparts[1], out o);
                     {
                        //Instance.QueueChatMessage($"key={o.ToString("x")}");
                    return o.ToString("x");
                    }
      
            }
            return "";
        }


        private string AddToTop(ParseState state)
        {
            ParseState newstate = new ParseState(state); // Must use copies here, since these are all threads
            newstate.flags |= CmdFlags.MoveToTop | CmdFlags.NoFilter;
            newstate.info = "!ATT";
            return ProcessSongRequest(newstate);
        }

        private string ModAdd(ParseState state)
        {
            ParseState newstate = new ParseState(state); // Must use copies here, since these are all threads
            newstate.flags |= CmdFlags.NoFilter;
            newstate.info = "Unfiltered";
            return ProcessSongRequest(newstate);
        }


        private string ProcessSongRequest(ParseState state)
        {
            try
            {
                if (RequestBotConfig.Instance.RequestQueueOpen == false && !state.flags.HasFlag(CmdFlags.NoFilter) && !state.flags.HasFlag(CmdFlags.Local)) // BUG: Complex permission, Queue state message needs to be handled higher up
                {
                    QueueChatMessage($"Queue is currently closed.");
                    return success;
                }

                if (!RequestTracker.ContainsKey(state.user.Id))
                    RequestTracker.Add(state.user.Id, new RequestUserTracker());

                int limit = RequestBotConfig.Instance.UserRequestLimit;

                if (state.user is TwitchUser twitchUser) {
                    if (twitchUser.IsSubscriber) limit = Math.Max(limit, RequestBotConfig.Instance.SubRequestLimit);
                    if (state.user.IsModerator) limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                    if (twitchUser.IsVip) limit += RequestBotConfig.Instance.VipBonusRequests; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like
                }
                else {
                    if (state.user.IsModerator) limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                }

                if (!state.user.IsBroadcaster)
                {
                    if (RequestTracker[state.user.Id].numRequests >= limit)
                    {
                        if (RequestBotConfig.Instance.LimitUserRequestsToSession)
                        {
                            new DynamicText().Add("Requests", RequestTracker[state.user.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You've already used %Requests% requests this stream. Subscribers are limited to %RequestLimit%.");
                        }
                        else
                        {
                            new DynamicText().Add("Requests", RequestTracker[state.user.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");
                        }

                        return success;
                    }
                }

                // BUG: Need to clean up the new request pipeline
                string testrequest = normalize.RemoveSymbols(ref state.parameter, normalize._SymbolsNoDash);

                RequestInfo newRequest = new RequestInfo(state.user, state.parameter, DateTime.UtcNow, _digitRegex.IsMatch(testrequest) || _beatSaverRegex.IsMatch(testrequest),state, state.flags, state.info);

                if (!newRequest.isBeatSaverId && state.parameter.Length < 2) {
                    QueueChatMessage($"Request \"{state.parameter}\" is too short- Beat Saver searches must be at least 3 characters!");
                }
                    
                if (!UnverifiedRequestQueue.Contains(newRequest)) {
                    UnverifiedRequestQueue.Enqueue(newRequest);
                    this.ProcessRequestQueue();
                }   
                return success;
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
                throw;
            }
            finally {
                this.RecevieRequest?.Invoke();
            }
        }
        

        private static IChatUser SerchCreateChatUser()
        {
            if (Plugin.Instance.TwitchService?.LoggedInUser != null) {
                return Plugin.Instance.TwitchService?.LoggedInUser;
            }
            else {
                var obj = new
                {
                    Id = "",
                    UserName = RequestBotConfig.Instance.MixerUserName,
                    DisplayName = RequestBotConfig.Instance.MixerUserName,
                    Color = "#FFFFFFFF",
                    IsBroadcaster = true,
                    IsModerator = false,
                    IsSubscriber = false,
                    IsPro = false,
                    IsStaff = false,
                    Badges = new IChatBadge[0]
                };
                return new TwitchUser(JsonUtility.ToJson(obj));
            }
        }
 
    }
}