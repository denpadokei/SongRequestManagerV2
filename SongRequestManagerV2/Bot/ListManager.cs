using ChatCore.Interfaces;
using ChatCore.Utilities;
using SongRequestManagerV2.Bot;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
//using ChatCore.SimpleJSON;
//using StreamCore.Twitch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SongRequestManagerV2
{
    public partial class RequestBot// : MonoBehaviour
    {
        internal void showlists(IChatUser requestor, string request)
        {
            var msg = _messageFactroy.Create();
            msg.Header("Loaded lists: ");
            foreach (var entry in listcollection.ListCollection) msg.Add($"{entry.Key} ({entry.Value.Count()})", ", ");
            msg.end("...", "No lists loaded.");
        }

        internal string listaccess(ParseState state)
        {
            QueueChatMessage($"Hi, my name is {state._botcmd.UserParameter} , and I'm a list object!");
            return success;
        }

        internal void Addtolist(IChatUser requestor, string request)
        {
            string[] parts = request.Split(new char[] { ' ', ',' }, 2);
            if (parts.Length < 2) {
                QueueChatMessage("Usage text... use the official help method");
                return;
            }

            try {

                listcollection.add(ref parts[0], ref parts[1]);
                QueueChatMessage($"Added {parts[1]} to {parts[0]}");

            }
            catch {
                QueueChatMessage($"list {parts[0]} not found.");
            }
        }

        internal void ListList(IChatUser requestor, string request)
        {
            try {
                var list = listcollection.OpenList(request);

                var msg = _messageFactroy.Create();
                foreach (var entry in list.list) msg.Add(entry, ", ");
                msg.end("...", $"{request} is empty");
            }
            catch {
                QueueChatMessage($"{request} not found.");
            }
        }

        internal void RemoveFromlist(IChatUser requestor, string request)
        {
            string[] parts = request.Split(new char[] { ' ', ',' }, 2);
            if (parts.Length < 2) {
                //     NewCommands[Addtolist].ShortHelp();
                QueueChatMessage("Usage text... use the official help method");
                return;
            }

            try {

                listcollection.remove(ref parts[0], ref parts[1]);
                QueueChatMessage($"Removed {parts[1]} from {parts[0]}");

            }
            catch {
                QueueChatMessage($"list {parts[0]} not found.");
            }
        }

        internal void ClearList(IChatUser requestor, string request)
        {
            try {
                listcollection.ClearList(request);
                QueueChatMessage($"{request} is cleared.");
            }
            catch {
                QueueChatMessage($"Unable to clear {request}");
            }
        }

        internal void UnloadList(IChatUser requestor, string request)
        {
            try {
                listcollection.ListCollection.Remove(request.ToLower());
                QueueChatMessage($"{request} unloaded.");
            }
            catch {
                QueueChatMessage($"Unable to unload {request}");
            }
        }

        #region LIST MANAGER user interface

        internal void writelist(IChatUser requestor, string request)
        {

        }

        // Add list to queue, filtered by InQueue and duplicatelist
        internal string queuelist(ParseState state)
        {
            try {
                StringListManager list = listcollection.OpenList(state._parameter);
                foreach (var entry in list.list) ProcessSongRequest(_stateFactory.Create().Setup(state, entry)); // Must use copies here, since these are all threads
            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
            return success;
        }

        // Remove entire list from queue
        internal string unqueuelist(ParseState state)
        {
            state._flags |= FlagParameter.Silent;
            foreach (var entry in listcollection.OpenList(state._parameter).list) {
                state._parameter = entry;
                DequeueSong(state);
            }
            return success;
        }





        #endregion


        #region List Manager Related functions ...
        // List types:

        // This is a work in progress. 

        // .deck = lists of songs
        // .mapper = mapper lists
        // .users = twitch user lists
        // .command = command lists = linear scripting
        // .dict = list contains key value pairs
        // .json = (not part of list manager.. yet)

        // This code is currently in an extreme state of flux. Underlying implementation will change.

        internal void OpenList(IChatUser requestor, string request)
        {
            listcollection.OpenList(request.ToLower());
        }

        public static ListCollectionManager listcollection = new ListCollectionManager();

        [Flags] public enum ListFlags { ReadOnly = 1, InMemory = 2, Uncached = 4, Dynamic = 8, LineSeparator = 16, Unchanged = 256 };

        public static List<JSONObject> ReadJSON(string path)
        {
            List<JSONObject> objs = new List<JSONObject>();
            if (File.Exists(path)) {
                JSONNode json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull) {
                    foreach (JSONObject j in json.AsArray)
                        objs.Add(j);
                }
            }
            return objs;
        }

        public static void WriteJSON(string path, ref List<JSONObject> objs)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            JSONArray arr = new JSONArray();
            foreach (JSONObject obj in objs)
                arr.Add(obj);

            File.WriteAllText(path, arr.ToString());
        }
        #endregion
    }
}
