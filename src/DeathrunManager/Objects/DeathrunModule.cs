using System;
using System.IO;
using System.Linq;
using DeathrunManager.Managers;
using DeathrunManager.Shared;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DeathrunManager.Objects;

internal enum ModuleState
{
    Registering,
    Initializing,
    Running,
    Reloading,
    Unloading,
    Unloaded,
    Failed
}

internal class DeathrunModule : IDeathrunModule
{
    //internal properties and/or variables
    private PluginLoader?                           _moduleLoader;
    private IDeathrunModule?                        _instance;
    private IServiceScope?                          _serviceScope;
    private readonly string                         _moduleDllFilePath;
    private readonly string                         _tempPath;
    private readonly int                            _threadId;
    
    //module identity
    public string                                   Name                       => _instance?.Name ?? "Unknown";
    public string                                   Identifier                 { get; private set; }              
    public string                                   Author                     => _instance?.Author ?? "Unknown";
    public Version                                  Version                    => _instance?.GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); 
    
    //public properties and/or variables
    public IDeathrunManager                         DeathrunManager            => global::DeathrunManager.DeathrunManager.Instance;
    
    public ModuleState                              State                      { get; private set; }

    public DeathrunModule(IServiceProvider serviceProvider, string entryDll)
    {
        Identifier = Path.GetFileNameWithoutExtension(entryDll) ?? throw new ArgumentException("Module entry DLL is invalid!"); 

        _serviceScope = serviceProvider.CreateScope(); 
        _moduleDllFilePath = entryDll;
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _threadId  = Environment.CurrentManagedThreadId;
        
        State = ModuleState.Registering;
    }

    /// <summary>
    /// Initializes the Deathrun module by setting up its state, loading required assemblies,
    /// and preparing the module for runtime. This method handles the core initialization logic
    /// and ensures the module is ready for operation. If called during a hot reload process,
    /// certain components are reloaded to reflect updated configurations or states.
    /// </summary>
    /// <param name="hotReload">
    /// Specifies whether the initialization is performed as part of a hot reload. If true,
    /// the method re-initializes the module components without shutting down dependencies.
    /// </param>
    /// <returns>
    /// Returns true if the initialization completes successfully; otherwise, an exception is thrown.
    /// </returns>
    /// <exception cref="ApplicationException">
    /// Thrown when required resources, such as the module's DLL file or service scope,
    /// are not available or when the module's initialization process fails.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when paths or module directory configurations are invalid.
    /// </exception>
    /// <exception cref="BadImageFormatException">
    /// Thrown when the module's assembly is missing or doesn't implement the required
    /// <see cref="IDeathrunModule"/> interface in a valid manner.
    /// </exception>
    public bool Init(bool hotReload)
    {
        if (File.Exists(_moduleDllFilePath) is not true)
            throw new ApplicationException($"Deathrun Module's entry DLL file: '{_moduleDllFilePath}' doesn't exist!");

        if (_serviceScope is null)
            throw new ApplicationException("Service scope is null!");
        
        State                      = ModuleState.Initializing;
        
        if (hotReload is not true)
            _moduleLoader          = null;

        try
        {
            Directory.CreateDirectory(_tempPath);

            var moduleDir = Path.GetDirectoryName(_moduleDllFilePath) ?? throw new ArgumentException("Module directory is invalid!");
            foreach (var file in Directory.GetFiles(moduleDir))
            {
                File.Copy(file, Path.Combine(_tempPath, Path.GetFileName(file)), true);
            }
            
            var tempDll = Path.Combine(_tempPath, Path.GetFileName(_moduleDllFilePath));
            
            var moduleLoader = PluginLoader.CreateFromAssemblyFile(tempDll,
                                                                    config =>
                                                                    {
                                                                        config.PreferSharedTypes = true;
                                                                        config.IsLazyLoaded      = false;
                                                                        config.IsUnloadable      = true;
                                                                        config.LoadInMemory      = false;
                                                                        config.EnableHotReload   = false;
                                                                    });
        
            var assembly = moduleLoader.LoadDefaultAssembly();
            var deathrunModuleAssembly = assembly
                                                 .GetTypes()
                                                 .FirstOrDefault( t => typeof(IDeathrunModule).IsAssignableFrom(t) 
                                                                            && t.IsAbstract is not true) 
                                                  ?? throw new BadImageFormatException("Missing IDeathrunModule interface!");      
    
            if (ActivatorUtilities.CreateInstance(_serviceScope.ServiceProvider, deathrunModuleAssembly)
                is not IDeathrunModule { } deathrunModule)
            {
                throw new ApplicationException($"Class: '{assembly.GetName().Name}' doesn't implement IDeathrunModule interface!");
            }

            _moduleLoader = moduleLoader;
            _instance = deathrunModule;
            
            if (_instance.Init(hotReload) is not true)
                throw new ApplicationException($"Failed to initialize deathrun module: {assembly.GetName().Name}");
            
            State = ModuleState.Running;
            
            //call post-init
            _instance?.PostInit(hotReload); 
            
            Log(ConsoleColor.Black, ConsoleColor.Green, "Load Deathrun Module", $"{Identifier}");
            return true;
        }
        catch (Exception e)
        {
            State = ModuleState.Failed;

            try { _instance?.Shutdown(hotReload: false); }
            catch { throw new ApplicationException($"An error occurred when trying to shutdown deathrun module: {Identifier}"); }
            
            throw new ApplicationException($"An error occurred when trying to initialize deathrun module: {Identifier}", e);
        }
    }

    /// <summary>
    /// Performs post-initialization tasks after the module has been initialized.
    /// This is typically used for operations that depend on the successful completion
    /// of the module's initialization or for setting up components that were not
    /// configured during the initial setup phase.
    /// </summary>
    /// <param name="hotReload">
    /// Indicates whether the method is being invoked as a result of a hot reload.
    /// If true, the module should reload any necessary state or configuration
    /// that may have changed during runtime.
    /// </param>
    public void PostInit(bool hotReload) => _instance?.PostInit(hotReload);

    /// <summary>
    /// Invoked when all ModSharp modules have completed loading. This provides
    /// an opportunity for additional setup or initialization tasks that depend
    /// on all ModSharp modules being fully loaded and operational.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if called when the module is in an invalid or uninitialized state.
    /// </exception>
    public void OnAllModSharpModulesLoaded() => _instance?.OnAllModSharpModulesLoaded();

    /// <summary>
    /// Invoked when all Deathrun modules have finished loading. This allows modules
    /// to perform any final actions or initializations now that all modules are fully loaded.
    /// </summary>
    /// <param name="hotReload">
    /// A boolean value indicating whether the loading process was a result of a hot reload.
    /// Pass true if it is part of a hot reload operation; otherwise, pass false for normal module load behavior.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this method is invoked when modules are in an invalid state or incomplete.
    /// </exception>
    public void OnAllDeathrunModulesLoaded(bool hotReload) => _instance?.OnAllDeathrunModulesLoaded(hotReload);

    /// <summary>
    /// Shuts down the current Deathrun module, ensuring proper cleanup of resources
    /// and transitioning the module state to either unloaded or prepared for hot reload.
    /// </summary>
    /// <param name="hotReload">
    /// A boolean value indicating whether the shutdown is being performed for a hot reload.
    /// Pass true to prepare for a reload; otherwise, pass false to perform a complete resource cleanup.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the method is called from a thread other than the one where the module was instantiated.
    /// </exception>
    public void Shutdown(bool hotReload)
    {
        if (_threadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Shutdown must be called from the same thread as the constructor.");

        State = ModuleState.Unloading;

        _instance?.Shutdown(hotReload);
        
        if (hotReload)
        {
            //reload module context
            _moduleLoader?.Reload();
        }
        else
        {
            try
            {
                _serviceScope?.Dispose();
                _moduleLoader?.Dispose();
            }
            catch
            {
                /**/
            }
            finally
            {
                _instance = null;
                _moduleLoader = null;
                _serviceScope = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        
        Log(ConsoleColor.Black, ConsoleColor.DarkRed, "Shutdown Deathrun Module", $"{Identifier}");
        State = ModuleState.Unloaded;
    }

    /// <summary>
    /// Reloads the current Deathrun module. This involves shutting down, re-initializing,
    /// and optionally notifying all Deathrun modules about the reload process.
    /// </summary>
    /// <param name="shouldNotifyAllDeathrunModules">
    /// A boolean value indicating whether all Deathrun modules should be notified about the reload.
    /// Pass true to notify other modules; otherwise, pass false.
    /// </param>
    /// <returns>
    /// Returns true if the reload operation completes successfully; otherwise, false.
    /// </returns>
    /// <exception cref="ApplicationException">
    /// Thrown if the reload process fails due to an unexpected error during shutdown or initialization.
    /// </exception>
    public bool Reload(bool shouldNotifyAllDeathrunModules)
    {
        if (_threadId != Environment.CurrentManagedThreadId)
        {
            Console.WriteLine(new InvalidOperationException("Reload must be called from the same thread."));
            return false;
        }
        
        State = ModuleState.Reloading;

        try
        {
            Shutdown(hotReload: true);
            
            if (Init(hotReload: true) is not true)
            {
                Log(ConsoleColor.Black, ConsoleColor.Red, "Failed to Initialize Deathrun Module when being reloaded", "");
                return false;
            }
            
            PostInit(hotReload: true);
            
            if (shouldNotifyAllDeathrunModules) OnAllDeathrunModulesLoaded(hotReload: true);
        }
        catch (Exception e)
        {
            throw new ApplicationException($"Failed to reload deathrun module: {GetType().Assembly.GetName().Name} | {e}");
        }
        
        return true;
    }

    private static void Log(ConsoleColor textColor, ConsoleColor backgroundColor, string header, string message)
    {
        Console.ForegroundColor = textColor;
        Console.BackgroundColor = backgroundColor;
        Console.Write($"         {header}:");
        Console.ResetColor();
        Console.Write($" {message} \n");
    }
}