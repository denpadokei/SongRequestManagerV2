﻿using CatCore.Models.Shared;
using CatCore.Models.Twitch.IRC;
using CatCore.Services.Multiplexer;
using IPA.Loader;
using IPA.Utilities;
using SongRequestManagerV2.Bases;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Extentions;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.SimpleJSON;
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
using System.Web;
using UnityEngine.SceneManagement;
using Zenject;

#if DEBUG
using System.Diagnostics;
#endif
#if OLDVERSION
using TMPro;
#endif

namespace SongRequestManagerV2.Bots
{
    internal class RequestBot : BindableBase, IRequestBot, IInitializable, IDisposable
    {
        public static Dictionary<string, RequestUserTracker> RequestTracker { get; } = new Dictionary<string, RequestUserTracker>();
        public bool RefreshQueue { get; private set; } = false;
        private readonly bool _mapperWhitelist = false; // BUG: Need to clean these up a bit.
        private bool _isGameCore = false;

        public static System.Random Generator { get; } = new System.Random(Environment.TickCount); // BUG: Should at least seed from unity?
        public static List<JSONObject> Played { get; private set; } = new List<JSONObject>(); // Played list
        public static List<BotEvent> Events { get; } = new List<BotEvent>();
        public static UserInfo CurrentUser { get; private set; }


        private static StringListManager s_mapperwhitelist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static StringListManager s_mapperBanlist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static readonly string s_duplicatelist = "duplicate.list"; // BUG: Name of the list, needs to use a different interface for this.
        private static readonly string s_banlist = "banlist.unique"; // BUG: Name of the list, needs to use a different interface for this.
        private static readonly string s_whitelist = "whitelist.unique"; // BUG: Name of the list, needs to use a different interface for this.
        private static readonly string s_blockeduser = "blockeduser.unique";

        private static readonly Dictionary<string, string> s_songremap = new Dictionary<string, string>();
        public static Dictionary<string, string> deck = new Dictionary<string, string>(); // deck name/content

        private static readonly Regex s_digitRegex = new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex s_beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);

        public const string SCRAPED_SCORE_SABER_ALL_JSON_URL = "https://cdn.wes.cloud/beatstar/bssb/v2-ranked.json";
        public const string BEATMAPS_API_ROOT_URL = "https://api.beatsaver.com";
        public const string BEATMAPS_CDN_ROOT_URL = "https://cdn.beatsaver.com";
        public const string BEATMAPS_AS_CDN_ROOT_URL = "https://as.cdn.beatsaver.com";
        public const string BEATMAPS_NA_CDN_ROOT_URL = "https://na.cdn.beatsaver.com";

