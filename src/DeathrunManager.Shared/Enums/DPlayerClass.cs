namespace DeathrunManager.Shared.Enums;

/// <summary>
/// Represents the classification of a player in the game.
/// The classification determines the role and behavior of the player within the game's mechanics.
/// </summary>
public enum DPlayerClass
{
    /// <summary>
    /// Represents a player class designated as a Contestant in the game.
    /// Typically, a Contestant is a participant aimed at overcoming challenges set
    /// within the game environment.
    /// </summary>
    Contestant,

    /// <summary>
    /// Represents a player class designated as a GameMaster in the game.
    /// A GameMaster is typically responsible for overseeing, managing, or controlling
    /// the map's traps. 
    /// </summary>
    GameMaster
}