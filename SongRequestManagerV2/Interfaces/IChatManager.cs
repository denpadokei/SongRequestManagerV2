using CatCore;
using CatCore.Models.Twitch.IRC;
using CatCore.Services.Multiplexer;
using CatCore.Services.Twitch.Interfaces;
using SongRequestManagerV2.Bots;
using System.Collections.Concurrent;

namespace SongRequestManagerV2.Interfaces
{
    public interface IChatManager
    {
        CatCoreInstance CoreInstance { get; }
        ChatServiceMultiplexer MultiplexerInstance { get; }
        ConcurrentQueue<MultiplexedMessage> RecieveChatMessage { get; }
        ConcurrentQueue<RequestInfo> RequestInfos { get; }
        ConcurrentQueue<string> SendMessageQueue { get; }
        ITwitchService TwitchService { get; }
        ITwitchChannelManagementService TwitchChannelManagementService { get; }
        ITwitchUserStateTrackerService TwitchUserStateTrackerService { get; }
        TwitchUserState OwnUserData { get; }

        void QueueChatMessage(string message);
    }
}