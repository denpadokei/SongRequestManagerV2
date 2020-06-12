using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SongRequestManagerV2
{
    public class RequestBotConfig : INotifyPropertyChanged
    {
        private string FilePath = Path.Combine(Plugin.DataPath, "RequestBotSettings.ini");

        private bool _requestQueueOpen;
        public bool RequestQueueOpen
        {
            get => this._requestQueueOpen;

            set => this.SetProperty(ref this._requestQueueOpen, value);
        }
        public bool PersistentRequestQueue { get; set; } = true;

        public bool AutoplaySong { get; set; } = false; // Pressing play will automatically attempt to play the song you selected at the highest difficulty level it has
        public bool ClearNoFail { get; set; } = true; // Pressing play will automatically attempt to play the song you selected at the highest difficulty level it has

        public int RequestHistoryLimit { get; set; } = 40;
        public int UserRequestLimit { get; set; } = 2;
        public int SubRequestLimit { get; set; } = 5;
        public int ModRequestLimit { get; set; } = 10;
        public int VipBonusRequests { get; set; } = 1; // VIP's get bonus requests in addition to their base limit *IMPLEMENTED*
        public int SessionResetAfterXHours { get; set; } = 6; // Number of hours before persistent session properties are reset (ie: Queue, Played , Duplicate List)
        public bool LimitUserRequestsToSession { get; set; } = false; // Request limits do not reset after a song is played.  

        public float LowestAllowedRating { get; set; } = 0; // Lowest allowed song rating to be played 0-100 *IMPLEMENTED*, needs UI
        public float MaximumSongLength { get; set; } = 180; // Maximum song length in minutes
        public float MinimumNJS { get; set; } = 0;

        public int MaxiumScanRange { get; set; } = 5; // How far down the list to scan for new songs

        public int PPDeckMiniumumPP { get; set; } = 150; // Minimum PP to add to pp deck

        public string DeckList { get; set; } = "fun hard brutal dance chill";

        public bool AutopickFirstSong { get; set; } = false; // Pick the first song that !bsr finds instead of showing a short list. *IMPLEMENTED*, needs UI
        public bool AllowModAddClosedQueue { get; set; } = true; // Allow moderator to add songs while queue is closed 
        public bool SendNextSongBeingPlayedtoChat { get; set; } = true; // Enable chat message when you hit play
        public bool UpdateQueueStatusFiles { get; set; } = true; // Create and update queue list and open/close status files for OBS *IMPLEMENTED*, needs UI
        public int MaximumQueueTextEntries { get; set; } = 8;
        public string BotPrefix { get; set; } = "";

        public bool ModFullRights { get; set; } = false; // Allow moderator full broadcaster rights. Use at own risk!

        public int maximumqueuemessages { get; set; } = 1;
        public int maximumlookupmessages { get; set; } = 1;

        public string LastBackup { get; set; } = DateTime.MinValue.ToString();
        public string backuppath { get; set; } = Path.Combine(Environment.CurrentDirectory, "userdata", "backup");

        public bool OfflineMode { get; set; } = false;
        public bool SavedatabaseOnNewest { get; set; } = false;
        public string offlinepath { get; set; } = "d:\\customsongs";

        public bool LocalSearch { get; set; } = false;
        public bool PPSearch { get; set; } = false;
        public string additionalsongpath { get; set; } = "";
        public string songdownloadpath { get; set; } = "";

        public string MixerUserName {get;set;} = "";

        public event Action<RequestBotConfig> ConfigChangedEvent;
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        private static RequestBotConfig _instance = null;
        public static RequestBotConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new RequestBotConfig();
                return _instance;
            }

            private set
            {
                _instance = value;
            }
        }

        public RequestBotConfig()
        {
            Instance = this;

            _configWatcher = new FileSystemWatcher();

            Task.Run(() =>
            {
                while (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                    Thread.Sleep(100);

                Plugin.Log("FilePath exists! Continuing initialization!");

                if (File.Exists(FilePath))
                {
                    Load();
                }
                Save();

                _configWatcher.Path = Path.GetDirectoryName(FilePath);
                _configWatcher.NotifyFilter = NotifyFilters.LastWrite;
                _configWatcher.Filter = $"RequestBotSettings.ini";
                _configWatcher.EnableRaisingEvents = true;

                _configWatcher.Changed += ConfigWatcherOnChanged;
            });

            this.RequestQueueOpen = true;
        }

        ~RequestBotConfig()
        {
            _configWatcher.Changed -= ConfigWatcherOnChanged;
        }

        public void Load()
        {
            ConfigSerializer.LoadConfig(this, FilePath);
        }

        public void Save(bool callback = false)
        {
            if (!callback)
                _saving = true;
            ConfigSerializer.SaveConfig(this, FilePath);
        }

        private void ConfigWatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            if (_saving)
            {
                _saving = false;
                return;
            }

            Load();

            ConfigChangedEvent?.Invoke(this);
        }


        #region Prism
        /// <summary>
        /// Checks if a property already matches a desired value. Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners. This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;

            storage = value;
            RaisePropertyChanged(propertyName);

            return true;
        }

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners. This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        protected void RaisePropertyChanged([CallerMemberName]string propertyName = null)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="args">The PropertyChangedEventArgs</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            PropertyChanged?.Invoke(this, args);
        }
        #endregion
    }
}
