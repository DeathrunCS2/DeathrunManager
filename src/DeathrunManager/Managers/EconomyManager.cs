using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.Managers;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Sharp.Shared.HookParams;
using Sharp.Shared.Managers;

namespace DeathrunManager.Managers;

internal class EconomyManager(
    ILogger<EconomyManager> logger,
    IHookManager hookManager,
    IPlayersManager playersManager) : IEconomyManager
{
    public static EconomySystemConfig EconomySystemConfig = null!;
    public static string ConnectionString { get; set; } = "";
    
    #region IModule
    
    public bool Init()
    {
        //load database config
        EconomySystemConfig = LoadEconomySystemConfig();
        
        if (EconomySystemConfig.EnableEconomySystem is not true)
        {
            logger.LogWarning("[EconomySystem] {0}!", "The Economy System is disabled!");
            return true;
        }
        
        hookManager.PlayerKilledPost.InstallForward(PlayerKilledPost);
        
        //build connection string
        BuildDbConnectionString();

        //create the necessary db tables
        SetupDatabaseTables();
       
        return true;
    }
    
    public void Shutdown()
    {
        if (EconomySystemConfig.EnableEconomySystem is not true)
        {
            return;
        }
        
        hookManager.PlayerKilledPost.RemoveForward(PlayerKilledPost);
    }

    #endregion
    
    #region Hooks

    private void PlayerKilledPost(IPlayerKilledForwardParams parms)
    {
        var victimDeathrunPlayer = playersManager.GetDeathrunPlayer(parms.Client);
        if (victimDeathrunPlayer is null) return;
        
        var attackerDeathrunPlayer = playersManager.GetDeathrunPlayer(parms.AttackerPawnHandle);

        if (attackerDeathrunPlayer?.EconomySystem is null) return;
        
        attackerDeathrunPlayer.EconomySystem.AddCreditsNum(EconomySystemConfig.KillCreditsNum);
        attackerDeathrunPlayer
            .SendChatMessage($"You've received {{GREEN}}{EconomySystemConfig.KillCreditsNum} {{DEFAULT}}credits for killing "
                             + $"{{RED}}{victimDeathrunPlayer.Client.Name}{{DEFAULT}}!");
        
        //skip if the attacker is not a GameMaster(Terrorist)
        if (attackerDeathrunPlayer.Class is not DPlayerClass.GameMaster) return;
        
        attackerDeathrunPlayer.EconomySystem.AddCreditsNum(EconomySystemConfig.GameMasterKillCreditsBonusNum);
        attackerDeathrunPlayer
            .SendChatMessage($"You've also received a {{GOLD}}{EconomySystemConfig.GameMasterKillCreditsBonusNum} {{DEFAULT}}credits bonus for killing a contestant!");
    }

    #endregion
    
    #region ConnectionString

    private static void BuildDbConnectionString() 
    {
        //build connection string
        ConnectionString = new MySqlConnectionStringBuilder
        {
            Database = EconomySystemConfig.Database,
            UserID = EconomySystemConfig.User,
            Password = EconomySystemConfig.Password,
            Server = EconomySystemConfig.Host,
            Port = (uint)EconomySystemConfig.Port,
        }.ConnectionString;
    }

    #endregion
    
    #region Tables

    private static void SetupDatabaseTables()
    {
        Task.Run(() => CreateDatabaseTable($@" CREATE TABLE IF NOT EXISTS `{EconomySystemConfig.TableName}` 
                                               (
                                                   `id` BIGINT NOT NULL AUTO_INCREMENT,
                                                   `steamid64` BIGINT(255) NOT NULL UNIQUE,
                                                   `credits` BIGINT(8) DEFAULT 5,
                                                    
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

    private static EconomySystemConfig LoadEconomySystemConfig()
    {
        if (!Directory.Exists(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager")) 
            Directory.CreateDirectory(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager");
        
        var configPath = Path.Combine(DeathrunManager.Bridge.ConfigPath, "Deathrun.Manager/economy_system.json");
        if (!File.Exists(configPath)) return CreateEconomySystemConfig(configPath);

        var config = JsonSerializer.Deserialize<EconomySystemConfig>(File.ReadAllText(configPath))!;
        
        return config;
    }

    private static EconomySystemConfig CreateEconomySystemConfig(string configPath)
    {
        var config = new EconomySystemConfig() {};
            
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        return config;
    }
    
    //reload config
    public static void ReloadConfig() => EconomySystemConfig = LoadEconomySystemConfig();
    
    #endregion
}

public class EconomySystemConfig
{
    public bool EnableEconomySystem { get; init; } = true;
    public int StartCreditsNum { get; init; } = 5;
    public int KillCreditsNum { get; init; } = 2;
    public int GameMasterKillCreditsBonusNum { get; init; } = 2;

    
    public string Spacer { get; init; } = "// If EnableEconomySystem is true, you have to configure the database connection details below too.";
    
    public string Host { get; init; } = "localhost";
    public string Database { get; init; } = "database_name";
    public string User { get; init; } = "database_user";
    public string Password { get; init; } = "database_password";
    public int Port { get; init; } = 3306;
    public string TableName { get; init; } = "deathrun_economy";
    
}



