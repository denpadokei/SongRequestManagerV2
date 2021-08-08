using ChatCore;
using ChatCore.Interfaces;
using ChatCore.Services;
using ChatCore.Services.Twitch;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Interfaces;
using System;
using System.Collections.Concurrent;

namespace SongRequestManagerV2.Utils
{
    public class ChatManager : IChatManager, IDisposable
    {
        private bool disposedValue;

        public ChatCoreInstance CoreInstance { get; private set; }
        public ChatServiceMultiplexer MultiplexerInstance { get; private set; }
        public TwitchService TwitchService { get; private set; }

        public ConcurrentQueue<IChatMessage> RecieveChatMessage { get; } = new ConcurrentQueue<IChatMessage>();
        public ConcurrentQueue<RequestInfo> RequestInfos { get; } = new ConcurrentQueue<RequestInfo>();
        public ConcurrentQueue<string> SendMessageQueue { get; } = new ConcurrentQueue<string>();

        public void Initialize()
        {
            Logger.Debug("Initialize call");
            this.CoreInstance = ChatCoreInstance.Create();
            this.MultiplexerInstance = this.CoreInstance.RunAllServices();
            this.MultiplexerInstance.OnLogin -= this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnLogin += this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnJoinChannel -= this.MultiplexerInstance_OnJoinChannel;
            this.MultiplexerInstance.OnJoinChannel += this.MultiplexerInstance_OnJoinChannel;
            this.TwitchService = this.MultiplexerInstance.GetTwitchService();
            this.MultiplexerInstance.OnTextMessageReceived += this.MultiplexerInstance_OnTextMessageReceived;
        }

        /// <summary>
        /// メッセージを送信キューへ追加します。
        /// </summary>
        /// <param name="message">ストリームサービスへ送信したい文字列</param>
        public void QueueChatMessage(string message) => this.SendMessageQueue.Enqueue($"{RequestBotConfig.Instance.BotPrefix}{message}");

        private void MultiplexerInstance_OnTextMessageReceived(IChatService arg1, IChatMessage arg2) => this.RecieveChatMessage.Enqueue(arg2);

        private void MultiplexerInstance_OnJoinChannel(IChatService arg1, IChatChannel arg2)
        {
            Logger.Debug($"Joined! : [{arg1.DisplayName}][{arg2.Name}]");
            if (arg1 is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }

        private void MultiplexerInstance_OnLogin(IChatService obj)
        {
            Logger.Debug($"Loged in! : [{obj.DisplayName}]");
            if (obj is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue) {
                if (disposing) {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                    Logger.Debug("Dispose call");
                    this.MultiplexerInstance.OnLogin -= this.MultiplexerInstance_OnLogin;
                    this.MultiplexerInstance.OnJoinChannel -= this.MultiplexerInstance_OnJoinChannel;
                    this.MultiplexerInstance.OnTextMessageReceived += this.MultiplexerInstance_OnTextMessageReceived;
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                this.disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~ChatManager()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
