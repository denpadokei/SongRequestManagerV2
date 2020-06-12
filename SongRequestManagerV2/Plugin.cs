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
using ChatCore.Models.Mixer;
using ChatCore.Models.Twitch;

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
        public TwitchChannel TwitchChannel { get; internal set; }
        public MixerService MixerService { get; internal set; }
        public MixerChannel MixerChannel { get; internal set; }

        internal static WebClient WebClient;

        public bool IsAtMainMenu = true;
        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }

        private readonly RequestBotConfig RequestBotConfig = new RequestBotConfig();

        public static string DataPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "UserData", "StreamCore");
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
            this.CoreInstance = ChatCoreInstance.Create();
            this.MultiplexerInstance = this.CoreInstance.RunAllServices();
            this.MultiplexerInstance.OnLogin += this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnJoinChannel += this.MultiplexerInstance_OnJoinChannel;
            this.MultiplexerInstance.OnTextMessageReceived += RequestBot.Instance.RecievedMessages;
            //if (Instance != null) return;
            //Instance = this;

            Dispatcher.Initialize();

            // create our internal webclient
            WebClient = new WebClient();

            SongBrowserPluginPresent = IPA.Loader.PluginManager.GetPlugin("Song Browser") != null;

            // setup handle for fresh menu scene changes
            BSEvents.OnLoad();
            BSEvents.menuSceneLoadedFresh += OnMenuSceneLoadedFresh;

            // keep track of active scene
            BSEvents.menuSceneActive += () => { IsAtMainMenu = true; };
            BSEvents.gameSceneActive += () => { IsAtMainMenu = false; };

            // init sprites
            Base64Sprites.Init();
        }

        private void MultiplexerInstance_OnJoinChannel(IChatService arg1, IChatChannel arg2)
        {
            if (arg1 is MixerService mixerService) {
                this.MixerChannel = arg2 as MixerChannel;
                
            }
            else if (arg1 is TwitchService twitchService) {
                this.TwitchChannel = arg2 as TwitchChannel;
            }
            
        }

        private void MultiplexerInstance_OnLogin(IChatService obj)
        {
            if (obj is MixerService mixerService) {
                this.MixerService = mixerService;
            }
            else if (obj is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }

        private void OnMenuSceneLoadedFresh()
        {
            // setup settings ui
            BSMLSettings.instance.AddSettingsMenu("SRM", "SongRequestManagerV2.Views.SongRequestManagerSettings.bsml", SongRequestManagerSettings.instance);

            // main load point
            RequestBot.OnLoad();
            RequestBotConfig.Save(true);
        }

        public static void SongBrowserCancelFilter()
        {
            if (SongBrowserPluginPresent) {
                var _songBrowserUI = SongBrowser.SongBrowserApplication.Instance.GetField<SongBrowser.UI.SongBrowserUI, SongBrowser.SongBrowserApplication>("_songBrowserUI");
                if (_songBrowserUI) {
                    if (_songBrowserUI.Model.Settings.filterMode != SongBrowser.DataAccess.SongFilterMode.None && _songBrowserUI.Model.Settings.sortMode != SongBrowser.DataAccess.SongSortMode.Original) {
                        _songBrowserUI.CancelFilter();
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
