using DeathrunManager.Extensions;
using DeathrunManager.Interfaces;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Interfaces.Managers.Native;
using DeathrunManager.Managers.Native.ClientListener;
using DeathrunManager.Managers.Native.Event;
using DeathrunManager.Managers.Native.GameListener;
using DeathrunManager.Shared.Managers;
using Microsoft.Extensions.DependencyInjection;

namespace DeathrunManager.Managers;

internal static class ManagersDependencyInjection
{
    public static IServiceCollection AddNativeManagers(this IServiceCollection services)
    {
        services.AddSingleton<IManager, IClientListenerManager, ClientListenerManager>();
        services.AddSingleton<IManager, IEventManager, EventManager>();
        services.AddSingleton<IManager, IGameListenerManager, GameListenerManager>();
        
        return services;
    }
    
    public static IServiceCollection AddDeathrunManagers(this IServiceCollection services)
    {
        //internal managers
        services.AddSingleton<IManager, ILivesSystemManager, LivesSystemManager>();
        services.AddSingleton<IManager, IEconomyManager, EconomyManager>();
        
        //exposed managers
        services.AddSingleton<IManager, IPlayersManager, PlayersManager>();
        services.AddSingleton<IManager, IGameplayManager, GameplayManager>();
        services.AddSingleton<IManager, IDeathrunManagers, DeathrunManagers>();
        
        return services;
    }
    
}
