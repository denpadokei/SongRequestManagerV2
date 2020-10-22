using BeatSaberMarkupLanguage.Attributes;
using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Models.Twitch;
using ChatCore.SimpleJSON;
using HMUI;
using SongPlayListEditer.Bases;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using TMPro;
using UnityEngine;
using static SongRequestManagerV2.RequestBot;

namespace SongRequestManagerV2
{
    public class SongRequest : BindableBase
    {
        [UIComponent("coverImage")]
        public HMUI.ImageView _coverImage;

        [UIComponent("songNameText")]
        public TextMeshProUGUI _songNameText;

        [UIComponent("authorNameText")]
        public TextMeshProUGUI _authorNameText;

        /// <summary>説明 を取得、設定</summary>
        private string hint_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("hover-hint")]
        public string Hint
        {
            get => this.hint_ ?? "";

            set => this.SetProperty(ref this.hint_, value);
        }

        public JSONObject _song;
        public IChatUser _requestor;
        public DateTime _requestTime;
        public RequestStatus _status;
        public string _requestInfo; // Contains extra song info, Like : Sub/Donation request, Deck pick, Empty Queue pick,Mapper request, etc.

        public string _songName;
        public string _authorName;

        private static readonly Dictionary<string, Texture2D> _cachedTextures = new Dictionary<string, Texture2D>();

        public SongRequest(JSONObject obj)
        {
            _requestor = this.CreateRequester(obj);
            _requestTime = DateTime.FromFileTime(long.Parse(obj["time"].Value));
            _status = (RequestStatus)Enum.Parse(typeof(RequestStatus), obj["status"].Value);
            _song = obj["song"].AsObject;
            _requestInfo = obj["requestInfo"].Value;

            this._songName = _song["songName"].Value;
            this._authorName = _song["levelAuthor"].Value;
            _coverImage = new GameObject().AddComponent<ImageView>();
            this.SetCover();
            
        }
        public SongRequest(JSONObject song, IChatUser requestor, DateTime requestTime, RequestStatus status = RequestStatus.Invalid, string requestInfo = "")
        {
            this._song = song;
            this._songName = song["songName"].Value;
            this._authorName = song["levelAuthor"].Value;
            this._requestor = requestor;
            this._status = status;
            this._requestTime = requestTime;
            this._requestInfo = requestInfo;
            _coverImage = new GameObject().AddComponent<ImageView>();
            this.SetCover();
        }

        [UIAction("#post-parse")]
        internal void Setup()
        {
            if (!_songNameText || !_authorNameText) return;
            var filter = _coverImage.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            filter.aspectRatio = 1f;
            filter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.HeightControlsWidth;
            _songNameText.text = _songName;
            _authorNameText.text = _authorName;
            this.SetCover();
        }

        private CustomPreviewBeatmapLevel GetCustomLevel()
        {
            // get level id from hash
            var levelIds = SongCore.Collections.levelIDsForHash(_song["hash"]);
            if (levelIds.Count == 0) return null;

            // lookup song from level id
            return SongCore.Loader.CustomLevels.FirstOrDefault(s => string.Equals(s.Value.levelID, levelIds.First(), StringComparison.OrdinalIgnoreCase)).Value ?? null;
        }

        public void SetCover()
        {
            Dispatcher.RunOnMainThread(async () =>
            {
                try {
                    var dt = new RequestBot.DynamicText().AddSong(_song).AddUser(ref _requestor); // Get basic fields
                    dt.Add("Status", _status.ToString());
                    dt.Add("Info", (_requestInfo != "") ? " / " + _requestInfo : "");
                    dt.Add("RequestTime", _requestTime.ToLocalTime().ToString("hh:mm"));
                    this.Hint = dt.Parse(RequestBot.SongHintText);
                    //songName.text = $"{request._song["songName"].Value} <size=50%>{RequestBot.GetRating(ref request._song)} <color=#3fff3f>{pp}</color></size>"; // NEW VERSION

                    var imageSet = false;

                    if (SongCore.Loader.AreSongsLoaded) {
                        CustomPreviewBeatmapLevel level = GetCustomLevel();
                        if (level != null) {
                            //Plugin.Log("custom level found");
                            // set image from song's cover image
                            var tex = await level.GetCoverImageAsync(System.Threading.CancellationToken.None);
                            _coverImage.sprite = tex;
                            imageSet = true;
                        }
                    }

                    if (!imageSet) {
                        string url = _song["coverURL"].Value;

                        if (!_cachedTextures.TryGetValue(url, out var tex)) {
                            var b = await WebClient.DownloadImage($"https://beatsaver.com{url}", System.Threading.CancellationToken.None);

                            tex = new Texture2D(2, 2);
                            tex.LoadImage(b);

                            try {
                                _cachedTextures.Add(url, tex);
                            }
                            catch (Exception e) {
                                Plugin.Logger.Error(e);
                            }
                        }
                        _coverImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
                catch (Exception e) {
                    Plugin.Logger.Error(e);
                }
            });
        }

        public JSONObject ToJson()
        {
            try {
                var obj = new JSONObject();
                obj.Add("status", new JSONString(_status.ToString()));
                obj.Add("requestInfo", new JSONString(_requestInfo));
                obj.Add("time", new JSONString(_requestTime.ToFileTime().ToString()));
                obj.Add("requestor", _requestor.ToJson());
                obj.Add("song", _song);
                return obj;
            }
            catch (Exception ex) {
                Plugin.Log($"{ex}\r\n{ex.Message}");
                return null;
            }
        }

        private IChatUser CreateRequester(JSONObject obj)
        {
            try {
                var temp = new TwitchUser(obj["requestor"].AsObject.ToString());
                return temp;
            }
            catch (Exception e) {
                Plugin.Log($"{e}");
                return new UnknownChatUser(obj["requestor"].AsObject.ToString());
            }
        }
    }
}