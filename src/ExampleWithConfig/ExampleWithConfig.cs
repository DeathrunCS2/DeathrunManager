
using System;
using DeathrunManager.Shared;
using DeathrunManager.Shared.Config;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using ExampleWithConfig.Config;

namespace ExampleWithConfig;

public class ExampleWithConfig : IDeathrunModule, IDeathrunModuleConfig<TestConfigStructure>
{
    public string                                  Name                          => "Example Module";
    public string                                  Author                        => "AquaVadis";

    public IDeathrunManager                        DeathrunManager               { get; } //exposed deathrun manager instance
    
    private readonly ILogger<ExampleWithConfig>            _logger;
    private readonly ISharedSystem                 _sharedSystem;

    public TestConfigStructure?                    Config                        { get; set; }
    public ConfigOptions                           ConfigOptions                 { get; set; }
    
    //primary ctor is also valid
    public ExampleWithConfig(ISharedSystem sharedSystem, IDeathrunManager deathrunManagerApi)
    {
        _sharedSystem        = sharedSystem;
        _logger              = sharedSystem.GetLoggerFactory().CreateLogger<ExampleWithConfig>();
        
        DeathrunManager      = deathrunManagerApi;

        Config = new TestConfigStructure();
        ConfigOptions = new ConfigOptions { FileName = "test_suite_config2" };

    }
    
    #region IDeathrunModule
    
    //This method is called when the deathrun module tries to load and the return value is the result
    public void OnConfigParsed<TConfig>(TConfig config)
    {
        _logger.LogInformation("[{moduleName}] {colorMessage}",
            GetType().Name, 
            $"Reloaded config! New test string value: {(config as TestConfigStructure)?.TestString}");
    }

    public bool Init(bool hotReload)
    {
        //Subscribe to PlayersManager's `Created` event to be notified when a new deathrun player is created
        DeathrunManager.Managers.PlayersManager.Created += OnDeathrunPlayerCreated;
        
        _logger.LogInformation("Init: {colorMessage}", Config?.TestString);
        
        return true;
    }

    //This method is called after the deathrun module has been loaded successfully
    public void PostInit(bool hotReload) { }

    //This method is called when all the Deathrun modules have been loaded
    public void OnAllDeathrunModulesLoaded(bool hotReload) { }

    //This method is called when all the ModSharp modules have been loaded
    public void OnAllModSharpModulesLoaded() { }

    //The method is called when the DeathrunManager is shutting down, but before shutting down the DeathrunManager's managers
    public void Shutdown(bool hotReload)
    {
        //You must unsubscribe from the PlayersManager's `Created` event to avoid issues when reloading modules
        DeathrunManager.Managers.PlayersManager.Created -= OnDeathrunPlayerCreated;
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