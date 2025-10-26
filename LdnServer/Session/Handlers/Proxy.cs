using LanPlayServer.Network.Types;

namespace LanPlayServer
{
    public partial class LdnSession
    {
        private void HandleProxyDisconnect(LdnHeader header, ProxyDisconnectMessage message)
        {
            CurrentGame?.HandleProxyDisconnect(this, header, message);
        }

        private void HandleExternalProxyState(LdnHeader header, ExternalProxyConnectionState state)
        {
            CurrentGame?.HandleExternalProxyState(this, header, state);
        }

        private void HandleProxyData(LdnHeader header, ProxyDataHeader message, byte[] data)
        {
            CurrentGame?.HandleProxyData(this, header, message, data);
        }

        private void HandleProxyConnectReply(LdnHeader header, ProxyConnectResponse data)
        {
            CurrentGame?.HandleProxyConnectReply(this, header, data);
        }

        private void HandleProxyConnect(LdnHeader header, ProxyConnectRequest message)
        {
            CurrentGame?.HandleProxyConnect(this, header, message);
        }
    }
}