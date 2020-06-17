using ChatCore.Interfaces;
using ChatCore.Models.Twitch;
using ChatCore.SimpleJSON;
using JSONArray = ChatCore.SimpleJSON.JSONArray;
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
        public static JSONObject CustomToJson(this IChatUser chatUser)
        {
            JSONObject obj = new JSONObject();
            obj.Add("Id", new JSONString(chatUser.Id ?? ""));
            obj.Add("UserName", new JSONString(chatUser.UserName ?? ""));
            obj.Add("DisplayName", new JSONString(chatUser.DisplayName ?? ""));
            obj.Add("Color", new JSONString(chatUser.Color ?? ""));
            obj.Add("IsBroadcaster", new JSONBool(chatUser.IsBroadcaster));
            obj.Add("IsModerator", new JSONBool(chatUser.IsModerator));
            var badges = new JSONArray();
            if (chatUser.Badges != null) {
                foreach (var badge in chatUser.Badges) {
                    badges.Add(badge.ToJson());
                }
                obj.Add("Badges", badges);
            }
            return obj;
        }

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
