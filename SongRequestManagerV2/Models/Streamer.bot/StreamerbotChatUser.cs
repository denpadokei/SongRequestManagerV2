using CatCore.Models.Shared;
using SongRequestManagerV2.SimpleJsons;
using System;
using System.Collections.Generic;
using System.Text;

namespace SongRequestManagerV2.Models.Streamer.bot
{
    internal class StreamerbotChatUser : IChatUser
    {
        public string Id { get; set; }

        public string UserName { get; set; }

        public string DisplayName { get; set; }

        public string Color { get; set; }

        public bool IsBroadcaster { get; set; }

        public bool IsModerator { get; set; }
        public StreamerbotChatUser(string json)
        {
            try {
                var jsonNode = JSON.Parse(json);
                this.Id = jsonNode["userId"]?.Value;
                this.UserName = jsonNode["username"]?.Value;
                this.DisplayName = jsonNode["displayName"]?.Value;
                this.Color = jsonNode["color"]?.Value;
                this.IsBroadcaster = jsonNode["role"]?.AsInt == 4;
                this.IsModerator = jsonNode["role"]?.AsInt == 3;
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}
