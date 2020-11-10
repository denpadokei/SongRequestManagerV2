using BeatSaberMarkupLanguage.Notify;
using SongRequestManagerV2.Bases;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Networks
{
    public class Server : BSBindableBase
    {
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // プロパティ
        /// <summary>ポート を取得、設定</summary>
        private int port_;
        /// <summary>ポート を取得、設定</summary>
        public int Port
        {
            get => this.port_;

            set => this.SetProperty(ref this.port_, value);
        }

        /// <summary>IPアドレス を取得、設定</summary>
        private string ip_;
        /// <summary>IPアドレス を取得、設定</summary>
        public string IP
        {
            get => this.ip_;

            set => this.SetProperty(ref this.ip_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private bool isRunning_;
        /// <summary>説明 を取得、設定</summary>
        public bool IsRunning
        {
            get => this.isRunning_;

            set => this.SetProperty(ref this.isRunning_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string message_;
        /// <summary>説明 を取得、設定</summary>
        public string Message
        {
            get => this.message_;

            set => this.SetProperty(ref this.message_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private byte[] resBytes_;
        /// <summary>説明 を取得、設定</summary>
        public byte[] resBytes
        {
            get => this.resBytes_;

            set => this.SetProperty(ref this.resBytes_, value);
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // コマンド
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // コマンド用メソッド
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // オーバーライドメソッド
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // パブリックメソッド
        public async Task RunServer()
        {
            if (this.IsRunning) {
                return;
            }
            this.IsRunning = true;
            try {
                this._server = new TcpListener(IPAddress.Parse(this.IP), this.Port);
                this._server.Start();
                var enc = Encoding.UTF8;
                await Task.Run(() =>
                {
                    while (this.IsRunning) {
                        var client = this._server.AcceptTcpClient();
                        if (!this.IsRunning) {
                            return;
                        }

                        Logger.Debug("Connect Client.");

                        using (var ns = client.GetStream())
                        using (var ms = new MemoryStream()) {
                            ns.ReadTimeout = 5000;
                            ns.WriteTimeout = 5000;

                            var bytes = new byte[256];

                            do {
                                var size = ns.Read(bytes, 0, bytes.Length);
                                if (size == 0) {
                                    break;
                                }
                                ms.Write(bytes, 0, size);
                            } while (ns.DataAvailable);
                            this.resBytes = ms.GetBuffer();
                            var encType = this.resBytes[10];
                            if (encType == 0) {
                                enc = Encoding.UTF8;
                            }
                            else if (encType == 1) {
                                enc = Encoding.Unicode;
                            }else if(encType == 2) {
                                enc = Encoding.GetEncoding("shift_jis");
                            }
                            this.Message = enc.GetString(ms.GetBuffer(), 15, (int)ms.Length).Replace("。", "").Replace("\0", "");
                            Logger.Debug($"{this.Message}");
                        }
                    }
                });
                this.IsRunning = false;
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
            }
        }

        public void StopServer() {
            this.IsRunning = false;
        }
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // プライベートメソッド
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // メンバ変数
        TcpListener _server;
        #endregion
        //ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*ﾟ+｡｡+ﾟ*｡+ﾟ ﾟ+｡*
        #region // 構築・破棄
        public Server()
        {
            this.IP = "127.0.0.1";
        }
        #endregion
    }
}
