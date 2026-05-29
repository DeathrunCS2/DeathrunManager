
using System;
using DeathrunManager.Shared;
using DeathrunManager.Shared.Config;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using ExampleWithConfig.Config;

namespace ExampleWithConfig;

                                                  //TestConfigStructure is the config structure that will
                                                  //be used by the DeathrunManager
public class ExampleWithConfig : IDeathrunModule, IDeathrunModuleConfig<TestConfigStructure>
{
    public string                                  Name                          => "Example Module with Config";
    public string                                  Author                        => "AquaVadis";

    //exposed deathrun manager instance
    public IDeathrunManager                        DeathrunManager               { get; } 
    
    //getter and setter for the config
    public TestConfigStructure?                    Config                        { get; set; }
    
    //getter and setter for the config options
    public ConfigOptions                           ConfigOptions                 { get; set; }
    
    private readonly ILogger<ExampleWithConfig>    _logger;
    private readonly ISharedSystem                 _sharedSystem;
    
    //primary ctor is also valid
    public ExampleWithConfig(ISharedSystem sharedSystem, IDeathrunManager deathrunManagerApi)
    {
        _sharedSystem        = sharedSystem;
        _logger              = sharedSystem.GetLoggerFactory().CreateLogger<ExampleWithConfig>();
        DeathrunManager      = deathrunManagerApi;

        //set the config object matching the config structure we've defined in the generic interface
        Config = new TestConfigStructure();
        
        //set the config options object that will be used by the deathrun module to load the config
        ConfigOptions = new ConfigOptions
        {
            //override config's filename; default is `config` - no need to include the file extension
            FileName = "custom_config", 
            
            //optional; default is sharp/configs/Deathrun.Manager/modules/{moduleName}
            //if provided a custom config path, it will always be relative to the deathrun modules' config path
            CustomPath = "custom_path", 
            
            //whether to allow the deathrun module to be reloaded with the `ms_config_{moduleName}_reload` command
            AllowReloadCommand = true
        }; 
    }
    
    #region IDeathrunModule

    public void OnConfigParsed(TestConfigStructure config)
    {
        _logger.LogInformation("{coloredMsg}", "Configuration parsed successfully!");  
    }

    public bool Init(bool hotReload) => true;
    
    //The method is called when the DeathrunManager is shutting down, but before shutting down the DeathrunManager's managers
    public void Shutdown(bool hotReload) { }
 
    #endregion
    
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