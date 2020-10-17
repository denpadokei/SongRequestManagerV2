using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Utils
{
    public class AppInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Plugin.Log("InstallBindings()");
            Container.Bind<RequestBot>().FromNew().AsSingle();
        }
    }
}
