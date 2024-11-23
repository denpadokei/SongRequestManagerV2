using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.SimpleJsons;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Models.Streamer.bot
{
    public class StreamerBotWebSocketClient : IDisposable
    {
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // プロパティ
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // イベント
        public Action<object, string> OnReceivedMessage;
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // コマンド用メソッド
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // オーバーライドメソッド
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // パブリックメソッド
        public void StartClient()
        {
            _semaphore.Wait();
            try {
                Logger.Info("StartClient");
                if (_wokerThread != null) {
                    return;
                }

                _wokerThread = ReceiveProcess();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                _semaphore.Release();
            }
        }

        public async Task StopClient(string message = "OK")
        {
            _semaphore.Wait();
            try {
                Logger.Info("StopClient");
                if (_wokerThread == null) {
                    return;
                }
                this._cts.Cancel();
                this._cts = null;
                if (this._clientWebSocket.State == WebSocketState.Open) {

                    await this._clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, message, CancellationToken.None);
                }
                this._clientWebSocket.Dispose();
                this._clientWebSocket = null;
                //_wokerThread.Wait();
                _wokerThread = null;
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                _semaphore.Release();
            }
        }

        public async Task ReConnect(int millsec = 1000)
        {
            await this.StopClient();
            await Task.Delay(millsec);
            this.StartClient();
        }

        public async Task SendAsync(string message)
        {
            await SendAsync(message, CancellationToken.None);
        }

        public async Task SendAsync(string message, CancellationToken token)
        {
            if (this._clientWebSocket?.State != WebSocketState.Open || string.IsNullOrEmpty(message)) {
                return;
            }
            var json = JSON.Parse(message);
            json["id"] = IndentfyID;
            var seg = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json.ToString()));
            await this._clientWebSocket.SendAsync(seg, WebSocketMessageType.Text, true, token);
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // プライベートメソッド
        private async Task ReceiveProcess()
        {
            Logger.Info("Start websocket");
            //クライアント側のWebSocketを定義
            if (_clientWebSocket != null) {
                await this.StopClient();
            }
            _clientWebSocket = new ClientWebSocket();

            //接続先エンドポイントを指定
            var uri = new Uri($"ws://127.0.0.1:{RequestBotConfig.Instance.StreamerBotWebSocketPort}{RequestBotConfig.Instance.StreamerBotWebSocketEndpoint}");
            _cts = new CancellationTokenSource();

            //サーバに対し、接続を開始
            await _clientWebSocket.ConnectAsync(uri, _cts.Token);
            Logger.Info("Connected websocket");
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(SubRequestMessage)), WebSocketMessageType.Text, true, _cts.Token);
            var requestWait = true;
            var buffer = new byte[1024 * 10];

            //情報取得待ちループ
            while (true) {
                //所得情報確保用の配列を準備
                var segment = new ArraySegment<byte>(buffer);

                //サーバからのレスポンス情報を取得
                var result = await _clientWebSocket.ReceiveAsync(segment, _cts.Token);

                if (_cts.IsCancellationRequested) {
                    await this.StopClient();
                    Logger.Info("Close websoket:1");
                    break;
                }

                //エンドポイントCloseの場合、処理を中断
                if (result.MessageType == WebSocketMessageType.Close) {
                    await this.StopClient();
                    Logger.Info("Close websoket:2");
                    break;
                }

                //バイナリの場合は、当処理では扱えないため、処理を中断
                if (result.MessageType == WebSocketMessageType.Binary) {
                    await this.StopClient("I don't do binary");
                    Logger.Info("Close websoket:3");
                    break;
                }

                //メッセージの最後まで取得
                var count = result.Count;
                var fail = false;
                while (!result.EndOfMessage) {
                    if (count >= buffer.Length) {
                        fail = true;
                        break;
                    }
                    segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                    result = await _clientWebSocket.ReceiveAsync(segment, CancellationToken.None);

                    count += result.Count;
                }
                if (fail) {
                    await this.StopClient("That's too long");
                    Logger.Info("Close websoket:4");
                    break;
                }
                //メッセージを取得
                var message = Encoding.UTF8.GetString(buffer, 0, count);
                Logger.Info(message);
                var jsonObj = JSON.Parse(message);
                if (requestWait && jsonObj.HasKey("status") && string.Equals(jsonObj["status"].Value, "ok", StringComparison.OrdinalIgnoreCase)) {

                    requestWait = false;
                    continue;
                }
                if (jsonObj.HasKey("event") && jsonObj["event"].HasKey("type") && string.Equals(jsonObj["event"]["type"].Value, "ChatMessage")) {
                    Logger.Info("Receive message");
                }
                this.OnReceivedMessage?.Invoke(this, message);
            }
            _ = this.ReConnect();
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // メンバ変数
        internal const string IndentfyID = "C66D170A-C124-458D-8AC5-0BFD9D9A07E0";
        public const string SubRequestMessage = "{" +
            "  \"request\": \"Subscribe\"," +
            "  \"id\": \"" + IndentfyID + "\"," +
            "  \"events\": {" +
            //"    \"Youtube\": [" +
            //"      \"ChatMessage\"" +
            //"    ]" +
            "    \"Twitch\": [" +
            "      \"ChatMessage\"" +
            "    ]" +
            "  }," +
            "}";
        private bool _disposedValue;
        private Task _wokerThread;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private ClientWebSocket _clientWebSocket;
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // 構築・破棄
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue) {
                if (disposing) {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                    _ = this.StopClient();
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                _disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~WebSocketClient()
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
        #endregion
    }
}
