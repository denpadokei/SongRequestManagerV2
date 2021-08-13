using IPA;
using IPA.Config.Stores;
using IPA.Loader;
using SiraUtil.Zenject;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Installer;
using SongRequestManagerV2.Installers;
using SongRequestManagerV2.Networks;
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
        public bool IsApplicationExiting { get; set; } = false;
        public static Plugin Instance { get; private set; }

        public static string DataPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "UserData", "Song Request ManagerV2");
        [Init]
        public void Init(IPALogger log, IPA.Config.Config config, PluginMetadata meta, Zenjector zenjector)
        {
            Instance = this;
            _meta = meta;
            Logger = log;
            Logger.Debug("Logger initialized.");
            RequestBotConfig.Instance = config.Generated<RequestBotConfig>();
            zenjector.OnApp<SRMAppInstaller>();
            zenjector.OnMenu<SRMMenuInstaller>();
        }

        [OnStart]
        public void OnStart()
        {
            if (!Directory.Exists(DataPath)) {
                Directory.CreateDirectory(DataPath);
            }

        }

        [OnExit]
        public void OnExit() => this.IsApplicationExiting = true;
        [OnEnable]
        public void OnEnabled()
        {

        }
        [OnDisable]
        public void OnDisabled() => BouyomiPipeline.instance.Stop();
    }
}
