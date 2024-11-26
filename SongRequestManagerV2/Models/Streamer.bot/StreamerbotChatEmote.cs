using CatCore.Models.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace SongRequestManagerV2.Models.Streamer.bot
{
    internal class StreamerbotChatEmote : IChatEmote
    {
        public string Id {  get; set; }

        public string Name { get; set; }

        public int StartIndex { get; set; }

        public int EndIndex { get; set; }

        public string Url { get; set; }

        public bool Animated { get; set; }
    }
}
