using LanPlayServer.Network.Types;

namespace LanPlayServer
{
    public partial class LdnSession
    {
        private void HandleSetAcceptPolicy(LdnHeader header, SetAcceptPolicyRequest policy) 
            => CurrentGame?.HandleSetAcceptPolicy(this, header, policy);

        private void HandleSetAdvertiseData(LdnHeader header, byte[] data) 
            => CurrentGame?.HandleSetAdvertiseData(this, header, data);
    }
}