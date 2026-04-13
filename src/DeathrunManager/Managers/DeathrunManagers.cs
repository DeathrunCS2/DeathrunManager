using System;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Shared.Managers;
using Sharp.Modules.AdminManager.Shared;

namespace DeathrunManager.Managers;

internal class DeathrunManagers(
    IPlayersManager playersManager,
    IGameplayManager gameplayManager) : IManager, IDeathrunManagers
{
    public IPlayersManager PlayersManager => playersManager;
    
    public IGameplayManager GameplayManager => gameplayManager;
    
    public IAdminManager AdminManager 
        => DeathrunManager.Bridge.SharpModuleManager
                             .GetOptionalSharpModuleInterface<IAdminManager>(IAdminManager.Identity)?.Instance 
                                ?? throw new Exception("Failed to capture Admin Manager Api! ");
}