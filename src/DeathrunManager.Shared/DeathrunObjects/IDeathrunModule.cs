using Sharp.Modules.AdminManager.Shared;

namespace DeathrunManager.Shared.DeathrunObjects;

public interface IDeathrunModule
{
    IDeathrunManager DeathrunManager { get; }
    
    string Name { get; }
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
