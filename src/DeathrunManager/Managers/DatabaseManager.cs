using System.IO;
using System.Text.Json;
using DeathrunManager.Interfaces.Managers;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace DeathrunManager.Managers;

public class DatabaseManager(
    ILogger<DatabaseManager> logger) : IManager, IDatabaseManager
{
    private static DatabaseConfig? DatabaseConfig { get; set;  }
    public string? ConnectionString { get; private set; } = null;
    
    #region IModule
    
    public bool Init()
    {
        //load database config
        DatabaseConfig = LoadConfig();
        if (DatabaseConfig.Database is "database_name") logger.LogWarning("Configure database connection details from database.json config file first!");
        
        //cache connection string
        ConnectionString = new MySqlConnectionStringBuilder
        {
            Database = DatabaseConfig.Database,
            UserID = DatabaseConfig.User,
            Password = DatabaseConfig.Password,
            Server = DatabaseConfig.Host,
            Port = (uint)DatabaseConfig.Port,
        }.ConnectionString;
        
        return true;
    }
    
    public void Shutdown()
    {
        
    }

    #endregion
    
    #region Config

    private static DatabaseConfig LoadConfig()
    {
        if (!Directory.Exists(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager")) 
            Directory.CreateDirectory(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager");
        
        var configPath = Path.Combine(DeathrunManager.Bridge.ConfigPath, "Deathrun.Manager/database.json");
        if (File.Exists(configPath) is not true)
        {
            var databaseConfig = new DatabaseConfig();
            File.WriteAllText(configPath, JsonSerializer.Serialize(databaseConfig, new JsonSerializerOptions { WriteIndented = true }));
            return databaseConfig;
        }

        var config = JsonSerializer.Deserialize<DatabaseConfig>(File.ReadAllText(configPath))!;
        
        return config;
    }
    
    //reload config
    public static void ReloadConfig() => DatabaseConfig = LoadConfig();
    
    #endregion
}

public class DatabaseConfig
{
    public string Host { get; init; } = "localhost";
    public string Database { get; init; } = "database_name";
    public string User { get; init; } = "database_user";
    public string Password { get; init; } = "database_password";
    public int Port { get; init; } = 3306;
}


