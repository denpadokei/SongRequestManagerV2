﻿using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using IPA.Utilities;
using SongCore;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Configuration;
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
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRUIControls;
using Zenject;

namespace SongRequestManagerV2.Views
{
    [HotReload]
    public class SRMButton : BSMLAutomaticViewController, IInitializable
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        [Inject]
        private readonly MainFlowCoordinator _mainFlowCoordinator;
        [Inject]
        private readonly LevelCollectionNavigationController _levelCollectionNavigationController;
        [Inject]
        private readonly RequestFlowCoordinator _requestFlow;
        [Inject]
        private readonly IRequestBot _bot;
        [Inject]
        private readonly IChatManager _chatManager;
        [Inject]
        private readonly DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        private readonly StringNormalization _normalize;
        [Inject]
        private readonly SongListUtils _songListUtils;
        private GameObject _rootScreenGo;
        private Button _button;

        private readonly WaitForSeconds _waitForSeconds = new WaitForSeconds(0.07f);

        private volatile bool _isChangeing = false;
        private bool _isInGame = false;
        public Progress<double> DownloadProgress { get; } = new Progress<double>();

        public FlowCoordinator Current => this._mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

        private static readonly SemaphoreSlim s_downloadSemaphore = new SemaphoreSlim(1, 1);

        [UIAction("action")]
        public void Action()
        {
            try {
                this._button.interactable = false;
                this.SRMButtonPressed();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                this._button.interactable = true;
            }
        }

        internal void SRMButtonPressed()
        {
            if (this.Current is LevelSelectionFlowCoordinator) {
                this.Current.PresentFlowCoordinator(this._requestFlow, null, AnimationDirection.Horizontal, false, false);
            }
        }

        internal void SetButtonColor()
        {
            ImageView underLine = null;
            if (Utility.IsAprilFool()) {
                if (RequestManager.RequestSongs.Any()) {
                    Dispatcher.RunCoroutine(this.ChangeButtonColor());
                }
                else {
                    this._isChangeing = false;
                    var color = Color.red;
                    underLine = this._button.GetComponentsInChildren<ImageView>(true).FirstOrDefault(x => x.name == "Underline");
                    if (underLine != null) {
                        underLine.color = color;
                    }
                }
            }
            else {
                var color = RequestManager.RequestSongs.Any() ? Color.green : Color.red;
                underLine = this._button.GetComponentsInChildren<ImageView>(true).FirstOrDefault(x => x.name == "Underline");
                if (underLine != null) {
                    underLine.color = color;
                }
            }
            this._button.interactable = true;
        }

