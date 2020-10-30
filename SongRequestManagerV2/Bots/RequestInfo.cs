using ChatCore.Interfaces;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.Statics;
//using StreamCore.Twitch;
using System;

namespace SongRequestManagerV2
{
    public class RequestInfo
    {
        public IChatUser requestor;
        public string request;
        public bool isBeatSaverId;
        public DateTime requestTime;
        public CmdFlags flags; // Flags for the song request, include things like silence, bypass checks, etc.
        public string requestInfo; // This field contains additional information about a request. This could include the source of the request ( deck, Subscription bonus request) , comments about why a song was banned, etc.
        public ParseState state;

        public RequestInfo(IChatUser requestor, string request, DateTime requestTime, bool isBeatSaverId,  ParseState state, CmdFlags flags = 0,string userstring = "")
        {
            this.requestor = requestor;
            this.request = request;
            this.isBeatSaverId = isBeatSaverId;
            this.requestTime = requestTime;
            this.state = state;
            this.flags = flags;
            this.requestInfo = userstring;
        }
    }
}