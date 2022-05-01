using CatCore.Models.Shared;
using System;

namespace SongRequestManagerV2.Models
{
    internal class GenericChatUser : IChatUser
    {
        public string Id => throw new NotImplementedException();

        public string UserName => throw new NotImplementedException();

        public string DisplayName => throw new NotImplementedException();

        public string Color => throw new NotImplementedException();

        public bool IsBroadcaster => throw new NotImplementedException();

        public bool IsModerator => throw new NotImplementedException();
        public GenericChatUser(string json)
        {

        }
        public GenericChatUser()
        {

        }
    }
}
