using ChatCore.Services;
using ChatCore.Services.Twitch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Interfaces
{
    public interface IChatManager : IInitializable
    {
        ChatServiceMultiplexer MultiplexerInstance { get; }
        TwitchService TwitchService { get; }
    }
}
