using System.Collections.Generic;
using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.DeathrunObjects;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace DeathrunManager.Shared.Managers;

public interface IPlayersManager
{
    #region Events
    
    delegate void DeathrunPlayerChangedClassDelegate(IDeathrunPlayer player, DPlayerClass newClass);
    delegate void DeathrunDeathrunPlayerThinkPostDelegate(IDeathrunPlayer player);
    delegate void DeathrunDeathrunPlayerSendChatMessageDelegate(IDeathrunPlayer player, PlayerSendChatMessageEventArgs args);
    delegate void DeathrunPlayerCreatedDelegate(IDeathrunPlayer player);
    delegate void DeathrunPlayerRemovedDelegate(IDeathrunPlayer player);

    /// <summary>
    /// Fired when a deathrun player's class has been successfully changed.
    /// </summary>
    event DeathrunPlayerChangedClassDelegate? ChangedClass;

    /// <summary>
    /// Fired at the end of each think tick for a deathrun player.
    /// </summary>
    event DeathrunDeathrunPlayerThinkPostDelegate? ThinkPost;

    /// <summary>
    /// Fired when a deathrun player sends a chat message.
    /// </summary>
    event DeathrunDeathrunPlayerSendChatMessageDelegate? SendChatMessage;

    /// <summary>
    /// Fired when a deathrun player object has been successfully added to the player dictionary after successful connection.
    /// </summary>
    event DeathrunPlayerCreatedDelegate? Created;

    /// <summary>
    /// Fired when a deathrun player object has been successfully removed from the player dictionary on disconnect.
    /// Only fired for real players (bots are skipped).
    /// </summary>
    event DeathrunPlayerRemovedDelegate? Removed;

    #endregion
    
    #region DeathrunPlayer
    
    /// <summary>
    /// Retrieves a deathrun player associated with the specified game client.
    /// </summary>
    /// <param name="client">
    /// The game client to retrieve the associated deathrun player for.
    /// </param>
    /// <returns>
    /// An instance of <see cref="IDeathrunPlayer"/> if a valid associated player exists; otherwise, null.
    /// </returns>
    IDeathrunPlayer? GetDeathrunPlayer(IGameClient client);

    /// <summary>
    /// Retrieves a deathrun player associated with the specified Steam ID.
    /// </summary>
    /// <param name="steamId64">
    /// The 64-bit Steam ID of the player to retrieve the associated deathrun player for.
    /// </param>
    /// <returns>
    /// An instance of <see cref="IDeathrunPlayer"/> if a valid associated player exists; otherwise, null.
    /// </returns>
    IDeathrunPlayer? GetDeathrunPlayer(ulong steamId64);

    /// <summary>
    /// Retrieves a deathrun player associated with the specified player slot.
    /// </summary>
    /// <param name="slot">
    /// The player slot to retrieve the associated deathrun player for.
    /// </param>
    /// <returns>
    /// An instance of <see cref="IDeathrunPlayer"/> if a valid associated player exists; otherwise, null.
    /// </returns>
    IDeathrunPlayer? GetDeathrunPlayer(PlayerSlot slot);

    /// <summary>
    /// Retrieves a deathrun player associated with the specified player pawn handle.
    /// </summary>
    /// <param name="playerPawnHandle">
    /// The player pawn handle to retrieve the associated deathrun player for.
    /// </param>
    /// <returns>
    /// An instance of <see cref="IDeathrunPlayer"/> if a valid associated player exists; otherwise, null.
    /// </returns>
    IDeathrunPlayer? GetDeathrunPlayer(CEntityHandle<IPlayerPawn> playerPawnHandle);

    #endregion

    #region DeathrunPlayers
    
    /// <summary>
    /// Retrieves all deathrun players in the current session that have valid controller, pawn and are connected.
    /// </summary>
    /// <returns>
    /// A collection of <see cref="IDeathrunPlayer"/> instances representing players
    /// that are connected and have valid controller, pawn.
    /// </returns>
    IReadOnlyCollection<IDeathrunPlayer> GetAllValidDeathrunPlayers();
    
    /// <summary>
    /// Retrieves all currently valid and alive deathrun players.
    /// </summary>
    /// <returns>
    /// A collection of <see cref="IDeathrunPlayer"/> instances representing players who are alive and valid.
    /// </returns>
    IReadOnlyCollection<IDeathrunPlayer> GetAllAliveDeathrunPlayers();

    /// <summary>
    /// Retrieves all deathrun players that are considered valid but not alive.
    /// </summary>
    /// <returns>
    /// A collection of <see cref="IDeathrunPlayer"/> instances representing valid players who are currently dead.
    /// </returns>
    IReadOnlyCollection<IDeathrunPlayer> GetAllDeadDeathrunPlayers();
    
    #endregion
    
    #region DeathrunPlayers(Zero allocations)

    /// <summary>
    /// Populates the provided buffer with all valid deathrun players.
    /// Controller (VALID), PlayerPawn (VALID) and Connected = TRUE.
    /// This method is optimized for zero allocations and is suitable for use in scenarios where performance is critical.
    /// </summary>
    /// <param name="buffer">
    /// A list of <see cref="IDeathrunPlayer"/> to be populated with valid players.
    /// The buffer will be cleared before adding any players.
    /// </param>
    void GetAllValidDeathrunPlayersZAlloc(List<IDeathrunPlayer> buffer);

    /// <summary>
    /// Populates the provided buffer with all valid and alive deathrun players.
    /// Controller (VALID), PlayerPawn (VALID and ALIVE), Connected = TRUE.
    /// This method is optimized for zero allocations and is suitable for use in scenarios where performance is critical.
    /// </summary>
    /// <param name="buffer">
    /// A list to be cleared and then populated with instances of <see cref="IDeathrunPlayer"/> representing all valid and alive players.
    /// </param>
    void GetAllAliveDeathrunPlayersZAlloc(List<IDeathrunPlayer> buffer);

    #endregion
}