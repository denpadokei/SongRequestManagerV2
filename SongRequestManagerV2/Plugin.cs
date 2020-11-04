using IPA;
using IPALogger = IPA.Logging.Logger;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using SongRequestManagerV2.UI;
using BeatSaberMarkupLanguage.Settings;
using IPA.Utilities;
using ChatCore.Interfaces;
using ChatCore;
using ChatCore.Services;
using IPA.Loader;
using System.Reflection;
using BS_Utils.Utilities;
using ChatCore.Services.Twitch;
using ChatCore.Models.Twitch;
using SongRequestManagerV2.Networks;
using ChatCore.Models;
using SongRequestManagerV2.Models;
using SiraUtil.Zenject;
using SongRequestManagerV2.Installers;
using Zenject;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Installer;

namespace SongRequestManagerV2
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        public string Name => "Song Request ManagerV2";
        public static string Version => _meta.Version.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private static PluginMetadata _meta;

        public static IPALogger Logger { get; internal set; }

        

        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }

        private RequestBotConfig RequestBotConfig { get; } = RequestBotConfig.Instance;

        public static string DataPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "UserData", "Song Request ManagerV2");
        public static bool SongBrowserPluginPresent;

        [Init]
        public void Init(IPALogger log, PluginMetadata meta, Zenjector zenjector)
        {
            Instance = this;
            _meta = meta;
            Logger = log;
            Logger.Debug("Logger initialized.");
            zenjector.OnApp<SRMAppInstaller>();
            zenjector.OnMenu<SRMInstaller>();
        }

        public static void Log(string text,
                        [CallerFilePath] string file = "",
                        [CallerMemberName] string member = "",
                        [CallerLineNumber] int line = 0)
        {
            Logger.Info($"[SongRequestManagerV2] {Path.GetFileName(file)}->{member}({line}): {text}");
        }

        [OnStart]
        public void OnStart()
        {
            if (PluginManager.GetPlugin("Song Request Manager") != null) {
                return;
            }
            if (!Directory.Exists(DataPath)) {
                Directory.CreateDirectory(DataPath);
            }
            
            SongBrowserPluginPresent = PluginManager.GetPlugin("Song Browser") != null;
            // setup handle for fresh menu scene changes
            BSEvents.OnLoad();
            // keep track of active scene
            BSEvents.menuSceneActive += () => { IsAtMainMenu = true; };
            BSEvents.gameSceneActive += () => { IsAtMainMenu = false; };

            // init sprites
            Base64Sprites.Init();
        }

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
                    Plugin.Log("There was a problem obtaining SongBrowserUI object, unable to reset filters");
                }
            }
        }

        [OnExit]
        public void OnExit()
        {
            IsApplicationExiting = true;
            BouyomiPipeline.instance.Stop();
        }
    }
}
