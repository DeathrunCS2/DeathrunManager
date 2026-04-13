using System;
using System.Collections.Generic;
using DeathrunManager.Managers;
using DeathrunManager.Shared.DeathrunObjects;

namespace DeathrunManager.Extensions;

public static class ReadOnlyCollectionExtensions
{
    public static IReadOnlyCollection<IDeathrunPlayer> FilterPlayers(
        this IReadOnlyCollection<IDeathrunPlayer> collection, 
        Func<IDeathrunPlayer, bool> predicate)
    {
        var deathrunPlayers = new List<IDeathrunPlayer>(PlayersManager.Instance.ValidDeathrunPlayersNum);

        foreach (var deathrunPlayer in PlayersManager.Instance.DeathrunPlayers.Values)
            if (predicate(deathrunPlayer))
                deathrunPlayers.Add(deathrunPlayer);
        
        return deathrunPlayers;
    }
}