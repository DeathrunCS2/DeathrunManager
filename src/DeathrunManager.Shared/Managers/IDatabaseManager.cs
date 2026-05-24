namespace DeathrunManager.Shared.Managers;

public interface IDatabaseManager
{
    /// <summary>
    /// Get the connection string for the database using the connection string from the shared DatabaseManager.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var connection = new MySqlConnection(databaseManager.ConnectionString);
    /// connection.Open();
    /// </code>
    /// </example>
    /// <remarks>
    /// The connection string is retrieved from the configuration file: <br /><b>sharp/configs/Deathrun.Manager/database.json</b>
    /// </remarks>
    string? ConnectionString { get; }
}