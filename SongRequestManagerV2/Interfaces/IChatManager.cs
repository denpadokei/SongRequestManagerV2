using ChatCore.Interfaces;
using ChatCore.Services;
using ChatCore.Services.Twitch;
using System.Collections.Concurrent;
using Zenject;

namespace SongRequestManagerV2.Interfaces
{
    public interface IChatManager : IInitializable
    {
        ConcurrentQueue<IChatMessage> RecieveChatMessage { get; }
        ConcurrentQueue<RequestInfo> RequestInfos { get; }
        ConcurrentQueue<string> SendMessageQueue { get; }
        ChatServiceMultiplexer MultiplexerInstance { get; }
        TwitchService TwitchService { get; }
        void QueueChatMessage(string message);
    }
}
