using LanPlayServer.Stats;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using System.Collections.Generic;
using System;
using System.Linq;

namespace LanPlayServer
{
    public partial class LdnServer
    {
        public HostedGame CreateGame(string id, NetworkInfo info, AddressList dhcpConfig, string oldOwnerId)
        {
            id = id.ToLower();
            HostedGame game = new(id, info, dhcpConfig);
            bool idTaken = false;

            _hostedGames.AddOrUpdate(id, game, (id, oldGame) =>
            {
                if (oldGame.OwnerId == oldOwnerId)
                {
                    oldGame.Close();

                    Statistics.RemoveGameAnalytics(oldGame);

                    return game;
                }
                else
                {
                    game.Close();
                    idTaken = true;

                    Console.WriteLine($"id Taken: {id}");
                    return oldGame;
                }
            });

            if (idTaken)
            {
                return null;
            }

            Statistics.AddGameAnalytics(game);

            return game;
        }

        public HostedGame FindGame(string id)
        {
            id = id.ToLower();

            _hostedGames.TryGetValue(id, out HostedGame result);

            return result;
        }

        public HostedGame[] All() => _hostedGames.Values.ToArray();

        public void CloseGame(string id)
        {
            _hostedGames.Remove(id.ToLower(), out HostedGame removed);
            removed?.Close();

            if (removed != null)
            {
                Console.WriteLine($"Removing from analytics: {id}");
                Statistics.RemoveGameAnalytics(removed);
            }
        }
    }
}