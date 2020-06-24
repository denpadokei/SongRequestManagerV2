using ChatCore.Interfaces;
using ChatCore.Services.Mixer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Extentions
{
    public static class MixerServiceExtention
    {
        /// <summary>
        /// Mixerにwhisperを送ります。
        /// </summary>
        /// <param name="service"></param>
        /// <param name="message"></param>
        /// <param name="chatUser">Send target user</param>
        /// <param name="chatChannel"></param>
        public static void SendWhisperChat(this MixerService service, string message, IChatUser chatUser, IChatChannel chatChannel)
        {
            service?.SendMixerMessageOfType("whisper", chatChannel.Id, $"[\"{chatUser.UserName}\", \"{ message}\"]");
        }
    }
}