        private readonly System.Timers.Timer _timer = new System.Timers.Timer(500);

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
        private readonly NotifySound _notifySound;
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
        private SongRequest _currentSong;
        /// <summary>SongRequest を取得、設定</summary>
        public SongRequest CurrentSong
        {
            get => this._currentSong;

            set => this.SetProperty(ref this._currentSong, value);
        }
        public SongRequest PlayNow { get; set; }
        /// <summary>
        /// This is string empty.
        /// </summary>
        private static readonly string s_success = "";
        #region 構築・破棄
        [Inject]
        protected void Constractor(IPlatformUserModel platformUserModel)
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
            if (CurrentUser == null) {
                platformUserModel.GetUserInfo().Await(r =>
                {
                    CurrentUser = r;
                });
            }
        }
        public void Initialize()
        {
            Logger.Debug("Start Initialize");
            SceneManager.activeSceneChanged += this.SceneManager_activeSceneChanged;
            this._timer.Elapsed += this.Timer_Elapsed;
            this._timer.Start();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposedValue) {
                if (disposing) {
                    Logger.Debug("Dispose call");
                    this._timer.Elapsed -= this.Timer_Elapsed;
                    this._timer.Dispose();
                    SceneManager.activeSceneChanged -= this.SceneManager_activeSceneChanged;
                    RequestBotConfig.Instance.ConfigChangedEvent -= this.OnConfigChangedEvent;
                }
                this._disposedValue = true;
            }
        }
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
                if (item.Status == RequestStatus.SongSearch) {
                    this.DequeueRequest(item, false);
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

        internal void RecievedMessages(MultiplexedMessage msg)
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
            this.WriteQueueStatusToFile(this.QueueMessage(RequestBotConfig.Instance.RequestQueueOpen));
            UpdateUIRequest?.Invoke(true);
            SetButtonIntactivityRequest?.Invoke(true);
        }

        // BUG: Prototype code, used for testing.


        public void ScheduledCommand(string command, ElapsedEventArgs e)
        {
            this.Parse(this.GetLoginUser(), command);
        }

        public void RunStartupScripts()
        {
            this.ReadRemapList(); // BUG: This should use list manager

            this.MapperBanList(this.GetLoginUser(), "mapperban.list");
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
            this._timer.Stop();
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
                this._timer.Start();
            }
        }

        private void Setup()
        {
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
                    if (PlayedAge < TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) {
                        Played = this.ReadJSON(playedfilename); // Read the songsplayed file if less than x hours have passed 
                    }
                }
                catch (Exception ex) {
                    Logger.Error(ex);
                    this.ChatManager.QueueChatMessage("Failed to clear played file");

                }
                this._requestManager.ReadRequest(); // Might added the timespan check for this too. To be decided later.
                this._requestManager.ReadHistory();
                this.ListCollectionManager.OpenList("banlist.unique");
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
        }

        private void SendChatMessage(string message)
        {
            try {
                Logger.Debug($"Sending message: \"{message}\"");

                if (this.ChatManager.TwitchChannelManagementService != null) {
                    foreach (var channel in this.ChatManager.TwitchChannelManagementService.GetAllActiveChannels()) {
                        channel.SendMessage($"{message}");
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
                        if (this.MapDatabase.PPMap.TryGetValue(song1["id"].Value, out var pp1) && this.MapDatabase.PPMap.TryGetValue(song2["id"].Value, out var pp2)) {
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
                if (result == 0) {
                    continue;
                }

                if (s[0] == '-') {
                    return -result;
                }

                return result;
            }
            return result;
        }

        internal async Task UpdateSongMap(JSONObject song)
        {
            var resp = await WebClient.GetAsync($"{BEATMAPS_API_ROOT_URL}/maps/id/{song["id"].Value}", System.Threading.CancellationToken.None);
            if (resp.IsSuccessStatusCode) {
                var result = resp.ConvertToJsonNode();
                this.ChatManager.QueueChatMessage($"{result.AsObject}");
                if (result != null && result["id"].Value != "") {
                    var map = this._songMapFactory.Create(result.AsObject, "", "");
                    this.MapDatabase.IndexSong(map);
                }
            }
        }

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
            Logger.Debug($"id value : {id}");
            Logger.Debug($"normalrequest value : {normalrequest}");
            try {
                if (!string.IsNullOrEmpty(id)) {
                    // Remap song id if entry present. This is one time, and not correct as a result. No recursion right now, could be confusing to the end user.
                    if (s_songremap.ContainsKey(id) && !requestInfo.Flags.HasFlag(CmdFlags.NoFilter)) {
                        request = s_songremap[id];
                        this.ChatManager.QueueChatMessage($"Remapping request {requestInfo.Request} to {request}");
                    }

                    var requestcheckmessage = this.IsRequestInQueue(this.Normalize.RemoveSymbols(request, this.Normalize.SymbolsNoDash));               // Check if requested ID is in Queue  
                    if (requestcheckmessage != "") {
                        this.ChatManager.QueueChatMessage(requestcheckmessage);
                        return;
                    }
                }

                JSONNode result = null;

                var errorMessage = "";

                // Get song query results from beatsaver.com
                var requestUrl = "";
                WebResponse resp = null;
                if (!string.IsNullOrEmpty(id)) {
                    var idWithoutSymbols = this.Normalize.RemoveSymbols(request, this.Normalize.SymbolsNoDash);
                    requestUrl = $"{BEATMAPS_API_ROOT_URL}/maps/id/{idWithoutSymbols}";
                    resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);
                }
                if (resp == null || resp.StatusCode == System.Net.HttpStatusCode.NotFound) {
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
                var serchString = result != null ? result["id"].Value : "";
                var songs = this.GetSongListFromResults(result, serchString, SongFilter.none, requestInfo.State.Sort != "" ? requestInfo.State.Sort : StringFormat.AddSortOrder.ToString());
                var autopick = RequestBotConfig.Instance.AutopickFirstSong || requestInfo.Flags.HasFlag(CmdFlags.Autopick);
                // Filter out too many or too few results
                if (!songs.Any()) {
                    errorMessage = $"No results found for request \"{request}\"";
                }
                else if (!autopick && songs.Count >= 4) {
                    errorMessage = $"Request for '{request}' produces {songs.Count()} results, narrow your search by adding a mapper name, or use https://beatsaver.com to look it up.";
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
                    if (!requestInfo.Flags.HasFlag(CmdFlags.NoFilter)) {
                        errorMessage = this.SongSearchFilter(songs.First(), false);
                    }
                    else {
                        errorMessage = this.SongSearchFilter(songs.First(), false, SongFilter.Queue);
                    }
                }

                // Display reason why chosen song was rejected, if filter is triggered. Do not add filtered songs
                if (!string.IsNullOrEmpty(errorMessage)) {
                    this.ChatManager.QueueChatMessage(errorMessage);
                    return;
                }
                var song = songs[0];
                var req = this._songRequestFactory.Create();
                req.Init(song, requestor, requestInfo.RequestTime, RequestStatus.Queued, requestInfo.RequestInfoText);
                RequestTracker[requestor.Id].numRequests++;
                this.ListCollectionManager.Add(s_duplicatelist, song["id"]);
                if (RequestBotConfig.Instance.NotifySound) {
                    this._notifySound.PlaySound();
                }
                if ((requestInfo.Flags.HasFlag(CmdFlags.MoveToTop))) {
                    var reqs = new List<SongRequest>() { req };
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
            catch (NullReferenceException nullex) {
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

        public void RefreshSongQuere()
        {
            Dispatcher.RunOnMainThread(() =>
            {
                RefreshListRequest?.Invoke(false);
                this.RefreshQueue = true;
            });
        }

        public void DequeueRequest(SongRequest request, bool updateUI = true)
        {
            try {
                // Wrong song requests are not logged into history, is it possible that other status states shouldn't be moved either?
                if ((request.Status & (RequestStatus.Wrongsong | RequestStatus.SongSearch)) == 0) {
                    var reqs = new List<SongRequest>() { request };
                    var newList = reqs.Union(RequestManager.HistorySongs.ToArray());
                    RequestManager.HistorySongs.Clear();
                    RequestManager.HistorySongs.AddRange(newList);
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
                    if (RequestTracker.ContainsKey(request.Requestor.Id)) {
                        RequestTracker[request.Requestor.Id].numRequests--;
                    }
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

        public void SetRequestStatus(SongRequest request, RequestStatus status, bool fromHistory = false)
        {
            request.Status = status;
        }

        public void Blacklist(SongRequest request, bool fromHistory, bool skip)
        {
            // Add the song to the blacklist
            this.ListCollectionManager.Add(s_banlist, request.ID);

            this.ChatManager.QueueChatMessage($"{request.SongMetaData["songName"].Value} by {request.SongMetaData["songAuthorName"].Value} ({request.SongMetaData["id"].Value}) added to the blacklist.");

            if (!fromHistory) {
                if (skip) {
                    this.Skip(request, RequestStatus.Blacklisted);
                }
            }
            else {
                this.SetRequestStatus(request, RequestStatus.Blacklisted, fromHistory);
            }
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
            if (s_digitRegex.IsMatch(request)) {
                return request;
            }

            if (s_beatSaverRegex.IsMatch(request)) {
                var requestparts = request.Split(new char[] { '-' }, 2);
                //return requestparts[0];
                if (int.TryParse(requestparts[1], out var o)) {
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
                    return s_success;
                }

                if (!RequestTracker.ContainsKey(state.User.Id)) {
                    RequestTracker.Add(state.User.Id, new RequestUserTracker());
                }

                var limit = RequestBotConfig.Instance.UserRequestLimit;

                if (state.User is TwitchUser twitchUser) {
                    if (twitchUser.IsSubscriber) {
                        limit = Math.Max(limit, RequestBotConfig.Instance.SubRequestLimit);
                    }

                    if (state.User.IsModerator) {
                        limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                    }

                    if (twitchUser.IsVip) {
                        limit += RequestBotConfig.Instance.VipBonusRequests; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like
                    }
                }
                else {
                    if (state.User.IsModerator) {
                        limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                    }
                }

                if (!state.User.IsBroadcaster && RequestTracker[state.User.Id].numRequests >= limit) {
                    if (RequestBotConfig.Instance.LimitUserRequestsToSession) {
                        this._textFactory.Create().Add("Requests", RequestTracker[state.User.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You've already used %Requests% requests this stream. Subscribers are limited to %RequestLimit%.");
                    }
                    else {
                        this._textFactory.Create().Add("Requests", RequestTracker[state.User.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");
                    }

                    return s_success;
                }

                // BUG: Need to clean up the new request pipeline
                var testrequest = this.Normalize.RemoveSymbols(state.Parameter, this.Normalize.SymbolsNoDash);

                var newRequest = new RequestInfo(state.User, state.Parameter, DateTime.UtcNow, s_digitRegex.IsMatch(testrequest) || s_beatSaverRegex.IsMatch(testrequest), state, state.Flags, state.Info);

                if (!newRequest.IsBeatSaverId && state.Parameter.Length < 2) {
                    this.ChatManager.QueueChatMessage($"Request \"{state.Parameter}\" is too short- Beat Saver searches must be at least 3 characters!");
                }

                if (!this.ChatManager.RequestInfos.Contains(newRequest)) {
                    this.ChatManager.RequestInfos.Enqueue(newRequest);
                }
                return s_success;
            }
            catch (Exception ex) {
                Logger.Error(ex);
                return ex.ToString();
            }
            finally {
                ReceviedRequest?.Invoke();
            }
        }


        public IChatUser GetLoginUser()
        {
            if (this.ChatManager.OwnUserData != null) {
                var user = this.ChatManager.OwnUserData;
                var obj = new
                {
                    Id = user.UserId,
                    UserName = user.UserId,
                    DisplayName = user.DisplayName,
                    Color = user.Color,
                    IsBroadcaster = true,
                    IsModerator = user.IsModerator,
                    IsSubscriber = user.IsSubscriber,
                    IsTurbo = user.IsTurbo,
                    IsVip = user.IsVip,
                    Badges = Array.Empty<IChatBadge>()
                };
                return new TwitchUser(obj.Id, obj.UserName, obj.DisplayName, obj.Color, obj.IsModerator, obj.IsBroadcaster, obj.IsSubscriber, obj.IsTurbo, obj.IsVip, new System.Collections.ObjectModel.ReadOnlyCollection<IChatBadge>(obj.Badges));
            }
            else {
                var isInit = CurrentUser != null;

                var obj = new
                {
                    Id = isInit ? CurrentUser.platformUserId : "",
                    UserName = isInit ? CurrentUser.userName : "",
                    DisplayName = isInit ? CurrentUser.userName : "",
                    Color = "#FFFFFFFF",
                    IsBroadcaster = true,
                    IsModerator = false,
                    IsSubscriber = false,
                    IsPro = false,
                    IsStaff = false,
                    Badges = Array.Empty<IChatBadge>()
                };
                return new TwitchUser(obj.Id, obj.UserName, obj.DisplayName, obj.Color, obj.IsModerator, obj.IsBroadcaster, obj.IsSubscriber, false, false, new System.Collections.ObjectModel.ReadOnlyCollection<IChatBadge>(obj.Badges));
            }
        }
        public void Parse(IChatUser user, string request, CmdFlags flags = 0, string info = "")
        {
            if (string.IsNullOrEmpty(request)) {
                return;
            }

            if (!string.IsNullOrEmpty(user.Id) && this.ListCollectionManager.Contains(s_blockeduser, user.Id.ToLower())) {
                return;
            }

            // This will be used for all parsing type operations, allowing subcommands efficient access to parse state logic
            this._stateFactory.Create().Setup(user, request, flags, info).ParseCommand();
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
                dt.AddSong(RequestManager.HistorySongs.FirstOrDefault().SongNode); // Exposing the current song 
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }

            dt.QueueMessage(state.Parameter);
            return s_success;
        }

        public void RunScript(IChatUser requestor, string request)
        {
            this.ListCollectionManager.Runscript(request);
        }
        #endregion

        #region Filter support functions

        public bool DoesContainTerms(string request, ref string[] terms)
        {
            if (request == "") {
                return false;
            }

            request = request.ToLower();

            foreach (var term in terms) {
                foreach (var word in request.Split(' ')) {
                    if (word.Length > 2 && term.ToLower().Contains(word)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool Filtersong(JSONObject song)
        {
            var songid = song["id"].Value;
            if (this.IsInQueue(songid)) {
                return true;
            }

            if (this.ListCollectionManager.Contains(s_banlist, songid)) {
                return true;
            }

            if (this.ListCollectionManager.Contains(s_duplicatelist, songid)) {
                return true;
            }

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
            if (filter.HasFlag(SongFilter.Queue) && RequestManager.RequestSongs.Any(req => req.ID == songid)) {
                return fast ? "X" : $"Request {metadata["songName"].Value} by {metadata["levelAuthorName"].Value} already exists in queue!";
            }

            if (filter.HasFlag(SongFilter.Blacklist) && this.ListCollectionManager.Contains(s_banlist, songid)) {
                return fast ? "X" : $"{metadata["songName"].Value} by {metadata["levelAuthorName"].Value} ({songid}) is banned!";
            }

            if (filter.HasFlag(SongFilter.Mapper) && this.Mapperfiltered(song, this._mapperWhitelist)) {
                return fast ? "X" : $"{metadata["songName"].Value} by {metadata["levelAuthorName"].Value} does not have a permitted mapper!";
            }

            if (filter.HasFlag(SongFilter.Duplicate) && this.ListCollectionManager.Contains(s_duplicatelist, songid)) {
                return fast ? "X" : $"{metadata["songName"].Value} by  {metadata["levelAuthorName"].Value} already requested this session!";
            }

            if (this.ListCollectionManager.Contains(s_whitelist, songid)) {
                return "";
            }

            if (filter.HasFlag(SongFilter.Duration) && metadata["duration"].AsFloat > RequestBotConfig.Instance.MaximumSongLength * 60) {
                return fast ? "X" : $"{metadata["songName"].Value} ({metadata["duration"].Value}) by {metadata["levelAuthorName"].Value} ({songid}) is too long!";
            }

            var njs = 0f;
            foreach (var diff in version["diffs"].AsArray.Children) {
                if (njs < diff["njs"].AsFloat) {
                    njs = diff["njs"].AsFloat;
                }
            }

            if (filter.HasFlag(SongFilter.NJS) && njs < RequestBotConfig.Instance.MinimumNJS) {
                return fast ? "X" : $"{metadata["songName"].Value} ({metadata["duration"].Value}) by {metadata["levelAuthorName"].Value} ({songid}) NJS ({njs}) is too low!";
            }

            if (filter.HasFlag(SongFilter.Remap) && s_songremap.ContainsKey(songid)) {
                return fast ? "X" : $"no permitted results found!";
            }

            if (filter.HasFlag(SongFilter.Rating) && stats["score"].AsFloat < RequestBotConfig.Instance.LowestAllowedRating && stats["score"].AsFloat != 0) {
                return fast ? "X" : $"{metadata["songName"].Value} by {metadata["levelAuthorName"].Value} is below {RequestBotConfig.Instance.LowestAllowedRating}% rating!";
            }

            return "";
        }

        // checks if request is in the RequestManager.RequestSongs - needs to improve interface
        public string IsRequestInQueue(string request, bool fast = false)
        {
            foreach (var req in RequestManager.RequestSongs) {
                var song = req.SongNode;
                if (string.Equals(req.ID, request, StringComparison.InvariantCultureIgnoreCase)) {
                    return fast ? "X" : $"Request {song["songName"].Value} by {song["songAuthorName"].Value} ({req.ID}) already exists in queue!";
                }
            }
            return ""; // Empty string: The request is not in the RequestManager.RequestSongs
        }
        // unhappy about naming here
        private bool IsInQueue(string request)
        {
            return !(this.IsRequestInQueue(request) == "");
        }

        public string ClearDuplicateList(ParseState state)
        {
            if (!state._botcmd.Flags.HasFlag(CmdFlags.SilentResult)) {
                this.ChatManager.QueueChatMessage("Session duplicate list is now clear.");
            }

            this.ListCollectionManager.ClearList(s_duplicatelist);
            return s_success;
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

            if (this.ListCollectionManager.Contains(s_banlist, id)) {
                this.ChatManager.QueueChatMessage($"{id} is already on the ban list.");
                return;
            }

            if (!this.MapDatabase.MapLibrary.TryGetValue(id, out var song)) {
                JSONNode result = null;
                var requestUrl = $"{BEATMAPS_API_ROOT_URL}/maps/id/{id}";
                var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode) {
                    result = resp.ConvertToJsonNode();
                }
                else {
                    Logger.Debug($"Ban: Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                }

                if (result != null) {
                    song = this._songMapFactory.Create(result.AsObject, "", "");
                    this.MapDatabase.IndexSong(song);
                }
            }

            this.ListCollectionManager.Add(s_banlist, id);

            if (song == null) {
                this.ChatManager.QueueChatMessage($"{id} is now on the ban list.");
            }
            else {
                state.Msg(this._textFactory.Create().AddSong(song.SongObject).Parse(StringFormat.BanSongDetail), ", ");
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

            if (this.ListCollectionManager.Contains(s_banlist, unbanvalue)) {
                this.ChatManager.QueueChatMessage($"Removed {request} from the ban list.");
                this.ListCollectionManager.Remove(s_banlist, unbanvalue);
            }
            else {
                this.ChatManager.QueueChatMessage($"{request} is not on the ban list.");
            }
        }
        #endregion

        #region Deck Commands
        public string Restoredeck(ParseState state)
        {
            return this.Readdeck(this._stateFactory.Create().Setup(state, "savedqueue"));
        }

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

                foreach (var req in RequestManager.RequestSongs.ToArray()) {
                    var song = req.SongNode;
                    if (count > 0) {
                        sb.Append(",");
                    }

                    sb.Append(req.ID);
                    count++;
                }
                File.WriteAllText(queuefile, sb.ToString());
                if (request != "savedqueue") {
                    this.ChatManager.QueueChatMessage($"wrote {count} entries to {request}");
                }
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
                    if (this.IsInQueue(integerStrings[n])) {
                        continue;
                    }

                    var newstate = this._stateFactory.Create().Setup(state); // Must use copies here, since these are all threads
                    newstate.Parameter = integerStrings[n];
                    this.ProcessSongRequest(newstate);
                }
            }
            catch {
                this.ChatManager.QueueChatMessage("Unable to read deck {request}.");
            }

            return s_success;
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
                        var terms = new string[] { song.SongMetaData["songName"].Value, song.SongMetaData["songSubName"].Value, song.SongMetaData["songAuthorName"].Value, song.ID, song.Requestor.UserName };

                        if (this.DoesContainTerms(state.Parameter, ref terms)) {
                            dequeueSong = true;
                        }
                    }
                    else {
                        if (song.ID == songId) {
                            dequeueSong = true;
                        }
                    }

                    if (dequeueSong) {
                        this.ChatManager.QueueChatMessage($"{song.SongMetaData["songName"].Value} ({song.ID}) removed.");
                        this.Skip(song);
                        return s_success;
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
            s_mapperwhitelist = this.ListCollectionManager.OpenList(key); // BUG: this is still not the final interface
            this.ChatManager.QueueChatMessage($"Mapper whitelist set to {request}.");
        }

        public void MapperBanList(IChatUser requestor, string request)
        {
            var key = request.ToLower();
            s_mapperBanlist = this.ListCollectionManager.OpenList(key);
            //this.ChatManager.QueueChatMessage($"Mapper ban list set to {request}.");
        }

        // Not super efficient, but what can you do
        public bool Mapperfiltered(JSONObject song, bool white)
        {
            if (song["metadata"].IsObject) {
                song = song["metadata"].AsObject;
            }
            var normalizedauthor = song["levelAuthorName"].Value.ToLower();
            if (white && s_mapperwhitelist.list.Any()) {
                foreach (var mapper in s_mapperwhitelist.list) {
                    if (normalizedauthor.Contains(mapper)) {
                        return false;
                    }
                }
                return true;
            }

            foreach (var mapper in s_mapperBanlist.list) {
                if (normalizedauthor.Contains(mapper)) {
                    return true;
                }
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
                var song = entry.SongNode;
                var songMeta = entry.SongMetaData;

                if (string.IsNullOrEmpty(songId)) {
                    var terms = new string[] { songMeta["songName"].Value, songMeta["songSubName"].Value, songMeta["songAuthorName"].Value, songMeta["levelAuthorName"].Value, entry.ID, entry.Requestor.UserName };

                    if (this.DoesContainTerms(request, ref terms)) {
                        result = entry;

                        if (lastuser != result.Requestor.UserName) {
                            qm.Add($"{result.Requestor.UserName}: ");
                        }

                        qm.Add($"{result.SongMetaData["songName"].Value} ({result.ID})", ",");
                        lastuser = result.Requestor.UserName;
                    }
                }
                else {
                    if (string.Equals(entry.ID, songId, StringComparison.InvariantCultureIgnoreCase)) {
                        result = entry;
                        qm.Add($"{result.Requestor.UserName}: {result.SongMetaData["songName"].Value} ({result.ID})");
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
            return s_success;
        }

        public string Every(ParseState state)
        {
            var parts = state.Parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out var period)) {
                return state.Error($"You must specify a time in minutes after {state.Command}.");
            }

            if (period < 1) {
                return state.Error($"You must specify a period of at least 1 minute");
            }

            Events.Add(new BotEvent(TimeSpan.FromMinutes(period), parts[1], true, (s, e) => this.ScheduledCommand(s, e)));
            return s_success;
        }

        public string EventIn(ParseState state)
        {
            var parts = state.Parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out var period)) {
                return state.Error($"You must specify a time in minutes after {state.Command}.");
            }

            if (period < 0) {
                return state.Error($"You must specify a period of at least 0 minutes");
            }

            Events.Add(new BotEvent(TimeSpan.FromMinutes(period), parts[1], false, (s, e) => this.ScheduledCommand(s, e)));
            return s_success;
        }
        public string Who(ParseState state)
        {

            var qm = this._messageFactroy.Create();

            var result = this.FindMatch(RequestManager.RequestSongs.OfType<SongRequest>(), state.Parameter, qm);
            if (result == null) {
                result = this.FindMatch(RequestManager.HistorySongs.OfType<SongRequest>(), state.Parameter, qm);
            }

            //if (result != null) this.ChatManager.QueueChatMessage($"{result.song["songName"].Value} requested by {result.requestor.displayName}.");
            if (result != null) {
                qm.End("...");
            }

            return "";
        }

        public string SongMsg(ParseState state)
        {
            var parts = state.Parameter.Split(new char[] { ' ', ',' }, 2);
            var songId = this.GetBeatSaverId(parts[0]);
            if (songId == "") {
                return state.Helptext(true);
            }

            foreach (var entry in RequestManager.RequestSongs.OfType<SongRequest>()) {
                var songMeta = entry.SongMetaData;

                if (entry.ID == songId) {
                    entry.RequestInfo = "!" + parts[1];
                    this.ChatManager.QueueChatMessage($"{songMeta["songName"].Value} : {parts[1]}");
                    return s_success;
                }
            }
            this.ChatManager.QueueChatMessage($"Unable to find {songId}");
            return s_success;
        }

        public IEnumerator SetBombState(ParseState state)
        {
            state.Parameter = state.Parameter.ToLower();

            if (state.Parameter == "on") {
                state.Parameter = "enable";
            }

            if (state.Parameter == "off") {
                state.Parameter = "disable";
            }

            if (state.Parameter != "enable" && state.Parameter != "disable") {
                state.Msg(state._botcmd.ShortHelp);
                yield break;
            }

            //System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state.parameter}"));

            System.Diagnostics.Process.Start($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state.Parameter}");

            if (PluginManager.GetPlugin("WobbleSaber") != null) {
                var wobblestate = "off";
                if (state.Parameter == "enable") {
                    wobblestate = "on";
                }

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
                        var map = this._songMapFactory.Create(entry, "", "");
                        this.MapDatabase.IndexSong(map);

                        if (this.Mapperfiltered(entry, true)) {
                            continue; // This forces the mapper filter
                        }

                        if (this.Filtersong(entry)) {
                            continue;
                        }

                        if (state.Flags.HasFlag(CmdFlags.Local)) {
                            this.QueueSong(state, entry);
                        }

                        this.ListCollectionManager.Add("latest.deck", entry["id"].Value);
                        totalSongs++;
                    }
                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    break;
                }

                offset++; // Magic beatsaver.com skip static readonlyant.
            }

            if (totalSongs != 0 && state.Flags.HasFlag(CmdFlags.Local)) {
                this.UpdateRequestUI();
                this.RefreshSongQuere();
                this.RefreshQueue = true;
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
                        var map = this._songMapFactory.Create(entry, "", "");
                        this.MapDatabase.IndexSong(map);

                        if (this.Mapperfiltered(entry, true)) {
                            continue; // This forces the mapper filter
                        }

                        if (this.Filtersong(entry)) {
                            continue;
                        }

                        if (state.Flags.HasFlag(CmdFlags.Local)) {
                            this.QueueSong(state, entry);
                        }

                        this.ListCollectionManager.Add("latest.deck", entry["id"].Value);
                        totalSongs++;
                    }
                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    break;
                }

                offset++; // Magic beatsaver.com skip static readonlyant.
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
            var offset = 0;
            this.ListCollectionManager.ClearList("search.deck");
            //state.msg($"Flags: {state.flags}");
            // MaxiumAddScanRange
            while (offset < RequestBotConfig.Instance.MaxiumScanRange) {
                var requestUrl = !string.IsNullOrEmpty(id) ? $"{BEATMAPS_API_ROOT_URL}/maps/id/{this.Normalize.RemoveSymbols(state.Parameter, this.Normalize.SymbolsNoDash)}" : $"{BEATMAPS_API_ROOT_URL}/search/text/0?q={HttpUtility.UrlEncode(this.Normalize.RemoveSymbols(state.Parameter, this.Normalize.SymbolsNoDash))}&sortOrder=Relevance";
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
                        var map = this._songMapFactory.Create(entry, "", "");
                        this.MapDatabase.IndexSong(map);
                        if (this.Mapperfiltered(entry, true)) {
                            continue; // This forces the mapper filter
                        }

                        if (this.Filtersong(entry)) {
                            continue;
                        }

                        if (state.Flags.HasFlag(CmdFlags.Local)) {
                            this.QueueSong(state, entry);
                        }

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
            Logger.Debug($"beat saver id : {id}");
            var requestUrl = (id != "") ? $"{BEATMAPS_API_ROOT_URL}/maps/id/{this.Normalize.RemoveSymbols(state.Parameter, this.Normalize.SymbolsNoDash)}" : $"{BEATMAPS_API_ROOT_URL}/search/text/0?q={HttpUtility.UrlEncode(this.Normalize.RemoveSymbols(state.Parameter, this.Normalize.SymbolsNoDash))}&sortOrder=Relevance";
            Logger.Debug($"{state.Parameter}");
            Logger.Debug($"{state.Request}");
            Logger.Debug($"{requestUrl}");
            JSONNode result = null;
            var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

            if (resp != null && resp.IsSuccessStatusCode) {
                result = resp.ConvertToJsonNode();

            }
            else {
                Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {state.Parameter}!");
            }
            var filter = SongFilter.All;
            if (state.Flags.HasFlag(CmdFlags.NoFilter)) {
                filter = SongFilter.Queue;
            }
            var songs = this.GetSongListFromResults(result, state.Parameter, filter, state.Sort != "" ? state.Sort : StringFormat.LookupSortOrder.ToString(), -1);
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
                var newList = (new List<SongRequest>() { req }).Union(RequestManager.RequestSongs.ToArray());
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
            this.MoveRequestPositionInQueue(requestor, request, true);
        }

        public void MoveRequestToBottom(IChatUser requestor, string request)
        {
            this.MoveRequestPositionInQueue(requestor, request, false);
        }

        public void MoveRequestPositionInQueue(IChatUser requestor, string request, bool top)
        {

            var moveId = this.GetBeatSaverId(request);
            for (var i = RequestManager.RequestSongs.Count - 1; i >= 0; i--) {
                var req = RequestManager.RequestSongs.ElementAt(i);
                var song = req.SongNode;
                var songMeta = req.SongMetaData;
                

                var moveRequest = false;
                if (string.IsNullOrEmpty(moveId)) {
                    var terms = new string[] { songMeta["songName"].Value, songMeta["songSubName"].Value, songMeta["songAuthorName"].Value, songMeta["levelAuthorName"].Value, req.ID, (RequestManager.RequestSongs.ToArray()[i]).Requestor.UserName };
                    if (this.DoesContainTerms(request, ref terms)) {
                        moveRequest = true;
                    }
                }
                else {
                    if (req.ID == moveId) {
                        moveRequest = true;
                    }
                }

                if (moveRequest) {
                    // Remove the request from the queue
                    var songs = RequestManager.RequestSongs.ToList();
                    songs.RemoveAt(i);
                    RequestManager.RequestSongs.Clear();
                    RequestManager.RequestSongs.AddRange(songs);

                    // Then readd it at the appropriate position
                    if (top) {
                        var tmp = (new List<SongRequest>() { req }).Union(RequestManager.RequestSongs.ToArray());
                        RequestManager.RequestSongs.Clear();
                        RequestManager.RequestSongs.AddRange(tmp);
                    }
                    else {
                        RequestManager.RequestSongs.Add(req);
                    }

                    // Write the modified request queue to file
                    this._requestManager.WriteRequest();

                    // Refresh the queue ui
                    this.RefreshSongQuere();
                    this.RefreshQueue = true;

                    // And write a summary to file
                    this.WriteQueueSummaryToFile();

                    this.ChatManager.QueueChatMessage($"{songMeta["songName"].Value} ({req.ID}) {(top ? "promoted" : "demoted")}.");
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
            this.ToggleQueue(state.User, state.Parameter, true);
            return s_success;
        }

        public string CloseQueue(ParseState state)
        {
            this.ToggleQueue(state.User, state.Parameter, false);
            return s_success;
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

            if (!RequestBotConfig.Instance.UpdateQueueStatusFiles) {
                return;
            }

            try {
                var statusfile = Path.Combine(Plugin.DataPath, "queuelist.txt");
                var queuesummary = new StringBuilder();
                var count = 0;

                foreach (var req in RequestManager.RequestSongs.ToArray()) {
                    var song = req.SongNode;
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
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        public string QueueLottery(ParseState state)
        {
            int.TryParse(state.Parameter, out var entrycount);
            var list = RequestManager.RequestSongs.OfType<SongRequest>().ToList();
            this.Shuffle(list);


            //var list = RequestManager.RequestSongs.OfType<SongRequest>().ToList();
            for (var i = entrycount; i < list.Count; i++) {
                try {
                    if (RequestTracker.ContainsKey(list[i].Requestor.Id)) {
                        RequestTracker[list[i].Requestor.Id].numRequests--;
                    }

                    this.ListCollectionManager.Remove(s_duplicatelist, list[i].ID);
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
            return s_success;
        }

        public void Clearqueue(IChatUser requestor, string request)
        {
            // Write our current queue to file so we can restore it if needed
            this.Writedeck(requestor, "justcleared");

            // Cycle through each song in the final request queue, adding them to the song history

            while (RequestManager.RequestSongs.Count > 0) {
                this.DequeueRequest(RequestManager.RequestSongs.FirstOrDefault(), false); // More correct now, previous version did not keep track of user requests 
            }

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

            if (s_songremap.ContainsKey(parts[0])) {
                s_songremap.Remove(parts[0]);
            }

            s_songremap.Add(parts[0], parts[1]);
            this.ChatManager.QueueChatMessage($"Song {parts[0]} remapped to {parts[1]}");
            this.WriteRemapList();
        }

        public void Unmap(IChatUser requestor, string request)
        {

            if (s_songremap.ContainsKey(request)) {
                this.ChatManager.QueueChatMessage($"Remap entry {request} removed.");
                s_songremap.Remove(request);
            }
            this.WriteRemapList();
        }

        public void WriteRemapList()
        {

            // BUG: Its more efficient to write it in one call

            try {
                var remapfile = Path.Combine(Plugin.DataPath, "remap.list");

                var sb = new StringBuilder();

                foreach (var entry in s_songremap) {
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
                    if (parts.Length > 1) {
                        s_songremap.Add(parts[0], parts[1]);
                    }
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
            foreach (var song in RequestManager.RequestSongs.Reverse()) {
                if (song.Requestor.Id == requestor.Id) {
                    this.ChatManager.QueueChatMessage($"{song.SongMetaData["songName"].Value} ({song.ID}) removed.");

                    this.ListCollectionManager.Remove(s_duplicatelist, song.ID);
                    this.Skip(song, RequestStatus.Wrongsong);
                    return;
                }
            }
            this.ChatManager.QueueChatMessage($"You have no requests in the queue.");
        }
        #endregion

        // BUG: This requires a switch, or should be disabled for those who don't allow links
        public string ShowSongLink(ParseState state)
        {
            JSONObject json;
            switch (RequestBotConfig.Instance.LinkType) {
                case LinkType.OnlyRequest:
                    if (this.PlayNow == null) {
                        return s_success;
                    }
                    json = this.PlayNow.SongNode;
                    break;
                case LinkType.All:
                    if (SongInfomationProvider.CurrentSongLevel == null) {
                        return s_success;
                    }
                    json = SongInfomationProvider.CurrentSongLevel;
                    break;
                default:
                    return s_success;
            }
            this._textFactory.Create().AddSong(json).QueueMessage(StringFormat.LinkSonglink.ToString());
            return s_success;
        }

        public string Queueduration()
        {
            var total = 0;
            foreach (var songrequest in RequestManager.RequestSongs) {
                try {
                    total += songrequest.SongMetaData["duration"];
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
            }
            return $"{total / 60}:{ total % 60:00}";
        }

        public string QueueStatus(ParseState state)
        {
            var queuestate = RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open. " : "Queue is closed. ";
            this.ChatManager.QueueChatMessage($"{queuestate} There are {RequestManager.RequestSongs.Count} maps ({this.Queueduration()}) in the queue.");
            return s_success;
        }
        #endregion

        #region ListManager
        public void Showlists(IChatUser requestor, string request)
        {
            var msg = this._messageFactroy.Create();
            msg.Header("Loaded lists: ");
            foreach (var entry in this.ListCollectionManager.ListCollection) {
                msg.Add($"{entry.Key} ({entry.Value.Count()})", ", ");
            }

            msg.End("...", "No lists loaded.");
        }

        public string Listaccess(ParseState state)
        {
            this.ChatManager.QueueChatMessage($"Hi, my name is {state._botcmd.UserParameter} , and I'm a list object!");
            return s_success;
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
                foreach (var entry in list.list) {
                    msg.Add(entry, ", ");
                }

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
                foreach (var entry in list.list) {
                    this.ProcessSongRequest(this._stateFactory.Create().Setup(state, entry)); // Must use copies here, since these are all threads
                }
            }
            catch (Exception ex) { Logger.Error(ex); } // Going to try this form, to reduce code verbosity.              
            return s_success;
        }

        // Remove entire list from queue
        public string Unqueuelist(ParseState state)
        {
            state.Flags |= FlagParameter.Silent;
            foreach (var entry in this.ListCollectionManager.OpenList(state.Parameter).list) {
                state.Parameter = entry;
                this.DequeueSong(state);
            }
            return s_success;
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
            this.ListCollectionManager.OpenList(request.ToLower());
        }

        public List<JSONObject> ReadJSON(string path)
        {
            var objs = new List<JSONObject>();
            if (File.Exists(path)) {
                var json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull) {
                    foreach (JSONObject j in json.AsArray) {
                        objs.Add(j);
                    }
                }
            }
            return objs;
        }

        public void WriteJSON(string path, List<JSONObject> objs)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path))) {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            var arr = new JSONArray();
            foreach (var obj in objs) {
                arr.Add(obj);
            }

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
            if (errormsg == "") {
                state.Msg("SRManager files backed up.");
            }

            return errormsg;
        }
        public string Backup()
        {
            var now = DateTime.Now;
            var BackupName = Path.Combine(RequestBotConfig.Instance.BackupPath, $"SRMBACKUP-{now:yyyy-MM-dd-HHmm}.zip");
            try {
                if (!Directory.Exists(RequestBotConfig.Instance.BackupPath)) {
                    Directory.CreateDirectory(RequestBotConfig.Instance.BackupPath);
                }

                ZipFile.CreateFromDirectory(Plugin.DataPath, BackupName, System.IO.Compression.CompressionLevel.Fastest, true);
                RequestBotConfig.Instance.LastBackup = now.ToString();
            }
            catch (Exception ex) {
                Logger.Error(ex);
                return $"Failed to backup to {BackupName}";
            }
            return s_success;
        }
        #endregion

        #region SongDatabase
        public static readonly int partialhash = 3; // Do Not ever set this below 4. It will cause severe performance loss
        public List<JSONObject> GetSongListFromResults(JSONNode result, string searchString, SongFilter filter = SongFilter.All, string sortby = "-rating", int reverse = 1)
        {
            var list = new HashSet<SongMap>();
            if (result != null) {
                // Add query results to out song database.
                if (result["docs"].IsArray) {
                    var downloadedsongs = result["docs"].AsArray;
                    foreach (var currentSong in downloadedsongs.Children) {
                        var map = this._songMapFactory.Create(currentSong.AsObject, "", "");
                        this.MapDatabase.IndexSong(map);
                        list.Add(map);
                    }
                }
                else {
                    var map = this._songMapFactory.Create(result.AsObject, "", "");
                    this.MapDatabase.IndexSong(map);
                }
            }
            if (!string.IsNullOrEmpty(searchString)) {
                var hashSet = this.MapDatabase.Search(searchString);
                foreach (var map in hashSet) {
                    list.Add(map);
                }
            }
            var sortorder = sortby.Split(' ');
            var songs = list
                .Where(x => string.IsNullOrEmpty(this.SongSearchFilter(x.SongObject, true, filter)))
                .OrderBy(x => x, Comparer<SongMap>.Create((x, y) =>
                {
                    return reverse * this.CompareSong(x.SongObject, y.SongObject, ref sortorder);
                }))
                .Select(x => x.SongObject)
                .ToList();
            return songs;
        }
        public string GetGCCount(ParseState state)
        {
            state.Msg($"Gc0:{GC.CollectionCount(0)} GC1:{GC.CollectionCount(1)} GC2:{GC.CollectionCount(2)}");
            state.Msg($"{GC.GetTotalMemory(false)}");
            return s_success;
        }
        public string GenerateIvailedHash(string dir)
        {
            var combinedBytes = Array.Empty<byte>();
            foreach (var file in Directory.EnumerateFiles(dir)) {
                combinedBytes = combinedBytes.Concat(File.ReadAllBytes(file)).ToArray();
            }

            var hash = this.CreateSha1FromBytes(combinedBytes.ToArray());
            return hash;
        }

        private string CreateSha1FromBytes(byte[] input)
        {
            using (var sha1 = SHA1.Create()) {
                var inputBytes = input;
                var hashBytes = sha1.ComputeHash(inputBytes);

                return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
            }
        }
        public static bool pploading = false;
        private bool _disposedValue;
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
                        this.MapDatabase.PPMap.TryAdd(key, maxpp);
                        if (key != "" && maxpp > 100) {
                            this.ListCollectionManager.Add("pp.deck", key);
                        }

                        if (this.MapDatabase.MapLibrary.TryGetValue(key, out var map)) {
                            map.PP = maxpp;
                            map.SRMInfo.Add("pp", maxpp);
                            this.MapDatabase.IndexSong(map);
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