using LanPlayServer.Network;
using LanPlayServer.Network.Types;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Threading;

namespace LanPlayServer
{
    public partial class LdnSession
    {
        private void HandleScan(LdnHeader ldnPacket, ScanFilter filter)
        {
            Thread.Sleep(200);
            int games = _tcpServer.Scan(ref _scanBuffer, filter, Passphrase, CurrentGame);

            for (int i = 0; i < games; i++)
            {
                NetworkInfo info = _scanBuffer[i];

                SendAsync(RyuLdnProtocol.Encode(PacketId.ScanReply, info));
            }

            SendAsync(RyuLdnProtocol.Encode(PacketId.ScanReplyEnd));
        }
    }
}