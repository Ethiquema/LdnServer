using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace LanPlayServer
{
    internal static class BanList
    {
        private static readonly ConcurrentBag<string> BannedIPs;
        private static readonly object Lock = new();

        private static readonly string BanFilePath =
            Environment.GetEnvironmentVariable("IP_BAN_FILE_PATH") ?? "bannedips.txt";

        private static readonly FileSystemWatcher BanFileWatcher = new(BanFilePath);

        private static bool IsSelfCausedWrite;

        static BanList()
        {
            BanFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            BanFileWatcher.Changed += BanFileWatcher_OnChanged;

            BannedIPs = [];
            if (!File.Exists(BanFilePath))
            {
                File.Create(BanFilePath).Close();
            }
            else 
                Load();
        }

        private static void BanFileWatcher_OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType is WatcherChangeTypes.Changed && !IsSelfCausedWrite)
            {
                Console.WriteLine("Detected a change in the IP ban list. Reloading.");
                Load();
            }
        }

        private static void Load()
        {
            BannedIPs.Clear();

            string[] lines = File.ReadAllLines(BanFilePath);
            foreach (string line in lines)
            {
                BannedIPs.Add(line);
            }
        }

        public static void Add(IPAddress ip)
        {
            try
            {
                string ipString = ip.ToString();
                lock (Lock)
                {
                    if (!BannedIPs.Contains(ipString))
                    {
                        BannedIPs.Add(ipString);
                        IsSelfCausedWrite = true;
                        File.AppendAllText(BanFilePath, ipString + Environment.NewLine);
                        IsSelfCausedWrite = false;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to ban IP {ip}: {e.Message}");
            }
        }

        public static bool IsBanned(IPAddress ip) => BannedIPs.Contains(ip.ToString());

        public static List<string> GetBannedIPs() => BannedIPs.ToList();
    }
}