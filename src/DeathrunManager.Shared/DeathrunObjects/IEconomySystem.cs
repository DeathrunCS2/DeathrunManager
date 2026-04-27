namespace DeathrunManager.Shared.DeathrunObjects;

public interface IEconomySystem
{
    /// <summary>
    /// Gets the owner of the economy system.
    /// </summary>
    /// <remarks>
    /// The <c>Owner</c> property represents the player who owns or is associated with this particular instance of the economy system.
    /// It provides access to the player's details or functionalities through the <see cref="IDeathrunPlayer"/> interface.
    /// </remarks>
    IDeathrunPlayer? Owner { get; }

    /// <summary>
    /// Gets or sets the number of credits available in the economy system.
    /// </summary>
    /// <remarks>
    /// The <c>Credits</c> property represents the balance of credits associated with the economy system.
    /// It allows for modifying and retrieving the current credit count, which can be used for various in-game transactions or purposes.
    /// </remarks>
    int Credits { get; set; }

    /// <summary>
    /// Sets the number of credits for the associated player in the economy system.
    /// </summary>
    /// <param name="amount">The number of credits to set. Must be a non-negative integer.</param>
    void SetCreditsNum(int amount);

    /// <summary>
    /// Adds a specified number of credits to the associated player in the economy system.
    /// </summary>
    /// <param name="amount">The number of credits to add. Must be a non-negative integer.</param>
    void AddCreditsNum(int amount);
    
    /// <summary>
    /// Deducts a specified number of credits from the associated player in the economy system.
    /// </summary>
    /// <param name="amount">The number of credits to deduct. Must be a non-negative integer.</param>
    void DeductCreditsNum(int amount);

    /// <summary>
    /// Resets the credits of the associated player in the economy system to zero.
    /// </summary>
    void ResetCredits();
    
    string? GetCreditsNumHtmlString();
}