using SongRequestManagerV2.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Installer
{
    public class SRMAppInstaller : Zenject.Installer
    {
        public override void InstallBindings()
        {
            this.Container.BindInterfacesAndSelfTo<ChatManager>().AsSingle();
        }
    }
}
