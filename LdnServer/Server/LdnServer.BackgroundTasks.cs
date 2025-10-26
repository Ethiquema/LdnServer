using LanPlayServer.Stats;
using NetCoreServer;
using System;
using System.Threading.Tasks;

namespace LanPlayServer
{
    public partial class LdnServer
    {
        private async Task BackgroundPingTask()
        {
            while (!IsDisposed)
            {
                foreach ((_, TcpSession session) in Sessions)
                {
                    (session as LdnSession)?.Ping();
                }

                try
                {
                    await Task.Delay(InactivityPingFrequency, _cancel.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        private async Task BackgroundDumpTask()
        {
            while (!IsDisposed)
            {
                await Task.Delay(5000, _cancel.Token);
                try
                {
                    await StatsDumper.DumpAll(_hostedGames);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}