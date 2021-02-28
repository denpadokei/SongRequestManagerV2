using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using SongCore;
using SongRequestManagerV2.Bases;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRUIControls;
using Zenject;
using Image = UnityEngine.UI.Image;
using KEYBOARD = SongRequestManagerV2.Bots.KEYBOARD;

namespace SongRequestManagerV2.Views
{
    [HotReload]
    public class RequestBotListView : ViewContlollerBindableBase, IInitializable
    {
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // プロパティ
        // ui elements
        /// <summary>説明 を取得、設定</summary>
        private string playButtonName_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("play-button-text")]
        public string PlayButtonText
        {
            get => this.playButtonName_ ?? "PLAY";

            set => this.SetProperty(ref this.playButtonName_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string skipButtonName_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("skip-button-text")]
        public string SkipButtonName
        {
            get => this.skipButtonName_ ?? "SKIP";

            set => this.SetProperty(ref this.skipButtonName_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string historyButtonText_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("history-button-text")]
        public string HistoryButtonText
        {
            get => this.historyButtonText_ ?? "HISTORY";

            set => this.SetProperty(ref this.historyButtonText_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string historyHoverHint_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("history-hint")]
        public string HistoryHoverHint
        {
            get => this.historyHoverHint_ ?? "";

            set => this.SetProperty(ref this.historyHoverHint_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string queueButtonText_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("queue-button-text")]
        public string QueueButtonText
        {
            get => this.queueButtonText_ ?? "QUEUQ CLOSE";

            set => this.SetProperty(ref this.queueButtonText_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string blacklistButtonText_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("blacklist-button-text")]
        public string BlackListButtonText
        {
            get => this.blacklistButtonText_ ?? "BLACK LIST";

            set => this.SetProperty(ref this.blacklistButtonText_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        [UIValue("requests")]
        public List<object> Songs { get; } = new List<object>();
        /// <summary>説明 を取得、設定</summary>
        private string progressText_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("progress-text")]
        public string ProgressText
        {
            get => this.progressText_ ?? "Download Progress - 0 %";

            set => this.SetProperty(ref this.progressText_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private bool isHistoryButtonEnable_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("history-button-enable")]
        public bool IsHistoryButtonEnable
        {
            get => this.isHistoryButtonEnable_;

            set => this.SetProperty(ref this.isHistoryButtonEnable_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private bool isPlayButtonEnable_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("play-button-enable")]
        public bool IsPlayButtonEnable
        {
            get => this.isPlayButtonEnable_;

            set => this.SetProperty(ref this.isPlayButtonEnable_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private bool isSkipButtonEnable_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("skip-button-enable")]
        public bool IsSkipButtonEnable
        {
            get => this.isSkipButtonEnable_;

            set => this.SetProperty(ref this.isSkipButtonEnable_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private bool isBlacklistButtonEnable_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("blacklist-button-enable")]
        public bool IsBlacklistButtonEnable
        {
            get => this.isBlacklistButtonEnable_;

            set => this.SetProperty(ref this.isBlacklistButtonEnable_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private bool isPerformanceMode_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("performance-mode")]
        public bool PerformanceMode
        {
            get => this.isPerformanceMode_;

            set => this.SetProperty(ref this.isPerformanceMode_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private bool isShowHistory_ = false;
        /// <summary>説明 を取得、設定</summary>
        public bool IsShowHistory
        {
            get => this.isShowHistory_;

            set => this.SetProperty(ref this.isShowHistory_, value);
        }

        private int SelectedRow
        {
            get
            {
                if (this._bot.CurrentSong == null) {
                    return -1;
                }
                else {
                    return this.Songs.IndexOf(this._bot.CurrentSong);
                }
            }
        }

        /// <summary>説明 を取得、設定</summary>
        private bool isActiveButton_;
        [UIValue("is-active-button")]
        /// <summary>説明 を取得、設定</summary>
        public bool IsActiveButton
        {
            get => this.isActiveButton_;

            set => this.SetProperty(ref this.isActiveButton_, value);
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // コマンド
        public event Action<string> ChangeTitle;
        public event Action<SongRequest, bool> PlayProcessEvent;
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // Unity Message
        protected override void OnDestroy()
        {
            this._bot.UpdateUIRequest -= this.UpdateRequestUI;
            this._bot.SetButtonIntactivityRequest -= this.SetUIInteractivity;
            this._bot.PropertyChanged -= this.OnBotPropertyChanged;
            Loader.SongsLoadedEvent -= SongLoader_SongsLoadedEvent;
            base.OnDestroy();
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // オーバーライドメソッド
        protected override void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnPropertyChanged(args);
            if (args.PropertyName == nameof(this.IsShowHistory)) {
                this.UpdateRequestUI(true);
                this.SetUIInteractivity();
            }
            else if (args.PropertyName == nameof(this.PerformanceMode)) {
                RequestBotConfig.Instance.PerformanceMode = this.PerformanceMode;
                RequestBotConfig.Instance.Save();
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            this.UpdateRequestUI(true);
            this.SetUIInteractivity(true);
            this.IsActiveButton = true;
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (!confirmDialogActive) {
                this.IsShowHistory = false;
            }
            this.UpdateRequestUI();
            this.SetUIInteractivity(true);
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // パブリックメソッド
        public void ChangeProgressText(double progress)
        {
            HMMainThreadDispatcher.instance?.Enqueue(() =>
            {
                this.ProgressText = $"Download Progress - {progress * 100:0.00} %";
            });
        }
        public void UpdateRequestUI(bool selectRowCallback = false)
        {
            if (SceneManager.GetActiveScene().name == "GameCore") {
                return;
            }
            Dispatcher.RunOnMainThread(() =>
            {
                try {
                    this.QueueButtonText = RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed";
                    this._queueButton.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline").color = RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red; ;
                    this.HistoryHoverHint = IsShowHistory ? "Go back to your current song request queue." : "View the history of song requests from the current session.";
                    HistoryButtonText = IsShowHistory ? "Requests" : "History";
                    PlayButtonText = IsShowHistory ? "Replay" : "Play";
                    this.PerformanceMode = RequestBotConfig.Instance.PerformanceMode;
                    RefreshSongQueueList(selectRowCallback);
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
            });
            Dispatcher.RunOnMainThread(() => this._playButton.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline").color = this.SelectedRow >= 0 ? Color.green : Color.red);
        }
        /// <summary>
        /// Alter the state of the buttons based on selection
        /// </summary>
        /// <param name="interactive">Set to false to force disable all buttons, true to auto enable buttons based on states</param>
        public void SetUIInteractivity(bool interactive = true)
        {
            if (!this.isActivated) {
                return;
            }
            try {
                var toggled = interactive;

                if (_requestTable.NumberOfCells() == 0 || SelectedRow == -1 || SelectedRow >= Songs.Count()) {
                    Logger.Debug("Nothing selected, or empty list, buttons should be off");
                    toggled = false;
                }

                var playButtonEnabled = toggled;
                if (toggled && !IsShowHistory) {
                    var isChallenge = _bot.CurrentSong._requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                    playButtonEnabled = isChallenge ? false : toggled;
                }
                this.IsPlayButtonEnable = playButtonEnabled;

                var skipButtonEnabled = toggled;
                if (toggled && IsShowHistory) {
                    skipButtonEnabled = false;
                }
                this.IsSkipButtonEnable = skipButtonEnabled;

                this.IsBlacklistButtonEnable = toggled;

                // history button can be enabled even if others are disabled
                this.IsHistoryButtonEnable = true;
                this.IsHistoryButtonEnable = interactive;

                this.IsPlayButtonEnable = interactive;
                this.IsSkipButtonEnable = interactive;
                this.IsBlacklistButtonEnable = interactive;
                // history button can be enabled even if others are disabled
                this.IsHistoryButtonEnable = true;
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
        public void RefreshSongQueueList(bool selectRowCallback = false)
        {
            try {
                //UpdateSelectSongInfo();
                lock (_lockObject) {
                    this.Songs.Clear();
                    if (this.IsShowHistory) {
                        this.Songs.AddRange(RequestManager.HistorySongs);
                    }
                    else {
                        this.Songs.AddRange(RequestManager.RequestSongs);
                    }
                    Dispatcher.RunOnMainThread(() =>
                    {
                        this._requestTable?.tableView?.ReloadData();
                        if (!selectRowCallback || this._requestTable?.tableView?.numberOfCells > (uint)this.SelectedRow) {
                            try {
                                this._requestTable?.tableView?.SelectCellWithIdx(this.SelectedRow, selectRowCallback);
                                this._requestTable?.tableView?.ScrollToCellWithIdx(this.SelectedRow, TableViewScroller.ScrollPositionType.Center, true);
                            }
                            catch (Exception e) {
                                Logger.Error(e);
                            }
                        }
                    });
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

#if UNRELEASED
        public void InvokeBeatSaberButton(String buttonName)
        {
            Button buttonInstance = Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == buttonName));
            buttonInstance.onClick.Invoke();
        }

        public void ColorDeckButtons(KEYBOARD kb, Color basecolor, Color Present)
        {
            if (!RequestManager.HistorySongs.Any()) return;
            foreach (KEYBOARD.KEY key in kb.keys) {
                foreach (var item in RequestBot.deck) {
                    string search = $"!{item.Key}/selected/toggle";
                    if (key.value.StartsWith(search)) {
                        string deckname = item.Key.ToLower() + ".deck";
                        Color color = (_bot.ListCollectionManager.Contains(deckname, _bot.CurrentSong._song["id"].Value)) ? Present : basecolor;
                        key.mybutton.GetComponentInChildren<Image>().color = color;
                    }
                }
            }
        }

        public void UpdateSelectSongInfo()
        {

            if (RequestManager.HistorySongs.Count > 0)
            {
                var currentsong = CurrentlySelectedSong();

                _CurrentSongName.text = currentsong.song["songName"].Value;
                _CurrentSongName2.text = $"{currentsong.song["authorName"].Value} ({currentsong.song["version"].Value})";

                ColorDeckButtons(CenterKeys, Color.white, Color.magenta);
            }

        }
#endif
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // プライベートメソッド
        private void OnBotPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is IRequestBot bot) {
                if (e.PropertyName == nameof(bot.CurrentSong)) {
                    this._playButton.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline").color = this.SelectedRow >= 0 ? Color.green : Color.red;
                }
            }
        }
        [UIAction("#post-parse")]
        private void PostParse()
        {
            // Set default RequestFlowCoordinator title
            ChangeTitle?.Invoke(this.IsShowHistory ? "Song Request History" : "Song Request Queue");
        }
        [UIAction("history-click")]
        private void HistoryButtonClick()
        {
            IsShowHistory = !IsShowHistory;

            ChangeTitle?.Invoke(IsShowHistory ? "Song Request History" : "Song Request Queue");
        }
        [UIAction("skip-click")]
        private void SkipButtonClick()
        {
            if (_requestTable.NumberOfCells() > 0) {
                void _onConfirm()
                {
                    // skip it
                    _bot.Skip(_bot.CurrentSong);
                    // indicate dialog is no longer active
                    confirmDialogActive = false;
                }

                // get song
                var song = _bot.CurrentSong._song;

                // indicate dialog is active
                confirmDialogActive = true;

                // show dialog
                this.ShowDialog("Skip Song Warning", $"Skipping {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?", _onConfirm, () => { confirmDialogActive = false; });
            }
        }
        [UIAction("blacklist-click")]
        private void BlacklistButtonClick()
        {
            if (_requestTable.NumberOfCells() > 0) {
                void _onConfirm()
                {
                    _bot.Blacklist(_bot.CurrentSong, IsShowHistory, true);
                    confirmDialogActive = false;
                }

                // get song
                var song = _bot.CurrentSong._song;

                // indicate dialog is active
                confirmDialogActive = true;

                // show dialog
                this.ShowDialog("Blacklist Song Warning", $"Blacklisting {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?", _onConfirm, () => { confirmDialogActive = false; });
            }
        }
        [UIAction("play-click")]
        private void PlayButtonClick()
        {
            if (_requestTable.NumberOfCells() > 0) {
                RequestBot.Played.Add(_bot.CurrentSong._song);
                _bot.WriteJSON(RequestBot.playedfilename, RequestBot.Played);

                SetUIInteractivity(false);
                this.PlayProcessEvent?.Invoke(_bot.CurrentSong, this.IsShowHistory);
            }
        }

        [UIAction("queue-click")]
        private void QueueButtonClick()
        {
            RequestBotConfig.Instance.RequestQueueOpen = !RequestBotConfig.Instance.RequestQueueOpen;
            RequestBotConfig.Instance.Save();
            _bot.WriteQueueStatusToFile(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
            this._chatManager.QueueChatMessage(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
            UpdateRequestUI();
        }

        [UIAction("selected-cell")]
        private void SelectedCell(TableView tableView, object row)
        {
            _bot.CurrentSong = row as SongRequest;
            this._playButton.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline").color = this.SelectedRow >= 0 ? Color.green : Color.red;

            if (!IsShowHistory) {
                var isChallenge = _bot.CurrentSong._requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                this.IsPlayButtonEnable = !isChallenge;
            }
            //UpdateSelectSongInfo();
            SetUIInteractivity();
        }
        private void SongLoader_SongsLoadedEvent(Loader arg1, System.Collections.Concurrent.ConcurrentDictionary<string, CustomPreviewBeatmapLevel> arg2)
        {
            _requestTable?.tableView?.ReloadData();
        }

#if UNRELEASED
        private IPreviewBeatmapLevel CustomLevelForRow(int row)
        {
            // get level id from hash
            var levelIds = SongCore.Collections.levelIDsForHash(SongInfoForRow(row)._song["hash"]);
            if (levelIds.Count == 0) return null;

            // lookup song from level id
            return SongCore.Loader.CustomLevels.FirstOrDefault(s => string.Equals(s.Value.levelID, levelIds.First(), StringComparison.OrdinalIgnoreCase)).Value ?? null;
        }
        private void PlayPreview(IPreviewBeatmapLevel level)
        {
            _songPreviewPlayer.CrossfadeTo(level.previewAudioClip, level.previewStartTime, level.previewDuration);
        }
#endif
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // メンバ変数
        private static readonly object _lockObject = new object();
        private bool confirmDialogActive = false;
        private KEYBOARD CenterKeys;

        [UIComponent("request-list")]
        private CustomCellListTableData _requestTable;
        [UIComponent("queue-button")]
        private NoTransitionsButton _queueButton;
        [UIComponent("play-button")]
        private NoTransitionsButton _playButton;

        [Inject]
        protected PhysicsRaycasterWithCache _physicsRaycaster;
        [Inject]
        KEYBOARD.KEYBOARDFactiry _factiry;
        [Inject]
        IRequestBot _bot;
        [Inject]
        IChatManager _chatManager;
#if UNRELEASED
        private TextMeshProUGUI _CurrentSongName;
        private TextMeshProUGUI _CurrentSongName2;
        private SongPreviewPlayer _songPreviewPlayer;
        string SONGLISTKEY = @"
[blacklist last]/0'!block/current%CR%'

[fun +]/25'!fun/current/toggle%CR%' [hard +]/25'!hard/current/toggle%CR%'
[dance +]/25'!dance/current/toggle%CR%' [chill +]/25'!chill/current/toggle%CR%'
[brutal +]/25'!brutal/current/toggle%CR%' [sehria +]/25'!sehria/current/toggle%CR%'

[rock +]/25'!rock/current/toggle%CR%' [metal +]/25'!metal/current/toggle%CR%'  
[anime +]/25'!anime/current/toggle%CR%' [pop +]/25'!pop/current/toggle%CR%' 

[Random song!]/0'!decklist draw%CR%'";
#endif
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // 構築・破棄
        [Inject]
        void Constractor()
        {

            this._bot.UpdateUIRequest -= this.UpdateRequestUI;
            this._bot.UpdateUIRequest += this.UpdateRequestUI;
            this._bot.SetButtonIntactivityRequest -= this.SetUIInteractivity;
            this._bot.SetButtonIntactivityRequest += this.SetUIInteractivity;
            this._bot.PropertyChanged -= this.OnBotPropertyChanged;
            this._bot.PropertyChanged += this.OnBotPropertyChanged;
        }

        public void Initialize()
        {
            try {
                Loader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
#if UNRELEASED
                    _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();
#endif
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
            }
            try {
                try {
                    CenterKeys = _factiry.Create().Setup(this.rectTransform, "", false, -15, 15);
                    CenterKeys.AddKeyboard("CenterPanel.kbd");
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
#if UNRELEASED
                // BUG: Need additional modes disabling one shot buttons
                // BUG: Need to make sure the buttons are usable on older headsets

                _CurrentSongName = BeatSaberUI.CreateText(container, "", new Vector2(-35, 37f));
                _CurrentSongName.fontSize = 3f;
                _CurrentSongName.color = Color.cyan;
                _CurrentSongName.alignment = TextAlignmentOptions.Left;
                _CurrentSongName.enableWordWrapping = false;
                _CurrentSongName.text = "";

                _CurrentSongName2 = BeatSaberUI.CreateText(container, "", new Vector2(-35, 34f));
                _CurrentSongName2.fontSize = 3f;
                _CurrentSongName2.color = Color.cyan;
                _CurrentSongName2.alignment = TextAlignmentOptions.Left;
                _CurrentSongName2.enableWordWrapping = false;
                _CurrentSongName2.text = "";
                
                //CenterKeys.AddKeys(SONGLISTKEY);
                RequestBot.AddKeyboard(CenterKeys, "mainpanel.kbd");
                ColorDeckButtons(CenterKeys, Color.white, Color.magenta);
#endif
                CenterKeys.DefaultActions();
                try {
                    #region History button
                    // History button
                    this.HistoryButtonText = "HISTORY";
                    #endregion
                }
                catch (Exception e) {
                    Logger.Debug($"{e}");
                }
                try {
                    #region Blacklist button
                    // Blacklist button
                    this.BlackListButtonText = "Blacklist";
                    #endregion
                }
                catch (Exception e) {
                    Logger.Debug($"{e}");
                }
                try {
                    #region Skip button
                    this.SkipButtonName = "Skip";
                    #endregion
                }
                catch (Exception e) {
                    Logger.Debug($"{e}");
                }
                try {
                    #region Play button
                    // Play button
                    this.PlayButtonText = "Play";
                    #endregion
                }
                catch (Exception e) {
                    Logger.Debug($"{e}");
                }
                try {
                    #region Queue button
                    // Queue button
                    this.QueueButtonText = RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed";
                    #endregion
                }
                catch (Exception e) {
                    Logger.Debug($"{e}");
                }
                try {
                    #region Progress
                    this.ChangeProgressText(0f);
                    #endregion
                }
                catch (Exception e) {
                    Logger.Debug($"{e}");
                }
                this._requestTable.tableView.selectionType = TableViewSelectionType.Single;

            }
            catch (Exception e) {
                Logger.Debug($"{e}");
            }
        }

        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region Modal
        private Action OnConfirm;
        private Action OnDecline;

        [UIComponent("modal")]
        internal ModalView modal;
        /// <summary>説明 を取得、設定</summary>
        private string title_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("title")]
        public string Title
        {
            get => this.title_ ?? "";

            set => this.SetProperty(ref this.title_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string message_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("message")]
        public string Message
        {
            get => this.message_ ?? "";

            set => this.SetProperty(ref this.message_, value);
        }

        [UIAction("yes-click")]
        private void YesClick()
        {
            modal.Hide(true);
            OnConfirm?.Invoke();
            OnConfirm = null;
        }

        [UIAction("no-click")]
        private void NoClick()
        {
            modal.Hide(true);
            OnDecline?.Invoke();
            OnDecline = null;
        }

        [UIAction("show-dialog")]
        public void ShowDialog(string title, string message, Action onConfirm = null, Action onDecline = null)
        {
            this.Title = title;
            this.Message = message;

            OnConfirm = onConfirm;
            OnDecline = onDecline;

            modal.Show(true);
        }
        #endregion
    }
}