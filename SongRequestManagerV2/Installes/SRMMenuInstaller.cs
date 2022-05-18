using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Views;
using Zenject;

namespace SongRequestManagerV2.Installers
{
    public class SRMMenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            this.Container.BindFactory<Keyboard, Keyboard.KEYBOARDFactiry>().AsCached();


            this.Container.BindInterfacesAndSelfTo<SongListUtils>().AsCached();

            this.Container.BindInterfacesAndSelfTo<RequestBotListView>().FromNewComponentAsViewController().AsSingle();
            this.Container.BindInterfacesAndSelfTo<KeyboardViewController>().FromNewComponentAsViewController().AsSingle();
            this.Container.BindInterfacesAndSelfTo<SongRequestManagerSettings>().FromNewComponentAsViewController().AsSingle();
            this.Container.BindInterfacesAndSelfTo<RequestFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            this.Container.BindInterfacesAndSelfTo<SRMButton>().FromNewComponentAsViewController().AsSingle().NonLazy();
        }
    }
}
