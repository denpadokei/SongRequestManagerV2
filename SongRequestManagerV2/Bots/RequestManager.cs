using ChatCore.Utilities;
using SongRequestManagerV2.Extentions;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zenject;

namespace SongRequestManagerV2
{
    public class RequestManager
    {
        [Inject]
        private readonly SongRequest.SongRequestFactory factory;
        [Inject]
        private readonly IChatManager _chatManager;

        public static BlockingCollection<object> RequestSongs { get; } = new BlockingCollection<object>();
        private static readonly string requestsPath = Path.Combine(Plugin.DataPath, "SongRequestQueue.dat");

        public static BlockingCollection<object> HistorySongs { get; } = new BlockingCollection<object>();
        private static readonly string historyPath = Path.Combine(Plugin.DataPath, "SongRequestHistory.dat");

        public List<object> Read(string path)
        {
            var songs = new List<object>();
            if (File.Exists(path)) {
                var json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull) {
                    foreach (var j in json.AsArray) {
                        try {
                            if (!j.Value.IsNull && j.Value is JSONObject obj) {
                                var req = this.factory.Create().Init(obj);
                                songs.Add(req);
                            }
                        }
                        catch (Exception e) {
                            Logger.Debug($"{e}");
                        }
                    }
                }
            }
            return songs;
        }

        public void Write(string path, IEnumerable<object> songs)
        {
            Logger.Debug($"Start write");
            try {
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                var arr = new JSONArray();
                foreach (var song in songs.Where(x => x != null).Select(x => x as SongRequest)) {
                    try {
                        arr.Add(song.ToJson());
                    }
                    catch (Exception ex) {
                        Logger.Debug($"{ex}\r\n{song}");
                    }
                }
                File.WriteAllText(path, arr.ToString());
            }
            catch (Exception ex) {
                Logger.Debug($"{ex}");
            }
            finally {
                Logger.Debug($"End write");
            }
        }

        public void ReadRequest()
        {
            try {
                RequestSongs.Clear();
                RequestSongs.AddRange(this.Read(requestsPath));
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
                this._chatManager.QueueChatMessage("There was an error reading the request queue.");
            }

        }

        public void WriteRequest()
        {
            this.Write(requestsPath, RequestSongs);
        }

        public void ReadHistory()
        {
            try {
                HistorySongs.Clear();
                var list = this.Read(historyPath);
                HistorySongs.AddRange(this.Read(historyPath));
                foreach (var item in list) {
                    HistoryManager.AddSong(item as SongRequest);
                }
            }
            catch {
                this._chatManager.QueueChatMessage("There was an error reading the request history.");
            }

        }

        public void WriteHistory()
        {
            this.Write(historyPath, HistorySongs);
        }
    }
}