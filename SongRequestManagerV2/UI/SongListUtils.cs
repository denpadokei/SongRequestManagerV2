using BS_Utils.Utilities;
using HMUI;
using IPA.Utilities;
using SongCore;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.UI;
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
        private void SelectCustomSongPack(int index)
        {
            // get the Level Filtering Nav Controller, the top bar
            // get the tab bar
            var segcontrol = this._selectLevelCategoryViewController.GetField<IconSegmentedControl, SelectLevelCategoryViewController>("_levelFilterCategoryIconSegmentedControl");
            segcontrol.SelectCellWithNumber(index);
            this._selectLevelCategoryViewController.LevelFilterCategoryIconSegmentedControlDidSelectCell(segcontrol, index);
        }

        public IEnumerator ScrollToLevel(string levelID, Action callback, bool isWip = false)
        {
            if (this._levelCollectionViewController) {
                // Make sure our custom songpack is selected
                this.SelectCustomSongPack(1);

                this._levelFilteringNavigationController.UpdateCustomSongs();

                yield return new WaitWhile(() => this._levelFilteringNavigationController.GetField<CancellationTokenSource>("_cancellationTokenSource") != null);
                var gridView = this._annotatedBeatmapLevelCollectionsViewController.GetField<AnnotatedBeatmapLevelCollectionsGridView, AnnotatedBeatmapLevelCollectionsViewController>("_annotatedBeatmapLevelCollectionsGridView");
                gridView.SelectAndScrollToCellWithIdx(isWip ? 1 : 0);
                var customSong = isWip
                    ? gridView.GetField<IReadOnlyList<IAnnotatedBeatmapLevelCollection>, AnnotatedBeatmapLevelCollectionsGridView>("_annotatedBeatmapLevelCollections").ElementAt(1)
                    : gridView.GetField<IReadOnlyList<IAnnotatedBeatmapLevelCollection>, AnnotatedBeatmapLevelCollectionsGridView>("_annotatedBeatmapLevelCollections").FirstOrDefault();
                this._annotatedBeatmapLevelCollectionsViewController.HandleDidSelectAnnotatedBeatmapLevelCollection(customSong);
                var song = isWip ? Loader.GetLevelById($"custom_level_{levelID.Split('_').Last().ToUpper()} WIP") : Loader.GetLevelByHash(levelID.Split('_').Last());
                if (song == null) {
                    yield break;
                }
                // handle if song browser is present
                if (BetterSongListController.BetterSongListPluginPresent) {
                    BetterSongListController.ClearFilter();
                }
                else if (SongBrowserController.SongBrowserPluginPresent) {
                    SongBrowserController.SongBrowserCancelFilter();
                }
                yield return null;
                // get the table view
                var levelsTableView = this._levelCollectionViewController.GetField<LevelCollectionTableView, LevelCollectionViewController>("_levelCollectionTableView");
                levelsTableView.SelectLevel(song);
            }
            if (RequestBotConfig.Instance?.ClearNoFail == true) {
                var gameplayModifiersPanelController = this._gameplaySetupViewController.GetField<GameplayModifiersPanelController, GameplaySetupViewController>("_gameplayModifiersPanelController");
                gameplayModifiersPanelController.gameplayModifiers.SetField("_noFailOn0Energy", false);
                this._gameplaySetupViewController.RefreshActivePanel();
            }
            callback?.Invoke();
        }
    }
}
