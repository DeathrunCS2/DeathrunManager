namespace DeathrunManager.Shared.DeathrunObjects;

public interface IDeathrunManagerApiBaseConfig
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