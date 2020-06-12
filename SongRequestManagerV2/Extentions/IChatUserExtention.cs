using ChatCore.Interfaces;
using ChatCore.Models.Twitch;
using ChatCore.SimpleJSON;
//using ChatCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Extentions
{
    public static class IChatuserExtention
    {
        //public static JSONObject ToJson(this IChatUser chatUser)
        //{
        //    JSONObject obj = new JSONObject();
        //    obj.Add("displayName", new JSONString(chatUser.DisplayName));
        //    obj.Add("id", new JSONString(chatUser.Id));
        //    obj.Add("color", new JSONString(chatUser.Color));
        //    obj.Add("isBroadcaster", new JSONBool(chatUser.IsBroadcaster));
        //    obj.Add("isMod", new JSONBool(chatUser.IsModerator));
        //    return obj;
        //}

        //public static void FromJson(this IChatUser chatUser, JSONObject obj)
        //{
        //    if (chatUser is TwitchUser) {
        //        chatUser = new TwitchUser();
        //    }

        //    chatUser.UserName = obj["displayName"].Value;
        //    chatUser.Id = obj["id"].Value;
        //    chatUser.Color = obj["color"].Value;
        //    chatUser.IsBroadcaster = obj["isBroadcaster"].AsBool;
        //    chatUser.IsModerator = obj["isMod"].AsBool;
        //}
    }
}
