using System;
using System.Net;
using LanPlayServer.Stats;
using LanPlayServer.Utils;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace LanPlayServer
{
    static class Program
    {
        private static readonly IPAddress Host = IPAddress.Parse(Environment.GetEnvironmentVariable("LDN_HOST") ?? "0.0.0.0");
        private static readonly int Port = int.Parse(Environment.GetEnvironmentVariable("LDN_PORT") ?? "30456");
        private static readonly string GamelistPath = Environment.GetEnvironmentVariable("LDN_GAMELIST_PATH") ?? "gamelist.json";
        private static readonly string RedisSocketPath = Environment.GetEnvironmentVariable("LDN_REDIS_SOCKET") ?? "";
        private static readonly string RedisHost = Environment.GetEnvironmentVariable("LDN_REDIS_HOST") ?? "127.0.0.1";
        private static readonly int RedisPort = int.Parse(Environment.GetEnvironmentVariable("LDN_REDIS_PORT") ?? "6379");

        private static readonly ManualResetEventSlim StopEvent = new();

        private static LdnServer _ldnServer;

        static void Main()
        {
            Console.CancelKeyPress += (_, _) => StopEvent.Set();
            PosixSignalRegistration.Create(PosixSignal.SIGINT, _ => StopEvent.Set());
            PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => StopEvent.Set());
            PosixSignalRegistration.Create(PosixSignal.SIGQUIT, _ => StopEvent.Set());
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => StopEvent.Set());

            Console.WriteLine();
            Console.WriteLine( "__________                     __ .__                  .____         .___        ");
            Console.WriteLine(@"\______   \ ___.__. __ __     |__||__|  ____  ___  ___ |    |      __| _/  ____  ");
            Console.WriteLine(@" |       _/<   |  ||  |  \    |  ||  | /    \ \  \/  / |    |     / __ |  /    \ ");
            Console.WriteLine(@" |    |   \ \___  ||  |  /    |  ||  ||   |  \ >    <  |    |___ / /_/ | |   |  \");
            Console.WriteLine(@" |____|_  / / ____||____/ /\__|  ||__||___|  //__/\_ \ |_______ \\____ | |___|  /");
            Console.WriteLine(@"        \/  \/            \______|         \/       \/         \/     \/      \/ ");
            Console.WriteLine();
            Console.WriteLine( "_________________________________________________________________________________");
            Console.WriteLine();
            Console.WriteLine("- Information");

            Console.Write($"\tReading '{GamelistPath}'...");
            GameList.Initialize(File.ReadAllText(GamelistPath));
            Console.WriteLine(" Done!");

            _ldnServer = new(Host, Port);

            var bannedIPs = BanList.GetBannedIPs();

            Console.WriteLine($"Loaded {bannedIPs.Count} banned IPs");

            Console.Write($"\tLdnServer (port: {Port}) starting...");
            _ldnServer.Start();
            Console.WriteLine(" Done!");

            bool usingUnixSocket = !string.IsNullOrWhiteSpace(RedisSocketPath);
            EndPoint redisEndpoint;

            if (usingUnixSocket)
            {
                redisEndpoint = new UnixDomainSocketEndPoint(RedisSocketPath);
            }
            else
            {
                if (!IPAddress.TryParse(RedisHost, out IPAddress ipAddress))
                {
                    ipAddress = Dns.GetHostEntry(RedisHost!).AddressList[0];
                }

                redisEndpoint = new IPEndPoint(ipAddress, RedisPort);
            }

            Console.Write($"\tRedis analytics starting (using unix socket: {usingUnixSocket})...");
            StatsDumper.Start(redisEndpoint);
            Console.WriteLine(" Done!");

            StopEvent.Wait();

            StatsDumper.Stop();
            _ldnServer.Dispose();
        }
    }
}