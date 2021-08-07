using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Settings;
using BS_Utils.Utilities;
using IPA;
using IPA.Config.Stores;
using IPA.Loader;
using IPA.Utilities;
using SiraUtil.Zenject;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Installer;
using SongRequestManagerV2.Installers;
using SongRequestManagerV2.Networks;
using SongRequestManagerV2.UI;
using SongRequestManagerV2.Views;
using System;
using System.IO;
using System.Reflection;
using IPALogger = IPA.Logging.Logger;

namespace SongRequestManagerV2
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        public string Name => "Song Request ManagerV2";
        public static string Version => _meta.HVersion.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private static PluginMetadata _meta;

        public static IPALogger Logger { get; private set; }
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }

        public static string DataPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "UserData", "Song Request ManagerV2");
        public static bool SongBrowserPluginPresent;

        [Init]
        public void Init(IPALogger log, IPA.Config.Config config, PluginMetadata meta, Zenjector zenjector)
        {
            Instance = this;
            _meta = meta;
            Logger = log;
            Logger.Debug("Logger initialized.");
            RequestBotConfig.Instance = config.Generated<RequestBotConfig>();
            zenjector.OnApp<SRMAppInstaller>();
            zenjector.OnMenu<SRMInstaller>();
        }

        [OnStart]
        public void OnStart()
        {
            if (!Directory.Exists(DataPath)) {
                Directory.CreateDirectory(DataPath);
            }

            SongBrowserPluginPresent = PluginManager.GetPlugin("Song Browser") != null;
            BSEvents.lateMenuSceneLoadedFresh += this.BSEvents_lateMenuSceneLoadedFresh;
            // init sprites
            Base64Sprites.Init();
        }
        /// <summary>
        /// setup settings ui
        /// </summary>
        /// <param name="obj"></param>
        private void BSEvents_lateMenuSceneLoadedFresh(ScenesTransitionSetupDataSO obj) => BSMLSettings.instance.AddSettingsMenu("SRM V2", "SongRequestManagerV2.Views.SongRequestManagerSettings.bsml", BeatSaberUI.CreateViewController<SongRequestManagerSettings>());

        public static void SongBrowserCancelFilter()
        {
            if (SongBrowserPluginPresent) {
                var songBrowserUI = SongBrowser.SongBrowserApplication.Instance.GetField<SongBrowser.UI.SongBrowserUI, SongBrowser.SongBrowserApplication>("_songBrowserUI");
                if (songBrowserUI) {
                    if (songBrowserUI.Model.Settings.filterMode != SongBrowser.DataAccess.SongFilterMode.None && songBrowserUI.Model.Settings.sortMode != SongBrowser.DataAccess.SongSortMode.Original) {
                        songBrowserUI.CancelFilter();
                    }
                }
                else {
                    Logger.Debug("There was a problem obtaining SongBrowserUI object, unable to reset filters");
                }
            }
        }

        [OnExit]
        public void OnExit()
        {
            BSEvents.lateMenuSceneLoadedFresh -= this.BSEvents_lateMenuSceneLoadedFresh;
            this.IsApplicationExiting = true;
            BouyomiPipeline.instance.Stop();
        }
    }
}
