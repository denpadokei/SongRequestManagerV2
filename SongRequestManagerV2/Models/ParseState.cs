using ChatCore.Interfaces;
using ChatCore.Models.Twitch;
using SongRequestManagerV2.Bot;
using SongRequestManagerV2.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;
using static SongRequestManagerV2.RequestBot;

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

        public SRMCommand _botcmd = null;

        public string _subparameter = "";

        const string notsubcommand = "NotSubcmd";

        [Inject]
        RequestBot _bot;
        [Inject]
        CommandManager _commandManager;
        [Inject]
        DynamicText.DynamicTextFactory _textFactory;

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
            Plugin.Log("Execute SubCommand");
            int commandstart = 0;

            if (_parameter.Length < 2) return notsubcommand;

            int subcommandend = _parameter.IndexOfAny(new[] { ' ', '/' }, 1);
            if (subcommandend == -1) subcommandend = _parameter.Length;

            int subcommandsectionend = _parameter.IndexOf('/', 1);
            if (subcommandsectionend == -1) subcommandsectionend = _parameter.Length;

            //RequestBot.Instance.QueueChatMessage($"parameter [{parameter}] ({subcommandend},{subcommandsectionend})");

            int commandlength = subcommandend - commandstart;

            if (commandlength == 0) return notsubcommand;

            string subcommand = _parameter.Substring(commandstart, commandlength).ToLower();

            _subparameter = (subcommandsectionend - subcommandend > 0) ? _parameter.Substring(subcommandend, subcommandsectionend - subcommandend).Trim(' ') : "";

            SRMCommand subcmd;
            if (!_commandManager.Aliases.TryGetValue(subcommand, out subcmd)) return notsubcommand;

            if (!subcmd.Flags.HasFlag(CmdFlags.Subcommand)) return notsubcommand;
            // BUG: Need to check subcmd permissions here.     

            if (!HasRights(subcmd, _user, _flags)) return error($"No permission to use {subcommand}");

            if (subcmd.Flags.HasFlag(CmdFlags.NoParameter)) {
                _parameter = _parameter.Substring(subcommandend).Trim(' ');
            }
            else {
                _parameter = _parameter.Substring(subcommandsectionend);
            }

            try {
                return subcmd.subcommand(this);
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }

            return "";
        }

        public string msg(string text, string result = "")
        {
            if (!_flags.HasFlag(CmdFlags.SilentResult)) _textFactory.Create().AddUser(_user).AddBotCmd(_botcmd).QueueMessage(ref text);
            return result;
        }

        public string error(string Error)
        {
            return text(Error);
        }

        public string helptext(bool showlong = false)
        {
            return _textFactory.Create().AddUser(_user).AddBotCmd(_botcmd).Parse(_botcmd.ShortHelp, showlong);
        }

        public string text(string text) // Return a formatted text message
        {
            return _textFactory.Create().AddUser(_user).AddBotCmd(_botcmd).Parse(text);
        }



        static readonly string _done = "X";
        public async void ExecuteCommand()
        {
            if (!_commandManager.Aliases.TryGetValue(_command, out _botcmd)) {
                Plugin.Logger.Info("Unknown command");
                return; // Unknown command
            }

            // Permissions for these sub commands will always be by Broadcaster,or the (BUG: Future feature) user list of the EnhancedTwitchBot command. Note command behaviour that alters with permission should treat userlist as an escalation to Broadcaster.
            // Since these are never meant for an end user, they are not going to be configurable.

            // Example: !challenge/allow myfriends
            //          !decklist/setflags SUB
            //          !lookup/sethelp usage: %alias%<song name or id>
            //
            while (true) {
                string errormsg = ExecuteSubcommand();
                Plugin.Log($"errormsg : {errormsg}");
                if (errormsg == notsubcommand) break;
                if (errormsg != "") {
                    if (errormsg == _done) {
                        _flags |= CmdFlags.Disabled; // Temporarily disable the rest of the command - flags is local parse state flag.
                        continue;
                    }
                    else {
                        _bot.QueueChatMessage(errormsg);
                        //ShowHelpMessage(ref botcmd, ref user, parameter, false);
                    }
                    return;
                }
            }

            if (_botcmd.ChangedParameters != 0 && !_botcmd.ChangedParameters.HasFlag(ChangedFlags.Saved)) {
                _commandManager.WriteCommandConfiguration();
                _botcmd.ChangedParameters |= ChangedFlags.Saved;
            }

            if (_botcmd.Flags.HasFlag(CmdFlags.Disabled) || _flags.HasFlag(CmdFlags.Disabled)) return; // Disabled commands fail silently

            // Check permissions first

            bool allow = HasRights(_botcmd, _user, _flags);


            // Num is Nani?
            if (!allow && !_botcmd.Flags.HasFlag(CmdFlags.BypassRights) && !listcollection.contains(ref _botcmd.permittedusers, _user.UserName.ToLower())) {
                CmdFlags twitchpermission = _botcmd.Flags & CmdFlags.TwitchLevel;
                if (!_botcmd.Flags.HasFlag(CmdFlags.SilentCheck)) Instance?.QueueChatMessage($"{_command} is restricted to {twitchpermission.ToString()}");
                return;
            }

            if (_parameter == "?") // Handle per command help requests - If permitted.
            {
                _commandManager.ShowHelpMessage(_botcmd, _user, _parameter, true);
                return;
            }

            // Check regex

            if (!_botcmd.regexfilter.IsMatch(_parameter)) {
                if (!_botcmd.Flags.HasFlag(CmdFlags.SilentCheck)) _commandManager.ShowHelpMessage(_botcmd, _user, _parameter, false);
                return;
            }

            try {
                string errormsg = await _botcmd.Execute(this); // Call the command
                if (errormsg != "" && !_flags.HasFlag(CmdFlags.SilentError)) {
                    _bot.QueueChatMessage(errormsg);
                }
            }
            catch (Exception ex) {
                // Display failure message, and lock out command for a time period. Not yet.
                Plugin.Log(ex.ToString());
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

        public Task<ParseState> ParseCommand()
        {
            return Task.Run(() =>
            {
                Plugin.Logger.Info("Start ParceCommand in ParseCommand()");
                Plugin.Logger.Info($"request : {this._request}");

                // Notes for later.
                //var match = Regex.Match(request, "^!(?<command>[^ ^/]*?<parameter>.*)");
                //string username = match.Success ? match.Groups["command"].Value : null;

                int commandstart = 0;
                int parameterstart = 0;

                // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
                while (parameterstart < _request.Length && (_request[parameterstart] != '=' && _request[parameterstart] != '/' && _request[parameterstart] != ' ')) parameterstart++;  // Command name ends with #... for now, I'll clean up some more later           
                int commandlength = parameterstart - commandstart;
                while (parameterstart < _request.Length && _request[parameterstart] == ' ') parameterstart++; // Eat the space(s) if that's the separator after the command
                if (commandlength == 0) return this;

                _command = _request.Substring(commandstart, commandlength).ToLower();
                Plugin.Logger.Info($"command : {this._command}");
                if (_commandManager.Aliases.ContainsKey(_command)) {
                    Plugin.Log("Contain ailias commad");
                    _parameter = _request.Substring(parameterstart);

                    try {
                        Plugin.Log("Start command");
                        ExecuteCommand();
                    }
                    catch (Exception ex) {
                        Plugin.Log(ex.ToString());
                    }
                }
                else {
                    Plugin.Log("Not Contain ailias commad");
                }

                return this;
            });
        }

        public class ParseStateFactory : PlaceholderFactory<ParseState>
        {

        }
    }
}
