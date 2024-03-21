using SiraUtil.Interfaces;
using Zenject;

namespace SongRequestManagerV2.Localizes
{
    /// <summary>
    /// SiraLocalizer復活したらこっちでの実装も考える。
    /// </summary>
    public class LocalizeController : IInitializable
    {
        private readonly ILocalizer _localizer;

        public LocalizeController([InjectOptional(Id = "SIRA.Localizer")] ILocalizer localizer)
        {
            this._localizer = localizer;
        }

        public void Initialize()
        {
            Logger.Debug($"{this._localizer}:{this._localizer.GetType()}");
            _ = (this._localizer?.AddLocalizationSheetFromAssembly("SongRequestManagerV2.Resources.localize.csv", BGLib.Polyglot.GoogleDriveDownloadFormat.CSV));
        }
    }
}
