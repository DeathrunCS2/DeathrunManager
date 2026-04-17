namespace DeathrunManager.Shared.Config;

public interface IManagerConfig
{
    IManagerBaseConfig GetBaseConfig { get; }
}