using ChatCore.Interfaces;
using ChatCore.Models.Twitch;
using SongRequestManagerV2.SimpleJSON;
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
using System.Collections.Generic;
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
#if DEBUG
using System.Diagnostics;
#endif
using SongRequestManagerV2.Configuration;
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

        private readonly bool _mapperWhitelist = false; // BUG: Need to clean these up a bit.
        private bool _isGameCore = false;

        public static System.Random Generator { get; } = new System.Random(); // BUG: Should at least seed from unity?
        public static List<JSONObject> Played { get; private set; } = new List<JSONObject>(); // Played list
        public static List<BotEvent> Events { get; } = new List<BotEvent>();
        public static UserInfo CurrentUser { get; private set; }


        private static StringListManager mapperwhitelist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static StringListManager mapperBanlist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static StringListManager Whitelist = new StringListManager();
        private static StringListManager BlockedUser = new StringListManager();

        private static readonly string duplicatelist = "duplicate.list"; // BUG: Name of the list, needs to use a different interface for this.
        private static readonly string banlist = "banlist.unique"; // BUG: Name of the list, needs to use a different interface for this.
        private static readonly string _whitelist = "whitelist.unique"; // BUG: Name of the list, needs to use a different interface for this.
        private static readonly string _blockeduser = "blockeduser.unique";

        private static readonly Dictionary<string, string> songremap = new Dictionary<string, string>();
        public static Dictionary<string, string> deck = new Dictionary<string, string>(); // deck name/content

        private static readonly Regex _digitRegex = new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _deck = new Regex("^(current|draw|first|last|random|unload)$|$^", RegexOptions.Compiled); // Checks deck command parameters
        private static readonly Regex _drawcard = new Regex("($^)|(^[0-9a-zA-Z]+$)", RegexOptions.Compiled);

        public const string SCRAPED_SCORE_SABER_ALL_JSON_URL = "https://cdn.wes.cloud/beatstar/bssb/v2-ranked.json";
        public const string BEATMAPS_API_ROOT_URL = "https://beatsaver.com/api";
        public const string BEATMAPS_CDN_ROOT_URL = "https://cdn.beatsaver.com";

        private readonly System.Timers.Timer timer = new System.Timers.Timer(500);

        [Inject]
        public StringNormalization Normalize { get; private set; }
        [Inject]
        public MapDatabase MapDatabase { get; private set; }
        [Inject]
        public ListCollectionManager ListCollectionManager { get; private set; }
        [Inject]
        public IChatManager ChatManager { get; }
        [Inject]
        private readonly RequestManager _requestManager;
        [Inject]
        private readonly NotifySound notifySound;
        [Inject]
        private readonly QueueLongMessage.QueueLongMessageFactroy _messageFactroy;
        [Inject]
        private readonly SongRequest.SongRequestFactory _songRequestFactory;
        [Inject]
        private readonly DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        private readonly ParseState.ParseStateFactory _stateFactory;
        [Inject]
        private readonly SongMap.SongMapFactory _songMapFactory;

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
        private const string success = "";
        private const string endcommand = "X";
        private const string notsubcommand = "NotSubcmd";

        #region 構築・破棄
        [Inject]
        private void Constractor()
        {
            Logger.Debug("Constractor call");
            if (RequestBotConfig.Instance.PPSearch) {
                // Start loading PP data
                Dispatcher.RunOnMainThread(async () =>
                {
                    await this.GetPPData();
                });
            }
            this.Setup();
            this.MapDatabase.LoadDatabase();
            if (RequestBotConfig.Instance.LocalSearch) {
                // This is a background process
                this.MapDatabase.LoadCustomSongs().Await(null);
            }
            if (CurrentUser == null) {
                BS_Utils.Gameplay.GetUserInfo.GetUserAsync().Await(r =>
                {
                    CurrentUser = r;
                });
            }
        }
        public void Initialize()
        {
            Logger.Debug("Start Initialize");
            SceneManager.activeSceneChanged += this.SceneManager_activeSceneChanged;
            this.timer.Elapsed += this.Timer_Elapsed;
            this.timer.Start();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue) {
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
                this.disposedValue = true;
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
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion



        public void Newest(Keyboard.KEY key)
        {
            this.ClearSearches();
            this.Parse(this.GetLoginUser(), $"!addnew/top", CmdFlags.Local);
        }

        public void PP(Keyboard.KEY key)
        {
            this.ClearSearches();
            this.Parse(this.GetLoginUser(), $"!addpp/top/mod/pp", CmdFlags.Local);
        }

        public void Search(Keyboard.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            this.ClearSearches();
            this.Parse(this.GetLoginUser(), $"!addsongs/top {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void MSD(Keyboard.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            this.ClearSearches();
            this.Parse(this.GetLoginUser(), $"!makesearchdeck {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void UnfilteredSearch(Keyboard.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!")) {
                key.kb.Enter(key);
            }
            this.ClearSearches();
            this.Parse(this.GetLoginUser(), $"!addsongs/top/mod {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public void ClearSearches()
        {
            foreach (var item in RequestManager.RequestSongs) {
                if (item is SongRequest request && request.Status == RequestStatus.SongSearch) {
                    this.DequeueRequest(request, false);
                }
            }
            this.UpdateRequestUI();
        }

        public void ClearSearch(Keyboard.KEY key)
        {
            this.ClearSearches();
            this.RefreshSongQuere();
            this.UpdateRequestUI();
            this.RefreshQueue = true;
        }

        public bool MyChatMessageHandler(IChatMessage msg)
        {
            var excludefilename = "chatexclude.users";
            return this.ListCollectionManager.Contains(excludefilename, msg.Sender.UserName.ToLower(), ListFlags.Uncached);
        }

        internal void RecievedMessages(IChatMessage msg)
        {
            Logger.Debug($"Received Message : {msg.Message}");
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            this.Parse(msg.Sender, msg.Message.Replace("！", "!"));
#if DEBUG
            stopwatch.Stop();
            Logger.Debug($"{stopwatch.ElapsedMilliseconds} ms");
#endif
        }

        internal void OnConfigChangedEvent(RequestBotConfig config)
        {
            this.UpdateRequestUI();
            this.UpdateUIRequest?.Invoke(true);
            this.SetButtonIntactivityRequest?.Invoke(true);
        }

        // BUG: Prototype code, used for testing.


        public void ScheduledCommand(string command, ElapsedEventArgs e) => this.Parse(this.GetLoginUser(), command);

        public void RunStartupScripts()
        {
            this.ReadRemapList(); // BUG: This should use list manager

            this.MapperBanList(this.GetLoginUser(), "mapperban.list");
            this.WhiteList(this.GetLoginUser(), "whitelist.unique");
            this.BlockedUserList(this.GetLoginUser(), "blockeduser.unique");

#if UNRELEASED
            OpenList(SerchCreateChatUser(), "mapper.list"); // Open mapper list so we can get new songs filtered by our favorite mappers.
            MapperAllowList(SerchCreateChatUser(), "mapper.list");
            accesslist("mapper.list");

            loaddecks(SerchCreateChatUser(), ""); // Load our default deck collection
            // BUG: Command failure observed once, no permission to use /chatcommand. Possible cause: OurIChatUser isn't authenticated yet.

            RunScript(SerchCreateChatUser(), "startup.script"); // Run startup script. This can include any bot commands.
#endif
        }

        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1) => this._isGameCore = arg1.name == "GameCore";

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (RequestBotConfig.Instance.PerformanceMode && this._isGameCore) {
                return;
            }
            this.timer.Stop();
            try {
                if (this.ChatManager.RequestInfos.TryDequeue(out var requestInfo)) {
                    await this.CheckRequest(requestInfo);
                    this.UpdateRequestUI();
                    this.RefreshSongQuere();
                    this.RefreshQueue = true;
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

        private void Setup()
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
            try {
                var filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
                if (Directory.Exists(filesToDelete)) {
                    Utility.EmptyDirectory(filesToDelete);
                }

                try {
                    var timeSinceBackup = DateTime.Now - DateTime.Parse(RequestBotConfig.Instance.LastBackup);
                    if (timeSinceBackup > TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) {
                        this.Backup();
                    }
                }
                catch (Exception ex) {
                    Logger.Error(ex);
                    this.ChatManager.QueueChatMessage("Failed to run Backup");
                }

                try {
                    var PlayedAge = Utility.GetFileAgeDifference(playedfilename);
                    if (PlayedAge < TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours))
                        Played = this.ReadJSON(playedfilename); // Read the songsplayed file if less than x hours have passed 
                }
                catch (Exception ex) {
                    Logger.Error(ex);
                    this.ChatManager.QueueChatMessage("Failed to clear played file");

                }
                this._requestManager.ReadRequest(); // Might added the timespan check for this too. To be decided later.
                this._requestManager.ReadHistory();
                this.ListCollectionManager.OpenList("banlist.unique");

#if UNRELEASED
            //GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            //GC.Collect();
            //Instance.this.ChatManager.QueueChatMessage($"hashentries: {SongMap.hashcount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");
#endif

                this.ListCollectionManager.ClearOldList("duplicate.list", TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours));

                this.UpdateRequestUI();
                RequestBotConfig.Instance.ConfigChangedEvent += this.OnConfigChangedEvent;
            }
            catch (Exception ex) {
                Logger.Error(ex);
                this.ChatManager.QueueChatMessage(ex.ToString());
            }

            this.WriteQueueSummaryToFile();
            this.WriteQueueStatusToFile(this.QueueMessage(RequestBotConfig.Instance.RequestQueueOpen));

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


        // if (!silence) this.ChatManager.QueueChatMessage($"{request.Key.song["songName"].Value}/{request.Key.song["songAuthorName"].Value} ({songId}) added to the blacklist.");
        private void SendChatMessage(string message)
        {
            try {
                Logger.Debug($"Sending message: \"{message}\"");

                if (this.ChatManager.TwitchService != null) {
                    foreach (var channel in this.ChatManager.TwitchService.Channels) {
                        this.ChatManager.TwitchService.SendTextMessage($"{message}", channel.Value);
                    }
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        private int CompareSong(JSONObject song2, JSONObject song1, ref string[] sortorder)
        {
            var result = 0;

            foreach (var s in sortorder) {
                var sortby = s.Substring(1);
                switch (sortby) {
                    case "rating":
                        result = song2["stats"].AsObject["score"].AsFloat.CompareTo(song1["stats"].AsObject["score"].AsFloat);
                        break;
                    case "pp":
                        if (MapDatabase.PPMap.TryGetValue(song1["id"].Value, out var pp1) && MapDatabase.PPMap.TryGetValue(song2["id"].Value, out var pp2)) {
                            result = pp2.CompareTo(pp1);
                        }
                        else {
                            result = 0;
                        }
                        break;
                    case "id":
                        // BUG: This hack makes sorting by version and ID sort of work. In reality, we're comparing 1-2 numbers
                        result = this.GetBeatSaverId(song2[sortby].Value).PadLeft(6).CompareTo(this.GetBeatSaverId(song1[sortby].Value).PadLeft(6));
                        break;

                    default:
                        result = song2[sortby].Value.CompareTo(song1[sortby].Value);
                        break;
                }
                if (result == 0)
                    continue;

                if (s[0] == '-')
                    return -result;

                return result;
            }
            return result;
        }

        internal void UpdateSongMap(JSONObject song) => WebClient.GetAsync($"{BEATMAPS_API_ROOT_URL}/maps/id/{song["id"].Value}", System.Threading.CancellationToken.None).Await(resp =>
                                                      {
                                                          if (resp.IsSuccessStatusCode) {
                                                              var result = resp.ConvertToJsonNode();

                                                              this.ChatManager.QueueChatMessage($"{result.AsObject}");

                                                              if (result != null && result["id"].Value != "") {
                                                                  this._songMapFactory.Create(result.AsObject);
                                                              }
                                                          }
                                                      }, null, null);

        // BUG: Testing major changes. This will get seriously refactored soon.
        internal async Task CheckRequest(RequestInfo requestInfo)
        {
            if (requestInfo == null) {
                return;
            }
#if DEBUG
            Logger.Debug("Start CheckRequest");
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            var requestor = requestInfo.Requestor;
            var request = requestInfo.Request;

            var normalrequest = this.Normalize.NormalizeBeatSaverString(requestInfo.Request);

            var id = this.GetBeatSaverId(this.Normalize.RemoveSymbols(request, this.Normalize.SymbolsNoDash));
            try {
                if (!string.IsNullOrEmpty(id)) {
                    // Remap song id if entry present. This is one time, and not correct as a result. No recursion right now, could be confusing to the end user.
                    if (songremap.ContainsKey(id) && !requestInfo.Flags.HasFlag(CmdFlags.NoFilter)) {
                        request = songremap[id];
                        this.ChatManager.QueueChatMessage($"Remapping request {requestInfo.Request} to {request}");
                    }

                    var requestcheckmessage = this.IsRequestInQueue(this.Normalize.RemoveSymbols(request, this.Normalize.SymbolsNoDash));               // Check if requested ID is in Queue  
                    if (requestcheckmessage != "") {
                        this.ChatManager.QueueChatMessage(requestcheckmessage);
                        return;
                    }

                    if (RequestBotConfig.Instance.OfflineMode && RequestBotConfig.Instance.OfflinePath != "" && !MapDatabase.MapLibrary.ContainsKey(id)) {
                        Dispatcher.RunCoroutine(this.LoadOfflineDataBase(id));
                    }
                }

                JSONNode result = null;

                var errorMessage = "";

                // Get song query results from beatsaver.com
                if (!RequestBotConfig.Instance.OfflineMode) {
                    var requestUrl = "";
                    WebResponse resp = null;
                    if (!string.IsNullOrEmpty(id)) {
                        var idWithoutSymbols = this.Normalize.RemoveSymbols(request, this.Normalize.SymbolsNoDash);
                        requestUrl = $"{BEATMAPS_API_ROOT_URL}/maps/id/{idWithoutSymbols}";
                        resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);
                    }
                    else {
                        requestUrl = $"{BEATMAPS_API_ROOT_URL}/search/text/0?sortOrder=Latest&q={normalrequest}";
                        resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);
                    }
#if DEBUG
                    Logger.Debug($"Start get map detial : {stopwatch.ElapsedMilliseconds} ms");
#endif
                    if (resp == null) {
                        errorMessage = $"beatsaver is down now.";
                    }
                    else if (resp.IsSuccessStatusCode) {
                        result = resp.ConvertToJsonNode();
                    }
                    else {
                        errorMessage = $"Invalid BeatSaver ID \"{request}\" specified. {requestUrl}";
                    }
                }

                var filter = SongFilter.All;
                if (requestInfo.Flags.HasFlag(CmdFlags.NoFilter)) {
                    filter = SongFilter.Queue;
                }
                var songs = this.GetSongListFromResults(result, result["id"], ref errorMessage, filter, requestInfo.State.Sort != "" ? requestInfo.State.Sort : StringFormat.AddSortOrder.ToString());

                var autopick = RequestBotConfig.Instance.AutopickFirstSong || requestInfo.Flags.HasFlag(CmdFlags.Autopick);

                // Filter out too many or too few results
                if (songs.Count == 0) {
                    if (errorMessage == "") {
                        errorMessage = $"No results found for request \"{request}\"";
                    }
                }
                else if (!autopick && songs.Count >= 4) {
                    errorMessage = $"Request for '{request}' produces {songs.Count} results, narrow your search by adding a mapper name, or use https://beatmaps.io to look it up.";
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
                        msg.Add(this._textFactory.Create().AddSong(eachsong).Parse(StringFormat.BsrSongDetail), ", ");
                    }
                    msg.End("...", $"No matching songs for for {request}");
                    return;
                }
                else {
                    if (!requestInfo.Flags.HasFlag(CmdFlags.NoFilter))
                        errorMessage = this.SongSearchFilter(songs[0], false);
                }

                // Display reason why chosen song was rejected, if filter is triggered. Do not add filtered songs
                if (errorMessage != "") {
                    this.ChatManager.QueueChatMessage(errorMessage);
                    return;
                }
                var song = songs[0];
                var req = this._songRequestFactory.Create();
                req.Init(song, requestor, requestInfo.RequestTime, RequestStatus.Queued, requestInfo.RequestInfoText);
                RequestTracker[requestor.Id].numRequests++;
                this.ListCollectionManager.Add(duplicatelist, song["id"].Value);
                if (RequestBotConfig.Instance.NotifySound) {
                    this.notifySound.PlaySound();
                }
                if ((requestInfo.Flags.HasFlag(CmdFlags.MoveToTop))) {
                    var reqs = new List<object>() { req };
                    var newList = reqs.Union(RequestManager.RequestSongs.ToArray());
                    RequestManager.RequestSongs.Clear();
                    RequestManager.RequestSongs.AddRange(newList);
                }
                else {
                    RequestManager.RequestSongs.Add(req);
                }
                this._requestManager.WriteRequest();

                this.Writedeck(requestor, "savedqueue"); // This can be used as a backup if persistent Queue is turned off.

                if (!requestInfo.Flags.HasFlag(CmdFlags.SilentResult)) {
                    this._textFactory.Create().AddSong(song).QueueMessage(StringFormat.AddSongToQueueText.ToString());
                }
            }
            catch(NullReferenceException nullex) {
                Logger.Error(nullex);
                Logger.Error(nullex.Message);
                Logger.Error(nullex.StackTrace);
                Logger.Error(nullex.Source);
                Logger.Error(nullex.InnerException);
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
            foreach (var directory in Directory.EnumerateDirectories(RequestBotConfig.Instance.OfflinePath, id + "*")) {
                this.MapDatabase.LoadCustomSongs(directory, id).Await(null, e => { Logger.Error(e); }, null);
                yield return new WaitForSeconds(0.025f);
                yield return new WaitWhile(() => MapDatabase.DatabaseLoading);
                // break;
            }
        }
        public void UpdateRequestUI(bool writeSummary = true)
        {
            try {
                if (writeSummary) {
                    this.WriteQueueSummaryToFile(); // Write out queue status to file, do it first
                }
                Dispatcher.RunOnMainThread(() =>
                {
                    try {
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
        }

        public void RefreshSongQuere() => Dispatcher.RunOnMainThread(() =>
                                        {
                                            this.RefreshListRequest?.Invoke(false);
                                            this.RefreshQueue = true;
                                        });

        public void DequeueRequest(SongRequest request, bool updateUI = true)
        {
            try {
                if (request.Status != RequestStatus.Wrongsong && request.Status != RequestStatus.SongSearch) {
                    var reqs = new List<object>() { request };
                    var newList = reqs.Union(RequestManager.HistorySongs.ToArray());
                    RequestManager.HistorySongs.Clear();
                    RequestManager.HistorySongs.AddRange(newList);
                    // Wrong song requests are not logged into history, is it possible that other status states shouldn't be moved either?
                }


                if (RequestManager.HistorySongs.Count > RequestBotConfig.Instance.RequestHistoryLimit) {
                    var diff = RequestManager.HistorySongs.Count - RequestBotConfig.Instance.RequestHistoryLimit;
                    var songs = RequestManager.HistorySongs.ToList();
                    songs.RemoveRange(RequestManager.HistorySongs.Count - diff - 1, diff);
                    RequestManager.HistorySongs.Clear();
                    RequestManager.HistorySongs.AddRange(songs);
                }
                var requests = RequestManager.RequestSongs.ToList();
                requests.Remove(request);
                RequestManager.RequestSongs.Clear();
                RequestManager.RequestSongs.AddRange(requests);
                this._requestManager.WriteHistory();

                HistoryManager.AddSong(request);
                this._requestManager.WriteRequest();

                this.CurrentSong = null;


                // Decrement the requestors request count, since their request is now out of the queue

                if (!RequestBotConfig.Instance.LimitUserRequestsToSession) {
                    if (RequestTracker.ContainsKey(request._requestor.Id))
                        RequestTracker[request._requestor.Id].numRequests--;
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                if (updateUI == true) {
                    this.UpdateRequestUI();
                }
                this.RefreshQueue = true;
            }
        }

        public void SetRequestStatus(SongRequest request, RequestStatus status, bool fromHistory = false) => request.Status = status;

        public void Blacklist(SongRequest request, bool fromHistory, bool skip)
        {
            // Add the song to the blacklist
            this.ListCollectionManager.Add(banlist, request.SongMetaData["id"].Value);

            this.ChatManager.QueueChatMessage($"{request.SongMetaData["songName"].Value} by {request.SongMetaData["songAuthorName"].Value} ({request.SongMetaData["id"].Value}) added to the blacklist.");

            if (!fromHistory) {
                if (skip)
                    this.Skip(request, RequestStatus.Blacklisted);
            }
            else
                this.SetRequestStatus(request, RequestStatus.Blacklisted, fromHistory);
        }

        public void Skip(SongRequest request, RequestStatus status = RequestStatus.Skipped)
        {
            // Set the final status of the request
            this.SetRequestStatus(request, status);

            // Then dequeue it
            this.DequeueRequest(request);

            this.UpdateRequestUI();
            this.RefreshSongQuere();
        }
        public string GetBeatSaverId(string request)
        {
            request = this.Normalize.RemoveSymbols(request, this.Normalize.SymbolsNoDash);
            if (_digitRegex.IsMatch(request))
                return request;
            if (_beatSaverRegex.IsMatch(request)) {
                var requestparts = request.Split(new char[] { '-' }, 2);
                //return requestparts[0];
                if (Int32.TryParse(requestparts[1], out var o)) {
                    // this.ChatManager.QueueChatMessage($"key={o.ToString("x")}");
                    return o.ToString("x");
                }
            }
            return "";
        }


        public string AddToTop(ParseState state)
        {
            var newstate = this._stateFactory.Create().Setup(state); // Must use copies here, since these are all threads
            newstate.Flags |= CmdFlags.MoveToTop | CmdFlags.NoFilter;
            newstate.Info = "!ATT";
            return this.ProcessSongRequest(newstate);
        }

        public string ModAdd(ParseState state)
        {
            var newstate = this._stateFactory.Create().Setup(state); // Must use copies here, since these are all threads
            newstate.Flags |= CmdFlags.NoFilter;
            newstate.Info = "Unfiltered";
            return this.ProcessSongRequest(newstate);
        }


        public string ProcessSongRequest(ParseState state)
        {
            try {
                if (RequestBotConfig.Instance.RequestQueueOpen == false && !state.Flags.HasFlag(CmdFlags.NoFilter) && !state.Flags.HasFlag(CmdFlags.Local)) // BUG: Complex permission, Queue state message needs to be handled higher up
                {
                    this.ChatManager.QueueChatMessage($"Queue is currently closed.");
                    return success;
                }

                if (!RequestTracker.ContainsKey(state.User.Id))
                    RequestTracker.Add(state.User.Id, new RequestUserTracker());

                var limit = RequestBotConfig.Instance.UserRequestLimit;

                if (state.User is TwitchUser twitchUser) {
                    if (twitchUser.IsSubscriber)
                        limit = Math.Max(limit, RequestBotConfig.Instance.SubRequestLimit);
                    if (state.User.IsModerator)
                        limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                    if (twitchUser.IsVip)
                        limit += RequestBotConfig.Instance.VipBonusRequests; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like
                }
                else {
                    if (state.User.IsModerator)
                        limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                }

                if (!state.User.IsBroadcaster && RequestTracker[state.User.Id].numRequests >= limit) {
                    if (RequestBotConfig.Instance.LimitUserRequestsToSession) {
                        this._textFactory.Create().Add("Requests", RequestTracker[state.User.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You've already used %Requests% requests this stream. Subscribers are limited to %RequestLimit%.");
                    }
                    else {
                        this._textFactory.Create().Add("Requests", RequestTracker[state.User.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");
                    }

                    return success;
                }

                // BUG: Need to clean up the new request pipeline
                var testrequest = this.Normalize.RemoveSymbols(state.Parameter, this.Normalize.SymbolsNoDash);

                var newRequest = new RequestInfo(state.User, state.Parameter, DateTime.UtcNow, _digitRegex.IsMatch(testrequest) || _beatSaverRegex.IsMatch(testrequest), state, state.Flags, state.Info);

                if (!newRequest.IsBeatSaverId && state.Parameter.Length < 2) {
                    this.ChatManager.QueueChatMessage($"Request \"{state.Parameter}\" is too short- Beat Saver searches must be at least 3 characters!");
                }

                if (!this.ChatManager.RequestInfos.Contains(newRequest)) {
                    this.ChatManager.RequestInfos.Enqueue(newRequest);
                }
                return success;
            }
            catch (Exception ex) {
                Logger.Error(ex);
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
                    Id = CurrentUser.platformUserId,
                    UserName = CurrentUser.userName,
                    DisplayName = CurrentUser.userName,
                    Color = "#FFFFFFFF",
                    IsBroadcaster = true,
                    IsModerator = false,
                    IsSubscriber = false,
                    IsPro = false,
                    IsStaff = false,
                    Badges = Array.Empty<IChatBadge>()
                };
                return new TwitchUser(JsonUtility.ToJson(obj));
            }
        }
        public void Parse(IChatUser user, string request, CmdFlags flags = 0, string info = "")
        {
            if (string.IsNullOrEmpty(request)) {
                return;
            }

            if (!string.IsNullOrEmpty(user.Id) && this.ListCollectionManager.Contains(_blockeduser, user.Id.ToLower())) {
                return;
            }

            // This will be used for all parsing type operations, allowing subcommands efficient access to parse state logic
            this._stateFactory.Create().Setup(user, request, flags, info).ParseCommand();
        }
        private void Instance_ReceiveMessege(string obj)
        {
            var message = new MessageEntity()
            {
                Message = obj
            };

            this.RecievedMessages(message);
        }

        #region ChatCommand
        // BUG: This one needs to be cleaned up a lot imo
        // BUG: This file needs to be split up a little, but not just yet... Its easier for me to move around in one massive file, since I can see the whole thing at once. 





        #region Utility functions
        public static int MaximumTwitchMessageLength => 498 - RequestBotConfig.Instance.BotPrefix.Length;

        public string ChatMessage(ParseState state)
        {
            var dt = this._textFactory.Create().AddUser(state.User);
            try {
                dt.AddSong((RequestManager.HistorySongs.FirstOrDefault() as SongRequest).SongMetaData); // Exposing the current song 
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }

            dt.QueueMessage(state.Parameter);
            return success;
        }

        public void RunScript(IChatUser requestor, string request) => this.ListCollectionManager.Runscript(request);
        #endregion

        #region Filter support functions

        public bool DoesContainTerms(string request, ref string[] terms)
        {
            if (request == "")
                return false;
            request = request.ToLower();

            foreach (var term in terms)
                foreach (var word in request.Split(' '))
                    if (word.Length > 2 && term.ToLower().Contains(word))
                        return true;

            return false;
        }

        private bool IsModerator(IChatUser requestor, string message = "")
        {
            if (requestor.IsBroadcaster || requestor.IsModerator)
                return true;
            if (message != "")
                this.ChatManager.QueueChatMessage($"{message} is moderator only.");
            return false;
        }

        public bool Filtersong(JSONObject song)
        {
            var songid = song["id"].Value;
            if (this.IsInQueue(songid))
                return true;
            if (this.ListCollectionManager.Contains(banlist, songid))
                return true;
            if (this.ListCollectionManager.Contains(duplicatelist, songid))
                return true;
            return false;
        }

        // Returns error text if filter triggers, or "" otherwise, "fast" version returns X if filter triggers


        /// <summary>
        /// 
        /// </summary>
        /// <param name="song">this is parent songNode</param>
        /// <param name="fast"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public string SongSearchFilter(JSONObject song, bool fast = false, SongFilter filter = SongFilter.All) // BUG: This could be nicer
        {
            var songid = song["id"].Value;
            var metadata = song["metadata"].AsObject;
            var version = song["versions"].AsArray.Children.FirstOrDefault(x => x["state"].Value == MapStatus.Published.ToString());
            var stats = song["stats"].AsObject;
            if (version == null) {
                version = song["versions"].AsArray.Children.OrderBy(x => DateTime.Parse(x["createdAt"].Value)).LastOrDefault();
            }
            if (metadata.IsNull || stats.IsNull) {
                return fast ? "X" : "Ivalided json type.";
            }
            if (filter.HasFlag(SongFilter.Queue) && RequestManager.RequestSongs.OfType<SongRequest>().Any(req => req.SongNode["id"] == songid))
                return fast ? "X" : $"Request {metadata["songName"].Value} by {metadata["levelAuthorName"].Value} already exists in queue!";

            if (filter.HasFlag(SongFilter.Blacklist) && this.ListCollectionManager.Contains(banlist, songid))
                return fast ? "X" : $"{metadata["songName"].Value} by {metadata["levelAuthorName"].Value} ({songid}) is banned!";

            if (filter.HasFlag(SongFilter.Mapper) && this.Mapperfiltered(song, this._mapperWhitelist))
                return fast ? "X" : $"{metadata["songName"].Value} by {metadata["levelAuthorName"].Value} does not have a permitted mapper!";

            if (filter.HasFlag(SongFilter.Duplicate) && this.ListCollectionManager.Contains(duplicatelist, songid))
                return fast ? "X" : $"{metadata["songName"].Value} by  {metadata["levelAuthorName"].Value} already requested this session!";

            if (this.ListCollectionManager.Contains(_whitelist, songid))
                return "";

            if (filter.HasFlag(SongFilter.Duration) && metadata["duration"].AsFloat > RequestBotConfig.Instance.MaximumSongLength * 60)
                return fast ? "X" : $"{metadata["songName"].Value} ({metadata["duration"].Value}) by {metadata["levelAuthorName"].Value} ({songid}) is too long!";
            var njs = 0f;
            foreach (var diff in version["diffs"].AsArray.Children) {
                if (njs < diff["njs"].AsFloat) {
                    njs = diff["njs"].AsFloat;
                }
            }

            if (filter.HasFlag(SongFilter.NJS) && njs < RequestBotConfig.Instance.MinimumNJS)
                return fast ? "X" : $"{metadata["songName"].Value} ({metadata["duration"].Value}) by {metadata["levelAuthorName"].Value} ({songid}) NJS ({njs}) is too low!";

            if (filter.HasFlag(SongFilter.Remap) && songremap.ContainsKey(songid))
                return fast ? "X" : $"no permitted results found!";

            if (filter.HasFlag(SongFilter.Rating) && stats["score"].AsFloat < RequestBotConfig.Instance.LowestAllowedRating && stats["score"].AsFloat != 0)
                return fast ? "X" : $"{metadata["songName"].Value} by {metadata["levelAuthorName"].Value} is below {RequestBotConfig.Instance.LowestAllowedRating}% rating!";

            return "";
        }

        // checks if request is in the RequestManager.RequestSongs - needs to improve interface
        public string IsRequestInQueue(string request, bool fast = false)
        {
            foreach (SongRequest req in RequestManager.RequestSongs) {
                var song = req.SongMetaData;
                if (string.Equals(song["id"].Value, request, StringComparison.InvariantCultureIgnoreCase))
                    return fast ? "X" : $"Request {song["songName"].Value} by {song["songAuthorName"].Value} ({song["id"].Value}) already exists in queue!";
            }
            return ""; // Empty string: The request is not in the RequestManager.RequestSongs
        }
        // unhappy about naming here
        private bool IsInQueue(string request) => !(this.IsRequestInQueue(request) == "");

        public string ClearDuplicateList(ParseState state)
        {
            if (!state._botcmd.Flags.HasFlag(CmdFlags.SilentResult))
                this.ChatManager.QueueChatMessage("Session duplicate list is now clear.");
            this.ListCollectionManager.ClearList(duplicatelist);
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
            var id = this.GetBeatSaverId(state.Parameter.ToLower());

            if (this.ListCollectionManager.Contains(banlist, id)) {
                this.ChatManager.QueueChatMessage($"{id} is already on the ban list.");
                return;
            }

            if (!MapDatabase.MapLibrary.TryGetValue(id, out var song)) {
                JSONNode result = null;

                if (!RequestBotConfig.Instance.OfflineMode) {
                    var requestUrl = $"{BEATMAPS_API_ROOT_URL}/maps/id/{id}";
                    var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                    if (resp.IsSuccessStatusCode) {
                        result = resp.ConvertToJsonNode();
                    }
                    else {
                        Logger.Debug($"Ban: Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    }
                }

                if (result != null)
                    song = this._songMapFactory.Create(result.AsObject);
            }

            this.ListCollectionManager.Add(banlist, id);

            if (song == null) {
                this.ChatManager.QueueChatMessage($"{id} is now on the ban list.");
            }
            else {
                state.Msg(this._textFactory.Create().AddSong(song.SRMInfo).Parse(StringFormat.BanSongDetail), ", ");
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
            var unbanvalue = this.GetBeatSaverId(request);

            if (this.ListCollectionManager.Contains(banlist, unbanvalue)) {
                this.ChatManager.QueueChatMessage($"Removed {request} from the ban list.");
                this.ListCollectionManager.Remove(banlist, unbanvalue);
            }
            else {
                this.ChatManager.QueueChatMessage($"{request} is not on the ban list.");
            }
        }
        #endregion

        #region Deck Commands
        public string Restoredeck(ParseState state) => this.Readdeck(this._stateFactory.Create().Setup(state, "savedqueue"));

        public void Writedeck(IChatUser requestor, string request)
        {
            var queuefile = Path.Combine(Plugin.DataPath, request + ".deck");
            try {
                var count = 0;
                if (RequestManager.RequestSongs.Count == 0) {
                    this.ChatManager.QueueChatMessage("Queue is empty  .");
                    return;
                }
                var sb = new StringBuilder();

                foreach (SongRequest req in RequestManager.RequestSongs.ToArray()) {
                    var song = req.SongNode;
                    if (count > 0)
                        sb.Append(",");
                    sb.Append(song["id"].Value);
                    count++;
                }
                File.WriteAllText(queuefile, sb.ToString());
                if (request != "savedqueue")
                    this.ChatManager.QueueChatMessage($"wrote {count} entries to {request}");
            }
            catch {
                this.ChatManager.QueueChatMessage($"Was unable to write {queuefile}.");
            }
        }

        public string Readdeck(ParseState state)
        {
            try {
                var queuefile = Path.Combine(Plugin.DataPath, state.Parameter + ".deck");
                if (!File.Exists(queuefile)) {
                    using (File.Create(queuefile)) { };
                }

                var fileContent = File.ReadAllText(queuefile);
                var integerStrings = fileContent.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                for (var n = 0; n < integerStrings.Length; n++) {
                    if (this.IsInQueue(integerStrings[n]))
                        continue;

                    var newstate = this._stateFactory.Create().Setup(state); // Must use copies here, since these are all threads
                    newstate.Parameter = integerStrings[n];
                    this.ProcessSongRequest(newstate);
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

            var songId = this.GetBeatSaverId(state.Parameter);
            for (var i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                var dequeueSong = false;
                if (RequestManager.RequestSongs.ToArray()[i] is SongRequest song) {
                    if (songId == "") {
                        var terms = new string[] { song.SongMetaData["songName"].Value, song.SongMetaData["songSubName"].Value, song.SongMetaData["songAuthorName"].Value, song.SongMetaData["id"].Value, song._requestor.UserName };

                        if (this.DoesContainTerms(state.Parameter, ref terms))
                            dequeueSong = true;
                    }
                    else {
                        if (song.SongNode["id"].Value == songId)
                            dequeueSong = true;
                    }

                    if (dequeueSong) {
                        this.ChatManager.QueueChatMessage($"{song.SongMetaData["songName"].Value} ({song.SongMetaData["id"].Value}) removed.");
                        this.Skip(song);
                        return success;
                    }
                }
            }
            return $"{state.Parameter} was not found in the queue.";
        }
        #endregion


        // BUG: Will use a new interface to the list manager
        public void MapperAllowList(IChatUser requestor, string request)
        {
            var key = request.ToLower();
            mapperwhitelist = this.ListCollectionManager.OpenList(key); // BUG: this is still not the final interface
            this.ChatManager.QueueChatMessage($"Mapper whitelist set to {request}.");
        }

        public void MapperBanList(IChatUser requestor, string request)
        {
            var key = request.ToLower();
            mapperBanlist = this.ListCollectionManager.OpenList(key);
            //this.ChatManager.QueueChatMessage($"Mapper ban list set to {request}.");
        }

        public void WhiteList(IChatUser requestor, string request)
        {
            var key = request.ToLower();
            Whitelist = this.ListCollectionManager.OpenList(key);
        }

        public void BlockedUserList(IChatUser requestor, string request)
        {
            var key = request.ToLower();
            BlockedUser = this.ListCollectionManager.OpenList(key);
        }

        // Not super efficient, but what can you do
        public bool Mapperfiltered(JSONObject song, bool white)
        {
            if (song["metadata"].IsObject) {
                song = song["metadata"].AsObject;
            }
            var normalizedauthor = song["levelAuthorName"].Value.ToLower();
            if (white && mapperwhitelist.list.Any()) {
                foreach (var mapper in mapperwhitelist.list) {
                    if (normalizedauthor.Contains(mapper))
                        return false;
                }
                return true;
            }

            foreach (var mapper in mapperBanlist.list) {
                if (normalizedauthor.Contains(mapper))
                    return true;
            }

            return false;
        }

        // return a songrequest match in a SongRequest list. Good for scanning Queue or History
        private SongRequest FindMatch(IEnumerable<SongRequest> queue, string request, QueueLongMessage qm)
        {
            var songId = this.GetBeatSaverId(request);

            SongRequest result = null;

            var lastuser = "";
            foreach (var entry in queue) {
                var song = entry.SongMetaData;

                if (string.IsNullOrEmpty(songId)) {
                    var terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["songAuthorName"].Value, song["levelAuthorName"].Value, song["id"].Value, entry._requestor.UserName };

                    if (this.DoesContainTerms(request, ref terms)) {
                        result = entry;

                        if (lastuser != result._requestor.UserName)
                            qm.Add($"{result._requestor.UserName}: ");
                        qm.Add($"{result.SongMetaData["songName"].Value} ({result.SongNode["id"].Value})", ",");
                        lastuser = result._requestor.UserName;
                    }
                }
                else {
                    if (string.Equals(entry.SongNode["id"].Value, songId, StringComparison.InvariantCultureIgnoreCase)) {
                        result = entry;
                        qm.Add($"{result._requestor.UserName}: {result.SongMetaData["songName"].Value} ({result.SongNode["id"].Value})");
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
            var parts = state.Parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out var period))
                return state.Error($"You must specify a time in minutes after {state.Command}.");
            if (period < 1)
                return state.Error($"You must specify a period of at least 1 minute");
            Events.Add(new BotEvent(TimeSpan.FromMinutes(period), parts[1], true, (s, e) => this.ScheduledCommand(s, e)));
            return success;
        }

        public string EventIn(ParseState state)
        {
            var parts = state.Parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out var period))
                return state.Error($"You must specify a time in minutes after {state.Command}.");
            if (period < 0)
                return state.Error($"You must specify a period of at least 0 minutes");
            Events.Add(new BotEvent(TimeSpan.FromMinutes(period), parts[1], false, (s, e) => this.ScheduledCommand(s, e)));
            return success;
        }
        public string Who(ParseState state)
        {

            var qm = this._messageFactroy.Create();

            var result = this.FindMatch(RequestManager.RequestSongs.OfType<SongRequest>(), state.Parameter, qm);
            if (result == null)
                result = this.FindMatch(RequestManager.HistorySongs.OfType<SongRequest>(), state.Parameter, qm);

            //if (result != null) this.ChatManager.QueueChatMessage($"{result.song["songName"].Value} requested by {result.requestor.displayName}.");
            if (result != null)
                qm.End("...");
            return "";
        }

        public string SongMsg(ParseState state)
        {
            var parts = state.Parameter.Split(new char[] { ' ', ',' }, 2);
            var songId = this.GetBeatSaverId(parts[0]);
            if (songId == "")
                return state.Helptext(true);

            foreach (var entry in RequestManager.RequestSongs.OfType<SongRequest>()) {
                var song = entry.SongMetaData;

                if (entry.SongNode["id"].Value == songId) {
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
            state.Parameter = state.Parameter.ToLower();

            if (state.Parameter == "on")
                state.Parameter = "enable";
            if (state.Parameter == "off")
                state.Parameter = "disable";

            if (state.Parameter != "enable" && state.Parameter != "disable") {
                state.Msg(state._botcmd.ShortHelp);
                yield break;
            }

            //System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state.parameter}"));

            System.Diagnostics.Process.Start($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state.Parameter}");

            if (PluginManager.GetPlugin("WobbleSaber") != null) {
                var wobblestate = "off";
                if (state.Parameter == "enable")
                    wobblestate = "on";
                this.ChatManager.QueueChatMessage($"!wadmin toggle {wobblestate} ");
            }

            state.Msg($"The !bomb command is now {state.Parameter}d.");

            yield break;
        }


        public async Task AddsongsFromnewest(ParseState state)
        {
            var totalSongs = 0;
            //if (RequestBotConfig.Instance.OfflineMode) return;
            this.ListCollectionManager.ClearList("latest.deck");
            //state.msg($"Flags: {state.flags}");
            var offset = 0;
            while (offset < RequestBotConfig.Instance.MaxiumScanRange) // MaxiumAddScanRange
            {
                var requestUrl = $"{BEATMAPS_API_ROOT_URL}/search/text/{offset}?sortOrder=Latest";
                var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode) {
                    var result = resp.ConvertToJsonNode();
                    if (!result["docs"].IsArray) {
                        Logger.Debug("Responce is not JSON.");
                        break;
                    }
                    if (!result["docs"].AsArray.Children.Any()) {
                        Logger.Debug("Has not any songs.");
                        break;
                    }
                    foreach (var doc in result["docs"].Children) {
                        var entry = doc.AsObject;
                        this._songMapFactory.Create(entry);

                        if (this.Mapperfiltered(entry, true))
                            continue; // This forces the mapper filter
                        if (this.Filtersong(entry))
                            continue;

                        if (state.Flags.HasFlag(CmdFlags.Local))
                            this.QueueSong(state, entry);
                        this.ListCollectionManager.Add("latest.deck", entry["id"].Value);
                        totalSongs++;
                    }
                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    break;
                }

                offset++; // Magic beatsaver.com skip constant.
            }

            if (totalSongs == 0) {
                //this.ChatManager.QueueChatMessage($"No new songs found.");
            }
            else {
#if UNRELEASED
                COMMAND.Parse(TwitchWebSocketClient.OurIChatUser, "!deck latest",state.flags);
#endif

                if (state.Flags.HasFlag(CmdFlags.Local)) {
                    this.UpdateRequestUI();
                    this.RefreshSongQuere();
                    this.RefreshQueue = true;
                }
            }
            Logger.Debug($"Total songs : {totalSongs}");
        }

        public async Task AddsongsFromRank(ParseState state)
        {
            var totalSongs = 0;
            //if (RequestBotConfig.Instance.OfflineMode) return;
            this.ListCollectionManager.ClearList("latest.deck");
            //state.msg($"Flags: {state.flags}");
            var offset = 0;
            while (offset < RequestBotConfig.Instance.MaxiumScanRange) // MaxiumAddScanRange
            {
                var requestUrl = $"{BEATMAPS_API_ROOT_URL}/search/text/{offset}?ranked=true&sortOrder=Latest";
                var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode) {
                    var result = resp.ConvertToJsonNode();
                    if (!result["docs"].IsArray) {
                        Logger.Debug("Responce is not JSON.");
                        break;
                    }
                    if (!result["docs"].AsArray.Children.Any()) {
                        Logger.Debug("Has not any songs.");
                        break;
                    }
                    foreach (var doc in result["docs"].Children) {
                        var entry = doc.AsObject;
                        this._songMapFactory.Create(entry);

                        if (this.Mapperfiltered(entry, true))
                            continue; // This forces the mapper filter
                        if (this.Filtersong(entry))
                            continue;

                        if (state.Flags.HasFlag(CmdFlags.Local))
                            this.QueueSong(state, entry);
                        this.ListCollectionManager.Add("latest.deck", entry["id"].Value);
                        totalSongs++;
                    }
                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    break;
                }

                offset++; // Magic beatsaver.com skip constant.
            }

            if (totalSongs != 0 && state.Flags.HasFlag(CmdFlags.Local)) {
                this.UpdateRequestUI();
                this.RefreshSongQuere();
                this.RefreshQueue = true;
                
            }
            Logger.Debug($"Total songs : {totalSongs}");
        }

        public async Task Makelistfromsearch(ParseState state)
        {
            var totalSongs = 0;
            var id = this.GetBeatSaverId(state.Parameter);
            if (RequestBotConfig.Instance.OfflineMode) {
                return;
            }
            var offset = 0;
            this.ListCollectionManager.ClearList("search.deck");
            //state.msg($"Flags: {state.flags}");
            // MaxiumAddScanRange
            while (offset < RequestBotConfig.Instance.MaxiumScanRange) {
                var requestUrl = !string.IsNullOrEmpty(id) ? $"{BEATMAPS_API_ROOT_URL}/maps/id/{this.Normalize.RemoveSymbols(state.Parameter, this.Normalize.SymbolsNoDash)}" : $"{BEATMAPS_API_ROOT_URL}/search/text/0?sortOrder=Latest";
                var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode) {
                    var result = resp.ConvertToJsonNode();
                    if (!result["docs"].IsArray) {
                        break;
                    }
                    if (!result["docs"].AsArray.Children.Any()) {
                        break;
                    }
                    foreach (var doc in result["docs"].Children) {
                        var entry = doc.AsObject;
                        this._songMapFactory.Create(entry);

                        if (this.Mapperfiltered(entry, true))
                            continue; // This forces the mapper filter
                        if (this.Filtersong(entry))
                            continue;

                        if (state.Flags.HasFlag(CmdFlags.Local))
                            this.QueueSong(state, entry);
                        this.ListCollectionManager.Add("search.deck", entry["id"].Value);
                        totalSongs++;
                    }
                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    break;
                }
                offset++;
            }

            if (totalSongs == 0) {
                //this.ChatManager.QueueChatMessage($"No new songs found.");
            }
            else {
#if UNRELEASED
                COMMAND.Parse(TwitchWebSocketClient.OurIChatUser, "!deck search", state.flags);
#endif

                if (state.Flags.HasFlag(CmdFlags.Local)) {
                    this.UpdateRequestUI();
                    this.RefreshSongQuere();
                    this.RefreshQueue = true;
                }
            }
            Logger.Debug($"Total songs : {totalSongs}");
        }

        // General search version
        public async Task Addsongs(ParseState state)
        {

            var id = this.GetBeatSaverId(state.Parameter);
            var requestUrl = (id != "") ? $"{BEATMAPS_API_ROOT_URL}/maps/id/{this.Normalize.RemoveSymbols(state.Parameter, this.Normalize.SymbolsNoDash)}" : $"{BEATMAPS_API_ROOT_URL}/search/text/0?q={state.Request}&sortOrder=Latest";

            var errorMessage = "";

            if (RequestBotConfig.Instance.OfflineMode)
                requestUrl = "";

            JSONNode result = null;

            if (!RequestBotConfig.Instance.OfflineMode) {
                var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                if (resp != null && resp.IsSuccessStatusCode) {
                    result = resp.ConvertToJsonNode();

                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {state.Parameter}!");
                    errorMessage = $"Invalid BeatSaver ID \"{state.Parameter}\" specified.";
                }
            }

            var filter = SongFilter.All;
            if (state.Flags.HasFlag(CmdFlags.NoFilter))
                filter = SongFilter.Queue;
            var songs = this.GetSongListFromResults(result, state.Parameter, ref errorMessage, filter, state.Sort != "" ? state.Sort : StringFormat.LookupSortOrder.ToString(), -1);

            foreach (var entry in songs) {
                this.QueueSong(state, entry);
            }

            this.UpdateRequestUI();
            this.RefreshSongQuere();
            this.RefreshQueue = true;
        }

        public void QueueSong(ParseState state, JSONObject song)
        {
            var req = this._songRequestFactory.Create();
            req.Init(song, state.User, DateTime.UtcNow, RequestStatus.SongSearch, "search result");

            if ((state.Flags.HasFlag(CmdFlags.MoveToTop))) {
                var newList = (new List<object>() { req }).Union(RequestManager.RequestSongs.ToArray());
                RequestManager.RequestSongs.Clear();
                RequestManager.RequestSongs.AddRange(newList);
            }
            else {
                RequestManager.RequestSongs.Add(req);
            }

        }

        #region Move Request To Top/Bottom

        public void MoveRequestToTop(IChatUser requestor, string request) => this.MoveRequestPositionInQueue(requestor, request, true);

        public void MoveRequestToBottom(IChatUser requestor, string request) => this.MoveRequestPositionInQueue(requestor, request, false);

        public void MoveRequestPositionInQueue(IChatUser requestor, string request, bool top)
        {

            var moveId = this.GetBeatSaverId(request);
            for (var i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                var req = RequestManager.RequestSongs.ElementAt(i) as SongRequest;
                var song = req.SongMetaData;

                var moveRequest = false;
                if (moveId == "") {
                    var terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["songAuthorName"].Value, song["levelAuthorName"].Value, req.SongNode["id"].Value, (RequestManager.RequestSongs.ToArray()[i] as SongRequest)._requestor.UserName };
                    if (this.DoesContainTerms(request, ref terms))
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
                    this._requestManager.WriteRequest();

                    // Refresh the queue ui
                    this.RefreshSongQuere();
                    this.RefreshQueue = true;

                    // And write a summary to file
                    this.WriteQueueSummaryToFile();

                    this.ChatManager.QueueChatMessage($"{song["songName"].Value} ({song["id"].Value}) {(top ? "promoted" : "demoted")}.");
                    return;
                }
            }
            this.ChatManager.QueueChatMessage($"{request} was not found in the queue.");
        }
        #endregion



        #region Queue Related

        // This function existing to unify the queue message strings, and to allow user configurable QueueMessages in the future
        public string QueueMessage(bool QueueState) => QueueState ? "Queue is open" : "Queue is closed";
        public string OpenQueue(ParseState state)
        {
            this.ToggleQueue(state.User, state.Parameter, true);
            return success;
        }

        public string CloseQueue(ParseState state)
        {
            this.ToggleQueue(state.User, state.Parameter, false);
            return success;
        }

        public void ToggleQueue(IChatUser requestor, string request, bool state)
        {
            RequestBotConfig.Instance.RequestQueueOpen = state;

            this.ChatManager.QueueChatMessage(state ? "Queue is now open." : "Queue is now closed.");
            this.WriteQueueStatusToFile(this.QueueMessage(state));
            this.RefreshSongQuere();
            this.RefreshQueue = true;
        }
        public void WriteQueueSummaryToFile()
        {

            if (!RequestBotConfig.Instance.UpdateQueueStatusFiles)
                return;

            try {
                var statusfile = Path.Combine(Plugin.DataPath, "queuelist.txt");
                var queuesummary = new StringBuilder();
                var count = 0;

                foreach (SongRequest req in RequestManager.RequestSongs.ToArray()) {
                    var song = req.SongMetaData;
                    queuesummary.Append(this._textFactory.Create().AddSong(song).Parse(StringFormat.QueueTextFileFormat));  // Format of Queue is now user configurable

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
                var statusfile = Path.Combine(Plugin.DataPath, "queuestatus.txt");
                File.WriteAllText(statusfile, status);
            }

            catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public void Shuffle<T>(List<T> list)
        {
            var n = list.Count;
            while (n > 1) {
                n--;
                var k = Generator.Next(0, n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public string QueueLottery(ParseState state)
        {
            Int32.TryParse(state.Parameter, out var entrycount);
            var list = RequestManager.RequestSongs.OfType<SongRequest>().ToList();
            this.Shuffle(list);


            //var list = RequestManager.RequestSongs.OfType<SongRequest>().ToList();
            for (var i = entrycount; i < list.Count; i++) {
                try {
                    if (RequestTracker.ContainsKey(list[i]._requestor.Id))
                        RequestTracker[list[i]._requestor.Id].numRequests--;
                    this.ListCollectionManager.Remove(duplicatelist, list[i].SongNode["id"]);
                }
                catch { }
            }

            if (entrycount > 0) {
                try {
                    this.Writedeck(state.User, "prelotto");
                    list.RemoveRange(entrycount, RequestManager.RequestSongs.Count - entrycount);
                }
                catch { }
            }
            RequestManager.RequestSongs.Clear();
            RequestManager.RequestSongs.AddRange(list);
            this._requestManager.WriteRequest();

            // Notify the chat that the queue was cleared
            this.ChatManager.QueueChatMessage($"Queue lottery complete!");

            this.ToggleQueue(state.User, state.Parameter, false); // Close the queue.
            // Reload the queue
            this.UpdateRequestUI();
            this.RefreshSongQuere();
            this.RefreshQueue = true;
            return success;
        }

        public void Clearqueue(IChatUser requestor, string request)
        {
            // Write our current queue to file so we can restore it if needed
            this.Writedeck(requestor, "justcleared");

            // Cycle through each song in the final request queue, adding them to the song history

            while (RequestManager.RequestSongs.Count > 0)
                this.DequeueRequest(RequestManager.RequestSongs.FirstOrDefault() as SongRequest, false); // More correct now, previous version did not keep track of user requests 

            this._requestManager.WriteRequest();

            // Update the request button ui accordingly
            this.UpdateRequestUI();

            // Notify the chat that the queue was cleared
            this.ChatManager.QueueChatMessage($"Queue is now empty.");

            // Reload the queue
            this.RefreshSongQuere();
            this.RefreshQueue = true;
        }

        #endregion

        #region Unmap/Remap Commands
        public void Remap(IChatUser requestor, string request)
        {
            var parts = request.Split(',', ' ');

            if (parts.Length < 2) {
                this.ChatManager.QueueChatMessage("usage: !remap <songid>,<songid>, omit the <>'s");
                return;
            }

            if (songremap.ContainsKey(parts[0]))
                songremap.Remove(parts[0]);
            songremap.Add(parts[0], parts[1]);
            this.ChatManager.QueueChatMessage($"Song {parts[0]} remapped to {parts[1]}");
            this.WriteRemapList();
        }

        public void Unmap(IChatUser requestor, string request)
        {

            if (songremap.ContainsKey(request)) {
                this.ChatManager.QueueChatMessage($"Remap entry {request} removed.");
                songremap.Remove(request);
            }
            this.WriteRemapList();
        }

        public void WriteRemapList()
        {

            // BUG: Its more efficient to write it in one call

            try {
                var remapfile = Path.Combine(Plugin.DataPath, "remap.list");

                var sb = new StringBuilder();

                foreach (var entry in songremap) {
                    sb.Append($"{entry.Key},{entry.Value}\n");
                }
                File.WriteAllText(remapfile, sb.ToString());
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public void ReadRemapList()
        {
            var remapfile = Path.Combine(Plugin.DataPath, "remap.list");

            if (!File.Exists(remapfile)) {
                using (var file = File.Create(remapfile)) { };
            }

            try {
                var fileContent = File.ReadAllText(remapfile);

                var maps = fileContent.Split('\r', '\n');
                foreach (var map in maps) {
                    var parts = map.Split(',', ' ');
                    if (parts.Length > 1)
                        songremap.Add(parts[0], parts[1]);
                }
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }
        }
        #endregion

        #region Wrong Song
        public void WrongSong(IChatUser requestor, string request)
        {
            // Note: Scanning backwards to remove LastIn, for loop is best known way.
            for (var i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                if (RequestManager.RequestSongs.ToArray()[i] is SongRequest song) {
                    if (song._requestor.Id == requestor.Id) {
                        this.ChatManager.QueueChatMessage($"{song.SongMetaData["songName"].Value} ({song.SongNode["id"].Value}) removed.");

                        this.ListCollectionManager.Remove(duplicatelist, song.SongNode["id"].Value);
                        this.Skip(song, RequestStatus.Wrongsong);
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
                var song = RequestManager.HistorySongs.FirstOrDefault() as SongRequest;
                if (song != null) {
                    var json = song.SongMetaData;
                    this._textFactory.Create().AddSong(json).QueueMessage(StringFormat.LinkSonglink.ToString());
                }   
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }

            return success;
        }

        public string Queueduration()
        {
            var total = 0;
            try {
                foreach (var songrequest in RequestManager.RequestSongs.OfType<SongRequest>()) {
                    total += songrequest.SongMetaData["songduration"];
                }
            }
            catch {


            }

            return $"{total / 60}:{ total % 60:00}";
        }

        public string QueueStatus(ParseState state)
        {
            var queuestate = RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open. " : "Queue is closed. ";
            this.ChatManager.QueueChatMessage($"{queuestate} There are {RequestManager.RequestSongs.Count} maps ({this.Queueduration()}) in the queue.");
            return success;
        }
        #region DynamicText class and support functions.



        #endregion
        #endregion

        #region ListManager
        public void Showlists(IChatUser requestor, string request)
        {
            var msg = this._messageFactroy.Create();
            msg.Header("Loaded lists: ");
            foreach (var entry in this.ListCollectionManager.ListCollection)
                msg.Add($"{entry.Key} ({entry.Value.Count()})", ", ");
            msg.End("...", "No lists loaded.");
        }

        public string Listaccess(ParseState state)
        {
            this.ChatManager.QueueChatMessage($"Hi, my name is {state._botcmd.UserParameter} , and I'm a list object!");
            return success;
        }

        public void Addtolist(IChatUser requestor, string request)
        {
            var parts = request.Split(new char[] { ' ', ',' }, 2);
            if (parts.Length < 2) {
                this.ChatManager.QueueChatMessage("Usage text... use the official help method");
                return;
            }

            try {

                this.ListCollectionManager.Add(parts[0], parts[1]);
                this.ChatManager.QueueChatMessage($"Added {parts[1]} to {parts[0]}");

            }
            catch {
                this.ChatManager.QueueChatMessage($"list {parts[0]} not found.");
            }
        }

        public void ListList(IChatUser requestor, string request)
        {
            try {
                var list = this.ListCollectionManager.OpenList(request);

                var msg = this._messageFactroy.Create();
                foreach (var entry in list.list)
                    msg.Add(entry, ", ");
                msg.End("...", $"{request} is empty");
            }
            catch {
                this.ChatManager.QueueChatMessage($"{request} not found.");
            }
        }

        public void RemoveFromlist(IChatUser requestor, string request)
        {
            var parts = request.Split(new char[] { ' ', ',' }, 2);
            if (parts.Length < 2) {
                //     NewCommands[Addtolist].ShortHelp();
                this.ChatManager.QueueChatMessage("Usage text... use the official help method");
                return;
            }

            try {

                this.ListCollectionManager.Remove(ref parts[0], ref parts[1]);
                this.ChatManager.QueueChatMessage($"Removed {parts[1]} from {parts[0]}");

            }
            catch {
                this.ChatManager.QueueChatMessage($"list {parts[0]} not found.");
            }
        }

        public void ClearList(IChatUser requestor, string request)
        {
            try {
                this.ListCollectionManager.ClearList(request);
                this.ChatManager.QueueChatMessage($"{request} is cleared.");
            }
            catch {
                this.ChatManager.QueueChatMessage($"Unable to clear {request}");
            }
        }

        public void UnloadList(IChatUser requestor, string request)
        {
            try {
                this.ListCollectionManager.ListCollection.Remove(request.ToLower());
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
                var list = this.ListCollectionManager.OpenList(state.Parameter);
                foreach (var entry in list.list)
                    this.ProcessSongRequest(this._stateFactory.Create().Setup(state, entry)); // Must use copies here, since these are all threads
            }
            catch (Exception ex) { Logger.Error(ex); } // Going to try this form, to reduce code verbosity.              
            return success;
        }

        // Remove entire list from queue
        public string Unqueuelist(ParseState state)
        {
            state.Flags |= FlagParameter.Silent;
            foreach (var entry in this.ListCollectionManager.OpenList(state.Parameter).list) {
                state.Parameter = entry;
                this.DequeueSong(state);
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

        public void OpenList(IChatUser requestor, string request) => this.ListCollectionManager.OpenList(request.ToLower());

        public List<JSONObject> ReadJSON(string path)
        {
            var objs = new List<JSONObject>();
            if (File.Exists(path)) {
                var json = JSON.Parse(File.ReadAllText(path));
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

            var arr = new JSONArray();
            foreach (var obj in objs)
                arr.Add(obj);

            File.WriteAllText(path, arr.ToString());
        }
        #endregion
        #endregion

        #region Utilties
        public void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (var dir in source.GetDirectories()) {
                this.CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            }

            foreach (var file in source.GetFiles()) {
                var newFilePath = Path.Combine(target.FullName, file.Name);
                try {
                    file.CopyTo(newFilePath);
                }
                catch (Exception) {
                }
            }
        }

        public string BackupStreamcore(ParseState state)
        {
            var errormsg = this.Backup();
            if (errormsg == "")
                state.Msg("SRManager files backed up.");
            return errormsg;
        }
        public string Backup()
        {
            var now = DateTime.Now;
            var BackupName = Path.Combine(RequestBotConfig.Instance.BackupPath, $"SRMBACKUP-{now.ToString("yyyy-MM-dd-HHmm")}.zip");
            try {
                if (!Directory.Exists(RequestBotConfig.Instance.BackupPath))
                    Directory.CreateDirectory(RequestBotConfig.Instance.BackupPath);

                ZipFile.CreateFromDirectory(Plugin.DataPath, BackupName, System.IO.Compression.CompressionLevel.Fastest, true);
                RequestBotConfig.Instance.LastBackup = now.ToString();
            }
            catch (Exception ex) {
                Logger.Error(ex);
                return $"Failed to backup to {BackupName}";
            }
            return success;
        }
        #endregion

        #region SongDatabase
        public const int partialhash = 3; // Do Not ever set this below 4. It will cause severe performance loss

        // Song primary key can be song ID/version , or level hashes. This dictionary is many:1
        public bool CreateMD5FromFile(string path, out string hash)
        {
            hash = "";
            if (!File.Exists(path))
                return false;
            using (var md5 = MD5.Create()) {
                using (var stream = File.OpenRead(path)) {
                    var hashBytes = md5.ComputeHash(stream);

                    var sb = new StringBuilder();
                    foreach (var hashByte in hashBytes) {
                        sb.Append(hashByte.ToString("X2"));
                    }

                    hash = sb.ToString();
                    return true;
                }
            }
        }
        public List<JSONObject> GetSongListFromResults(JSONNode result, string searchString, ref string errorMessage, SongFilter filter = SongFilter.All, string sortby = "-rating", int reverse = 1)
        {
            var songs = new List<JSONObject>();

            if (result != null) {
                // Add query results to out song database.
                if (result["docs"].IsArray) {
                    var downloadedsongs = result["docs"].AsArray;
                    foreach (JSONObject currentSong in downloadedsongs.Children) {
                        this._songMapFactory.Create(currentSong);
                    }
                }
                else {
                    this._songMapFactory.Create(result.AsObject);
                }
            }

            var list = this.MapDatabase.Search(searchString);

            try {
                var sortorder = sortby.Split(' ');

                list.Sort(delegate (SongMap c1, SongMap c2)
                {
                    return reverse * this.CompareSong(c1.SongObject, c2.SongObject, ref sortorder);
                });
            }
            catch (Exception e) {
                //this.ChatManager.QueueChatMessage($"Exception {e} sorting song list");
                Logger.Error($"Exception sorting a returned song list.");
                Logger.Error(e);
            }

            foreach (var song in list) {
                errorMessage = this.SongSearchFilter(song.SongObject, false, filter);
                if (errorMessage == "")
                    songs.Add(song.SongObject);
            }

            return songs;
        }

        public IEnumerator RefreshSongs(ParseState state)
        {

            this.MapDatabase.LoadCustomSongs().Await(null, null, null);
            yield break;
        }

        public string GetGCCount(ParseState state)
        {
            state.Msg($"Gc0:{GC.CollectionCount(0)} GC1:{GC.CollectionCount(1)} GC2:{GC.CollectionCount(2)}");
            state.Msg($"{GC.GetTotalMemory(false)}");
            return success;
        }


        public Task ReadArchive(ParseState state) => this.MapDatabase.LoadZIPDirectory();

        public IEnumerator SaveSongDatabase(ParseState state)
        {
            this.MapDatabase.SaveDatabase();
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
                for (var i = 0; i < hashBytes.Length; i++) {
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
                    return;
                }
                pploading = true;
                var resp = await WebClient.GetAsync(SCRAPED_SCORE_SABER_ALL_JSON_URL, System.Threading.CancellationToken.None);

                if (!resp.IsSuccessStatusCode) {
                    pploading = false;
                    return;
                }
                //Instance.this.ChatManager.QueueChatMessage($"Parsing PP Data {result.Length}");

                var rootNode = resp.ConvertToJsonNode();

                this.ListCollectionManager.ClearList("pp.deck");

                foreach (var kvp in rootNode) {
                    var difficultyNodes = kvp.Value;
                    var key = difficultyNodes["key"].Value;

                    //Instance.this.ChatManager.QueueChatMessage($"{id}");
                    var maxpp = difficultyNodes["diffs"].AsArray.Linq.Max(x => x.Value["pp"].AsFloat);
                    var maxstar = difficultyNodes["diffs"].AsArray.Linq.Max(x => x.Value["star"].AsFloat);
                    if (maxpp > 0) {
                        //Instance.this.ChatManager.QueueChatMessage($"{id} = {maxpp}");
                        MapDatabase.PPMap.TryAdd(key, maxpp);
                        if (key != "" && maxpp > 100)
                            this.ListCollectionManager.Add("pp.deck", key);

                        if (MapDatabase.MapLibrary.TryGetValue(key, out var map)) {
                            map.PP = maxpp;
                            map.SRMInfo.Add("pp", maxpp);
                            map.IndexSong(map.SongObject);
                        }
                    }
                }
                this.Parse(this.GetLoginUser(), "!deck pp", CmdFlags.Local);

                // this.ChatManager.QueueChatMessage("PP Data indexed");
                pploading = false;
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
        #endregion
    }
}