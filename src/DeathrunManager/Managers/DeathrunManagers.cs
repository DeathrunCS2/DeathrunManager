using System;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Shared.Managers;
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.MenuManager.Shared;

namespace DeathrunManager.Managers;

internal class DeathrunManagers(
    IPlayersManager playersManager,
    IGameplayManager gameplayManager,
    IDatabaseManager databaseManager) : IManager, IDeathrunManagers
{
    public IDatabaseManager DatabaseManager => databaseManager;
    
    public IPlayersManager PlayersManager => playersManager;
    
    public IGameplayManager GameplayManager => gameplayManager;
    
    public IAdminManager AdminManager 
        => DeathrunManager.Bridge.SharpModuleManager
                             .GetOptionalSharpModuleInterface<IAdminManager>(IAdminManager.Identity)?.Instance 
                                ?? throw new Exception("Failed to capture Admin Manager Api! ");
    
    public IMenuManager MenuManager 
        => DeathrunManager.Bridge.SharpModuleManager
                                 .GetOptionalSharpModuleInterface<IMenuManager>(IMenuManager.Identity)?.Instance
                                    ?? throw new Exception("Failed to capture Menu Manager Api! ");
}