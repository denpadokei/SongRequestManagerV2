using ChatCore.Interfaces;
using ChatCore.Utilities;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class DynamicText
    {
        [Inject]
        IRequestBot _bot;

        public Dictionary<string, string> Dynamicvariables { get; } = new Dictionary<string, string>();  // A list of the variables available to us, we're using a list of pairs because the match we use uses BeginsWith,since the name of the string is unknown. The list is very short, so no biggie

        public bool AllowLinks = true;


        public DynamicText Add(string key, string value)
        {
            Dynamicvariables.Add(key, value); // Make the code slower but more readable :(
            return this;
        }
        public DynamicText AddUser(IChatUser user)
        {
            try {
                Add("user", user.DisplayName);
            }
            catch {
                // Don't care. Twitch user doesn't HAVE to be defined.
            }
            return this;
        }

        public DynamicText AddLinks()
        {
            if (AllowLinks) {
                Add("beatsaver", "https://beatsaver.com");
                Add("beatsaber", "https://beatsaber.com");
                Add("scoresaber", "https://scoresaber.com");
            }
            else {
                Add("beatsaver", "beatsaver site");
                Add("beatsaver", "beatsaber site");
                Add("scoresaber", "scoresaber site");
            }

            return this;
        }


        public DynamicText AddBotCmd(ISRMCommand botcmd)
        {

            StringBuilder aliastext = new StringBuilder();
            foreach (var alias in botcmd.Aliases) aliastext.Append($"{alias} ");
            Add("alias", aliastext.ToString());

            aliastext.Clear();
            aliastext.Append('[');
            aliastext.Append(botcmd.Flags & CmdFlags.TwitchLevel).ToString();
            aliastext.Append(']');
            Add("rights", aliastext.ToString());
            return this;
        }

        // Adds a JSON object to the dictionary. You can define a prefix to make the object identifiers unique if needed.
        public DynamicText AddJSON(ref JSONObject json, string prefix = "")
        {
            foreach (var element in json) Add(prefix + element.Key, element.Value);
            return this;
        }

        public DynamicText AddSong(JSONObject json, string prefix = "") // Alternate call for direct object
        {
            return AddSong(ref json, prefix);
        }

        public DynamicText AddSong(ref JSONObject song, string prefix = "")
        {
            AddJSON(ref song, prefix); // Add the song JSON

            //SongMap map;
            //if (MapDatabase.MapLibrary.TryGetValue(song["version"].Value, out map) && map.pp>0)
            //{
            //    Add("pp", map.pp.ToString());
            //}
            //else
            //{
            //    Add("pp", "");
            //}


            if (song["pp"].AsFloat > 0) Add("PP", song["pp"].AsInt.ToString() + " PP"); else Add("PP", "");

            Add("StarRating", _bot.GetStarRating(ref song)); // Add additional dynamic properties
            Add("Rating", _bot.GetRating(ref song));
            Add("BeatsaverLink", $"https://beatsaver.com/beatmap/{song["id"].Value}");
            Add("BeatsaberLink", $"https://bsaber.com/songs/{song["id"].Value}");
            return this;
        }

        public string Parse(StringBuilder text, bool parselong = false) // We implement a path for ref or nonref
        {
            return Parse(text.ToString(), parselong);
        }

        // Refactor, supports %variable%, and no longer uses split, should be closer to c++ speed.
        public string Parse(string text, bool parselong = false)
        {
            StringBuilder output = new StringBuilder(text.Length); // We assume a starting capacity at LEAST = to length of original string;

            for (int p = 0; p < text.Length; p++) // P is pointer, that's good enough for me
            {
                char c = text[p];
                if (c == '%') {
                    int keywordstart = p + 1;
                    int keywordlength = 0;

                    int end = Math.Min(p + 32, text.Length); // Limit the scan for the 2nd % to 32 characters, or the end of the string
                    for (int k = keywordstart; k < end; k++) // Pretty sure there's a function for this, I'll look it up later
                    {
                        if (text[k] == '%') {
                            keywordlength = k - keywordstart;
                            break;
                        }
                    }

                    string substitutetext;

                    if (keywordlength > 0 && keywordlength != 0 && Dynamicvariables.TryGetValue(text.Substring(keywordstart, keywordlength), out substitutetext)) {

                        if (keywordlength == 1 && !parselong) return output.ToString(); // Return at first sepearator on first 1 character code. 

                        output.Append(substitutetext);

                        p += keywordlength + 1; // Reset regular text
                        continue;
                    }
                }
                output.Append(c);
            }

            return output.ToString();
        }

        public DynamicText QueueMessage(string text, bool parselong = false)
        {
            _bot.QueueChatMessage(Parse(text, parselong));
            return this;
        }


        public class DynamicTextFactory : PlaceholderFactory<DynamicText>
        {
            public override DynamicText Create()
            {
                var dt = base.Create();
                dt.Add("|", ""); // This is the official section separator character, its used in help to separate usage from extended help, and because its easy to detect when parsing, being one character long

                // BUG: Note -- Its my intent to allow sections to be used as a form of conditional. If a result failure occurs within a section, we should be able to rollback the entire section, and continue to the next. Its a better way of handline missing dynamic fields without excessive scripting
                // This isn't implemented yet.

                dt.AddLinks();

                var now = DateTime.Now; //"MM/dd/yyyy hh:mm:ss.fffffff";         
                dt.Add("SRM", "Song Request Manager");
                dt.Add("Time", now.ToString("hh:mm"));
                dt.Add("LongTime", now.ToString("hh:mm:ss"));
                dt.Add("Date", now.ToString("yyyy/MM/dd"));
                dt.Add("LF", "\n"); // Allow carriage return
                return dt;
            }
        }
    }
}
