using ChatCore.Interfaces;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Bases
{
    public abstract class SRMCommandBase : ISRMCommand
    {
        #region common Regex expressions
        protected static readonly Regex _anything = new Regex(".*", RegexOptions.Compiled); // Is this the most efficient way?
        protected static readonly string success = "";

        #endregion
        // BUG: Extra methods will be removed after the offending code is migrated, There will likely always be 2-3.
        protected Action<IChatUser, string> Method = null;  // Method to call
        protected Action<IChatUser, string, CmdFlags, string> Method2 = null; // Alternate method
                                                                              //private Func<COMMAND, IChatUser, string, CmdFlags, string, string> Method3 = null; // Prefered method, returns the error msg as a string.
        protected Func<ParseState, IEnumerator> func1 = null;

        protected static string _blockeduser = "blockeduser.unique";

        public Func<ParseState, string> Subcommand { get; protected set; } = null; // Prefered calling convention. It does expose calling command base properties, so be careful.
        public Func<ParseState> Subcommand2 { get; protected set; } = null;
        public Func<ParseState, Task> AsyncSubCommand { get; protected set; } = null;

        public CmdFlags Flags { set; get; } = FlagParameter.Broadcaster;          // flags
        public string ShortHelp { set; get; } = "";                   // short help text (on failing preliminary check
        public List<string> Aliases { get; } = new List<string>();               // list of command aliases
        public Regex Regexfilter { get; protected set; } = _anything;                 // reg ex filter to apply. For now, we're going to use a single string

        public string LongHelp { get; protected set; } = null; // Long help text
        public string HelpLink { get; protected set; } = null; // Help website link, Using a wikia might be the way to go
        public string Permittedusers { get; set; } = ""; // Name of list of permitted users.

        public StringBuilder UserParameter { get; set; } = new StringBuilder(); // This is here incase I need it for some specific purpose
        public string UserString { get; set; } = "";
        public int UserNumber { get; set; } = 0;
        public int UseCount { get; set; } = 0;  // Number of times command has been used, sadly without references, updating this is costly.
        public ChangedFlags ChangedParameters { get; set; } = 0; // Indicates if any prameters were changed by the user

        [Inject]
        protected RequestBot _bot;

        [Inject]
        public abstract void Constractor();

        public ISRMCommand AddAliases()
        {
            foreach (var entry in Aliases) {
                if (entry.Length == 0) continue; // Make sure we don't get a blank command
                Plugin.Logger.Debug(entry);
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
            Permittedusers = fixedname;
        }

        public async Task<string> Execute(ParseState state)
        {
            // BUG: Most of these will be replaced.  

            if (Method2 != null) Method2(state._user, state._parameter, state._flags, state._info);
            else if (Method != null) Method(state._user, state._parameter);
            //else if (Method3 != null) return Method3(this, state.user, state.parameter, state.flags, state.info);
            else if (func1 != null) Dispatcher.RunCoroutine(func1(state));
            else if (Subcommand != null) return Subcommand(state); // Recommended.
            else if (Subcommand2 != null) Subcommand(state);
            else if (AsyncSubCommand != null) await AsyncSubCommand(state);
            return success;
        }

        public ISRMCommand Setup(string alias)
        {
            Aliases.Clear();
            Aliases.Add(alias.ToLower());
            AddAliases();
            return this;
        }

        public ISRMCommand Setup(IEnumerable<string> alias)
        {
            Aliases.Clear();
            foreach (var element in alias) {
                Aliases.Add(element.ToLower());
            }
            AddAliases();
            return this;
        }

        public ISRMCommand Setup(string variablename, StringBuilder reference)
        {
            UserParameter = reference;
            Flags = CmdFlags.Variable | CmdFlags.Broadcaster;
            Aliases.Clear();
            Aliases.Add(variablename.ToLower());
            Subcommand = _bot.Variable;
            Regexfilter = _anything;
            ShortHelp = "the = operator currently requires a space after it";
            UserString = reference.ToString(); // Save a backup
            AddAliases();
            return this;
        }

        public ISRMCommand Action(Func<ParseState, string> action)
        {
            Subcommand = action;
            return this;
        }
        public ISRMCommand Action(Func<ParseState> action)
        {
            Subcommand2 = action;
            return this;
        }
        public ISRMCommand AsyncAction(Func<ParseState, Task> action)
        {
            AsyncSubCommand = action;
            return this;
        }
        public ISRMCommand Help(CmdFlags flags = FlagParameter.Broadcaster, string ShortHelp = "", Regex regexfilter = null)
        {
            this.Flags = flags;
            this.ShortHelp = ShortHelp;
            this.Regexfilter = regexfilter != null ? regexfilter : _anything;

            return this;
        }

        public ISRMCommand User(string userstring)
        {
            UserParameter.Clear().Append(userstring);
            return this;
        }

        public ISRMCommand Action(Action<IChatUser, string, CmdFlags, string> action)
        {
            Method2 = action;
            return this;
        }

        public ISRMCommand Action(Action<IChatUser, string> action)
        {
            Method = action;
            return this;
        }

        public ISRMCommand Coroutine(Func<ParseState, IEnumerator> action)
        {
            func1 = action;
            return this;
        }



        #region Command List Save / Load functionality
        public string GetHelpText()
        {
            return ShortHelp;
        }

        public string GetFlags()
        {
            return Flags.ToString();
        }

        public string GetAliases()
        {
            return String.Join(",", Aliases.ToArray());
        }
        #endregion

        public class SRMCommandFactory : PlaceholderFactory<SRMCommand>
        {

        }
    }
}
