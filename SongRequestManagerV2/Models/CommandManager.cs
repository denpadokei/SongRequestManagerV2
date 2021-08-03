using ChatCore.Interfaces;
using SongRequestManagerV2.SimpleJSON;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Interfaces;
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
        private readonly StringNormalization normalize;

        #region common Regex expressions


        private static readonly Regex _alphaNumericRegex = new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled);
        private static readonly Regex _RemapRegex = new Regex("^[0-9a-fA-F]+,[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex _beatsaversongversion = new Regex("^[0-9a-zA-Z]+$", RegexOptions.Compiled);
        private static readonly Regex _nothing = new Regex("$^", RegexOptions.Compiled);
        private static readonly Regex _anything = new Regex(".*", RegexOptions.Compiled); // Is this the most efficient way?
        private static readonly Regex _atleast1 = new Regex("..*", RegexOptions.Compiled); // Allow usage message to kick in for blank 
        private static readonly Regex _fail = new Regex("(?!x)x", RegexOptions.Compiled); // Not sure what the official fastest way to auto-fail a match is, so this will do


        private const string success = "";
        private const string endcommand = "X";
        private const string notsubcommand = "NotSubcmd";

        public static StringBuilder Commandsummary { get; } = new StringBuilder();

        #endregion

        public ConcurrentDictionary<string, ISRMCommand> Aliases { get; private set; } = new ConcurrentDictionary<string, ISRMCommand>(); // There can be only one (static)!

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
            commands.Add(this._commandFactory.Create().Setup(new string[] { "!bsr", "!request", "!add", "!sr", "!srm" }).Action(this.Bot.ProcessSongRequest).Help(FlagParameter.Everyone, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the request queue. Try and be a little specific. You can look up songs on %beatsaver%", _atleast1));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "!lookup", "!find" }).AsyncAction(this.LookupSongs).Help(FlagParameter.Mod | FlagParameter.Sub | FlagParameter.VIP, "usage: %alias%<song name> or <song id>, omit <>'s.%|%Get a list of songs from %beatsaver% matching your search criteria.", _atleast1));

            commands.Add(this._commandFactory.Create().Setup("!link").Action(this.Bot.ShowSongLink).Help(FlagParameter.Everyone, "usage: %alias% %|%... Shows song details, and an %beatsaver% link to the current song", _nothing));

            commands.Add(this._commandFactory.Create().Setup("!open").Action(this.Bot.OpenQueue).Help(FlagParameter.Mod, "usage: %alias%%|%... Opens the queue allowing song requests.", _nothing));
            commands.Add(this._commandFactory.Create().Setup("!close").Action(this.Bot.CloseQueue).Help(FlagParameter.Mod, "usage: %alias%%|%... Closes the request queue.", _nothing));

            commands.Add(this._commandFactory.Create().Setup("!queue").Action(this.ListQueue).Help(FlagParameter.Everyone, "usage: %alias%%|% ... Displays a list of the currently requested songs.", _nothing));
            commands.Add(this._commandFactory.Create().Setup("!played").Action(this.ShowSongsplayed).Help(FlagParameter.Mod, "usage: %alias%%|%... Displays all the songs already played this session.", _nothing));
            commands.Add(this._commandFactory.Create().Setup("!history").Action(this.ShowHistory).Help(FlagParameter.Mod, "usage: %alias% %|% Shows a list of the recently played songs, starting from the most recent.", _nothing));
            commands.Add(this._commandFactory.Create().Setup("!who").Action(this.Bot.Who).Help(FlagParameter.Sub | FlagParameter.VIP | FlagParameter.Mod, "usage: %alias% <songid or name>%|%Find out who requested the song in the currently queue or recent history.", _atleast1));

            commands.Add(this._commandFactory.Create().Setup("!modadd").Action(this.Bot.ModAdd).Help(FlagParameter.Mod, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the request queue. This ignores ALL filters including bans.", _atleast1));
            commands.Add(this._commandFactory.Create().Setup("!mtt").Action(this.Bot.MoveRequestToTop).Help(FlagParameter.Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Moves a song to the top of the request queue.", _atleast1));
            commands.Add(this._commandFactory.Create().Setup("!att").Action(this.Bot.AddToTop).Help(FlagParameter.Mod, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the top of the request queue. Try and be a little specific. You can look up songs on %beatsaver%", _atleast1));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "!last", "!demote", "!later" }).Action(this.Bot.MoveRequestToBottom).Help(FlagParameter.Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Moves a song to the bottom of the request queue.", _atleast1));
            commands.Add(this._commandFactory.Create().Setup("!remove").Action(this.Bot.DequeueSong).Help(FlagParameter.Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Removes a song from the queue.", _atleast1));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "!wrongsong", "!wrong", "!oops" }).Action(this.Bot.WrongSong).Help(FlagParameter.Everyone, "usage: %alias%%|%... Removes your last requested song form the queue. It can be requested again later.", _nothing));

            commands.Add(this._commandFactory.Create().Setup("!unblock").Action(this.Bot.Unban).Help(FlagParameter.Mod, "usage: %alias%<song id>, do not include <,>'s.", _beatsaversongversion));
            commands.Add(this._commandFactory.Create().Setup("!block").AsyncAction(this.Bot.Ban).Help(FlagParameter.Mod, "usage: %alias%<song id>, do not include <,>'s.", _beatsaversongversion));
            commands.Add(this._commandFactory.Create().Setup("!blist").Action(this.ShowBanList).Help(FlagParameter.Broadcaster, "usage: Don't use, it will spam chat!", _atleast1)); // Purposely annoying to use, add a character after the command to make it happen 

            commands.Add(this._commandFactory.Create().Setup("!remap").Action(this.Bot.Remap).Help(FlagParameter.Mod, "usage: %alias%<songid1> , <songid2>%|%... Remaps future song requests of <songid1> to <songid2> , hopefully a newer/better version of the map.", _RemapRegex));
            commands.Add(this._commandFactory.Create().Setup("!unmap").Action(this.Bot.Unmap).Help(FlagParameter.Mod, "usage: %alias%<songid> %|%... Remove future remaps for songid.", _beatsaversongversion));

            commands.Add(this._commandFactory.Create().Setup("!clearqueue").Action(this.Bot.Clearqueue).Help(FlagParameter.Mod, "usage: %alias%%|%... Clears the song request queue. You can still get it back from the JustCleared deck, or the history window", _nothing));
            commands.Add(this._commandFactory.Create().Setup("!clearalreadyplayed").Action(this.Bot.ClearDuplicateList).Help(FlagParameter.Mod, "usage: %alias%%|%... clears the list of already requested songs, allowing them to be requested again.", _nothing)); // Needs a better name
            commands.Add(this._commandFactory.Create().Setup("!restore").Action(this.Bot.Restoredeck).Help(FlagParameter.Mod, "usage: %alias%%|%... Restores the request queue from the previous session. Only useful if you have persistent Queue turned off.", _nothing));

            commands.Add(this._commandFactory.Create().Setup("!about").Help(CmdFlags.Broadcaster | CmdFlags.SilentCheck, $"Song Request Manager version {Plugin.Version}. Github angturil/SongRequestManager", _fail)); // Help commands have no code
            commands.Add(this._commandFactory.Create().Setup(new string[] { "!help" }).Action(this.Help).Help(FlagParameter.Everyone, "usage: %alias%<command name>, or just %alias%to show a list of all commands available to you.", _anything));
            commands.Add(this._commandFactory.Create().Setup("!commandlist").Action(this.ShowCommandlist).Help(FlagParameter.Everyone, "usage: %alias%%|%... Displays all the bot commands available to you.", _nothing));

            commands.Add(this._commandFactory.Create().Setup("!readdeck").Action(this.Bot.Readdeck).Help(FlagParameter.Mod, "usage: %alias", _alphaNumericRegex));
            commands.Add(this._commandFactory.Create().Setup("!writedeck").Action(this.Bot.Writedeck).Help(FlagParameter.Broadcaster, "usage: %alias", _alphaNumericRegex));

            commands.Add(this._commandFactory.Create().Setup("!chatmessage").Action(this.Bot.ChatMessage).Help(FlagParameter.Broadcaster, "usage: %alias%<what you want to say in chat, supports % variables>", _atleast1)); // BUG: Song support requires more intelligent %CurrentSong that correctly handles missing current song. Also, need a function to get the currenly playing song.
            commands.Add(this._commandFactory.Create().Setup("!runscript").Action(this.Bot.RunScript).Help(FlagParameter.Mod, "usage: %alias%<name>%|%Runs a script with a .script extension, no conditionals are allowed. startup.script will run when the bot is first started. Its probably best that you use an external editor to edit the scripts which are located in UserData/StreamCore", _atleast1));

            commands.Add(this._commandFactory.Create().Setup("!formatlist").Action(this.ShowFormatList).Help(FlagParameter.Broadcaster, "Show a list of all the available customizable text format strings. Use caution, as this can make the output of some commands unusable. You can use /default to return a variable to its default setting."));


            commands.Add(this._commandFactory.Create().Setup("!songmsg").Action(this.Bot.SongMsg).Help(FlagParameter.Mod, "usage: %alias% <songid> Message%|% Assign a message to a songid, which will be visible to the player during song selection.", _atleast1));

            commands.Add(this._commandFactory.Create().Setup("!addsongs").AsyncAction(this.Bot.Addsongs).Help(FlagParameter.Broadcaster, "usage: %alias%%|% Add all songs matching a criteria (up to 40) to the queue", _atleast1));

            commands.Add(this._commandFactory.Create().Setup("!every").Action(this.Bot.Every).Help(FlagParameter.Broadcaster, "usage: every <minutes> %|% Run a command every <minutes>.", _atleast1));
            commands.Add(this._commandFactory.Create().Setup("!in").Action(this.Bot.EventIn).Help(FlagParameter.Broadcaster, "usage: in <minutes> <bot command>.", _atleast1));
            commands.Add(this._commandFactory.Create().Setup("!clearevents").Action(this.Bot.ClearEvents).Help(FlagParameter.Broadcaster, "usage: %alias% %|% Clear all timer events."));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "!addnew", "!addlatest" }).AsyncAction(this.Bot.AddsongsFromnewest).Help(FlagParameter.Mod, "usage: %alias% <listname>%|%... Adds the latest maps from %beatsaver%, filtered by the previous selected allowmappers command", _nothing));
            commands.Add(this._commandFactory.Create().Setup("!backup").Action(this.Bot.BackupStreamcore).Help(CmdFlags.Broadcaster, "Backup %SRM% directory.", _anything));

            //new COMMAND("!refreshsongs").Coroutine(RefreshSongs).Help(Broadcaster, "Adds custom songs to bot list. This is a pre-release feature."); // BUG: Broken in 1.10
            commands.Add(this._commandFactory.Create().Setup("!savesongdatabase").Coroutine(this.Bot.SaveSongDatabase).Help(FlagParameter.Broadcaster));

            commands.Add(this._commandFactory.Create().Setup("!queuestatus").Action(this.Bot.QueueStatus).Help(FlagParameter.Mod, "usage: %alias% %|% Show current queue status", _nothing));

            commands.Add(this._commandFactory.Create().Setup("!QueueLottery").Action(this.Bot.QueueLottery).Help(FlagParameter.Broadcaster, "usage: %alias% <entry count> %|% Shuffle the queue and reduce to <entry count> entries. Close the queue.", _anything));

            commands.Add(this._commandFactory.Create().Setup("!addtoqueue").Action(this.Bot.Queuelist).Help(FlagParameter.Broadcaster, "usage: %alias% <list>", _atleast1));


            #region Gamechanger Specific           
            var GameChangerInstalled = IPA.Loader.PluginManager.GetPlugin("Beat Bits") != null;
            //_WobbleInstalled = IPA.Loader.PluginManager.GetPlugin("WobbleSaber") != null;

            if (GameChangerInstalled) {
                commands.Add(this._commandFactory.Create().Setup("!sabotage").Coroutine(this.Bot.SetBombState).Help(FlagParameter.Mod, "Usage: %alias% on/off (LIV Gamechanger only). %|% Turns bombs on and off in Gamechanger."));
            }
            #endregion

#if UNRELEASED
            new COMMAND("!makesearchdeck").AsyncAction(makelistfromsearch).Help(Broadcaster, "usage: %alias%%|% Add all songs matching a criteria to search.deck", _atleast1);

            //new COMMAND("!sdk2test").Action(livsdktest).Help(Broadcaster,"usage: don't",_anything);

            //new COMMAND("!getpp").Coroutine(GetPPData).Help(Broadcaster, "Get PP Data");

            new COMMAND("!downloadsongs").AsyncAction(DownloadEverything).Help(Broadcaster, "Adds custom songs to bot list. This is a pre-release feature.");

            // These comments contain forward looking statement that are absolutely subject to change. I make no commitment to following through
            // on any specific feature,interface or implementation. I do not promise to make them generally available. Its probably best to avoid using or making assumptions based on these.

            new COMMAND("!readarchive").Coroutine(ReadArchive).Help(Broadcaster, "Adds archived sngs to bot");

            new COMMAND("!at").Help(Broadcaster, "Run a command at a certain time.", _atleast1); // BUG: No code
            new COMMAND("!alias").Help(Broadcaster, "usage: %alias %|% Create a command alias, short cuts version a commands. Single line only. Supports %variables% (processed at execution time), parameters are appended.", _atleast1); // BUG: No action

            new COMMAND("!detail"); // Get song details BUG: NO code

            new COMMAND("!allowmappers").Action(MapperAllowList).Help(Broadcaster, "usage: %alias%<mapper list> %|%... Selects the mapper list used by the AddNew command for adding the latest songs from %beatsaver%, filtered by the mapper list.", _alphaNumericRegex);  // The message needs better wording, but I don't feel like it right now
            new COMMAND("!blockmappers").Action(MapperBanList).Help(Broadcaster, "usage: %alias%<mapper list> %|%... Selects a mapper list that will not be allowed in any song requests.", _alphaNumericRegex); // BUG: This code is behind a switch that can't be enabled yet.

            new COMMAND("!mapperdeck").AsyncAction(AddmapperToDeck).Help(Broadcaster, "usage: %alias%<mapperlist>");

            //new COMMAND("!glitter").Action(AddLIVGlitter).Help(Broadcaster, "usage: %alias% <number> Message");

            // These commands will use a completely new format in future builds and rely on a slightly more flexible parser. Commands like userlist.add george, userlist1=userlist2 will be allowed. 

            new COMMAND("!unqueuelist").Action(unqueuelist).Help(Mod, "usage %alias% <list name> %|% Remove any songs on a list from the queue",_atleast1);
            new COMMAND("!openlist").Action(OpenList); // BUG: this command makes  list available.
            new COMMAND("!unload").Action(UnloadList);
            new COMMAND("!clearlist").Action(ClearList);
            new COMMAND("!write").Action(writelist);
            new COMMAND("!list").Action(ListList);
            new COMMAND("!lists").Action(showlists);
            new COMMAND("!addtolist").Action(Addtolist).Help(Broadcaster, "usage: %alias%<list> <value to add>", _atleast1);
            new COMMAND("!removefromlist").Action(RemoveFromlist).Help(Broadcaster, "usage: %alias%<list> <value to add>", _atleast1);
            new COMMAND("!listundo").Action(Addtolist).Help(Broadcaster, "usage: %alias%<list>", _atleast1); // BUG: No function defined yet, undo the last operation

            // Deck related commandws are currently not released

            new COMMAND("!deck").Action(createdeck);
            new COMMAND("!unloaddeck").Action(unloaddeck);
            new COMMAND("!loaddecks").Action(loaddecks);
            new COMMAND("!whatdeck").Action(whatdeck).Help(Mod, "usage: %alias%<songid> or 'current'", _beatsaversongversion);
            new COMMAND("!decklist").Action(decklist).Help(Mod, "usage: %alias", _deck);

            new COMMAND("!unqueuemsg").Help(Broadcaster, "usage: %alias% msg text to match", _atleast1); // BUG: No code

            new COMMAND(new string[] { "/toggle", "subcomdtoggle" }).Action(SubcmdToggle).Help(Subcmd | Mod | CmdFlags.NoParameter, "usage: <!deck> /toggle <songid> %|% Adds a song to a deck if not present, otherwise removes it. Used primarily for button actions");

            new COMMAND("!updatemappers").AsyncAction(UpdateMappers).Help(Broadcaster, "usage: %alias% %|% Update mapper lists/decks. This may take a while, don't do live.");
            new COMMAND("!joinrooms").Coroutine(GetRooms).Help(Broadcaster, "usage: %alias% %|% This is not fully functional, allows the bot to accept commands from your other rooms.") ;
            new COMMAND("!savecommands").Action(SaveCommands);

            new COMMAND("gccount").Action(GetGCCount).Help(Broadcaster);
#endif


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

            commands.Add(this._commandFactory.Create().Setup(new string[] { "/enable", "subcmdenable" }).Action(this.SubcmdEnable).Help(FlagParameter.Subcmd, "usage: <command>/enable"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/disable", "subcmddisable" }).Action(this.SubcmdDisable).Help(FlagParameter.Subcmd, "usage: <command>/disable"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/current", "subcmdcurrent" }).Action(this.SubcmdCurrentSong).Help(FlagParameter.Subcmd | FlagParameter.Everyone, "usage: <command>/current"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/selected", "subcmdselected" }).Action(this.SubcmdSelected).Help(FlagParameter.Subcmd | FlagParameter.Mod, "usage: <command>/selected"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/last", "/previous", "subcmdlast" }).Action(this.SubcmdPreviousSong).Help(FlagParameter.Subcmd | FlagParameter.Everyone, "usage: <command>/last"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/next", "subcmdnext" }).Action(this.SubcmdNextSong).Help(FlagParameter.Subcmd | FlagParameter.Everyone, "usage: <command>/next"));

            commands.Add(this._commandFactory.Create().Setup(new string[] { "/requestor", "subcmduser" }).Action(this.SubcmdCurrentUser).Help(FlagParameter.Subcmd | FlagParameter.Everyone, "usage: <command>/requestor"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/list", "subcmdlist" }).Action(this.SubcmdList).Help(FlagParameter.Subcmd | FlagParameter.Mod, "usage: <command>/list"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/add", "subcmdadd" }).Action(this.SubcmdAdd).Help(FlagParameter.Subcmd | FlagParameter.Mod, "usage: <command>/add"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/remove", "subcmdremove" }).Action(this.SubcmdRemove).Help(FlagParameter.Subcmd | FlagParameter.Mod, "usage: <command>/remove"));


            commands.Add(this._commandFactory.Create().Setup(new string[] { "/flags", "subcmdflags" }).Action(this.SubcmdShowflags).Help(FlagParameter.Subcmd, "usage: <command>/next"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/set", "subcmdset" }).Action(this.SubcmdSetflags).Help(FlagParameter.Subcmd, "usage: <command>/set <flags>"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/clear", "subcmdclear" }).Action(this.SubcmdClearflags).Help(FlagParameter.Subcmd, "usage: <command>/clear <flags>"));

            commands.Add(this._commandFactory.Create().Setup(new string[] { "/allow", "subcmdallow" }).Action(this.SubcmdAllow).Help(FlagParameter.Subcmd, "usage: <command>/allow"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/sethelp", "/helpmsg", "subcmdsethelp" }).Action(this.SubcmdSethelp).Help(FlagParameter.Subcmd, "usage: <command>/sethelp"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/silent", "subcmdsilent" }).Action(this.SubcmdSilent).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone, "usage: <command>/silent"));

            commands.Add(this._commandFactory.Create().Setup(new string[] { "=", "subcmdequal" }).Action(this.SubcmdEqual).Help(FlagParameter.Subcmd | FlagParameter.Broadcaster, "usage: ="));

            commands.Add(this._commandFactory.Create().Setup(new string[] { "/alias", "subcmdalias" }).Action(this.SubcmdAlias).Help(FlagParameter.Subcmd | FlagParameter.Broadcaster, "usage: %alias% %|% Defines all the aliases a command can use"));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/default", "subcmddefault" }).Action(this.SubcmdDefault).Help(FlagParameter.Subcmd | FlagParameter.Broadcaster, "usage: <formattext> %alias%"));

            commands.Add(this._commandFactory.Create().Setup(new string[] { "/newest", "subcmdnewest" }).Action(this.SubcmdNewest).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/best", "subcmdbest" }).Action(this.SubcmdBest).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/oldest", "subcmdoldest" }).Action(this.SubcmdOldest).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/pp", "subcmdpp" }).Action(this.SubcmdPP).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Everyone));


            commands.Add(this._commandFactory.Create().Setup(new string[] { "/top", "subcmdtop" }).Action(this.SubcmdTop).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Mod | FlagParameter.Broadcaster, "%alias% sets a flag to move the request(s) to the top of the queue."));
            commands.Add(this._commandFactory.Create().Setup(new string[] { "/mod", "subcmdmod" }).Action(this.SubcmdMod).Help(FlagParameter.Subcmd | CmdFlags.NoParameter | FlagParameter.Mod | FlagParameter.Broadcaster, "%alias% sets a flag to ignore all filtering"));
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

        public string SaveCommands(ParseState state)
        {
            this.WriteCommandConfiguration();
            return success;
        }

        public void AddAliases(ISRMCommand command)
        {
            foreach (var entry in command.Aliases.Select(x => x.ToLower())) {
                if (string.IsNullOrEmpty(entry))
                    continue; // Make sure we don't get a blank command
                this.Aliases.AddOrUpdate(entry, command, (s, c) => command);
            }
        }

        public void SummarizeCommands(StringBuilder target = null, bool everything = true)
        {
            var unique = new SortedDictionary<string, ISRMCommand>();

            if (target == null)
                target = Commandsummary;

            foreach (var alias in this.Aliases) {
                var BaseKey = alias.Value.Aliases.FirstOrDefault() ?? "";
                if (!unique.ContainsKey(BaseKey))
                    unique.Add(BaseKey, alias.Value); // Create a sorted dictionary of each unique command object
            }


            foreach (var entry in unique) {
                var command = entry.Value;

                if (command.Flags.HasFlag(CmdFlags.Dynamic) || command.Flags.HasFlag(CmdFlags.Subcommand))
                    continue; // we do not allow customization of Subcommands or dynamic commands at this time

                var cmdname = command.Aliases.FirstOrDefault() ?? "";
                if (everything)
                    cmdname += new string(' ', 20 - cmdname.Length);

                if (command.Flags.HasFlag(CmdFlags.Variable) && (everything | command.ChangedParameters.HasFlag(ChangedFlags.Variable))) {
                    if (everything)
                        target.Append("// ");
                    target.Append($"{cmdname}= {command.UserParameter.ToString()}\r\n");
                }
                else {
                    if (everything || (command.ChangedParameters & ChangedFlags.Any) != 0) {
                        if (everything)
                            target.Append("// ");
                        target.Append($"{cmdname} =");
                        if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Aliases))
                            target.Append($" /alias {command.GetAliases()}");
                        if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Flags))
                            target.Append($" /flags {command.GetFlags()}");
                        if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Help))
                            target.Append($" /sethelp {command.GetHelpText()}");
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
                            if (line.Length < 2 || line.StartsWith("//"))
                                continue;

                            UserSettings.Append(line).Append("\r\n");
                            // MAGICALLY configure the customized commands
                            this.Bot.Parse(this.Bot.GetLoginUser(), line, CmdFlags.SilentResult | CmdFlags.Local);
                        }
                        sr.Close();
                    }

                }
                catch {
                    // If it doesn't exist, or ends early, that's fine.
                }
            }

            this.WriteCommandConfiguration();
        }

        // Get help on a command
        internal string Help(ParseState state)
        {
            if (state._parameter == "") {
                var msg = this._queueFactory.Create();
                msg.Header("Usage: help < ");
                foreach (var entry in this.Aliases) {
                    var botcmd = entry.Value;
                    if (Utility.HasRights(botcmd, state._user, 0) && !botcmd.Flags.HasFlag(FlagParameter.Subcmd) && !botcmd.Flags.HasFlag(FlagParameter.Var))

                        msg.Add($"{entry.Key.TrimStart('!')}", " "); // BUG: Removes the built in ! in the commands, letting it slide... for now 
                }
                msg.Add(">");
                msg.End("...", $"No commands available >");
                return success;
            }
            if (this.Aliases.ContainsKey(state._parameter.ToLower())) {
                var BotCmd = this.Aliases[state._parameter.ToLower()];
                this.ShowHelpMessage(BotCmd, state._user, state._parameter, true);
            }
            else if (this.Aliases.ContainsKey("!" + state._parameter.ToLower())) // BUG: Ugly code, gets help on ! version of command
            {
                var BotCmd = this.Aliases["!" + state._parameter.ToLower()];
                this.ShowHelpMessage(BotCmd, state._user, state._parameter, true);
            }
            else {
                this._chatManager.QueueChatMessage($"Unable to find help for {state._parameter}.");
            }
            return success;
        }



        #region Subcommands
        public string SubcmdEnable(ParseState state)
        {
            state._botcmd.Flags &= ~CmdFlags.Disabled;
            state._botcmd.UpdateCommand(ChangedFlags.Flags);
            this._chatManager.QueueChatMessage($"{state._command} Enabled.");
            return endcommand;
        }

        public string SubcmdNewest(ParseState state)
        {
            state._flags |= CmdFlags.Autopick;
            state._sort = "-id -rating";
            return success;
        }

        public string SubcmdPP(ParseState state)
        {
            state._flags |= CmdFlags.Autopick;
            state._sort = "-pp -rating -id";
            return success;
        }


        public string SubcmdBest(ParseState state)
        {
            state._flags |= CmdFlags.Autopick;
            state._sort = "-rating -id";
            return success;
        }

        public string SubcmdOldest(ParseState state)
        {
            state._flags |= CmdFlags.Autopick;
            state._sort = "+id -rating";
            return success;
        }



        public string SubcmdDisable(ParseState state)
        {
            state._botcmd.Flags |= CmdFlags.Disabled;
            state._botcmd.UpdateCommand(ChangedFlags.Flags);
            this._chatManager.QueueChatMessage($"{state._command} Disabled.");
            return endcommand;
        }

        public string SubcmdList(ParseState state)
        {
            this.Bot.ListList(state._user, state._botcmd.UserParameter.ToString());
            return endcommand;
        }

        public string SubcmdAdd(ParseState state)
        {
            this.Bot.Addtolist(state._user, state._botcmd.UserParameter.ToString() + " " + state._subparameter);
            return endcommand;
        }

        public string SubcmdRemove(ParseState state)
        {
            this.Bot.RemoveFromlist(state._user, state._botcmd.UserParameter.ToString() + " " + state._subparameter);
            return endcommand;
        }


        public string SubcmdCurrentSong(ParseState state)
        {
            try {
                if (state._parameter != "")
                    state._parameter += " ";
                state._parameter += (RequestManager.HistorySongs.FirstOrDefault() as SongRequest).SongNode["version"];
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
                if (state._parameter != "")
                    state._parameter += " ";
                state._parameter += this.Bot.CurrentSong.SongNode["version"];
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
                if (state._parameter != "")
                    state._parameter += " ";
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
                if (state._parameter != "")
                    state._parameter += " ";
                state._parameter += (RequestManager.HistorySongs.GetConsumingEnumerable().ElementAt(1) as SongRequest).SongNode["version"];
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
                if (state._parameter != "")
                    state._parameter += " ";
                state._parameter += (RequestManager.RequestSongs.FirstOrDefault() as SongRequest).SongNode["version"];
                return "";
            }
            catch {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.Error($"There are no songs in the queue.");
        }


        public string SubcmdShowflags(ParseState state)
        {
            if (state._subparameter == "") {
                this._chatManager.QueueChatMessage($"{state._command} flags: {state._botcmd.Flags.ToString()}");
            }
            else {

                return this.SubcmdSetflags(state);
            }
            return endcommand;
        }


        public string SubcmdSetflags(ParseState state)
        {
            try {

                var flags = state._subparameter.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                var flag = (CmdFlags)Enum.Parse(typeof(CmdFlags), state._subparameter);
                state._botcmd.Flags |= flag;
                state._botcmd.UpdateCommand(ChangedFlags.Flags);

                if (!state._flags.HasFlag(CmdFlags.SilentResult))
                    this._chatManager.QueueChatMessage($"{state._command} flags: {state._botcmd.Flags.ToString()}");

            }
            catch {
                return $"Unable to set  {state._command} flags to {state._subparameter}";
            }

            return endcommand;
        }

        public string SubcmdClearflags(ParseState state)
        {
            //var flags = state._subparameter.Split(new char[] { ' ', ',' });

            var flag = (CmdFlags)Enum.Parse(typeof(CmdFlags), state._subparameter);

            state._botcmd.Flags &= ~flag;

            state._botcmd.UpdateCommand(ChangedFlags.Flags);
            if (!state._flags.HasFlag(CmdFlags.SilentResult))
                this._chatManager.QueueChatMessage($"{state._command} flags: {state._botcmd.Flags.ToString()}");

            return endcommand;
        }


        public string SubcmdAllow(ParseState state)
        {
            // BUG: No parameter checking
            var key = state._subparameter.ToLower();
            state._botcmd.Permittedusers = key;
            if (!state._flags.HasFlag(CmdFlags.SilentResult))
                this._chatManager.QueueChatMessage($"Permit custom userlist set to  {key}.");
            return endcommand;
        }

        public string SubcmdAlias(ParseState state)
        {

            state._subparameter.ToLower();

            if (state._botcmd.Aliases.Contains(state._botcmd.Aliases.FirstOrDefault() ?? "") || this.Aliases.ContainsKey(state._botcmd.Aliases.FirstOrDefault() ?? "")) {
                foreach (var alias in state._botcmd.Aliases)
                    this.Aliases.TryRemove(alias, out _);
                state._botcmd.Aliases.Clear();
                state._botcmd.AddAliases(state._subparameter.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
                state._botcmd.UpdateCommand(ChangedFlags.Aliases);
                this.AddAliases(state._botcmd);
            }
            else {
                return $"Unable to set {state._command} aliases to {state._subparameter}";
            }

            return endcommand;
        }


        public string SubcmdSethelp(ParseState state)
        {
            state._botcmd.ShortHelp = state._subparameter + state._parameter; // This one's different
            state._botcmd.UpdateCommand(ChangedFlags.Help);

            if (!state._flags.HasFlag(CmdFlags.SilentResult))
                this._chatManager.QueueChatMessage($"{state._command} help: {state._botcmd.ShortHelp}");
            return endcommand;
        }


        public string SubcmdSilent(ParseState state)
        {
            state._flags |= CmdFlags.Silent;
            return success;
        }

        public string SubcmdTop(ParseState state)
        {
            state._flags |= CmdFlags.MoveToTop;
            return success;
        }

        public string SubcmdMod(ParseState state)
        {
            state._flags |= CmdFlags.NoFilter;
            return success;
        }


        public string SubcmdEqual(ParseState state)
        {
            state._flags |= CmdFlags.SilentResult; // Turn off success messages, but still allow errors.

            if (state._botcmd.Flags.HasFlag(CmdFlags.Variable)) {
                state._botcmd.UserParameter.Clear().Append(state._subparameter + state._parameter);
                state._botcmd.UpdateCommand(ChangedFlags.Variable);

            }

            return endcommand; // This is an assignment, we're not executing the object.
        }


        public string SubcmdDefault(ParseState state)
        {
            if (state._botcmd.Flags.HasFlag(CmdFlags.Variable)) {
                state._botcmd.UserParameter.Clear().Append(state._botcmd.UserString);
                state._botcmd.UpdateCommand(ChangedFlags.Variable);
                return state.Msg($"{state._command} has been reset to its original value.", endcommand);
            }

            return state.Text("You cannot use /default on anything except a Format variable at this time.");
        }

        #endregion

        // A much more general solution for extracting dymatic values into a text string. If we need to convert a text message to one containing local values, but the availability of those values varies by calling location
        // We thus build a table with only those values we have. 

        // BUG: This is actually part of botcmd, please move
        public void ShowHelpMessage(ISRMCommand botcmd, IChatUser user, string param, bool showlong)
        {
            if (botcmd.Flags.HasFlag(CmdFlags.Disabled))
                return; // Make sure we're allowed to show help

            this._textFactory.Create().AddUser(user).AddBotCmd(botcmd).QueueMessage(botcmd.ShortHelp, showlong);
            return;
        }

        internal string Accesslist(string request)
        {
            var listname = request.Split('.');

            var req = listname[0];

            if (!this.Aliases.ContainsKey(req)) {

                var cmd = this._commandFactory.Create().Setup('!' + req).Action(this.Bot.Listaccess).Help(FlagParameter.Everyone | CmdFlags.Dynamic, "usage: %alias%   %|%Draws a song from one of the curated `. Does not repeat or conflict.", _anything).User(request);
                this.AddAliases(cmd);
            }

            return success;
        }

        #region List Commands

        internal void ShowCommandlist(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create();

            foreach (var entry in this.Aliases) {
                var botcmd = entry.Value;
                // BUG: Please refactor this its getting too damn long
                if (Utility.HasRights(botcmd, requestor, 0) && !botcmd.Flags.HasFlag(FlagParameter.Var) && !botcmd.Flags.HasFlag(FlagParameter.Subcmd))
                    msg.Add($"{entry.Key}", " "); // Only show commands you're allowed to use
            }
            msg.End("...", $"No commands available.");
        }

        internal void ShowFormatList(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create();

            foreach (var entry in this.Aliases) {
                var botcmd = entry.Value;
                // BUG: Please refactor this its getting too damn long
                if (Utility.HasRights(botcmd, requestor, 0) && botcmd.Flags.HasFlag(FlagParameter.Var))
                    msg.Add($"{entry.Key}", ", "); // Only show commands you're allowed to use
            }
            msg.End("...", $"No commands available.");
        }


        internal async Task LookupSongs(ParseState state)
        {

            var id = this.Bot.GetBeatSaverId(state._parameter);

            JSONNode result = null;

            if (!RequestBotConfig.Instance.OfflineMode) {
                var requestUrl = (id != "") ? $"{RequestBot.BEATMAPS_API_ROOT_URL}/maps/detail/{id}" : $"{RequestBot.BEATMAPS_API_ROOT_URL}/search/text/0?q={this.normalize.NormalizeBeatSaverString(state._parameter)}";
                var resp = await WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode) {
                    result = resp.ConvertToJsonNode();
                }
                else {
                    Logger.Debug($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                }
            }

            var errorMessage = "";
            var filter = SongFilter.none;
            if (state._flags.HasFlag(CmdFlags.NoFilter))
                filter = SongFilter.Queue;
            var songs = this.Bot.GetSongListFromResults(result, state._parameter, ref errorMessage, filter, state._sort != "" ? state._sort : StringFormat.LookupSortOrder.ToString());

            JSONObject song;

            var msg = this._queueFactory.Create().SetUp(1, 5); // One message maximum, 5 bytes reserved for the ...
            msg.Header($"{songs.Count} found: ");
            foreach (var entry in songs) {
                //entry.Add("pp", 100);
                //SongBrowserPlugin.DataAccess.ScoreSaberDataFile

                song = entry;
                msg.Add(this._textFactory.Create().AddSong(ref song).Parse(StringFormat.LookupSongDetail), ", ");
            }

            msg.End("...", $"No results for {state._parameter}");
        }

        // BUG: Should be dynamic text
        public void ListQueue(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create().SetUp(RequestBotConfig.Instance.maximumqueuemessages);

            foreach (SongRequest req in RequestManager.RequestSongs.ToArray()) {
                var song = req.SongNode;
                if (msg.Add(this._textFactory.Create().AddSong(ref song).Parse(StringFormat.QueueListFormat), ", "))
                    break;
            }
            msg.End($" ... and {RequestManager.RequestSongs.Count - msg.Count} more songs.", "Queue is empty.");
            return;

        }

        public void ShowHistory(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create().SetUp(1);

            foreach (var entry in RequestManager.HistorySongs.OfType<SongRequest>()) {
                var song = entry.SongNode;
                if (msg.Add(this._textFactory.Create().AddSong(ref song).Parse(StringFormat.HistoryListFormat), ", "))
                    break;
            }
            msg.End($" ... and {RequestManager.HistorySongs.Count - msg.Count} more songs.", "History is empty.");
            return;

        }

        public void ShowSongsplayed(IChatUser requestor, string request) // Note: This can be spammy.
        {
            var msg = this._queueFactory.Create().SetUp(2);

            msg.Header($"{RequestBot.Played.Count} songs played tonight: ");

            foreach (var song in RequestBot.Played) {
                if (msg.Add(song["songName"].Value + " (" + song["version"] + ")", ", "))
                    break;
            }
            msg.End($" ... and {RequestBot.Played.Count - msg.Count} other songs.", "No songs have been played.");
            return;

        }

        public void ShowBanList(IChatUser requestor, string request)
        {

            var msg = this._queueFactory.Create().SetUp(1);

            msg.Header("Banlist ");

            foreach (var songId in this.Bot.ListCollectionManager.OpenList("banlist.unique").list) {
                if (msg.Add(songId, ", "))
                    break;
            }
            msg.End($" ... and {this.Bot.ListCollectionManager.OpenList("banlist.unique").list.Count - msg.Count} more entries.", "is empty.");

        }

        #endregion
    }
}
