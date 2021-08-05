using System;

namespace SongRequestManagerV2.Statics
{
    public static class FlagParameter
    {
        public const CmdFlags Default = 0;
        public const CmdFlags Everyone = Default | CmdFlags.Everyone;
        public const CmdFlags Broadcaster = Default | CmdFlags.Broadcaster;
        public const CmdFlags Mod = Default | CmdFlags.Broadcaster | CmdFlags.Mod;
        public const CmdFlags Sub = Default | CmdFlags.Sub;
        public const CmdFlags VIP = Default | CmdFlags.VIP;
        public const CmdFlags Help = CmdFlags.BypassRights;
        public const CmdFlags Silent = CmdFlags.Silent;
        public const CmdFlags Subcmd = CmdFlags.Subcommand | Broadcaster;
        public const CmdFlags Var = CmdFlags.Variable | Broadcaster;
    }

    [Flags]
    public enum RequestStatus
    {
        Invalid = 1,
        Queued = 1 << 2,
        Blacklisted = 1 << 3,
        Skipped = 1 << 4,
        Played = 1 << 5,
        Wrongsong = 1 << 6,
        SongSearch = 1 << 7
    }

    [Flags]
    public enum ListFlags
    {
        ReadOnly = 1,
        InMemory = 2,
        Uncached = 4,
        Dynamic = 8,
        LineSeparator = 16,
        Unchanged = 256
    }

    internal enum MapField
    {
        id,
        version,
        songName,
        songSubName,
        authorName,
        rating,
        hashMd5,
        hashSha1
    }

    internal enum MapStatus
    {
        Uploaded,
        Testplay,
        Published,
        Feedback
    }

    #region COMMANDFLAGS
    [Flags]
    public enum ChangedFlags
    {
        none = 0,
        Aliases = 1,
        Flags = 2,
        Help = 4,
        Allow = 8,
        Any = 15,
        Variable = 16,

        Saved = 1 << 30, // These changes were saved
        All = -1
    }

    [Flags]
    public enum CmdFlags
    {
        None = 0,
        Everyone = 1, // Im
        Sub = 2,
        Mod = 4,
        Broadcaster = 8,
        VIP = 16,
        UserList = 32,  // If this is enabled, users on a list are allowed to use a command (this is an OR, so leave restrictions to Broadcaster if you want ONLY users on a list)
        TwitchLevel = 63, // This is used to show ONLY the twitch user flags when showing permissions

        ShowRestrictions = 64, // Using the command without the right access level will show permissions error. Mostly used for commands that can be unlocked at different tiers.

        BypassRights = 128, // Bypass right check on command, allowing error messages, and a later code based check. Often used for help only commands. 
        NoFilter = 256, // Return no results on failed preflight checks.

        HelpLink = 512, // Enable link to web documentation

        WhisperReply = 1024, // Reply in a whisper to the user (future feature?). Allow commands to send the results to the user, avoiding channel spam

        Timeout = 2048, // Applies a timeout to regular users after a command is succesfully invoked this is just a concept atm
        TimeoutSub = 4096, // Applies a timeout to Subs
        Autopick = 8192, // Auto pick first song when adding
        Local = 16384, // The command is being executed from console and therefore always full priveledge

        NoLinks = 32768, // Turn off any links that the command may normally generate

        //Silent = 65536, // Command produces no output at all - but still executes
        Verbose = 131072, // Turn off command output limits, This can result in excessive channel spam
        Log = 262144, // Log every use of the command to a file
        RegEx = 524288, // Enable regex check
        UserFlag1 = 1048576, // Use it for whatever bit makes you happy 
        NoParameter = 2097152, // The (subcommand) takes no parameter

        Variable = 4194304, // This is a variable 
        Dynamic = 8388608, // This command is generated dynamically, and cannot be saved/loaded 

        ToQueue = 16277216, //  Request is moved directly to queue, bypassing song check

        MoveToTop = 1 << 25, // Private, used by ATT command. Its possible to have multiple aliases for the same flag

        SilentCheck = 1 << 26, // Initial command check failure returns no message
        SilentError = 1 << 27, // Command failure returns no message
        SilentResult = 1 << 28, // Command returns no visible results

        Silent = SilentCheck | SilentError | SilentResult,

        Subcommand = 1 << 29, // This is a subcommand, it may only be invoked within a command

        Disabled = 1 << 30, // If ON, the command will not be added to the alias list at all.

    }
    #endregion

    [Flags] public enum SongFilter { none = 0, Queue = 1, Blacklist = 2, Mapper = 4, Duplicate = 8, Remap = 16, Rating = 32, Duration = 64, NJS = 128, All = -1 };
}
