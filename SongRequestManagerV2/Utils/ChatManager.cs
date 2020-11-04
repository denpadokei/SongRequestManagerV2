using ChatCore;
using ChatCore.Interfaces;
using ChatCore.Services;
using ChatCore.Services.Twitch;
using SongRequestManagerV2.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Utils
{
    public class ChatManager : IChatManager
    {
        public ChatCoreInstance CoreInstance { get; private set; }
        public ChatServiceMultiplexer MultiplexerInstance { get; private set; }
        public TwitchService TwitchService { get; private set; }

        public void Initialize()
        {
            this.CoreInstance = ChatCoreInstance.Create();
            this.MultiplexerInstance = this.CoreInstance.RunAllServices();
            this.MultiplexerInstance.OnLogin -= this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnLogin += this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnJoinChannel -= this.MultiplexerInstance_OnJoinChannel;
            this.MultiplexerInstance.OnJoinChannel += this.MultiplexerInstance_OnJoinChannel;
            this.TwitchService = this.MultiplexerInstance.GetTwitchService();
        }

        void MultiplexerInstance_OnJoinChannel(IChatService arg1, IChatChannel arg2)
        {
            Plugin.Log($"Joined! : [{arg1.DisplayName}][{arg2.Name}]");
            if (arg1 is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }

        void MultiplexerInstance_OnLogin(IChatService obj)
        {
            Plugin.Log($"Loged in! : [{obj.DisplayName}]");
            if (obj is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }
    }
}
