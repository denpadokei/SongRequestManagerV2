using CatCore.Models.Shared;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.SimpleJsons;
using System;

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
        public IChatEmote[] Emotes { get; private set; }

        public bool IsMentioned => throw new NotImplementedException();

        public MessageEntity()
        {
            this.Id = "";
            this.Message = "";
            this.Sender = new GenericChatUser();
            //this.Channel = new UnknownChatChannel();
            this.Emotes = new IChatEmote[0];
            //this.Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        public JSONObject ToJson()
        {
            throw new NotImplementedException();
        }
    }
}
