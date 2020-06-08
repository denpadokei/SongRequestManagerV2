using ChatCore.Interfaces;
using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManager.Extentions
{
    public static class IChatuserExtention
    {
        public static JSONObject ToJson(this IChatUser chatUser)
        {
            JSONObject obj = new JSONObject();
            obj.Add("displayName", new JSONString(chatUser.Name));
            obj.Add("id", new JSONString(chatUser.Id));
            obj.Add("color", new JSONString(chatUser.Color));
            obj.Add("isBroadcaster", new JSONBool(chatUser.IsBroadcaster));
            obj.Add("isMod", new JSONBool(chatUser.IsModerator));
            return obj;
        }

        public static void FromJson(this IChatUser chatUser, JSONObject obj)
        {
            chatUser.Name = obj["displayName"].Value;
            chatUser.Id = obj["id"].Value;
            chatUser.Color = obj["color"].Value;
            chatUser.IsBroadcaster = obj["isBroadcaster"].AsBool;
            chatUser.IsModerator = obj["isMod"].AsBool;
        }
    }
}
