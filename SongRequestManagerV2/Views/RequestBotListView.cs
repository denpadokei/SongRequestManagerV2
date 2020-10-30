using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using SongCore;
using SongRequestManagerV2.Bases;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;
using Zenject;
using Image = UnityEngine.UI.Image;

namespace SongRequestManagerV2.Views
{
    [HotReload]
    public class RequestBotListView : ViewContlollerBindableBase
    {
        public static RequestBotListView Instance { get; private set; }
        public bool IsLoading { get; set; }

        private bool confirmDialogActive = false;
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

        //public ObservableCollection<object> Songs { get; } = new ObservableCollection<object>();

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

        [UIComponent("request-list")]
        private CustomCellListTableData _requestTable;

        [UIComponent("queue-button")]
        private NoTransitionsButton _queueButton;
        [Inject]
        protected PhysicsRaycasterWithCache _physicsRaycaster;
        [Inject]
        KEYBOARD.KEYBOARDFactiry _factiry;
        [Inject]
        IRequestBot _bot;

        private TextMeshProUGUI _CurrentSongName;
        private TextMeshProUGUI _CurrentSongName2;
        private SongPreviewPlayer _songPreviewPlayer;

        internal Progress<double> _progress;

        public event Action<string> ChangeTitle;

        private int _requestRow = -1;
        private int _historyRow = -1;
        private int _lastSelection = -1;

        /// <summary>説明 を取得、設定</summary>
        private bool isShowHistory_;
        /// <summary>説明 を取得、設定</summary>
        public bool IsShowHistory
        {
            get => this.isShowHistory_;

            set => this.SetProperty(ref this.isShowHistory_, value);
        }

        private int SelectedRow
        {
            get { return IsShowHistory ? _historyRow : _requestRow; }
            set
            {
                if (IsShowHistory)
                    _historyRow = value;
                else
                    _requestRow = value;
            }
        }

        private KEYBOARD CenterKeys;

        string SONGLISTKEY = @"
[blacklist last]/0'!block/current%CR%'

[fun +]/25'!fun/current/toggle%CR%' [hard +]/25'!hard/current/toggle%CR%'
[dance +]/25'!dance/current/toggle%CR%' [chill +]/25'!chill/current/toggle%CR%'
[brutal +]/25'!brutal/current/toggle%CR%' [sehria +]/25'!sehria/current/toggle%CR%'

[rock +]/25'!rock/current/toggle%CR%' [metal +]/25'!metal/current/toggle%CR%'  
[anime +]/25'!anime/current/toggle%CR%' [pop +]/25'!pop/current/toggle%CR%' 

[Random song!]/0'!decklist draw%CR%'";

