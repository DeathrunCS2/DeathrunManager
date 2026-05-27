using DeathrunManager.Shared.Objects;

namespace DeathrunManager.Objects;

public class CommonVars(
    string configsPath,
    string dllPath,
    string sharpPath) : ICommonVars
{
    public string ConfigsPath             { get; }               = configsPath;
    public string DllPath                 { get; }               = dllPath;
    public string SharpPath               { get; }               = sharpPath;
}