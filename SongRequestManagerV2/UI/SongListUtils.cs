using BS_Utils.Utilities;
using HMUI;
using IPA.Utilities;
using SongCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Zenject;

namespace SongRequestManagerV2
{
    public class SongListUtils
    {
        [Inject]
        private readonly LevelCollectionViewController _levelCollectionViewController;
        [Inject]
        private readonly SelectLevelCategoryViewController _selectLevelCategoryViewController;
        [Inject]
        private readonly GameplaySetupViewController _gameplaySetupViewController;
        [Inject]
        private readonly LevelFilteringNavigationController _levelFilteringNavigationController;
        [Inject]
        private readonly AnnotatedBeatmapLevelCollectionsViewController _annotatedBeatmapLevelCollectionsViewController;
        [Inject]
        private readonly ResultsViewController _resultsViewController;

        private bool _initialized = false;
        //private static bool _songBrowserInstalled = false;
        //private static bool _songDownloaderInstalled = false;
        [Inject]
        public void Constractor()
        {
            if (!this._initialized) {
                this._initialized = true;
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

            yield break;

            // Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        private void SelectCustomSongPack(int index)
        {
            // get the Level Filtering Nav Controller, the top bar
            // get the tab bar
            //var selectLevelCategoryViewController = Resources.FindObjectsOfTypeAll<SelectLevelCategoryViewController>().First();
            var segcontrol = this._selectLevelCategoryViewController.GetField<IconSegmentedControl, SelectLevelCategoryViewController>("_levelFilterCategoryIconSegmentedControl");
            segcontrol.SelectCellWithNumber(index);
            this._selectLevelCategoryViewController.LevelFilterCategoryIconSegmentedControlDidSelectCell(segcontrol, index);
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

        public IEnumerator ScrollToLevel(string levelID, Action callback, bool isWip = false)
        {
            if (this._levelCollectionViewController) {
                // handle if song browser is present
                if (Plugin.SongBrowserPluginPresent) {
                    Plugin.SongBrowserCancelFilter();
                }

                // Make sure our custom songpack is selected
                this.SelectCustomSongPack(2);

                this._levelFilteringNavigationController.UpdateCustomSongs();

                yield return new WaitWhile(() => this._levelFilteringNavigationController.GetField<CancellationTokenSource>("_cancellationTokenSource") != null);
                var tableView = this._annotatedBeatmapLevelCollectionsViewController.GetField<AnnotatedBeatmapLevelCollectionsTableView, AnnotatedBeatmapLevelCollectionsViewController>("_annotatedBeatmapLevelCollectionsTableView");
                tableView.SelectAndScrollToCellWithIdx(isWip ? 1 : 0);
                var customSong = isWip
                    ? tableView.GetField<IReadOnlyList<IAnnotatedBeatmapLevelCollection>, AnnotatedBeatmapLevelCollectionsTableView>("_annotatedBeatmapLevelCollections").ElementAt(1)
                    : tableView.GetField<IReadOnlyList<IAnnotatedBeatmapLevelCollection>, AnnotatedBeatmapLevelCollectionsTableView>("_annotatedBeatmapLevelCollections").FirstOrDefault();
                this._levelFilteringNavigationController.HandleAnnotatedBeatmapLevelCollectionsViewControllerDidSelectAnnotatedBeatmapLevelCollection(customSong);
                var song = isWip ? Loader.GetLevelById($"custom_level_{levelID.Split('_').Last().ToUpper()} WIP") : Loader.GetLevelByHash(levelID.Split('_').Last());
                if (song == null) {
                    yield break;
                }
                // get the table view
                var levelsTableView = this._levelCollectionViewController.GetField<LevelCollectionTableView, LevelCollectionViewController>("_levelCollectionTableView");
                levelsTableView.SelectLevel(song);
            }
            callback?.Invoke();
            if (RequestBotConfig.Instance?.ClearNoFail == true) {
                var gameplayModifiersPanelController = this._gameplaySetupViewController.GetField<GameplayModifiersPanelController, GameplaySetupViewController>("_gameplayModifiersPanelController");
                gameplayModifiersPanelController.gameplayModifiers.SetField("_noFailOn0Energy", false);
                this._gameplaySetupViewController.RefreshActivePanel();
            }
        }
    }
}
