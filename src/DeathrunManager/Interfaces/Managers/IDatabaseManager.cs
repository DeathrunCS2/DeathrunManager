namespace DeathrunManager.Interfaces.Managers;

public interface IDatabaseManager
{
    /// <summary>
    /// Get the connection string for the database using the connection string from the shared DatabaseManager.
    /// </summary>
    /// <code>
    /// <example>
    /// await using var connection = new MySqlConnection(databaseManager.ConnectionString);
    /// connection.Open();
    /// </example>
    /// </code>
    /// <remarks>
    /// Configuration file: <b>sharp/configs/Deathrun.Manager/database.json</b>
    /// </remarks>
    string? ConnectionString { get; }
}