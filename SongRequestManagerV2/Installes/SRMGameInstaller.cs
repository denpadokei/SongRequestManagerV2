using SongRequestManagerV2.Models;

namespace SongRequestManagerV2.Installes
{
    public class SRMGameInstaller : Zenject.Installer
    {
        public override void InstallBindings()
        {
            this.Container.BindInterfacesAndSelfTo<SongInfomationProvider>().AsCached().NonLazy();
        }
    }
}
