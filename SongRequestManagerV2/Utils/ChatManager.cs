using CatCore;
using CatCore.Models.Twitch.IRC;
using CatCore.Services.Multiplexer;
using CatCore.Services.Twitch.Interfaces;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Interfaces;
using System;
using System.Collections.Concurrent;
using Zenject;

namespace SongRequestManagerV2.Utils
{
    public class ChatManager : IDisposable, IInitializable, IChatManager
    {
        private bool _disposedValue;

        public CatCoreInstance CoreInstance { get; private set; }
        public ChatServiceMultiplexer MultiplexerInstance { get; private set; }
        public ITwitchService TwitchService { get; private set; }
        public MultiplexedChannel Channel { get; private set; }
        public ConcurrentQueue<MultiplexedMessage> RecieveChatMessage { get; } = new ConcurrentQueue<MultiplexedMessage>();
        public ConcurrentQueue<RequestInfo> RequestInfos { get; } = new ConcurrentQueue<RequestInfo>();
        public ConcurrentQueue<string> SendMessageQueue { get; } = new ConcurrentQueue<string>();
        public ITwitchChannelManagementService TwitchChannelManagementService { get; private set; }
        public ITwitchUserStateTrackerService TwitchUserStateTrackerService { get; private set; }
        public TwitchUserState OwnUserData { get; private set; }

        public void Initialize()
        {
            Logger.Debug("Initialize call");
            try {
                this.CoreInstance = CatCoreInstance.Create();
                this.MultiplexerInstance = this.CoreInstance.RunAllServices();
                this.MultiplexerInstance.OnTextMessageReceived += this.MultiplexerInstance_OnTextMessageReceived;
                this.MultiplexerInstance.OnChatConnected += this.MultiplexerInstance_OnChatConnected;
                this.TwitchService = this.MultiplexerInstance.GetTwitchPlatformService();
                this.TwitchChannelManagementService = this.TwitchService?.GetChannelManagementService();
                this.TwitchUserStateTrackerService = this.TwitchService?.GetUserStateTrackerService();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        private void MultiplexerInstance_OnChatConnected(MultiplexedPlatformService obj)
        {
            this.Channel = obj.DefaultChannel;
            this.OwnUserData = this.TwitchUserStateTrackerService?.GetUserState(this.Channel.Id);
        }

        private void MultiplexerInstance_OnTextMessageReceived(MultiplexedPlatformService arg1, MultiplexedMessage arg2)
        {
            this.RecieveChatMessage.Enqueue(arg2);
        }

        /// <summary>
        /// メッセージを送信キューへ追加します。
        /// </summary>
        /// <param name="message">ストリームサービスへ送信したい文字列</param>
        public void QueueChatMessage(string message)
        {
            this.SendMessageQueue.Enqueue($"{RequestBotConfig.Instance.BotPrefix}{message}");
        }

        //private void MultiplexerInstance_OnTextMessageReceived(IChatService arg1, IChatMessage arg2)
        //{
        //    this.RecieveChatMessage.Enqueue(arg2);
        //}

        //private void MultiplexerInstance_OnJoinChannel(IChatService arg1, IChatChannel arg2)
        //{
        //    Logger.Debug($"Joined! : [{arg1.DisplayName}][{arg2.Name}]");
        //    if (arg1 is TwitchService twitchService) {
        //        this.TwitchService = twitchService;
        //    }
        //}

        //private void MultiplexerInstance_OnLogin(IChatService obj)
        //{
        //    Logger.Debug($"Loged in! : [{obj.DisplayName}]");
        //    if (obj is TwitchService twitchService) {
        //        this.TwitchService = twitchService;
        //    }
        //}

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposedValue) {
                if (disposing) {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                    Logger.Debug("Dispose call");
                    this.MultiplexerInstance.OnTextMessageReceived -= this.MultiplexerInstance_OnTextMessageReceived;
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                this._disposedValue = true;
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
