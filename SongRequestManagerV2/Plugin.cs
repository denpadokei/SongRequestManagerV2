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
using ChatCore.Services.Mixer;
using ChatCore.Services.Twitch;

namespace SongRequestManagerV2
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public string Name => "Song Request ManagerV2";
        public static string Version => _meta.Version.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private static PluginMetadata _meta;

        public static IPALogger Logger { get; internal set; }

        public ChatCoreInstance CoreInstance { get; internal set; }
        public ChatServiceMultiplexer MultiplexerInstance { get; internal set; }
        public TwitchService TwitchService { get; internal set; }
        public MixerService MixerService { get; internal set; }

        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }

        private RequestBotConfig RequestBotConfig { get; } = RequestBotConfig.Instance;

        public static string DataPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "UserData", "Song Request ManagerV2");
        public static bool SongBrowserPluginPresent;

        [Init]
        public void Init(IPALogger log, PluginMetadata meta)
        {
            Instance = this;
            _meta = meta;
            Logger = log;
            Logger.Debug("Logger initialized.");
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
            if (!Directory.Exists(DataPath)) {
                try {
                    Directory.CreateDirectory(DataPath);
                }
                catch (Exception e) {
                    Log($"{e}");
                }
            }
            this.CoreInstance = ChatCoreInstance.Create();
            this.MultiplexerInstance = this.CoreInstance.RunAllServices();
            RequestBot.Instance.Awake();
            //if (Instance != null) return;
            //Instance = this;
            Dispatcher.Initialize();


            SongBrowserPluginPresent = PluginManager.GetPlugin("Song Browser") != null;
            // setup handle for fresh menu scene changes
            BSEvents.OnLoad();
            BSEvents.lateMenuSceneLoadedFresh += OnMenuSceneLoadedFresh;

            // keep track of active scene
            BSEvents.menuSceneActive += () => { IsAtMainMenu = true; };
            BSEvents.gameSceneActive += () => { IsAtMainMenu = false; };

            // init sprites
            Base64Sprites.Init();
        }

        private void MultiplexerInstance_OnJoinChannel(IChatService arg1, IChatChannel arg2)
        {
            Log($"Joined! : [{arg1.DisplayName}][{arg2.Name}]");
            if (arg1 is MixerService mixerService) {
                this.MixerService = mixerService;
                
            }
            else if (arg1 is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
            
        }

        private void MultiplexerInstance_OnLogin(IChatService obj)
        {
            Log($"Loged in! : [{obj.DisplayName}]");
            if (obj is MixerService mixerService) {
                this.MixerService = mixerService;
            }
            else if (obj is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }

        private void OnMenuSceneLoadedFresh(ScenesTransitionSetupDataSO scenes)
        {
            Log("Menu Scene Loaded Fresh!");
            this.MultiplexerInstance.OnLogin -= this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnLogin += this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnJoinChannel -= this.MultiplexerInstance_OnJoinChannel;
            this.MultiplexerInstance.OnJoinChannel += this.MultiplexerInstance_OnJoinChannel;
            this.MultiplexerInstance.OnTextMessageReceived -= RequestBot.Instance.RecievedMessages;
            this.MultiplexerInstance.OnTextMessageReceived += RequestBot.Instance.RecievedMessages;
            this.MixerService = this.MultiplexerInstance.GetMixerService();
            this.TwitchService = this.MultiplexerInstance.GetTwitchService();

            // setup settings ui
            BSMLSettings.instance.AddSettingsMenu("SRM V2", "SongRequestManagerV2.Views.SongRequestManagerSettings.bsml", SongRequestManagerSettings.instance);

            try {
                // main load point
                RequestBot.OnLoad();
            }
            catch (Exception e) {
                Log($"{e}");
            }
            RequestBotConfig.Save(true);
            Log("end Menu Scene Loaded Fresh!");
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
        }
    }
}
