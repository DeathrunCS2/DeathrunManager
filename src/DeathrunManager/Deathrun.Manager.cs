using System;
using System.IO;
using DeathrunManager.Config;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Managers;
using DeathrunManager.Shared;
using DeathrunManager.Shared.Config;
using DeathrunManager.Shared.Managers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sharp.Shared;

namespace DeathrunManager;

public class DeathrunManager : IModSharpModule, IDeathrunManager
{
    public string DisplayName                            => $"Deathrun Manager (Built: {Bridge.FileTime} / Commit: {Bridge.CommitHashShort})";
    public string DisplayAuthor                          => "AquaVadis";

    private readonly ILogger<DeathrunManager>            _logger;
    private readonly ServiceProvider                     _serviceProvider;
    private readonly ServiceCollection                   _services                     = new ();
    private ModulesManager?                              _modulesManager;
    
    public ISharedSystem                                 SharedSystem                  { get; }
    public static IDeathrunManager                       Instance =                    null!;                    
    public static IServiceProvider?                      ServiceProvider               { get; private set; }
    
#pragma warning disable CA2211
    public static InterfaceBridge                        Bridge                        = null!;
    
    public DeathrunManager(ISharedSystem sharedSystem,
        string                   dllPath,
        string                   sharpPath,
        Version                  version,
        IConfiguration           coreConfiguration,
        bool                     hotReload)
    {
        Instance = this;
        Bridge = new InterfaceBridge(dllPath, sharpPath, version, sharedSystem);
        SharedSystem = sharedSystem;
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<DeathrunManager>();
        
        var configuration = new ConfigurationBuilder()
                                .AddJsonFile(Path.Combine(dllPath, "base.json"), true, false)
                                .Build();
        
        _services.AddSingleton<IDeathrunManager>(this);
        _services.AddSingleton(Bridge);
        _services.AddSingleton(Bridge.ClientManager);
        _services.AddSingleton(Bridge.EventManager);
        _services.AddSingleton(Bridge.EntityManager);
        _services.AddSingleton(Bridge.HookManager);
        _services.AddSingleton(Bridge.ModSharp);
        _services.AddSingleton(Bridge.SharpModuleManager);
        _services.AddSingleton(Bridge.ConVarManager);
        _services.AddSingleton(Bridge.LoggerFactory);
        _services.AddSingleton(sharedSystem);
        _services.AddSingleton<IConfiguration>(configuration);
        _services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

        _services.AddSingleton(ManagerConfig.LoadManagerBaseConfig());
        
        _services.AddNativeManagers();
        _services.AddDeathrunManagers();
        
        _serviceProvider = _services.BuildServiceProvider();
        ServiceProvider = _serviceProvider;
    }

    #region IModule
    
    public bool Init()
    {
        _logger.LogInformation("{colorMessage}", "Load Deathrun Manager");
        
        //load managers
        CallInitManagers();
        
        return true;
    }

    public void PostInit()
    {
        CallPostInitManagers();
        
        //expose shared interface
        Bridge.SharpModuleManager.RegisterSharpModuleInterface<IDeathrunManager>(this, IDeathrunManager.Identity, this);
    }
    
    public void OnAllModulesLoaded()
    {
        CallOnAllSharpModulesLoadedManagers();
     
        _modulesManager = new ModulesManager(SharedSystem, _serviceProvider);
        
        //load deathrun modules after all ModSharp modules have been loaded
        _modulesManager.InitModules();
        
        //notify all deathrun modules that we've loaded them'
        _modulesManager.OnAllModSharpModulesLoaded();
    }
    
    public void Shutdown()
    {
        _modulesManager?.ShutdownModules();

        CallShutdownManagers();
        
        _logger.LogInformation("[Manager] {colorMessage}", "Shutdown Deathrun Manager!");
    }
    
    #endregion
    
    #region Injected Instances' Caller methods
    
    private int CallInitManagers()
    {
        var init = 0;

        foreach (var service in _serviceProvider.GetServices<IManager>())
        {
            if (service.Init() is not true)
            {
                _logger.LogError("Failed to Init {service}!", service.GetType().FullName);

                return -1;
            }

            init++;
        }
        
        // _serviceProvider.GetService<IPlayersManager>()
        // _serviceProvider.GetService<IGameplayManager>()
        // _serviceProvider.GetService<IDeathrunManagers>()

        return init;
    }

    private void CallPostInitManagers()
    {
        foreach (var service in _serviceProvider.GetServices<IManager>())
        {
            try
            {
                service.PostInit();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while calling PostInit in {m}", service.GetType().Name);
            }
        }
    }

    private void CallShutdownManagers()
    {
        foreach (var service in _serviceProvider.GetServices<IManager>())
        {
            try
            {
                service.Shutdown();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while calling Shutdown in {m}", service.GetType().Name);
            }
        }
    }

    private void CallOnAllSharpModulesLoadedManagers()
    {
        foreach (var service in _serviceProvider.GetServices<IManager>())
        {
            try
            {
                service.OnAllModulesLoaded();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while calling OnAllSharpModulesLoaded in {m}", service.GetType().Name);
            }
        }
    }

    #endregion
    
    public IDeathrunManagers Managers => _serviceProvider.GetRequiredService<IDeathrunManagers>();
    
    public IManagerBaseConfig Config => _serviceProvider.GetRequiredService<IManagerBaseConfig>();
}