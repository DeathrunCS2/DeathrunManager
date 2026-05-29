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
    
    /// <summary>
    /// Invoked automatically when the configuration file has been successfully loaded and parsed.
    /// This allows the module to perform custom initialization, validation, or post-processing logic
    /// using the newly parsed configuration settings.
    /// </summary>
    /// <param name="config">The parsed configuration instance containing the loaded settings.</param>
    void OnConfigParsed(TConfig config) { }
}

public class ConfigOptions
{
    /// Gets or sets the name of the configuration file.
    /// This property specifies the base name, without extension, of the JSON file
    /// where the configuration for the module will be stored or loaded from.
    /// If not specified, the default value is "config".
    public string FileName { get; init; } = "config";

    /// Gets or sets the custom path for the configuration files used in the module.
    /// This property specifies an optional subdirectory relative to the module's default configuration path.
    /// If set to a non-null value, this path will be appended to the module's base path to determine the
    /// location where configuration files are stored. If null, the default path is used.
    public string? CustomPath { get; init; } = null;

    /// Gets a value indicating whether the module allows the reload command to be executed.
    /// If set to true, the module can be reloaded dynamically using a specific command.
    /// This property is primarily used to enable or disable runtime configuration reloading for the module.
    public bool AllowReloadCommand { get; init; } = true;
}