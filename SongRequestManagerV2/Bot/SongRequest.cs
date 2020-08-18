using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Models.Twitch;
using ChatCore.SimpleJSON;
using SongRequestManagerV2.Extentions;
//using ChatCore.SimpleJSON;
using System;
using UnityEngine;
using static SongRequestManagerV2.RequestBot;

namespace SongRequestManagerV2
{
    public class SongRequest
    {
        public JSONObject song;
        public IChatUser requestor;
        public DateTime requestTime;
        public RequestStatus status;
        public string requestInfo; // Contains extra song info, Like : Sub/Donation request, Deck pick, Empty Queue pick,Mapper request, etc.

        public SongRequest() { }
        public SongRequest(JSONObject song, IChatUser requestor, DateTime requestTime, RequestStatus status = RequestStatus.Invalid, string requestInfo = "")
        {
            this.song = song;
            this.requestor = requestor;
            this.status = status;
            this.requestTime = requestTime;
            this.requestInfo = requestInfo;
        }

        public JSONObject ToJson()
        {
            try {
                var obj = new JSONObject();
                obj.Add("status", new JSONString(status.ToString()));
                obj.Add("requestInfo", new JSONString(requestInfo));
                obj.Add("time", new JSONString(requestTime.ToFileTime().ToString()));
                obj.Add("requestor", requestor.CustomToJson());
                obj.Add("song", song);
                return obj;
            }
            catch (Exception ex) {
                Plugin.Log($"{ex}\r\n{ex.Message}");
                return null;
            }
        }

        public SongRequest FromJson(JSONNode node)
        {
            requestor = new UnknownChatUser(node["requestor"].Value);
            requestTime = DateTime.FromFileTime(long.Parse(node["time"].Value));
            status = (RequestStatus)Enum.Parse(typeof(RequestStatus), node["status"].Value);
            song = node["song"].AsObject;
            requestInfo = node["requestInfo"].Value;
            return this;
        }
    }
}