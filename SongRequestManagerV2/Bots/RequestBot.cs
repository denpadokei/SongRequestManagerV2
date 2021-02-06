using ChatCore.Interfaces;
using ChatCore.Models.Twitch;
using ChatCore.Utilities;
using IPA.Loader;
using SongRequestManagerV2.Bases;
using SongRequestManagerV2.Extentions;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Networks;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
#if OLDVERSION
using TMPro;
#endif

namespace SongRequestManagerV2.Bots
{
    internal class RequestBot : BindableBase, IRequestBot, IInitializable, IDisposable
    {
        public static Dictionary<string, RequestUserTracker> RequestTracker { get; } = new Dictionary<string, RequestUserTracker>();
        //private ChatServiceMultiplexer _chatService { get; set; }

        //SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        //synthesizer.Volume = 100;  // 0...100
        //    synthesizer.Rate = -2;     // -10...10
        public bool RefreshQueue { get; private set; } = false;
        bool _mapperWhitelist = false; // BUG: Need to clean these up a bit.
        bool _isGameCore = false;

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

        public const string SCRAPED_SCORE_SABER_ALL_JSON_URL = "https://cdn.wes.cloud/beatstar/bssb/v2-ranked.json";

        private readonly Timer timer = new Timer(500);

        [Inject]
        public StringNormalization Normalize { get; private set; }
        [Inject]
        public MapDatabase MapDatabase { get; private set; }
        [Inject]
        public ListCollectionManager ListCollectionManager { get; private set; }
        [Inject]
        public IChatManager ChatManager { get; }
        [Inject]
        RequestManager _requestManager;
        [Inject]
        NotifySound notifySound;
        [Inject]
        QueueLongMessage.QueueLongMessageFactroy _messageFactroy;
        [Inject]
        SongRequest.SongRequestFactory _songRequestFactory;
        [Inject]
        DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        ParseState.ParseStateFactory _stateFactory;
        [Inject]
        SongMap.SongMapFactory _songMapFactory;

        public static string playedfilename = "";
        public event Action ReceviedRequest;
        public event Action<bool> RefreshListRequest;
        public event Action<bool> UpdateUIRequest;
        public event Action<bool> SetButtonIntactivityRequest;
        public event Action ChangeButtonColor;

        /// <summary>SongRequest を取得、設定</summary>
        private SongRequest currentSong_;
        /// <summary>SongRequest を取得、設定</summary>
        public SongRequest CurrentSong
        {
            get => this.currentSong_;

            set => this.SetProperty(ref this.currentSong_, value);
        }

        /// <summary>
        /// This is string empty.
        /// </summary>
        const string success = "";
        const string endcommand = "X";
        const string notsubcommand = "NotSubcmd";

