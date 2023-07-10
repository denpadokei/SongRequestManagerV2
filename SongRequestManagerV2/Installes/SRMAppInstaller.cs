using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.SimpleJsons;
using SongRequestManagerV2.Utils;
using Zenject;

namespace SongRequestManagerV2.Installes
{
    public class SRMAppInstaller : Installer
    {
        public override void InstallBindings()
        {
            _ = this.Container.BindFactory<QueueLongMessage, QueueLongMessage.QueueLongMessageFactroy>().AsCached();
            _ = this.Container.BindFactory<SongRequest, SongRequest.SongRequestFactory>().AsCached();
            _ = this.Container.BindFactory<ParseState, ParseState.ParseStateFactory>().AsCached();
            _ = this.Container.BindFactory<SRMCommand, SRMCommand.SRMCommandFactory>().AsCached();
            _ = this.Container.BindFactory<JSONObject, string, string, SongMap, SongMap.SongMapFactory>().AsCached();
            _ = this.Container.Bind<MapDatabase>().AsSingle();
            _ = this.Container.Bind<RequestManager>().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<CommandManager>().AsSingle();
            _ = this.Container.BindFactory<DynamicText, DynamicText.DynamicTextFactory>().AsCached();
            _ = this.Container.BindInterfacesAndSelfTo<ChatManager>().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<StringNormalization>().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<NotifySound>().FromNewComponentOnNewGameObject().AsCached();
            _ = this.Container.Bind<ListCollectionManager>().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<RequestBot>().AsSingle();
            _ = this.Container.BindInterfacesAndSelfTo<UpdateChecker>().AsTransient();
        }
    }
}
