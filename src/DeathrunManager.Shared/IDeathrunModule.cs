using DeathrunManager.Shared.Config;
using DeathrunManager.Shared.Managers;

namespace DeathrunManager.Shared;

public interface IDeathrunModule
{
    /// <summary>
    /// Represents the main manager interface for Deathrun functionality.
    /// It acts as the central entry point for accessing configuration and sub-managers within the Deathrun system.
    /// </summary>
    /// <remarks>
    /// This class is responsible for providing access to core configurations and the managers that handle
    /// different aspects of the Deathrun game mode. It can be used to query system configurations and
    /// interact with necessary parts such as player and gameplay managers.
    /// </remarks>
    /// <seealso cref="IManagerBaseConfig"/>
    /// <seealso cref="IDeathrunManagers"/>
    IDeathrunManager DeathrunManager { get; }

    /// <summary>
    /// Gets the name of the Deathrun module.
    /// </summary>
    /// <remarks>
    /// This property represents the identifiable name of the Deathrun module.
    /// It is primarily used to distinguish between different modules and ensure proper identification during operations like initialization or management.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Represents the author or creator of the module.
    /// This property identifies the individual or organization responsible for the development of the module.
    /// </summary>
    /// <remarks>
    /// The Author property provides information regarding the entity that contributed to or owns the development of the related module.
    /// </remarks>
    string Author { get; }

    bool Init(bool hotReload);

    void PostInit(bool hotReload)
    {
    }

    void OnAllModSharpModulesLoaded()
    {
    }
    
    void OnAllDeathrunModulesLoaded(bool hotReload)
    {
    }
    
    void Shutdown(bool hotReload);
}
