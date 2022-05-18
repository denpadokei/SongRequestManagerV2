using CatCore.Models.Shared;
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
        public IChatUser User { get; set; }
        public string Request { get; set; }
        public CmdFlags Flags { get; set; }
        public string Info { get; set; }
        public string Command { get; set; } = null;
        public string Parameter { get; set; } = "";
        public string Sort { get; set; } = "";

        public ISRMCommand _botcmd = null;

        public string Subparameter { get; set; } = "";
        private static readonly string s_notsubcommand = "NotSubcmd";
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
            this.User = state.User;
            this._botcmd = state._botcmd;

            this.Flags = state.Flags;
            this.Parameter = state.Parameter;
            if (parameter != null) {
                this.Parameter = parameter;
            }

            this.Subparameter = state.Subparameter;
            this.Command = state.Command;
            this.Info = state.Info;
            this.Sort = state.Sort;

            return this;
        }

        public ParseState Setup(IChatUser user, string request, CmdFlags flags, string info)
        {
            this.User = user;
            this.Request = request;
            this.Flags = flags;
            this.Info = info;

            return this;
        }

        // BUG: Execute command and subcommand can probably be largely unified soon

        public string ExecuteSubcommand() // BUG: Only one supported for now (till I finalize the parse logic) ,we'll make it all work eventually
        {
            var commandstart = 0;

            if (this.Parameter.Length < 2) {
                return s_notsubcommand;
            }

            var subcommandend = this.Parameter.IndexOfAny(new[] { ' ', '/' }, 1);
            if (subcommandend == -1) {
                subcommandend = this.Parameter.Length;
            }

            var subcommandsectionend = this.Parameter.IndexOf('/', 1);
            if (subcommandsectionend == -1) {
                subcommandsectionend = this.Parameter.Length;
            }

            //RequestBot.Instance.QueueChatMessage($"parameter [{parameter}] ({subcommandend},{subcommandsectionend})");

            var commandlength = subcommandend - commandstart;

            if (commandlength == 0) {
                return s_notsubcommand;
            }

            var subcommand = this.Parameter.Substring(commandstart, commandlength).ToLower();

            this.Subparameter = (subcommandsectionend - subcommandend > 0) ? this.Parameter.Substring(subcommandend, subcommandsectionend - subcommandend).Trim(' ') : "";


            if (!this._commandManager.Aliases.TryGetValue(subcommand, out var subcmd)) {
                return s_notsubcommand;
            }

            if (!subcmd.Flags.HasFlag(CmdFlags.Subcommand)) {
                return s_notsubcommand;
            }
            // BUG: Need to check subcmd permissions here.     

            if (!Utility.HasRights(subcmd, this.User, this.Flags)) {
                return this.Error($"No permission to use {subcommand}");
            }

            if (subcmd.Flags.HasFlag(CmdFlags.NoParameter)) {
                this.Parameter = this.Parameter.Substring(subcommandend).Trim(' ');
            }
            else {
                this.Parameter = this.Parameter.Substring(subcommandsectionend);
            }

            try {
                subcmd.Subcommand?.Invoke(this);
            }
            catch (Exception ex) {
                Logger.Error(ex);
                return ex.Message;
            }

            return "";
        }

        public string Msg(string text, string result = "")
        {
            if (!this.Flags.HasFlag(CmdFlags.SilentResult)) {
                this._textFactory.Create().AddUser(this.User).AddBotCmd(this._botcmd).QueueMessage(text);
            }

            return result;
        }

        public string Error(string Error)
        {
            return this.Text(Error);
        }

        public string Helptext(bool showlong = false)
        {
            return this._textFactory.Create().AddUser(this.User).AddBotCmd(this._botcmd).Parse(this._botcmd.ShortHelp, showlong);
        }

        public string Text(string text) // Return a formatted text message
        {
            return this._textFactory.Create().AddUser(this.User).AddBotCmd(this._botcmd).Parse(text);
        }

        private static readonly string s_done = "X";
        public void ExecuteCommand()
        {
            if (!this._commandManager.Aliases.TryGetValue(this.Command, out this._botcmd)) {
                return; // Unknown command
            }

            // Permissions for these sub commands will always be by Broadcaster,or the (BUG: Future feature) user list of the EnhancedTwitchBot command. Note command behaviour that alters with permission should treat userlist as an escalation to Broadcaster.
            // Since these are never meant for an end user, they are not going to be configurable.

            // Example: !challenge/allow myfriends
            //          !decklist/setflags SUB
            //          !lookup/sethelp usage: %alias%<song name or id>
            //
            while (true) {
                var errormsg = this.ExecuteSubcommand();
                Logger.Debug($"errormsg : {errormsg}");
                if (errormsg == s_notsubcommand) {
                    break;
                }

                if (errormsg != "") {
                    if (errormsg == s_done) {
                        this.Flags |= CmdFlags.Disabled; // Temporarily disable the rest of the command - flags is local parse state flag.
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

            if (this._botcmd.Flags.HasFlag(CmdFlags.Disabled) || this.Flags.HasFlag(CmdFlags.Disabled)) {
                return; // Disabled commands fail silently
            }

            // Check permissions first

            var allow = Utility.HasRights(this._botcmd, this.User, this.Flags);


            // Num is Nani?
            if (!allow && !this._botcmd.Flags.HasFlag(CmdFlags.BypassRights) && !this._listCollectionManager.Contains(this._botcmd.Permittedusers, this.User.UserName.ToLower())) {
                var twitchpermission = this._botcmd.Flags & CmdFlags.TwitchLevel;
                if (!this._botcmd.Flags.HasFlag(CmdFlags.SilentCheck)) {
                    this._chatManager.QueueChatMessage($"{this.Command} is restricted to {twitchpermission}");
                }

                return;
            }

            if (this.Parameter == "?") // Handle per command help requests - If permitted.
            {
                this._commandManager.ShowHelpMessage(this._botcmd, this.User, true);
                return;
            }

            // Check regex

            if (!this._botcmd.Regexfilter.IsMatch(this.Parameter)) {
                if (!this._botcmd.Flags.HasFlag(CmdFlags.SilentCheck)) {
                    this._commandManager.ShowHelpMessage(this._botcmd, this.User, false);
                }

                return;
            }

            try {
                var errormsg = this._botcmd.Execute(this); // Call the command
                if (errormsg != "" && !this.Flags.HasFlag(CmdFlags.SilentError)) {
                    this._chatManager.QueueChatMessage(errormsg);
                }
            }
            catch (Exception ex) {
                // Display failure message, and lock out command for a time period. Not yet.
                Logger.Error(ex);
            }
        }


        public static string GetCommand(ref string request)
        {
            var commandlength = 0;
            // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
            while (commandlength < request.Length && (request[commandlength] != '=' && request[commandlength] != '/' && request[commandlength] != ' ')) {
                commandlength++;  // Command name ends with #... for now, I'll clean up some more later    
            }

            if (commandlength == 0) {
                return "";
            }

            return request.Substring(0, commandlength).ToLower();
        }

        public ParseState ParseCommand()
        {
            // Notes for later.
            //var match = Regex.Match(request, "^!(?<command>[^ ^/]*?<parameter>.*)");
            //string username = match.Success ? match.Groups["command"].Value : null;

            var commandstart = 0;
            var parameterstart = 0;

            // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
            while (parameterstart < this.Request.Length && (this.Request[parameterstart] != '=' && this.Request[parameterstart] != '/' && this.Request[parameterstart] != ' ')) {
                parameterstart++;  // Command name ends with #... for now, I'll clean up some more later           
            }

            var commandlength = parameterstart - commandstart;
            while (parameterstart < this.Request.Length && this.Request[parameterstart] == ' ') {
                parameterstart++; // Eat the space(s) if that's the separator after the command
            }

            if (commandlength == 0) {
                return this;
            }

            this.Command = this.Request.Substring(commandstart, commandlength).ToLower();

            if (this._commandManager.Aliases.ContainsKey(this.Command)) {
                this.Parameter = this.Request.Substring(parameterstart);
                try {
                    this.ExecuteCommand();
                }
                catch (Exception ex) {
                    Logger.Error(ex);
                }
            }

            return this;
        }

        public class ParseStateFactory : PlaceholderFactory<ParseState>
        {

        }
    }
}
