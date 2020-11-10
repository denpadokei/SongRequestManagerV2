using ChatCore.Utilities;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SongRequestManagerV2
{
    internal class WebResponse
    {
        public readonly HttpStatusCode StatusCode;
        public readonly string ReasonPhrase;
        public readonly HttpResponseHeaders Headers;
        public readonly HttpRequestMessage RequestMessage;
        public readonly bool IsSuccessStatusCode;

        private readonly byte[] _content;

        internal WebResponse(HttpResponseMessage resp, byte[] body)
        {
            StatusCode = resp.StatusCode;
            ReasonPhrase = resp.ReasonPhrase;
            Headers = resp.Headers;
            RequestMessage = resp.RequestMessage;
            IsSuccessStatusCode = resp.IsSuccessStatusCode;

            _content = body;
        }

        public byte[] ContentToBytes() => _content;
        public string ContentToString() => Encoding.UTF8.GetString(_content);
        public JSONNode ConvertToJsonNode()
        {
            return JSONNode.Parse(ContentToString());
        }
    }

    internal static class WebClient
    {
        private static HttpClient _client;
        private static HttpClient Client
        {
            get
            {
                if (_client == null) {
                    Connect();
                }

                return _client;
            }
        }

        private static readonly int RETRY_COUNT = 5;

        private static void Connect()
        {
            try {
                _client?.Dispose();
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
            }
            

            _client = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 15)
            };
            _client.DefaultRequestHeaders.UserAgent.TryParseAdd($"SongRequestManagerV2/{Plugin.Version}");
        }

        internal static async Task<WebResponse> GetAsync(string url, CancellationToken token)
        {
            try {
                return await SendAsync(HttpMethod.Get, url, token);
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
                return null;
            }
        }

        internal static async Task<byte[]> DownloadImage(string url, CancellationToken token)
        {
            try {
                var response = await SendAsync(HttpMethod.Get, url, token);
                if (response?.IsSuccessStatusCode == true) {
                    return response.ContentToBytes();
                }
                return null;
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
                return null;
            }
        }

        internal static async Task<byte[]> DownloadSong(string url, CancellationToken token, IProgress<double> progress = null)
        {
            // check if beatsaver url needs to be pre-pended
            if (!url.StartsWith(@"https://beatsaver.com/"))
            {
                url = $"https://beatsaver.com/{url}";
            }
            try {
                var response = await SendAsync(HttpMethod.Get, url, token, progress: progress);

                if (response?.IsSuccessStatusCode == true) {
                    return response.ContentToBytes();
                }
                return null;
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
                return null;
            }
        }

        internal static async Task<WebResponse> SendAsync(HttpMethod methodType, string url, CancellationToken token, IProgress<double> progress = null)
        {
            Logger.Debug($"{methodType.ToString()}: {url}");
            
            // send request
            try {
                HttpResponseMessage resp = null;
                var retryCount = 0;
                do {
                    try {
                        // create new request messsage
                        var req = new HttpRequestMessage(methodType, url);
                        if (retryCount != 0) {
                            await Task.Delay(1000);
                        }
                        retryCount++;
                        resp = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                        Logger.Debug($"resp code : {resp.StatusCode}");
                    }
                    catch (Exception e) {
                        Logger.Debug($"Error : {e}");
                        Logger.Debug($"{resp?.StatusCode}");
                    }
                } while (resp?.StatusCode != HttpStatusCode.NotFound && resp?.IsSuccessStatusCode != true && retryCount <= RETRY_COUNT);
                

                if (token.IsCancellationRequested) throw new TaskCanceledException();

                using (var memoryStream = new MemoryStream())
                using (var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false)) {
                    var buffer = new byte[8192];
                    var bytesRead = 0; ;

                    long? contentLength = resp?.Content.Headers.ContentLength;
                    var totalRead = 0;

                    // send report
                    progress?.Report(0);

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0) {
                        if (token.IsCancellationRequested) throw new TaskCanceledException();

                        if (contentLength != null) {
                            progress?.Report((double)totalRead / (double)contentLength);
                        }

                        await memoryStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                        totalRead += bytesRead;
                    }

                    progress?.Report(1);
                    byte[] bytes = memoryStream.ToArray();

                    return new WebResponse(resp, bytes);
                }
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
                throw;
            }
        }
    }
}
