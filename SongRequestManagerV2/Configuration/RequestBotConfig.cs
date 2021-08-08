using IPA.Config.Stores;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace SongRequestManagerV2.Configuration
{
    public class RequestBotConfig
    {
        public static RequestBotConfig Instance { get; internal set; }
        public virtual bool AutoOpenRequestQueue { get; set; } = true;
        public virtual bool RequestQueueOpen { get; set; } = true;
        public virtual bool ClearNoFail { get; set; } = false; // Pressing play will automatically attempt to play the song you selected at the highest difficulty level it has
        public virtual int RequestHistoryLimit { get; set; } = 40;
        public virtual int UserRequestLimit { get; set; } = 2;
        public virtual int SubRequestLimit { get; set; } = 5;
        public virtual int ModRequestLimit { get; set; } = 10;
        public virtual int VipBonusRequests { get; set; } = 1; // VIP's get bonus requests in addition to their base limit *IMPLEMENTED*
        public virtual int SessionResetAfterXHours { get; set; } = 6; // Number of hours before persistent session properties are reset (ie: Queue, Played , Duplicate List)
        public virtual bool LimitUserRequestsToSession { get; set; } = false; // Request limits do not reset after a song is played.  
        public virtual float LowestAllowedRating { get; set; } = 0; // Lowest allowed song rating to be played 0-100 *IMPLEMENTED*, needs UI
        public virtual float MaximumSongLength { get; set; } = 180; // Maximum song length in minutes
        public virtual float MinimumNJS { get; set; } = 0;
        public virtual int MaxiumScanRange { get; set; } = 5; // How far down the list to scan for new songs
        public virtual bool AutopickFirstSong { get; set; } = false; // Pick the first song that !bsr finds instead of showing a short list. *IMPLEMENTED*, needs UI
        public virtual bool UpdateQueueStatusFiles { get; set; } = true; // Create and update queue list and open/close status files for OBS *IMPLEMENTED*, needs UI
        public virtual int MaximumQueueTextEntries { get; set; } = 8;
        public virtual string BotPrefix { get; set; } = "";
        public virtual bool ModFullRights { get; set; } = false; // Allow moderator full broadcaster rights. Use at own risk!
        public virtual int MaximumQueueMessages { get; set; } = 1;
        public virtual DateTime LastBackup { get; set; } = DateTime.MinValue;
        public virtual string BackupPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "userdata", "backup");
        public virtual bool OfflineMode { get; set; } = false;
        public virtual string OfflinePath { get; set; } = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels");
        public virtual bool LocalSearch { get; set; } = false;
        public virtual bool PPSearch { get; set; } = true;
        public virtual string AdditionalSongPath { get; set; } = "";
        public virtual bool IsStartServer { get; set; } = false;
        public virtual int ReceivePort { get; set; } = 50001;
        public virtual bool IsSendBouyomi { get; set; } = false;
        public virtual int SendPort { get; set; } = 50005;
        public virtual bool PerformanceMode { get; set; } = false;
        public virtual bool NotifySound { get; set; } = false;
        public virtual int SoundVolume { get; set; } = 50;
        // 使ってない設定達 R.I.P
#if false
        public virtual bool PersistentRequestQueue { get; set; } = true;
        public virtual bool AutoplaySong { get; set; } = false; // Pressing play will automatically attempt to play the song you selected at the highest difficulty level it has
        public virtual int MaximumLookupMessages { get; set; } = 1;
        public virtual int PPDeckMiniumumPP { get; set; } = 150; // Minimum PP to add to pp deck
        public virtual string DeckList { get; set; } = "fun hard brutal dance chill";
        public virtual bool AllowModAddClosedQueue { get; set; } = true; // Allow moderator to add songs while queue is closed 
        public virtual bool SendNextSongBeingPlayedtoChat { get; set; } = true; // Enable chat message when you hit play
        public virtual bool SavedatabaseOnNewest { get; set; } = false;
        public virtual string SongdownloadPath { get; set; } = "";
#endif
        public event Action<RequestBotConfig> ConfigChangedEvent;
        /// <summary>
        /// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
        /// </summary>
        public virtual void OnReload()
        {

        }
        /// <summary>
        /// Call this to force BSIPA to update the config file. This is also called by BSIPA if it detects the file was modified.
        /// </summary>
        public virtual void Changed()
        {
            // Do stuff when the config is changed.
            this.ConfigChangedEvent?.Invoke(this);
        }

        /// <summary>
        /// Call this to have BSIPA copy the values from <paramref name="other"/> into this config.
        /// </summary>
        public virtual void CopyFrom(RequestBotConfig other)
        {
            var props = other.GetType().GetProperties();
            foreach (var prop in props) {
                if (prop.Name == nameof(Instance)) {
                    continue;
                }
                var currentProp = this.GetType().GetProperty(prop.Name);
                if (currentProp == null) {
                    continue;
                }
                currentProp.SetValue(this, prop.GetMethod.Invoke(other, null));
            }
        }
    }   
}
