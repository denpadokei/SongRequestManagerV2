//using StreamCore.Utils;
using HMUI;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using IPA.Utilities;
using IPA.Loader;
using SongCore;
using System.Threading.Tasks;
using System.Threading;
using Zenject;
using System.Collections.Generic;

namespace SongRequestManagerV2
{
    public class SongListUtils
    {
        [Inject]
        private LevelCollectionViewController _levelCollectionViewController;
        [Inject]
        private SelectLevelCategoryViewController _selectLevelCategoryViewController;
        [Inject]
        private GameplaySetupViewController _gameplaySetupViewController;
        [Inject]
        private LevelFilteringNavigationController _levelFilteringNavigationController;
        [Inject]
        private AnnotatedBeatmapLevelCollectionsViewController _annotatedBeatmapLevelCollectionsViewController;

        private static bool _initialized = false;
        //private static bool _songBrowserInstalled = false;
        //private static bool _songDownloaderInstalled = false;
        [Inject]
        public void Initialize()
        {
            

            if (!_initialized)
            {
                try
                {
                    //_songBrowserInstalled = Utilities.IsModInstalled("Song Browser");
                    //_songDownloaderInstalled = IPA.Loader.PluginManager.GetPlugin("BeatSaver Downloader") != null;

                    //Logger.Debug($"Song Browser installed: {_songBrowserInstalled}");
                    //Logger.Debug($"Downloader installed: {_songDownloaderInstalled}");
                    _initialized = true;
                }
                catch (Exception e)
                {
                    Logger.Debug($"Exception {e}");
                }
            }
        }

        private enum SongBrowserAction { ResetFilter = 1 }
        private static void ExecuteSongBrowserAction(SongBrowserAction action)
        {
            //var _songBrowserUI = SongBrowser.SongBrowserApplication.Instance.GetField<SongBrowser.UI.SongBrowserUI, SongBrowser.SongBrowserApplication>("_songBrowserUI");
            //if (_songBrowserUI)
            //{
            //    if (action.HasFlag(SongBrowserAction.ResetFilter))
            //    {
            //        // if filter mode is set, clear it
            //        if (_songBrowserUI.Model.Settings.filterMode != SongBrowser.DataAccess.SongFilterMode.None)
            //        {
            //            _songBrowserUI.InvokePrivateMethod("OnClearButtonClickEvent");
            //        }
            //    }
            //}
        }

        //private enum SongDownloaderAction { ResetFilter = 1 }
        //private static void ExecuteSongDownloaderAction(SongDownloaderAction action)
        //{
        //    //if (action.HasFlag(SongDownloaderAction.ResetFilter))
        //    //{
        //    //    SongListTweaks.Instance.SetLevels(SortMode.Newest, "");
        //    //}
        //}

        //public static IEnumerator RetrieveNewSong(string songFolderName, bool resetFilterMode = false)
        //{
        //    //if (!SongLoaderPlugin.SongLoader.AreSongsLoaded) yield break;

        //    //if (!_standardLevelListViewController) yield break;

        //    //SongLoaderPlugin.SongLoader.Instance.RetrieveNewSong(songFolderName);

        //    yield return null;

        //    //// If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
        //    //if (resetFilterMode)
        //    //{
        //    //    // If song browser is installed, update/refresh it
        //    //    if (_songBrowserInstalled)
        //    //        ExecuteSongBrowserAction(SongBrowserAction.ResetFilter);
        //    //    // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
        //    //if (_songDownloaderInstalled)
        //    //  ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);
        //    //}

        //    //// Set the row index to the previously selected song
        //    //if (selectOldLevel)
        //    //    ScrollToLevel(selectedLevelId);
        //}

