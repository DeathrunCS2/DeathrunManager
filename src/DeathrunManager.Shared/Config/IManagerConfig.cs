namespace DeathrunManager.Shared.Config;

public interface IManagerConfig
{
    /// <summary>
    /// Property providing access to the base configuration for the deathrun manager.
    /// </summary>
    /// <value>
    /// An implementation of <see cref="IManagerBaseConfig"/> containing the core configuration settings.
    /// </value>
    IManagerBaseConfig GetBaseConfig { get; }
}