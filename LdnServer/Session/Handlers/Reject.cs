using LanPlayServer.Network.Types;

namespace LanPlayServer
{
    public partial class LdnSession
    {
        private void HandleReject(LdnHeader header, RejectRequest reject) 
            => CurrentGame?.HandleReject(this, header, reject);
    }
}