using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using HMUI;
using IPA.Utilities;
using SiraUtil;
using SongRequestManagerV2.UI;
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
            
            Container.BindViewController<YesNoModal>(BeatSaberUI.CreateViewController<YesNoModal>());
            Container.BindViewController<RequestBotListViewController>(BeatSaberUI.CreateViewController<RequestBotListViewController>());
            Container.BindViewController<KeyboardViewController>(BeatSaberUI.CreateViewController<KeyboardViewController>());
            Container.BindFlowCoordinator<RequestFlowCoordinator>(BeatSaberUI.CreateFlowCoordinator<RequestFlowCoordinator>());
            Container.BindViewController<SRMButton>(BeatSaberUI.CreateViewController<SRMButton>());
            Container.Bind<RequestBot>().FromNewComponentOnNewGameObject("SRMBot").AsSingle().NonLazy();
        }

        
    }
}
