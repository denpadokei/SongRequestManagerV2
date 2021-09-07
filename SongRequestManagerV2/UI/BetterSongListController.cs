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

        public static void ClearFilter()
        {
            try {
                if (!BetterSongListPluginPresent) {
                    return;
                }
                if (_betterSonglistMetaData.HVersion.Major != 0) {
                    return;
                }
                var filerUI = Type.GetType("BetterSongList.UI.FilterUI, BetterSongList");
                var filterUIInstance = filerUI.GetField("persistentNuts", (BindingFlags.NonPublic | BindingFlags.Static)).GetValue(filerUI);
                var filterDorpDown = (DropdownWithTableView)filerUI.GetField("_filterDropdown", (BindingFlags.NonPublic | BindingFlags.Instance)).GetValue(filterUIInstance);
                if (filterDorpDown.selectedIndex != 0) {
                    var setFilterMethod = filerUI.GetMethod("SetFilter", (BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
                    setFilterMethod.Invoke(filerUI, new object[] { null, true, true });
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}
