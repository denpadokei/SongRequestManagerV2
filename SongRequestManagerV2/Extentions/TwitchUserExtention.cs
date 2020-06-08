using ChatCore.Models.Twitch;
using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Extentions
{
    public static class TwitchUserExtention
    {
        public static JSONObject ConvertToJsonFromTwitchUser(this TwitchUser twitchUser)
        {
            JSONObject obj = new JSONObject();
            obj.Add("displayName", new JSONString(twitchUser.Name));
            obj.Add("id", new JSONString(twitchUser.Id));
            obj.Add("color", new JSONString(twitchUser.Color));
            //obj.Add("badges", new JSONString(badges));
            obj.Add("isBroadcaster", new JSONBool(twitchUser.IsBroadcaster));
            obj.Add("isMod", new JSONBool(twitchUser.IsModerator));
            obj.Add("isSub", new JSONBool(twitchUser.IsSubscriber));
            obj.Add("isTurbo", new JSONBool(twitchUser.IsTurbo));
            obj.Add("isVip", new JSONBool(twitchUser.IsVip));
            return obj;
        }

        public static void ConvertToTwitchUserFromJson(this TwitchUser twitchUser, JSONObject obj)
        {
            twitchUser.Name = obj["displayName"].Value;
            twitchUser.Id = obj["id"].Value;
            twitchUser.Color = obj["color"].Value;
            twitchUser.IsBroadcaster = obj["isBroadcaster"].AsBool;
            twitchUser.IsModerator = obj["isMod"].AsBool;
            twitchUser.IsSubscriber = obj["isSub"].AsBool;
            twitchUser.IsTurbo = obj["isTurbo"].AsBool;
            twitchUser.IsVip = obj["isVip"].AsBool;
        }
    }
}
