using IPA.Loader;
using System;
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
                var configType = Type.GetType("SongBrowser.Configuration.PluginConfig, SongBrowser");
                var configInstance = configType.GetProperty("Instance", (BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)).GetValue(configType);
                var filterModeProp = configType.GetProperty("FilterMode");
                var sortModeProp = configType.GetProperty("SortMode");
                var sbAppInfo = Type.GetType("SongBrowser.SongBrowserApplication, SongBrowser");
                var sbAppInstance = sbAppInfo.GetField("Instance", (BindingFlags.Static | BindingFlags.Public)).GetValue(sbAppInfo);
                var songBrowserUIType = Type.GetType("SongBrowser.UI.SongBrowserUI, SongBrowser"); //SongBrowserApplication.Instance.Ui;
                var songBrowserUI = sbAppInfo.GetProperty("Ui", BindingFlags.Public | BindingFlags.Instance).GetValue(sbAppInstance);
                if (filterModeProp != null && sortModeProp != null && songBrowserUI != null) {
                    var filter = (int)filterModeProp.GetValue(configInstance);
                    var sortMode = (int)sortModeProp.GetValue(configInstance);
                    if (filter != 0 && sortMode != 2) {
                        songBrowserUIType.GetMethod("CancelFilter").Invoke(songBrowserUI, null);
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
