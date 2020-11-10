using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    /// <summary>
    /// The list collection maintains a dictionary of named, persistent lists. Accessing a collection by name automatically loads or crates it.
    /// What I really want though is a collection of container objects with the same interface. I need to look into Dynamic to see if I can make this work. Damn being a c# noob
    /// </summary>
    public class ListCollectionManager
    {
        // BUG: DoNotCreate flags currently do nothing
        // BUG: List name case normalization is inconsistent. I'll probably fix it by changing the list interface (its currently just the filename)

        public Dictionary<string, StringListManager> ListCollection = new Dictionary<string, StringListManager>();

        public ListCollectionManager()
        {
            // Add an empty list so we can set various lists to empty
            StringListManager empty = new StringListManager();
            ListCollection.Add("empty", empty);
        }

        public StringListManager ClearOldList(string request, TimeSpan delta, ListFlags flags = ListFlags.Unchanged)
        {
            string listfilename = Path.Combine(Plugin.DataPath, request);
            TimeSpan UpdatedAge = Utility.GetFileAgeDifference(listfilename);

            StringListManager list = OpenList(request, flags);

            if (File.Exists(listfilename) && UpdatedAge > delta) // BUG: There's probably a better way to handle this
            {
                //RequestBot.Instance.QueueChatMessage($"Clearing old session {request}");
                list.Clear();
                if (!(flags.HasFlag(ListFlags.InMemory) | flags.HasFlag(ListFlags.ReadOnly))) list.Writefile(request);

            }

            return list;
        }

        public StringListManager OpenList(string request, ListFlags flags = ListFlags.Unchanged) // All lists are accessed through here, flags determine mode
        {
            StringListManager list;
            if (!ListCollection.TryGetValue(request, out list)) {
                list = new StringListManager();
                ListCollection.Add(request, list);
                if (!flags.HasFlag(ListFlags.InMemory)) list.Readfile(request); // If in memory, we never read from disk
            }
            else {
                if (flags.HasFlag(ListFlags.Uncached)) list.Readfile(request); // If Cache is off, ALWAYS re-read file.
            }
            return list;
        }

        public bool Contains(string listname, string key, ListFlags flags = ListFlags.Unchanged)
        {
            try {
                StringListManager list = OpenList(listname);
                return list.Contains(key);
            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              

            return false;
        }

        public bool Add(string listname, string key, ListFlags flags = ListFlags.Unchanged)
        {
            return Add(ref listname, ref key, flags);
        }

        public bool Add(ref string listname, ref string key, ListFlags flags = ListFlags.Unchanged)
        {
            try {
                StringListManager list = OpenList(listname);

                list.Add(key);


                if (!(flags.HasFlag(ListFlags.InMemory) | flags.HasFlag(ListFlags.ReadOnly))) list.Writefile(listname);
                return true;

            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); }

            return false;
        }

        public bool Remove(string listname, string key, ListFlags flags = ListFlags.Unchanged)
        {
            return Remove(ref listname, ref key, flags);
        }
        public bool Remove(ref string listname, ref string key, ListFlags flags = ListFlags.Unchanged)
        {
            try {
                StringListManager list = OpenList(listname);

                list.Removeentry(key);

                if (!(flags.HasFlag(ListFlags.InMemory) | flags.HasFlag(ListFlags.ReadOnly))) list.Writefile(listname);

                return false;

            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              

            return false;
        }

        public void Runscript(string listname, ListFlags flags = ListFlags.Unchanged)
        {
            try {
                OpenList(listname, flags).Runscript();

            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
        }

        public void ClearList(string listname, ListFlags flags = ListFlags.Unchanged)
        {
            try {
                OpenList(listname).Clear();
            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
        }

    }
}
