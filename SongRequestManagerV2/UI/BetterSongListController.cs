using HMUI;
using IPA.Loader;
using System;
using System.Reflection;

namespace SongRequestManagerV2.UI
{
    public class BetterSongListController
    {
        public static bool BetterSongListPluginPresent { get; private set; }
        private static readonly PluginMetadata _betterSonglistMetaData;
        static BetterSongListController()
        {
            _betterSonglistMetaData = PluginManager.GetPlugin("BetterSongList");
            BetterSongListPluginPresent = _betterSonglistMetaData != null;
        }

        /// <summary>
        /// 通常のフィルタークリアと違って同期的に行うためきちんとリロードまで待ちます。
        /// </summary>
        public static void ClearFilter()
        {
            try {
                if (!BetterSongListPluginPresent) {
                    return;
                }
                if (_betterSonglistMetaData.HVersion.Major != 0) {
                    return;
                }
                Type filerUI = Type.GetType("BetterSongList.UI.FilterUI, BetterSongList");
                object filterUIInstance = filerUI.GetField("persistentNuts", (BindingFlags.NonPublic | BindingFlags.Static)).GetValue(filerUI);
                DropdownWithTableView filterDorpDown = (DropdownWithTableView)filerUI.GetField("_filterDropdown", (BindingFlags.NonPublic | BindingFlags.Instance)).GetValue(filterUIInstance);
                if (filterDorpDown.selectedIndex != 0) {
                    MethodInfo setFilterMethod = filerUI.GetMethod("SetFilter", (BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
                    setFilterMethod.Invoke(filerUI, new object[] { null, true, false });
                    ResetLevelCollectionTableSet();
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        /// <summary>
        /// リセット用メソッド
        /// </summary>
        /// <param name="asyncProcess"></param>
        public static void ResetLevelCollectionTableSet(bool asyncProcess = false)
        {
            if (!BetterSongListPluginPresent) {
                return;
            }
            if (_betterSonglistMetaData.HVersion.Major != 0) {
                return;
            }
            try {
                Type levelCollectionTableSet = Type.GetType("BetterSongList.HarmonyPatches.HookLevelCollectionTableSet, BetterSongList");
                MethodInfo setFilterMethod = levelCollectionTableSet.GetMethod("Refresh", (BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
                setFilterMethod.Invoke(levelCollectionTableSet, new object[] { asyncProcess });
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}
