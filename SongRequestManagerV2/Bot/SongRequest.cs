using BeatSaberMarkupLanguage.Attributes;
using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Models.Twitch;
using ChatCore.Utilities;
using HMUI;
using SongCore;
using SongRequestManagerV2.Bases;
using SongRequestManagerV2.Bot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using VRUIControls;
using Zenject;
using static SongRequestManagerV2.RequestBot;

namespace SongRequestManagerV2
{
    public class SongRequest : BindableBase
    {
        [UIComponent("coverImage")]
        public ImageView _coverImage;

        [UIComponent("songNameText")]
        public TextMeshProUGUI _songNameText;

        [UIComponent("authorNameText")]
        public TextMeshProUGUI _authorNameText;

        [Inject]
        RequestBot _bot;

        [Inject]
        DynamicText.DynamicTextFactory _textFactory;

        //[Inject]
        //private PhysicsRaycasterWithCache _physicsRaycaster;

        /// <summary>説明 を取得、設定</summary>
        private string hint_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("hover-hint")]
        public string Hint
        {
            get => this.hint_ ?? "";

            set => this.SetProperty(ref this.hint_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string songName_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("song-name")]
        public string SongName
        {
            get => this.songName_ ?? "";

            set => this.SetProperty(ref this.songName_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string authorName_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("author-name")]
        public string AuthorName
        {
            get => this.authorName_ ?? "";

            set => this.SetProperty(ref this.authorName_, value);
        }

        public JSONObject _song;
        public IChatUser _requestor;
        public DateTime _requestTime;
        public RequestStatus _status;
        public string _requestInfo; // Contains extra song info, Like : Sub/Donation request, Deck pick, Empty Queue pick,Mapper request, etc.

        public string _songName;
        public string _authorName;

        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(4, 4);

        private static readonly ConcurrentDictionary<string, Texture2D> _cachedTextures = new ConcurrentDictionary<string, Texture2D>();

        public SongRequest Init(JSONObject obj)
        {
            this.Init(
                obj["song"].AsObject,
                this.CreateRequester(obj),
                DateTime.FromFileTime(long.Parse(obj["time"].Value)),
                (RequestStatus)Enum.Parse(typeof(RequestStatus),
                obj["status"].Value),
                obj["requestInfo"].Value);
            return this;
        }

        public SongRequest Init(JSONObject song, IChatUser requestor, DateTime requestTime, RequestStatus status = RequestStatus.Invalid, string requestInfo = "")
        {
            this._song = song;
            this._songName = song["songName"].Value;
            this._authorName = song["levelAuthor"].Value;
            this._requestor = requestor;
            this._status = status;
            this._requestTime = requestTime;
            this._requestInfo = requestInfo;
            return this;
        }

        [UIAction("#post-parse")]
        internal void Setup()
        {
            this.SongName = $"{_songName} <size=50%>{_bot.GetRating(ref _song)}";
            this.SetCover();
        }

        [UIAction("selected")]
        private void Selected()
        {
            Plugin.Logger.Debug($"Selected : {this._songName}");
        }

        [UIAction("hovered")]
        private void Hovered()
        {
            Plugin.Logger.Debug($"Hovered : {this._songName}");
        }

        [UIAction("un-selected-un-hovered")]
        private void UnSelectedUnHovered()
        {
            Plugin.Logger.Debug($"UnSelectedUnHovered : {this._songName}");
        }

        private IPreviewBeatmapLevel GetCustomLevel()
        {
            // lookup song from level id
            return Loader.GetLevelByHash(_song["hash"]);
        }

        public void SetCover()
        {
            Dispatcher.RunOnMainThread(async () =>
            {
                try {
                    _coverImage.enabled = false;
                    var dt = _textFactory.Create().AddSong(_song).AddUser(_requestor); // Get basic fields
                    dt.Add("Status", _status.ToString());
                    dt.Add("Info", (_requestInfo != "") ? " / " + _requestInfo : "");
                    dt.Add("RequestTime", _requestTime.ToLocalTime().ToString("hh:mm"));
                    this.AuthorName = dt.Parse(RequestBot.QueueListRow2);
                    this.Hint = dt.Parse(RequestBot.SongHintText);

                    var imageSet = false;

                    if (SongCore.Loader.AreSongsLoaded) {
                        var level = GetCustomLevel();
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
                            var b = await WebClient.DownloadImage($"https://beatsaver.com{url}", System.Threading.CancellationToken.None).ConfigureAwait(true);

                            tex = new Texture2D(2, 2);
                            tex.LoadImage(b);

                            try {
                                _cachedTextures.AddOrUpdate(url, tex, (s, v) => tex);
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
                finally {
                    _coverImage.enabled = true;
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

        public class SongRequestFactory : PlaceholderFactory<SongRequest>
        {

        }
    }
}