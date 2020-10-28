using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;
using static SongRequestManagerV2.RequestBot;

namespace SongRequestManagerV2.Bot
{
    /// <summary>
    /// All variables are public for now until we finalize the interface
    /// </summary>
    public class StringListManager
    {
        private static char[] anyseparator = { ',', ' ', '\t', '\r', '\n' };
        private static char[] lineseparator = { '\n', '\r' };

        public List<string> list = new List<string>();
        private HashSet<string> hashlist = new HashSet<string>();

        [Inject]
        SRMCommand.SRMCommandFactory _commandFactory;

        ListFlags flags = 0;

        // Callback function prototype here

        public StringListManager(ListFlags ReadOnly = ListFlags.Unchanged)
        {

        }

        public bool Readfile(string filename, bool ConvertToLower = false)
        {
            if (flags.HasFlag(ListFlags.InMemory)) return false;

            try {
                string listfilename = Path.Combine(Plugin.DataPath, filename);
                string fileContent = File.ReadAllText(listfilename);
                if (listfilename.EndsWith(".script"))
                    list = fileContent.Split(lineseparator, StringSplitOptions.RemoveEmptyEntries).ToList();
                else
                    list = fileContent.Split(anyseparator, StringSplitOptions.RemoveEmptyEntries).ToList();

                if (ConvertToLower) LowercaseList();
                return true;
            }
            catch {
                // Ignoring this for now, I expect it to fail
            }

            return false;
        }

        public void runscript()
        {
            try {
                // BUG: A DynamicText context needs to be applied to each command to allow use of dynamic variables

                foreach (var line in list) _commandFactory.Create().Parse(null, line, CmdFlags.Local);
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            } // Going to try this form, to reduce code verbosity.            
        }

        public bool Writefile(string filename)
        {
            string separator = filename.EndsWith(".script") ? "\r\n" : ",";

            try {
                string listfilename = Path.Combine(Plugin.DataPath, filename);

                var output = String.Join(separator, list.ToArray());
                File.WriteAllText(listfilename, output);
                return true;
            }
            catch {
                // Ignoring this for now, failed write can be silent
            }
            return false;
        }

        public bool Contains(string entry)
        {
            if (list.Contains(entry)) return true;
            return false;
        }

        public bool Add(string entry)
        {
            if (list.Contains(entry)) return false;
            list.Add(entry);
            return true;
        }

        public bool Removeentry(string entry)
        {
            return list.Remove(entry);
        }

        // Picks a random entry and returns it, removing it from the list
        public string Drawentry()
        {
            if (list.Count == 0) return "";
            int entry = generator.Next(0, list.Count);
            string result = list.ElementAt(entry);
            list.RemoveAt(entry);
            return result;
        }

        // Picks a random entry but does not remove it
        public string Randomentry()
        {
            if (list.Count == 0) return "";
            int entry = generator.Next(0, list.Count);
            string result = list.ElementAt(entry);
            return result;
        }

        public int Count()
        {
            return list.Count;
        }

        public void Clear()
        {
            list.Clear();
        }

        public void LowercaseList()
        {
            for (int i = 0; i < list.Count; i++) {
                list[i] = list[i].ToLower();
            }
        }
        public void Outputlist(QueueLongMessage msg, string separator = ", ")
        {
            foreach (string entry in list) msg.Add(entry, separator);
        }
    }
}
