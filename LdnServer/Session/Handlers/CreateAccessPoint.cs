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
        private void HandleCreateAccessPoint(LdnHeader ldnPacket, CreateAccessPointRequest request, byte[] advertiseData)
        {
            var nameAsString = StringUtils.ReadUtf8String(request.UserConfig.UserName.AsSpan());
            if (nameAsString.ContainsSlur())
            {
                var ipToBan = ((IPEndPoint)Socket.RemoteEndPoint).Address;
                Console.WriteLine($"Banning {nameAsString} ({ipToBan})");
                BanList.Add(ipToBan);
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError, new NetworkErrorMessage { Error = NetworkError.BannedByServer }));
                Disconnect();
                return;
            }
            if (CurrentGame != null || !_initialized)
            {
                // Cannot create an access point while in a game.
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError, new NetworkErrorMessage { Error = NetworkError.Unknown }));

                return;
            }

            string id = Guid.NewGuid().ToString().Replace("-", "");

            AddressList dhcpConfig = new();

            AccessPointConfigToNetworkInfo(id, request.NetworkConfig, request.UserConfig, request.RyuNetworkConfig, request.SecurityConfig, dhcpConfig, advertiseData);
        }

        private void HandleCreateAccessPointPrivate(LdnHeader ldnPacket, CreateAccessPointPrivateRequest request, byte[] advertiseData)
        {
            if (CurrentGame != null || !_initialized)
            {
                // Cannot create an access point while in a game.
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError, new NetworkErrorMessage { Error = NetworkError.Unknown }));

                return;
            }

            string id = Convert.ToHexString(request.SecurityParameter.SessionId.AsSpan());

            AccessPointConfigToNetworkInfo(id, request.NetworkConfig, request.UserConfig, request.RyuNetworkConfig, request.SecurityConfig, request.AddressList, advertiseData);
        }
        
        private void AccessPointConfigToNetworkInfo(string id, NetworkConfig networkConfig, UserConfig userConfig, RyuNetworkConfig ryuNetworkConfig, SecurityConfig securityConfig, AddressList dhcpConfig, byte[] advertiseData)
        {
            string userId = StringId;

            Array16<byte> sessionId = new();
            Convert.FromHexString(id).CopyTo(sessionId.AsSpan());

            NetworkInfo networkInfo = new()
            {
                NetworkId = new NetworkId
                {
                    IntentId = new IntentId
                    {
                        LocalCommunicationId = networkConfig.IntentId.LocalCommunicationId,
                        SceneId              = networkConfig.IntentId.SceneId
                    },
                    SessionId = sessionId
                },
                Common = new CommonNetworkInfo
                {
                    Channel     = networkConfig.Channel,
                    LinkLevel   = 3,
                    NetworkType = 2,
                    MacAddress  = MacAddress,
                    Ssid        = new Ssid
                    {
                        Length = 32,
                    }
                },
                Ldn = new LdnNetworkInfo
                {
                    SecurityMode      = (ushort)securityConfig.SecurityMode,
                    NodeCountMax      = networkConfig.NodeCountMax,
                    NodeCount         = 0,
                    AdvertiseDataSize = (ushort)advertiseData.Length,
                    AuthenticationId  = 0
                }
            };

            "12345678123456781234567812345678"u8.ToArray().CopyTo(networkInfo.Common.Ssid.Name.AsSpan());
            advertiseData.CopyTo(networkInfo.Ldn.AdvertiseData.AsSpan());

            NodeInfo myInfo = new()
            {
                Ipv4Address               = IpAddress,
                MacAddress                = MacAddress,
                NodeId                    = 0x00,
                IsConnected               = 0x01,
                UserName                  = userConfig.UserName,
                LocalCommunicationVersion = networkConfig.LocalCommunicationVersion,
            };

            for (int i = 0; i < 8; i++)
            {
                networkInfo.Ldn.Nodes[i] = new NodeInfo();
            }

            if (ryuNetworkConfig.ExternalProxyPort != 0 && !IsProxyReachable(ryuNetworkConfig.ExternalProxyPort))
            {
                ryuNetworkConfig.ExternalProxyPort = 0;
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError, new NetworkErrorMessage { Error = NetworkError.PortUnreachable }));
            }

            /*
            if (networkInfo.NetworkId.IntentId.LocalCommunicationId == 0x0100abf008968000ul)
            {
                SendAsync(_protocol.Encode(PacketId.NetworkError, new NetworkErrorMessage { Error = NetworkError.Unknown }));
                return;
            }
            */

            HostedGame game = _tcpServer.CreateGame(id, networkInfo, dhcpConfig, userId);

            if (game == null)
            {
                SendAsync(RyuLdnProtocol.Encode(PacketId.NetworkError, new NetworkErrorMessage { Error = NetworkError.Unknown }));
                return;
            }

            lock (_connectionLock)
            {
                if (_disconnected)
                {
                    Console.WriteLine($"Emergency disconnect: {id}");
                    game = null;
                }

                game?.SetOwner(this, ryuNetworkConfig);
                game?.Connect(this, myInfo);
            }

            if (game == null)
            {
                Console.WriteLine($"Null close: {id}");
                _tcpServer.CloseGame(id);
            }
        }
    }
}