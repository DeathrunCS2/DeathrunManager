namespace DeathrunManager.Shared.Config;

public interface IManagerConfig
{
    IManagerBaseConfig GetBaseConfig { get; }
}

public interface IDeathrunManagerConfigStructure
{
    bool GiveWeaponToCTs { get;}
    bool RemoveBuyZones { get;}
    bool RemoveMoneyFromGameAndHud { get;}
    bool SetRoundTimeOneHour { get;}
    bool EnableClippingThroughTeamMates { get;}
    bool EnableAutoBunnyHopping { get;}
    
    bool EnableKillCommandForCTs { get;}
    bool EnableKillCommandForTs { get;}

    string Prefix { get; }
}