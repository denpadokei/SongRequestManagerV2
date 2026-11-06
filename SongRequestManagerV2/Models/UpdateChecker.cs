using IPA.Loader;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Utils;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace SongRequestManagerV2.Models
{
    internal class UpdateChecker : IInitializable, IUpdateChecker
    {
        private Hive.Versioning.Version _gameVersion;
        private const string s_latest = "/releases";
        private static readonly Regex s_zipFileRegex = new Regex(@"bs([0-9]+\.){2}[0-9]+");
        private static readonly string s_pending = Path.Combine(Environment.CurrentDirectory, "IPA", "Pending");
        private static readonly string[] s_allowedDownloadHosts = {
            "github.com",
            "api.github.com",
            "codeload.github.com",
            "objects.githubusercontent.com",
            "github-releases.githubusercontent.com",
            "release-assets.githubusercontent.com",
        };
        private readonly object _lockObject = new object();
        private string _expectedDigest = "";
        public Hive.Versioning.Version CurrentLatestVersion { get; private set; } = new Hive.Versioning.Version(0, 0, 0);
        public string DownloadURL { get; private set; } = "";
        public bool AnyUpdate { get; private set; } = false;
        public void Initialize()
        {
            this._gameVersion = new Hive.Versioning.Version(Application.version.Replace("_", "-"));
        }

        public Task<bool> CheckUpdate(PluginMetadata metadata)
        {
            return metadata == null ? Task.FromResult(false) : this.CheckUpdate(metadata.HVersion, metadata.PluginSourceLink?.ToString());
        }

        public async Task<bool> CheckUpdate(Hive.Versioning.Version version, string githubURL)
        {
            if (string.IsNullOrEmpty(githubURL)) {
                return false;
            }
            var apiRoot = githubURL.Replace(@"github.com", @"api.github.com/repos");
            this.AnyUpdate = false;
            this.DownloadURL = "";
            this._expectedDigest = "";
            var res = await WebClient.GetAsync($"{apiRoot}{s_latest}", CancellationToken.None);
            if (res == null || !res.IsSuccessStatusCode) {
                return false;
            }
            var releases = res.ConvertToJsonNode();
            if (releases == null) {
                return false;
            }
            foreach (var releseJson in releases.AsArray.Children) {
                var tag = releseJson["tag_name"].Value;
                if (string.IsNullOrEmpty(tag)) {
                    continue;
                }
                var asaets = releseJson["assets"].AsArray;
                foreach (var asset in asaets.Children) {
                    var assetName = asset["name"].Value;
                    if (!IsZipAsset(asset["content_type"].Value, assetName)) {
                        continue;
                    }
                    var fileVersion = s_zipFileRegex.Match(assetName).Value.Replace("bs", "");
                    if (string.IsNullOrEmpty(fileVersion)) {
                        continue;
                    }
                    var fileBSVersion = new Hive.Versioning.Version(fileVersion);
                    if (this._gameVersion < fileBSVersion) {
                        continue;
                    }
                    var downloadUrl = asset["browser_download_url"].Value;
                    if (!IsTrustedDownloadUrl(downloadUrl)) {
                        Logger.Error($"Refusing to download an update from an untrusted host: {downloadUrl}");
                        continue;
                    }
                    this.DownloadURL = downloadUrl;
                    var digest = asset["digest"].Value;
                    this._expectedDigest = digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                        ? digest.Substring("sha256:".Length)
                        : "";
                    break;
                }
                var latestVersion = new Hive.Versioning.Version(tag);
                if (latestVersion <= version || latestVersion.Major != version.Major) {
                    continue;
                }
                else {
                    this.CurrentLatestVersion = latestVersion;
                    this.AnyUpdate = true;
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> UpdateMod()
        {
            if (!this.AnyUpdate) {
                return true;
            }
            if (string.IsNullOrEmpty(this.DownloadURL)) {
                Logger.Error("No compatible update archive was found for this game version.");
                return false;
            }
            var zip = await WebClient.GetAsync(this.DownloadURL, CancellationToken.None);
            if (zip == null || !zip.IsSuccessStatusCode) {
                return false;
            }
            var content = zip.ContentToBytes();
            if (string.IsNullOrEmpty(this._expectedDigest)) {
                Logger.Error("The release does not publish a SHA-256 digest for this asset, so the update was rejected.");
                return false;
            }
            string actualDigest;
            using (var sha256 = SHA256.Create()) {
                actualDigest = BitConverter.ToString(sha256.ComputeHash(content)).Replace("-", "");
            }
            if (!string.Equals(actualDigest, this._expectedDigest, StringComparison.OrdinalIgnoreCase)) {
                Logger.Error($"Update digest mismatch. Expected {this._expectedDigest}, got {actualDigest}.");
                return false;
            }
            try {
                lock (this._lockObject) {
                    var tmpFolder = Path.GetTempPath();
                    tmpFolder = Path.Combine(tmpFolder, Guid.NewGuid().ToString());
                    using (var ms = new MemoryStream(content))
                    using (var archive = new ZipArchive(ms)) {
                        Utility.ExtractZipSafely(archive, tmpFolder);
                    }
                    this.CopyDirectory(tmpFolder, s_pending);
                    Directory.Delete(tmpFolder, true);
                }
            }
            catch (Exception e) {
                Logger.Error(e);
                return false;
            }
            this.AnyUpdate = false;
            return true;
        }

        private static bool IsZipAsset(string contentType, string name)
        {
            return string.Equals(contentType, "application/zip", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/x-zip-compressed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
                || (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTrustedDownloadUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) {
                return false;
            }

            return s_allowedDownloadHosts.Any(host =>
                string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(destDir) || !Directory.Exists(sourceDir)) {
                return;
            }
            if (!Directory.Exists(destDir)) {
                _ = Directory.CreateDirectory(destDir);
            }
            foreach (var file in Directory.EnumerateFiles(sourceDir)) {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.EnumerateDirectories(sourceDir)) {
                this.CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }
    }
}
