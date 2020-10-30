using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if OLDVERSION
using TMPro;
#endif

using UnityEngine;
using SongCore;
using System.Threading.Tasks;
using System.IO.Compression;
using ChatCore.Models.Twitch;
using ChatCore.Interfaces;
using ChatCore.Services;
using ChatCore.Services.Twitch;
using SongRequestManagerV2.Extentions;
using SongRequestManagerV2.Utils;
using System.Text.RegularExpressions;
using SongRequestManagerV2.Views;
using Zenject;
using ChatCore.Utilities;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Networks;
using BeatSaberMarkupLanguage.Settings;
using SongRequestManagerV2.UI;
using System.Text;
using System.Security.Cryptography;
using IPA.Loader;

namespace SongRequestManagerV2.Bots
{
    public class RequestBot : MonoBehaviour, IRequestBot, IInitializable
    {
        public ChatServiceMultiplexer MultiplexerInstance { get; internal set; }
        public TwitchService TwitchService { get; internal set; }

        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue { get; } = new ConcurrentQueue<RequestInfo>();
        public static Dictionary<string, RequestUserTracker> RequestTracker { get; } = new Dictionary<string, RequestUserTracker>();
        //private ChatServiceMultiplexer _chatService { get; set; }

        //SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        //synthesizer.Volume = 100;  // 0...100
        //    synthesizer.Rate = -2;     // -10...10
        public bool RefreshQueue { get; set; } = false;

        private static Queue<string> _botMessageQueue = new Queue<string>();

        bool _mapperWhitelist = false; // BUG: Need to clean these up a bit.

        public static System.Random Generator { get; } = new System.Random(); // BUG: Should at least seed from unity?

        public static List<JSONObject> Played { get; private set; } = new List<JSONObject>(); // Played list
        public static List<BotEvent> Events { get; } = new List<BotEvent>();

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

        private static readonly Regex _digitRegex = new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _deck = new Regex("^(current|draw|first|last|random|unload)$|$^", RegexOptions.Compiled); // Checks deck command parameters
        private static readonly Regex _drawcard = new Regex("($^)|(^[0-9a-zA-Z]+$)", RegexOptions.Compiled);
        [Inject]
        public SongListUtils SongListUtils { get; private set; }
        [Inject]
        public StringNormalization Normalize { get; private set; }
        [Inject]
        public MapDatabase MapDatabase { get; private set; }
        [Inject]
        RequestManager _requestManager;
        [Inject]
        QueueLongMessage.QueueLongMessageFactroy _messageFactroy;
        [Inject]
        SongRequest.SongRequestFactory _songRequestFactory;
        [Inject]
        DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        ParseState.ParseStateFactory _stateFactory;

        [Inject]
        public ListCollectionManager ListCollectionManager { get; private set; }

        public static string playedfilename = "";
        public event Action RecevieRequest;
        public event Action DismissRequest;
        public event Action<bool> RefreshListRequest;
        public event Action<Color> ChangeButtonColor;

        private static readonly object _lockObject = new object();

        public SongRequest Currentsong { get; set; }


        const string success = "";
        const string endcommand = "X";
        const string notsubcommand = "NotSubcmd";

        void OnDestroy()
        {
            this.MultiplexerInstance.OnLogin -= this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnJoinChannel -= this.MultiplexerInstance_OnJoinChannel;
            this.MultiplexerInstance.OnTextMessageReceived -= this.RecievedMessages;
        }

        [Inject]
        private async void Constroctor()
        {
            Plugin.Log("Constroctor()");
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
                    if (!DateTime.TryParse(RequestBotConfig.Instance.LastBackup, out var LastBackup)) LastBackup = DateTime.MinValue;
                    TimeSpan TimeSinceBackup = DateTime.Now - LastBackup;
                    if (TimeSinceBackup > TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) {
                        Plugin.Log("try buck up");
                        Backup();
                        Plugin.Log("end buck up");
                    }
                }
                catch (Exception ex) {
                    Plugin.Log(ex.ToString());
                    QueueChatMessage("Failed to run Backup");

                }

                try {
                    TimeSpan PlayedAge = GetFileAgeDifference(playedfilename);
                    if (PlayedAge < TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) Played = ReadJSON(playedfilename); // Read the songsplayed file if less than x hours have passed 
                }
                catch (Exception ex) {
                    Plugin.Log(ex.ToString());
                    QueueChatMessage("Failed to clear played file");

                }

                if (RequestBotConfig.Instance.PPSearch) await GetPPData(); // Start loading PP data

                Plugin.Log("try load database");
                MapDatabase.LoadDatabase();
                Plugin.Log("end load database");

                if (RequestBotConfig.Instance.LocalSearch) await MapDatabase.LoadCustomSongs(); // This is a background process

                _requestManager.ReadRequest(); // Might added the timespan check for this too. To be decided later.
                _requestManager.ReadHistory();
                ListCollectionManager.OpenList("banlist.unique");

#if UNRELEASED
            //GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            //GC.Collect();
            //Instance.QueueChatMessage($"hashentries: {SongMap.hashcount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");
#endif

                ListCollectionManager.ClearOldList("duplicate.list", TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours));

                UpdateRequestUI();

                this.MultiplexerInstance = Plugin.CoreInstance.RunAllServices();
                this.MultiplexerInstance.OnLogin -= this.MultiplexerInstance_OnLogin;
                this.MultiplexerInstance.OnLogin += this.MultiplexerInstance_OnLogin;
                this.MultiplexerInstance.OnJoinChannel -= this.MultiplexerInstance_OnJoinChannel;
                this.MultiplexerInstance.OnJoinChannel += this.MultiplexerInstance_OnJoinChannel;
                this.MultiplexerInstance.OnTextMessageReceived -= this.RecievedMessages;
                this.MultiplexerInstance.OnTextMessageReceived += this.RecievedMessages;

                this.TwitchService = this.MultiplexerInstance.GetTwitchService();

