using LanPlayServer.Network.Types;

namespace LanPlayServer
{
    public partial class LdnSession
    {
        private void HandleDisconnect(LdnHeader header, DisconnectMessage message) => DisconnectFromGame();
    }
}