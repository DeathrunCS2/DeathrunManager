using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.DeathrunObjects;
using Sharp.Shared.GameEntities;

namespace DeathrunManager.Shared.Managers;

public interface IGameplayManager
{
    #region Events

    delegate void GameMasterPickedDelegate(IDeathrunPlayer gameMaster);
    delegate void DeathrunPlayerSpawnPostDelegate(IDeathrunPlayer deathrunPlayer);
    delegate void DeathrunPlayerDeathPostDelegate(IDeathrunPlayer victimDPlayer,
                                                  IDeathrunPlayer attackerDPlayer,
                                                  IBaseEntity attackerWeaponEntity,
                                                  float damageTaken, float damageTotal);
    delegate void RoundStartDelegate();
    delegate void RoundEndDelegate();
    delegate void GameStartDelegate(string mapName);
    delegate void MapStartDelegate(string mapName);
    delegate void MapEndDelegate(string mapName);

    /// <summary>
    /// Fired when a game master is picked at the start of a deathrun round.
    /// </summary>
    event GameMasterPickedDelegate? GameMasterPicked;

    /// <summary>
    /// Fired after a Deathrun player spawns and their spawn setup (class assignment, weapon equip) has completed.
    /// </summary>
    event DeathrunPlayerSpawnPostDelegate? DeathrunPlayerSpawned;

    /// <summary>
    /// Fired when a deathrun player dies during a round.
    /// </summary>
    event DeathrunPlayerDeathPostDelegate? DeathrunPlayerKilled;

    /// <summary>
    /// Fired after the deathrun round has started.
    /// </summary>
    event RoundStartDelegate? RoundStarted;

    /// <summary>
    /// Fired after the deathrun round has ended.
    /// </summary>
    event RoundEndDelegate? RoundEnded;

    /// <summary>
    /// Fired when a game begins
    /// </summary>
    event GameStartDelegate? GameStarted;

    /// <summary>
    /// Triggered when a new map starts in the game, providing the name of the map.
    /// </summary>
    event MapStartDelegate? MapStarted;

    /// <summary>
    /// Fired when the current map ends.
    /// </summary>
    event MapEndDelegate? MapEnded;

    #endregion
    
    /// <summary>
    /// Retrieves the current state of the deathrun round.
    /// </summary>
    /// <returns>
    /// A value of type <see cref="DRoundState"/> indicating the current round state.
    /// </returns>
    DRoundState GetRoundState();

    /// <summary>
    /// Retrieves the deathrun player currently designated as the game master.
    /// </summary>
    /// <returns>
    /// An instance of <see cref="IDeathrunPlayer"/> representing the game master if one is assigned; otherwise, null.
    /// </returns>
    IDeathrunPlayer? GetGameMaster();
}