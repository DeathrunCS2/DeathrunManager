
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.MenuManager.Shared;

namespace DeathrunManager.Shared.Managers;

public interface IDeathrunManagers
{
    IPlayersManager         PlayersManager        { get; }
    IGameplayManager        GameplayManager       { get; }
    IAdminManager           AdminManager          { get; }
    IMenuManager            MenuManager           { get; }
}
