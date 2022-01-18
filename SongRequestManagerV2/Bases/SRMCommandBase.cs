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

        protected static readonly string _blockeduser = "blockeduser.unique";

        public Func<ParseState, string> Subcommand { get; protected set; } = null; // Prefered calling convention. It does expose calling command base properties, so be careful.
        public Func<ParseState> Subcommand2 { get; protected set; } = null;
        public Func<ParseState, Task> AsyncSubCommand { get; protected set; } = null;

        public CmdFlags Flags { set; get; } = FlagParameter.Broadcaster;          // flags
        public string ShortHelp { set; get; } = "";                   // short help text (on failing preliminary check
        public HashSet<string> Aliases { get; } = new HashSet<string>();               // list of command aliases
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
        public abstract void Constractor();

        [Inject]
        private readonly IChatManager _chatManager;

        public ISRMCommand AddAliases(params string[] aliases)
        {
            foreach (var item in aliases) {
                this.Aliases.Add(item.ToLower());
            }
            return this;
        }

        public void UpdateCommand(ChangedFlags changed)
        {
            this.ChangedParameters |= changed;
            this.ChangedParameters &= ~ChangedFlags.Saved;
        }




        public void SetPermittedUsers(string listname)
        {
            // BUG: Needs additional checking

            var fixedname = listname.ToLower();
            if (!fixedname.EndsWith(".users")) {
                fixedname += ".users";
            }

            this.Permittedusers = fixedname;
        }

        public string Execute(ParseState state)
        {
            // BUG: Most of these will be replaced.  

            if (this.Method2 != null) {
                this.Method2(state.User, state.Parameter, state.Flags, state.Info);
            }
            else if (this.Method != null) {
                this.Method(state.User, state.Parameter);
            }
            //else if (Method3 != null) return Method3(this, state.user, state.parameter, state.flags, state.info);
            else if (this.func1 != null) {
                Dispatcher.RunCoroutine(this.func1(state));
            }
            else if (this.Subcommand != null) {
                return this.Subcommand(state); // Recommended.
            }
            else if (this.Subcommand2 != null) {
                this.Subcommand(state);
            }
            else if (this.AsyncSubCommand != null) {
                _ = this.AsyncSubCommand(state);
            }

            return success;
        }
        public ISRMCommand Setup(params string[] alias)
        {
            this.Aliases.Clear();
            foreach (var element in alias) {
                this.Aliases.Add(element.ToLower());
            }
            return this;
        }

        public ISRMCommand Setup(string variablename, StringBuilder reference)
        {
            this.UserParameter = reference;
            this.Flags = CmdFlags.Variable | CmdFlags.Broadcaster;
            this.Aliases.Clear();
            this.Aliases.Add(variablename.ToLower());
            this.Subcommand = this.Variable;
            this.Regexfilter = _anything;
            this.ShortHelp = "the = operator currently requires a space after it";
            this.UserString = reference.ToString(); // Save a backup
            return this;
        }

        public ISRMCommand Action(Func<ParseState, string> action)
        {
            this.Subcommand = action;
            return this;
        }
        public ISRMCommand Action(Func<ParseState> action)
        {
            this.Subcommand2 = action;
            return this;
        }
        public ISRMCommand AsyncAction(Func<ParseState, Task> action)
        {
            this.AsyncSubCommand = action;
            return this;
        }
        public ISRMCommand Help(CmdFlags flags = FlagParameter.Broadcaster, string ShortHelp = "", Regex regexfilter = null)
        {
            this.Flags = flags;
            this.ShortHelp = ShortHelp;
            this.Regexfilter = regexfilter ?? _anything;

            return this;
        }

        public ISRMCommand User(string userstring)
        {
            this.UserParameter.Clear().Append(userstring);
            return this;
        }

        public ISRMCommand Action(Action<IChatUser, string, CmdFlags, string> action)
        {
            this.Method2 = action;
            return this;
        }

        public ISRMCommand Action(Action<IChatUser, string> action)
        {
            this.Method = action;
            return this;
        }

        public ISRMCommand Coroutine(Func<ParseState, IEnumerator> action)
        {
            this.func1 = action;
            return this;
        }

        #region Command List Save / Load functionality
        public string GetHelpText()
        {
            return this.ShortHelp;
        }

        public string GetFlags()
        {
            return this.Flags.ToString();
        }

        public string GetAliases()
        {
            return string.Join(",", this.Aliases.ToArray());
        }
        #endregion

        private string Variable(ParseState state) // Basically show the value of a variable without parsing
        {
            this._chatManager.QueueChatMessage(state._botcmd.UserParameter.ToString());
            return "";
        }
    }
}