        internal void BackButtonPressed()
        {
            if (this.Current.name != this._requestFlow.name) {
                return;
            }
            try {
                this.Current.GetField<FlowCoordinator, FlowCoordinator>("_parentFlowCoordinator")?.DismissFlowCoordinator(this.Current, null, AnimationDirection.Horizontal, true);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
        public void Initialize()
        {
            this._bot.ChangeButtonColor += this.SetButtonColor;
            this._bot.RefreshListRequest += this.RefreshListRequest;
            RequestBotConfig.Instance.ConfigChangedEvent += this.OnConfigChangedEvent;
            this._requestFlow.QueueStatusChanged += this.ChangeButtonText;
            this._requestFlow.PlayProcessEvent += this.ProcessSongRequest;

            this.DownloadProgress.ProgressChanged -= this.Progress_ProgressChanged;
            this.DownloadProgress.ProgressChanged += this.Progress_ProgressChanged;
            SceneManager.activeSceneChanged += this.SceneManager_activeSceneChanged;
            try {
                this._rootScreenGo = new GameObject("SRMButton", typeof(CanvasScaler), typeof(RectMask2D), typeof(VRGraphicRaycaster), typeof(CurvedCanvasSettings));
                this._rootScreenGo.GetComponent<VRGraphicRaycaster>().SetField("_physicsRaycaster", BeatSaberUI.PhysicsRaycasterWithCache);
                var vertical = this._rootScreenGo.AddComponent<VerticalLayoutGroup>();
                var fitter = vertical.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                (this._rootScreenGo.transform as RectTransform).sizeDelta = new Vector2(40f, 30f);
                (this._rootScreenGo.transform as RectTransform).SetParent(this._levelCollectionNavigationController.transform as RectTransform, false);
                (this._rootScreenGo.transform as RectTransform).anchoredPosition = new Vector2(70f, 80f);
                this._rootScreenGo.transform.localScale = Vector3.one * 2;
                if (this._button == null) {
                    this._button = UIHelper.CreateUIButton(this._rootScreenGo.transform as RectTransform, "CancelButton", Vector2.zero, Vector2.zero, this.Action, "OPEN", null) as NoTransitionsButton;
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }

            if (RequestBotConfig.Instance.AutoOpenRequestQueue && !RequestBotConfig.Instance.RequestQueueOpen) {
                RequestBotConfig.Instance.RequestQueueOpen = true;
                this._bot.WriteQueueStatusToFile(this._bot.QueueMessage(true));
            }

            this._bot.UpdateRequestUI();
            this.SetButtonColor();
        }

        private void OnConfigChangedEvent(RequestBotConfig obj)
        {
            Dispatcher.RunOnMainThread(this.ChangeButtonText);
        }

        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            this._isInGame = string.Equals(arg1.name, "GameCore", StringComparison.CurrentCultureIgnoreCase);
        }

        #region Unity message
        protected override void OnDestroy()
        {
            Logger.Debug("OnDestroy");
            this._bot.ChangeButtonColor -= this.SetButtonColor;
            this._bot.RefreshListRequest -= this.RefreshListRequest;
            RequestBotConfig.Instance.ConfigChangedEvent -= this.OnConfigChangedEvent;
            this._requestFlow.PlayProcessEvent -= this.ProcessSongRequest;
            this.DownloadProgress.ProgressChanged -= this.Progress_ProgressChanged;
            SceneManager.activeSceneChanged -= this.SceneManager_activeSceneChanged;
            Destroy(this._rootScreenGo);
            base.OnDestroy();
        }

        public void OnEnable()
        {
            this.ChangeButtonText();
        }
        #endregion
        private void ChangeButtonText()
        {
            try {
                var externalComponents = this._button.gameObject.GetComponentsInChildren<ExternalComponents>(true).FirstOrDefault();
                var textMesh = externalComponents.Components.FirstOrDefault(x => x as TextMeshProUGUI) as TextMeshProUGUI;
                if (textMesh != null) {
                    textMesh.text = RequestBotConfig.Instance.RequestQueueOpen ? "OPEN" : "CLOSE";
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
            if (this._isInGame) {
                return;
            }
            this._requestFlow.RefreshSongList(obj);
        }

        /// <summary>
        /// プレイボタンを押したときの処理
        /// </summary>
        /// <param name="request"></param>
        /// <param name="fromHistory"></param>
        private async void ProcessSongRequest(SongRequest request, bool fromHistory = false)
        {
            if (!fromHistory && !RequestManager.RequestSongs.Any()) {
                return;
            }
            if (fromHistory && !RequestManager.HistorySongs.Any()) {
                return;
            }
            await s_downloadSemaphore.WaitAsync();
            try {
                this._bot.PlayNow = request;
                if (!fromHistory) {
                    this._bot.SetRequestStatus(request, RequestStatus.Played);
                    this._bot.DequeueRequest(request);
                }

                if (request == null) {
                    return;
                }
                var currentSongDirectory = this.CreateSongDirectory(request);
                var songHash = request.SongVersion["hash"].Value.ToUpper();

                if (Loader.GetLevelByHash(songHash) == null) {
                    var result = await request.DownloadZip(CancellationToken.None, this.DownloadProgress);
                    if (result == null) {
                        this._chatManager.QueueChatMessage("beatsaver is down now.");
                    }
                    using (var zipStream = new MemoryStream(result))
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
                        try {
                            // open zip archive from memory stream
                            archive.ExtractToDirectory(currentSongDirectory);
                        }
                        catch (Exception e) {
                            this._chatManager.QueueChatMessage($"Oops! Sorry unable to extract ZIP!");
                            Logger.Error(e);
                            return;
                        }
                    }
                    Dispatcher.RunCoroutine(this.WaitForRefreshAndSchroll(request));
                }
                else {
                    Dispatcher.RunOnMainThread(() => this.BackButtonPressed());
                    Dispatcher.RunCoroutine(this._songListUtils.ScrollToLevel(songHash, () =>
                    {
                        this._bot.UpdateRequestUI();
                    },
                    request.IsWIP));
                    if (!request.SongNode.IsNull) {
                        // Display next song message
                        _ = this._textFactory.Create().AddUser(request.Requestor).AddSong(request.SongNode).QueueMessage(StringFormat.NextSonglink.ToString());
                    }
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                _ = s_downloadSemaphore.Release();
            }
        }

        private string CreateSongDirectory(SongRequest request)
        {
            var songIndex = Regex.Replace($"{request.ID} ({request.SongMetaData["songName"].Value} - {request.SongMetaData["levelAuthorName"].Value})", "[\\\\:*/?\"<>|]", "_");
            songIndex = this._normalize.RemoveDirectorySymbols(songIndex); // Remove invalid characters.
            var result = request.IsWIP ? Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomWIPLevels", songIndex) : Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", songIndex);
            var count = 1;
            var resultLength = result.Length;
            while (Directory.Exists(result)) {
                result = $"{result.Substring(0, resultLength)}({count})";
                count++;
            }
            return result;
        }

        private IEnumerator WaitForRefreshAndSchroll(SongRequest request)
        {
            yield return null;
            if (this._isInGame) {
                // ダウンロード中に1曲やるかーって人向けの処理
                while (true) {
                    yield return new WaitWhile(() => this._isInGame);
                    yield return new WaitForSeconds(4.0f);
                    if (!this._isInGame) {
                        break;
                    }
                    yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
                    Loader.Instance.RefreshSongs(false);
                }
                ((IProgress<double>)this.DownloadProgress).Report(0d);
            }
            else {
                yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
                Loader.Instance.RefreshSongs(false);
                yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
                Utility.EmptyDirectory(".requestcache", true);

                Dispatcher.RunOnMainThread(() => this.BackButtonPressed());
                Dispatcher.RunCoroutine(this._songListUtils.ScrollToLevel($"custom_level_{request.SongVersion["hash"].Value.ToLower()}", () =>
                {
                    this._bot.UpdateRequestUI();
                },
                request.IsWIP));

                ((IProgress<double>)this.DownloadProgress).Report(0d);
                if (!request.SongNode.IsNull) {
                    // Display next song message
                    _ = this._textFactory.Create().AddUser(request.Requestor).AddSong(request.SongNode).QueueMessage(StringFormat.NextSonglink.ToString());
                }
            }
        }

        private IEnumerator ChangeButtonColor()
        {
            if (this._isChangeing) {
                yield break;
            }
            this._isChangeing = true;
            while (this._isChangeing) {
                var red = UnityEngine.Random.Range(0f, 1f);
                var green = UnityEngine.Random.Range(0f, 1f);
                var blue = UnityEngine.Random.Range(0f, 1f);
                var color = new Color(red, green, blue);
                var underLine = this._button.GetComponentsInChildren<ImageView>(true).FirstOrDefault(x => x.name == "Underline");
                if (underLine != null) {
                    underLine.color = color;
                }
                yield return this._waitForSeconds;
            }
        }
    }
}
