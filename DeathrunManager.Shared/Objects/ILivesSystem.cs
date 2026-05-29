namespace DeathrunManager.Shared.Objects;

public interface ILivesSystem
{
    /// <summary>
    /// Gets the owning player associated with the current life system instance.
    /// This property represents the player to whom the lives system belongs or is linked.
    /// </summary>
    IDeathrunPlayer? Owner { get; }

    /// <summary>
    /// Gets the current number of extra lives assigned to the player in the life system.
    /// This property provides a read-only integer value representing the player's remaining lives.
    /// </summary>
    int GetLivesNum { get; }

    /// <summary>
    /// Sets the number of lives for the player to the specified amount.
    /// This method directly updates the player's lives count, overriding the current value.
    /// </summary>
    /// <param name="amount">The number of lives to set for the player. Must be a non-negative integer.</param>
    void SetLivesNum(int amount);

    /// <summary>
    /// Adds the specified number of lives to the player's current lives count.
    /// This method increments the player's lives without overriding the existing value.
    /// </summary>
    /// <param name="amount">The number of lives to add to the player's current count. Must be a positive integer.</param>
    void AddLivesNum(int amount);

    /// <summary>
    /// Removes one life from the player's current lives count.
    /// If the player has at least one life, their life count is decremented by one.
    /// If the player has zero or fewer lives, the life count remains at zero.
    /// </summary>
    void RemoveLife();

    /// <summary>
    /// Removes a specified number of lives from the player's current lives count, or removes all lives if specified.
    /// The method adjusts the player's life count based on the provided parameters.
    /// </summary>
    /// <param name="amount">The number of lives to remove from the player's current count. Defaults to 0. Must be a non-negative integer.</param>
    /// <param name="allLives">A boolean value indicating whether all lives should be removed. If true, the player's life count is set to zero.</param>
    void RemoveLives(int amount = 0, bool allLives = false);
    
    /// <summary>
    /// Attempts to respawn the player by using one of their available extra lives.
    /// This method checks the current round state to determine if respawning is allowed.
    /// If the player is not permitted to respawn based on the round's state, a message
    /// is logged and the operation is terminated without consuming a life.
    /// </summary>
    /// <returns>
    /// Returns true if the respawn action is successfully performed, otherwise false.
    /// </returns>
    bool Respawn(bool useLife = true);

    /// <summary>
    /// Generates an HTML string representation of the player's current lives counter.
    /// The format and visibility of the string depend on configurable settings defined in the lives system manager.
    /// </summary>
    /// <returns>
    /// An HTML-formatted string displaying the player's extra lives count,
    /// or an empty string if the lives counter is not configured to be displayed.
    /// </returns>
    string? GetLivesCounterHtmlString();
}