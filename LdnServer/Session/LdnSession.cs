using LanPlayServer.Network;
using LanPlayServer.Network.Types;
using LanPlayServer.Utils;
using NetCoreServer;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LanPlayServer
{
    public partial class LdnSession : TcpSession
    {
        private const int ExternalProxyTimeout = 2;

        public HostedGame CurrentGame { get; set; }
        public Array6<byte> MacAddress { get; private set; }
        public uint IpAddress { get; private set; }
        public uint RealIpAddress { get; private set; }
        public string Passphrase { get; private set; } = "";

        public string StringId => Id.ToString().Replace("-", "");

        private readonly LdnServer _tcpServer;
        private readonly RyuLdnProtocol _protocol;
        private NetworkInfo[] _scanBuffer = new NetworkInfo[1];

        private long _lastMessageTicks = Stopwatch.GetTimestamp();
        private int _waitingPingId = -1;
        private byte _pingId = 0;

        /// <summary>
        /// Node ID when in a game. This does not change while the user is still in that game.
        /// </summary>
        public int NodeId { get; set; }

        private bool _initialized = false;
        private bool _disconnected = false;
        private readonly object _connectionLock = new();

        private bool _connected = false;

        public LdnSession(LdnServer server) : base(server)
        {
            _tcpServer = server;

            MacAddress = new Array6<byte>();

            Random.Shared.NextBytes(MacAddress.AsSpan());

            _protocol = new RyuLdnProtocol();

            _protocol.Initialize += HandleInitialize;
            _protocol.Passphrase += HandlePassphrase;
            _protocol.CreateAccessPoint += HandleCreateAccessPoint;
            _protocol.CreateAccessPointPrivate += HandleCreateAccessPointPrivate;
            _protocol.Reject += HandleReject;
            _protocol.SetAcceptPolicy += HandleSetAcceptPolicy;
            _protocol.SetAdvertiseData += HandleSetAdvertiseData;
            _protocol.Scan += HandleScan;
            _protocol.Connect += HandleConnect;
            _protocol.ConnectPrivate += HandleConnectPrivate;
            _protocol.Disconnected += HandleDisconnect;

            _protocol.ProxyConnect += HandleProxyConnect;
            _protocol.ProxyConnectReply += HandleProxyConnectReply;
            _protocol.ProxyData += HandleProxyData;
            _protocol.ProxyDisconnect += HandleProxyDisconnect;

            _protocol.ExternalProxyState += HandleExternalProxyState;
            _protocol.Ping += HandlePing;

            //_protocol.Any += HandleAny;
        }

        //private void HandleAny(LdnHeader obj)
        //{
        //    Console.WriteLine($"  ({PrintIp()}) -> {(PacketId)obj.Type}");
        //}

        #region Overrides

        protected override void OnConnected()
        {
            if (!_connected)
            {
                try
                {
                    RealIpAddress = GetSessionIp();
                    var ipToCheck = ((IPEndPoint)Socket.RemoteEndPoint).Address;
                    if (BanList.IsBanned(ipToCheck))
                    {
                        Console.WriteLine($"Banned IP tried to connect: {ipToCheck}");
                        Disconnect();
                        return;
                    }
                }
                catch
                {
                    Console.WriteLine("IP unavailable!");
                    // Already disconnected?
                }

                Console.WriteLine($"LDN TCP session with Id {Id} connected! ({PrintIp()})");

                _connected = true;
            }
        }

        protected override void OnDisconnected()
        {
            Task.Run(() =>
            {
                lock (_connectionLock)
                {
                    _disconnected = true;
                    DisconnectFromGame();
                }

                Console.WriteLine($"LDN TCP session with Id {Id} disconnected! ({PrintIp()})");

                _protocol.Dispose();
            });
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                OnConnected();

                _protocol.Read(buffer, (int)offset, (int)size);

                _lastMessageTicks = Stopwatch.GetTimestamp();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Closing session with Id {Id} due to exception: {e}");

                Disconnect();
            }
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"LDN TCP session caught an error with code {error}");
        }

        #endregion

        #region Utilities

        private string PrintIp()
        {
            return
                $"{RealIpAddress >> 24}.{(RealIpAddress >> 16) & 0xFF}.{(RealIpAddress >> 8) & 0xFF}.{RealIpAddress & 0xFF}";
        }

        public void Ping()
        {
            if (_waitingPingId != -1)
            {
                // The last ping was not responded to. Force a disconnect (async).
                Console.WriteLine($"Closing session with Id {Id} due to idle.");
                Task.Run(Disconnect);
            }
            else
            {
                long ticks = Stopwatch.GetTimestamp();
                long deltaTicks = ticks - _lastMessageTicks;
                long deltaMs = deltaTicks / (Stopwatch.Frequency / 1000);

                if (deltaMs > LdnServer.InactivityPingFrequency)
                {
                    byte pingId = _pingId++;

                    _waitingPingId = pingId;

                    SendAsync(RyuLdnProtocol.Encode(PacketId.Ping, new PingMessage { Id = pingId, Requester = 0 }));
                }
            }
        }

        private void DisconnectFromGame()
        {
            HostedGame game = CurrentGame;

            game?.Disconnect(this, false);

            if (game?.Owner == this)
            {
                game.Closing = true;
                _tcpServer.CloseGame(game.Id);
            }
        }

        private uint GetSessionIp()
        {
            IPAddress remoteIp = ((IPEndPoint)Socket.RemoteEndPoint).Address;
            byte[] bytes = remoteIp.GetAddressBytes();

            Array.Reverse(bytes);

            return BitConverter.ToUInt32(bytes);
        }

        public bool SetIpV4(uint ip, uint subnet, bool internalProxy)
        {
            if (!_tcpServer.UseProxy)
                return false;

            IpAddress = ip;

            if (internalProxy)
            {
                ProxyConfig config = new() { ProxyIp = ip, ProxySubnetMask = subnet };

                // Tell the client about the proxy configuration.
                SendAsync(RyuLdnProtocol.Encode(PacketId.ProxyConfig, config));
            }

            return true;
        }

        private bool IsProxyReachable(ushort port)
        {
            // Attempt to establish a connection to the p2p server owned by the host.
            // We don't need to send anything, just establish a TCP connection.
            // If that is not possible, then their external proxy isn't reachable from the internet.

            IPEndPoint searchEndpoint;
            try
            {
                searchEndpoint = Socket.RemoteEndPoint as IPEndPoint;
            }
            catch
            {
                return false;
            }

            IPEndPoint ep = new(searchEndpoint.Address, port);

            NetCoreServer.TcpClient client = new(ep);
            client.ConnectAsync();

            long endTime = Stopwatch.GetTimestamp() + Stopwatch.Frequency * ExternalProxyTimeout;

            while (Stopwatch.GetTimestamp() < endTime)
            {
                if (client.IsConnected)
                {
                    client.Dispose();

                    return true;
                }

                Thread.Sleep(1);
            }

            client.Dispose();

            return false;
        }

        #endregion
    }
}