using SongRequestManagerV2.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SongRequestManagerV2.Installer
{
    public class SRMGameInstaller : Zenject.Installer
    {
        public override void InstallBindings()
        {
            this.Container.BindInterfacesAndSelfTo<SongInfomationProvider>().AsCached().NonLazy();
        }
    }
}
