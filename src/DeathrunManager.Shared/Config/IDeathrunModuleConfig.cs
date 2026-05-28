using System;
using DeathrunManager.Shared.Objects;

namespace DeathrunManager.Shared.Config;

public interface IDeathrunModuleConfig<TConfig>
{
    /// Gets or sets the configuration object specific to the module.
    /// This property represents the main configuration structure used by the implementing module
    /// and typically contains the settings necessary for the module's operation.
    /// The type of the configuration object is determined by the generic parameter provided during implementation.
    TConfig? Config { get; set; }

    /// Gets or sets the configuration options for the module.
    /// This property encapsulates additional settings that define parameters such as file names
    /// and custom file paths, providing flexibility over how configurations are stored or accessed.
    ConfigOptions ConfigOptions { get; set; }
}

public class ConfigOptions
{
    public string FileName { get; init; } = "config";
    public string? CustomPath { get; init; } = null;
    
    //server only! ms_config_{moduleName}_reload
    public bool AllowReloadCommand { get; init; } = true;
}