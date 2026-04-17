
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.MenuManager.Shared;

namespace DeathrunManager.Shared.Managers;

public interface IDeathrunManagers
{
    /// <summary>
    /// Provides access to the deathrun players management system in the Deathrun manager.
    /// </summary>
    /// <remarks>
    /// The PlayersManager property allows interactions with player-related functionalities
    /// such as querying players, managing events, and retrieving collections of players
    /// based on their state or attributes within the Deathrun game logic.
    /// </remarks>
    /// <value>
    /// An instance implementing the <see cref="IPlayersManager"/> interface.
    /// </value>
    IPlayersManager PlayersManager { get; }

    /// <summary>
    /// Provides access to the gameplay management system in the Deathrun manager.
    /// </summary>
    /// <remarks>
    /// The GameplayManager property offers functionality for managing game-specific mechanics
    /// such as round state transitions, tracking player events, and handling key gameplay triggers
    /// within the Deathrun environment.
    /// </remarks>
    /// <value>
    /// An instance implementing the <see cref="IGameplayManager"/> interface.
    /// </value>
    IGameplayManager GameplayManager { get; }

    /// <summary>
    /// Provides access to the ModSharp's IAdminManager interface.
    /// </summary>
    /// <remarks>
    /// Check the <see cref="IAdminManager"/> interface for detailed information about the available methods.
    /// </remarks>
    /// <value>
    /// An instance implementing the <see cref="IAdminManager"/> interface.
    /// </value>
    IAdminManager AdminManager { get; }

    /// <summary>
    /// Provides access to the ModSharp's IMenuManager interface.
    /// </summary>
    /// <remarks>
    /// Check the <see cref="IMenuManager"/> interface for detailed information about the available methods.
    /// </remarks>
    /// <value>
    /// An instance implementing the <see cref="IMenuManager"/> interface.
    /// </value>
    IMenuManager MenuManager { get; }
}
