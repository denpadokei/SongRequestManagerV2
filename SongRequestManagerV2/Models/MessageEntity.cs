using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            Id = "";
            Message = "";
            Sender = new RequesterEntity();
            Channel = new UnknownChatChannel();
            Emotes = new IChatEmote[0];
            Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        public JSONObject ToJson()
        {
            throw new NotImplementedException();
        }
    }
}
