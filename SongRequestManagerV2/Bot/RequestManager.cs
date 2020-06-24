//using ChatCore.SimpleJSON;
using ChatCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SongRequestManagerV2
{
    public class RequestManager
    {
        public static List<SongRequest> Read(string path)
        {
            List<SongRequest> songs = new List<SongRequest>();
            if (File.Exists(path))
            {
                var json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull)
                {
                    foreach (var j in json.AsArray) {
                        try {
                            if (!j.Value.IsNull) {
                                var songobj = j.Value;
                                //Plugin.Log($"{songobj}");
                                songs.Add(new SongRequest().FromJson(songobj));
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

        public static void Write(string path, List<SongRequest> songs)
        {
            Plugin.Log($"Start write");
            try {
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                var arr = new JSONArray();
                foreach (var song in songs.Where(x => x != null)) {
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
        public static List<SongRequest> Songs { get; } = new List<SongRequest>();
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
        public static List<SongRequest> Songs { get; } = new List<SongRequest>();
        private static string historyPath = Path.Combine(Plugin.DataPath, "SongRequestHistory.dat");
        public static void Read()
        {
            try
            {
                Songs.Clear();
                Songs.AddRange(RequestManager.Read(historyPath));
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