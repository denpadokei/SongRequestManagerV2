using CatCore.Models.Shared;
using System;

namespace SongRequestManagerV2.Models
{
    internal class GenericChatUser : IChatUser
    {
        public string Id {  get; set; }

        public string UserName { get; set; }

        public string DisplayName { get; set; }

        public string Color { get; set; }

        public bool IsBroadcaster { get; set; }

        public bool IsModerator { get; set; }
        public GenericChatUser(string json)
        {

        }
        public GenericChatUser()
        {

        }
    }
}
