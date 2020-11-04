using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using ChatCore.Utilities;
using HMUI;
using IPA.Utilities;
using SiraUtil;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Models;
using SongRequestManagerV2.UI;
using SongRequestManagerV2.Utils;
using SongRequestManagerV2.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using VRUIControls;
using Zenject;

namespace SongRequestManagerV2.Installers
{
    public class SRMInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindFactory<KEYBOARD, KEYBOARD.KEYBOARDFactiry>().AsCached();
            Container.BindFactory<QueueLongMessage, QueueLongMessage.QueueLongMessageFactroy>().AsCached();
            Container.BindFactory<DynamicText, DynamicText.DynamicTextFactory>().AsCached();
            Container.BindFactory<SongRequest, SongRequest.SongRequestFactory>().AsCached();
            Container.BindFactory<ParseState, ParseState.ParseStateFactory>().AsCached();
            Container.BindFactory<SRMCommand, SRMCommand.SRMCommandFactory>().AsCached();
            Container.BindFactory<JSONObject, string, string, SongMap, SongMap.SongMapFactory>().AsCached();
            
            Container.BindInterfacesAndSelfTo<CommandManager>().AsSingle();
            
            Container.Bind<SongListUtils>().AsCached();
            Container.Bind<MapDatabase>().AsSingle();
            Container.Bind<StringNormalization>().AsSingle();
            Container.Bind<ListCollectionManager>().AsSingle();
            Container.Bind<RequestManager>().AsSingle();

            Container.BindInterfacesAndSelfTo<RequestBot>().FromNewComponentOnNewGameObject("SRMBot").AsCached();

            Container.BindViewController<RequestBotListView>();
            Container.BindViewController<KeyboardViewController>();
            Container.BindFlowCoordinator<RequestFlowCoordinator>();
            Container.BindViewController<SRMButton>();
        }
    }
}
