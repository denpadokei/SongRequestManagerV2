using CatCore.Models.Shared;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.SimpleJSON;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Models
{
    public class CommandManager : IInitializable
    {
        [Inject]
        private readonly SRMCommand.SRMCommandFactory _commandFactory;
        [Inject]
        private readonly QueueLongMessage.QueueLongMessageFactroy _queueFactory;
        [Inject]
        private readonly DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        private IRequestBot Bot { get; }
        [Inject]
        private readonly IChatManager _chatManager;
        [Inject]
        private readonly DiContainer _diContainer;
        [Inject]
        private readonly StringNormalization _normalize;

        #region common Regex expressions


        private static readonly Regex s_alphaNumericRegex = new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled);
        private static readonly Regex s_remapRegex = new Regex("^[0-9a-fA-F]+,[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex s_beatsaversongversion = new Regex("^[0-9a-zA-Z]+$", RegexOptions.Compiled);
        private static readonly Regex s_nothing = new Regex("$^", RegexOptions.Compiled);
        private static readonly Regex s_anything = new Regex(".*", RegexOptions.Compiled); // Is this the most efficient way?
        private static readonly Regex s_atleast1 = new Regex("..*", RegexOptions.Compiled); // Allow usage message to kick in for blank 
        private static readonly Regex s_fail = new Regex("(?!x)x", RegexOptions.Compiled); // Not sure what the official fastest way to auto-fail a match is, so this will do


        private const string s_success = "";
        private const string s_endcommand = "X";

        public static StringBuilder Commandsummary { get; } = new StringBuilder();

        #endregion

        public ConcurrentDictionary<string, ISRMCommand> Aliases { get; } = new ConcurrentDictionary<string, ISRMCommand>(); // There can be only one (static)!

        public void Initialize()
        {
            /*
                *VERY IMPORTANT*
 
                The Command name or FIRST alias in the alias list is considered the Base name of the command, and absolutely should not be changed through code. Choose this first name wisely.
                We use the Base name to allow user command Customization, it is how the command is identified to the user. You can alter the alias list of the commands in 
                the command configuration file (srmcommands.ini).
 
            */

            var commands = new List<ISRMCommand>();

            this.Aliases.Clear();

            #region 初期化ごにょごにょ
            commands.Add(this._commandFactory.Create().Setup("!bsr", "!request", "!add", "!sr", "!srm").Action(this.Bot.ProcessSongRequest).Help(FlagParameter.Everyone, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the request queue. Try and be a little specific. You can look up songs on %beatsaver%", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!lookup", "!find").AsyncAction(this.LookupSongs).Help(FlagParameter.Mod | FlagParameter.Sub | FlagParameter.VIP, "usage: %alias%<song name> or <song id>, omit <>'s.%|%Get a list of songs from %beatsaver% matching your search criteria.", s_atleast1));

            commands.Add(this._commandFactory.Create().Setup("!link").Action(this.Bot.ShowSongLink).Help(FlagParameter.Everyone, "usage: %alias% %|%... Shows song details, and an %beatsaver% link to the current song", s_nothing));

            commands.Add(this._commandFactory.Create().Setup("!open").Action(this.Bot.OpenQueue).Help(FlagParameter.Mod, "usage: %alias%%|%... Opens the queue allowing song requests.", s_nothing));
            commands.Add(this._commandFactory.Create().Setup("!close").Action(this.Bot.CloseQueue).Help(FlagParameter.Mod, "usage: %alias%%|%... Closes the request queue.", s_nothing));

            commands.Add(this._commandFactory.Create().Setup("!queue").Action(this.ListQueue).Help(FlagParameter.Everyone, "usage: %alias%%|% ... Displays a list of the currently requested songs.", s_nothing));
            commands.Add(this._commandFactory.Create().Setup("!played").Action(this.ShowSongsplayed).Help(FlagParameter.Mod, "usage: %alias%%|%... Displays all the songs already played this session.", s_nothing));
            commands.Add(this._commandFactory.Create().Setup("!history").Action(this.ShowHistory).Help(FlagParameter.Mod, "usage: %alias% %|% Shows a list of the recently played songs, starting from the most recent.", s_nothing));
            commands.Add(this._commandFactory.Create().Setup("!who").Action(this.Bot.Who).Help(FlagParameter.Sub | FlagParameter.VIP | FlagParameter.Mod, "usage: %alias% <songid or name>%|%Find out who requested the song in the currently queue or recent history.", s_atleast1));

            commands.Add(this._commandFactory.Create().Setup("!modadd").Action(this.Bot.ModAdd).Help(FlagParameter.Mod, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the request queue. This ignores ALL filters including bans.", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!mtt").Action(this.Bot.MoveRequestToTop).Help(FlagParameter.Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Moves a song to the top of the request queue.", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!att").Action(this.Bot.AddToTop).Help(FlagParameter.Mod, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the top of the request queue. Try and be a little specific. You can look up songs on %beatsaver%", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!last", "!demote", "!later").Action(this.Bot.MoveRequestToBottom).Help(FlagParameter.Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Moves a song to the bottom of the request queue.", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!remove").Action(this.Bot.DequeueSong).Help(FlagParameter.Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Removes a song from the queue.", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!wrongsong", "!wrong", "!oops").Action(this.Bot.WrongSong).Help(FlagParameter.Everyone, "usage: %alias%%|%... Removes your last requested song form the queue. It can be requested again later.", s_nothing));

            commands.Add(this._commandFactory.Create().Setup("!unblock").Action(this.Bot.Unban).Help(FlagParameter.Mod, "usage: %alias%<song id>, do not include <,>'s.", s_beatsaversongversion));
            commands.Add(this._commandFactory.Create().Setup("!block").AsyncAction(this.Bot.Ban).Help(FlagParameter.Mod, "usage: %alias%<song id>, do not include <,>'s.", s_beatsaversongversion));
            commands.Add(this._commandFactory.Create().Setup("!blist").Action(this.ShowBanList).Help(FlagParameter.Broadcaster, "usage: Don't use, it will spam chat!", s_atleast1)); // Purposely annoying to use, add a character after the command to make it happen 

            commands.Add(this._commandFactory.Create().Setup("!remap").Action(this.Bot.Remap).Help(FlagParameter.Mod, "usage: %alias%<songid1> , <songid2>%|%... Remaps future song requests of <songid1> to <songid2> , hopefully a newer/better version of the map.", s_remapRegex));
            commands.Add(this._commandFactory.Create().Setup("!unmap").Action(this.Bot.Unmap).Help(FlagParameter.Mod, "usage: %alias%<songid> %|%... Remove future remaps for songid.", s_beatsaversongversion));

            commands.Add(this._commandFactory.Create().Setup("!clearqueue").Action(this.Bot.Clearqueue).Help(FlagParameter.Mod, "usage: %alias%%|%... Clears the song request queue. You can still get it back from the JustCleared deck, or the history window", s_nothing));
            commands.Add(this._commandFactory.Create().Setup("!clearalreadyplayed").Action(this.Bot.ClearDuplicateList).Help(FlagParameter.Mod, "usage: %alias%%|%... clears the list of already requested songs, allowing them to be requested again.", s_nothing)); // Needs a better name
            commands.Add(this._commandFactory.Create().Setup("!restore").Action(this.Bot.Restoredeck).Help(FlagParameter.Mod, "usage: %alias%%|%... Restores the request queue from the previous session. Only useful if you have persistent Queue turned off.", s_nothing));

            commands.Add(this._commandFactory.Create().Setup("!about").Help(CmdFlags.Broadcaster | CmdFlags.SilentCheck, $"Song Request Manager version {Plugin.Version}. Github angturil/SongRequestManager", s_fail)); // Help commands have no code
            commands.Add(this._commandFactory.Create().Setup("!help").Action(this.Help).Help(FlagParameter.Everyone, "usage: %alias%<command name>, or just %alias%to show a list of all commands available to you.", s_anything));
            commands.Add(this._commandFactory.Create().Setup("!commandlist").Action(this.ShowCommandlist).Help(FlagParameter.Everyone, "usage: %alias%%|%... Displays all the bot commands available to you.", s_nothing));

            commands.Add(this._commandFactory.Create().Setup("!readdeck").Action(this.Bot.Readdeck).Help(FlagParameter.Mod, "usage: %alias", s_alphaNumericRegex));
            commands.Add(this._commandFactory.Create().Setup("!writedeck").Action(this.Bot.Writedeck).Help(FlagParameter.Broadcaster, "usage: %alias", s_alphaNumericRegex));

            commands.Add(this._commandFactory.Create().Setup("!chatmessage").Action(this.Bot.ChatMessage).Help(FlagParameter.Broadcaster, "usage: %alias%<what you want to say in chat, supports % variables>", s_atleast1)); // BUG: Song support requires more intelligent %CurrentSong that correctly handles missing current song. Also, need a function to get the currenly playing song.
            commands.Add(this._commandFactory.Create().Setup("!runscript").Action(this.Bot.RunScript).Help(FlagParameter.Mod, "usage: %alias%<name>%|%Runs a script with a .script extension, no conditionals are allowed. startup.script will run when the bot is first started. Its probably best that you use an external editor to edit the scripts which are located in UserData/StreamCore", s_atleast1));

            commands.Add(this._commandFactory.Create().Setup("!formatlist").Action(this.ShowFormatList).Help(FlagParameter.Broadcaster, "Show a list of all the available customizable text format strings. Use caution, as this can make the output of some commands unusable. You can use /default to return a variable to its default setting."));


            commands.Add(this._commandFactory.Create().Setup("!songmsg").Action(this.Bot.SongMsg).Help(FlagParameter.Mod, "usage: %alias% <songid> Message%|% Assign a message to a songid, which will be visible to the player during song selection.", s_atleast1));

            commands.Add(this._commandFactory.Create().Setup("!addsongs").AsyncAction(this.Bot.Addsongs).Help(FlagParameter.Broadcaster, "usage: %alias%%|% Add all songs matching a criteria (up to 40) to the queue", s_atleast1));

            commands.Add(this._commandFactory.Create().Setup("!every").Action(this.Bot.Every).Help(FlagParameter.Broadcaster, "usage: every <minutes> %|% Run a command every <minutes>.", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!in").Action(this.Bot.EventIn).Help(FlagParameter.Broadcaster, "usage: in <minutes> <bot command>.", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!clearevents").Action(this.Bot.ClearEvents).Help(FlagParameter.Broadcaster, "usage: %alias% %|% Clear all timer events."));
            commands.Add(this._commandFactory.Create().Setup("!addnew", "!addlatest").AsyncAction(this.Bot.AddsongsFromnewest).Help(FlagParameter.Mod, "usage: %alias% <listname>%|%... Adds the latest maps from %beatsaver%, filtered by the previous selected allowmappers command", s_nothing));
            commands.Add(this._commandFactory.Create().Setup("!addpp").AsyncAction(this.Bot.AddsongsFromRank).Help(FlagParameter.Mod, "usage: %alias% <listname>%|%... Adds the rank maps from %beatsaver%, filtered by the previous selected allowmappers command", s_nothing));
            commands.Add(this._commandFactory.Create().Setup("!backup").Action(this.Bot.BackupStreamcore).Help(CmdFlags.Broadcaster, "Backup %SRM% directory.", s_anything));

            commands.Add(this._commandFactory.Create().Setup("!queuestatus").Action(this.Bot.QueueStatus).Help(FlagParameter.Mod, "usage: %alias% %|% Show current queue status", s_nothing));

            commands.Add(this._commandFactory.Create().Setup("!QueueLottery").Action(this.Bot.QueueLottery).Help(FlagParameter.Broadcaster, "usage: %alias% <entry count> %|% Shuffle the queue and reduce to <entry count> entries. Close the queue.", s_anything));

            commands.Add(this._commandFactory.Create().Setup("!addtoqueue").Action(this.Bot.Queuelist).Help(FlagParameter.Broadcaster, "usage: %alias% <list>", s_atleast1));
            commands.Add(this._commandFactory.Create().Setup("!makesearchdeck").AsyncAction(this.Bot.Makelistfromsearch).Help(FlagParameter.Broadcaster, "usage: %alias%%|% Add all songs matching a criteria to search.deck", s_atleast1));

            #region Gamechanger Specific           
            var GameChangerInstalled = IPA.Loader.PluginManager.GetPlugin("Beat Bits") != null;
            //_WobbleInstalled = IPA.Loader.PluginManager.GetPlugin("WobbleSaber") != null;

            if (GameChangerInstalled) {
                commands.Add(this._commandFactory.Create().Setup("!sabotage").Coroutine(this.Bot.SetBombState).Help(FlagParameter.Mod, "Usage: %alias% on/off (LIV Gamechanger only). %|% Turns bombs on and off in Gamechanger."));
            }
            #endregion

            #region Text Format fields

            //would be good to use reflections for these
            commands.Add(this._commandFactory.Create().Setup("AddSongToQueueText", StringFormat.AddSongToQueueText)); // These variables are bound due to class reference assignment
            commands.Add(this._commandFactory.Create().Setup("LookupSongDetail", StringFormat.LookupSongDetail));
            commands.Add(this._commandFactory.Create().Setup("BsrSongDetail", StringFormat.BsrSongDetail));
            commands.Add(this._commandFactory.Create().Setup("LinkSonglink", StringFormat.LinkSonglink));
            commands.Add(this._commandFactory.Create().Setup("NextSonglink", StringFormat.NextSonglink));
            commands.Add(this._commandFactory.Create().Setup("SongHintText", StringFormat.SongHintText));
            commands.Add(this._commandFactory.Create().Setup("QueueTextFileFormat", StringFormat.QueueTextFileFormat));
            commands.Add(this._commandFactory.Create().Setup("QueueListRow2", StringFormat.QueueListRow2));
            commands.Add(this._commandFactory.Create().Setup("QueueListFormat", StringFormat.QueueListFormat));
            commands.Add(this._commandFactory.Create().Setup("HistoryListFormat", StringFormat.HistoryListFormat));
            commands.Add(this._commandFactory.Create().Setup("AddSortOrder", StringFormat.AddSortOrder));
            commands.Add(this._commandFactory.Create().Setup("LookupSortOrder", StringFormat.LookupSortOrder)); // -ranking +id , note that +/- are mandatory
            commands.Add(this._commandFactory.Create().Setup("AddSongsSortOrder", StringFormat.AddSongsSortOrder));

            #endregion

            #region SUBCOMMAND Declarations
            // BEGIN SUBCOMMANDS - these modify the Properties of a command, or the current parse state. 
            // sub commands need to have at least one alias that does not begin with an illegal character, or you will not be able to alter them in twitch chat

            commands.Add(this._commandFactory.Create().Setup("/enable", "subcmdenable").Action(this.SubcmdEnable).Help(FlagParameter.Subcmd, "usage: <command>/enable"));
            commands.Add(this._commandFactory.Create().Setup("/disable", "subcmddisable").Action(this.SubcmdDisable).Help(FlagParameter.Subcmd, "usage: <command>/disable"));
            commands.Add(this._commandFactory.Create().Setup("/current", "subcmdcurrent").Action(this.SubcmdCurrentSong).Help(FlagParameter.Subcmd | FlagParameter.Everyone, "usage: <command>/current"));
            commands.Add(this._commandFactory.Create().Setup("/selected", "subcmdselected").Action(this.SubcmdSelected).Help(FlagParameter.Subcmd | FlagParameter.Mod, "usage: <command>/selected"));
            commands.Add(this._commandFactory.Create().Setup("/last", "/previous", "subcmdlast").Action(this.SubcmdPreviousSong).Help(FlagParameter.Subcmd | FlagParameter.Everyone, "usage: <command>/last"));
            commands.Add(this._commandFactory.Create().Setup("/next", "subcmdnext").Action(this.SubcmdNextSong).Help(FlagParameter.Subcmd | FlagParameter.Everyone, "usage: <command>/next"));

            commands.Add(this._commandFactory.Create().Setup("/requestor", "subcmduser").Action(this.SubcmdCurrentUser).Help(FlagParameter.Subcmd | FlagParameter.Everyone, "usage: <command>/requestor"));
            commands.Add(this._commandFactory.Create().Setup("/list", "subcmdlist").Action(this.SubcmdList).Help(FlagParameter.Subcmd | FlagParameter.Mod, "usage: <command>/list"));
            commands.Add(this._commandFactory.Create().Setup("/add", "subcmdadd").Action(this.SubcmdAdd).Help(FlagParameter.Subcmd | FlagParameter.Mod, "usage: <command>/add"));
            commands.Add(this._commandFactory.Create().Setup("/remove", "subcmdremove").Action(this.SubcmdRemove).Help(FlagParameter.Subcmd | FlagParameter.Mod, "usage: <command>/remove"));


            commands.Add(this._commandFactory.Create().Setup("/flags", "subcmdflags").Action(this.SubcmdShowflags).Help(FlagParameter.Subcmd, "usage: <command>/next"));
            commands.Add(this._commandFactory.Create().Setup("/set", "subcmdset").Action(this.SubcmdSetflags).Help(FlagParameter.Subcmd, "usage: <command>/set <flags>"));
            commands.Add(this._commandFactory.Create().Setup("/clear", "subcmdclear").Action(this.SubcmdClearflags).Help(FlagParameter.Subcmd, "usage: <command>/clear <flags>"));

            commands.Add(this._commandFactory.Create().Setup("/allow", "subcmdallow").Action(this.SubcmdAllow).Help(FlagParameter.Subcmd, "usage: <command>/allow"));
            commands.Add(this._commandFactory.Create().Setup("/sethelp", "/helpmsg", "subcmdsethelp").Action(this.SubcmdSethelp).Help(FlagParameter.Subcmd, "usage: <command>/sethelp"));
            commands.Add(this._commandFactory.Create().Setup("/silent", "subcmdsilent").Action(this.SubcmdSilent).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone, "usage: <command>/silent"));

            commands.Add(this._commandFactory.Create().Setup("=", "subcmdequal").Action(this.SubcmdEqual).Help(FlagParameter.Subcmd | FlagParameter.Broadcaster, "usage: ="));

            commands.Add(this._commandFactory.Create().Setup("/alias", "subcmdalias").Action(this.SubcmdAlias).Help(FlagParameter.Subcmd | FlagParameter.Broadcaster, "usage: %alias% %|% Defines all the aliases a command can use"));
            commands.Add(this._commandFactory.Create().Setup("/default", "subcmddefault").Action(this.SubcmdDefault).Help(FlagParameter.Subcmd | FlagParameter.Broadcaster, "usage: <formattext> %alias%"));

            commands.Add(this._commandFactory.Create().Setup("/newest", "subcmdnewest").Action(this.SubcmdNewest).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone));
            commands.Add(this._commandFactory.Create().Setup("/best", "subcmdbest").Action(this.SubcmdBest).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone));
            commands.Add(this._commandFactory.Create().Setup("/oldest", "subcmdoldest").Action(this.SubcmdOldest).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone));
            commands.Add(this._commandFactory.Create().Setup("/pp", "subcmdpp").Action(this.SubcmdPP).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone));


            commands.Add(this._commandFactory.Create().Setup("/top", "subcmdtop").Action(this.SubcmdTop).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Mod | FlagParameter.Broadcaster, "%alias% sets a flag to move the request(s) to the top of the queue."));
            commands.Add(this._commandFactory.Create().Setup("/mod", "subcmdmod").Action(this.SubcmdMod).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Mod | FlagParameter.Broadcaster, "%alias% sets a flag to ignore all filtering"));
            #endregion
            commands.AddRange(this._diContainer.ResolveAll<ISRMCommand>());

            foreach (var item in commands) {
                this.AddAliases(item);
            }
            this.SummarizeCommands(); // Save original command states string.
            #endregion
            try {
                this.CommandConfiguration();
                this.Bot.RunStartupScripts();
                this.Accesslist("whitelist.unique");
                this.Accesslist("blockeduser.unique");
                this.Accesslist("mapperban.list");
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        public void AddAliases(ISRMCommand command)
        {
            foreach (var entry in command.Aliases.Select(x => x.ToLower())) {
                if (string.IsNullOrEmpty(entry)) {
                    continue; // Make sure we don't get a blank command
                }

                this.Aliases.AddOrUpdate(entry, command, (s, c) => command);
            }
        }

        public void SummarizeCommands(StringBuilder target = null, bool everything = true)
        {
            var unique = new SortedDictionary<string, ISRMCommand>();

            if (target == null) {
                target = Commandsummary;
            }

            foreach (var alias in this.Aliases) {
                var BaseKey = alias.Value.Aliases.FirstOrDefault() ?? "";
                if (!unique.ContainsKey(BaseKey)) {
                    unique.Add(BaseKey, alias.Value); // Create a sorted dictionary of each unique command object
                }
            }


            foreach (var entry in unique) {
                var command = entry.Value;

                if (command.Flags.HasFlag(CmdFlags.Dynamic) || command.Flags.HasFlag(CmdFlags.Subcommand)) {
                    continue; // we do not allow customization of Subcommands or dynamic commands at this time
                }

                var cmdname = command.Aliases.FirstOrDefault() ?? "";
                if (everything) {
                    cmdname += new string(' ', 20 - cmdname.Length);
                }

                if (command.Flags.HasFlag(CmdFlags.Variable) && (everything | command.ChangedParameters.HasFlag(ChangedFlags.Variable))) {
                    if (everything) {
                        target.Append("// ");
                    }

                    target.Append($"{cmdname}= {command.UserParameter}\r\n");
                }
                else {
                    if (everything || (command.ChangedParameters & ChangedFlags.Any) != 0) {
                        if (everything) {
                            target.Append("// ");
                        }

                        target.Append($"{cmdname} =");
                        if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Aliases)) {
                            target.Append($" /alias {command.GetAliases()}");
                        }

                        if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Flags)) {
                            target.Append($" /flags {command.GetFlags()}");
                        }

                        if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Help)) {
                            target.Append($" /sethelp {command.GetHelpText()}");
                        }

                        target.Append("\r\n");
                    }
                }
            }
        }

        public void WriteCommandConfiguration(string configfilename = "SRMCommands")
        {
            var UserSettings = new StringBuilder("// This section contains ONLY commands that have changed.\r\n\r\n");

            this.SummarizeCommands(UserSettings, false);

            UserSettings.Append("\r\n");
            UserSettings.Append("// This is a summary of the current command states, these are for reference only. Use the uncommented section for your changes.\r\n\r\n");

            UserSettings.Append(Commandsummary.ToString());
            // BUG: Ok, we should probably just use a text file. But I very 

            UserSettings.Append(
@"//
// The Command name or FIRST alias in the alias list is considered the Base name of the command, and absolutely should not be changed through code. Choose this first name wisely
// We use the Base name to allow user command Customization, it is how the command is identified to the user. You can alter the alias list of the commands in
// the command configuration file(botcommands.ini).
// 
// The file format is as follows:
//  
// < !Base commandname > /alias < alias(s) /flags < command flags > /sethelp Help text
//            
// You only need to change the parts that you wish to modify. Leaving a section out, or blank will result in it being ignored.
// /sethelp MUST be the last section, since it allows command text with /'s, up to and including help messages for /sethelp.
// Command lines with errors will be displayed, possibly ignored. 
//                    
// Examples:
//                   
// !request /alias request bsr add sr /flags Mod Sub VIP Broadcaster /sethelp New help text for request
// !queue /alias queue, requested
// !block /alias block, Ban 
// !lookup /disable
//");
            var filename = Path.Combine(Plugin.DataPath, configfilename + ".ini");
            lock (this) {
                File.WriteAllText(filename, UserSettings.ToString());
            }
        }

        // BUG: This is pass 1, refactoring will get done eventually.
        public void CommandConfiguration(string configfilename = "SRMCommands")
        {

            var UserSettings = new StringBuilder("// This section contains ONLY commands that have changed.\r\n\r\n");

            var filename = Path.Combine(Plugin.DataPath, configfilename + ".ini");
            lock (this) {
                // This is probably just runscript

                try {
                    if (!File.Exists(filename)) {
                        using (File.Create(filename)) {

                        }
                    }

                    using (var sr = new StreamReader(filename)) {
                        while (sr.Peek() >= 0) {
                            var line = sr.ReadLine();
                            line.Trim(' ');
                            if (line.Length < 2 || line.StartsWith("//")) {
                                continue;
                            }

                            UserSettings.Append(line).Append("\r\n");
                            // MAGICALLY configure the customized commands
                            this.Bot.Parse(this.Bot.GetLoginUser(), line, CmdFlags.SilentResult | CmdFlags.Local);
                        }
                        sr.Close();
                    }
                }
                catch (Exception e) {
                    // If it doesn't exist, or ends early, that's fine.
                    Logger.Error(e);
                }
            }
            this.WriteCommandConfiguration();
        }

        // Get help on a command
        internal string Help(ParseState state)
        {
            if (state.Parameter == "") {
                var msg = this._queueFactory.Create();
                msg.Header("Usage: help < ");
                foreach (var entry in this.Aliases) {
                    var botcmd = entry.Value;
                    if (Utility.HasRights(botcmd, state.User, 0) && !botcmd.Flags.HasFlag(FlagParameter.Subcmd) && !botcmd.Flags.HasFlag(FlagParameter.Var)) {
                        msg.Add($"{entry.Key.TrimStart('!')}", " "); // BUG: Removes the built in ! in the commands, letting it slide... for now 
                    }
                }
                msg.Add(">");
                msg.End("...", $"No commands available >");
                return s_success;
            }
            if (this.Aliases.ContainsKey(state.Parameter.ToLower())) {
                var BotCmd = this.Aliases[state.Parameter.ToLower()];
                this.ShowHelpMessage(BotCmd, state.User, true);
            }
            else if (this.Aliases.ContainsKey("!" + state.Parameter.ToLower())) // BUG: Ugly code, gets help on ! version of command
            {
                var BotCmd = this.Aliases["!" + state.Parameter.ToLower()];
                this.ShowHelpMessage(BotCmd, state.User, true);
            }
            else {
                this._chatManager.QueueChatMessage($"Unable to find help for {state.Parameter}.");
            }
            return s_success;
        }



        #region Subcommands
        public string SubcmdEnable(ParseState state)
        {
            state._botcmd.Flags &= ~CmdFlags.Disabled;
            state._botcmd.UpdateCommand(ChangedFlags.Flags);
            this._chatManager.QueueChatMessage($"{state.Command} Enabled.");
            return s_endcommand;
        }

        public string SubcmdNewest(ParseState state)
        {
            state.Flags |= CmdFlags.Autopick;
            state.Sort = "-id -rating";
            return s_success;
        }

        public string SubcmdPP(ParseState state)
        {
            state.Flags |= CmdFlags.Autopick;
            state.Sort = "-pp -rating -id";
            return s_success;
        }


        public string SubcmdBest(ParseState state)
        {
            state.Flags |= CmdFlags.Autopick;
            state.Sort = "-rating -id";
            return s_success;
        }

        public string SubcmdOldest(ParseState state)
        {
            state.Flags |= CmdFlags.Autopick;
            state.Sort = "+id -rating";
            return s_success;
        }



        public string SubcmdDisable(ParseState state)
        {
            state._botcmd.Flags |= CmdFlags.Disabled;
            state._botcmd.UpdateCommand(ChangedFlags.Flags);
            this._chatManager.QueueChatMessage($"{state.Command} Disabled.");
            return s_endcommand;
        }

        public string SubcmdList(ParseState state)
        {
            this.Bot.ListList(state.User, state._botcmd.UserParameter.ToString());
            return s_endcommand;
        }

        public string SubcmdAdd(ParseState state)
        {
            this.Bot.Addtolist(state.User, state._botcmd.UserParameter.ToString() + " " + state.Subparameter);
            return s_endcommand;
        }

        public string SubcmdRemove(ParseState state)
        {
            this.Bot.RemoveFromlist(state.User, state._botcmd.UserParameter.ToString() + " " + state.Subparameter);
            return s_endcommand;
        }


        public string SubcmdCurrentSong(ParseState state)
        {
            try {
                if (state.Parameter != "") {
                    state.Parameter += " ";
                }

                state.Parameter += RequestManager.HistorySongs.FirstOrDefault().SongNode["id"];
                return "";
            }
            catch {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.Error($"Theree is no current song available");
        }

        public string SubcmdSelected(ParseState state)
        {
            try {
                if (state.Parameter != "") {
                    state.Parameter += " ";
                }

                state.Parameter += this.Bot.CurrentSong.SongNode["id"];
                return "";
            }
            catch {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.Error($"Theree is no current song available");
        }



        public string SubcmdCurrentUser(ParseState state)
        {
            try {
                if (state.Parameter != "") {
                    state.Parameter += " ";
                }
                //state.parameter += RequestManager.HistorySongs[0][""];
                return "";
            }
            catch {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.Error($"Theree is no current user available");
        }


        public string SubcmdPreviousSong(ParseState state)
        {
            try {
                if (state.Parameter != "") {
                    state.Parameter += " ";
                }

                state.Parameter += (RequestManager.HistorySongs.GetConsumingEnumerable().ElementAt(1)).SongNode["id"];
                return "";
            }
            catch {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.Error($"Theree is no previous song available");
        }

        public string SubcmdNextSong(ParseState state)
        {
            try {
                if (state.Parameter != "") {
                    state.Parameter += " ";
                }

                state.Parameter += (RequestManager.RequestSongs.FirstOrDefault()).SongNode["id"];
                return "";
            }
            catch {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.Error($"There are no songs in the queue.");
        }


        public string SubcmdShowflags(ParseState state)
        {
            if (state.Subparameter == "") {
                this._chatManager.QueueChatMessage($"{state.Command} flags: {state._botcmd.Flags}");
            }
            else {

                return this.SubcmdSetflags(state);
            }
            return s_endcommand;
        }


        public string SubcmdSetflags(ParseState state)
        {
            try {

                var flags = state.Subparameter.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                var flag = (CmdFlags)Enum.Parse(typeof(CmdFlags), state.Subparameter);
                state._botcmd.Flags |= flag;
                state._botcmd.UpdateCommand(ChangedFlags.Flags);

                if (!state.Flags.HasFlag(CmdFlags.SilentResult)) {
                    this._chatManager.QueueChatMessage($"{state.Command} flags: {state._botcmd.Flags}");
                }
            }
            catch {
                return $"Unable to set  {state.Command} flags to {state.Subparameter}";
            }

            return s_endcommand;
        }

        public string SubcmdClearflags(ParseState state)
        {
            //var flags = state._subparameter.Split(new char[] { ' ', ',' });

            var flag = (CmdFlags)Enum.Parse(typeof(CmdFlags), state.Subparameter);

            state._botcmd.Flags &= ~flag;

            state._botcmd.UpdateCommand(ChangedFlags.Flags);
            if (!state.Flags.HasFlag(CmdFlags.SilentResult)) {
                this._chatManager.QueueChatMessage($"{state.Command} flags: {state._botcmd.Flags}");
            }

            return s_endcommand;
        }


        public string SubcmdAllow(ParseState state)
        {
            // BUG: No parameter checking
            var key = state.Subparameter.ToLower();
            state._botcmd.Permittedusers = key;
            if (!state.Flags.HasFlag(CmdFlags.SilentResult)) {
                this._chatManager.QueueChatMessage($"Permit custom userlist set to  {key}.");
            }

            return s_endcommand;
        }

        public string SubcmdAlias(ParseState state)
        {

            state.Subparameter.ToLower();

            if (state._botcmd.Aliases.Contains(state._botcmd.Aliases.FirstOrDefault() ?? "") || this.Aliases.ContainsKey(state._botcmd.Aliases.FirstOrDefault() ?? "")) {
                foreach (var alias in state._botcmd.Aliases) {
                    this.Aliases.TryRemove(alias, out _);
                }

                state._botcmd.Aliases.Clear();
                state._botcmd.AddAliases(state.Subparameter.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
                state._botcmd.UpdateCommand(ChangedFlags.Aliases);
                this.AddAliases(state._botcmd);
            }
            else {
                return $"Unable to set {state.Command} aliases to {state.Subparameter}";
            }

            return s_endcommand;
        }


        public string SubcmdSethelp(ParseState state)
        {
            state._botcmd.ShortHelp = state.Subparameter + state.Parameter; // This one's different
            state._botcmd.UpdateCommand(ChangedFlags.Help);

            if (!state.Flags.HasFlag(CmdFlags.SilentResult)) {
                this._chatManager.QueueChatMessage($"{state.Command} help: {state._botcmd.ShortHelp}");
            }

            return s_endcommand;
        }


        public string SubcmdSilent(ParseState state)
        {
            state.Flags |= CmdFlags.Silent;
            return s_success;
        }

        public string SubcmdTop(ParseState state)
        {
            state.Flags |= CmdFlags.MoveToTop;
            return s_success;
        }

        public string SubcmdMod(ParseState state)
        {
            state.Flags |= CmdFlags.NoFilter;
            return s_success;
        }


        public string SubcmdEqual(ParseState state)
        {
            state.Flags |= CmdFlags.SilentResult; // Turn off success messages, but still allow errors.

            if (state._botcmd.Flags.HasFlag(CmdFlags.Variable)) {
                state._botcmd.UserParameter.Clear().Append(state.Subparameter + state.Parameter);
                state._botcmd.UpdateCommand(ChangedFlags.Variable);

            }

            return s_endcommand; // This is an assignment, we're not executing the object.
        }


        public string SubcmdDefault(ParseState state)
        {
            if (state._botcmd.Flags.HasFlag(CmdFlags.Variable)) {
                state._botcmd.UserParameter.Clear().Append(state._botcmd.UserString);
                state._botcmd.UpdateCommand(ChangedFlags.Variable);
                return state.Msg($"{state.Command} has been reset to its original value.", s_endcommand);
            }

            return state.Text("You cannot use /default on anything except a Format variable at this time.");
        }

        #endregion

        // A much more general solution for extracting dymatic values into a text string. If we need to convert a text message to one containing local values, but the availability of those values varies by calling location
        // We thus build a table with only those values we have. 

        // BUG: This is actually part of botcmd, please move
        public void ShowHelpMessage(ISRMCommand botcmd, IChatUser user, bool showlong)
        {
            if (botcmd.Flags.HasFlag(CmdFlags.Disabled)) {
                return; // Make sure we're allowed to show help
            }

            this._textFactory.Create().AddUser(user).AddBotCmd(botcmd).QueueMessage(botcmd.ShortHelp, showlong);
            return;
        }

        internal string Accesslist(string request)
        {
            var listname = request.Split('.');

            var req = listname[0];

            if (!this.Aliases.ContainsKey(req)) {

                var cmd = this._commandFactory.Create().Setup('!' + req).Action(this.Bot.Listaccess).Help(FlagParameter.Everyone | CmdFlags.Dynamic, "usage: %alias%   %|%Draws a song from one of the curated `. Does not repeat or conflict.", s_anything).User(request);
                this.AddAliases(cmd);
            }

            return s_success;
        }

        #region List Commands

        internal void ShowCommandlist(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create();

            foreach (var entry in this.Aliases) {
                var botcmd = entry.Value;
                // BUG: Please refactor this its getting too damn long
                if (Utility.HasRights(botcmd, requestor, 0) && !botcmd.Flags.HasFlag(FlagParameter.Var) && !botcmd.Flags.HasFlag(FlagParameter.Subcmd)) {
                    msg.Add($"{entry.Key}", " "); // Only show commands you're allowed to use
                }
            }
            msg.End("...", $"No commands available.");
        }

        internal void ShowFormatList(IChatUser requestor, string request)
        {
            var msg = this._queueFactory.Create();
            foreach (var entry in this.Aliases) {
                var botcmd = entry.Value;
                // BUG: Please refactor this its getting too damn long
                if (Utility.HasRights(botcmd, requestor, 0) && botcmd.Flags.HasFlag(FlagParameter.Var)) {
                    msg.Add($"{entry.Key}", ", "); // Only show commands you're allowed to use
                }
            }
            msg.End("...", $"No commands available.");
        }


        internal async Task LookupSongs(ParseState state)
        {

            var id = this.Bot.GetBeatSaverId(state.Parameter);

            JSONNode result = null;
            var requestUrl = (id != "") ? $"{RequestBot.BEATMAPS_API_ROOT_URL}/maps/id/{id}" : $"{RequestBot.BEATMAPS_API_ROOT_URL}/search/text/0?q={this._normalize.NormalizeBeatSaverString(state.Parameter)}&sortOrder=Relevance";
            var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

            if (resp.IsSuccessStatusCode) {
                result = resp.ConvertToJsonNode();
            }
            else {
                Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
            }
            var filter = SongFilter.none;
            if (state.Flags.HasFlag(CmdFlags.NoFilter)) {
                filter = SongFilter.Queue;
            }

            var songs = this.Bot.GetSongListFromResults(result, state.Parameter, filter, state.Sort != "" ? state.Sort : StringFormat.LookupSortOrder.ToString());

            JSONObject song;

            var msg = this._queueFactory.Create().SetUp(1, 5); // One message maximum, 5 bytes reserved for the ...
            msg.Header($"{songs.Count} found: ");
            foreach (var entry in songs) {
                //entry.Add("pp", 100);
                //SongBrowserPlugin.DataAccess.ScoreSaberDataFile
                song = entry;
                msg.Add(this._textFactory.Create().AddSong(song).Parse(StringFormat.LookupSongDetail), ", ");
            }

            msg.End("...", $"No results for {state.Parameter}");
        }

        // BUG: Should be dynamic text
        public void ListQueue(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create().SetUp(RequestBotConfig.Instance.MaximumQueueMessages);

            foreach (var req in RequestManager.RequestSongs.ToArray()) {
                var song = req.SongNode;
                if (msg.Add(this._textFactory.Create().AddSong(song).Parse(StringFormat.QueueListFormat), ", ")) {
                    break;
                }
            }
            msg.End($" ... and {RequestManager.RequestSongs.Count - msg.Count} more songs.", "Queue is empty.");
            return;

        }

        public void ShowHistory(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create().SetUp(1);

            foreach (var entry in RequestManager.HistorySongs.OfType<SongRequest>()) {
                var song = entry.SongNode;
                if (msg.Add(this._textFactory.Create().AddSong(song).Parse(StringFormat.HistoryListFormat), ", ")) {
                    break;
                }
            }
            msg.End($" ... and {RequestManager.HistorySongs.Count - msg.Count} more songs.", "History is empty.");
            return;
        }

        public void ShowSongsplayed(IChatUser requestor, string request) // Note: This can be spammy.
        {
            var msg = this._queueFactory.Create().SetUp(2);

            msg.Header($"{RequestBot.Played.Count} songs played tonight: ");

            foreach (var song in RequestBot.Played) {
                if (msg.Add(song["songName"].Value + " (" + song["id"] + ")", ", ")) {
                    break;
                }
            }
            msg.End($" ... and {RequestBot.Played.Count - msg.Count} other songs.", "No songs have been played.");
            return;

        }

        public void ShowBanList(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create().SetUp(1);

            msg.Header("Banlist ");

            foreach (var songId in this.Bot.ListCollectionManager.OpenList("banlist.unique").list) {
                if (msg.Add(songId, ", ")) {
                    break;
                }
            }
            msg.End($" ... and {this.Bot.ListCollectionManager.OpenList("banlist.unique").list.Count - msg.Count} more entries.", "is empty.");

        }

        #endregion
    }
}
