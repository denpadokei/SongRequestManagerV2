using SongRequestManagerV2.Bases;
using Zenject;

namespace SongRequestManagerV2.Models
{
    #region COMMAND Class
    public class SRMCommand : SRMCommandBase
    {
        public override void Constractor()
        {
        }

        public class SRMCommandFactory : PlaceholderFactory<SRMCommand>
        {

        }
    }
    #endregion
}
