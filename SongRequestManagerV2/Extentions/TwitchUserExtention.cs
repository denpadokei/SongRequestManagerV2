using ChatCore.Models.Twitch;
using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManager.Extentions
{
    public static class IChatUserExtention
    {
        public static JSONObject ConvertToJsonFromIChatUser(this IChatUser IChatUser)
        {
            JSONObject obj = new JSONObject();
            obj.Add("displayName", new JSONString(IChatUser.Name));
            obj.Add("id", new JSONString(IChatUser.Id));
            obj.Add("color", new JSONString(IChatUser.Color));
            //obj.Add("badges", new JSONString(badges));
            obj.Add("isBroadcaster", new JSONBool(IChatUser.IsBroadcaster));
            obj.Add("isMod", new JSONBool(IChatUser.IsModerator));
            obj.Add("isSub", new JSONBool(IChatUser.IsSubscriber));
            obj.Add("isTurbo", new JSONBool(IChatUser.IsTurbo));
            obj.Add("isVip", new JSONBool(IChatUser.IsVip));
            return obj;
        }

        public static void ConvertToIChatUserFromJson(this IChatUser IChatUser, JSONObject obj)
        {
            IChatUser.Name = obj["displayName"].Value;
            IChatUser.Id = obj["id"].Value;
            IChatUser.Color = obj["color"].Value;
            IChatUser.IsBroadcaster = obj["isBroadcaster"].AsBool;
            IChatUser.IsModerator = obj["isMod"].AsBool;
            IChatUser.IsSubscriber = obj["isSub"].AsBool;
            IChatUser.IsTurbo = obj["isTurbo"].AsBool;
            IChatUser.IsVip = obj["isVip"].AsBool;
        }
    }
}
