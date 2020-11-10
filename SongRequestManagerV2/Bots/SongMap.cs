using ChatCore.Utilities;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class SongMap
    {
        [Inject]
        StringNormalization _normalize;
        [Inject]
        IChatManager _chatManager;

        public JSONObject Song { get; set; }
        public string Path { get; set; }
        public string LevelId { get; set; }
        public float PP { get; set; } = 0;

        public static int HashCount { get; private set; } = 0;

        void IndexFields(bool Add, int id, params string[] parameters)
        {
            foreach (var field in parameters) {
                string[] parts = _normalize.Split(field);
                foreach (var part in parts) {
                    if (part.Length < RequestBot.partialhash) UpdateSearchEntry(part, id, Add);
                    for (int i = RequestBot.partialhash; i <= part.Length; i++) {
                        UpdateSearchEntry(part.Substring(0, i), id, Add);
                    }
                }
            }
        }

        void UpdateSearchEntry(string key, int id, bool Add = true)
        {

            if (Add) HashCount++; else HashCount--;

            if (Add)
                MapDatabase.SearchDictionary.AddOrUpdate(key, (k) => {
                    HashSet<int> va = new HashSet<int>
                    {
                        id
                    }; return va; }, (k, va) => { va.Add(id); return va; });
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

                bool degrees90 = false;
                bool degrees360 = false;

                try {

                    var characteristics = metadata["characteristics"][0]["difficulties"];
                    foreach (var entry in metadata["characteristics"]) {
                        if (entry.Value["name"] == "360Degree") degrees360 = true;
                        if (entry.Value["name"] == "90Degree") degrees90 = true;
                    }

                    int maxnjs = 0;
                    foreach (var entry in characteristics) {
                        if (entry.Value.IsNull) continue;
                        var diff = entry.Value["length"].AsInt;
                        var njs = entry.Value["njs"].AsInt;
                        if (njs > maxnjs) maxnjs = njs;



                        if (diff > 0) {
                            this.Song.Add("songlength", $"{diff / 60}:{diff % 60:00}");
                            this.Song.Add("songduration", diff);
                            //Instance.QueueChatMessage($"{diff / 60}:{diff % 60}");
                        }
                    }

                    if (maxnjs > 0) {
                        this.Song.Add("njs", maxnjs);
                    }
                    if (degrees360 || degrees90) this.Song.Add("maptype", "360");
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
            }

            if (RequestBot.PPmap.TryGetValue(this.Song["id"].Value, out var songpp)) {
                this.Song.Add("pp", songpp);
            }
            IndexSong(this.Song);
        }

        void UnIndexSong(int id)
        {
            string indexpp = (Song["pp"].AsFloat > 0) ? "PP" : "";

            IndexFields(false, id, Song["songName"].Value, Song["songSubName"].Value, Song["authorName"].Value, Song["levelAuthor"].Value, indexpp, Song["maptype"].Value);

            MapDatabase.MapLibrary.TryRemove(Song["id"].Value, out _);
            MapDatabase.MapLibrary.TryRemove(Song["version"].Value, out _);
            //MapDatabase.LevelId.TryRemove(LevelId, out temp);
        }

        public void IndexSong(JSONObject song)
        {
            try {
                string indexpp = (song["pp"].AsFloat > 0) ? "PP" : "";

                int id = int.Parse(song["id"].Value.ToUpper(), System.Globalization.NumberStyles.HexNumber);

                //Instance.QueueChatMessage($"id={song["id"].Value} = {id}");

                IndexFields(true, id, song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"], indexpp, song["maptype"].Value);

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
            public override SongMap Create(JSONObject param1, string param2 = "", string param3 = "")
            {
                return base.Create(param1, param2, param3);
            }
        }
    }
}
