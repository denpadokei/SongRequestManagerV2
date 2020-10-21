using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using SongRequestManagerV2.UI;
using IPA.Utilities;
using BeatSaberMarkupLanguage;
//using Utilities = SongRequestManagerV2.Utils.Utility;
using System.Threading.Tasks;
using SongCore;
using SongRequestManagerV2.Extentions;
using BeatSaberMarkupLanguage.Components;
using Zenject;
using BeatSaberMarkupLanguage.ViewControllers;

namespace SongRequestManagerV2
{

    public class RequestBotListViewController : BSMLViewController, TableView.IDataSource
    {
        public static RequestBotListViewController Instance { get; private set; }
        public override string Content => @"<bg></bg>";

        public bool IsLoading { get; set; }

        private bool confirmDialogActive = false;

        // ui elements
        private Button _pageUpButton;
        private Button _pageDownButton;
        private Button _playButton;
        private Button _skipButton;
        private Button _blacklistButton;
        private Button _historyButton;
        private Button _queueButton;

        private TableView _songListTableView;
        [Inject]
        private DiContainer diContainer;
        [Inject]
        private LevelFilteringNavigationController _levelFilteringNavigationController;

        private LevelListTableCell _requestListTableCellInstance;

        private TextMeshProUGUI _CurrentSongName;
        private TextMeshProUGUI _CurrentSongName2;

        private HoverHint _historyHintText;

        private SongPreviewPlayer _songPreviewPlayer;

        internal Progress<double> _progress;
        private GameObject _progressTextObject;

        private FormattableText _progressText;

        private int _requestRow = 0;
        private int _historyRow = 0;
        private int _lastSelection = -1;

        private bool isShowingHistory = false;

        public event Action<string> ChangeTitle;

