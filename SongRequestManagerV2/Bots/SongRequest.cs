using BeatSaberMarkupLanguage.Attributes;
using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Models.Twitch;
using HMUI;
using SongCore;
using SongRequestManagerV2.Bases;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.SimpleJSON;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;

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
        private readonly DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        private readonly MapDatabase _mapDatabase;

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

        public JSONObject SongNode { get; private set; }
        public JSONObject SongMetaData => this.SongNode["metadata"].AsObject;
        public JSONObject SongVersion { get; private set; }
        public bool IsWIP { get; private set; }
        public IChatUser _requestor;
        public DateTime RequestTime { get; private set; }
        public RequestStatus Status { get; set; }
        public string _requestInfo; // Contains extra song info, Like : Sub/Donation request, Deck pick, Empty Queue pick,Mapper request, etc.
        public string _songName;
        public string _authorName;
        private string _hash;
        private string _coverURL;
        private string _downloadURL;

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
            this.SongNode = song;
            this._songName = this.SongMetaData["songName"].Value;
            this._authorName = this.SongMetaData["levelAuthorName"].Value;
            this._requestor = requestor;
            this.Status = status;
            this.RequestTime = requestTime;
            this._requestInfo = requestInfo;
            var version = this.SongNode["versions"].AsArray.Children.FirstOrDefault(x => x["state"].Value == MapStatus.Published.ToString());
            if (version == null) {
                this.SongVersion = this.SongNode["versions"].AsArray.Children.OrderBy(x => DateTime.Parse(x["createdAt"].Value)).LastOrDefault().AsObject;
                this.IsWIP = true;
            }
            else {
                this.SongVersion = this.SongNode["versions"].AsArray.Children.FirstOrDefault(x => x["state"].Value == MapStatus.Published.ToString()).AsObject;
                this.IsWIP = false;
            }
            this._hash = this.SongVersion["hash"].Value;
            this._coverURL = this.SongVersion["coverURL"].Value;
            this._downloadURL = this.SongVersion["downloadURL"].Value.Replace(RequestBot.BEATMAPS_AS_CDN_ROOT_URL, RequestBot.BEATMAPS_CDN_ROOT_URL);
            if (this._mapDatabase.PPMap.TryGetValue(this.SongNode["id"].Value, out var pp)) {
                this.SongNode.Add("pp", new JSONNumber(pp));
            }
            return this;
        }

        [UIAction("#post-parse")]
        internal void Setup()
        {
            var name = this.IsWIP ? $"<color=\"yellow\">[WIP]</color> {this._songName}" : this._songName;
            if (RequestBotConfig.Instance.PPSearch && this._mapDatabase.PPMap.TryGetValue(this.SongNode["id"].Value, out var pp) && 0 < pp) {
                this.SongName = $"{name} <size=50%>{Utility.GetRating(this.SongNode)} <color=#4169e1>{pp:0.00} PP</color></size>";
            }
            else {
                this.SongName = $"{name} <size=50%>{Utility.GetRating(this.SongNode)}</size>";
            }

            this.SetCover();
        }

        [UIAction("selected")]
        private void Selected() { }

        [UIAction("hovered")]
        private void Hovered() { }

        [UIAction("un-selected-un-hovered")]
        private void UnSelectedUnHovered() { }
        /// <summary>
        /// lookup song from level id
        /// </summary>
        /// <returns></returns>
        private IPreviewBeatmapLevel GetCustomLevel() => Loader.GetLevelByHash(this._hash.ToUpper());

        public void SetCover() => Dispatcher.RunOnMainThread(async () =>
                                {
                                    try {
                                        this._coverImage.enabled = false;
                                        var dt = this._textFactory.Create().AddSong(this.SongNode).AddUser(this._requestor); // Get basic fields
                                        dt.Add("Status", this.Status.ToString());
                                        dt.Add("Info", (this._requestInfo != "") ? " / " + this._requestInfo : "");
                                        dt.Add("RequestTime", this.RequestTime.ToLocalTime().ToString("hh:mm"));
                                        this.AuthorName = dt.Parse(StringFormat.QueueListRow2);
                                        this.Hint = dt.Parse(StringFormat.SongHintText);

                                        var imageSet = false;

                                        if (SongCore.Loader.AreSongsLoaded) {
                                            var level = this.GetCustomLevel();
                                            if (level != null) {
                                                //Logger.Debug("custom level found");
                                                // set image from song's cover image
                                                var tex = await level.GetCoverImageAsync(System.Threading.CancellationToken.None);
                                                this._coverImage.sprite = tex;
                                                imageSet = true;
                                            }
                                        }

                                        if (!imageSet) {
                                            var url = "";
                                            if (!string.IsNullOrEmpty(this._coverURL)) {
                                                url = this._coverURL;
                                            }
                                            else {
                                                url = $"{RequestBot.BEATMAPS_CDN_ROOT_URL}/{this._hash.ToLower()}.jpg";
                                            }
                                            if (!_cachedTextures.TryGetValue(url, out var tex)) {
                                                var b = await WebClient.DownloadImage(url, System.Threading.CancellationToken.None).ConfigureAwait(true);

                                                tex = new Texture2D(2, 2);
                                                tex.LoadImage(b);

                                                try {
                                                    _cachedTextures.AddOrUpdate(url, tex, (s, v) => tex);
                                                }
                                                catch (Exception e) {
                                                    Logger.Error(e);
                                                }
                                            }
                                            this._coverImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                                        }
                                    }
                                    catch (Exception e) {
                                        Logger.Error(e);
                                    }
                                    finally {
                                        this._coverImage.enabled = true;
                                    }
                                });

        public JSONObject ToJson()
        {
            try {
                var obj = new JSONObject();
                obj.Add("status", new JSONString(this.Status.ToString()));
                obj.Add("requestInfo", new JSONString(this._requestInfo));
                obj.Add("time", new JSONString(this.RequestTime.ToFileTime().ToString()));
                obj.Add("requestor", JSON.Parse(this._requestor.ToJson().ToString()));
                obj.Add("song", this.SongNode);
                return obj;
            }
            catch (Exception ex) {
                Logger.Error(ex);
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
                Logger.Error(e);
                return new UnknownChatUser(obj["requestor"].AsObject.ToString());
            }
        }

        public async Task<byte[]> DownloadZip(CancellationToken token = default(CancellationToken), IProgress<double> progress = null)
        {
            try {
                var url = "";
                if (!string.IsNullOrEmpty(this._downloadURL)) {
                    url = this._downloadURL;
                }
                else {
                    url = $"{RequestBot.BEATMAPS_CDN_ROOT_URL}/{this._hash.ToLower()}.zip";
                }
                var response = await WebClient.SendAsync(HttpMethod.Get, url, token, progress);

                if (response?.IsSuccessStatusCode == true) {
                    return response.ContentToBytes();
                }
                return null;
            }
            catch (Exception e) {
                Logger.Error(e);
                return null;
            }
        }

        public class SongRequestFactory : PlaceholderFactory<SongRequest>
        {

        }
    }
}