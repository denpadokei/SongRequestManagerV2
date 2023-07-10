using CatCore.Models.Shared;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.SimpleJsons;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class DynamicText
    {
        [Inject]
        private readonly IChatManager _chatManager;

        public Dictionary<string, string> Dynamicvariables { get; } = new Dictionary<string, string>();  // A list of the variables available to us, we're using a list of pairs because the match we use uses BeginsWith,since the name of the string is unknown. The list is very short, so no biggie

        public bool AllowLinks = true;

        public DynamicText Add(string key, string value)
        {
            if (this.Dynamicvariables.ContainsKey(key)) {
                return this;
            }
            this.Dynamicvariables.Add(key, value); // Make the code slower but more readable :(
            return this;
        }
        public DynamicText AddUser(IChatUser user)
        {
            try {
                _ = this.Add("user", user.DisplayName);
            }
            catch {
                // Don't care. Twitch user doesn't HAVE to be defined.
            }
            return this;
        }

        public DynamicText AddLinks()
        {
            if (this.AllowLinks) {
                _ = this.Add("beatsaver", "https://beatsaver.com");
                _ = this.Add("beatsaber", "https://beatsaber.com");
                _ = this.Add("scoresaber", "https://scoresaber.com");
            }
            else {
                _ = this.Add("beatsaver", "beatsaver site");
                _ = this.Add("beatsaber", "beatsaber site");
                _ = this.Add("scoresaber", "scoresaber site");
            }

            return this;
        }

        public DynamicText AddBotCmd(ISRMCommand botcmd)
        {

            var aliastext = new StringBuilder();
            foreach (var alias in botcmd.Aliases) {
                _ = aliastext.Append($"{alias} ");
            }
            _ = this.Add("alias", aliastext.ToString());

            _ = aliastext.Clear();
            _ = aliastext.Append('[');
            _ = aliastext.Append(botcmd.Flags & CmdFlags.TwitchLevel).ToString();
            _ = aliastext.Append(']');
            _ = this.Add("rights", aliastext.ToString());
            return this;
        }

        // Adds a JSON object to the dictionary. You can define a prefix to make the object identifiers unique if needed.
        public DynamicText AddJSON(JSONObject json, string prefix = "")
        {
            foreach (var element in json.DeepChildren) {
                foreach (var item in element.Children) {
                    foreach (var ditem in item) {
                        _ = this.Add(prefix + ditem.Key, ditem.Value);
                    }
                }
                foreach (var item in element) {
                    _ = this.Add(prefix + item.Key, item.Value);
                }
            }
            foreach (var item in json.Children) {
                foreach (var element in item) {
                    _ = this.Add(prefix + element.Key, element.Value);
                }
            }
            foreach (var item in json) {
                _ = this.Add(prefix + item.Key, item.Value);
            }
            return this;
        }

        // Alternate call for direct object
        public DynamicText AddSong(JSONObject song, string prefix = "")
        {
            _ = this.AddJSON(song, prefix); // Add the song JSON
            _ = song["pp"].AsFloat > 0 ? this.Add("PP", song["pp"].AsInt.ToString() + " PP") : this.Add("PP", "");
            _ = this.Add("StarRating", Utility.GetStarRating(song)); // Add additional dynamic properties
            _ = this.Add("Rating", Utility.GetRating(song));
            _ = this.Add("BeatsaverLink", $"https://beatsaver.com/maps/{song["id"].Value}");
            _ = this.Add("BeatsaberLink", $"https://bsaber.com/songs/{song["id"].Value}");
            if (!string.IsNullOrEmpty(song["id"].Value)) {
                _ = this.Add("key", song["id"].Value);
            }
            return this;
        }

        /// <summary>
        /// We implement a path for ref or nonref
        /// </summary>
        /// <param name="text"></param>
        /// <param name="parselong"></param>
        /// <returns></returns>
        public string Parse(StringBuilder text, bool parselong = false)
        {
            return this.Parse(text.ToString(), parselong);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="parselong"></param>
        /// <returns></returns>
        /// <remarks>Refactor, supports %variable%, and no longer uses split, should be closer to c++ speed.</remarks>
        public string Parse(string text, bool parselong = false)
        {
            var output = new StringBuilder(text.Length); // We assume a starting capacity at LEAST = to length of original string;

            for (var p = 0; p < text.Length; p++) // P is pointer, that's good enough for me
            {
                var c = text[p];
                if (c == '%') {
                    var keywordstart = p + 1;
                    var keywordlength = 0;

                    var end = Math.Min(p + 32, text.Length); // Limit the scan for the 2nd % to 32 characters, or the end of the string
                    for (var k = keywordstart; k < end; k++) // Pretty sure there's a function for this, I'll look it up later
                    {
                        if (text[k] == '%') {
                            keywordlength = k - keywordstart;
                            break;
                        }
                    }
                    if (keywordlength > 0 && keywordlength != 0 && this.Dynamicvariables.TryGetValue(text.Substring(keywordstart, keywordlength), out var substitutetext)) {

                        if (keywordlength == 1 && !parselong) {
                            return output.ToString(); // Return at first sepearator on first 1 character code. 
                        }

                        _ = output.Append(substitutetext);

                        p += keywordlength + 1; // Reset regular text
                        continue;
                    }
                }
                _ = output.Append(c);
            }

            return output.ToString();
        }

        public DynamicText QueueMessage(string text, bool parselong = false)
        {
            this._chatManager.QueueChatMessage(this.Parse(text, parselong));
            return this;
        }

        public class DynamicTextFactory : PlaceholderFactory<DynamicText>
        {
            public override DynamicText Create()
            {
                var dt = base.Create();
                _ = dt.Add("|", ""); // This is the official section separator character, its used in help to separate usage from extended help, and because its easy to detect when parsing, being one character long

                // BUG: Note -- Its my intent to allow sections to be used as a form of conditional. If a result failure occurs within a section, we should be able to rollback the entire section, and continue to the next. Its a better way of handline missing dynamic fields without excessive scripting
                // This isn't implemented yet.

                _ = dt.AddLinks();

                var now = DateTime.Now; //"MM/dd/yyyy hh:mm:ss.fffffff";         
                _ = dt.Add("SRM", "Song Request Manager");
                _ = dt.Add("Time", now.ToString("hh:mm"));
                _ = dt.Add("LongTime", now.ToString("hh:mm:ss"));
                _ = dt.Add("Date", now.ToString("yyyy/MM/dd"));
                _ = dt.Add("LF", "\n"); // Allow carriage return
                return dt;
            }
        }
    }
}
