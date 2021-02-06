using ChatCore.Utilities;
using SongRequestManagerV2.Extentions;
using SongRequestManagerV2.Interfaces;
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
        public static ConcurrentDictionary<string, SongMap> MapLibrary { get; } = new ConcurrentDictionary<string, SongMap>();
        public static ConcurrentDictionary<string, SongMap> LevelId { get; } = new ConcurrentDictionary<string, SongMap>();
        public static ConcurrentDictionary<string, HashSet<int>> SearchDictionary { get; } = new ConcurrentDictionary<string, HashSet<int>>();
        public static ConcurrentDictionary<string, float> PPMap { get; } = new ConcurrentDictionary<string, float>();

        static int tempid = 100000; // For now, we use these for local ID less songs

        static bool DatabaseImported = false;
        public static bool DatabaseLoading = false;

        [Inject]
        IRequestBot _bot { get; }
        [Inject]
        IChatManager _chatManager;

        [Inject]
        StringNormalization normalize;
        [Inject]
        SongMap.SongMapFactory _songMapFactory;

        // Fast? Full Text Search
        public List<SongMap> Search(string SearchKey)
        {
            if (!DatabaseImported && RequestBotConfig.Instance.LocalSearch) {
                LoadCustomSongs().Await(null, null, null);
            }

            List<SongMap> result = new List<SongMap>();

            if (_bot.GetBeatSaverId(SearchKey) != "") {
                if (MapDatabase.MapLibrary.TryGetValue(normalize.RemoveSymbols(ref SearchKey, normalize._SymbolsNoDash), out var song)) {
                    result.Add(song);
                    return result;
                }
            }

            List<HashSet<int>> resultlist = new List<HashSet<int>>();

            string[] SearchParts = normalize.Split(SearchKey);

            foreach (var part in SearchParts) {
                if (!SearchDictionary.TryGetValue(part, out var idset)) return result; // Keyword must be found
                resultlist.Add(idset);
            }

            // We now have n lists of candidates

            resultlist.Sort((L1, L2) => L1.Count.CompareTo(L2.Count));

            // We now have an optimized query

            // Compute all matches
            foreach (var map in resultlist[0]) {
                for (int i = 1; i < resultlist.Count; i++) {
                    if (!resultlist[i].Contains(map)) goto next; // We can't continue from here :(    
                }


                try {
                    result.Add(MapDatabase.MapLibrary[map.ToString("x")]);
                }
                catch {
                    _chatManager.QueueChatMessage($"map fail = {map}");
                }

            next:
                ;
            }

            return result;
        }


        public void RemoveMap(JSONObject song)
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
                DateTime start = DateTime.Now;
                //JSONArray arr = new JSONArray();
                JSONObject arr = new JSONObject();
                foreach (var entry in MapLibrary)
                    arr.Add(entry.Value.Song["id"], entry.Value.Song);
                File.WriteAllText(Path.Combine(Plugin.DataPath, "SongDatabase.dat"), arr.ToString());
                _chatManager.QueueChatMessage($"Saved Song Databse in  {(DateTime.Now - start).Seconds} secs.");
            }
            catch (Exception ex) {
                Logger.Debug(ex.ToString());
            }

        }

        public void LoadDatabase()
        {
            try {
                DateTime start = DateTime.Now;
                string path = Path.Combine(Plugin.DataPath, "SongDatabase.dat");

                if (File.Exists(path)) {
                    JSONNode json = JSON.Parse(File.ReadAllText(path));
                    if (!json.IsNull) {
                        int Count = json.Count;

                        //foreach (JSONObject j in json.AsArray)
                        //{                                    
                        //    new SongMap(j);
                        //}


                        foreach (KeyValuePair<string, JSONNode> kvp in json) {
                            _songMapFactory.Create((JSONObject)kvp.Value);
                        }


                        json = 0; // BUG: This doesn't actually help. The problem is that the json object is still being referenced.

                        _chatManager.QueueChatMessage($"Finished reading {Count} in {(DateTime.Now - start).Seconds} secs.");
                    }
                }
            }
            catch (Exception ex) {

                Logger.Debug(ex.ToString());
                _chatManager.QueueChatMessage($"{ex}");
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
            if (info == null) return "";

            StreamReader reader = new StreamReader(info.Open());
            string result = reader.ReadToEnd();
            reader.Close();
            return result;
        }


        // Early code... index a full zip archive.
        public async Task LoadZIPDirectory(string folder = @"d:\beatsaver")
        {
            if (MapDatabase.DatabaseLoading) return;

            await Task.Run(() =>
            {

                var startingmem = GC.GetTotalMemory(true);

                _chatManager.QueueChatMessage($"Starting to read archive.");
                int addcount = 0;
                var StarTime = DateTime.Now;

                var di = new DirectoryInfo(folder);

                foreach (FileInfo f in di.GetFiles("*.zip")) {

                    try {
                        var x = ZipFile.OpenRead(f.FullName);
                        var info = x.Entries.First<ZipArchiveEntry>(e => (e.Name.EndsWith("info.json")));

                        string id = "";
                        string version = "";
                        GetIdFromPath(f.Name, ref id, ref version);

                        if (MapDatabase.MapLibrary.ContainsKey(id)) {
                            if (MapLibrary[id].Path != "") MapLibrary[id].Path = f.FullName;
                            continue;
                        }

                        JSONObject song = JSONObject.Parse(Readzipjson(x)).AsObject;

                        string hash;

                        JSONNode difficultylevels = song["difficultyLevels"].AsArray;
                        var FileAccumulator = new StringBuilder();
                        foreach (var level in difficultylevels) {
                            try {
                                FileAccumulator.Append(Readzipjson(x, level.Value));
                            }
                            catch {
                                //Instance.QueueChatMessage($"key={level.Key} value={level.Value}");
                                //throw;
                            }
                        }

                        hash = _bot.CreateMD5FromString(FileAccumulator.ToString());

                        string levelId = string.Join("∎", hash, song["songName"].Value, song["songSubName"].Value, song["authorName"], song["beatsPerMinute"].AsFloat.ToString()) + "∎";

                        if (LevelId.ContainsKey(levelId)) {

                            LevelId[levelId].Path = f.FullName;
                            continue;
                        }

                        addcount++;

                        song.Add("id", id);
                        song.Add("version", version);
                        song.Add("hashMd5", hash);

                        _songMapFactory.Create(song, levelId, f.FullName);

                        x = null;

                    }
                    catch (Exception) {
                        _chatManager.QueueChatMessage($"Failed to process {f.FullName}");
                        //Instance.QueueChatMessage(ex.ToString());
                    }


                }
                _chatManager.QueueChatMessage($"Archive indexing done, {addcount} files added. ({(DateTime.Now - StarTime).TotalSeconds} secs.");
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                _chatManager.QueueChatMessage($"hashentries: {SongMap.HashCount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");


            });


            MapDatabase.DatabaseLoading = false;
        }


        // Update Database from Directory
        public Task LoadCustomSongs(string folder = "", string songid = "")
        {
            if (MapDatabase.DatabaseLoading) {
                return Task.CompletedTask;
            }

            DatabaseLoading = true;
            return Task.Run(() =>
            {
                if (songid == "") _chatManager.QueueChatMessage($"Starting song indexing {folder}");

                var StarTime = DateTime.UtcNow;

                if (folder == "") folder = Path.Combine(Environment.CurrentDirectory, "customsongs");

                List<FileInfo> files = new List<FileInfo>();  // List that will hold the files and subfiles in path
                List<DirectoryInfo> folders = new List<DirectoryInfo>(); // List that hold direcotries that cannot be accessed

                DirectoryInfo di = new DirectoryInfo(folder);
                FullDirList(di, "*");

                if (RequestBotConfig.Instance.additionalsongpath != "") {
                    di = new DirectoryInfo(RequestBotConfig.Instance.additionalsongpath);
                    FullDirList(di, "*");
                }

                void FullDirList(DirectoryInfo dir, string searchPattern)
                {
                    try {
                        foreach (FileInfo f in dir.GetFiles(searchPattern)) {
                            if (f.FullName.EndsWith("info.json"))
                                files.Add(f);
                        }
                    }
                    catch {
                        Console.WriteLine("Directory {0}  \n could not be accessed!!!!", dir.FullName);
                        return;
                    }

                    foreach (DirectoryInfo d in dir.GetDirectories()) {
                        folders.Add(d);
                        FullDirList(d, searchPattern);
                    }
                }

                // This might need some optimization


                _chatManager.QueueChatMessage($"Processing {files.Count} maps. ");
                foreach (var item in files) {

                    //msg.Add(item.FullName,", ");

                    string id = "", version = "0";

                    GetIdFromPath(item.DirectoryName, ref id, ref version);

                    try {
                        if (MapDatabase.MapLibrary.ContainsKey(id)) continue;

                        JSONObject song = JSONObject.Parse(File.ReadAllText(item.FullName)).AsObject;

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

                        hash = _bot.CreateMD5FromString(FileAccumulator.ToString());

                        string levelId = string.Join("∎", hash, song["songName"].Value, song["songSubName"].Value, song["authorName"], song["beatsPerMinute"].AsFloat.ToString()) + "∎";

                        if (LevelId.ContainsKey(levelId)) {
                            LevelId[levelId].Path = item.DirectoryName;
                            continue;
                        }

                        song.Add("id", id);
                        song.Add("version", version);
                        song.Add("hashMd5", hash);

                        _songMapFactory.Create(song, levelId, item.DirectoryName);
                    }
                    catch (Exception) {
                        _chatManager.QueueChatMessage($"Failed to process {item}.");
                    }

                }
                var duration = DateTime.UtcNow - StarTime;
                if (songid == "") _chatManager.QueueChatMessage($"Song indexing done. ({duration.TotalSeconds} secs.");

                DatabaseImported = true;
                DatabaseLoading = false;
            });
        }

        bool GetIdFromPath(string path, ref string id, ref string version)
        {
            string[] parts = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            id = "";
            version = "0";

            foreach (var part in parts) {
                id = _bot.GetBeatSaverId(part);
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
