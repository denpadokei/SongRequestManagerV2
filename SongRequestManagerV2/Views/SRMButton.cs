using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using BS_Utils.Utilities;
using HMUI;
using IPA.Utilities;
using SongCore;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.UI;
using SongRequestManagerV2.Utils;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRUIControls;
using Zenject;

namespace SongRequestManagerV2.Views
{
    [HotReload]
    public class SRMButton : BSMLAutomaticViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        [Inject]
        MainFlowCoordinator _mainFlowCoordinator;
        [Inject]
        LevelCollectionNavigationController levelCollectionNavigationController;
        [Inject]
        RequestFlowCoordinator _requestFlow;
        [Inject]
        IRequestBot _bot;
        [Inject]
        IChatManager ChatManager { get; }
        [Inject]
        DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        StringNormalization Normalize;
        [Inject]
        SongListUtils SongListUtils;

        private NoTransitionsButton _button;

        public Progress<double> DownloadProgress { get; } = new Progress<double>();

        public HMUI.Screen Screen { get; set; }

        public Canvas ButtonCanvas { get; set; }

        public FlowCoordinator Current => _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

        [UIAction("action")]
        public void Action()
        {
            try {
                Logger.Debug("action");
                _button.interactable = false;
                SRMButtonPressed();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                _button.interactable = true;
            }

        }

        internal void SRMButtonPressed()
        {
            if (Current is LevelSelectionFlowCoordinator) {
                Current.PresentFlowCoordinator(_requestFlow, null, AnimationDirection.Horizontal, false, false);
            }
            Logger.Debug($"{Current.name}");
            return;
        }

        internal void SetButtonColor()
        {
            var color = RequestManager.RequestSongs.Any() ? Color.green : Color.red;
            this._button.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline").color = color;
            this._button.interactable = true;
        }

