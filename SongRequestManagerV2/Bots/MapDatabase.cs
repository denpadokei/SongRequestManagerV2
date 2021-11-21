using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Extentions;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.SimpleJSON;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class MapDatabase
    {
        public ConcurrentDictionary<string, SongMap> MapLibrary { get; } = new ConcurrentDictionary<string, SongMap>();
        public ConcurrentDictionary<string, SongMap> LevelId { get; } = new ConcurrentDictionary<string, SongMap>();
        public ConcurrentDictionary<string, HashSet<string>> SearchDictionary { get; } = new ConcurrentDictionary<string, HashSet<string>>();
        public ConcurrentDictionary<string, float> PPMap { get; } = new ConcurrentDictionary<string, float>();
        private static int tempid = 100000; // For now, we use these for local ID less songs
        public volatile int _hashCount = 0;
        private static bool DatabaseImported = false;
        public bool DatabaseLoading { get; private set; } = false;

        [Inject]
        private IRequestBot Bot { get; }
        [Inject]
        private readonly IChatManager _chatManager;
        [Inject]
        private readonly StringNormalization _normalize;
        [Inject]
        private readonly SongMap.SongMapFactory _songMapFactory;

        // Fast? Full Text Search
        public List<SongMap> Search(string searchKey)
        {
            if (!DatabaseImported && RequestBotConfig.Instance.LocalSearch) {
                this.LoadCustomSongs().Await(null, null, null);
            }

            var result = new List<SongMap>();

            if (this.Bot.GetBeatSaverId(searchKey) != "") {
                if (this.MapLibrary.TryGetValue(this._normalize.RemoveSymbols(searchKey, this._normalize.SymbolsNoDash), out var song)) {
                    result.Add(song);
                    return result;
                }
            }

            var resultlist = new List<HashSet<string>>();

            var SearchParts = this._normalize.Split(searchKey);

            foreach (var part in SearchParts) {
                if (!this.SearchDictionary.TryGetValue(part, out var idset)) {
                    return result; // Keyword must be found
                }

                resultlist.Add(idset);
            }
            if (!resultlist.Any()) {
                return result;
            }
            // We now have n lists of candidates
            resultlist.Sort((L1, L2) => L1.Count.CompareTo(L2.Count));

            // We now have an optimized query
            // Compute all matches

            foreach (var map in resultlist[0]) {
                var contain = true;
                for (var i = 1; i < resultlist.Count; i++) {
                    contain = contain && resultlist[i].Contains(map);
                }
                if (!contain) {
                    continue;
                }
                try {
                    result.Add(this.MapLibrary[map]);
                }
                catch {
                    this._chatManager.QueueChatMessage($"map fail = {map}");
                }
            }
            return result;
        }

        public void IndexSong(SongMap map)
        {
            var song = map.SongObject;
            try {
                if (!song["srm_info"].IsObject) {
                    return;
                }
                var info = song["srm_info"].AsObject;
                var indexpp = (info["pp"].AsFloat > 0) ? "PP" : "";
                var id = info["id"].Value;
                this.IndexFields(true, id, info["songName"].Value, info["songSubName"].Value, info["songAuthorName"].Value, info["levelAuthorName"].Value, indexpp, info["maptype"].Value);

                if (!string.IsNullOrEmpty(info["id"].Value)) {
                    this.MapLibrary.AddOrUpdate(info["id"].Value, map, (key, value) => map);
                }
            }
            catch (Exception ex) {
                this._chatManager.QueueChatMessage(ex.ToString());
            }
        }

        public void UnIndexSong(SongMap map)
        {
            var song = map.SongObject;
            var srmInfo = map.SRMInfo;
            var indexpp = (song["pp"].AsFloat > 0) ? "PP" : "";
            this.IndexFields(false, song["id"].Value, srmInfo["songName"].Value, srmInfo["songSubName"].Value, srmInfo["songAuthorName"].Value, srmInfo["levelAuthorName"].Value, indexpp, srmInfo["maptype"].Value);
            this.MapLibrary.TryRemove(song["id"].Value, out _);
        }

        private void IndexFields(bool Add, string id, params string[] parameters)
        {
            foreach (var field in parameters) {
                var parts = this._normalize.Split(field);
                foreach (var part in parts) {
                    if (part.Length < RequestBot.partialhash) {
                        this.UpdateSearchEntry(part, id, Add);
                    }
                    for (var i = RequestBot.partialhash; i <= part.Length; i++) {
                        this.UpdateSearchEntry(part.Substring(0, i), id, Add);
                    }
                }
            }
        }

        private void UpdateSearchEntry(string key, string id, bool Add = true)
        {

            if (Add) {
                this._hashCount++;
            }
            else {
                this._hashCount--;
            }

            if (Add) {
                this.SearchDictionary.AddOrUpdate(key, (k) =>
                {
                    var va = new HashSet<string>
                    {
                        id
                    };
                    return va;
                },
                (k, va) =>
                {
                    va.Add(id);
                    return va;
                });
            }
            else {
                if (this.SearchDictionary.TryRemove(key, out var result)) {
                    result?.Remove(id); // An empty keyword is fine, and actually uncommon
                }
            }
        }


        public void RemoveMap(JSONObject _)
        {

        }
        public void AddDirectory()
        {

        }

        public void DownloadSongs()
        {

        }

        public void SaveDatabase()
        {
            try {
                var start = DateTime.Now;
                //JSONArray arr = new JSONArray();
                var arr = new JSONObject();
                foreach (var entry in this.MapLibrary) {
                    arr.Add(entry.Value.SongObject["id"], entry.Value.SongObject);
                }

                File.WriteAllText(Path.Combine(Plugin.DataPath, "SongDatabase.dat"), arr.ToString());
                this._chatManager.QueueChatMessage($"Saved Song Databse in  {(DateTime.Now - start).Seconds} secs.");
            }
            catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public void LoadDatabase()
        {
            try {
                var start = DateTime.Now;
                var path = Path.Combine(Plugin.DataPath, "SongDatabase.dat");

                if (File.Exists(path)) {
                    var json = JSON.Parse(File.ReadAllText(path));
                    if (!json.IsNull) {
                        var Count = json.Count;

                        //foreach (JSONObject j in json.AsArray)
                        //{                                    
                        //    new SongMap(j);
                        //}


                        foreach (var kvp in json) {
                            this._songMapFactory.Create((JSONObject)kvp.Value);
                        }
                        json = 0; // BUG: This doesn't actually help. The problem is that the json object is still being referenced.

                        this._chatManager.QueueChatMessage($"Finished reading {Count} in {(DateTime.Now - start).Seconds} secs.");
                    }
                }
            }
            catch (Exception ex) {

                Logger.Error(ex);
                this._chatManager.QueueChatMessage($"{ex}");
            }
        }


        public void ImportLoaderDatabase()
        {
            //foreach (var level in SongLoader.CustomLevels) {
            //    new SongMap(level.customSongInfo.path);
            //}
        }

        public string Readzipjson(ZipArchive archive, string filename = "info.json")
        {
            var info = archive.Entries.First<ZipArchiveEntry>(e => (e.Name.EndsWith(filename)));
            if (info == null) {
                return "";
            }

            var reader = new StreamReader(info.Open());
            var result = reader.ReadToEnd();
            reader.Close();
            return result;
        }


        // Early code... index a full zip archive.
        public async Task LoadZIPDirectory(string folder = "")
        {
            if (this.DatabaseLoading) {
                return;
            }

            if (string.IsNullOrEmpty(folder)) {
                folder = Environment.CurrentDirectory;
            }

            await Task.Run(() =>
            {

                var startingmem = GC.GetTotalMemory(true);

                this._chatManager.QueueChatMessage($"Starting to read archive.");
                var addcount = 0;
                var StarTime = DateTime.Now;

                var di = new DirectoryInfo(folder);

                foreach (var f in di.GetFiles("*.zip")) {

                    try {
                        var x = ZipFile.OpenRead(f.FullName);
                        var info = x.Entries.First<ZipArchiveEntry>(e => (e.Name.EndsWith("info.json")));

                        var id = "";
                        var version = "";
                        this.GetIdFromPath(f.Name, ref id, ref version);

                        if (this.MapLibrary.ContainsKey(id)) {
                            if (this.MapLibrary[id].Path != "") {
                                this.MapLibrary[id].Path = f.FullName;
                            }

                            continue;
                        }

                        var song = JSONObject.Parse(this.Readzipjson(x)).AsObject;

                        string hash;

                        JSONNode difficultylevels = song["difficultyLevels"].AsArray;
                        var FileAccumulator = new StringBuilder();
                        foreach (var level in difficultylevels) {
                            try {
                                FileAccumulator.Append(this.Readzipjson(x, level.Value));
                            }
                            catch {
                                //Instance.QueueChatMessage($"key={level.Key} value={level.Value}");
                                //throw;
                            }
                        }

                        hash = this.Bot.CreateMD5FromString(FileAccumulator.ToString());

                        var levelId = string.Join("∎", hash, song["songName"].Value, song["songSubName"].Value, song["songAuthorName"], song["beatsPerMinute"].AsFloat.ToString()) + "∎";

                        if (this.LevelId.ContainsKey(levelId)) {

                            this.LevelId[levelId].Path = f.FullName;
                            continue;
                        }

                        addcount++;

                        song.Add("id", id);
                        song.Add("hashMd5", hash);

                        this._songMapFactory.Create(song, levelId, f.FullName);

                        x = null;

                    }
                    catch (Exception) {
                        this._chatManager.QueueChatMessage($"Failed to process {f.FullName}");
                        //Instance.QueueChatMessage(ex.ToString());
                    }
                }
                this._chatManager.QueueChatMessage($"Archive indexing done, {addcount} files added. ({(DateTime.Now - StarTime).TotalSeconds} secs.");
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                this._chatManager.QueueChatMessage($"hashentries: {this._hashCount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");


            });


            this.DatabaseLoading = false;
        }


        // Update Database from Directory
        public Task LoadCustomSongs(string folder = "", string songid = "")
        {
            if (this.DatabaseLoading) {
                return Task.CompletedTask;
            }

            this.DatabaseLoading = true;
            return Task.Run(() =>
            {
                if (songid == "") {
                    this._chatManager.QueueChatMessage($"Starting song indexing {folder}");
                }

                var StarTime = DateTime.UtcNow;

                if (folder == "") {
                    folder = Path.Combine(Environment.CurrentDirectory, "customsongs");
                }

                var files = new List<FileInfo>();  // List that will hold the files and subfiles in path
                var folders = new List<DirectoryInfo>(); // List that hold direcotries that cannot be accessed

                var di = new DirectoryInfo(folder);
                FullDirList(di, "*");

                if (RequestBotConfig.Instance.AdditionalSongPath != "") {
                    di = new DirectoryInfo(RequestBotConfig.Instance.AdditionalSongPath);
                    FullDirList(di, "*");
                }

                void FullDirList(DirectoryInfo dir, string searchPattern)
                {
                    try {
                        foreach (var f in dir.GetFiles(searchPattern)) {
                            if (f.FullName.EndsWith("info.json")) {
                                files.Add(f);
                            }
                        }
                    }
                    catch {
                        Console.WriteLine("Directory {0}  \n could not be accessed!!!!", dir.FullName);
                        return;
                    }

                    foreach (var d in dir.GetDirectories()) {
                        folders.Add(d);
                        FullDirList(d, searchPattern);
                    }
                }

                // This might need some optimization


                this._chatManager.QueueChatMessage($"Processing {files.Count} maps. ");
                foreach (var item in files) {

                    //msg.Add(item.FullName,", ");

                    string id = "", version = "0";

                    this.GetIdFromPath(item.DirectoryName, ref id, ref version);

                    try {
                        if (this.MapLibrary.ContainsKey(id)) {
                            continue;
                        }

                        var song = JSONObject.Parse(File.ReadAllText(item.FullName)).AsObject;

                        string hash;

                        JSONNode difficultylevels = song["difficultyLevels"].AsArray;
                        var FileAccumulator = new StringBuilder();
                        foreach (var level in difficultylevels) {
                            //Instance.QueueChatMessage($"key={level.Key} value={level.Value}");
                            try {
                                FileAccumulator.Append(File.ReadAllText($"{item.DirectoryName}\\{level.Value["jsonPath"].Value}"));
                            }
                            catch {
                                //Instance.QueueChatMessage($"key={level.Key} value={level.Value}");
                                //throw;
                            }
                        }

                        hash = this.Bot.CreateMD5FromString(FileAccumulator.ToString());

                        var levelId = string.Join("∎", hash, song["songName"].Value, song["songSubName"].Value, song["songAuthorName"], song["beatsPerMinute"].AsFloat.ToString()) + "∎";

                        if (this.LevelId.ContainsKey(levelId)) {
                            this.LevelId[levelId].Path = item.DirectoryName;
                            continue;
                        }

                        song.Add("id", id);
                        song.Add("hashMd5", hash);

                        this._songMapFactory.Create(song, levelId, item.DirectoryName);
                    }
                    catch (Exception) {
                        this._chatManager.QueueChatMessage($"Failed to process {item}.");
                    }
                }
                var duration = DateTime.UtcNow - StarTime;
                if (songid == "") {
                    this._chatManager.QueueChatMessage($"Song indexing done. ({duration.TotalSeconds} secs.");
                }

                DatabaseImported = true;
                this.DatabaseLoading = false;
            });
        }

        private bool GetIdFromPath(string path, ref string id, ref string version)
        {
            var parts = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            id = "";
            version = "0";

            foreach (var part in parts) {
                id = this.Bot.GetBeatSaverId(part);
                if (id != "") {
                    version = part;
                    return true;
                }
            }

            id = tempid++.ToString();
            version = $"{id}-0";
            return false;
        }
    }
}
