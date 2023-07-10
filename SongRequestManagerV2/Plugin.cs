using IPA;
using IPA.Config.Stores;
using IPA.Loader;
using SiraUtil.Zenject;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Installes;
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

        public static string Version => MetaData.HVersion.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public static PluginMetadata MetaData { get; private set; }
        public static IPALogger Logger { get; private set; }
        public bool IsApplicationExiting { get; set; } = false;
        public static Plugin Instance { get; private set; }

        public static string DataPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "UserData", "Song Request ManagerV2");
        [Init]
        public void Init(IPALogger log, IPA.Config.Config config, PluginMetadata meta, Zenjector zenjector)
        {
            Instance = this;
            MetaData = meta;
            Logger = log;
            Logger.Debug("Logger initialized.");
            RequestBotConfig.Instance = config.Generated<RequestBotConfig>();
            zenjector.Install<SRMAppInstaller>(Location.App);
            zenjector.Install<SRMMenuInstaller>(Location.Menu);
            zenjector.Install<SRMGameInstaller>(Location.Player);
        }

        [OnStart]
        public void OnStart()
        {
            if (!Directory.Exists(DataPath)) {
                _ = Directory.CreateDirectory(DataPath);
            }
        }

        [OnExit]
        public void OnExit()
        {
            this.IsApplicationExiting = true;
        }

        [OnEnable]
        public void OnEnabled()
        {

        }
        [OnDisable]
        public void OnDisabled()
        {

        }
    }
}
