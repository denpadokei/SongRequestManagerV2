using SongRequestManagerV2.Interfaces;
using System.Text;
using Zenject;

namespace SongRequestManagerV2.Bots
{
    public class QueueLongMessage
    {
        private readonly StringBuilder msgBuilder = new StringBuilder();
        private int messageCount = 1;
        private int maxMessages = 2;
        private int maxoverflowtextlength = 60; // We don't know ahead of time, so we're going to do a safe estimate. 

        private int maxoverflowpoint = 0; // The offset in the string where the overflow message needs to go
        private int overflowcount = 0; // We need to save Count
        private int separatorlength = 0;
        public int Count = 0;

        [Inject]
        private readonly IChatManager _chatManager;

        // BUG: This version doesn't reallly strings > twitchmessagelength well, will support
        public QueueLongMessage() // Constructor supports setting max messages
        {

        }

        public QueueLongMessage SetUp(int maximummessageallowed = 2, int maxoverflowtext = 60)
        {
            this.maxMessages = maximummessageallowed;
            this.maxoverflowtextlength = maxoverflowtext;
            return this;
        }

        public void Header(string text)
        {
            _ = this.msgBuilder.Append(text);
        }

        // BUG: Only works form string < MaximumTwitchMessageLength
        public bool Add(string text, string separator = "") // Make sure you use Header(text) for your initial nonlist message, or your displayed message count will be wrong.
        {

            // Save the point where we would put the overflow message
            if (this.messageCount >= this.maxMessages && this.maxoverflowpoint == 0 && this.msgBuilder.Length + text.Length > RequestBot.MaximumTwitchMessageLength - this.maxoverflowtextlength) {
                this.maxoverflowpoint = this.msgBuilder.Length - this.separatorlength;
                this.overflowcount = this.Count;
            }

            if (this.msgBuilder.Length + text.Length > RequestBot.MaximumTwitchMessageLength) {
                this.messageCount++;

                if (this.maxoverflowpoint > 0) {
                    this.msgBuilder.Length = this.maxoverflowpoint;
                    this.Count = this.overflowcount;
                    return true;
                }
                this._chatManager.QueueChatMessage(this.msgBuilder.ToString(0, this.msgBuilder.Length - this.separatorlength));
                _ = this.msgBuilder.Clear();
            }

            this.Count++;
            _ = this.msgBuilder.Append(text);
            _ = this.msgBuilder.Append(separator);
            this.separatorlength = separator.Length;

            return false;
        }

        public void End(string overflowtext = "", string emptymsg = "")
        {
            if (this.Count == 0) {
                this._chatManager.QueueChatMessage(emptymsg); // Note, this means header doesn't get printed either for empty lists                
            }
            else if (this.messageCount > this.maxMessages && this.overflowcount > 0) {
                this._chatManager.QueueChatMessage(this.msgBuilder.ToString() + overflowtext);
            }
            else {
                this.msgBuilder.Length -= this.separatorlength;
                this._chatManager.QueueChatMessage(this.msgBuilder.ToString());
            }

            // Reset the class for reuse
            this.maxoverflowpoint = 0;
            this.messageCount = 1;
            _ = this.msgBuilder.Clear();
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
