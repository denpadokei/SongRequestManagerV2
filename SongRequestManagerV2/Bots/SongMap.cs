using SongRequestManagerV2.SimpleJSON;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Generic;
using Zenject;
using System.Linq;
using SongRequestManagerV2.Statics;

namespace SongRequestManagerV2.Bots
{
    public class SongMap
    {
        [Inject]
        private readonly StringNormalization _normalize;
        [Inject]
        private readonly IChatManager _chatManager;

        public JSONObject SongObject { get; set; }
        public JSONObject SongVersion { get; private set; }
        public JSONObject SRMInfo => SongObject["srm_info"].AsObject;
        public string Path { get; set; }
        public string LevelId { get; set; }
        public float PP { get; set; } = 0;

        public static int HashCount { get; private set; } = 0;

        private void IndexFields(bool Add, int id, params string[] parameters)
        {
            foreach (var field in parameters) {
                var parts = this._normalize.Split(field);
                foreach (var part in parts) {
                    if (part.Length < RequestBot.partialhash)
                        this.UpdateSearchEntry(part, id, Add);
                    for (var i = RequestBot.partialhash; i <= part.Length; i++) {
                        this.UpdateSearchEntry(part.Substring(0, i), id, Add);
                    }
                }
            }
        }

        private void UpdateSearchEntry(string key, int id, bool Add = true)
        {

            if (Add)
                HashCount++;
            else
                HashCount--;

            if (Add)
                MapDatabase.SearchDictionary.AddOrUpdate(key, (k) =>
                {
                    var va = new HashSet<int>
                    {
                        id
                    };
                    return va;
                }, (k, va) => { va.Add(id); return va; });
            else {
                MapDatabase.SearchDictionary[key].Remove(id); // An empty keyword is fine, and actually uncommon
            }

        }

        public SongMap(JSONObject song, string levelId = "", string path = "")
        {
            this.SongObject = song;
            var versions = this.SongObject["versions"].AsArray.Children.FirstOrDefault(x => x["state"].Value == MapStatus.Published.ToString());
            if (versions == null) {
                this.SongVersion = new JSONObject();
            }
            else {
                this.SongVersion = this.SongObject["versions"].AsArray.Children.FirstOrDefault(x => x["state"].Value == MapStatus.Published.ToString()).AsObject;
            }
            this.LevelId = levelId;
            this.Path = path;
        }

        [Inject]
        private void Constractor()
        {
            if (!this.SongObject["srm_info"].IsObject) {
                var srmJson = new JSONObject();
                srmJson.Add("id", this.SongObject["id"].Value);
                srmJson.Add("key", this.SongObject["id"].Value);
                var metadata = this.SongObject["metadata"];
                srmJson.Add("songName", metadata["songName"].Value);
                srmJson.Add("songSubName", metadata["songSubName"].Value);
                srmJson.Add("songAuthorName", metadata["songAuthorName"].Value);
                srmJson.Add("levelAuthorName", metadata["levelAuthorName"].Value);
                srmJson.Add("rating", new JSONNumber(this.SongObject["stats"]["score"].AsFloat * 100));

                var degrees90 = false;
                var degrees360 = false;

                try {
                    var diffs = this.SongVersion["diffs"].AsArray;
                    var maxnjs = 0d;
                    foreach (var diff in diffs) {
                        var chara = diff.Value["characteristic"].Value;
                        if (chara.Equals("_360Degree", StringComparison.InvariantCultureIgnoreCase)) {
                            degrees360 = true;
                        }
                        if (chara.Equals("_90Degree", StringComparison.InvariantCultureIgnoreCase)) {
                            degrees90 = true;
                        }
                        var seconds = diff.Value["seconds"].AsDouble;
                        var njs = diff.Value["njs"].AsFloat;
                        if (njs > maxnjs) {
                            maxnjs = njs;
                        }
                        if (seconds > 0) {
                            srmJson.Add("songlength", $"{(int)(seconds / 60)}:{seconds % 60:00}");
                            srmJson.Add("songduration", seconds);
                        }
                    }
                    if (maxnjs > 0) {
                        srmJson.Add("njs", maxnjs);
                    }
                    if (degrees360 || degrees90) {
                        srmJson.Add("maptype", "360");
                    }
                    if (MapDatabase.PPMap.TryGetValue(this.SongObject["id"].Value, out var songpp)) {
                        srmJson.Add("pp", songpp);
                    }

                    this.SongObject.Add("srm_info", srmJson);
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
            }
            this.IndexSong(this.SongObject);
        }

        private void UnIndexSong(int id)
        {
            var indexpp = (this.SongObject["pp"].AsFloat > 0) ? "PP" : "";

            this.IndexFields(false, id, this.SRMInfo["songName"].Value, this.SRMInfo["songSubName"].Value, this.SRMInfo["songAuthorName"].Value, this.SRMInfo["levelAuthorName"].Value, indexpp, this.SRMInfo["maptype"].Value);

            MapDatabase.MapLibrary.TryRemove(this.SongObject["id"].Value, out _);
        }

        public void IndexSong(JSONObject song)
        {
            try {
                if (!song["srm_info"].IsObject) {
                    return;
                }
                var info = song["srm_info"].AsObject;
                var indexpp = (info["pp"].AsFloat > 0) ? "PP" : "";

                var id = info["id"];

                //Instance.QueueChatMessage($"id={song["id"].Value} = {id}");

                this.IndexFields(true, id, info["songName"].Value, info["songSubName"].Value, info["songAuthorName"].Value, info["levelAuthorName"], indexpp, info["maptype"].Value);

                if (!string.IsNullOrEmpty(info["id"].Value)) {
                    MapDatabase.MapLibrary.AddOrUpdate(info["id"].Value, this, (key, value) => this);
                }
            }
            catch (Exception ex) {
                this._chatManager.QueueChatMessage(ex.ToString());
            }
        }

        public class SongMapFactory : PlaceholderFactory<JSONObject, string, string, SongMap>
        {
            /// <summary>
            /// Create songmap.
            /// </summary>
            /// <param name="param1">Song</param>
            /// <param name="param2">Level ID</param>
            /// <param name="param3">Path</param>
            /// <returns></returns>
            public override SongMap Create(JSONObject param1, string param2 = "", string param3 = "") => base.Create(param1, param2, param3);
        }
    }
}
