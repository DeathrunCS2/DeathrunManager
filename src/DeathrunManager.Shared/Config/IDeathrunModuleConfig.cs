namespace DeathrunManager.Shared.Config;

public interface IDeathrunModuleConfig<TConfig>
{
    TConfig? Config { get; set; }

    ConfigOptions ConfigOptions { get; set; }
}

public class ConfigOptions
{
    public string FileName { get; set; } = "config";
    public string? CustomPath { get; set; } = null;
}