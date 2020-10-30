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
        IRequestBot _bot;
        [Inject]
        StringNormalization _normalize;

        public JSONObject song;
        public string path;
        public string LevelId;
        public float pp = 0;

        public static int hashcount = 0;

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

            if (Add) hashcount++; else hashcount--;

            if (Add)
                MapDatabase.SearchDictionary.AddOrUpdate(key, (k) => { HashSet<int> va = new HashSet<int>(); va.Add(id); return va; }, (k, va) => { va.Add(id); return va; });
            else {
                MapDatabase.SearchDictionary[key].Remove(id); // An empty keyword is fine, and actually uncommon
            }

        }

        public SongMap(string id, string version, string songName, string songSubName, string authorName, string duration, string rating)
        {
            //JSONObject song = new JSONObject();

            //IndexSong(song);
        }


        public SongMap(JSONObject song, string LevelId = "", string path = "")
        {

            if (!song["version"].IsString) {
                //RequestBot.Instance.QueueChatMessage($"{song["key"].Value}: {song["metadata"]}");
                song.Add("id", song["key"]);
                song.Add("version", song["key"]);

                var metadata = song["metadata"];
                song.Add("songName", metadata["songName"].Value);
                song.Add("songSubName", metadata["songSubName"].Value);
                song.Add("authorName", metadata["songAuthorName"].Value);
                song.Add("levelAuthor", metadata["levelAuthorName"].Value);
                song.Add("rating", song["stats"]["rating"].AsFloat * 100);

                bool degrees90 = false;
                bool degrees360 = false;

                try {

                    var characteristics = metadata["characteristics"][0]["difficulties"];

                    //Instance.QueueChatMessage($"{characteristics}");

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
                            song.Add("songlength", $"{diff / 60}:{diff % 60:00}");
                            song.Add("songduration", diff);
                            //Instance.QueueChatMessage($"{diff / 60}:{diff % 60}");
                        }
                    }

                    if (maxnjs > 0) {
                        song.Add("njs", maxnjs);
                    }
                    if (degrees360 || degrees90) song.Add("maptype", "360");
                }
                catch {
                }

            }

            if (RequestBot.ppmap.TryGetValue(song["id"].Value, out var songpp)) {
                song.Add("pp", songpp);
            }

            //SongMap oldmap;
            //if (MapDatabase.MapLibrary.TryGetValue(song["id"].Value,out oldmap))
            //{

            //    if (LevelId == oldmap.LevelId && song["version"].Value == oldmap.song["version"].Value)
            //    {
            //        oldmap.song = song;
            //        return;
            //    }

            //    int id = int.Parse(song["id"].Value.ToUpper(), System.Globalization.NumberStyles.HexNumber);

            //    oldmap.UnIndexSong(id);                    
            //}

            this.path = path;
            //this.LevelId = LevelId;
            IndexSong(song);
        }

        void UnIndexSong(int id)
        {
            string indexpp = (song["pp"].AsFloat > 0) ? "PP" : "";

            IndexFields(false, id, song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"].Value, indexpp, song["maptype"].Value);

            MapDatabase.MapLibrary.TryRemove(song["id"].Value, out _);
            MapDatabase.MapLibrary.TryRemove(song["version"].Value, out _);
            //MapDatabase.LevelId.TryRemove(LevelId, out temp);
        }

        public void IndexSong(JSONObject song)
        {
            try {
                this.song = song;
                string indexpp = (song["pp"].AsFloat > 0) ? "PP" : "";

                int id = int.Parse(song["id"].Value.ToUpper(), System.Globalization.NumberStyles.HexNumber);

                //Instance.QueueChatMessage($"id={song["id"].Value} = {id}");

                IndexFields(true, id, song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"], indexpp, song["maptype"].Value);

                MapDatabase.MapLibrary.TryAdd(song["id"].Value, this);
                MapDatabase.MapLibrary.TryAdd(song["version"].Value, this);
                //MapDatabase.LevelId.TryAdd(LevelId, this);
            }
            catch (Exception ex) {
                _bot.QueueChatMessage(ex.ToString());
            }
        }
    }
}
