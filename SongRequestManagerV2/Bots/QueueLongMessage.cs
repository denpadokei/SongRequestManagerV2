using SongRequestManagerV2.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class QueueLongMessage
    {
        private StringBuilder msgBuilder = new StringBuilder();
        private int messageCount = 1;
        private int maxMessages = 2;
        int maxoverflowtextlength = 60; // We don't know ahead of time, so we're going to do a safe estimate. 

        private int maxoverflowpoint = 0; // The offset in the string where the overflow message needs to go
        private int overflowcount = 0; // We need to save Count
        private int separatorlength = 0;
        public int Count = 0;

        [Inject]
        IChatManager _chatManager;

        // BUG: This version doesn't reallly strings > twitchmessagelength well, will support
        public QueueLongMessage() // Constructor supports setting max messages
        {

        }

        public QueueLongMessage SetUp(int maximummessageallowed = 2, int maxoverflowtext = 60)
        {
            maxMessages = maximummessageallowed;
            maxoverflowtextlength = maxoverflowtext;
            return this;
        }

        public void Header(string text)
        {
            msgBuilder.Append(text);
        }

        // BUG: Only works form string < MaximumTwitchMessageLength
        public bool Add(string text, string separator = "") // Make sure you use Header(text) for your initial nonlist message, or your displayed message count will be wrong.
        {

            // Save the point where we would put the overflow message
            if (messageCount >= maxMessages && maxoverflowpoint == 0 && msgBuilder.Length + text.Length > RequestBot.MaximumTwitchMessageLength - maxoverflowtextlength) {
                maxoverflowpoint = msgBuilder.Length - separatorlength;
                overflowcount = Count;
            }

            if (msgBuilder.Length + text.Length > RequestBot.MaximumTwitchMessageLength) {
                messageCount++;

                if (maxoverflowpoint > 0) {
                    msgBuilder.Length = maxoverflowpoint;
                    Count = overflowcount;
                    return true;
                }
                _chatManager.QueueChatMessage(msgBuilder.ToString(0, msgBuilder.Length - separatorlength));
                msgBuilder.Clear();
            }

            Count++;
            msgBuilder.Append(text);
            msgBuilder.Append(separator);
            separatorlength = separator.Length;

            return false;
        }

        public void End(string overflowtext = "", string emptymsg = "")
        {
            if (Count == 0)
                this._chatManager.QueueChatMessage(emptymsg); // Note, this means header doesn't get printed either for empty lists                
            else if (messageCount > maxMessages && overflowcount > 0)
                this._chatManager.QueueChatMessage(msgBuilder.ToString() + overflowtext);
            else {
                msgBuilder.Length -= separatorlength;
                this._chatManager.QueueChatMessage(msgBuilder.ToString());
            }

            // Reset the class for reuse
            maxoverflowpoint = 0;
            messageCount = 1;
            msgBuilder.Clear();
        }

        public class QueueLongMessageFactroy : PlaceholderFactory<QueueLongMessage>
        {
            public override QueueLongMessage Create()
            {
                return base.Create().SetUp();
            }
        }
    }
}
