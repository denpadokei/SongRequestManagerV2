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
                JSONNode json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull)
                {
                    foreach (JSONObject j in json.AsArray)
                        songs.Add(new SongRequest().FromJson(j));
                }
            }
            return songs;
        }

        public static void Write(string path, ref List<SongRequest> songs)
        {
            Plugin.Log($"Start write");
            try {
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                JSONArray arr = new JSONArray();
                foreach (SongRequest song in songs.Where(x => x != null)) {
                    try {
                        arr.Add(song.ToJson());
                        Plugin.Log($"Added {song.song}");
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
        public static List<SongRequest> Songs = new List<SongRequest>();
        private static string requestsPath = Path.Combine(Plugin.DataPath, "SongRequestQueue.dat");
        public static void Read()
        {
            try
            {
                Songs = RequestManager.Read(requestsPath);
            }
            catch
            {
                RequestBot.Instance.QueueChatMessage("There was an error reading the request queue.");
            }

        }

        public static void Write()
        {
            RequestManager.Write(requestsPath, ref Songs);
        }
    }

    public class RequestHistory
    {
        public static List<SongRequest> Songs = new List<SongRequest>();
        private static string historyPath = Path.Combine(Plugin.DataPath, "SongRequestHistory.dat");
        public static void Read()
        {
            try
            {
                Songs = RequestManager.Read(historyPath);
            }
            catch
            {
                RequestBot.Instance.QueueChatMessage("There was an error reading the request history.");
            }

        }

        public static void Write()
        {
            RequestManager.Write(historyPath, ref Songs);
        }
    }

}