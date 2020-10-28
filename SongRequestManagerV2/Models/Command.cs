using ChatCore.Interfaces;
using SongRequestManagerV2.Extentions;
using SongRequestManagerV2.Statics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zenject;
using static SongRequestManagerV2.RequestBot;

namespace SongRequestManagerV2.Models
{
    #region COMMAND Class
    public class SRMCommand
    {
        public SRMCommand()
        {

        }

        #region common Regex expressions

        private static readonly Regex _digitRegex = new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _alphaNumericRegex = new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled);
        private static readonly Regex _RemapRegex = new Regex("^[0-9a-fA-F]+,[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex _beatsaversongversion = new Regex("^[0-9a-zA-Z]+$", RegexOptions.Compiled);
        private static readonly Regex _nothing = new Regex("$^", RegexOptions.Compiled);
        private static readonly Regex _anything = new Regex(".*", RegexOptions.Compiled); // Is this the most efficient way?
        private static readonly Regex _atleast1 = new Regex("..*", RegexOptions.Compiled); // Allow usage message to kick in for blank 
        private static readonly Regex _fail = new Regex("(?!x)x", RegexOptions.Compiled); // Not sure what the official fastest way to auto-fail a match is, so this will do
        private static readonly Regex _deck = new Regex("^(current|draw|first|last|random|unload)$|$^", RegexOptions.Compiled); // Checks deck command parameters

        private static readonly Regex _drawcard = new Regex("($^)|(^[0-9a-zA-Z]+$)", RegexOptions.Compiled);

        private static readonly string success = "";

        #endregion
        // BUG: Extra methods will be removed after the offending code is migrated, There will likely always be 2-3.
        private Action<IChatUser, string> Method = null;  // Method to call
        private Action<IChatUser, string, CmdFlags, string> Method2 = null; // Alternate method
                                                                            //private Func<COMMAND, IChatUser, string, CmdFlags, string, string> Method3 = null; // Prefered method, returns the error msg as a string.
        private Func<ParseState, IEnumerator> func1 = null;

        private static string _blockeduser = "blockeduser.unique";

        public Func<ParseState, string> subcommand = null; // Prefered calling convention. It does expose calling command base properties, so be careful.
        public Func<ParseState> subcommand2 = null;
        public Func<ParseState, Task> AsyncSubCommand = null;

        public CmdFlags Flags = FlagParameter.Broadcaster;          // flags
        public string ShortHelp = "";                   // short help text (on failing preliminary check
        public List<string> aliases { get; } = new List<string>();               // list of command aliases
        public Regex regexfilter = _anything;                 // reg ex filter to apply. For now, we're going to use a single string

        public string LongHelp = null; // Long help text
        public string HelpLink = null; // Help website link, Using a wikia might be the way to go
        public string permittedusers = ""; // Name of list of permitted users.

        public StringBuilder userParameter = new StringBuilder(); // This is here incase I need it for some specific purpose
        public string UserString = "";
        public int userNumber = 0;
        public int UseCount = 0;  // Number of times command has been used, sadly without references, updating this is costly.
        public ChangedFlags ChangedParameters = 0; // Indicates if any prameters were changed by the user

        [Inject]
        RequestBot _bot;
        [Inject]
        ParseState.ParseStateFactory _stateFactory;

        public SRMCommand AddAliases()
        {
            foreach (var entry in aliases) {
                var cmdname = entry;
                entry.ToLower();
                if (entry.Length == 0) continue; // Make sure we don't get a blank command
                
            }
            return this;
        }

        public void UpdateCommand(ChangedFlags changed)
        {
            ChangedParameters |= changed;
            ChangedParameters &= ~ChangedFlags.Saved;
        }



        
        public void SetPermittedUsers(string listname)
        {
            // BUG: Needs additional checking

            string fixedname = listname.ToLower();
            if (!fixedname.EndsWith(".users")) fixedname += ".users";
            permittedusers = fixedname;
        }

        public async Task<string> Execute(ParseState state)
        {
            // BUG: Most of these will be replaced.  

            if (Method2 != null) Method2(state._user, state._parameter, state._flags, state._info);
            else if (Method != null) Method(state._user, state._parameter);
            //else if (Method3 != null) return Method3(this, state.user, state.parameter, state.flags, state.info);
            else if (func1 != null) Dispatcher.RunCoroutine(func1(state));
            else if (subcommand != null) return subcommand(state); // Recommended.
            else if (subcommand2 != null) subcommand(state);
            else if (AsyncSubCommand != null) await AsyncSubCommand(state);
            return success;
        }

        public SRMCommand Setup(string alias)
        {
            aliases.Clear();
            aliases.Add(alias.ToLower());
            AddAliases();
            return this;
        }

        public SRMCommand Setup(IEnumerable<string> alias)
        {
            aliases.Clear();
            foreach (var element in alias) {
                aliases.Add(element.ToLower());
            }
            AddAliases();
            return this;
        }

        public SRMCommand Setup(string variablename, StringBuilder reference)
        {
            userParameter = reference;
            Flags = CmdFlags.Variable | CmdFlags.Broadcaster;
            aliases.Clear();
            aliases.Add(variablename.ToLower());
            subcommand = _bot.Variable;
            regexfilter = _anything;
            ShortHelp = "the = operator currently requires a space after it";
            UserString = reference.ToString(); // Save a backup
            AddAliases();
            return this;
        }

        public SRMCommand Action(Func<ParseState, string> action)
        {
            subcommand = action;
            return this;
        }
        public SRMCommand Action(Func<ParseState> action)
        {
            subcommand2 = action;
            return this;
        }
        public SRMCommand AsyncAction(Func<ParseState, Task> action)
        {
            AsyncSubCommand = action;
            return this;
        }
        public SRMCommand Help(CmdFlags flags = FlagParameter.Broadcaster, string ShortHelp = "", Regex regexfilter = null)
        {
            this.Flags = flags;
            this.ShortHelp = ShortHelp;
            this.regexfilter = regexfilter != null ? regexfilter : _anything;

            return this;
        }

        public SRMCommand User(string userstring)
        {
            userParameter.Clear().Append(userstring);
            return this;
        }

        //public COMMAND Action(Func<COMMAND, IChatUser, string, CmdFlags, string, string> action)
        //{
        //    Method3 = action;
        //    return this;
        //}

        public SRMCommand Action(Action<IChatUser, string, CmdFlags, string> action)
        {
            Method2 = action;
            return this;
        }

        public SRMCommand Action(Action<IChatUser, string> action)
        {
            Method = action;
            return this;
        }

        public SRMCommand Coroutine(Func<ParseState, IEnumerator> action)
        {
            func1 = action;
            return this;
        }

        public void Parse(IChatUser user, string request, CmdFlags flags = 0, string info = "")
        {
            if (string.IsNullOrEmpty(request)) {
                Plugin.Log($"request strings is null : {request}");
                return;
            }

            if (!string.IsNullOrEmpty(user.Id) && listcollection.contains(ref _blockeduser, user.Id.ToLower())) {
                Plugin.Log($"Sender is contain blacklist : {user.UserName}");
                return;
            }

            // This will be used for all parsing type operations, allowing subcommands efficient access to parse state logic
            _stateFactory.Create().Setup(user, request, flags, info).ParseCommand().Await(result => { Plugin.Log("finish ParceCommand"); }, null, null);
        }

        #region Command List Save / Load functionality
        internal string GetHelpText()
        {
            return ShortHelp;
        }

        internal string GetFlags()
        {
            return Flags.ToString();
        }

        internal string GetAliases()
        {
            return String.Join(",", aliases.ToArray());
        }
        #endregion

        public class SRMCommandFactory : PlaceholderFactory<SRMCommand>
        {

        }
    }
    #endregion
}
