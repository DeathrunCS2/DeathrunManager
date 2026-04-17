using System;
using DeathrunManager.Shared.Config;
using DeathrunManager.Shared.Managers;

namespace DeathrunManager.Shared;

public interface IDeathrunManager
{
    #region Identity

    /// <summary>
    /// Represents the unique identity of the <see cref="IDeathrunManager"/> interface.
    /// </summary>
    /// <remarks>
    /// This property uniquely identifies the <see cref="IDeathrunManager"/> interface and can be used
    /// for purposes such as registration, discovery, or logging.
    /// The returned value is derived from the fully qualified name of the interface or its name if the full name is null.
    /// </remarks>
    static string Identity => typeof(IDeathrunManager).FullName ?? nameof(IDeathrunManager);

    /// <summary>
    /// Provides an instance of the <see cref="IDeathrunManager"/> implementation.
    /// </summary>
    /// <remarks>
    /// This static property is designed to return the singleton or shared instance
    /// of the <see cref="IDeathrunManager"/> implementation.
    /// It serves as a central access point for interacting with the deathrun manager's features
    /// and configuration.
    /// The actual instance must be provided by the derived implementation.
    /// </remarks>
    static IDeathrunManager? Instance => throw new NotImplementedException();

    #endregion

    /// <summary>
    /// Provides access to the configuration properties specific to the deathrun functionality of the
    /// <see cref="IDeathrunManager"/> interface.
    /// </summary>
    /// <remarks>
    /// This property encapsulates the configuration settings implemented by <see cref="IManagerBaseConfig"/>.
    /// These settings include various gameplay behaviors and server adjustments such as enabling auto bunny hopping,
    /// removing buy zones, setting round times, and more.
    /// The exact behavior and values of these configurations are determined by the implementation of the
    /// <see cref="IManagerBaseConfig"/> interface.
    /// </remarks>
    public IManagerBaseConfig Config { get; }

    /// <summary>
    /// Provides access to various subsystem managers within the <see cref="IDeathrunManager"/> interface.
    /// </summary>
    /// <remarks>
    /// This property contains references to the key functional managers of the <see cref="IDeathrunManager"/> system,
    /// such as player management, gameplay rules, administrative controls, and menu handling. These managers
    /// facilitate modular and organized interactions within the deathrun environment.
    /// </remarks>
    public IDeathrunManagers Managers { get; }
}
