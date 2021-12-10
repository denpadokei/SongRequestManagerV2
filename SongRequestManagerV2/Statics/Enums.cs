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
        /// <summary>
        /// Im
        /// </summary>
        Everyone = 1,
        Sub = 2,
        Mod = 4,
        Broadcaster = 8,
        VIP = 16,
        /// <summary>
        /// If this is enabled, users on a list are allowed to use a command (this is an OR, so leave restrictions to Broadcaster if you want ONLY users on a list)
        /// </summary>
        UserList = 32,
        /// <summary>
        ///  This is used to show ONLY the twitch user flags when showing permissions
        /// </summary>
        TwitchLevel = 63,
        /// <summary>
        /// Using the command without the right access level will show permissions error. Mostly used for commands that can be unlocked at different tiers.
        /// </summary>
        ShowRestrictions = 64,
        /// <summary>
        ///  Bypass right check on command, allowing error messages, and a later code based check. Often used for help only commands. 
        /// </summary>
        BypassRights = 128,
        /// <summary>
        /// Return no results on failed preflight checks.
        /// </summary>
        NoFilter = 256,

        /// <summary>
        /// Enable link to web documentation
        /// </summary>
        HelpLink = 512,
        /// <summary>
        ///  Reply in a whisper to the user (future feature?). Allow commands to send the results to the user, avoiding channel spam
        /// </summary>
        WhisperReply = 1024,
        /// <summary>
        /// Applies a timeout to regular users after a command is succesfully invoked this is just a concept atm
        /// </summary>
        Timeout = 2048,
        /// <summary>
        /// Applies a timeout to regular users after a command is succesfully invoked this is just a concept atm
        /// </summary>
        TimeoutSub = 4096,
        /// <summary>
        /// Auto pick first song when adding
        /// </summary>
        Autopick = 8192,
        /// <summary>
        /// Auto pick first song when adding
        /// </summary>
        Local = 16384,

        /// <summary>
        /// Turn off any links that the command may normally generate
        /// </summary>
        NoLinks = 32768,
        /// <summary>
        /// Command produces no output at all - but still executes
        /// </summary>
        //Silent = 65536,
        /// <summary>
        /// Turn off command output limits, This can result in excessive channel spam
        /// </summary>
        Verbose = 131072,
        /// <summary>
        /// Log every use of the command to a file
        /// </summary>
        Log = 262144,
        /// <summary>
        /// Enable regex check
        /// </summary>
        RegEx = 524288,
        /// <summary>
        /// Use it for whatever bit makes you happy 
        /// </summary>
        UserFlag1 = 1048576,
        /// <summary>
        /// The (subcommand) takes no parameter
        /// </summary>
        NoParameter = 2097152,

        /// <summary>
        /// This is a variable 
        /// </summary>
        Variable = 4194304,
        /// <summary>
        /// This command is generated dynamically, and cannot be saved/loaded 
        /// </summary>
        Dynamic = 8388608,

        /// <summary>
        /// Request is moved directly to queue, bypassing song check
        /// </summary>
        ToQueue = 16277216,

        /// <summary>
        /// Private, used by ATT command. Its possible to have multiple aliases for the same flag
        /// </summary>
        MoveToTop = 1 << 25,

        /// <summary>
        /// Initial command check failure returns no message
        /// </summary>
        SilentCheck = 1 << 26,
        /// <summary>
        /// Command failure returns no message
        /// </summary>
        SilentError = 1 << 27,
        /// <summary>
        /// Command returns no visible results
        /// </summary>
        SilentResult = 1 << 28,

        Silent = SilentCheck | SilentError | SilentResult,

        /// <summary>
        /// This is a subcommand, it may only be invoked within a command
        /// </summary>
        Subcommand = 1 << 29,

        /// <summary>
        /// If ON, the command will not be added to the alias list at all.
        /// </summary>
        Disabled = 1 << 30,
    }
    #endregion

    [Flags] public enum SongFilter { none = 0, Queue = 1, Blacklist = 2, Mapper = 4, Duplicate = 8, Remap = 16, Rating = 32, Duration = 64, NJS = 128, All = -1 };
}
