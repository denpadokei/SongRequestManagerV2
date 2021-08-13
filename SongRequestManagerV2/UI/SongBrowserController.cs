using IPA.Loader;
using IPA.Utilities;
using SongBrowser;
using SongBrowser.DataAccess;
using SongBrowser.UI;
using System;

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
                var songBrowserUI = SongBrowserApplication.Instance.GetField<SongBrowserUI, SongBrowserApplication>("_songBrowserUI");
                if (songBrowserUI) {
                    if (songBrowserUI.Model.Settings.filterMode != SongFilterMode.None
                        && songBrowserUI.Model.Settings.sortMode != SongSortMode.Original) {
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
