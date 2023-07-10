using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class MapDatabase
    {
        public ConcurrentDictionary<string, SongMap> MapLibrary { get; } = new ConcurrentDictionary<string, SongMap>();
        public ConcurrentDictionary<string, HashSet<string>> SearchDictionary { get; } = new ConcurrentDictionary<string, HashSet<string>>();
        public ConcurrentDictionary<string, float> PPMap { get; } = new ConcurrentDictionary<string, float>();
        public volatile int _hashCount = 0;
        [Inject]
        private readonly IRequestBot _bot;
        [Inject]
        private readonly IChatManager _chatManager;
        [Inject]
        private readonly StringNormalization _normalize;
        // Fast? Full Text Search
        public HashSet<SongMap> Search(string searchKey)
        {
            var result = new HashSet<SongMap>();

            if (this._bot.GetBeatSaverId(searchKey) != "") {
                if (this.MapLibrary.TryGetValue(this._normalize.RemoveSymbols(searchKey, this._normalize.SymbolsNoDash), out var song)) {
                    _ = result.Add(song);
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
                    _ = result.Add(this.MapLibrary[map]);
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
                    _ = this.MapLibrary.AddOrUpdate(info["id"].Value, map, (key, value) => map);
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
            _ = this.MapLibrary.TryRemove(song["id"].Value, out _);
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
                _ = this.SearchDictionary.AddOrUpdate(key, (k) =>
                {
                    var va = new HashSet<string>
                    {
                        id
                    };
                    return va;
                },
                (k, va) =>
                {
                    _ = va.Add(id);
                    return va;
                });
            }
            else {
                if (this.SearchDictionary.TryRemove(key, out var result)) {
                    _ = (result?.Remove(id)); // An empty keyword is fine, and actually uncommon
                }
            }
        }
    }
}
