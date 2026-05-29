namespace DeathrunManager.Interfaces.Managers;

public interface IManager
{
    bool Init() => true;

    void PostInit()
    {
    }

    void Shutdown()
    {
    }

    void OnAllModulesLoaded()
    {
    }
}