        internal void BackButtonPressed()
        {
            Logger.Debug($"{Current.name} : {_requestFlow.name}");
            if (Current.name != _requestFlow.name) {
                return;
            }
            try {
                Current.GetField<FlowCoordinator, FlowCoordinator>("_parentFlowCoordinator")?.DismissFlowCoordinator(Current, null, AnimationDirection.Horizontal, true);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        #region Unity message
        void Start()
        {
            Logger.Debug("Start()");

            _bot.ChangeButtonColor += this.SetButtonColor;
            _bot.RefreshListRequest += this.RefreshListRequest;
            _requestFlow.QueueStatusChanged += this.OnQueueStatusChanged;
            _requestFlow.PlayProcessEvent += this.ProcessSongRequest;

            this.DownloadProgress.ProgressChanged -= this.Progress_ProgressChanged;
            this.DownloadProgress.ProgressChanged += this.Progress_ProgressChanged;
            try {
                var screen = new GameObject("SRMButton", typeof(CanvasScaler), typeof(RectMask2D), typeof(VRGraphicRaycaster), typeof(CurvedCanvasSettings));
                screen.GetComponent<VRGraphicRaycaster>().SetField("_physicsRaycaster", BeatSaberUI.PhysicsRaycasterWithCache);
                (screen.transform as RectTransform).sizeDelta = new Vector2(30f, 10f);
                (screen.transform as RectTransform).SetParent(levelCollectionNavigationController.transform as RectTransform, false);
                (screen.transform as RectTransform).anchoredPosition = new Vector2(70f, 80f);
                screen.transform.localScale = new Vector3(2f, 2f, 2f);

                Logger.Debug($"{_button == null}");
                if (_button == null) {
                    _button = UIHelper.CreateUIButton((screen.transform as RectTransform), "CancelButton", Vector2.zero, Vector2.zero, Action, "OPEN", null) as NoTransitionsButton;
                    _button.selectionStateDidChangeEvent += this._button_selectionStateDidChangeEvent;
                }
                Logger.Debug($"screem size : {(screen.transform as RectTransform).sizeDelta}");
                Logger.Debug($"button size : {(_button.transform as RectTransform).sizeDelta}");
                Logger.Debug($"button position : {_button.transform.localPosition}");
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            
            this._bot.UpdateRequestUI();

            Logger.Debug("Created request button!");
            Logger.Debug("Start() end");
        }

        private void _button_selectionStateDidChangeEvent(NoTransitionsButton.SelectionState obj)
        {
            this.SetButtonColor();
        }

        protected override void OnDestroy()
        {
            Logger.Debug("OnDestroy");
            _bot.ChangeButtonColor -= this.SetButtonColor;
            _bot.RefreshListRequest -= this.RefreshListRequest;
            _requestFlow.QueueStatusChanged -= this.OnQueueStatusChanged;
            _requestFlow.PlayProcessEvent -= this.ProcessSongRequest;
            this.DownloadProgress.ProgressChanged -= this.Progress_ProgressChanged;
            base.OnDestroy();
        }
        #endregion

        private void OnQueueStatusChanged()
        {
            try {
                var externalComponents = _button.gameObject.GetComponentsInChildren<ExternalComponents>(true).FirstOrDefault();
                var textMesh = externalComponents.components.FirstOrDefault(x => x as TextMeshProUGUI) as TextMeshProUGUI;

                if (RequestBotConfig.Instance.RequestQueueOpen) {
                    textMesh.text = "OPEN";
                }
                else {
                    textMesh.text = "CLOSE";
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        private void Progress_ProgressChanged(object sender, double e)
        {
            this._requestFlow.ChangeProgressText(e);
        }

        private void RefreshListRequest(bool obj)
        {
            if (SceneManager.GetActiveScene().name == "GameCore") {
                return;
            }
            this._requestFlow.RefreshSongList(obj);
        }

        async void ProcessSongRequest(SongRequest request, bool fromHistory = false)
        {
            if ((RequestManager.RequestSongs.Any() && !fromHistory) || (RequestManager.HistorySongs.Any() && fromHistory)) {
                if (!fromHistory) {
                    Logger.Debug("Set status to request");
                    _bot.SetRequestStatus(request, RequestStatus.Played);
                    _bot.DequeueRequest(request);
                }

                if (request == null) {
                    Logger.Debug("Can't process a null request! Aborting!");
                    return;
                }
                else
                    Logger.Debug($"Processing song request {request._song["songName"].Value}");
                string songName = request._song["songName"].Value;
                string songIndex = Regex.Replace($"{request._song["id"].Value} ({request._song["songName"].Value} - {request._song["levelAuthor"].Value})", "[\\\\:*/?\"<>|]", "_");
                songIndex = Normalize.RemoveDirectorySymbols(ref songIndex); // Remove invalid characters.

                string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data\\CustomLevels", songIndex);
                string songHash = request._song["hash"].Value.ToUpper();

                if (Loader.GetLevelByHash(songHash) == null) {
                    Utility.EmptyDirectory(".requestcache", false);

                    if (Directory.Exists(currentSongDirectory)) {
                        Utility.EmptyDirectory(currentSongDirectory, true);
                        Logger.Debug($"Deleting {currentSongDirectory}");
                    }
                    string localPath = Path.Combine(Environment.CurrentDirectory, ".requestcache", $"{request._song["id"].Value}.zip");
#if UNRELEASED
                    // Direct download hack
                    var ext = Path.GetExtension(request.song["coverURL"].Value);
                    var k = request.song["coverURL"].Value.Replace(ext, ".zip");

                    var songZip = await Plugin.WebClient.DownloadSong($"https://beatsaver.com{k}", System.Threading.CancellationToken.None);
#else
                    var result = await WebClient.DownloadSong($"https://beatsaver.com{request._song["downloadURL"].Value}", System.Threading.CancellationToken.None, this.DownloadProgress);
                    if (result == null) {
                        this.ChatManager.QueueChatMessage("BeatSaver is down now.");
                    }
                    using (var zipStream = new MemoryStream(result))
                    using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
                        try {
                            // open zip archive from memory stream
                            archive.ExtractToDirectory(currentSongDirectory);
                        }
                        catch (Exception e) {
                            Logger.Debug($"Unable to extract ZIP! Exception: {e}");
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
                    Logger.Debug($"Song {songName} already exists!");
                    this.BackButtonPressed();
                    bool success = false;
                    Dispatcher.RunOnMainThread(() => this.BackButtonPressed());
                    Dispatcher.RunCoroutine(SongListUtils.ScrollToLevel(songHash, (s) =>
                    {
                        success = s;
                        _bot.UpdateRequestUI();
                    }, false));
                    if (!request._song.IsNull) {
                        // Display next song message
                        _textFactory.Create().AddUser(request._requestor).AddSong(request._song).QueueMessage(StringFormat.NextSonglink.ToString());
                    }
                }
            }
        }

        IEnumerator WaitForRefreshAndSchroll(SongRequest request)
        {
            yield return null;
            yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
            Loader.Instance.RefreshSongs(false);
            yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
            Utility.EmptyDirectory(".requestcache", true);
            bool success = false;
            Dispatcher.RunOnMainThread(() => this.BackButtonPressed());
            Dispatcher.RunCoroutine(SongListUtils.ScrollToLevel(request._song["hash"].Value.ToUpper(), (s) =>
            {
                success = s;
                _bot.UpdateRequestUI();
            }, false));

            ((IProgress<double>)this.DownloadProgress).Report(0d);
            if (!request._song.IsNull) {
                // Display next song message
                _textFactory.Create().AddUser(request._requestor).AddSong(request._song).QueueMessage(StringFormat.NextSonglink.ToString());
            }
        }
    }
}
