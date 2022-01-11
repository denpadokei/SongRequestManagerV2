using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.SimpleJSON;
using SongRequestManagerV2.Utils;
using UnityEngine;

namespace SongRequestManagerV2.Installer
{
    public class SRMAppInstaller : Zenject.Installer
    {
        public override void InstallBindings()
        {
            this.Container.BindFactory<QueueLongMessage, QueueLongMessage.QueueLongMessageFactroy>().AsCached();
            this.Container.BindFactory<SongRequest, SongRequest.SongRequestFactory>().AsCached();
            this.Container.BindFactory<ParseState, ParseState.ParseStateFactory>().AsCached();
            this.Container.BindFactory<SRMCommand, SRMCommand.SRMCommandFactory>().AsCached();
            this.Container.BindFactory<JSONObject, string, string, SongMap, SongMap.SongMapFactory>().AsCached();
            this.Container.Bind<MapDatabase>().AsSingle();
            this.Container.Bind<RequestManager>().AsSingle();
            this.Container.BindInterfacesAndSelfTo<CommandManager>().AsSingle();
            this.Container.BindFactory<DynamicText, DynamicText.DynamicTextFactory>().AsCached();
            this.Container.BindInterfacesAndSelfTo<ChatManager>().AsSingle();
            this.Container.BindInterfacesAndSelfTo<StringNormalization>().AsSingle();
            this.Container.BindInterfacesAndSelfTo<NotifySound>().FromNewComponentOn(new GameObject(nameof(NotifySound))).AsSingle();
            this.Container.Bind<ListCollectionManager>().AsSingle();
            this.Container.BindInterfacesAndSelfTo<RequestBot>().AsSingle();
        }
    }
}