        public IEnumerator RefreshSongs(bool fullRefresh = false, bool selectOldLevel = true)
        {
            //if (!SongLoaderPlugin.SongLoader.AreSongsLoaded) yield break;
            //if (!_standardLevelListViewController) yield break;

            // Grab the currently selected level id so we can restore it after refreshing
            //string selectedLevelId = _standardLevelListViewController.selectedLevel?.levelID;

            // Wait until song loader is finished loading, then refresh the song list
            //while (SongLoaderPlugin.SongLoader.AreSongsLoading) yield return null;
            //SongLoaderPlugin.SongLoader.Instance.RefreshSongs(fullRefresh);
            //while (SongLoaderPlugin.SongLoader.AreSongsLoading) yield return null;

            yield return null;

            // Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        private void SelectCustomSongPack(int index)
        {
            // get the Level Filtering Nav Controller, the top bar
            // get the tab bar
            //var selectLevelCategoryViewController = Resources.FindObjectsOfTypeAll<SelectLevelCategoryViewController>().First();
            var segcontrol = _selectLevelCategoryViewController.GetField<IconSegmentedControl, SelectLevelCategoryViewController>("_levelFilterCategoryIconSegmentedControl");
            segcontrol.SelectCellWithNumber(index);
            _selectLevelCategoryViewController.LevelFilterCategoryIconSegmentedControlDidSelectCell(segcontrol, index);
        }

        //public static SongCore.OverrideClasses.SongCoreCustomLevelCollection BeatSaverDownloaderGetLevelPackWithLevels()
        //{
        //    var levels = SongCore.Loader.CustomLevelsPack.beatmapLevelCollection.beatmapLevels.Cast<CustomPreviewBeatmapLevel>().ToArray();
        //    var pack = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.First(x => x.packID == "custom_levelpack_CustomLevels");
        //    //return BeatSaverDownloader.Misc.CustomHelpers.GetLevelPackWithLevels(levels, "Custom Songs", pack.coverImage);
        //    return null;
        //}

        //bool barf(string s)
        //{
        //    RequestBot.Instance.QueueChatMessage($"x={s}");
        //    return true;
        //}

        public IEnumerator ScrollToLevel(string levelID, Action<bool> callback, bool animated, bool isRetry = false)
        {
            if (_levelCollectionViewController)
            {
                Logger.Debug($"Scrolling to {levelID}! Retry={isRetry}");

                // handle if song browser is present
                if (Plugin.SongBrowserPluginPresent)
                {
                    Plugin.SongBrowserCancelFilter();
                }

                // Make sure our custom songpack is selected
                SelectCustomSongPack(2);
                _levelFilteringNavigationController.UpdateCustomSongs();
                var tableView = _annotatedBeatmapLevelCollectionsViewController.GetField<AnnotatedBeatmapLevelCollectionsTableView, AnnotatedBeatmapLevelCollectionsViewController>("_annotatedBeatmapLevelCollectionsTableView");
                tableView.SelectAndScrollToCellWithIdx(0);
                var customSong = tableView.GetField<IReadOnlyList<IAnnotatedBeatmapLevelCollection>, AnnotatedBeatmapLevelCollectionsTableView>("_annotatedBeatmapLevelCollections").FirstOrDefault();
                _levelFilteringNavigationController.HandleAnnotatedBeatmapLevelCollectionsViewControllerDidSelectAnnotatedBeatmapLevelCollection(customSong);


                var song = Loader.GetLevelByHash(levelID.Split('_').Last());
                if (song == null) {
                    Logger.Debug("Song not find.");
                    yield break;
                }
                // get the table view
                var levelsTableView = _levelCollectionViewController.GetField<LevelCollectionTableView, LevelCollectionViewController>("_levelCollectionTableView");
                levelsTableView.SelectLevel(song);
                
            }
            callback?.Invoke(false);

            if (RequestBotConfig.Instance?.ClearNoFail == true) {
                var gameplayModifiersPanelController = this._gameplaySetupViewController.GetField<GameplayModifiersPanelController, GameplaySetupViewController>("_gameplayModifiersPanelController");
                gameplayModifiersPanelController.gameplayModifiers.SetField("_noFail", false);
                this._gameplaySetupViewController.RefreshActivePanel();
            }
        }
    }
}