        private int _selectedRow
        {
            get { return isShowingHistory ? _historyRow : _requestRow; }
            set
            {
                if (isShowingHistory)
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
            this._progress.ProgressChanged -= this._progress_ProgressChanged;
            this._progress.ProgressChanged += this._progress_ProgressChanged;
        }

        private void _progress_ProgressChanged(object sender, double e)
        {
            this.ChangeProgressText(e);
        }

        public void ColorDeckButtons(KEYBOARD kb, Color basecolor, Color Present)
        {
            if (RequestHistory.Songs.Count == 0) return;
            foreach (KEYBOARD.KEY key in kb.keys) {
                foreach (var item in RequestBot.deck) {
                    string search = $"!{item.Key}/selected/toggle";
                    if (key.value.StartsWith(search)) {
                        string deckname = item.Key.ToLower() + ".deck";
                        Color color = (RequestBot.listcollection.contains(ref deckname, CurrentlySelectedSong.song["id"].Value)) ? Present : basecolor;
                        key.mybutton.GetComponentInChildren<Image>().color = color;
                    }
                }
            }
        }

        static public SongRequest currentsong = null;
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (firstActivation) {
                try {
                    if (!Loader.AreSongsLoaded) {
                        SongCore.Loader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
                    }

                    // get table cell instance
                    _requestListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First((LevelListTableCell x) => x.name == "LevelListTableCell");
                    _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();
                }
                catch (Exception e) {
                    Plugin.Logger.Error(e);
                }
                try {
                    RectTransform container = new GameObject("BSMLCustomListContainer", typeof(RectTransform)).transform as RectTransform;
                    #region BSMLList
                    try {
                        LayoutElement layoutElement = container.gameObject.AddComponent<LayoutElement>();
                        container.SetParent(rectTransform, false);
                        container.sizeDelta = new Vector2(60f, 0f);

                        GameObject gameObject = new GameObject();
                        gameObject.name = "BSMLCustomList";
                        gameObject.SetActive(false);

                        gameObject.AddComponent<ScrollRect>();
                        gameObject.AddComponent<Touchable>();

                        _songListTableView = gameObject.AddComponent<BSMLTableView>();
                        //CustomCellListTableData tableData = this.gameObject.AddComponent<CustomCellListTableData>();
                        //tableData.tableView = _songListTableView;

                        gameObject.AddComponent<RectMask2D>();
                        _songListTableView.transform.SetParent(container, false);

                        _songListTableView.SetField("_preallocatedCells", new TableView.CellsGroup[0]);
                        _songListTableView.SetField("_isInitialized", false);

                        RectTransform viewport = new GameObject("Viewport").AddComponent<RectTransform>();
                        viewport.SetParent(gameObject.GetComponent<RectTransform>(), false);
                        gameObject.GetComponent<ScrollRect>().viewport = viewport;

                        (viewport.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                        (viewport.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                        (viewport.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                        //(viewport.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                        //(_songListTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                        //(_songListTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                        //(_songListTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                        //(_songListTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                        (_songListTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                        (_songListTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                        (_songListTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                        (_songListTableView.transform as RectTransform).anchoredPosition = new Vector2(0f, -3f);

                        _songListTableView.LazyInit();

                        _songListTableView.SetDataSource(this, true);

                        gameObject.SetActive(true);

                        

                        _songListTableView.didSelectCellWithIdxEvent += DidSelectRow;

                        //rectTransform.anchorMin = new Vector2(0.5f, 0f);
                        //rectTransform.anchorMax = new Vector2(0.5f, 1f);
                        //rectTransform.sizeDelta = new Vector2(74f, 0f);
                        //rectTransform.pivot = new Vector2(0.4f, 0.5f);

                        var songListTableViewScroller = _songListTableView.GetField<TableViewScroller, TableView>("scroller");

                        _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageUpButton")), container, false);
                        (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 20f);
                        _pageUpButton.interactable = true;
                        _pageUpButton.onClick.AddListener(delegate ()
                        {
                            songListTableViewScroller.PageScrollUp();
                        });

                        _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), container, false);
                        (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -20f);
                        _pageDownButton.interactable = true;
                        _pageDownButton.onClick.AddListener(delegate ()
                        {
                            songListTableViewScroller.PageScrollDown();
                        });
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Logger.Error(e);
                    }
                    //container = new GameObject("RequestBotContainer", typeof(RectTransform)).transform as RectTransform;
                    //container.SetParent(rectTransform, false);
                    //container.sizeDelta = new Vector2(60f, 0f);
                    //try {
                    //    #region TableView Setup and Initialization
                    //    var go = new GameObject();
                    //    go.AddComponent<RectTransform>();
                    //    go.name = "SongRequestTableView";
                    //    go.SetActive(false);
                    //    _songListTableView = go.AddComponent<TableView>();
                    //    _songListTableView.gameObject.AddComponent<RectMask2D>();
                    //    _songListTableView.transform.SetParent(container, false);

                    //    _songListTableView.SetField("_preallocatedCells", new TableView.CellsGroup[0]);
                    //    _songListTableView.SetField("_isInitialized", false);

                    //    var viewport = new GameObject("Viewport").AddComponent<RectTransform>();
                    //    viewport.SetParent(go.GetComponent<RectTransform>(), false);
                    //    (viewport.transform as RectTransform).sizeDelta = new Vector2(0, 0);
                    //    (viewport.transform as RectTransform).anchorMin = new Vector2(0, 0);
                    //    (viewport.transform as RectTransform).anchorMax = new Vector2(1, 1);
                    //    go.GetComponent<ScrollRect>().viewport = viewport;

                    //    _songListTableView.InvokeMethod<object, TableView>("Init");

                    //    _songListTableView.SetDataSource(this, false);

                    //    go.SetActive(true);

                    //    (_songListTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                    //    (_songListTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                    //    (_songListTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                    //    (_songListTableView.transform as RectTransform).anchoredPosition = new Vector2(0f, -3f);

                    //    _songListTableView.didSelectCellWithIdxEvent += DidSelectRow;

                    //    rectTransform.anchorMin = new Vector2(0.5f, 0f);
                    //    rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    //    rectTransform.sizeDelta = new Vector2(74f, 0f);
                    //    rectTransform.pivot = new Vector2(0.4f, 0.5f);

                    //    var songListTableViewScroller = _songListTableView.GetField<TableViewScroller, TableView>("scroller");

                    //    _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageUpButton")), container, false);
                    //    (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 35f);
                    //    _pageUpButton.interactable = true;
                    //    _pageUpButton.onClick.AddListener(delegate ()
                    //    {
                    //        songListTableViewScroller.PageScrollUp();
                    //    });

                    //    _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), container, false);
                    //    (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -41f);
                    //    _pageDownButton.interactable = true;
                    //    _pageDownButton.onClick.AddListener(delegate ()
                    //    {
                    //        songListTableViewScroller.PageScrollDown();
                    //    });
                    //    #endregion
                    //}
                    //catch (Exception e) {
                    //    Plugin.Log($"{e}");
                    //}

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
                    CenterKeys = new KEYBOARD(container, "", false, -15, 15);
                    RequestBot.AddKeyboard(CenterKeys, "CenterPanel.kbd");

                    CenterKeys.DefaultActions();
                    try {
                        #region History button
                        // History button
                        _historyButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                        _historyButton.ToggleWordWrapping(false);
                        (_historyButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 30f);
                        _historyButton.SetButtonText("History");
                        _historyButton.onClick.RemoveAllListeners();
                        _historyButton.onClick.AddListener(delegate ()
                        {
                            isShowingHistory = !isShowingHistory;
                            ChangeTitle?.Invoke(isShowingHistory ? "Song Request History" : "Song Request Queue");
                            UpdateRequestUI(true);
                            SetUIInteractivity();
                            _lastSelection = -1;
                        });
                        _historyHintText = UIHelper.AddHintText(_historyButton.transform as RectTransform, "");
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Blacklist button
                        // Blacklist button
                        _blacklistButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                        _blacklistButton.ToggleWordWrapping(false);
                        (_blacklistButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 10f);
                        _blacklistButton.SetButtonText("Blacklist");
                        //_blacklistButton.GetComponentInChildren<Image>().color = Color.red;
                        _blacklistButton.onClick.RemoveAllListeners();
                        _blacklistButton.onClick.AddListener(delegate ()
                        {
                            if (NumberOfCells() > 0) {
                                void _onConfirm()
                                {
                                    RequestBot.Instance.Blacklist(_selectedRow, isShowingHistory, true);
                                    if (_selectedRow > 0)
                                        _selectedRow--;
                                    confirmDialogActive = false;
                                }

                                // get song
                                var song = SongInfoForRow(_selectedRow).song;

                                // indicate dialog is active
                                confirmDialogActive = true;

                                // show dialog
                                YesNoModal.instance.ShowDialog("Blacklist Song Warning", $"Blacklisting {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?", _onConfirm, () => { confirmDialogActive = false; });
                            }
                        });
                        UIHelper.AddHintText(_blacklistButton.transform as RectTransform, "Block the selected request from being queued in the future.");
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Skip button
                        // Skip button
                        _skipButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                        _skipButton.ToggleWordWrapping(false);
                        (_skipButton.transform as RectTransform).anchoredPosition = new Vector2(90f, 0f);
                        _skipButton.SetButtonText("Skip");
                        //_skipButton.GetComponentInChildren<Image>().color = Color.yellow;
                        _skipButton.onClick.RemoveAllListeners();
                        _skipButton.onClick.AddListener(delegate ()
                        {
                            if (NumberOfCells() > 0) {
                                void _onConfirm()
                                {
                                    // get selected song
                                    currentsong = SongInfoForRow(_selectedRow);

                                    // skip it
                                    RequestBot.Instance.Skip(_selectedRow);

                                    // select previous song if not first song
                                    if (_selectedRow > 0) {
                                        _selectedRow--;
                                    }

                                    // indicate dialog is no longer active
                                    confirmDialogActive = false;
                                }

                                // get song
                                var song = SongInfoForRow(_selectedRow).song;

                                // indicate dialog is active
                                confirmDialogActive = true;

                                // show dialog
                                YesNoModal.instance.ShowDialog("Skip Song Warning", $"Skipping {song["songName"].Value} by {song["authorName"].Value}\r\nDo you want to continue?", _onConfirm, () => { confirmDialogActive = false; });
                            }
                        });
                        UIHelper.AddHintText(_skipButton.transform as RectTransform, "Remove the selected request from the queue.");
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Play button
                        // Play button
                        _playButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                        _playButton.ToggleWordWrapping(false);
                        (_playButton.transform as RectTransform).anchoredPosition = new Vector2(90f, -10f);
                        _playButton.SetButtonText("Play");
                        _playButton.GetComponentInChildren<Image>().color = Color.green;
                        _playButton.onClick.RemoveAllListeners();
                        _playButton.onClick.AddListener(delegate ()
                        {
                            if (NumberOfCells() > 0) {
                                currentsong = SongInfoForRow(_selectedRow);
                                RequestBot.played.Add(currentsong.song);
                                RequestBot.WriteJSON(RequestBot.playedfilename, ref RequestBot.played);

                                SetUIInteractivity(false);
                                RequestBot.Instance.Process(_selectedRow, isShowingHistory);
                                _selectedRow = -1;
                            }
                        });
                        UIHelper.AddHintText(_playButton.transform as RectTransform, "Download and scroll to the currently selected request.");
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Queue button
                        // Queue button
                        _queueButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(o => (o.name == "OkButton")), container, false);
                        _queueButton.ToggleWordWrapping(false);
                        _queueButton.SetButtonTextSize(3.5f);
                        (_queueButton.transform as RectTransform).anchoredPosition = new Vector2(90f, -30f);
                        _queueButton.SetButtonText(RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed");
                        _queueButton.GetComponentInChildren<Image>().color = RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red; ;
                        _queueButton.interactable = true;
                        _queueButton.onClick.RemoveAllListeners();
                        _queueButton.onClick.AddListener(delegate ()
                        {
                            RequestBotConfig.Instance.RequestQueueOpen = !RequestBotConfig.Instance.RequestQueueOpen;
                            RequestBotConfig.Instance.Save();
                            RequestBot.WriteQueueStatusToFile(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
                            RequestBot.Instance.QueueChatMessage(RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open." : "Queue is closed.");
                            UpdateRequestUI();
                        });
                        UIHelper.AddHintText(_queueButton.transform as RectTransform, "Open/Close the queue.");
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    try {
                        #region Progress
                        this.ChangeProgressText(0f);
                        this._progressTextObject.SetActive(true);
                        this._progressText.transform.SetParent(this.transform as RectTransform, false);
                        (this._progressText.transform as RectTransform).anchoredPosition = new Vector2(23f, -50f);
                        #endregion
                    }
                    catch (Exception e) {
                        Plugin.Log($"{e}");
                    }
                    // Set default RequestFlowCoordinator title
                    ChangeTitle?.Invoke(isShowingHistory ? "Song Request History" : "Song Request Queue");
                }
                catch (Exception e) {
                    Plugin.Logger.Error(e);
                }
            }
            UpdateRequestUI();
            SetUIInteractivity(true);
        }

        internal void ChangeProgressText(double progress)
        {
            if (this._progressTextObject == null) {
                this._progressTextObject = new GameObject("ProgressText");
                this._progressText = this._progressTextObject.AddComponent<FormattableText>();

                this._progressText.font = MonoBehaviour.Instantiate(Resources.FindObjectsOfTypeAll<TMP_FontAsset>().First(t => t.name == "Teko-Medium SDF No Glow"));
                this._progressText.fontSize = 4;
                this._progressText.color = Color.white;

                this._progressText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                this._progressText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }
            
            this._progressText.SetText($"Download Progress - {progress * 100:0.00} %");
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            if (!confirmDialogActive) {
                isShowingHistory = false;
            }
        }

        public SongRequest CurrentlySelectedSong
        {
            get
            {
                var currentsong = RequestHistory.Songs[0];

                if (_selectedRow != -1 && NumberOfCells() > _selectedRow) {
                    currentsong = SongInfoForRow(_selectedRow);
                }
                return currentsong;
            }
        }

        public void UpdateSelectSongInfo()
        {
#if UNRELEASED
            if (RequestHistory.Songs.Count > 0)
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
                    _playButton.GetComponentInChildren<Image>().color = ((isShowingHistory && RequestHistory.Songs.Count > 0) || (!isShowingHistory && RequestQueue.Songs.Count > 0)) ? Color.green : Color.red;
                    _queueButton.SetButtonText(RequestBotConfig.Instance.RequestQueueOpen ? "Queue Open" : "Queue Closed");
                    _queueButton.GetComponentInChildren<Image>().color = RequestBotConfig.Instance.RequestQueueOpen ? Color.green : Color.red; ;
                    _historyHintText.text = isShowingHistory ? "Go back to your current song request queue." : "View the history of song requests from the current session.";
                    _historyButton.SetButtonText(isShowingHistory ? "Requests" : "History");
                    _playButton.SetButtonText(isShowingHistory ? "Replay" : "Play");

                    RefreshSongQueueList(selectRowCallback);
                }
                catch (Exception e) {
                    Plugin.Logger.Error(e);
                }
            });
        }

        private void DidSelectRow(TableView table, int row)
        {
            _selectedRow = row;
            if (row != _lastSelection) {
                _lastSelection = row;
            }

            // if not in history, disable play button if request is a challenge
            if (!isShowingHistory) {
                var request = SongInfoForRow(row);
                var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                _playButton.interactable = !isChallenge;
            }

            UpdateSelectSongInfo();

            SetUIInteractivity();
        }

        private void SongLoader_SongsLoadedEvent(SongCore.Loader arg1, Dictionary<string, CustomPreviewBeatmapLevel> arg2)
        {
            _songListTableView?.ReloadData();
        }

        private List<SongRequest> Songs => isShowingHistory ? RequestHistory.Songs : RequestQueue.Songs;

        

        /// <summary>
        /// Alter the state of the buttons based on selection
        /// </summary>
        /// <param name="interactive">Set to false to force disable all buttons, true to auto enable buttons based on states</param>
        public void SetUIInteractivity(bool interactive = true)
        {
            try {
                var toggled = interactive;

                if (_selectedRow >= (isShowingHistory ? RequestHistory.Songs : RequestQueue.Songs).Count()) _selectedRow = -1;

                if (NumberOfCells() == 0 || _selectedRow == -1 || _selectedRow >= Songs.Count()) {
                    Plugin.Log("Nothing selected, or empty list, buttons should be off");
                    toggled = false;
                }

                var playButtonEnabled = toggled;
                if (toggled && !isShowingHistory) {
                    var request = SongInfoForRow(_selectedRow);
                    var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;
                    playButtonEnabled = isChallenge ? false : toggled;
                }

                _playButton.interactable = playButtonEnabled;

                var skipButtonEnabled = toggled;
                if (toggled && isShowingHistory) {
                    skipButtonEnabled = false;
                }
                _skipButton.interactable = skipButtonEnabled;

                _blacklistButton.interactable = toggled;

                // history button can be enabled even if others are disabled
                _historyButton.interactable = true;
                _historyButton.interactable = interactive;

                _playButton.interactable = interactive;
                _skipButton.interactable = interactive;
                _blacklistButton.interactable = interactive;
                // history button can be enabled even if others are disabled
                _historyButton.interactable = true;
            }
            catch (Exception e) {
                Plugin.Logger.Error(e);
            }
        }

        public void RefreshSongQueueList(bool selectRowCallback = false)
        {
            try {
                UpdateSelectSongInfo();

                this._songListTableView.ReloadData();

                if (_selectedRow == -1) return;

                if (NumberOfCells() > this._selectedRow) {
                    this._songListTableView.SelectCellWithIdx(_selectedRow, selectRowCallback);
                    this._songListTableView.ScrollToCellWithIdx(_selectedRow, TableViewScroller.ScrollPositionType.Beginning, true);
                }
            }
            catch (Exception e) {
                Plugin.Logger.Error(e);
            }
        }

        private CustomPreviewBeatmapLevel CustomLevelForRow(int row)
        {
            // get level id from hash
            var levelIds = SongCore.Collections.levelIDsForHash(SongInfoForRow(row).song["hash"]);
            if (levelIds.Count == 0) return null;

            // lookup song from level id
            return SongCore.Loader.CustomLevels.FirstOrDefault(s => string.Equals(s.Value.levelID, levelIds.First(), StringComparison.OrdinalIgnoreCase)).Value ?? null;
        }

        private SongRequest SongInfoForRow(int row)
        {
            return isShowingHistory ? RequestHistory.Songs.ElementAt(row) : RequestQueue.Songs.ElementAt(row);
        }

        private void PlayPreview(CustomPreviewBeatmapLevel level)
        {
            //_songPreviewPlayer.CrossfadeTo(level.previewAudioClip, level.previewStartTime, level.previewDuration);
        }

        private static Dictionary<string, Texture2D> _cachedTextures = new Dictionary<string, Texture2D>();

        #region TableView.IDataSource interface
        public float CellSize() { return 10f; }

        public int NumberOfCells()
        {
            return isShowingHistory ? RequestHistory.Songs.Count() : RequestQueue.Songs.Count();
        }

        public TableCell CellForIdx(TableView tableView, int row)
        {
            LevelListTableCell _tableCell = Instantiate(_requestListTableCellInstance);
            _tableCell.reuseIdentifier = "RequestBotFriendCell";
            _tableCell.SetField("_notOwned", true);

            SongRequest request = SongInfoForRow(row);
            SetDataFromLevelAsync(request, _tableCell, row).Await(null, e => { Plugin.Log($"{e}"); }, null);
            return _tableCell;
        }
        #endregion

        private async Task SetDataFromLevelAsync(SongRequest request, LevelListTableCell _tableCell, int row)
        {
            var favouritesBadge = _tableCell.GetField<Image, LevelListTableCell>("_favoritesBadgeImage");
            favouritesBadge.enabled = false;

            bool highlight = (request.requestInfo.Length > 0) && (request.requestInfo[0] == '!');

            string msg = highlight ? "MSG" : "";

            var hasMessage = (request.requestInfo.Length > 0) && (request.requestInfo[0] == '!');
            var isChallenge = request.requestInfo.IndexOf("!challenge", StringComparison.OrdinalIgnoreCase) >= 0;

            //var beatmapCharacteristicImages = _tableCell.GetField<UnityEngine.UI.Image[], LevelListTableCell>("_beatmapCharacteristicImages"); // NEW VERSION
            //foreach (var i in beatmapCharacteristicImages) i.enabled = false;

            // causing a nullex?
            //_tableCell.SetField("_beatmapCharacteristicAlphas", new float[5] { 1f, 1f, 1f, 1f, 1f });

            // set message icon if request has a message // NEW VERSION
            //if (hasMessage) {
            //    beatmapCharacteristicImages.Last().sprite = Base64Sprites.InfoIcon;
            //    beatmapCharacteristicImages.Last().enabled = true;
            //}

            // set challenge icon if song is a challenge
            //if (isChallenge) {
            //    var el = beatmapCharacteristicImages.ElementAt(beatmapCharacteristicImages.Length - 2);

            //    el.sprite = Base64Sprites.VersusChallengeIcon;
            //    el.enabled = true;
            //}

            string pp = "";
            int ppvalue = request.song["pp"].AsInt;
            if (ppvalue > 0) pp = $" {ppvalue} PP";

            var dt = new RequestBot.DynamicText().AddSong(request.song).AddUser(ref request.requestor); // Get basic fields
            dt.Add("Status", request.status.ToString());
            dt.Add("Info", (request.requestInfo != "") ? " / " + request.requestInfo : "");
            dt.Add("RequestTime", request.requestTime.ToLocalTime().ToString("hh:mm"));

            var songName = _tableCell.GetField<TextMeshProUGUI, LevelListTableCell>("_songNameText");
            //songName.text = $"{request.song["songName"].Value} <size=50%>{RequestBot.GetRating(ref request.song)} <color=#3fff3f>{pp}</color></size> <color=#ff00ff>{msg}</color>";
            songName.text = $"{request.song["songName"].Value} <size=50%>{RequestBot.GetRating(ref request.song)} <color=#3fff3f>{pp}</color></size>"; // NEW VERSION

            var author = _tableCell.GetField<TextMeshProUGUI, LevelListTableCell>("_songAuthorText");

            author.text = dt.Parse(RequestBot.QueueListRow2);

            var image = _tableCell.GetField<Image, LevelListTableCell>("_coverImage");
            var imageSet = false;

            if (SongCore.Loader.AreSongsLoaded) {
                CustomPreviewBeatmapLevel level = CustomLevelForRow(row);
                if (level != null) {
                    //Plugin.Log("custom level found");
                    // set image from song's cover image
                    var tex = await level.GetCoverImageAsync(System.Threading.CancellationToken.None);
                    image.sprite = tex;
                    imageSet = true;
                }
            }

            if (!imageSet) {
                string url = request.song["coverURL"].Value;

                if (!_cachedTextures.TryGetValue(url, out var tex)) {
                    var b = await WebClient.DownloadImage($"https://beatsaver.com{url}", System.Threading.CancellationToken.None);

                    tex = new Texture2D(2, 2);
                    tex.LoadImage(b);
                    
                    try {
                        _cachedTextures.Add(url, tex);
                    }
                    catch (Exception) {

                    }
                }

                image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            UIHelper.AddHintText(_tableCell.transform as RectTransform, dt.Parse(RequestBot.SongHintText));
        }
    }
}