                RequestBotConfig.Instance.ConfigChangedEvent += OnConfigChangedEvent;
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
                QueueChatMessage(ex.ToString());
            }

            Plugin.Logger.Debug("OnLoad()");

            WriteQueueSummaryToFile();
            WriteQueueStatusToFile(QueueMessage(RequestBotConfig.Instance.RequestQueueOpen));
        }

        public void Initialize()
        {
            Plugin.Log("Start Initialize");
            if (RequestBotConfig.Instance.IsStartServer) {
                BouyomiPipeline.instance.ReceiveMessege -= this.Instance_ReceiveMessege;
                BouyomiPipeline.instance.ReceiveMessege += this.Instance_ReceiveMessege;
                BouyomiPipeline.instance.Start();
            }
            else {
                BouyomiPipeline.instance.ReceiveMessege -= this.Instance_ReceiveMessege;
                BouyomiPipeline.instance.Stop();
            }

            // setup settings ui
            BSMLSettings.instance.AddSettingsMenu("SRM V2", "SongRequestManagerV2.Views.SongRequestManagerSettings.bsml", SongRequestManagerSettings.instance);


            RequestBotConfig.Instance.Save(true);
            Plugin.Log("End Initialize");
        }

        internal void MultiplexerInstance_OnJoinChannel(IChatService arg1, IChatChannel arg2)
        {
            Plugin.Log($"Joined! : [{arg1.DisplayName}][{arg2.Name}]");
            if (arg1 is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }

        internal void MultiplexerInstance_OnLogin(IChatService obj)
        {
            Plugin.Log($"Loged in! : [{obj.DisplayName}]");
            if (obj is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }

        public void Newest(KEYBOARD.KEY key)
        {
            ClearSearches();
            Parse(SerchCreateChatUser(), $"!addnew/top", CmdFlags.Local);
        }

        public void Search(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            ClearSearches();
            Parse(SerchCreateChatUser(), $"!addsongs/top {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void MSD(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            ClearSearches();
            Parse(SerchCreateChatUser(), $"!makesearchdeck {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void UnfilteredSearch(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            ClearSearches();
            Parse(SerchCreateChatUser(), $"!addsongs/top/mod {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void ClearSearches()
        {
            for (int i = 0; i < RequestManager.RequestSongs.Count; i++) {
                var entry = (RequestManager.RequestSongs[i] as SongRequest);
                if (entry._status == RequestStatus.SongSearch) {
                    DequeueRequest(i, false);
                    i--;
                }
            }
        }

        public void ClearSearch(KEYBOARD.KEY key)
        {
            ClearSearches();
            RefreshSongQuere();
            RefreshQueue = true;
        }

        public bool MyChatMessageHandler(IChatMessage msg)
        {
            string excludefilename = "chatexclude.users";
            return ListCollectionManager.Contains(excludefilename, msg.Sender.UserName.ToLower(), ListFlags.Uncached);
        }

        internal void RecievedMessages(IChatService _, IChatMessage msg)
        {
            Plugin.Log($"Received Message : {msg.Message}");
            Parse(msg.Sender, msg.Message.Replace("！", "!"));
        }

        internal void OnConfigChangedEvent(RequestBotConfig config)
        {
            UpdateRequestUI();

            if (RequestBotListView.Instance.isActivated) {
                RequestBotListView.Instance.UpdateRequestUI(true);
                RequestBotListView.Instance.SetUIInteractivity();
            }
        }

        // BUG: Prototype code, used for testing.
        

        public void ScheduledCommand(string command, System.Timers.ElapsedEventArgs e)
        {
            Parse(SerchCreateChatUser(), command);
        }

        public void RunStartupScripts()
        {
            ReadRemapList(); // BUG: This should use list manager

            MapperBanList(SerchCreateChatUser(), "mapperban.list");
            WhiteList(SerchCreateChatUser(), "whitelist.unique");
            BlockedUserList(SerchCreateChatUser(), "blockeduser.unique");

#if UNRELEASED
            OpenList(SerchCreateChatUser(), "mapper.list"); // Open mapper list so we can get new songs filtered by our favorite mappers.
            MapperAllowList(SerchCreateChatUser(), "mapper.list");
            accesslist("mapper.list");

            loaddecks(SerchCreateChatUser(), ""); // Load our default deck collection
            // BUG: Command failure observed once, no permission to use /chatcommand. Possible cause: OurIChatUser isn't authenticated yet.

            RunScript(SerchCreateChatUser(), "startup.script"); // Run startup script. This can include any bot commands.
#endif
        }

        //internal void FixedUpdate()
        //{
        //    if (_configChanged)
        //        OnConfigChanged();
        //    if (_refreshQueue)
        //    {
        //        if (RequestBotListView.Instance.isActivated)
        //        {
        //            RequestBotListView.Instance.UpdateRequestUI(true);
        //            RequestBotListView.Instance.SetUIInteractivity();
        //        }
        //        _refreshQueue = false;
        //    }
        //}

        // if (!silence) QueueChatMessage($"{request.Key.song["songName"].Value}/{request.Key.song["authorName"].Value} ({songId}) added to the blacklist.");
        internal void SendChatMessage(string message)
        {
            Task.Run(() =>
            {
                Plugin.Log($"Sending message: \"{message}\"");

                if (this.TwitchService != null) {
                    foreach (var channel in this.TwitchService.Channels) {
                        this.TwitchService.SendTextMessage($"{message}", channel.Value);
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
                if (this.TwitchService != null) {
                    foreach (var channel in this.TwitchService.Channels) {
                        this.TwitchService.SendTextMessage($"{RequestBotConfig.Instance.BotPrefix}\uFEFF{message}", channel.Value);
                    }
                }
            }).Await(() => { Plugin.Log("Finish Quere chat message"); },
            e =>
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e}");
            }, null);
        }

        internal void ProcessRequestQueue()
        {
            lock (_lockObject) {
                while (!UnverifiedRequestQueue.IsEmpty) {
                    try {
                        if (UnverifiedRequestQueue.TryDequeue(out var requestInfo)) {
                            CheckRequest(requestInfo).Await(() =>
                            {
                                Plugin.Log("ProcessRequestQueue()");
                                UpdateRequestUI();
                                RefreshSongQuere();
                                RefreshQueue = true;
                                Plugin.Log("end ProcessRequestQueue()");
                            }, e => { Plugin.Log($"{e}"); }, null);
                        }
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                }
            }
        }

        int CompareSong(JSONObject song2, JSONObject song1, ref string[] sortorder)
        {
            int result = 0;

            foreach (string s in sortorder) {
                string sortby = s.Substring(1);
                switch (sortby) {
                    case "rating":
                    case "pp":

                        //QueueChatMessage($"{song2[sortby].AsFloat} < {song1[sortby].AsFloat}");
                        result = song2[sortby].AsFloat.CompareTo(song1[sortby].AsFloat);
                        break;

                    case "id":
                    case "version":
                        // BUG: This hack makes sorting by version and ID sort of work. In reality, we're comparing 1-2 numbers
                        result = GetBeatSaverId(song2[sortby].Value).PadLeft(6).CompareTo(GetBeatSaverId(song1[sortby].Value).PadLeft(6));
                        break;

                    default:
                        result = song2[sortby].Value.CompareTo(song1[sortby].Value);
                        break;
                }
                if (result == 0) continue;

                if (s[0] == '-') return -result;

                return result;
            }
            return result;
        }

        internal void UpdateSongMap(JSONObject song)
        {
            WebClient.GetAsync($"https://beatsaver.com/api/maps/detail/{song["id"].Value}", System.Threading.CancellationToken.None).Await(resp =>
            {
                if (resp.IsSuccessStatusCode) {
                    var result = resp.ConvertToJsonNode();

                    QueueChatMessage($"{result.AsObject}");

                    if (result != null && result["id"].Value != "") {
                        new SongMap(result.AsObject);
                    }
                }
            },null,null);
        }

        // BUG: Testing major changes. This will get seriously refactored soon.
        internal async Task CheckRequest(RequestInfo requestInfo)
        {
            IChatUser requestor = requestInfo.requestor;
            string request = requestInfo.request;

            string normalrequest = Normalize.NormalizeBeatSaverString(requestInfo.request);

            var id = GetBeatSaverId(Normalize.RemoveSymbols(ref request, Normalize._SymbolsNoDash));

            if (id != "") {
                // Remap song id if entry present. This is one time, and not correct as a result. No recursion right now, could be confusing to the end user.
                if (songremap.ContainsKey(id) && !requestInfo.flags.HasFlag(CmdFlags.NoFilter)) {
                    request = songremap[id];
                    QueueChatMessage($"Remapping request {requestInfo.request} to {request}");
                }

                string requestcheckmessage = IsRequestInQueue(Normalize.RemoveSymbols(ref request, Normalize._SymbolsNoDash));               // Check if requested ID is in Queue  
                if (requestcheckmessage != "") {
                    QueueChatMessage(requestcheckmessage);
                    return;
                }

                if (RequestBotConfig.Instance.OfflineMode && RequestBotConfig.Instance.offlinepath != "" && !MapDatabase.MapLibrary.ContainsKey(id)) {
                    Dispatcher.RunCoroutine(this.LoadOfflineDataBase(id));
                }
            }

            JSONNode result = null;

            string errorMessage = "";

            // Get song query results from beatsaver.com
            if (!RequestBotConfig.Instance.OfflineMode) {
                string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{Normalize.RemoveSymbols(ref request, Normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text/0?q={normalrequest}";

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
            List<JSONObject> songs = GetSongListFromResults(result, request, ref errorMessage, filter, requestInfo.state._sort != "" ? requestInfo.state._sort : StringFormat.AddSortOrder.ToString());

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
                var msg = this._messageFactroy.Create().SetUp(1, 5);
                //ToDo: Support Mixer whisper
                if (requestor is TwitchUser) {
                    msg.Header($"@{requestor.UserName}, please choose: ");
                }
                else {
                    msg.Header($"@{requestor.UserName}, please choose: ");
                }
                foreach (var eachsong in songs) {
                    msg.Add(_textFactory.Create().AddSong(eachsong).Parse(StringFormat.BsrSongDetail), ", ");
                }
                msg.End("...", $"No matching songs for for {request}");
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
            ListCollectionManager.Add(duplicatelist, song["id"].Value);
            var req = _songRequestFactory.Create();
            req.Init(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo);
            if ((requestInfo.flags.HasFlag(CmdFlags.MoveToTop))) {
                RequestManager.RequestSongs.Insert(0, req);
            }
            else {
                RequestManager.RequestSongs.Add(req);
            }
            _requestManager.WriteRequest();

            Writedeck(requestor, "savedqueue"); // This can be used as a backup if persistent Queue is turned off.

            if (!requestInfo.flags.HasFlag(CmdFlags.SilentResult)) {
                _textFactory.Create().AddSong(ref song).QueueMessage(StringFormat.AddSongToQueueText.ToString());
            }
        }

        internal IEnumerator LoadOfflineDataBase(string id)
        {
            foreach (string directory in Directory.GetDirectories(RequestBotConfig.Instance.offlinepath, id + "*")) {
                MapDatabase.LoadCustomSongs(directory, id).Await(null, e => { Plugin.Log($"{e}"); }, null);
                Task.Delay(25).Wait();
                yield return new WaitWhile(() => MapDatabase.DatabaseLoading);
                // break;
            }
        }

        internal async Task ProcessSongRequest(int index, bool fromHistory = false)
        {
            if ((RequestManager.RequestSongs.Count > 0 && !fromHistory) || (RequestManager.HistorySongs.Count > 0 && fromHistory)) {
                SongRequest request = null;
                if (!fromHistory) {
                    Plugin.Log("Set status to request");
                    SetRequestStatus(index, RequestStatus.Played);
                    request = DequeueRequest(index);
                }
                else {
                    request = RequestManager.HistorySongs.ElementAt(index) as SongRequest;
                }

                if (request == null) {
                    Plugin.Log("Can't process a null request! Aborting!");
                    return;
                }
                else
                    Plugin.Log($"Processing song request {request._song["songName"].Value}");
                string songName = request._song["songName"].Value;
                string songIndex = Regex.Replace($"{request._song["id"].Value} ({request._song["songName"].Value} - {request._song["levelAuthor"].Value})", "[\\\\:*/?\"<>|]", "_");
                songIndex = Normalize.RemoveDirectorySymbols(ref songIndex); // Remove invalid characters.

                string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data\\CustomLevels", songIndex);
                string songHash = request._song["hash"].Value.ToUpper();


                // Check to see if level exists, download if not.

                // Replace with level check.
                //CustomLevel[] levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                //if (levels.Length == 0)

                //var rat = SongCore.Collections.levelIDsForHash(songHash);
                //bool mapexists = (rat.Count>0) && (rat[0] != "");

                if (Loader.GetLevelByHash(songHash) == null) {
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
                    string localPath = Path.Combine(Environment.CurrentDirectory, ".requestcache", $"{request._song["id"].Value}.zip");
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
                    var result = await WebClient.DownloadSong($"https://beatsaver.com{request._song["downloadURL"].Value}", System.Threading.CancellationToken.None, RequestBotListView.Instance._progress);
                    if (result == null) {
                        QueueChatMessage("BeatSaver is down now.");
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
                    Dispatcher.RunCoroutine(WaitForRefreshAndSchroll(request));
#if UNRELEASED
                        //if (!request.song.IsNull) // Experimental!
                        //{
                        //TwitchWebSocketClient.SendCommand("/marker "+ _textFactory.Create().AddUser(ref request.requestor).AddSong(request.song).Parse(NextSonglink.ToString()));
                        //}
#endif
#endif
                }
                else {
                    //Instance.QueueChatMessage($"Directory exists: {currentSongDirectory}");
                    Plugin.Log($"Song {songName} already exists!");
                    DismissRequest?.Invoke();
                    bool success = false;
                    Dispatcher.RunOnMainThread(() => DismissRequest?.Invoke());
                    Dispatcher.RunCoroutine(SongListUtils.ScrollToLevel(songHash, (s) =>
                    {
                        success = s;
                        UpdateRequestUI();
                    }, false));
                    if (!request._song.IsNull) {
                        // Display next song message
                        _textFactory.Create().AddUser(request._requestor).AddSong(request._song).QueueMessage(StringFormat.NextSonglink.ToString());
                    }
                }
            }
        }

        internal IEnumerator WaitForRefreshAndSchroll(SongRequest request)
        {
            yield return null;
            yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
            Loader.Instance.RefreshSongs(false);
            yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
            Utility.EmptyDirectory(".requestcache", true);
            Dispatcher.RunOnMainThread(() => DismissRequest?.Invoke());
            bool success = false;
            Dispatcher.RunCoroutine(SongListUtils.ScrollToLevel(request._song["hash"].Value.ToUpper(), (s) =>
            {
                success = s;
                UpdateRequestUI();
            }, false));
            RequestBotListView.Instance?.ChangeProgressText(0f);
            if (!request._song.IsNull) {
                // Display next song message
                _textFactory.Create().AddUser(request._requestor).AddSong(request._song).QueueMessage(StringFormat.NextSonglink.ToString());
            }
        }

        public void UpdateRequestUI(bool writeSummary = true)
        {
            Plugin.Log("start updateUI");
            try {
                if (writeSummary) {
                    WriteQueueSummaryToFile(); // Write out queue status to file, do it first
                }
                Dispatcher.RunOnMainThread(() =>
                {
                    try {
                        Plugin.Log("Invoke Change Color");
                        if (RequestManager.RequestSongs.Any()) {
                            ChangeButtonColor?.Invoke(Color.green);
                        }
                        else {
                            ChangeButtonColor?.Invoke(Color.red);
                        }
                    }
                    catch (Exception e) {
                        Plugin.Logger.Error(e);
                    }
                });
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }
            finally {
                Plugin.Log("end update UI");
            }
        }

        public void RefreshSongQuere()
        {
            this.RefreshListRequest?.Invoke(false);
            Dispatcher.RunOnMainThread(RequestBotListView.Instance.RefreshSongQueueList, false);
        }

        public void DequeueRequest(SongRequest request, bool updateUI = true)
        {
            Plugin.Log("start to deque request");
            try {
                if (request._status != RequestStatus.Wrongsong && request._status != RequestStatus.SongSearch) RequestManager.HistorySongs.Insert(0, request); // Wrong song requests are not logged into history, is it possible that other status states shouldn't be moved either?

                if (RequestManager.HistorySongs.Count > RequestBotConfig.Instance.RequestHistoryLimit) {
                    int diff = RequestManager.HistorySongs.Count - RequestBotConfig.Instance.RequestHistoryLimit;
                    RequestManager.HistorySongs.RemoveRange(RequestManager.HistorySongs.Count - diff - 1, diff);
                }
                RequestManager.RequestSongs.RemoveAt(RequestManager.RequestSongs.IndexOf(request));
                _requestManager.WriteHistory();
                
                HistoryManager.AddSong(request);
                _requestManager.WriteRequest();
                

                // Decrement the requestors request count, since their request is now out of the queue

                if (!RequestBotConfig.Instance.LimitUserRequestsToSession) {
                    if (RequestTracker.ContainsKey(request._requestor.Id)) RequestTracker[request._requestor.Id].numRequests--;
                }

                if (updateUI == false) return;
            }
            catch (Exception e) {
                Plugin.Log($"{e}");
            }
            finally {
                UpdateRequestUI();
                RefreshQueue = true;
                Plugin.Log("end Deque");
            }
        }

        public SongRequest DequeueRequest(int index, bool updateUI = true)
        {
            SongRequest request = RequestManager.RequestSongs.OfType<SongRequest>().ToList().ElementAt(index);

            if (request != null)
                DequeueRequest(request, updateUI);

#if UNRELEASED
            // If the queue is empty, Execute a custom command, the could be a chat message, a deck request, or nothing
            try
            {
                if (RequestBotConfig.Instance.RequestQueueOpen && updateUI == true && RequestManager.RequestSongs.Count == 0) RequestBot.listcollection.runscript("emptyqueue.script");
            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); }
#endif
            return request;
        }

        public void SetRequestStatus(int index, RequestStatus status, bool fromHistory = false)
        {
            if (!fromHistory)
                (RequestManager.RequestSongs[index] as SongRequest)._status = status;
            else
                (RequestManager.HistorySongs[index] as SongRequest)._status = status;
        }

        public void Blacklist(int index, bool fromHistory, bool skip)
        {
            // Add the song to the blacklist
            SongRequest request = fromHistory ? RequestManager.HistorySongs.OfType<SongRequest>().ToList().ElementAt(index) : RequestManager.RequestSongs.OfType<SongRequest>().ToList().ElementAt(index);

            ListCollectionManager.Add(banlist, request._song["id"].Value);

            QueueChatMessage($"{request._song["songName"].Value} by {request._song["authorName"].Value} ({request._song["id"].Value}) added to the blacklist.");

            if (!fromHistory) {
                if (skip)
                    Skip(index, RequestStatus.Blacklisted);
            }
            else
                SetRequestStatus(index, RequestStatus.Blacklisted, fromHistory);
        }

        public void Skip(int index, RequestStatus status = RequestStatus.Skipped)
        {
            // Set the final status of the request
            SetRequestStatus(index, status);

            // Then dequeue it
            DequeueRequest(index);

            UpdateRequestUI();
            RefreshSongQuere();
        }

        public void Process(int index, bool fromHistory)
        {
            ProcessSongRequest(index, fromHistory).Await(null, null, null);
        }

        public void Next()
        {
            ProcessSongRequest(0).Await(null, null, null); ;
        }


        public string GetBeatSaverId(string request)
        {
            request = Normalize.RemoveSymbols(ref request, Normalize._SymbolsNoDash);
            if (request != "360" && _digitRegex.IsMatch(request)) return request;
            if (_beatSaverRegex.IsMatch(request)) {
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


        public string AddToTop(ParseState state)
        {
            ParseState newstate = _stateFactory.Create().Setup(state); // Must use copies here, since these are all threads
            newstate._flags |= CmdFlags.MoveToTop | CmdFlags.NoFilter;
            newstate._info = "!ATT";
            return ProcessSongRequest(newstate);
        }

        public string ModAdd(ParseState state)
        {
            ParseState newstate = _stateFactory.Create().Setup(state); // Must use copies here, since these are all threads
            newstate._flags |= CmdFlags.NoFilter;
            newstate._info = "Unfiltered";
            return ProcessSongRequest(newstate);
        }


        public string ProcessSongRequest(ParseState state)
        {
            try {
                if (RequestBotConfig.Instance.RequestQueueOpen == false && !state._flags.HasFlag(CmdFlags.NoFilter) && !state._flags.HasFlag(CmdFlags.Local)) // BUG: Complex permission, Queue state message needs to be handled higher up
                {
                    QueueChatMessage($"Queue is currently closed.");
                    return success;
                }

                if (!RequestTracker.ContainsKey(state._user.Id))
                    RequestTracker.Add(state._user.Id, new RequestUserTracker());

                int limit = RequestBotConfig.Instance.UserRequestLimit;

                if (state._user is TwitchUser twitchUser) {
                    if (twitchUser.IsSubscriber) limit = Math.Max(limit, RequestBotConfig.Instance.SubRequestLimit);
                    if (state._user.IsModerator) limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                    if (twitchUser.IsVip) limit += RequestBotConfig.Instance.VipBonusRequests; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like
                }
                else {
                    if (state._user.IsModerator) limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                }

                if (!state._user.IsBroadcaster) {
                    if (RequestTracker[state._user.Id].numRequests >= limit) {
                        if (RequestBotConfig.Instance.LimitUserRequestsToSession) {
                            _textFactory.Create().Add("Requests", RequestTracker[state._user.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You've already used %Requests% requests this stream. Subscribers are limited to %RequestLimit%.");
                        }
                        else {
                            _textFactory.Create().Add("Requests", RequestTracker[state._user.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");
                        }

                        return success;
                    }
                }

                // BUG: Need to clean up the new request pipeline
                string testrequest = Normalize.RemoveSymbols(ref state._parameter, Normalize._SymbolsNoDash);

                RequestInfo newRequest = new RequestInfo(state._user, state._parameter, DateTime.UtcNow, _digitRegex.IsMatch(testrequest) || _beatSaverRegex.IsMatch(testrequest), state, state._flags, state._info);

                if (!newRequest.isBeatSaverId && state._parameter.Length < 2) {
                    QueueChatMessage($"Request \"{state._parameter}\" is too short- Beat Saver searches must be at least 3 characters!");
                }

                if (!UnverifiedRequestQueue.Contains(newRequest)) {
                    UnverifiedRequestQueue.Enqueue(newRequest);
                    this.ProcessRequestQueue();
                }
                return success;
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
                throw;
            }
            finally {
                this.RecevieRequest?.Invoke();
            }
        }


        public IChatUser SerchCreateChatUser()
        {
            if (this.TwitchService?.LoggedInUser != null) {
                return this.TwitchService?.LoggedInUser;
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
        public void Parse(IChatUser user, string request, CmdFlags flags = 0, string info = "")
        {
            if (string.IsNullOrEmpty(request)) {
                Plugin.Log($"request strings is null : {request}");
                return;
            }

            if (!string.IsNullOrEmpty(user.Id) && ListCollectionManager.Contains(_blockeduser, user.Id.ToLower())) {
                Plugin.Log($"Sender is contain blacklist : {user.UserName}");
                return;
            }

            // This will be used for all parsing type operations, allowing subcommands efficient access to parse state logic
            _stateFactory.Create().Setup(user, request, flags, info).ParseCommand().Await(result => { Plugin.Log("finish ParceCommand"); }, null, null);
        }

        public bool HasRights(ISRMCommand botcmd, IChatUser user, CmdFlags flags)
        {
            if (flags.HasFlag(CmdFlags.Local)) return true;
            if (botcmd.Flags.HasFlag(CmdFlags.Disabled)) return false;
            if (botcmd.Flags.HasFlag(CmdFlags.Everyone)) return true; // Not sure if this is the best approach actually, not worth thinking about right now
            if (user.IsModerator & RequestBotConfig.Instance.ModFullRights) return true;
            if (user.IsBroadcaster & botcmd.Flags.HasFlag(CmdFlags.Broadcaster)) return true;
            if (user.IsModerator & botcmd.Flags.HasFlag(CmdFlags.Mod)) return true;
            if (user is TwitchUser twitchUser && twitchUser.IsSubscriber & botcmd.Flags.HasFlag(CmdFlags.Sub)) return true;
            if (user is TwitchUser twitchUser1 && twitchUser1.IsVip & botcmd.Flags.HasFlag(CmdFlags.VIP)) return true;
            return false;
        }

        private void Instance_ReceiveMessege(string obj)
        {
            var message = new MessageEntity()
            {
                Message = obj
            };

            RecievedMessages(null, message);
        }

        #region ChatCommand
        // BUG: This one needs to be cleaned up a lot imo
        // BUG: This file needs to be split up a little, but not just yet... Its easier for me to move around in one massive file, since I can see the whole thing at once. 

        



        #region Utility functions

        public string Variable(ParseState state) // Basically show the value of a variable without parsing
        {
            QueueChatMessage(state._botcmd.UserParameter.ToString());
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
            ListCollectionManager.Runscript(request);
        }

        public TimeSpan GetFileAgeDifference(string filename)
        {
            DateTime lastModified = System.IO.File.GetLastWriteTime(filename);
            return DateTime.Now - lastModified;
        }
        #endregion

        #region Filter support functions

        public bool DoesContainTerms(string request, ref string[] terms)
        {
            if (request == "") return false;
            request = request.ToLower();

            foreach (string term in terms)
                foreach (string word in request.Split(' '))
                    if (word.Length > 2 && term.ToLower().Contains(word)) return true;

            return false;
        }


        bool IsNotModerator(IChatUser requestor, string message = "")
        {
            if (requestor.IsBroadcaster || requestor.IsModerator) return false;
            if (message != "") QueueChatMessage($"{message} is moderator only.");
            return true;
        }

        public bool Filtersong(JSONObject song)
        {
            string songid = song["id"].Value;
            if (IsInQueue(songid)) return true;
            if (ListCollectionManager.Contains(banlist, songid)) return true;
            if (ListCollectionManager.Contains(duplicatelist, songid)) return true;
            return false;
        }

        // Returns error text if filter triggers, or "" otherwise, "fast" version returns X if filter triggers



        public string SongSearchFilter(JSONObject song, bool fast = false, SongFilter filter = SongFilter.All) // BUG: This could be nicer
        {
            string songid = song["id"].Value;
            if (filter.HasFlag(SongFilter.Queue) && RequestManager.RequestSongs.OfType<SongRequest>().Any(req => req._song["version"] == song["version"])) return fast ? "X" : $"Request {song["songName"].Value} by {song["authorName"].Value} already exists in queue!";

            if (filter.HasFlag(SongFilter.Blacklist) && ListCollectionManager.Contains(banlist, songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is banned!";

            if (filter.HasFlag(SongFilter.Mapper) && Mapperfiltered(song, _mapperWhitelist)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} does not have a permitted mapper!";

            if (filter.HasFlag(SongFilter.Duplicate) && ListCollectionManager.Contains(duplicatelist, songid)) return fast ? "X" : $"{song["songName"].Value} by  {song["authorName"].Value} already requested this session!";

            if (ListCollectionManager.Contains(_whitelist, songid)) return "";

            if (filter.HasFlag(SongFilter.Duration) && song["songduration"].AsFloat > RequestBotConfig.Instance.MaximumSongLength * 60) return fast ? "X" : $"{song["songName"].Value} ({song["songlength"].Value}) by {song["authorName"].Value} ({song["version"].Value}) is too long!";

            if (filter.HasFlag(SongFilter.NJS) && song["njs"].AsInt < RequestBotConfig.Instance.MinimumNJS) return fast ? "X" : $"{song["songName"].Value} ({song["songlength"].Value}) by {song["authorName"].Value} ({song["version"].Value}) NJS ({song["njs"].Value}) is too low!";

            if (filter.HasFlag(SongFilter.Remap) && songremap.ContainsKey(songid)) return fast ? "X" : $"no permitted results found!";

            if (filter.HasFlag(SongFilter.Rating) && song["rating"].AsFloat < RequestBotConfig.Instance.LowestAllowedRating && song["rating"] != 0) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} is below {RequestBotConfig.Instance.LowestAllowedRating}% rating!";

            return "";
        }

        // checks if request is in the RequestManager.RequestSongs - needs to improve interface
        public string IsRequestInQueue(string request, bool fast = false)
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

        public string ClearDuplicateList(ParseState state)
        {
            if (!state._botcmd.Flags.HasFlag(CmdFlags.SilentResult)) QueueChatMessage("Session duplicate list is now clear.");
            ListCollectionManager.ClearList(duplicatelist);
            return success;
        }
        #endregion

        #region Ban/Unban Song
        //public void Ban(IChatUser requestor, string request)
        //{
        //    Ban(requestor, request, false);
        //}

        public async Task Ban(ParseState state)
        {
            var id = GetBeatSaverId(state._parameter.ToLower());

            if (ListCollectionManager.Contains(banlist, id)) {
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

            ListCollectionManager.Add(banlist, id);

            if (song == null) {
                QueueChatMessage($"{id} is now on the ban list.");
            }
            else {
                state.Msg(_textFactory.Create().AddSong(ref song.song).Parse(StringFormat.BanSongDetail), ", ");
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

        public void Unban(IChatUser requestor, string request)
        {
            var unbanvalue = GetBeatSaverId(request);

            if (ListCollectionManager.Contains(banlist, unbanvalue)) {
                QueueChatMessage($"Removed {request} from the ban list.");
                ListCollectionManager.Remove(banlist, unbanvalue);
            }
            else {
                QueueChatMessage($"{request} is not on the ban list.");
            }
        }
        #endregion

        #region Deck Commands
        public string Restoredeck(ParseState state)
        {
            return Readdeck(_stateFactory.Create().Setup(state, "savedqueue"));
        }

        public void Writedeck(IChatUser requestor, string request)
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

        public string Readdeck(ParseState state)
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
        public string DequeueSong(ParseState state)
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
        public void MapperAllowList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            mapperwhitelist = ListCollectionManager.OpenList(key); // BUG: this is still not the final interface
            QueueChatMessage($"Mapper whitelist set to {request}.");
        }

        public void MapperBanList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            mapperBanlist = ListCollectionManager.OpenList(key);
            //QueueChatMessage($"Mapper ban list set to {request}.");
        }

        public void WhiteList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            Whitelist = ListCollectionManager.OpenList(key);
        }

        public void BlockedUserList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            BlockedUser = ListCollectionManager.OpenList(key);
        }

        // Not super efficient, but what can you do
        public bool Mapperfiltered(JSONObject song, bool white)
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
            foreach (var item in Events) {
                try {
                    item.StopTimer();
                    item.Dispose();
                }
                catch (Exception e) {
                    Plugin.Logger.Error(e);
                }
            }
            Events.Clear();
            return success;
        }

        public string Every(ParseState state)
        {
            float period;

            string[] parts = state._parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out period)) return state.Error($"You must specify a time in minutes after {state._command}.");
            if (period < 1) return state.Error($"You must specify a period of at least 1 minute");
            Events.Add(new BotEvent(TimeSpan.FromMinutes(period), parts[1], true, (s, e) => ScheduledCommand(s, e)));
            return success;
        }

        public string EventIn(ParseState state)
        {
            float period;
            string[] parts = state._parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out period)) return state.Error($"You must specify a time in minutes after {state._command}.");
            if (period < 0) return state.Error($"You must specify a period of at least 0 minutes");
            Events.Add(new BotEvent(TimeSpan.FromMinutes(period), parts[1], false, (s, e) => ScheduledCommand(s, e)));
            return success;
        }
        public string Who(ParseState state)
        {

            var qm = this._messageFactroy.Create();
            
            var result = FindMatch(RequestManager.RequestSongs.OfType<SongRequest>(), state._parameter, qm);
            if (result == null) result = FindMatch(RequestManager.HistorySongs.OfType<SongRequest>(), state._parameter, qm);

            //if (result != null) QueueChatMessage($"{result.song["songName"].Value} requested by {result.requestor.displayName}.");
            if (result != null) qm.End("...");
            return "";
        }

        public string SongMsg(ParseState state)
        {
            string[] parts = state._parameter.Split(new char[] { ' ', ',' }, 2);
            var songId = GetBeatSaverId(parts[0]);
            if (songId == "") return state.Helptext(true);

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

        public IEnumerator SetBombState(ParseState state)
        {
            state._parameter = state._parameter.ToLower();

            if (state._parameter == "on") state._parameter = "enable";
            if (state._parameter == "off") state._parameter = "disable";

            if (state._parameter != "enable" && state._parameter != "disable") {
                state.Msg(state._botcmd.ShortHelp);
                yield break;
            }

            //System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state.parameter}"));

            System.Diagnostics.Process.Start($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state._parameter}");

            if (PluginManager.GetPlugin("WobbleSaber") != null) {
                string wobblestate = "off";
                if (state._parameter == "enable") wobblestate = "on";
                SendChatMessage($"!wadmin toggle {wobblestate} ");
            }

            state.Msg($"The !bomb command is now {state._parameter}d.");

            yield break;
        }


        public async Task AddsongsFromnewest(ParseState state)
        {
            int totalSongs = 0;

            string requestUrl = "https://beatsaver.com/api/maps/latest";

            //if (RequestBotConfig.Instance.OfflineMode) return;

            int offset = 0;

            ListCollectionManager.ClearList("latest.deck");

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

                            if (Mapperfiltered(song, true)) continue; // This forces the mapper filter
                            if (Filtersong(song)) continue;

                            if (state._flags.HasFlag(CmdFlags.Local)) QueueSong(state, song);
                            ListCollectionManager.Add("latest.deck", song["id"].Value);
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
                    RefreshQueue = true;
                }
            }
        }

        public async Task Makelistfromsearch(ParseState state)
        {
            int totalSongs = 0;

            var id = GetBeatSaverId(state._parameter);

            string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{Normalize.RemoveSymbols(ref state._parameter, Normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text";

            if (RequestBotConfig.Instance.OfflineMode) return;

            int offset = 0;

            ListCollectionManager.ClearList("search.deck");

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

                            if (Mapperfiltered(song, true)) continue; // This forces the mapper filter
                            if (Filtersong(song)) continue;

                            if (state._flags.HasFlag(CmdFlags.Local)) QueueSong(state, song);
                            ListCollectionManager.Add("search.deck", song["id"].Value);
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
                    RefreshQueue = true;
                }
            }
        }

        // General search version
        public async Task Addsongs(ParseState state)
        {

            var id = GetBeatSaverId(state._parameter);
            string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{Normalize.RemoveSymbols(ref state._parameter, Normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text/0?q={state._request}";

            string errorMessage = "";

            if (RequestBotConfig.Instance.OfflineMode) requestUrl = "";

            JSONNode result = null;

            if (!RequestBotConfig.Instance.OfflineMode) {
                var resp = await WebClient.GetAsync($"{requestUrl}/{Normalize.NormalizeBeatSaverString(state._parameter)}", System.Threading.CancellationToken.None);

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
            List<JSONObject> songs = GetSongListFromResults(result, state._parameter, ref errorMessage, filter, state._sort != "" ? state._sort : StringFormat.LookupSortOrder.ToString(), -1);

            foreach (JSONObject entry in songs) {
                QueueSong(state, entry);
            }

            UpdateRequestUI();
            RefreshSongQuere();
            RefreshQueue = true;
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

        public void MoveRequestToTop(IChatUser requestor, string request)
        {
            MoveRequestPositionInQueue(requestor, request, true);
        }

        public void MoveRequestToBottom(IChatUser requestor, string request)
        {
            MoveRequestPositionInQueue(requestor, request, false);
        }

        public void MoveRequestPositionInQueue(IChatUser requestor, string request, bool top)
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
                    RefreshQueue = true;

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
        public string QueueMessage(bool QueueState)
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

        public void ToggleQueue(IChatUser requestor, string request, bool state)
        {
            RequestBotConfig.Instance.RequestQueueOpen = state;
            RequestBotConfig.Instance.Save();

            QueueChatMessage(state ? "Queue is now open." : "Queue is now closed.");
            WriteQueueStatusToFile(QueueMessage(state));
            RefreshSongQuere();
            RefreshQueue = true;
        }
        public void WriteQueueSummaryToFile()
        {

            if (!RequestBotConfig.Instance.UpdateQueueStatusFiles) return;

            try {
                string statusfile = Path.Combine(Plugin.DataPath, "queuelist.txt");
                var queuesummary = new StringBuilder();
                int count = 0;

                foreach (SongRequest req in RequestManager.RequestSongs.ToArray()) {
                    var song = req._song;
                    queuesummary.Append(_textFactory.Create().AddSong(song).Parse(StringFormat.QueueTextFileFormat));  // Format of Queue is now user configurable

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

        public void WriteQueueStatusToFile(string status)
        {
            try {
                string statusfile = Path.Combine(Plugin.DataPath, "queuestatus.txt");
                File.WriteAllText(statusfile, status);
            }

            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }
        }

        public void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1) {
                n--;
                int k = Generator.Next(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public string QueueLottery(ParseState state)
        {
            Int32.TryParse(state._parameter, out int entrycount);

            Shuffle(RequestManager.RequestSongs);

            var list = RequestManager.RequestSongs.OfType<SongRequest>().ToList();
            for (int i = entrycount; i < list.Count; i++) {
                try {
                    if (RequestTracker.ContainsKey(list[i]._requestor.Id)) RequestTracker[list[i]._requestor.Id].numRequests--;
                    ListCollectionManager.Remove(duplicatelist, list[i]._song["id"]);
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
            RefreshQueue = true;
            return success;
        }

        public void Clearqueue(IChatUser requestor, string request)
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
            RefreshQueue = true;
        }

        #endregion

        #region Unmap/Remap Commands
        public void Remap(IChatUser requestor, string request)
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

        public void Unmap(IChatUser requestor, string request)
        {

            if (songremap.ContainsKey(request)) {
                QueueChatMessage($"Remap entry {request} removed.");
                songremap.Remove(request);
            }
            WriteRemapList();
        }

        public void WriteRemapList()
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

        public void ReadRemapList()
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
        public void WrongSong(IChatUser requestor, string request)
        {
            // Note: Scanning backwards to remove LastIn, for loop is best known way.
            for (int i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                var song = (RequestManager.RequestSongs[i] as SongRequest)._song;
                if ((RequestManager.RequestSongs[i] as SongRequest)._requestor.Id == requestor.Id) {
                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");

                    ListCollectionManager.Remove(duplicatelist, song["id"].Value);
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
                if (!song.IsNull) _textFactory.Create().AddSong(ref song).QueueMessage(StringFormat.LinkSonglink.ToString());
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }

            return success;
        }

        public string Queueduration()
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

        public string QueueStatus(ParseState state)
        {
            string queuestate = RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open. " : "Queue is closed. ";
            QueueChatMessage($"{queuestate} There are {RequestManager.RequestSongs.Count} maps ({Queueduration()}) in the queue.");
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
        #endregion

        #region ListManager
        public void Showlists(IChatUser requestor, string request)
        {
            var msg = _messageFactroy.Create();
            msg.Header("Loaded lists: ");
            foreach (var entry in ListCollectionManager.ListCollection) msg.Add($"{entry.Key} ({entry.Value.Count()})", ", ");
            msg.End("...", "No lists loaded.");
        }

        public string Listaccess(ParseState state)
        {
            QueueChatMessage($"Hi, my name is {state._botcmd.UserParameter} , and I'm a list object!");
            return success;
        }

        public void Addtolist(IChatUser requestor, string request)
        {
            string[] parts = request.Split(new char[] { ' ', ',' }, 2);
            if (parts.Length < 2) {
                QueueChatMessage("Usage text... use the official help method");
                return;
            }

            try {

                ListCollectionManager.Add(ref parts[0], ref parts[1]);
                QueueChatMessage($"Added {parts[1]} to {parts[0]}");

            }
            catch {
                QueueChatMessage($"list {parts[0]} not found.");
            }
        }

        public void ListList(IChatUser requestor, string request)
        {
            try {
                var list = ListCollectionManager.OpenList(request);

                var msg = _messageFactroy.Create();
                foreach (var entry in list.list) msg.Add(entry, ", ");
                msg.End("...", $"{request} is empty");
            }
            catch {
                QueueChatMessage($"{request} not found.");
            }
        }

        public void RemoveFromlist(IChatUser requestor, string request)
        {
            string[] parts = request.Split(new char[] { ' ', ',' }, 2);
            if (parts.Length < 2) {
                //     NewCommands[Addtolist].ShortHelp();
                QueueChatMessage("Usage text... use the official help method");
                return;
            }

            try {

                ListCollectionManager.Remove(ref parts[0], ref parts[1]);
                QueueChatMessage($"Removed {parts[1]} from {parts[0]}");

            }
            catch {
                QueueChatMessage($"list {parts[0]} not found.");
            }
        }

        public void ClearList(IChatUser requestor, string request)
        {
            try {
                ListCollectionManager.ClearList(request);
                QueueChatMessage($"{request} is cleared.");
            }
            catch {
                QueueChatMessage($"Unable to clear {request}");
            }
        }

        public void UnloadList(IChatUser requestor, string request)
        {
            try {
                ListCollectionManager.ListCollection.Remove(request.ToLower());
                QueueChatMessage($"{request} unloaded.");
            }
            catch {
                QueueChatMessage($"Unable to unload {request}");
            }
        }

        #region LIST MANAGER user interface

        public void Writelist(IChatUser requestor, string request)
        {

        }

        // Add list to queue, filtered by InQueue and duplicatelist
        public string Queuelist(ParseState state)
        {
            try {
                StringListManager list = ListCollectionManager.OpenList(state._parameter);
                foreach (var entry in list.list) ProcessSongRequest(_stateFactory.Create().Setup(state, entry)); // Must use copies here, since these are all threads
            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
            return success;
        }

        // Remove entire list from queue
        public string Unqueuelist(ParseState state)
        {
            state._flags |= FlagParameter.Silent;
            foreach (var entry in ListCollectionManager.OpenList(state._parameter).list) {
                state._parameter = entry;
                DequeueSong(state);
            }
            return success;
        }





        #endregion


        #region List Manager Related functions ...
        // List types:

        // This is a work in progress. 

        // .deck = lists of songs
        // .mapper = mapper lists
        // .users = twitch user lists
        // .command = command lists = linear scripting
        // .dict = list contains key value pairs
        // .json = (not part of list manager.. yet)

        // This code is currently in an extreme state of flux. Underlying implementation will change.

        public void OpenList(IChatUser requestor, string request)
        {
            ListCollectionManager.OpenList(request.ToLower());
        }

        public List<JSONObject> ReadJSON(string path)
        {
            List<JSONObject> objs = new List<JSONObject>();
            if (File.Exists(path)) {
                JSONNode json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull) {
                    foreach (JSONObject j in json.AsArray)
                        objs.Add(j);
                }
            }
            return objs;
        }

        public void WriteJSON(string path, List<JSONObject> objs)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            JSONArray arr = new JSONArray();
            foreach (JSONObject obj in objs)
                arr.Add(obj);

            File.WriteAllText(path, arr.ToString());
        }
        #endregion
        #endregion

        #region Utilties
        public void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories()) {
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            }

            foreach (FileInfo file in source.GetFiles()) {
                string newFilePath = Path.Combine(target.FullName, file.Name);
                try {
                    file.CopyTo(newFilePath);
                }
                catch (Exception) {
                }
            }
        }

        public string BackupStreamcore(ParseState state)
        {
            string errormsg = Backup();
            if (errormsg == "") state.Msg("SRManager files backed up.");
            return errormsg;
        }
        public string Backup()
        {
            DateTime Now = DateTime.Now;
            string BackupName = Path.Combine(RequestBotConfig.Instance.backuppath, $"SRMBACKUP-{Now.ToString("yyyy-MM-dd-HHmm")}.zip");

            Plugin.Log($"Backing up {Plugin.DataPath}");
            try {
                if (!Directory.Exists(RequestBotConfig.Instance.backuppath))
                    Directory.CreateDirectory(RequestBotConfig.Instance.backuppath);

                ZipFile.CreateFromDirectory(Plugin.DataPath, BackupName, System.IO.Compression.CompressionLevel.Fastest, true);
                RequestBotConfig.Instance.LastBackup = DateTime.Now.ToString();
                RequestBotConfig.Instance.Save();

                Plugin.Log($"Backup success writing {BackupName}");
                return success;
            }
            catch {

            }
            Plugin.Log($"Backup failed writing {BackupName}");
            return $"Failed to backup to {BackupName}";
        }
        #endregion

        #region SongDatabase
        public const int partialhash = 3; // Do Not ever set this below 4. It will cause severe performance loss

        // Song primary key can be song ID/version , or level hashes. This dictionary is many:1
        public bool CreateMD5FromFile(string path, out string hash)
        {
            hash = "";
            if (!File.Exists(path)) return false;
            using (MD5 md5 = MD5.Create()) {
                using (var stream = File.OpenRead(path)) {
                    byte[] hashBytes = md5.ComputeHash(stream);

                    StringBuilder sb = new StringBuilder();
                    foreach (byte hashByte in hashBytes) {
                        sb.Append(hashByte.ToString("X2"));
                    }

                    hash = sb.ToString();
                    return true;
                }
            }
        }






        public List<JSONObject> GetSongListFromResults(JSONNode result, string SearchString, ref string errorMessage, SongFilter filter = SongFilter.All, string sortby = "-rating", int reverse = 1)
        {
            List<JSONObject> songs = new List<JSONObject>();

            if (result != null) {
                // Add query results to out song database.
                if (result["docs"].IsArray) {
                    var downloadedsongs = result["docs"].AsArray;
                    for (int i = 0; i < downloadedsongs.Count; i++) new SongMap(downloadedsongs[i].AsObject);

                    foreach (JSONObject currentSong in result["docs"].AsArray) {
                        new SongMap(currentSong);
                    }
                }
                else {
                    new SongMap(result.AsObject);
                }
            }

            var list = MapDatabase.Search(SearchString);

            try {
                string[] sortorder = sortby.Split(' ');

                list.Sort(delegate (SongMap c1, SongMap c2)
                {
                    return reverse * CompareSong(c1.song, c2.song, ref sortorder);
                });
            }
            catch (Exception e) {
                //QueueChatMessage($"Exception {e} sorting song list");
                Plugin.Log($"Exception sorting a returned song list. {e.ToString()}");
            }

            foreach (var song in list) {
                errorMessage = SongSearchFilter(song.song, false, filter);
                if (errorMessage == "") songs.Add(song.song);
            }

            return songs;
        }

        public IEnumerator RefreshSongs(ParseState state)
        {

            MapDatabase.LoadCustomSongs().Await(null, null, null);
            yield break;
        }

        public string GetGCCount(ParseState state)
        {
            state.Msg($"Gc0:{GC.CollectionCount(0)} GC1:{GC.CollectionCount(1)} GC2:{GC.CollectionCount(2)}");
            state.Msg($"{GC.GetTotalMemory(false)}");
            return success;
        }


        public IEnumerator ReadArchive(ParseState state)
        {

            MapDatabase.LoadZIPDirectory();
            yield break;
        }

        public IEnumerator SaveSongDatabase(ParseState state)
        {
            MapDatabase.SaveDatabase();
            yield break;
        }


        /*

         public string GetIdentifier()
         {
             var combinedJson = "";
             foreach (var diffLevel in difficultyLevels)
             {
                 if (!File.Exists(path + "/" + diffLevel.jsonPath))
                 {
                     continue;
                 }

                 diffLevel.json = File.ReadAllText(path + "/" + diffLevel.jsonPath);
                 combinedJson += diffLevel.json;
             }

             var hash = Utils.CreateMD5FromString(combinedJson);
             levelId = hash + "∎" + string.Join("∎", songName, songSubName, GetSongAuthor(), beatsPerMinute.ToString()) + "∎";
             return levelId;
         }

         public static string GetLevelID(Song song)
         {
             string[] values = new string[] { song.hash, song.songName, song.songSubName, song.authorName, song.beatsPerMinute };
             return string.Join("∎", values) + "∎";
         }

         public static BeatmapLevelSO GetLevel(string levelId)
         {
             return SongLoader.CustomLevelCollectionSO.beatmapLevels.FirstOrDefault(x => x.levelID == levelId) as BeatmapLevelSO;
         }

         public static bool CreateMD5FromFile(string path, out string hash)
         {
             hash = "";
             if (!File.Exists(path)) return false;
             using (MD5 md5 = MD5.Create())
             {
                 using (var stream = File.OpenRead(path))
                 {
                     byte[] hashBytes = md5.ComputeHash(stream);

                     StringBuilder sb = new StringBuilder();
                     foreach (byte hashByte in hashBytes)
                     {
                         sb.Append(hashByte.ToString("X2"));
                     }

                     hash = sb.ToString();
                     return true;
                 }
             }
         }

         public void RequestSongByLevelID(string levelId, Action<Song> callback)
         {
             StartCoroutine(RequestSongByLevelIDCoroutine(levelId, callback));
         }

         // Beatsaver.com filtered characters
         '@', '*', '+', '-', '<', '~', '>', '(', ')'



         */

        public string CreateMD5FromString(string input)
        {
            // Use input string to calculate MD5 hash
            using (var md5 = MD5.Create()) {
                var inputBytes = Encoding.ASCII.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++) {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }


        //SongLoader.Instance.RemoveSongWithLevelID(level.levelID);
        //SongLoader.CustomLevelCollectionSO.beatmapLevels.FirstOrDefault(x => x.levelID == levelId) as BeatmapLevelSO;

        public static ConcurrentDictionary<String, float> ppmap = new ConcurrentDictionary<string, float>();
        public static bool pploading = false;

        public async Task GetPPData()
        {
            if (pploading) {
                Plugin.Log("PPloaded");
                return;
            }

            pploading = true;

            //Instance.QueueChatMessage("Getting PP Data");
            //var StarTime = DateTime.UtcNow;
            string requestUrl = "https://cdn.wes.cloud/beatstar/bssb/v2-ranked.json";
            //public const String SCRAPED_SCORE_SABER_ALL_JSON_URL = "https://cdn.wes.cloud/beatstar/bssb/v2-all.json";

            string result;

            System.Globalization.NumberStyles style = System.Globalization.NumberStyles.AllowDecimalPoint;
            var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

            if (resp.IsSuccessStatusCode) {
                result = resp.ContentToString();
            }
            else {
                Plugin.Log("Failed to get pp");
                pploading = false;
                return;
            }

            //Instance.QueueChatMessage($"Parsing PP Data {result.Length}");

            JSONNode rootNode = JSON.Parse(result);

            ListCollectionManager.ClearList("pp.deck");

            foreach (KeyValuePair<string, JSONNode> kvp in rootNode) {
                JSONNode difficultyNodes = kvp.Value;

                float maxpp = 0;
                float maxstar = 0;

                //Instance.QueueChatMessage($"{kvp.Value}");

                var id = difficultyNodes["key"];

                //Instance.QueueChatMessage($"{id}");

                foreach (var innerKvp in difficultyNodes["diffs"]) {
                    JSONNode node = innerKvp.Value;

                    //Instance.QueueChatMessage($"{node}");

                    float.TryParse(node["pp"], style, System.Globalization.CultureInfo.InvariantCulture, out float pp);
                    if (pp > maxpp) maxpp = pp;

                    float.TryParse(node["star"], style, System.Globalization.CultureInfo.InvariantCulture, out float starDifficulty);
                    if (starDifficulty > maxstar) maxstar = starDifficulty;
                }

                if (maxpp > 0) {
                    //Instance.QueueChatMessage($"{id} = {maxpp}");

                    ppmap.TryAdd(id, (int)(maxpp));

                    if (id != "" && maxpp > 200) ListCollectionManager.Add("pp.deck", id);

                    if (MapDatabase.MapLibrary.TryGetValue(id, out SongMap map)) {
                        map.pp = (int)(maxpp);
                        map.song.Add("pp", maxpp);
                        map.IndexSong(map.song);

                    }
                }
            }
            Parse(SerchCreateChatUser(), "!deck pp", CmdFlags.Local);

            QueueChatMessage("PP Data indexed");
            pploading = false;
        }
        #endregion
    }
}