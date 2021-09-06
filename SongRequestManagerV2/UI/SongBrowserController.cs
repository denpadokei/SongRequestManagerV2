using IPA.Loader;
using SongBrowser;
using SongBrowser.Configuration;
using System;
using System.Linq;
using System.Reflection;

namespace SongRequestManagerV2.UI
{
    public class SongBrowserController
    {
        public static bool SongBrowserPluginPresent { get; private set; }
        private static readonly PluginMetadata _songBrowserMetaData;

        static SongBrowserController()
        {
            _songBrowserMetaData = PluginManager.GetPlugin("Song Browser");
            SongBrowserPluginPresent = _songBrowserMetaData != null;
        }

        public static void SongBrowserCancelFilter()
        {
            try {
                if (!SongBrowserPluginPresent) {
                    return;
                }
                if (_songBrowserMetaData.HVersion.Major != 6) {
                    return;
                }
                var asm = Assembly.GetAssembly(typeof(SongBrowserApplication));
                var sbModule = asm.GetModule("SongBrowser.dll");
                var configType = sbModule.GetType("SongBrowser.Configuration.PluginConfig");
                var configInstance = configType.GetProperty("Instance", (BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)).GetValue(configType);
                var filterModeProp = configType.GetProperty("FilterMode");
                var sortModeProp = configType.GetProperty("SortMode");
                var songBrowserUI = SongBrowserApplication.Instance.Ui;
                if (filterModeProp != null && sortModeProp != null && songBrowserUI) {
                    var filter = (SongFilterMode)filterModeProp.GetValue(configInstance);
                    var sortMode = (SongSortMode)sortModeProp.GetValue(configInstance);
                    if (filter != SongFilterMode.None
                        && sortMode != SongSortMode.Original) {
                        songBrowserUI.CancelFilter();
                    }
                }
                else {
                    Logger.Debug("There was a problem obtaining SongBrowserUI object, unable to reset filters");
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}
