using SongRequestManagerV2.Bots;
using SongRequestManagerV2.UI;
using SongRequestManagerV2.Views;
using Zenject;

namespace SongRequestManagerV2.Installes
{
    public class SRMMenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            _ = this.Container.BindFactory<Keyboard, Keyboard.KEYBOARDFactiry>().AsCached();

            _ = this.Container.BindInterfacesAndSelfTo<SongListUtils>().AsCached();

            _ = this.Container.BindInterfacesAndSelfTo<RequestBotListView>().FromNewComponentAsViewController().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<KeyboardViewController>().FromNewComponentAsViewController().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<SongRequestManagerSettings>().FromNewComponentAsViewController().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<RequestFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<SRMButton>().FromNewComponentAsViewController().AsSingle().NonLazy();
        }
    }
}
