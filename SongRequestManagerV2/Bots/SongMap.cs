using ChatCore.Utilities;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Generic;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class SongMap
    {
        [Inject]
        private readonly StringNormalization _normalize;
        [Inject]
        private readonly IChatManager _chatManager;

        public JSONObject Song { get; set; }
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
            this.Song = song;
            this.LevelId = levelId;
            this.Path = path;
        }

        [Inject]
        private void Constractor()
        {
            if (!this.Song["version"].IsString) {
                this.Song.Add("id", this.Song["key"]);
                this.Song.Add("version", this.Song["key"]);

                var metadata = this.Song["metadata"];
                this.Song.Add("songName", metadata["songName"].Value);
                this.Song.Add("songSubName", metadata["songSubName"].Value);
                this.Song.Add("authorName", metadata["songAuthorName"].Value);
                this.Song.Add("levelAuthor", metadata["levelAuthorName"].Value);
                this.Song.Add("rating", this.Song["stats"]["rating"].AsFloat * 100);

                var degrees90 = false;
                var degrees360 = false;

                try {

                    var characteristics = metadata["characteristics"][0]["difficulties"];
                    foreach (var entry in metadata["characteristics"]) {
                        if (entry.Value["name"] == "360Degree")
                            degrees360 = true;
                        if (entry.Value["name"] == "90Degree")
                            degrees90 = true;
                    }

                    var maxnjs = 0;
                    foreach (var entry in characteristics) {
                        if (entry.Value.IsNull)
                            continue;
                        var diff = entry.Value["length"].AsInt;
                        var njs = entry.Value["njs"].AsInt;
                        if (njs > maxnjs)
                            maxnjs = njs;



                        if (diff > 0) {
                            this.Song.Add("songlength", $"{diff / 60}:{diff % 60:00}");
                            this.Song.Add("songduration", diff);
                            //Instance.QueueChatMessage($"{diff / 60}:{diff % 60}");
                        }
                    }

                    if (maxnjs > 0) {
                        this.Song.Add("njs", maxnjs);
                    }
                    if (degrees360 || degrees90)
                        this.Song.Add("maptype", "360");
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
            }

            if (MapDatabase.PPMap.TryGetValue(this.Song["id"].Value, out var songpp)) {
                this.Song.Add("pp", songpp);
            }
            this.IndexSong(this.Song);
        }

        private void UnIndexSong(int id)
        {
            var indexpp = (this.Song["pp"].AsFloat > 0) ? "PP" : "";

            this.IndexFields(false, id, this.Song["songName"].Value, this.Song["songSubName"].Value, this.Song["authorName"].Value, this.Song["levelAuthor"].Value, indexpp, this.Song["maptype"].Value);

            MapDatabase.MapLibrary.TryRemove(this.Song["id"].Value, out _);
            MapDatabase.MapLibrary.TryRemove(this.Song["version"].Value, out _);
            //MapDatabase.LevelId.TryRemove(LevelId, out temp);
        }

        public void IndexSong(JSONObject song)
        {
            try {
                var indexpp = (song["pp"].AsFloat > 0) ? "PP" : "";

                var id = int.Parse(song["id"].Value.ToUpper(), System.Globalization.NumberStyles.HexNumber);

                //Instance.QueueChatMessage($"id={song["id"].Value} = {id}");

                this.IndexFields(true, id, song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"], indexpp, song["maptype"].Value);

                if (string.IsNullOrEmpty(song["version"].Value)) {
                    MapDatabase.MapLibrary.AddOrUpdate(song["id"].Value, this, (key, value) => this);
                }
                else {
                    MapDatabase.MapLibrary.AddOrUpdate(song["version"].Value, this, (key, value) => this);
                }
                //MapDatabase.LevelId.TryAdd(LevelId, this);
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
