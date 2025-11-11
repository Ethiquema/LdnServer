using LanPlayServer.Network;
using LanPlayServer.Network.Types;
using LanPlayServer.Utils;
using System;

namespace LanPlayServer
{
    public partial class LdnSession
    {
        private void HandleInitialize(LdnHeader header, InitializeMessage message)
        {
            if (_initialized)
            {
                return;
            }

            MacAddress = _tcpServer.MacAddresses.TryFind(Convert.ToHexString(message.Id.AsSpan()), message.MacAddress.AsSpan(), StringId);

            Array16<byte> id = new();
            Convert.FromHexString(StringId).CopyTo(id.AsSpan());

            SendAsync(RyuLdnProtocol.Encode(PacketId.Initialize, new InitializeMessage() { Id = id, MacAddress = MacAddress }));

            _initialized = true;
        }
    }
}