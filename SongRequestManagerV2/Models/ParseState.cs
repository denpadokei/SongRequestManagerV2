using ChatCore.Interfaces;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.Utils;
using System;
using Zenject;

namespace SongRequestManagerV2.Models
{
    public class ParseState
    {
        public IChatUser _user;
        public String _request;
        public CmdFlags _flags;
        public string _info;

        public string _command = null;
        public string _parameter = "";
        public string _sort = "";

        public ISRMCommand _botcmd = null;

        public string _subparameter = "";
        private const string notsubcommand = "NotSubcmd";
        [Inject]
        private readonly IChatManager _chatManager;
        [Inject]
        private readonly CommandManager _commandManager;
        [Inject]
        private readonly ListCollectionManager _listCollectionManager;
        [Inject]
        private readonly DynamicText.DynamicTextFactory _textFactory;

        public ParseState Setup(ParseState state, string parameter = null)
        {
            // These are references
            this._user = state._user;
            this._botcmd = state._botcmd;

            this._flags = state._flags;
            this._parameter = state._parameter;
            if (parameter != null) this._parameter = parameter;
            this._subparameter = state._subparameter;
            this._command = state._command;
            this._info = state._info;
            this._sort = state._sort;

            return this;
        }

        public ParseState Setup(IChatUser user, string request, CmdFlags flags, string info)
        {
            this._user = user;
            this._request = request;
            this._flags = flags;
            this._info = info;

            return this;
        }

        // BUG: Execute command and subcommand can probably be largely unified soon

        public string ExecuteSubcommand() // BUG: Only one supported for now (till I finalize the parse logic) ,we'll make it all work eventually
        {
            Logger.Debug("Execute SubCommand");
            int commandstart = 0;

            if (this._parameter.Length < 2) return notsubcommand;

            int subcommandend = this._parameter.IndexOfAny(new[] { ' ', '/' }, 1);
            if (subcommandend == -1) subcommandend = this._parameter.Length;

            int subcommandsectionend = this._parameter.IndexOf('/', 1);
            if (subcommandsectionend == -1) subcommandsectionend = this._parameter.Length;

            //RequestBot.Instance.QueueChatMessage($"parameter [{parameter}] ({subcommandend},{subcommandsectionend})");

            int commandlength = subcommandend - commandstart;

            if (commandlength == 0) return notsubcommand;

            string subcommand = this._parameter.Substring(commandstart, commandlength).ToLower();

            this._subparameter = (subcommandsectionend - subcommandend > 0) ? this._parameter.Substring(subcommandend, subcommandsectionend - subcommandend).Trim(' ') : "";


            if (!this._commandManager.Aliases.TryGetValue(subcommand, out var subcmd)) return notsubcommand;

            if (!subcmd.Flags.HasFlag(CmdFlags.Subcommand)) return notsubcommand;
            // BUG: Need to check subcmd permissions here.     

            if (!Utility.HasRights(subcmd, this._user, this._flags)) return this.Error($"No permission to use {subcommand}");

            if (subcmd.Flags.HasFlag(CmdFlags.NoParameter)) {
                this._parameter = this._parameter.Substring(subcommandend).Trim(' ');
            }
            else {
                this._parameter = this._parameter.Substring(subcommandsectionend);
            }

            try {
                return subcmd.Subcommand(this);
            }
            catch (Exception ex) {
                Logger.Debug(ex.ToString());
            }

            return "";
        }

        public string Msg(string text, string result = "")
        {
            if (!this._flags.HasFlag(CmdFlags.SilentResult)) this._textFactory.Create().AddUser(this._user).AddBotCmd(this._botcmd).QueueMessage(text);
            return result;
        }

        public string Error(string Error)
        {
            return this.Text(Error);
        }

        public string Helptext(bool showlong = false)
        {
            return this._textFactory.Create().AddUser(this._user).AddBotCmd(this._botcmd).Parse(this._botcmd.ShortHelp, showlong);
        }

        public string Text(string text) // Return a formatted text message
        {
            return this._textFactory.Create().AddUser(this._user).AddBotCmd(this._botcmd).Parse(text);
        }

