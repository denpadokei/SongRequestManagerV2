﻿using Newtonsoft.Json;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
using System;
using System.IO;
using System.Linq;

namespace SongRequestManagerV2.Utils
{
    public static class HistoryManager
    {
        public static readonly string PlaylistName = "SRM History.bplist";
        public static readonly string PlaylistPath = Path.Combine(Environment.CurrentDirectory, "Playlists", PlaylistName);

        private static readonly object lockObject = new object();

        internal static void AddSong(SongRequest song)
        {
            // WIPはプレイリストにいれなくていいと思いました。
            if (song.IsWIP) {
                return;
            }
            // !oopsと検索系は除外
            if ((song.Status & (RequestStatus.Wrongsong | RequestStatus.SongSearch)) != 0) {
                return;
            }
            var fileInfo = new FileInfo(PlaylistPath);
            if (!Directory.Exists(fileInfo.Directory.FullName)) {
                Directory.CreateDirectory(fileInfo.Directory.FullName);
            }
            var songObject = song.SongMetaData;
            var version = song.SongVersion;

            var playlistsong = new PlaylistSongEntity()
            {
                songName = songObject["songName"].Value,
                levelAuthorName = songObject["levelAuthorName"].Value,
                key = song.ID,
                hash = version["hash"].Value.ToUpper(),
                levelid = $"custom_level_{version["hash"].Value.ToUpper()}",
                dateAdded = song.RequestTime.ToLocalTime()
            };

            var playlist = LoadPlaylist();
            if (playlist.songs.Any(x => x.hash.ToUpper() == playlistsong.hash)) {
                return;
            }

            playlist.songs.Add(playlistsong);

            try {
                lock (lockObject) {
                    playlist.songs = playlist.songs.OrderByDescending(x => x.dateAdded.ToLocalTime()).ToList();
                    File.WriteAllText(PlaylistPath, JsonConvert.SerializeObject(playlist, Formatting.Indented));
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        private static PlaylistEntity LoadPlaylist()
        {
            lock (lockObject) {
                try {
                    if (!File.Exists(PlaylistPath)) {
                        File.WriteAllText(PlaylistPath, JsonConvert.SerializeObject(new PlaylistEntity(), Formatting.Indented));
                    }

                    var value = File.ReadAllText(PlaylistPath);
                    var playlist = JsonConvert.DeserializeObject<PlaylistEntity>(value);

                    return playlist;
                }
                catch (Exception e) {
                    Logger.Error(e);
                    return null;
                }
            }
        }
    }
}
