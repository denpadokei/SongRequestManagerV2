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

        public bool Readfile(string filename, bool ConvertToLower = false)
        {
            if (this.flags.HasFlag(ListFlags.InMemory)) {
                return false;
            }

            try {
                var listfilename = Path.Combine(Plugin.DataPath, filename);
                if (!File.Exists(listfilename)) {
                    File.WriteAllText(listfilename, "");
                }
                var fileContent = File.ReadAllText(listfilename);
                if (listfilename.EndsWith(".script")) {
                    this.list = fileContent.Split(lineseparator, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                else {
                    this.list = fileContent.Split(anyseparator, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

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

        public void Runscript()
        {
            try {
                // BUG: A DynamicText context needs to be applied to each command to allow use of dynamic variables

                foreach (var line in this.list) {
                    this._bot.Parse(null, line, CmdFlags.Local);
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
                var listfilename = Path.Combine(Plugin.DataPath, filename);

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
            if (this.list.Contains(entry)) {
                return true;
            }

            return false;
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
                msg.Add(entry, separator);
            }
        }
    }
}
