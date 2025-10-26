using LanPlayServer.Network;
using LanPlayServer.Network.Types;
using LanPlayServer.Utils;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System;
using System.Net;

namespace LanPlayServer
{
    public partial class LdnSession
    {
        private void HandleConnect(LdnHeader ldnPacket, ConnectRequest request)
        {
            SecurityConfig securityConfig = request.SecurityConfig;
            UserConfig userConfig = request.UserConfig;
            uint localCommunicationVersion = request.LocalCommunicationVersion;
            uint optionUnknown = request.OptionUnknown;
            NetworkInfo networkInfo = request.NetworkInfo;

            if (!_initialized)
            {
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError,
                    new NetworkErrorMessage { Error = NetworkError.ConnectFailure }));

                return;
            }

            string id = Convert.ToHexString(networkInfo.NetworkId.SessionId.AsSpan());

            ConnectImpl(id, userConfig, localCommunicationVersion);
        }

        private void HandleConnectPrivate(LdnHeader ldnPacket, ConnectPrivateRequest request)
        {
            SecurityConfig securityConfig = request.SecurityConfig;
            UserConfig userConfig = request.UserConfig;
            uint localCommunicationVersion = request.LocalCommunicationVersion;
            uint optionUnknown = request.OptionUnknown;

            if (!_initialized)
            {
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError,
                    new NetworkErrorMessage { Error = NetworkError.ConnectFailure }));

                return;
            }

            string id = Convert.ToHexString(request.SecurityParameter.SessionId.AsSpan());

            ConnectImpl(id, userConfig, localCommunicationVersion);
        }

        private void ConnectImpl(string id, UserConfig userConfig, uint localCommunicationVersion)
        {
            var nameAsString = StringUtils.ReadUtf8String(userConfig.UserName.AsSpan());
            if (nameAsString.ContainsSlur())
            {
                var ipToBan = ((IPEndPoint)Socket.RemoteEndPoint).Address;
                Console.WriteLine($"Banning {nameAsString} ({ipToBan})");
                BanList.Add(ipToBan);
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError, 
                    new NetworkErrorMessage { Error = NetworkError.BannedByServer }));
                Disconnect();
                return;
            }

            HostedGame game = _tcpServer.FindGame(id);

            if (game != null)
            {
                NetworkInfo gameInfo = game.Info;

                // Node 0 will contain the expected version (the host). If there is no match, we cannot connect.
                uint hostVersion = gameInfo.Ldn.Nodes[0].LocalCommunicationVersion;

                if (localCommunicationVersion > hostVersion)
                {
                    SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError,
                        new NetworkErrorMessage { Error = NetworkError.VersionTooHigh }));

                    return;
                }

                if (localCommunicationVersion < hostVersion)
                {
                    SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError,
                        new NetworkErrorMessage { Error = NetworkError.VersionTooLow }));

                    return;
                }

                NodeInfo myNode = new()
                {
                    Ipv4Address = IpAddress,
                    MacAddress = MacAddress,
                    NodeId = 0, // Will be populated on insert.
                    IsConnected = 0x01,
                    UserName = userConfig.UserName,
                    LocalCommunicationVersion = (ushort)localCommunicationVersion
                };

                bool result = game.Connect(this, myNode);

                if (!result)
                {
                    // There wasn't enough room in the game.

                    SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError,
                        new NetworkErrorMessage { Error = NetworkError.TooManyPlayers }));
                }
            }
            else
            {
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError,
                    new NetworkErrorMessage { Error = NetworkError.ConnectNotFound }));
            }
        }
    }
}