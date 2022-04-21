using Hive.Versioning;
using IPA.Loader;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Interfaces
{
    internal interface IUpdateChecker
    {
        bool AnyUpdate { get; }
        Version CurrentLatestVersion { get; }
        string DownloadURL { get; }
        Task<bool> CheckUpdate(Version version, string githubURL);
        Task<bool> CheckUpdate(PluginMetadata metadata);
        Task<bool> UpdateMod();
    }
}