        private static readonly string _done = "X";
        public void ExecuteCommand()
        {
            if (!this._commandManager.Aliases.TryGetValue(this._command, out this._botcmd)) {
                Logger.Debug("Unknown command");
                return; // Unknown command
            }

            // Permissions for these sub commands will always be by Broadcaster,or the (BUG: Future feature) user list of the EnhancedTwitchBot command. Note command behaviour that alters with permission should treat userlist as an escalation to Broadcaster.
            // Since these are never meant for an end user, they are not going to be configurable.

            // Example: !challenge/allow myfriends
            //          !decklist/setflags SUB
            //          !lookup/sethelp usage: %alias%<song name or id>
            //
            while (true) {
                string errormsg = this.ExecuteSubcommand();
                Logger.Debug($"errormsg : {errormsg}");
                if (errormsg == notsubcommand) break;
                if (errormsg != "") {
                    if (errormsg == _done) {
                        this._flags |= CmdFlags.Disabled; // Temporarily disable the rest of the command - flags is local parse state flag.
                        continue;
                    }
                    else {
                        this._chatManager.QueueChatMessage(errormsg);
                        //ShowHelpMessage(ref botcmd, ref user, parameter, false);
                    }
                    return;
                }
            }

            if (this._botcmd.ChangedParameters != 0 && !this._botcmd.ChangedParameters.HasFlag(ChangedFlags.Saved)) {
                this._commandManager.WriteCommandConfiguration();
                this._botcmd.ChangedParameters |= ChangedFlags.Saved;
            }

            if (this._botcmd.Flags.HasFlag(CmdFlags.Disabled) || this._flags.HasFlag(CmdFlags.Disabled)) return; // Disabled commands fail silently

            // Check permissions first

            bool allow = Utility.HasRights(this._botcmd, this._user, this._flags);


            // Num is Nani?
            if (!allow && !this._botcmd.Flags.HasFlag(CmdFlags.BypassRights) && !this._listCollectionManager.Contains(this._botcmd.Permittedusers, this._user.UserName.ToLower())) {
                CmdFlags twitchpermission = this._botcmd.Flags & CmdFlags.TwitchLevel;
                if (!this._botcmd.Flags.HasFlag(CmdFlags.SilentCheck)) this._chatManager.QueueChatMessage($"{this._command} is restricted to {twitchpermission.ToString()}");
                return;
            }

            if (this._parameter == "?") // Handle per command help requests - If permitted.
            {
                this._commandManager.ShowHelpMessage(this._botcmd, this._user, this._parameter, true);
                return;
            }

            // Check regex

            if (!this._botcmd.Regexfilter.IsMatch(this._parameter)) {
                if (!this._botcmd.Flags.HasFlag(CmdFlags.SilentCheck)) this._commandManager.ShowHelpMessage(this._botcmd, this._user, this._parameter, false);
                return;
            }

            try {
                string errormsg = this._botcmd.Execute(this); // Call the command
                if (errormsg != "" && !this._flags.HasFlag(CmdFlags.SilentError)) {
                    this._chatManager.QueueChatMessage(errormsg);
                }
            }
            catch (Exception ex) {
                // Display failure message, and lock out command for a time period. Not yet.
                Logger.Debug(ex.ToString());
            }
        }


        public static string GetCommand(ref string request)
        {
            int commandlength = 0;
            // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
            while (commandlength < request.Length && (request[commandlength] != '=' && request[commandlength] != '/' && request[commandlength] != ' ')) commandlength++;  // Command name ends with #... for now, I'll clean up some more later    
            if (commandlength == 0) return "";
            return request.Substring(0, commandlength).ToLower();
        }

        public ParseState ParseCommand()
        {
            Logger.Debug("Start ParceCommand in ParseCommand()");
            Logger.Debug($"request : {this._request}");

            // Notes for later.
            //var match = Regex.Match(request, "^!(?<command>[^ ^/]*?<parameter>.*)");
            //string username = match.Success ? match.Groups["command"].Value : null;

            int commandstart = 0;
            int parameterstart = 0;

            // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
            while (parameterstart < this._request.Length && (this._request[parameterstart] != '=' && this._request[parameterstart] != '/' && this._request[parameterstart] != ' ')) parameterstart++;  // Command name ends with #... for now, I'll clean up some more later           
            int commandlength = parameterstart - commandstart;
            while (parameterstart < this._request.Length && this._request[parameterstart] == ' ') parameterstart++; // Eat the space(s) if that's the separator after the command
            if (commandlength == 0) return this;

            this._command = this._request.Substring(commandstart, commandlength).ToLower();
            Logger.Debug($"command : {this._command}");
            if (this._commandManager.Aliases.ContainsKey(this._command)) {
                Logger.Debug("Contain ailias commad");
                this._parameter = this._request.Substring(parameterstart);

                try {
                    Logger.Debug("Start command");
                    this.ExecuteCommand();
                }
                catch (Exception ex) {
                    Logger.Debug(ex.ToString());
                }
            }
            else {
                Logger.Debug("Not Contain ailias commad");
            }

            return this;
        }

        public class ParseStateFactory : PlaceholderFactory<ParseState>
        {

        }
    }
}
