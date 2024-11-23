using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.SimpleJsons;
using System;
using System.Collections.Generic;
using System.Text;

namespace SongRequestManagerV2.Models.Streamer.bot
{
    public class StreamerbotMessageParser
    {
        public static IChatMessage MessagePaese(string json)
        {
            var jsonObj = JSON.Parse(json);
            jsonObj = jsonObj["data"]["message"];
            var result = new MessageEntity();
            result.Sender = new StreamerbotChatUser(jsonObj.ToString());
            result.Id = jsonObj["msgId"].Value;
            result.Message = jsonObj["message"].Value;
            var emotes = jsonObj["emotes"].AsArray;
            var emoteList = new List<StreamerbotChatEmote>();
            foreach (var emote in emotes) {
                var sbemote = new StreamerbotChatEmote();
                sbemote.Id = emote.Value["id"].Value;
                sbemote.Name = emote.Value["name"].Value;
                sbemote.StartIndex = emote.Value["startIndex"].AsInt;
                sbemote.EndIndex = emote.Value["endIndex"].AsInt;
                sbemote.Url = emote.Value["imageUrl"].Value;
            }
            result.Emotes = emoteList.ToArray(); 
            return result;
        }


        public static IEnumerable<JSONObject> CreateSendCommentActionJson(string comment)
        {
            foreach (var p in RequestBotConfig.Instance.UsePlatform) {
                var json = new JSONObject();
                json["request"] = "DoAction";

                var actionNode = new JSONObject();
                actionNode["id"] = RequestBotConfig.Instance.SendChatActionGUID;
                actionNode["name"] = RequestBotConfig.Instance.SendChatActionName;
                json.Add("action", actionNode);

                var argsNode = new JSONObject();
                argsNode["userType"] = p.ToString().ToLower();
                argsNode["messageOutput"] = comment;
                json.Add("args", argsNode);
                yield return json;
            }
            
        }
    }
}
