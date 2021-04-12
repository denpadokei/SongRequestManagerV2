using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Events;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace SongRequestManagerV2.WebSockets
{
    public class MSJPServer : IDisposable
    {
        private const int port = 50005;
        private WebSocketServer server;
        private bool disposedValue;

        public event Action<string> RecivedMessage;
        private void ReciveMessageHandler(MessageEventArgs e)
        {
            this.RecivedMessage?.Invoke(e.Data);
        }

        public MSJPServer()
        {
            this.server = new WebSocketServer($"ws://localhost:{port}");
            this.server.AddWebSocketService<MSJPBehaviour>("/", i => i.Init(this.ReciveMessageHandler));
            this.server.Start();
        }
        public class MSJPBehaviour : WebSocketBehavior
        {
            public Action<MessageEventArgs> OnReceived;
            public void Init(Action<MessageEventArgs> action)
            {
                Logger.Debug("Init call");
                this.OnReceived = action;
            }
            protected override void OnMessage(MessageEventArgs e)
            {
                if (OnReceived != null) {
                    Logger.Debug($"{e.Data}");
                    OnReceived.Invoke(e);
                }
            }

            protected override void OnClose(CloseEventArgs e)
            {
                this.OnReceived = null;
                base.OnClose(e);
                Logger.Debug($"Closed.");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                }
                this.server.Stop();
                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~MSJPServer()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
