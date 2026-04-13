
using Sharp.Modules.AdminManager.Shared;

namespace DeathrunManager.Shared.Managers;

public interface IDeathrunManagers
{
    IPlayersManager         PlayersManager        { get; }
    IGameplayManager        GameplayManager       { get; }
    IAdminManager           AdminManager          { get; }
}
