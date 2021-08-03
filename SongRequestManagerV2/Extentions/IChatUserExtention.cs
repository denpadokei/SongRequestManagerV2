using ChatCore.Interfaces;
using ChatCore.Models.Twitch;
using SongRequestManagerV2.SimpleJSON;

namespace SongRequestManagerV2.Extentions
{
    public static class IChatuserExtention
    {
        public static JSONObject CustomToJson(this IChatUser chatUser)
        {
            var obj = new JSONObject();
            obj.Add(nameof(chatUser.Id), new JSONString(chatUser.Id ?? ""));
            obj.Add(nameof(chatUser.UserName), new JSONString(chatUser.UserName ?? ""));
            obj.Add(nameof(chatUser.DisplayName), new JSONString(chatUser.DisplayName ?? ""));
            obj.Add(nameof(chatUser.Color), new JSONString(chatUser.Color ?? ""));
            obj.Add(nameof(chatUser.IsBroadcaster), new JSONBool(chatUser.IsBroadcaster));
            obj.Add(nameof(chatUser.IsModerator), new JSONBool(chatUser.IsModerator));
            var badges = new JSONArray();
            if (chatUser.Badges != null) {
                foreach (var badge in chatUser.Badges) {
                    badges.Add(JSON.Parse(badge.ToJson().ToString()));
                }
                obj.Add(nameof(chatUser.Badges), badges);
            }
            if (chatUser is TwitchUser twitchUser) {
                obj.Add(nameof(twitchUser.IsSubscriber), twitchUser.IsSubscriber);
                obj.Add(nameof(twitchUser.IsTurbo), twitchUser.IsTurbo);
                obj.Add(nameof(twitchUser.IsVip), twitchUser.IsVip);
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
