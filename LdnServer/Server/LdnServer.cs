using NetCoreServer;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LanPlayServer
{
    public partial class LdnServer : TcpServer
    {
        public const long OneHour = 3600000;
        public const int InactivityPingFrequency = 10000;

        private readonly ConcurrentDictionary<string, HostedGame> _hostedGames = new();
        public MacAddressMemory MacAddresses { get; } = new();
        public bool UseProxy => true;

        private readonly CancellationTokenSource _cancel = new();

        public LdnServer(IPAddress address, int port) : base(address, port)
        {
            OptionNoDelay = true;

            Task.Run(BackgroundPingTask);
            Task.Run(BackgroundDumpTask);
        }

        public int Scan(ref NetworkInfo[] info, ScanFilter filter, string passphrase, HostedGame exclude)
        {
            KeyValuePair<string, HostedGame>[] all = _hostedGames.ToArray();

            int results = 0;
            int playerCount = 0;

            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Games older than this are probably bugged "ghost" lobbies, still rarely happens, there's probably still a lock issue somewhere
            long minTime = currentTime - (OneHour * 16);

            for (int i = 0; i < all.Length; i++)
            {
                HostedGame game = all[i].Value;

                game.TestReadLock();

                int nPlayers = game.Players;
                playerCount += nPlayers;

                if (game.Passphrase != passphrase || game == exclude)
                {
                    continue;
                }

                if (game.CreatedAt < minTime)
                {
                    continue;
                }

                NetworkInfo scanInfo = game.Info;

                if (scanInfo.Ldn.StationAcceptPolicy == 1)
                {
                    // Optimization: don't tell anyone about unjoinable networks.

                    continue;
                }

                if (filter.Flag.HasFlag(ScanFilterFlag.LocalCommunicationId))
                {
                    if (scanInfo.NetworkId.IntentId.LocalCommunicationId != filter.NetworkId.IntentId.LocalCommunicationId)
                    {
                        continue;
                    }
                }

                if (filter.Flag.HasFlag(ScanFilterFlag.SceneId))
                {
                    if (scanInfo.NetworkId.IntentId.SceneId != filter.NetworkId.IntentId.SceneId)
                    {
                        continue;
                    }
                }

                if (filter.Flag.HasFlag(ScanFilterFlag.SessionId))
                {
                    if (!scanInfo.NetworkId.SessionId.AsSpan().SequenceEqual(filter.NetworkId.SessionId.AsSpan()))
                    {
                        continue;
                    }
                }

                if (filter.Flag.HasFlag(ScanFilterFlag.Ssid))
                {
                    Span<byte> gameSsid = scanInfo.Common.Ssid.Name.AsSpan()[..scanInfo.Common.Ssid.Length];
                    Span<byte> scanSsid = filter.Ssid.Name.AsSpan()[..filter.Ssid.Length];
                    if (!gameSsid.SequenceEqual(scanSsid))
                    {
                        continue;
                    }
                }

                if (filter.Flag.HasFlag(ScanFilterFlag.NetworkType))
                {
                    if (scanInfo.Common.NetworkType != (byte)filter.NetworkType)
                    {
                        continue;
                    }
                }

                if (nPlayers == 0)
                {
                    continue;
                }

                // Mac address filter not implemented, since they are currently random.

                if (results >= info.Length)
                {
                    Array.Resize(ref info, info.Length + 1);
                }

                info[results++] = scanInfo;
            }

            return results;
        }

        protected override TcpSession CreateSession() 
            => new LdnSession(this);

        protected override void OnError(SocketError error) => Console.WriteLine($"LDN TCP server caught an error with code {error}");

        public override bool Stop()
        {
            _cancel.Cancel();

            return base.Stop();
        }
    }
}