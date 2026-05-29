using System.IO;
using System.Text.Json;
using DeathrunManager.Shared.Config;

namespace DeathrunManager.Config;

public class ManagerConfig : IManagerConfig
{
#pragma warning disable CA2211
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static ManagerBaseConfig BaseConfig = null!;
#pragma warning restore CA2211
    
    public IManagerBaseConfig GetBaseConfig => BaseConfig;
    
    #region Config

    public static ManagerBaseConfig LoadManagerBaseConfig()
    {
        if (!Directory.Exists(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager")) 
            Directory.CreateDirectory(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager");
        
        var prefixesConfigPath = Path.Combine(DeathrunManager.Bridge.ConfigPath, "Deathrun.Manager/deathrun.json");
        if (!File.Exists(prefixesConfigPath)) CreateManagerDefaultBaseConfig(prefixesConfigPath);

        var config = JsonSerializer.Deserialize<ManagerBaseConfig>(File.ReadAllText(prefixesConfigPath))!;
        
        return BaseConfig = config;
    }

    private static void CreateManagerDefaultBaseConfig(string configPath)
        => File.WriteAllText(configPath, JsonSerializer.Serialize(new ManagerBaseConfig() {}, JsonOptions));
    
    //reload config
    public static void ReloadConfig() => LoadManagerBaseConfig();
    
    #endregion
}

public class ManagerBaseConfig : IManagerBaseConfig
{
    public bool GiveWeaponToCTs { get; init; } = true;
    public bool RemoveBuyZones { get; init; } = true;
    public bool RemoveMoneyFromGameAndHud { get; init; } = true;
    public bool SetRoundTimeOneHour { get; init; } = true;
    public bool EnableClippingThroughTeamMates { get; init; } = true;
    public bool EnableAutoBunnyHopping { get; init; } = true;
    
    public bool EnableKillCommandForCTs { get; init; } = true;
    public bool EnableKillCommandForTs { get; init; } = false;
    
    public string Prefix { get; init; } = "{GREEN}[Deathrun]{DEFAULT}";
}