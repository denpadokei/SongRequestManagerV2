using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SiraUtil.Zenject;
using Zenject;
using UnityEngine;
using SongRequestManagerV2.UI;
using SiraUtil;

namespace SongRequestManagerV2.Utils
{
    public class MenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Plugin.Log("InstallBindings()");
            Container.Bind<RequestBotListViewController>().FromNewComponentOnNewGameObject().AsSingle();
            Container.Bind<KeyboardViewController>().FromNewComponentOnNewGameObject().AsSingle();
            Container.Bind<RequestFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            Container.Bind<SRMButton>().FromNewComponentOnNewGameObject().AsSingle();
        }
    }
}
