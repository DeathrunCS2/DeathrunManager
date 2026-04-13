using System;
using System.Globalization;
using DeathrunManager.Shared;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using Sharp.Shared;

namespace ExampleModule;

public class ExampleModule : IDeathrunModule
{
    public string                                  Name                          => "Example Module";
    public string                                  Author                        => "AquaVadis";
    public IDeathrunManager                        DeathrunManagerApi            { get; } //exposed deathrun manager instance

    private readonly ILogger<ExampleModule>        _logger;
    private readonly ISharedSystem                 _sharedSystem;
    
    //primary ctor is also valid
    public ExampleModule(ISharedSystem sharedSystem, IDeathrunManager deathrunManagerApi)
    {
        _sharedSystem        = sharedSystem;
        _logger              = sharedSystem.GetLoggerFactory().CreateLogger<ExampleModule>();
        
        DeathrunManagerApi   = deathrunManagerApi;
    }
        
    
    #region IDeathrunModule
    
    //This method is called when all the ModSharp modules have been loaded
    public bool Init(bool hotReload)
    {
        //Subscribe to PlayersManager's `Created` event to be notified when a new deathrun player is created
        DeathrunManagerApi.Managers.PlayersManager.Created += OnDeathrunPlayerCreated;
        return true;
    }

    //This method is called right after the Init method
    public void PostInit(bool hotReload) { }

    //This method is called when all the Deathrun modules have been loaded
    public void OnAllDeathrunModulesLoaded(bool hotReload) { }

    //This method is called when all the ModSharp modules have been loaded
    public void OnAllModSharpModulesLoaded() { }

    //The method is called when the DeathrunManager is shutting down, but before shutting down the DeathrunManager's managers
    public void Shutdown(bool hotReload)
    {
        //You must unsubscribe from the PlayersManager's `Created` event to avoid issues when reloading modules
        DeathrunManagerApi.Managers.PlayersManager.Created -= OnDeathrunPlayerCreated;
    }
 
    #endregion
    
    //This will be called when a new deathrun player is fully created by the DeathrunManager
    private void OnDeathrunPlayerCreated(IDeathrunPlayer deathrunPlayer)
    {
        _logger.LogInformation("[{moduleName}] {colorMessage}",
                                                GetType().Name, 
                                                $"Deathrun player {deathrunPlayer.Client.Name} has been created!");
    }
    
    #region Log 
    
    private static void Log(string header, string message, 
        ConsoleColor backgroundColor = ConsoleColor.DarkGray,
        ConsoleColor textColor = ConsoleColor.Black)
    {
        Console.ForegroundColor = textColor;
        Console.BackgroundColor = backgroundColor;
        Console.Write($"{header}:");
        Console.ResetColor();
        Console.Write($" {message} \n");
    }
    
    #endregion
}