        #region 構築・破棄
        [Inject]
        async void Constractor()
        {
            Logger.Debug("Constractor call");
            if (RequestBotConfig.Instance.PPSearch) await GetPPData(); // Start loading PP data
            this.Setup();
            Logger.Debug("try load database");
            MapDatabase.LoadDatabase();
            Logger.Debug("end load database");
            if (RequestBotConfig.Instance.LocalSearch) _ = MapDatabase.LoadCustomSongs(); // This is a background process
        }
        public void Initialize()
        {
            Logger.Debug("Start Initialize");
            RequestBotConfig.Instance.Save(true);
            Logger.Debug("Awake call");
            SceneManager.activeSceneChanged += this.SceneManager_activeSceneChanged;
            this.timer.Elapsed += this.Timer_Elapsed;
            this.timer.Start();
            Logger.Debug("End Initialize");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                    Logger.Debug("Dispose call");
                    this.timer.Elapsed -= this.Timer_Elapsed;
                    this.timer.Dispose();
                    SceneManager.activeSceneChanged -= this.SceneManager_activeSceneChanged;
                    try {
                        if (BouyomiPipeline.instance != null) {
                            BouyomiPipeline.instance.ReceiveMessege -= this.Instance_ReceiveMessege;
                        }
                    }
                    catch (Exception e) {
                        Logger.Error(e);
                    }
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~RequestBot()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion



        public void Newest(KEYBOARD.KEY key)
        {
            ClearSearches();
            Parse(GetLoginUser(), $"!addnew/top", CmdFlags.Local);
        }

        public void Search(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            ClearSearches();
            Parse(GetLoginUser(), $"!addsongs/top {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void MSD(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            ClearSearches();
            Parse(GetLoginUser(), $"!makesearchdeck {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void UnfilteredSearch(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            ClearSearches();
            Parse(GetLoginUser(), $"!addsongs/top/mod {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void ClearSearches()
        {
            foreach (var item in RequestManager.RequestSongs) {
                if (item is SongRequest request && request._status == RequestStatus.SongSearch) {
                    DequeueRequest(request, false);
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

        internal void RecievedMessages(IChatMessage msg)
        {
            Logger.Debug($"Received Message : {msg.Message}");
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            Parse(msg.Sender, msg.Message.Replace("！", "!"));
#if DEBUG
            stopwatch.Stop();
            Logger.Debug($"{stopwatch.ElapsedMilliseconds} ms");
#endif
        }

        internal void OnConfigChangedEvent(RequestBotConfig config)
        {
            UpdateRequestUI();
            this.UpdateUIRequest?.Invoke(true);
            this.SetButtonIntactivityRequest?.Invoke(true);
        }

        // BUG: Prototype code, used for testing.


        public void ScheduledCommand(string command, System.Timers.ElapsedEventArgs e)
        {
            Parse(GetLoginUser(), command);
        }

        public void RunStartupScripts()
        {
            ReadRemapList(); // BUG: This should use list manager

            MapperBanList(GetLoginUser(), "mapperban.list");
            WhiteList(GetLoginUser(), "whitelist.unique");
            BlockedUserList(GetLoginUser(), "blockeduser.unique");

#if UNRELEASED
            OpenList(SerchCreateChatUser(), "mapper.list"); // Open mapper list so we can get new songs filtered by our favorite mappers.
            MapperAllowList(SerchCreateChatUser(), "mapper.list");
            accesslist("mapper.list");

            loaddecks(SerchCreateChatUser(), ""); // Load our default deck collection
            // BUG: Command failure observed once, no permission to use /chatcommand. Possible cause: OurIChatUser isn't authenticated yet.

            RunScript(SerchCreateChatUser(), "startup.script"); // Run startup script. This can include any bot commands.
#endif
        }

        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            this._isGameCore = arg1.name == "GameCore";
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (RequestBotConfig.Instance.PerformanceMode && this._isGameCore) {
                return;
            }
            this.timer.Stop();
            try {
                if (this.ChatManager.RequestInfos.TryDequeue(out var requestInfo)) {
                    await CheckRequest(requestInfo);
                    UpdateRequestUI();
                    RefreshSongQuere();
                    RefreshQueue = true;
                }
                else if (this.ChatManager.RecieveChatMessage.TryDequeue(out var chatMessage)) {
                    this.RecievedMessages(chatMessage);
                }
                else if (this.ChatManager.SendMessageQueue.TryDequeue(out var message)) {
                    this.SendChatMessage(message);
                }
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }
            finally {
                this.timer.Start();
            }
        }

        void Setup()
        {
#if UNRELEASED
            var startingmem = GC.GetTotalMemory(true);

            //var folder = Path.Combine(Environment.CurrentDirectory, "userdata","streamcore");

           //List<FileInfo> files = new List<FileInfo>();  // List that will hold the files and subfiles in path
            //List<DirectoryInfo> folders = new List<DirectoryInfo>(); // List that hold direcotries that cannot be accessed

            //DirectoryInfo di = new DirectoryInfo(folder);

            //Dictionary<string, string> remap = new Dictionary<string, string>();
        
            //foreach (var entry in listcollection.OpenList("all.list").list) 
            //    {
            //    //Instance.this.ChatManager.QueueChatMessage($"Map {entry}");

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
            //            //Instance.this.ChatManager.QueueChatMessage($"Map {remapparts[0]} : {o.ToString("x")}");
            //        }
            //    }
            //}

            //Instance.this.ChatManager.QueueChatMessage($"Scanning lists");

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
            //                    //Instance.this.ChatManager.QueueChatMessage($"{List[i]} : {remap[List[i]]}");
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
            Logger.Debug("create playd path");
            try {
                string filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
                if (Directory.Exists(filesToDelete)) {
                    Logger.Debug("files delete");
                    Utility.EmptyDirectory(filesToDelete);
                }

                try {
                    if (!DateTime.TryParse(RequestBotConfig.Instance.LastBackup, out var LastBackup)) LastBackup = DateTime.MinValue;
                    TimeSpan TimeSinceBackup = DateTime.Now - LastBackup;
                    if (TimeSinceBackup > TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) {
                        Logger.Debug("try buck up");
                        Backup();
                        Logger.Debug("end buck up");
                    }
                }
                catch (Exception ex) {
                    Logger.Debug(ex.ToString());
                    this.ChatManager.QueueChatMessage("Failed to run Backup");

                }

                try {
                    TimeSpan PlayedAge = Utility.GetFileAgeDifference(playedfilename);
                    if (PlayedAge < TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) Played = ReadJSON(playedfilename); // Read the songsplayed file if less than x hours have passed 
                }
                catch (Exception ex) {
                    Logger.Debug(ex.ToString());
                    this.ChatManager.QueueChatMessage("Failed to clear played file");

                }
                _requestManager.ReadRequest(); // Might added the timespan check for this too. To be decided later.
                _requestManager.ReadHistory();
                ListCollectionManager.OpenList("banlist.unique");

#if UNRELEASED
            //GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            //GC.Collect();
            //Instance.this.ChatManager.QueueChatMessage($"hashentries: {SongMap.hashcount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");
#endif

                ListCollectionManager.ClearOldList("duplicate.list", TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours));

                UpdateRequestUI();
                RequestBotConfig.Instance.ConfigChangedEvent += OnConfigChangedEvent;
            }
            catch (Exception ex) {
                Logger.Debug(ex.ToString());
                this.ChatManager.QueueChatMessage(ex.ToString());
            }

            WriteQueueSummaryToFile();
            WriteQueueStatusToFile(QueueMessage(RequestBotConfig.Instance.RequestQueueOpen));

            if (RequestBotConfig.Instance.IsStartServer) {
                BouyomiPipeline.instance.ReceiveMessege -= this.Instance_ReceiveMessege;
                BouyomiPipeline.instance.ReceiveMessege += this.Instance_ReceiveMessege;
                BouyomiPipeline.instance.Start();
            }
            else {
                BouyomiPipeline.instance.ReceiveMessege -= this.Instance_ReceiveMessege;
                BouyomiPipeline.instance.Stop();
            }
        }


        // if (!silence) this.ChatManager.QueueChatMessage($"{request.Key.song["songName"].Value}/{request.Key.song["authorName"].Value} ({songId}) added to the blacklist.");
        private void SendChatMessage(string message)
        {
            try {
                Logger.Debug($"Sending message: \"{message}\"");

                if (this.ChatManager.TwitchService != null) {
                    foreach (var channel in this.ChatManager.TwitchService.Channels) {
                        this.ChatManager.TwitchService.SendTextMessage($"{message}", channel.Value);
                    }
                }
                Logger.Debug("Finish send chat message");
            }
            catch (Exception e) {
                Logger.Debug($"Exception was caught when trying to send bot message. {e}");
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

                        //this.ChatManager.QueueChatMessage($"{song2[sortby].AsFloat} < {song1[sortby].AsFloat}");
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

                    this.ChatManager.QueueChatMessage($"{result.AsObject}");

                    if (result != null && result["id"].Value != "") {
                        _songMapFactory.Create(result.AsObject);
                    }
                }
            }, null, null);
        }

        // BUG: Testing major changes. This will get seriously refactored soon.
        internal async Task CheckRequest(RequestInfo requestInfo)
        {
#if DEBUG
            Logger.Debug("Start CheckRequest");
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            var requestor = requestInfo.requestor;
            var request = requestInfo.request;

            var normalrequest = Normalize.NormalizeBeatSaverString(requestInfo.request);

            var id = GetBeatSaverId(Normalize.RemoveSymbols(ref request, Normalize._SymbolsNoDash));
            try {
                if (id != "") {
                    // Remap song id if entry present. This is one time, and not correct as a result. No recursion right now, could be confusing to the end user.
                    if (songremap.ContainsKey(id) && !requestInfo.flags.HasFlag(CmdFlags.NoFilter)) {
                        request = songremap[id];
                        this.ChatManager.QueueChatMessage($"Remapping request {requestInfo.request} to {request}");
                    }

                    string requestcheckmessage = IsRequestInQueue(Normalize.RemoveSymbols(ref request, Normalize._SymbolsNoDash));               // Check if requested ID is in Queue  
                    if (requestcheckmessage != "") {
                        this.ChatManager.QueueChatMessage(requestcheckmessage);
                        return;
                    }

                    if (RequestBotConfig.Instance.OfflineMode && RequestBotConfig.Instance.offlinepath != "" && !MapDatabase.MapLibrary.ContainsKey(id)) {
                        Dispatcher.RunCoroutine(this.LoadOfflineDataBase(id));
                    }
                }

                JSONNode result = null;

                var errorMessage = "";

                // Get song query results from beatsaver.com
                if (!RequestBotConfig.Instance.OfflineMode) {
                    var requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{Normalize.RemoveSymbols(ref request, Normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text/0?q={normalrequest}";
#if DEBUG
                    Logger.Debug($"Start get map detial : {stopwatch.ElapsedMilliseconds} ms");
#endif
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

                var filter = SongFilter.All;
                if (requestInfo.flags.HasFlag(CmdFlags.NoFilter)) filter = SongFilter.Queue;
                var songs = GetSongListFromResults(result, request, ref errorMessage, filter, requestInfo.state._sort != "" ? requestInfo.state._sort : StringFormat.AddSortOrder.ToString());

                var autopick = RequestBotConfig.Instance.AutopickFirstSong || requestInfo.flags.HasFlag(CmdFlags.Autopick);

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
                    this.ChatManager.QueueChatMessage(errorMessage);
                    return;
                }
                var song = songs[0];
                RequestTracker[requestor.Id].numRequests++;
                ListCollectionManager.Add(duplicatelist, song["id"].Value);
                var req = _songRequestFactory.Create();
                req.Init(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo);
                if (RequestBotConfig.Instance.NotifySound) {
                    notifySound.PlaySound();
                }
                if ((requestInfo.flags.HasFlag(CmdFlags.MoveToTop))) {
                    var reqs = new List<object>() { req };
                    var newList = reqs.Union(RequestManager.RequestSongs.ToArray());
                    RequestManager.RequestSongs.Clear();
                    RequestManager.RequestSongs.AddRange(newList);
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
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
#if DEBUG
                stopwatch.Stop();
                Logger.Debug($"Finish CheckRequest : {stopwatch.ElapsedMilliseconds} ms");
#endif
            }
        }

        internal IEnumerator LoadOfflineDataBase(string id)
        {
            foreach (string directory in Directory.GetDirectories(RequestBotConfig.Instance.offlinepath, id + "*")) {
                MapDatabase.LoadCustomSongs(directory, id).Await(null, e => { Logger.Debug($"{e}"); }, null);
                Task.Delay(25).Wait();
                yield return new WaitWhile(() => MapDatabase.DatabaseLoading);
                // break;
            }
        }
        public void UpdateRequestUI(bool writeSummary = true)
        {
            Logger.Debug("start updateUI");
            try {
                if (writeSummary) {
                    WriteQueueSummaryToFile(); // Write out queue status to file, do it first
                }
                Dispatcher.RunOnMainThread(() =>
                {
                    try {
                        Logger.Debug("Invoke Change Color");
                        ChangeButtonColor?.Invoke();
                    }
                    catch (Exception e) {
                        Logger.Error(e);
                    }
                });
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }
            finally {
                Logger.Debug("end update UI");
            }
        }

        public void RefreshSongQuere()
        {

            Dispatcher.RunOnMainThread(() =>
            {
                this.RefreshListRequest?.Invoke(false);
                this.RefreshQueue = true;
            });
        }

        public void DequeueRequest(SongRequest request, bool updateUI = true)
        {
            Logger.Debug("start to deque request");
            try {
                if (request._status != RequestStatus.Wrongsong && request._status != RequestStatus.SongSearch) {
                    var reqs = new List<object>() { request };
                    var newList = reqs.Union(RequestManager.HistorySongs.ToArray());
                    RequestManager.HistorySongs.Clear();
                    RequestManager.HistorySongs.AddRange(newList);
                    // Wrong song requests are not logged into history, is it possible that other status states shouldn't be moved either?
                }


                if (RequestManager.HistorySongs.Count > RequestBotConfig.Instance.RequestHistoryLimit) {
                    int diff = RequestManager.HistorySongs.Count - RequestBotConfig.Instance.RequestHistoryLimit;
                    var songs = RequestManager.HistorySongs.ToList();
                    songs.RemoveRange(RequestManager.HistorySongs.Count - diff - 1, diff);
                    RequestManager.HistorySongs.Clear();
                    RequestManager.HistorySongs.AddRange(songs);
                }
                var requests = RequestManager.RequestSongs.ToList();
                requests.Remove(request);
                RequestManager.RequestSongs.Clear();
                RequestManager.RequestSongs.AddRange(requests);
                _requestManager.WriteHistory();

                HistoryManager.AddSong(request);
                _requestManager.WriteRequest();

                this.CurrentSong = null;


                // Decrement the requestors request count, since their request is now out of the queue

                if (!RequestBotConfig.Instance.LimitUserRequestsToSession) {
                    if (RequestTracker.ContainsKey(request._requestor.Id)) RequestTracker[request._requestor.Id].numRequests--;
                }

                if (updateUI == false) return;
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
            }
            finally {
                UpdateRequestUI();
                RefreshQueue = true;
                Logger.Debug("end Deque");
            }
        }

        public void SetRequestStatus(SongRequest request, RequestStatus status, bool fromHistory = false)
        {
            request._status = status;
        }

        public void Blacklist(SongRequest request, bool fromHistory, bool skip)
        {
            // Add the song to the blacklist
            ListCollectionManager.Add(banlist, request._song["id"].Value);

            this.ChatManager.QueueChatMessage($"{request._song["songName"].Value} by {request._song["authorName"].Value} ({request._song["id"].Value}) added to the blacklist.");

            if (!fromHistory) {
                if (skip)
                    Skip(request, RequestStatus.Blacklisted);
            }
            else
                SetRequestStatus(request, RequestStatus.Blacklisted, fromHistory);
        }

        public void Skip(SongRequest request, RequestStatus status = RequestStatus.Skipped)
        {
            // Set the final status of the request
            SetRequestStatus(request, status);

            // Then dequeue it
            DequeueRequest(request);

            UpdateRequestUI();
            RefreshSongQuere();
        }
        public string GetBeatSaverId(string request)
        {
            request = Normalize.RemoveSymbols(ref request, Normalize._SymbolsNoDash);
            if (request != "360" && _digitRegex.IsMatch(request)) return request;
            if (_beatSaverRegex.IsMatch(request)) {
                string[] requestparts = request.Split(new char[] { '-' }, 2);
                //return requestparts[0];
                Int32.TryParse(requestparts[1], out var o);
                {
                    //Instance.this.ChatManager.QueueChatMessage($"key={o.ToString("x")}");
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
                    this.ChatManager.QueueChatMessage($"Queue is currently closed.");
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

                if (!state._user.IsBroadcaster && RequestTracker[state._user.Id].numRequests >= limit) {
                    if (RequestBotConfig.Instance.LimitUserRequestsToSession) {
                        _textFactory.Create().Add("Requests", RequestTracker[state._user.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You've already used %Requests% requests this stream. Subscribers are limited to %RequestLimit%.");
                    }
                    else {
                        _textFactory.Create().Add("Requests", RequestTracker[state._user.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");
                    }

                    return success;
                }

                // BUG: Need to clean up the new request pipeline
                string testrequest = Normalize.RemoveSymbols(ref state._parameter, Normalize._SymbolsNoDash);

                var newRequest = new RequestInfo(state._user, state._parameter, DateTime.UtcNow, _digitRegex.IsMatch(testrequest) || _beatSaverRegex.IsMatch(testrequest), state, state._flags, state._info);

                if (!newRequest.isBeatSaverId && state._parameter.Length < 2) {
                    this.ChatManager.QueueChatMessage($"Request \"{state._parameter}\" is too short- Beat Saver searches must be at least 3 characters!");
                }

                if (!this.ChatManager.RequestInfos.Contains(newRequest)) {
                    this.ChatManager.RequestInfos.Enqueue(newRequest);
                }
                return success;
            }
            catch (Exception ex) {
                Logger.Debug(ex.ToString());
                return ex.ToString();
            }
            finally {
                this.ReceviedRequest?.Invoke();
            }
        }


        public IChatUser GetLoginUser()
        {
            if (this.ChatManager.TwitchService?.LoggedInUser != null) {
                return this.ChatManager.TwitchService?.LoggedInUser;
            }
            else {
                var obj = new
                {
                    Id = "",
                    UserName = RequestBotConfig.Instance.LocalUserName,
                    DisplayName = RequestBotConfig.Instance.LocalUserName,
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
                Logger.Debug($"request strings is null : {request}");
                return;
            }

            if (!string.IsNullOrEmpty(user.Id) && ListCollectionManager.Contains(_blockeduser, user.Id.ToLower())) {
                Logger.Debug($"Sender is contain blacklist : {user.UserName}");
                return;
            }

            // This will be used for all parsing type operations, allowing subcommands efficient access to parse state logic
            _stateFactory.Create().Setup(user, request, flags, info).ParseCommand();
        }
        private void Instance_ReceiveMessege(string obj)
        {
            var message = new MessageEntity()
            {
                Message = obj
            };

            RecievedMessages(message);
        }

        #region ChatCommand
        // BUG: This one needs to be cleaned up a lot imo
        // BUG: This file needs to be split up a little, but not just yet... Its easier for me to move around in one massive file, since I can see the whole thing at once. 





        #region Utility functions
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
                dt.AddSong((RequestManager.HistorySongs.FirstOrDefault() as SongRequest)._song); // Exposing the current song 
            }
            catch (Exception ex) {
                Logger.Debug(ex.ToString());
            }

            dt.QueueMessage(state._parameter);
            return success;
        }

        public void RunScript(IChatUser requestor, string request)
        {
            ListCollectionManager.Runscript(request);
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
            if (message != "") this.ChatManager.QueueChatMessage($"{message} is moderator only.");
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
            if (!state._botcmd.Flags.HasFlag(CmdFlags.SilentResult)) this.ChatManager.QueueChatMessage("Session duplicate list is now clear.");
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
                this.ChatManager.QueueChatMessage($"{id} is already on the ban list.");
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
                        Logger.Debug($"Ban: Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    }
                }

                if (result != null) song = _songMapFactory.Create(result.AsObject);
            }

            ListCollectionManager.Add(banlist, id);

            if (song == null) {
                this.ChatManager.QueueChatMessage($"{id} is now on the ban list.");
            }
            else {
                state.Msg(_textFactory.Create().AddSong(song.Song).Parse(StringFormat.BanSongDetail), ", ");
            }
        }

        //public void Ban(IChatUser requestor, string request, bool silence)
        //{
        //    if (isNotModerator(requestor)) return;

        //    var songId = GetBeatSaverId(request);
        //    if (songId == "" && !silence)
        //    {
        //        this.ChatManager.QueueChatMessage($"usage: !block <songid>, omit <>'s.");
        //        return;
        //    }

        //    if (listcollection.contains(ref banlist,songId) && !silence)
        //    {
        //        this.ChatManager.QueueChatMessage($"{request} is already on the ban list.");
        //    }
        //    else
        //    {

        //        listcollection.add(banlist, songId);
        //        this.ChatManager.QueueChatMessage($"{request} is now on the ban list.");

        //    }
        //}

        public void Unban(IChatUser requestor, string request)
        {
            var unbanvalue = GetBeatSaverId(request);

            if (ListCollectionManager.Contains(banlist, unbanvalue)) {
                this.ChatManager.QueueChatMessage($"Removed {request} from the ban list.");
                ListCollectionManager.Remove(banlist, unbanvalue);
            }
            else {
                this.ChatManager.QueueChatMessage($"{request} is not on the ban list.");
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
                    this.ChatManager.QueueChatMessage("Queue is empty  .");
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
                if (request != "savedqueue") this.ChatManager.QueueChatMessage($"wrote {count} entries to {request}");
            }
            catch {
                this.ChatManager.QueueChatMessage("Was unable to write {queuefile}.");
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
                this.ChatManager.QueueChatMessage("Unable to read deck {request}.");
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
                if (RequestManager.RequestSongs.ToArray()[i] is SongRequest song) {
                    if (songId == "") {
                        string[] terms = new string[] { song._song["songName"].Value, song._song["songSubName"].Value, song._song["authorName"].Value, song._song["version"].Value, song._requestor.UserName };

                        if (DoesContainTerms(state._parameter, ref terms))
                            dequeueSong = true;
                    }
                    else {
                        if (song._song["id"].Value == songId)
                            dequeueSong = true;
                    }

                    if (dequeueSong) {
                        this.ChatManager.QueueChatMessage($"{song._song["songName"].Value} ({song._song["version"].Value}) removed.");
                        Skip(song);
                        return success;
                    }
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
            this.ChatManager.QueueChatMessage($"Mapper whitelist set to {request}.");
        }

        public void MapperBanList(IChatUser requestor, string request)
        {
            string key = request.ToLower();
            mapperBanlist = ListCollectionManager.OpenList(key);
            //this.ChatManager.QueueChatMessage($"Mapper ban list set to {request}.");
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
                    Logger.Error(e);
                }
            }
            Events.Clear();
            return success;
        }

        public string Every(ParseState state)
        {
            string[] parts = state._parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out var period)) return state.Error($"You must specify a time in minutes after {state._command}.");
            if (period < 1) return state.Error($"You must specify a period of at least 1 minute");
            Events.Add(new BotEvent(TimeSpan.FromMinutes(period), parts[1], true, (s, e) => ScheduledCommand(s, e)));
            return success;
        }

        public string EventIn(ParseState state)
        {
            string[] parts = state._parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out var period)) return state.Error($"You must specify a time in minutes after {state._command}.");
            if (period < 0) return state.Error($"You must specify a period of at least 0 minutes");
            Events.Add(new BotEvent(TimeSpan.FromMinutes(period), parts[1], false, (s, e) => ScheduledCommand(s, e)));
            return success;
        }
        public string Who(ParseState state)
        {

            var qm = this._messageFactroy.Create();

            var result = FindMatch(RequestManager.RequestSongs.OfType<SongRequest>(), state._parameter, qm);
            if (result == null) result = FindMatch(RequestManager.HistorySongs.OfType<SongRequest>(), state._parameter, qm);

            //if (result != null) this.ChatManager.QueueChatMessage($"{result.song["songName"].Value} requested by {result.requestor.displayName}.");
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
                    this.ChatManager.QueueChatMessage($"{song["songName"].Value} : {parts[1]}");
                    return success;
                }
            }
            this.ChatManager.QueueChatMessage($"Unable to find {songId}");
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
                this.ChatManager.QueueChatMessage($"!wadmin toggle {wobblestate} ");
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
                            _songMapFactory.Create(entry);

                            if (Mapperfiltered(entry, true)) continue; // This forces the mapper filter
                            if (Filtersong(entry)) continue;

                            if (state._flags.HasFlag(CmdFlags.Local)) QueueSong(state, entry);
                            ListCollectionManager.Add("latest.deck", entry["id"].Value);
                            totalSongs++;
                        }
                    }

                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    return;
                }

                offset += 1; // Magic beatsaver.com skip constant.
            }

            if (totalSongs == 0) {
                //this.ChatManager.QueueChatMessage($"No new songs found.");
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
                            _songMapFactory.Create(entry);

                            if (Mapperfiltered(entry, true)) continue; // This forces the mapper filter
                            if (Filtersong(entry)) continue;

                            if (state._flags.HasFlag(CmdFlags.Local)) QueueSong(state, entry);
                            ListCollectionManager.Add("search.deck", entry["id"].Value);
                            totalSongs++;
                        }
                    }
                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    return;
                }
                offset += 1;
            }

            if (totalSongs == 0) {
                //this.ChatManager.QueueChatMessage($"No new songs found.");
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
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {state._parameter}!");
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

            if ((state._flags.HasFlag(CmdFlags.MoveToTop))) {
                var newList = (new List<object>() { req }).Union(RequestManager.RequestSongs.ToArray());
                RequestManager.RequestSongs.Clear();
                RequestManager.RequestSongs.AddRange(newList);
            }
            else {
                RequestManager.RequestSongs.Add(req);
            }

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
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"].Value, song["version"].Value, (RequestManager.RequestSongs.ToArray()[i] as SongRequest)._requestor.UserName };
                    if (DoesContainTerms(request, ref terms))
                        moveRequest = true;
                }
                else {
                    if (song["id"].Value == moveId)
                        moveRequest = true;
                }

                if (moveRequest) {
                    // Remove the request from the queue
                    var songs = RequestManager.RequestSongs.ToList();
                    songs.RemoveAt(i);
                    RequestManager.RequestSongs.Clear();
                    RequestManager.RequestSongs.AddRange(songs);

                    // Then readd it at the appropriate position
                    if (top) {
                        var tmp = (new List<object>() { req }).Union(RequestManager.RequestSongs.ToArray());
                        RequestManager.RequestSongs.Clear();
                        RequestManager.RequestSongs.AddRange(tmp);
                    }
                    else
                        RequestManager.RequestSongs.Add(req);

                    // Write the modified request queue to file
                    _requestManager.WriteRequest();

                    // Refresh the queue ui
                    RefreshSongQuere();
                    RefreshQueue = true;

                    // And write a summary to file
                    WriteQueueSummaryToFile();

                    this.ChatManager.QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) {(top ? "promoted" : "demoted")}.");
                    return;
                }
            }
            this.ChatManager.QueueChatMessage($"{request} was not found in the queue.");
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

            this.ChatManager.QueueChatMessage(state ? "Queue is now open." : "Queue is now closed.");
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
                Logger.Error(ex);
            }
        }

        public void WriteQueueStatusToFile(string status)
        {
            try {
                string statusfile = Path.Combine(Plugin.DataPath, "queuestatus.txt");
                File.WriteAllText(statusfile, status);
            }

            catch (Exception ex) {
                Logger.Debug(ex.ToString());
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
            var list = RequestManager.RequestSongs.OfType<SongRequest>().ToList();
            Shuffle(list);


            //var list = RequestManager.RequestSongs.OfType<SongRequest>().ToList();
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
                    list.RemoveRange(entrycount, RequestManager.RequestSongs.Count - entrycount);
                }
                catch { }
            }
            RequestManager.RequestSongs.Clear();
            RequestManager.RequestSongs.AddRange(list);
            _requestManager.WriteRequest();

            // Notify the chat that the queue was cleared
            this.ChatManager.QueueChatMessage($"Queue lottery complete!");

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

            while (RequestManager.RequestSongs.Count > 0) DequeueRequest(RequestManager.RequestSongs.FirstOrDefault() as SongRequest, false); // More correct now, previous version did not keep track of user requests 

            _requestManager.WriteRequest();

            // Update the request button ui accordingly
            UpdateRequestUI();

            // Notify the chat that the queue was cleared
            this.ChatManager.QueueChatMessage($"Queue is now empty.");

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
                this.ChatManager.QueueChatMessage("usage: !remap <songid>,<songid>, omit the <>'s");
                return;
            }

            if (songremap.ContainsKey(parts[0])) songremap.Remove(parts[0]);
            songremap.Add(parts[0], parts[1]);
            this.ChatManager.QueueChatMessage($"Song {parts[0]} remapped to {parts[1]}");
            WriteRemapList();
        }

        public void Unmap(IChatUser requestor, string request)
        {

            if (songremap.ContainsKey(request)) {
                this.ChatManager.QueueChatMessage($"Remap entry {request} removed.");
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
                Logger.Debug(ex.ToString());
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
                Logger.Debug(ex.ToString());
            }
        }
        #endregion

        #region Wrong Song
        public void WrongSong(IChatUser requestor, string request)
        {
            // Note: Scanning backwards to remove LastIn, for loop is best known way.
            for (int i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                if (RequestManager.RequestSongs.ToArray()[i] is SongRequest song) {
                    if (song._requestor.Id == requestor.Id) {
                        this.ChatManager.QueueChatMessage($"{song._song["songName"].Value} ({song._song["version"].Value}) removed.");

                        ListCollectionManager.Remove(duplicatelist, song._song["id"].Value);
                        Skip(song, RequestStatus.Wrongsong);
                        return;
                    }
                }
                
            }
            this.ChatManager.QueueChatMessage($"You have no requests in the queue.");
        }
        #endregion

        // BUG: This requires a switch, or should be disabled for those who don't allow links
        public string ShowSongLink(ParseState state)
        {
            try  // We're accessing an element across threads, and currentsong doesn't need to be defined
            {
                var song = (RequestManager.RequestSongs.FirstOrDefault() as SongRequest)._song;
                if (!song.IsNull) _textFactory.Create().AddSong(ref song).QueueMessage(StringFormat.LinkSonglink.ToString());
            }
            catch (Exception ex) {
                Logger.Debug(ex.ToString());
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
            this.ChatManager.QueueChatMessage($"{queuestate} There are {RequestManager.RequestSongs.Count} maps ({Queueduration()}) in the queue.");
            return success;
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
            this.ChatManager.QueueChatMessage($"Hi, my name is {state._botcmd.UserParameter} , and I'm a list object!");
            return success;
        }

        public void Addtolist(IChatUser requestor, string request)
        {
            string[] parts = request.Split(new char[] { ' ', ',' }, 2);
            if (parts.Length < 2) {
                this.ChatManager.QueueChatMessage("Usage text... use the official help method");
                return;
            }

            try {

                ListCollectionManager.Add(ref parts[0], ref parts[1]);
                this.ChatManager.QueueChatMessage($"Added {parts[1]} to {parts[0]}");

            }
            catch {
                this.ChatManager.QueueChatMessage($"list {parts[0]} not found.");
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
                this.ChatManager.QueueChatMessage($"{request} not found.");
            }
        }

        public void RemoveFromlist(IChatUser requestor, string request)
        {
            string[] parts = request.Split(new char[] { ' ', ',' }, 2);
            if (parts.Length < 2) {
                //     NewCommands[Addtolist].ShortHelp();
                this.ChatManager.QueueChatMessage("Usage text... use the official help method");
                return;
            }

            try {

                ListCollectionManager.Remove(ref parts[0], ref parts[1]);
                this.ChatManager.QueueChatMessage($"Removed {parts[1]} from {parts[0]}");

            }
            catch {
                this.ChatManager.QueueChatMessage($"list {parts[0]} not found.");
            }
        }

        public void ClearList(IChatUser requestor, string request)
        {
            try {
                ListCollectionManager.ClearList(request);
                this.ChatManager.QueueChatMessage($"{request} is cleared.");
            }
            catch {
                this.ChatManager.QueueChatMessage($"Unable to clear {request}");
            }
        }

        public void UnloadList(IChatUser requestor, string request)
        {
            try {
                ListCollectionManager.ListCollection.Remove(request.ToLower());
                this.ChatManager.QueueChatMessage($"{request} unloaded.");
            }
            catch {
                this.ChatManager.QueueChatMessage($"Unable to unload {request}");
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
            catch (Exception ex) { Logger.Debug(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
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

            Logger.Debug($"Backing up {Plugin.DataPath}");
            try {
                if (!Directory.Exists(RequestBotConfig.Instance.backuppath))
                    Directory.CreateDirectory(RequestBotConfig.Instance.backuppath);

                ZipFile.CreateFromDirectory(Plugin.DataPath, BackupName, System.IO.Compression.CompressionLevel.Fastest, true);
                RequestBotConfig.Instance.LastBackup = DateTime.Now.ToString();
                RequestBotConfig.Instance.Save();

                Logger.Debug($"Backup success writing {BackupName}");
                return success;
            }
            catch {

            }
            Logger.Debug($"Backup failed writing {BackupName}");
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
            var songs = new List<JSONObject>();

            if (result != null) {
                // Add query results to out song database.
                if (result["docs"].IsArray) {
                    var downloadedsongs = result["docs"].AsArray;
                    for (int i = 0; i < downloadedsongs.Count; i++) _songMapFactory.Create(downloadedsongs[i].AsObject);

                    foreach (JSONObject currentSong in result["docs"].AsArray) {
                        _songMapFactory.Create(currentSong);
                    }
                }
                else {
                    _songMapFactory.Create(result.AsObject);
                }
            }

            var list = MapDatabase.Search(SearchString);

            try {
                string[] sortorder = sortby.Split(' ');

                list.Sort(delegate (SongMap c1, SongMap c2)
                {
                    return reverse * CompareSong(c1.Song, c2.Song, ref sortorder);
                });
            }
            catch (Exception e) {
                //this.ChatManager.QueueChatMessage($"Exception {e} sorting song list");
                Logger.Debug($"Exception sorting a returned song list. {e.ToString()}");
            }

            foreach (var song in list) {
                errorMessage = SongSearchFilter(song.Song, false, filter);
                if (errorMessage == "") songs.Add(song.Song);
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


        public Task ReadArchive(ParseState state)
        {
            return MapDatabase.LoadZIPDirectory();
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
        public static bool pploading = false;
        private bool disposedValue;

        public async Task GetPPData()
        {
            try {
                if (pploading) {
                    Logger.Debug("PPloaded");
                    return;
                }
                pploading = true;
                var resp = await WebClient.GetAsync(SCRAPED_SCORE_SABER_ALL_JSON_URL, System.Threading.CancellationToken.None);

                if (!resp.IsSuccessStatusCode) {
                    Logger.Debug("Failed to get pp");
                    pploading = false;
                    return;
                }
                //Instance.this.ChatManager.QueueChatMessage($"Parsing PP Data {result.Length}");

                var rootNode = resp.ConvertToJsonNode();

                ListCollectionManager.ClearList("pp.deck");

                foreach (var kvp in rootNode) {
                    var difficultyNodes = kvp.Value;
                    var key = difficultyNodes["key"].Value;

                    //Instance.this.ChatManager.QueueChatMessage($"{id}");
                    var maxpp = difficultyNodes["diffs"].AsArray.Linq.Max(x => x.Value["pp"].AsFloat);
                    var maxstar = difficultyNodes["diffs"].AsArray.Linq.Max(x => x.Value["star"].AsFloat);
                    if (maxpp > 0) {
                        //Instance.this.ChatManager.QueueChatMessage($"{id} = {maxpp}");
                        MapDatabase.PPMap.TryAdd(key, maxpp);
                        if (key != "" && maxpp > 100) ListCollectionManager.Add("pp.deck", key);

                        if (MapDatabase.MapLibrary.TryGetValue(key, out SongMap map)) {
                            map.PP = maxpp;
                            map.Song.Add("pp", maxpp);
                            map.IndexSong(map.Song);
                        }
                    }
                }
                Parse(GetLoginUser(), "!deck pp", CmdFlags.Local);

                // this.ChatManager.QueueChatMessage("PP Data indexed");
                Logger.Debug("PP Data indexed");
                pploading = false;
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
        #endregion
    }
}