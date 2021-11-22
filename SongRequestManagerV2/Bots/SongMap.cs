using SongRequestManagerV2.SimpleJSON;
using SongRequestManagerV2.Statics;
using System;
using System.Linq;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class SongMap : IEquatable<SongMap>, IComparable<SongMap>
    {
        public JSONObject SongObject { get; set; }
        public JSONObject SongVersion { get; private set; }
        public JSONObject SRMInfo => this.SongObject["srm_info"].AsObject;
        public string Path { get; set; }
        public string LevelId { get; set; }
        public float PP { get; set; } = 0;

        [Inject]
        public SongMap(JSONObject song, string levelId, string path, MapDatabase mapDatabase)
        {
            this.SongObject = song;
            var versions = this.SongObject["versions"].AsArray.Children.FirstOrDefault(x => x["state"].Value == MapStatus.Published.ToString());
            if (versions == null) {
                this.SongVersion = this.SongObject["versions"].AsArray.Children.OrderBy(x => DateTime.Parse(x["createdAt"])).LastOrDefault().AsObject;
            }
            else {
                this.SongVersion = this.SongObject["versions"].AsArray.Children.FirstOrDefault(x => x["state"].Value == MapStatus.Published.ToString()).AsObject;
            }
            if (string.IsNullOrEmpty(levelId)) {
                this.LevelId = this.SongObject["id"].Value;
            }
            else {
                this.LevelId = levelId;
            }
            this.Path = path;

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
                    if (mapDatabase.PPMap.TryGetValue(this.SongObject["id"].Value, out var songpp)) {
                        srmJson.Add("pp", songpp);
                    }

                    this.SongObject.Add("srm_info", srmJson);
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
            }
        }
        public bool Equals(SongMap other)
        {
            if (other == null) {
                return false;
            }
            return string.Equals(other.SongVersion["hash"].Value, this.SongVersion["hash"].Value, StringComparison.InvariantCultureIgnoreCase);
        }
        public int CompareTo(SongMap other)
        {
            return string.Compare(other.SRMInfo["id"].Value, this.SRMInfo["id"].Value, true);
        }
        public override int GetHashCode()
        {
            return this.SongVersion["hash"].Value.ToUpper().GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (obj is SongMap map) {
                return this.Equals(map);
            }
            else {
                return base.Equals(obj);
            }
        }
        public class SongMapFactory : PlaceholderFactory<JSONObject, string, string, SongMap>
        {
        }
    }
}
