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

            Container.BindViewController<RequestBotListView>();
            Container.BindViewController<KeyboardViewController>();
            Container.BindFlowCoordinator<RequestFlowCoordinator>();
            Container.BindViewController<SRMButton>();
        }
    }
}
