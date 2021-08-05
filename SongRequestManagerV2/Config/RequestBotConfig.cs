﻿using System;
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
        private readonly string FilePath = Path.Combine(Plugin.DataPath, "RequestBotSettings.ini");

        private bool _requestQueueOpen;
        public bool RequestQueueOpen
        {
            get => this._requestQueueOpen;

            set => this.SetProperty(ref this._requestQueueOpen, value);
        }
        public bool PersistentRequestQueue = true;

        public bool AutoplaySong = false; // Pressing play will automatically attempt to play the song you selected at the highest difficulty level it has
        public bool ClearNoFail = false; // Pressing play will automatically attempt to play the song you selected at the highest difficulty level it has

        public int RequestHistoryLimit = 40;
        public int UserRequestLimit = 2;
        public int SubRequestLimit = 5;
        public int ModRequestLimit = 10;
        public int VipBonusRequests = 1; // VIP's get bonus requests in addition to their base limit *IMPLEMENTED*
        public int SessionResetAfterXHours = 6; // Number of hours before persistent session properties are reset (ie: Queue, Played , Duplicate List)
        public bool LimitUserRequestsToSession = false; // Request limits do not reset after a song is played.  

        public float LowestAllowedRating = 0; // Lowest allowed song rating to be played 0-100 *IMPLEMENTED*, needs UI
        public float MaximumSongLength = 180; // Maximum song length in minutes
        public float MinimumNJS = 0;

        public int MaxiumScanRange = 5; // How far down the list to scan for new songs

        public int PPDeckMiniumumPP = 150; // Minimum PP to add to pp deck

        public string DeckList = "fun hard brutal dance chill";

        public bool AutopickFirstSong = false; // Pick the first song that !bsr finds instead of showing a short list. *IMPLEMENTED*, needs UI
        public bool AllowModAddClosedQueue = true; // Allow moderator to add songs while queue is closed 
        public bool SendNextSongBeingPlayedtoChat = true; // Enable chat message when you hit play
        public bool UpdateQueueStatusFiles = true; // Create and update queue list and open/close status files for OBS *IMPLEMENTED*, needs UI
        public int MaximumQueueTextEntries = 8;
        public string BotPrefix = "";

        public bool ModFullRights = false; // Allow moderator full broadcaster rights. Use at own risk!

        public int maximumqueuemessages = 1;
        public int maximumlookupmessages = 1;

        public string LastBackup = DateTime.MinValue.ToString();
        public string backuppath = Path.Combine(Environment.CurrentDirectory, "userdata", "backup");

        public bool OfflineMode = false;
        public bool SavedatabaseOnNewest = false;
        public string offlinepath = "d:\\customsongs";

        public bool LocalSearch = false;
        public bool PPSearch = true;
        public string additionalsongpath = "";
        public string songdownloadpath = "";
        public string LocalUserName = "";

        public bool IsStartServer = false;
        public string ReceivePort = "50001";
        public bool IsSendBouyomi = false;
        public string SendPort = "50005";

        public bool PerformanceMode = false;

        public bool NotifySound = false;
        public int SoundVolume = 50;

        public event Action<RequestBotConfig> ConfigChangedEvent;
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        public static RequestBotConfig Instance { get; } = new RequestBotConfig();

        private RequestBotConfig()
        {
            //Instance = this;

            this._configWatcher = new FileSystemWatcher();

            Task.Run(() =>
            {
                while (!Directory.Exists(Path.GetDirectoryName(this.FilePath)))
                    Thread.Sleep(100);

                Logger.Debug("FilePath exists! Continuing initialization!");

                if (File.Exists(this.FilePath)) {
                    this.Load();
                }
                this.Save();

                this._configWatcher.Path = Path.GetDirectoryName(this.FilePath);
                this._configWatcher.NotifyFilter = NotifyFilters.LastWrite;
                this._configWatcher.Filter = $"RequestBotSettings.ini";
                this._configWatcher.EnableRaisingEvents = true;

                this._configWatcher.Changed += this.ConfigWatcherOnChanged;
            });

            this.RequestQueueOpen = true;
        }

        ~RequestBotConfig()
        {
            this._configWatcher.Changed -= this.ConfigWatcherOnChanged;
        }

        public void Load() => ConfigSerializer.LoadConfig(this, this.FilePath);

        public void Save(bool callback = false)
        {
            try {
                if (!callback)
                    this._saving = true;
                ConfigSerializer.SaveConfig(this, this.FilePath);
            }
            catch (Exception e) {
                Logger.Debug($"faild to save : {e}\r\n{e.Message}");
            }
        }

        private void ConfigWatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            if (this._saving) {
                this._saving = false;
                return;
            }

            this.Load();

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
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            this.RaisePropertyChanged(propertyName);

            return true;
        }

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners. This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) => this.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="args">The PropertyChangedEventArgs</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);
        #endregion
    }
}
