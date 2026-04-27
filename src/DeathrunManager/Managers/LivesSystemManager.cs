using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Shared.Managers;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Sharp.Shared.Enums;
using Sharp.Shared.HookParams;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace DeathrunManager.Managers;

internal class LivesSystemManager(
    ILogger<LivesSystemManager> logger,
    IHookManager hookManager,
    IClientManager clientManager,
    IPlayersManager playersManager) : ILivesSystemManager
{
    public static LivesSystemConfig? LivesSystemConfig = null;
    public static string ConnectionString { get; set; } = "";
    
    #region IModule
    
    public bool Init()
    {
        //load database config
        LivesSystemConfig = LoadLivesSystemConfig();
        
        if (LivesSystemConfig.EnableLivesSystem is not true)
        {
            logger.LogWarning("[LivesSystem] {0}!", "The Lives System is disabled!");
            return true;
        }
        
        hookManager.PlayerKilledPost.InstallForward(PlayerKilledPost);
        
        clientManager.InstallCommandCallback("uselife", OnClientRespawnCommand);
        
        if (LivesSystemConfig.SaveLivesToDatabase is not true)
        {
            logger.LogWarning("[LivesSystem] {0}!", "Saving Lives to Database is disabled! Default to saving to current game session");
            return true;
        }
        
        //build connection string
        BuildDbConnectionString();

        //create the necessary db tables
        SetupDatabaseTables();
       
        return true;
    }

    public static void OnPostInit() { }

    public void Shutdown()
    {
        if (LivesSystemConfig?.EnableLivesSystem is not true)
        {
            return;
        }
        
        hookManager.PlayerKilledPost.RemoveForward(PlayerKilledPost);
        clientManager.RemoveCommandCallback("uselife", OnClientRespawnCommand);
    }

    #endregion

    #region Hooks

    private void PlayerKilledPost(IPlayerKilledForwardParams parms)
    {
        var victimDeathrunPlayer = playersManager.GetDeathrunPlayer(parms.Client);
        if (victimDeathrunPlayer is null) return;
        
        var attackerDeathrunPlayer = playersManager.GetDeathrunPlayer(parms.AttackerPawnHandle);
        if (attackerDeathrunPlayer?.LivesSystem is null) return;
        
        if (attackerDeathrunPlayer == victimDeathrunPlayer) return;
        
        attackerDeathrunPlayer.LivesSystem.AddLivesNum(1);
        attackerDeathrunPlayer
            .SendChatMessage($"You've received {{GREEN}}1 extra life {{DEFAULT}} for killing "
                            + $"{{RED}}{victimDeathrunPlayer.Client.Name}{{DEFAULT}}! "
                            + $"Total Lives: {{HEAD}}{attackerDeathrunPlayer.LivesSystem.GetLivesNum}");
    }

    #endregion
    
    #region Commands
    
    private ECommandAction OnClientRespawnCommand(IGameClient client, StringCommand command)
    {
        var deathrunPlayer = playersManager.GetDeathrunPlayer(client);
        if (deathrunPlayer is null) return ECommandAction.Stopped;
        
        deathrunPlayer.LivesSystem?.Respawn();
        return ECommandAction.Stopped;
    }
    
    #endregion
    
    #region ConnectionString

    private static void BuildDbConnectionString() 
    {
        if (LivesSystemConfig is null) return;
        
        //build connection string
        ConnectionString = new MySqlConnectionStringBuilder
        {
            Database = LivesSystemConfig.Database,
            UserID = LivesSystemConfig.User,
            Password = LivesSystemConfig.Password,
            Server = LivesSystemConfig.Host,
            Port = (uint)LivesSystemConfig.Port,
        }.ConnectionString;
    }

    #endregion
    
    #region Tables

    private static void SetupDatabaseTables()
    {
        if (LivesSystemConfig is null) return;
        
        Task.Run(() => CreateDatabaseTable($@" CREATE TABLE IF NOT EXISTS `{LivesSystemConfig.TableName}` 
                                               (
                                                   `id` BIGINT NOT NULL AUTO_INCREMENT,
                                                   `steamid64` BIGINT(255) NOT NULL UNIQUE,
                                                   `lives` BIGINT(8) DEFAULT 1,
                                                    
                                                   PRIMARY KEY (id)
                                               )"));
    }
    
    private static async Task CreateDatabaseTable(string databaseTableStringStructure)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(ConnectionString);
            dbConnection.Open();
            
            await dbConnection.ExecuteAsync(databaseTableStringStructure);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    #endregion
    
    #region Config

    private static LivesSystemConfig LoadLivesSystemConfig()
    {
        if (!Directory.Exists(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager")) 
            Directory.CreateDirectory(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager");
        
        var configPath = Path.Combine(DeathrunManager.Bridge.ConfigPath, "Deathrun.Manager/lives_system.json");
        if (!File.Exists(configPath)) return CreateLivesSystemConfig(configPath);

        var config = JsonSerializer.Deserialize<LivesSystemConfig>(File.ReadAllText(configPath))!;
        
        return config;
    }

    private static LivesSystemConfig CreateLivesSystemConfig(string configPath)
    {
        var config = new LivesSystemConfig() {};
            
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        return config;
    }
    
    //reload config
    public static void ReloadConfig() => LivesSystemConfig = LoadLivesSystemConfig();
    
    #endregion
}

public class LivesSystemConfig
{
    public bool EnableLivesSystem { get; init; } = false;
    public int StartLivesNum { get; init; } = 1;
    public bool ShowLivesCounter { get; init; } = true;

    public bool SaveLivesToDatabase { get; init; } = false;
    
    public string Spacer { get; init; } = "// If SaveLivesToDatabase is true, you have to configure the database connection details below too.";
    
    public string Host { get; init; } = "localhost";
    public string Database { get; init; } = "database_name";
    public string User { get; init; } = "database_user";
    public string Password { get; init; } = "database_password";
    public int Port { get; init; } = 3306;
    public string TableName { get; init; } = "deathrun_players";
    
}



