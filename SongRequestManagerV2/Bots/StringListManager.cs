using CatCore.Models.Shared;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Statics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    /// <summary>
    /// All variables are public for now until we finalize the interface
    /// </summary>
    public class StringListManager
    {
        private static readonly char[] anyseparator = { ',', ' ', '\t', '\r', '\n' };
        private static readonly char[] lineseparator = { '\n', '\r' };

        public List<string> list = new List<string>();
        private readonly HashSet<string> hashlist = new HashSet<string>();
        [Inject]
        private readonly IRequestBot _bot;
        private readonly ListFlags flags = 0;

        // Callback function prototype here

        public StringListManager(ListFlags flag = ListFlags.Unchanged)
        {
            this.flags = flag;
        }

        /// <summary>
        /// Resolves a list name to a file inside the plugin data directory. List names come from chat input,
        /// so anything that could escape the data directory is rejected.
        /// </summary>
        private static bool TryResolveListPath(string filename, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(filename)
                || Path.IsPathRooted(filename)
                || filename.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) {
                return false;
            }

            var dataRoot = Path.GetFullPath(Plugin.DataPath);
            if (!dataRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                dataRoot += Path.DirectorySeparatorChar;
            }

            var candidate = Path.GetFullPath(Path.Combine(dataRoot, filename));
            if (candidate.Length <= dataRoot.Length || !candidate.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            path = candidate;
            return true;
        }

        public bool Readfile(string filename, bool ConvertToLower = false)
        {
            if (this.flags.HasFlag(ListFlags.InMemory)) {
                return false;
            }

            try {
                if (!TryResolveListPath(filename, out var listfilename)) {
                    Logger.Error($"Invalid list name: {filename}");
                    return false;
                }
                if (!File.Exists(listfilename)) {
                    File.WriteAllText(listfilename, "");
                }
                var fileContent = File.ReadAllText(listfilename);
                this.list = listfilename.EndsWith(".script")
                    ? fileContent.Split(lineseparator, StringSplitOptions.RemoveEmptyEntries).ToList()
                    : fileContent.Split(anyseparator, StringSplitOptions.RemoveEmptyEntries).ToList();

                if (ConvertToLower) {
                    this.LowercaseList();
                }

                return true;
            }
            catch (Exception e) {
                // Ignoring this for now, I expect it to fail
                Logger.Error(e);
            }

            return false;
        }

        public void Runscript(IChatUser user, CmdFlags flags = 0)
        {
            try {
                // BUG: A DynamicText context needs to be applied to each command to allow use of dynamic variables
                if (this._bot == null) {
                    Logger.Error("Script execution is unavailable because the bot is not attached.");
                    return;
                }

                foreach (var line in this.list) {
                    this._bot.Parse(user, line, flags); // Scripts run with the privileges of the caller, never with CmdFlags.Local.
                }
            }
            catch (Exception ex) {
                Logger.Error(ex);
            } // Going to try this form, to reduce code verbosity.            
        }

        public bool Writefile(string filename)
        {
            var separator = filename.EndsWith(".script") ? "\r\n" : ",";

            try {
                if (!TryResolveListPath(filename, out var listfilename)) {
                    Logger.Error($"Invalid list name: {filename}");
                    return false;
                }

                var output = string.Join(separator, this.list.ToArray());
                File.WriteAllText(listfilename, output);
                return true;
            }
            catch (Exception e) {
                // Ignoring this for now, I expect it to fail
                Logger.Error(e);
            }
            return false;
        }

        public bool Contains(string entry)
        {
            return this.list.Contains(entry);
        }

        public bool Add(string entry)
        {
            if (this.list.Contains(entry)) {
                return false;
            }

            this.list.Add(entry);
            return true;
        }

        public bool Removeentry(string entry)
        {
            return this.list.Remove(entry);
        }

        // Picks a random entry and returns it, removing it from the list
        public string Drawentry()
        {
            if (this.list.Count == 0) {
                return "";
            }

            var entry = RequestBot.Generator.Next(0, this.list.Count);
            var result = this.list.ElementAt(entry);
            this.list.RemoveAt(entry);
            return result;
        }

        // Picks a random entry but does not remove it
        public string Randomentry()
        {
            if (this.list.Count == 0) {
                return "";
            }

            var entry = RequestBot.Generator.Next(0, this.list.Count);
            var result = this.list.ElementAt(entry);
            return result;
        }

        public int Count()
        {
            return this.list.Count;
        }

        public void Clear()
        {
            this.list.Clear();
        }

        public void LowercaseList()
        {
            for (var i = 0; i < this.list.Count; i++) {
                this.list[i] = this.list[i].ToLower();
            }
        }
        public void Outputlist(QueueLongMessage msg, string separator = ", ")
        {
            foreach (var entry in this.list) {
                _ = msg.Add(entry, separator);
            }
        }
    }
}
