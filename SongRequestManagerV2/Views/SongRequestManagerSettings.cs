using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using SongRequestManagerV2.Configuration;
using Zenject;

namespace SongRequestManagerV2.Views
{
    [HotReload]
    public class SongRequestManagerSettings : BSMLAutomaticViewController, IInitializable
    {
        public string ResourceName
        {
            get
            {
                return "SongRequestManagerV2.Views.SongRequestManagerSettings.bsml";
            }
        }

        [UIValue("autopick-first-song")]
        public bool AutopickFirstSong
        {
            get
            {
                return RequestBotConfig.Instance.AutopickFirstSong;
            }

            set
            {
                RequestBotConfig.Instance.AutopickFirstSong = value;
            }
        }
        [UIValue("clear-nofail")]
        public bool ClearNofail
        {
            get
            {
                return RequestBotConfig.Instance.ClearNoFail;
            }

            set
            {
                RequestBotConfig.Instance.ClearNoFail = value;
            }
        }

        [UIValue("lowest-allowed-rating")]
        public float LowestAllowedRating
        {
            get
            {
                return RequestBotConfig.Instance.LowestAllowedRating;
            }

            set
            {
                RequestBotConfig.Instance.LowestAllowedRating = value;
            }
        }

        [UIValue("maximum-song-length")]
        public int MaximumSongLength
        {
            get
            {
                return (int)RequestBotConfig.Instance.MaximumSongLength;
            }

            set
            {
                RequestBotConfig.Instance.MaximumSongLength = value;
            }
        }

        [UIValue("minimum-njs")]
        public int MinimumNJS
        {
            get
            {
                return (int)RequestBotConfig.Instance.MinimumNJS;
            }

            set
            {
                RequestBotConfig.Instance.MinimumNJS = value;
            }
        }

        [UIValue("tts-support")]
        public bool TtsSupport
        {
            get
            {
                return !string.IsNullOrEmpty(RequestBotConfig.Instance.BotPrefix);
            }

            set
            {
                RequestBotConfig.Instance.BotPrefix = value ? "! " : "";
            }
        }

        [UIValue("user-request-limit")]
        public int UserRequestLimit
        {
            get
            {
                return RequestBotConfig.Instance.UserRequestLimit;
            }

            set
            {
                RequestBotConfig.Instance.UserRequestLimit = value;
            }
        }

        [UIValue("sub-request-limit")]
        public int SubRequestLimit
        {
            get
            {
                return RequestBotConfig.Instance.SubRequestLimit;
            }

            set
            {
                RequestBotConfig.Instance.SubRequestLimit = value;
            }
        }

        [UIValue("mod-request-limit")]
        public int ModRequestLimit
        {
            get
            {
                return RequestBotConfig.Instance.ModRequestLimit;
            }

            set
            {
                RequestBotConfig.Instance.ModRequestLimit = value;
            }
        }

        [UIValue("vip-bonus-requests")]
        public int VipBonusRequests
        {
            get
            {
                return RequestBotConfig.Instance.VipBonusRequests;
            }

            set
            {
                RequestBotConfig.Instance.VipBonusRequests = value;
            }
        }

        [UIValue("mod-full-rights")]
        public bool ModFullRights
        {
            get
            {
                return RequestBotConfig.Instance.ModFullRights;
            }

            set
            {
                RequestBotConfig.Instance.ModFullRights = value;
            }
        }

        [UIValue("limit-user-requests-to-session")]
        public bool LimitUserRequestsToSession
        {
            get
            {
                return RequestBotConfig.Instance.LimitUserRequestsToSession;
            }

            set
            {
                RequestBotConfig.Instance.LimitUserRequestsToSession = value;
            }
        }

        [UIValue("session-reset-after-xhours")]
        public int SessionResetAfterXHours
        {
            get
            {
                return RequestBotConfig.Instance.SessionResetAfterXHours;
            }

            set
            {
                RequestBotConfig.Instance.SessionResetAfterXHours = value;
            }
        }

        [UIValue("performance-mode")]
        public bool PerformanceMode
        {
            get
            {
                return RequestBotConfig.Instance.PerformanceMode;
            }

            set
            {
                RequestBotConfig.Instance.PerformanceMode = value;
            }
        }

        [UIValue("is-sound-enable")]
        public bool IsSoundEnable
        {
            get
            {
                return RequestBotConfig.Instance.NotifySound;
            }

            set
            {
                RequestBotConfig.Instance.NotifySound = value;
            }
        }

        [UIValue("volume")]
        public int Volume
        {
            get
            {
                return RequestBotConfig.Instance.SoundVolume;
            }

            set
            {
                RequestBotConfig.Instance.SoundVolume = value;
            }
        }
        [UIValue("pp-sarch")]
        public bool PPSerch
        {
            get
            {
                return RequestBotConfig.Instance.PPSearch;
            }

            set
            {
                RequestBotConfig.Instance.PPSearch = value;
            }
        }

        public void Initialize()
        {
            BSMLSettings.instance.AddSettingsMenu("SRM V2", this.ResourceName, this);
        }
    }
}
