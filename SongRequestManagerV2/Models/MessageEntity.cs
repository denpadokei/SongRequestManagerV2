using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SongRequestManagerV2.Models
{
    public class MessageEntity : IChatMessage
    {
        public string Id { get; set; } = "";

        public bool IsSystemMessage { get; set; }

        public bool IsActionMessage { get; set; }

        public bool IsHighlighted { get; set; }

        public bool IsPing { get; set; }

        public string Message { get; set; } = "";

        public IChatUser Sender { get; set; }

        public IChatChannel Channel { get; set; }

        public IChatEmote[] Emotes { get; set; } = Array.Empty<IChatEmote>();

        public ReadOnlyDictionary<string, string> Metadata { get; set; }

        public MessageEntity()
        {
            this.Id = "";
            this.Message = "";
            this.Channel = new UnknownChatChannel();
            this.Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        public JSONObject ToJson()
        {
            var obj = new JSONObject();
            obj["Id"] = this.Id;
            obj["IsSystemMessage"] = new JSONBool(this.IsSystemMessage);
            obj["IsActionMessage"] = new JSONBool(this.IsActionMessage);
            obj["IsHighlighted"] = new JSONBool(this.IsHighlighted);
            obj["IsPing"] = new JSONBool(this.IsPing);
            obj["Message"] = this.Message;
            obj["Sender"] = this.Sender.ToJson();
            obj["Channel"] = this.Channel.ToJson();
            var array = new JSONArray();
            foreach (var emote in this.Emotes) {
                array.Add(emote.ToJson());
            }
            obj["Emotes"] = array;
            return obj;
            
        }
    }
}
