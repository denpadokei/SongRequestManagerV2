using ChatCore.Interfaces;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
//using StreamCore.Twitch;
using System;

namespace SongRequestManagerV2
{
    public class RequestInfo
    {
        public IChatUser Requestor { get; set; }
        public string Request { get; set; }
        public bool IsBeatSaverId { get; set; }
        public DateTime RequestTime { get; set; }
        public CmdFlags Flags { get; set; } // Flags for the song request, include things like silence, bypass checks, etc.
        public string RequestInfoText { get; set; } // This field contains additional information about a request. This could include the source of the request ( deck, Subscription bonus request) , comments about why a song was banned, etc.
        public ParseState State { get; set; }
        public bool IsWIP { get; set; }
        public RequestInfo(IChatUser requestor, string request, DateTime requestTime, bool isBeatSaverId, ParseState state, CmdFlags flags = 0, string userstring = "", bool isWip = false)
        {
            this.Requestor = requestor;
            this.Request = request;
            this.IsBeatSaverId = isBeatSaverId;
            this.RequestTime = requestTime;
            this.State = state;
            this.Flags = flags;
            this.RequestInfoText = userstring;
            this.IsWIP = isWip;
        }
    }
}