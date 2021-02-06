using SiraUtil;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Views;
using Zenject;

namespace SongRequestManagerV2.Installers
{
    public class SRMInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindFactory<KEYBOARD, KEYBOARD.KEYBOARDFactiry>().AsCached();


            Container.Bind<SongListUtils>().AsCached();

            Container.BindInterfacesAndSelfTo<RequestBotListView>().FromNewComponentAsViewController().AsSingle();
            Container.BindInterfacesAndSelfTo<KeyboardViewController>().FromNewComponentAsViewController().AsSingle();
            Container.BindInterfacesAndSelfTo<RequestFlowCoordinator>().FromNewComponentOnNewGameObject(nameof(RequestFlowCoordinator)).AsSingle();
            Container.BindInterfacesAndSelfTo<SRMButton>().FromNewComponentAsViewController().AsSingle().NonLazy();
        }
    }
}
