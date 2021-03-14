using ChatCore.Interfaces;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Interfaces
{
    public interface ISRMCommand
    {
        Func<ParseState, string> Subcommand { get; }
        Func<ParseState> Subcommand2 { get; }
        Func<ParseState, Task> AsyncSubCommand { get; }

        CmdFlags Flags { set; get; }
        string ShortHelp { set; get; }
        HashSet<string> Aliases { get; }
        Regex Regexfilter { get; }
        string LongHelp { get; }
        string HelpLink { get; }
        string Permittedusers { get; set; }
        StringBuilder UserParameter { get; set; }
        string UserString { get; set; }
        int UserNumber { get; set; }
        int UseCount { get; set; }
        ChangedFlags ChangedParameters { get; set; }

        ISRMCommand AddAliases(IEnumerable<string> aliases);
        ISRMCommand AddAliases(string aliases);
        void Constractor();
        void UpdateCommand(ChangedFlags changed);
        void SetPermittedUsers(string listname);
        string Execute(ParseState state);
        ISRMCommand Setup(string alias);
        ISRMCommand Setup(IEnumerable<string> alias);
        ISRMCommand Setup(string variablename, StringBuilder reference);
        ISRMCommand Action(Func<ParseState, string> action);
        ISRMCommand Action(Func<ParseState> action);
        ISRMCommand AsyncAction(Func<ParseState, Task> action);
        ISRMCommand Help(CmdFlags flags = FlagParameter.Broadcaster, string ShortHelp = "", Regex regexfilter = null);
        ISRMCommand User(string userstring);
        ISRMCommand Action(Action<IChatUser, string, CmdFlags, string> action);
        ISRMCommand Action(Action<IChatUser, string> action);
        ISRMCommand Coroutine(Func<ParseState, IEnumerator> action);
        string GetHelpText();
        string GetFlags();
        string GetAliases();
    }
}