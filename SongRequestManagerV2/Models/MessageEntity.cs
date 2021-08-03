using ChatCore.Interfaces;
using ChatCore.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SongRequestManagerV2.Models
{
    public class MessageEntity : IChatMessage
    {
        public string Id { get; set; }

        public bool IsSystemMessage { get; set; }

        public bool IsActionMessage { get; set; }

        public bool IsHighlighted { get; set; }

        public bool IsPing { get; set; }

        public string Message { get; set; }

        public IChatUser Sender { get; set; }

        public IChatChannel Channel { get; set; }

        public IChatEmote[] Emotes { get; set; }

        public ReadOnlyDictionary<string, string> Metadata { get; set; }

        public MessageEntity()
        {
            this.Id = "";
            this.Message = "";
            this.Sender = new RequesterEntity();
            this.Channel = new UnknownChatChannel();
            this.Emotes = new IChatEmote[0];
            this.Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        public ChatCore.Utilities.JSONObject ToJson() => throw new NotImplementedException();
    }
}
