using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using ChatCore.Interfaces;
using ChatCore.Services;
using ChatCore.Services.Twitch;
using HMUI;
using IPA.Utilities;
using SongCore;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.UI;
using SongRequestManagerV2.Utils;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public MainFlowCoordinator _mainFlowCoordinator;
        [Inject]
        public SoloFreePlayFlowCoordinator _soloFreeFlow;
        [Inject]
        public LevelCollectionNavigationController _levelCollectionNavigationController;
        [Inject]
        public RequestFlowCoordinator _requestFlow;
        [Inject]
        public SearchFilterParamsViewController _searchFilterParamsViewController;
        [Inject]
        private IRequestBot _bot;
        [Inject]
        IChatManager ChatManager { get; }
        [Inject]
        DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        StringNormalization Normalize;
        [Inject]
        SongListUtils SongListUtils;

        private Button _button;

        public Progress<double> DownloadProgress { get; } = new Progress<double>();

        //[Inject]
        //protected PhysicsRaycasterWithCache _physicsRaycaster;
        public HMUI.Screen Screen { get; set; }

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
            if (Current.name != _soloFreeFlow.name) {
                return;
            }
            Current.PresentFlowCoordinator(_requestFlow, null, AnimationDirection.Horizontal, false, false);
        }

        internal void SetButtonColor()
        {
            var color = RequestManager.RequestSongs.Any() ? Color.green : Color.red;
            Logger.Debug($"Change button color : {color}");
            var imageview = this._button.GetComponentsInChildren<ImageView>(true).FirstOrDefault(x => x?.name == "BG");
            if (imageview == null) {
                Logger.Debug("ImageView is null.");
                return;
            }
            imageview.color = color;
            imageview.color0 = color;
            imageview.color1 = color;
            this._button.interactable = true;
        }

        internal void BackButtonPressed()
        {
            Logger.Debug($"{Current.name} : {_requestFlow.name}");
            if (Current.name != _requestFlow.name) {
                Logger.Debug($"{Current.name != _requestFlow.name}");
                return;
            }
            try {
                Current.GetField<FlowCoordinator, FlowCoordinator>("_parentFlowCoordinator")?.DismissFlowCoordinator(Current, null, AnimationDirection.Horizontal, true);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        void Start()
        {
            Logger.Debug("Start()");

            _bot.ChangeButtonColor += this.SetButtonColor;
            _bot.RefreshListRequest += this.RefreshListRequest;
            _requestFlow.PlayProcessEvent += this.ProcessSongRequest;

            this.DownloadProgress.ProgressChanged -= this.Progress_ProgressChanged;
            this.DownloadProgress.ProgressChanged += this.Progress_ProgressChanged;

            if (this.Screen == null) {
                this.Screen = FloatingScreen.CreateFloatingScreen(new Vector2(20f, 20f), false, new Vector3(1.2f, 2.2f, 2.2f), Quaternion.Euler(Vector3.zero));
                var canvas = this.Screen.GetComponent<Canvas>();
                canvas.sortingOrder = 3;
                this.Screen.SetRootViewController(this, AnimationType.None);
            }
            Logger.Debug($"{_button == null}");
            if (_button == null) {
                _button = UIHelper.CreateUIButton(this.Screen.transform, "OkButton", Vector2.zero, Vector2.zero, Action, "SRM", null);
            }

            this._bot.UpdateRequestUI();

            Logger.Debug("Created request button!");
            Logger.Debug("Start() end");
        }

        private void Progress_ProgressChanged(object sender, double e)
        {
            this._requestFlow.ChangeProgressText(e);
        }

        protected override void OnDestroy()
        {
            Logger.Debug("OnDestroy");
            _bot.ChangeButtonColor -= this.SetButtonColor;
            _bot.RefreshListRequest -= this.RefreshListRequest;
            _requestFlow.PlayProcessEvent -= this.ProcessSongRequest;
            this.DownloadProgress.ProgressChanged -= this.Progress_ProgressChanged;
            base.OnDestroy();
        }

        private void RefreshListRequest(bool obj)
        {
            if (SceneManager.GetActiveScene().name == "GameCore") {
                return;
            }
            this._requestFlow.RefreshSongList(obj);
        }

        async void ProcessSongRequest(int index, bool fromHistory = false)
        {
            if ((RequestManager.RequestSongs.Any() && !fromHistory) || (RequestManager.HistorySongs.Any() && fromHistory)) {
                SongRequest request = null;
                if (!fromHistory) {
                    Logger.Debug("Set status to request");
                    _bot.SetRequestStatus(index, RequestStatus.Played);
                    request = _bot.DequeueRequest(index);
                }
                else {
                    request = RequestManager.HistorySongs.ElementAt(index) as SongRequest;
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
            Dispatcher.RunOnMainThread(() => this.BackButtonPressed());
            bool success = false;
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