        public static void InvokeBeatSaberButton(String buttonName)
        {
            Button buttonInstance = Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == buttonName));
            buttonInstance.onClick.Invoke();
        }

        public void Awake()
        {
            Plugin.Logger.Debug("ListView Awake()");
            Instance = this;
            this._progress = new Progress<double>();
            this._progress.ProgressChanged -= this.Progress_ProgressChanged;
            this._progress.ProgressChanged += this.Progress_ProgressChanged;
        }

        private void Progress_ProgressChanged(object sender, double e)
        {
            this.ChangeProgressText(e);
        }

        public void ColorDeckButtons(KEYBOARD kb, Color basecolor, Color Present)
        {
            if (!RequestManager.HistorySongs.Any()) return;
            foreach (KEYBOARD.KEY key in kb.keys) {
                foreach (var item in RequestBot.deck) {
                    string search = $"!{item.Key}/selected/toggle";
                    if (key.value.StartsWith(search)) {
                        string deckname = item.Key.ToLower() + ".deck";
                        Color color = (_bot.ListCollectionManager.Contains(deckname, CurrentlySelectedSong._song["id"].Value)) ? Present : basecolor;
                        key.mybutton.GetComponentInChildren<Image>().color = color;
                    }
                }
            }
        }
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (firstActivation) {
                try {
                    if (!Loader.AreSongsLoaded) {
                        Loader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
                    }

                    // get table cell instance
                    _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();
                }
                catch (Exception e) {
                    Plugin.Log($"{e}");
                }
                try {
                    try {
                        CenterKeys = _factiry.Create().Setup(this.rectTransform, "", false, -15, 15);
                        CenterKeys.AddKeyboard("CenterPanel.kbd");
                    }
                    catch (Exception e) {
                        Plugin.Logger.Error(e);
                    }
                    Plugin.Logger.Debug($"Songs is null? : {this.Songs == null}");
                    //this.Songs.CollectionChanged += this.Songs_CollectionChanged;
                    this.Songs.Clear();
                    foreach (var item in RequestManager.RequestSongs) {
                        Plugin.Logger.Debug($"{item}");
                        this.Songs.Add(item);
                    }
                    Plugin.Logger.Debug($"_requestTable is null : {this._requestTable == null}");
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
                        HistoryButtonText = "HISTORY";
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Blacklist button
                        // Blacklist button
                        this.BlackListButtonText = "Blacklist";
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Skip button
                        this.SkipButtonName = "Skip";
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Play button
                        // Play button
                        this.PlayButtonText = "Play";
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Queue button
                        // Queue button
                        this.QueueButtonText = RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed";
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Progress
                        this.ChangeProgressText(0f);
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    // Set default RequestFlowCoordinator title
                    ChangeTitle?.Invoke(IsShowHistory ? "Song Request History" : "Song Request Queue");
                }
                catch (Exception e) {
                    Plugin.Log($"{e}");
                }
            }

            this._requestTable.tableView.selectionType = TableViewSelectionType.Single;
            UpdateRequestUI();
            SetUIInteractivity(true);
        }
        protected override void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnPropertyChanged(args);
            if (args.PropertyName == nameof(this.IsShowHistory)) {
                this.Songs.Clear();
                if (this.IsShowHistory) {
                    foreach (var item in RequestManager.HistorySongs) {
                        this.Songs.Add(item);
                    }
                }
                else {
                    foreach (var item in RequestManager.RequestSongs) {
                        this.Songs.Add(item);
                    }
                }
                UpdateRequestUI();
                SetUIInteractivity();
                _lastSelection = -1;
            }
        }

        [UIAction("history-click")]
        private void HistoryButtonClick()
        {
            IsShowHistory = !IsShowHistory;

            ChangeTitle?.Invoke(IsShowHistory ? "Song Request History" : "Song Request Queue");
            this.Songs.Clear();
            if (this.IsShowHistory) {
                foreach (var item in RequestManager.HistorySongs) {
                    this.Songs.Add(item);
                }
            }
            else {
                foreach (var item in RequestManager.RequestSongs) {
                    this.Songs.Add(item);
                }
            }
            UpdateRequestUI(true);
            SetUIInteractivity();
            this._requestTable.tableView.SelectCellWithIdx(-1);
        }
        [UIAction("skip-click")]
        private void SkipButtonClick()
        {
            if (_requestTable.NumberOfCells() > 0) {
                void _onConfirm()
                {
                    // get selected song
                    _bot.Currentsong = SongInfoForRow(SelectedRow);

                    // skip it
                    _bot.Skip(SelectedRow);

                    // select previous song if not first song
                    if (SelectedRow > 0) {
                        SelectedRow--;
                    }

                    // indicate dialog is no longer active
                    confirmDialogActive = false;
                }

                // get song
                var song = SongInfoForRow(SelectedRow)._song;

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
                    _bot.Blacklist(SelectedRow, IsShowHistory, true);
                    if (SelectedRow > 0)
                        SelectedRow--;
                    confirmDialogActive = false;
                }

                // get song
                var song = SongInfoForRow(SelectedRow)._song;

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
                _bot.Currentsong = SongInfoForRow(SelectedRow);
                RequestBot.Played.Add(_bot.Currentsong._song);
                _bot.WriteJSON(RequestBot.playedfilename, RequestBot.Played);

                SetUIInteractivity(false);
                _bot.Process(SelectedRow, IsShowHistory);
                this._requestTable.tableView.SelectCellWithIdx(-1);
            }
        }

        [UIAction("queue-click")]
        private void QueueButtonClick()
        {
            RequestBotConfig.Instance.RequestQueueOpen = !RequestBotConfig.Instance.RequestQueueOpen;
            RequestBotConfig.Instance.Save();
            _bot.WriteQueueStatusToFile(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
            _bot.QueueChatMessage(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
            UpdateRequestUI();
        }

        [UIAction("selected-cell")]
        private void SelectedCell(TableView tableView, object row)
        {
            Plugin.Logger.Debug($"Selected cell : {tableView}({row})");

            SelectedRow = this.Songs.IndexOf(row);
            //if (row != _lastSelection) {
            //    _lastSelection = 0;
            //}

            // if not in history, disable play button if request is a challenge
            if (!IsShowHistory) {
                var request = SongInfoForRow(SelectedRow);
                var isChallenge = request._requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                this.IsPlayButtonEnable = !isChallenge;
            }

            UpdateSelectSongInfo();

            SetUIInteractivity();
        }


        internal void ChangeProgressText(double progress)
        {
            HMMainThreadDispatcher.instance?.Enqueue(() =>
            {
                this.ProgressText = $"Download Progress - {progress * 100:0.00} %";
            });
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            if (!confirmDialogActive) {
                IsShowHistory = false;
            }
        }

        public SongRequest CurrentlySelectedSong
        {
            get
            {
                var currentsong = RequestManager.HistorySongs[0];

                if (SelectedRow != -1 && _requestTable.NumberOfCells() > SelectedRow) {
                    currentsong = SongInfoForRow(SelectedRow);
                }
                return currentsong as SongRequest;
            }
        }

        public void UpdateSelectSongInfo()
        {
#if UNRELEASED
            if (RequestManager.HistorySongs.Count > 0)
            {
                var currentsong = CurrentlySelectedSong();

                _CurrentSongName.text = currentsong.song["songName"].Value;
                _CurrentSongName2.text = $"{currentsong.song["authorName"].Value} ({currentsong.song["version"].Value})";

                ColorDeckButtons(CenterKeys, Color.white, Color.magenta);
            }
#endif
        }

        public void UpdateRequestUI(bool selectRowCallback = false)
        {
            Dispatcher.RunOnMainThread(() =>
            {
                try {
                    //_playButton.GetComponentInChildren<Image>().color = ((IsShowHistory && RequestManager.HistorySongs.Count > 0) || (!IsShowHistory && RequestManager.RequestSongs.Count > 0)) ? Color.green : Color.red;
                    this.QueueButtonText = RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed";
                    //_queueButton.GetComponentInChildren<Image>().color = RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red; ;
                    this.HistoryHoverHint = IsShowHistory ? "Go back to your current song request queue." : "View the history of song requests from the current session.";
                    HistoryButtonText = IsShowHistory ? "Requests" : "History";
                    PlayButtonText = IsShowHistory ? "Replay" : "Play";
                    RefreshSongQueueList(selectRowCallback);
                }
                catch (Exception e) {
                    Plugin.Logger.Error(e);
                }
            });
        }

        private void SongLoader_SongsLoadedEvent(Loader arg1, System.Collections.Concurrent.ConcurrentDictionary<string, CustomPreviewBeatmapLevel> arg2)
        {
            _requestTable?.tableView?.ReloadData();
        }

        /// <summary>
        /// Alter the state of the buttons based on selection
        /// </summary>
        /// <param name="interactive">Set to false to force disable all buttons, true to auto enable buttons based on states</param>
        public void SetUIInteractivity(bool interactive = true)
        {
            try {
                var toggled = interactive;

                if (SelectedRow >= (IsShowHistory ? RequestManager.HistorySongs : RequestManager.RequestSongs).Count()) SelectedRow = -1;

                if (_requestTable.NumberOfCells() == 0 || SelectedRow == -1 || SelectedRow >= Songs.Count()) {
                    Plugin.Log("Nothing selected, or empty list, buttons should be off");
                    toggled = false;
                }

                var playButtonEnabled = toggled;
                if (toggled && !IsShowHistory) {
                    var request = SongInfoForRow(SelectedRow);
                    var isChallenge = request._requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
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
                Plugin.Logger.Error(e);
            }
        }

        public void RefreshSongQueueList(bool selectRowCallback = false)
        {
            try {
                UpdateSelectSongInfo();
                this.Songs.Clear();
                if (this.IsShowHistory) {
                    foreach (var item in RequestManager.HistorySongs) {
                        this.Songs.Add(item);
                    }
                }
                else {
                    foreach (var item in RequestManager.RequestSongs) {
                        this.Songs.Add(item);
                    }
                }
                Dispatcher.RunOnMainThread(this._requestTable.tableView.ReloadData);
                if (SelectedRow == -1) return;

                if (_requestTable.NumberOfCells() > this.SelectedRow) {
                    Dispatcher.RunOnMainThread(() =>
                    {
                        this._requestTable?.tableView?.SelectCellWithIdx(SelectedRow, selectRowCallback);
                        this._requestTable?.tableView?.ScrollToCellWithIdx(SelectedRow, TableViewScroller.ScrollPositionType.Beginning, true);
                    });
                }
            }
            catch (Exception e) {
                Plugin.Logger.Error(e);
            }
        }

        //private CustomPreviewBeatmapLevel CustomLevelForRow(int row)
        //{
        //    // get level id from hash
        //    var levelIds = SongCore.Collections.levelIDsForHash(SongInfoForRow(row)._song["hash"]);
        //    if (levelIds.Count == 0) return null;

        //    // lookup song from level id
        //    return SongCore.Loader.CustomLevels.FirstOrDefault(s => string.Equals(s.Value.levelID, levelIds.First(), StringComparison.OrdinalIgnoreCase)).Value ?? null;
        //}

        private SongRequest SongInfoForRow(int row)
        {
            if (this.Songs.Count < (uint)row) {
                Plugin.Logger.Debug($"row : {row}, Songs Count : {this.Songs.Count}");
                return this.Songs.FirstOrDefault() as SongRequest;
            }
            return this.Songs[row] as SongRequest;
        }

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

        private void PlayPreview(CustomPreviewBeatmapLevel level)
        {
            //_songPreviewPlayer.CrossfadeTo(level.previewAudioClip, level.previewStartTime, level.previewDuration);
        }

        #region TableView.IDataSource interface
        //public float CellSize() { return 10f; }

        //public int NumberOfCells()
        //{
        //    return IsShowHistory ? RequestManager.HistorySongs.Count() : RequestManager.RequestSongs.Count();
        //}

        //public TableCell CellForIdx(TableView tableView, int row)
        //{
        //    LevelListTableCell _tableCell = Instantiate(_requestListTableCellInstance);
        //    _tableCell.reuseIdentifier = "RequestBotFriendCell";
        //    _tableCell.SetField("_notOwned", true);

        //    SongRequest request = SongInfoForRow(row);
        //    SetDataFromLevelAsync(request, _tableCell, row).Await(null, e => { Plugin.Log($"{e}"); }, null);
        //    return _tableCell;
        //}
        #endregion
    }
}