using ChatCore.SimpleJSON;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SongRequestManagerV2
{
    public class RequestManager
    {
        public static List<object> Read(string path)
        {
            var songs = new List<object>();
            if (File.Exists(path))
            {
                var json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull)
                {
                    foreach (var j in json.AsArray) {
                        try {
                            if (!j.Value.IsNull && j.Value is JSONObject obj) {
                                var req = new SongRequest(obj);
                                songs.Add(req);
                            }
                        }
                        catch (Exception e) {
                            Plugin.Log($"{e}");
                        }
                    }   
                }
            }
            return songs;
        }

        public static void Write(string path, IEnumerable<object> songs)
        {
            Plugin.Log($"Start write");
            try {
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                var arr = new JSONArray();
                foreach (var song in songs.Where(x => x != null).Select(x => x as SongRequest)) {
                    try {
                        arr.Add(song.ToJson());
                        //Plugin.Log($"Added {song.song}");
                    }
                    catch (Exception ex) {
                        Plugin.Log($"{ex}\r\n{song}");
                    }
                }
                File.WriteAllText(path, arr.ToString());
            }
            catch (Exception ex) {
                Plugin.Log($"{ex}");
            }
            finally {
                Plugin.Log($"End write");
            }
        }
    }

    public class RequestQueue
    {
        public static List<object> Songs { get; } = new List<object>();
        private static string requestsPath = Path.Combine(Plugin.DataPath, "SongRequestQueue.dat");
        public static void Read()
        {
            try
            {
                Songs.Clear();
                Songs.AddRange(RequestManager.Read(requestsPath));
            }
            catch (Exception e)
            {
                Plugin.Log($"{e}");
                RequestBot.Instance.QueueChatMessage("There was an error reading the request queue.");
            }

        }

        public static void Write()
        {
            RequestManager.Write(requestsPath, Songs);
        }
    }

    public class RequestHistory
    {
        public static List<object> Songs { get; } = new List<object>();
        private static string historyPath = Path.Combine(Plugin.DataPath, "SongRequestHistory.dat");
        public static void Read()
        {
            try
            {
                Songs.Clear();
                var list = RequestManager.Read(historyPath);
                Songs.AddRange(RequestManager.Read(historyPath));
                foreach (var item in list) {
                    HistoryManager.AddSong(item as SongRequest);
                }
            }
            catch
            {
                RequestBot.Instance.QueueChatMessage("There was an error reading the request history.");
            }

        }

        public static void Write()
        {
            RequestManager.Write(historyPath, Songs);
        }
    }

}