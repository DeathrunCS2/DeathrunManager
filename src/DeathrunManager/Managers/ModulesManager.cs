using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeathrunManager.Objects;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Types;

namespace DeathrunManager.Managers;

public class ModulesManager(
    ISharedSystem sharedSystem,
    IServiceProvider serviceProvider)
{
    private List<DeathrunModule>           _deathrunModules           = [];
    private readonly string                _deathrunModulesDir        = Path.Combine(DeathrunManager.Bridge.DllPath, "modules");
    
    #region Deathrun Modules
 
    public bool InitModules()
    {
        RegisterDeathrunModulesServerCommands();
        
        //create the deathrun modules directory if it doesn't exist' yet
        if (Directory.Exists(_deathrunModulesDir) is not true) 
            Directory.CreateDirectory(_deathrunModulesDir);
        
        //start modules
        var moduleFolders = Directory.GetDirectories(_deathrunModulesDir, "*", SearchOption.AllDirectories);
        
        //load every applicable module in the deathrun modules directory
        foreach (var moduleFolder in moduleFolders)
            LoadModule(moduleFolder);

        //notify all deathrun modules that we've loaded them'
        CallOnAllDeathrunModulesLoaded();
        
        return true;
    }

    private void LoadModule(string moduleFolder)
    {
        var runtimeConfigFile = Directory.GetFiles(moduleFolder, "*.deps.json").FirstOrDefault();
        var entryDll = runtimeConfigFile?.Replace(".deps.json", ".dll") ?? "error";
     
        if (_deathrunModules.Any(dModule =>
                dModule.Identifier.Contains(Path.GetFileNameWithoutExtension(entryDll), StringComparison.OrdinalIgnoreCase)) is true)
        {
            return;
        }
        
        var deathrunModule = new DeathrunModule(serviceProvider, entryDll);
                
        _deathrunModules.Add(deathrunModule);

        try { deathrunModule.Init(hotReload: false); }
        catch (Exception e)
        {
            _deathrunModules.Remove(deathrunModule);
            throw new ApplicationException($"[ModulesManager] Failed initializing deathrun module: {moduleFolder} | {e}");
        }
            
        try { deathrunModule.PostInit(hotReload: false); }
        catch (Exception e)
        { throw new ApplicationException($"[ModulesManager] Failed to call PostInit to deathrun module: " + $"{deathrunModule.Identifier} {e}", e); }
    }
    
    private void CallOnAllDeathrunModulesLoaded()
    {
        foreach (var deathrunModule in _deathrunModules)
            try { deathrunModule.OnAllDeathrunModulesLoaded(hotReload: false); }
            catch (Exception e) { throw new ApplicationException($"[ModulesManager] Failed to call OnAllDeathrunModulesLoaded from deathrun module: " + $"{deathrunModule.Identifier} {e}", e); }
    }

    public void OnAllModSharpModulesLoaded()
    {
        foreach (var deathrunModule in _deathrunModules)
            try { deathrunModule.OnAllModSharpModulesLoaded(); }
            catch (Exception e) { throw new ApplicationException($"[ModulesManager] Failed to call OnAllModSharpModulesLoaded from deathrun module: " + $"{deathrunModule.Identifier} {e}", e); }
    }

    public void ShutdownModules()
    {
        DeregisterDeathrunModulesServerCommands();
        
        foreach (var deathrunModule in _deathrunModules)
            deathrunModule.Shutdown(hotReload: false);
        
        _deathrunModules = new ();
    }

    #endregion
    
    #region Server Commands
       
    private void RegisterDeathrunModulesServerCommands()
    {
        sharedSystem.GetConVarManager()
            .CreateServerCommand("ms_dr_modules", OnDeathrunModulesCommand, 
                "Manage Deathrun modules", ConVarFlags.Release);
    }

    private void DeregisterDeathrunModulesServerCommands()
    {
        sharedSystem.GetConVarManager().ReleaseCommand("ms_dr_modules");
    }

    private ECommandAction OnDeathrunModulesCommand(StringCommand command)
    {
        //default to showing the loaded deathrun modules list if no argument is provided
        if (command.ArgCount is 0)
        {
            ShowLoadedDeathrunModules();
        }
        
        if (command.ArgCount is 1)
        {
            var actionName = command.GetArg(1);

            switch (actionName)
            {
                case "reload":
                    
                    var reloadedModules = 0;
                    //reload all modules if a module name was not provided and the action name is reload
                    foreach (var deathrunModule in _deathrunModules)
                    {
                        deathrunModule.Reload(shouldNotifyAllDeathrunModules: true);
                        reloadedModules++;
                    }
                    
                    if (reloadedModules >= 2) 
                        Log(ConsoleColor.Black, ConsoleColor.DarkMagenta, "Reloaded All Deathrun modules!", "");
                    
                    Log(ConsoleColor.Black, ConsoleColor.DarkMagenta, "Checking for recently added and/or uninitialized deathrun modules...", "");
                    InitModules();
                    break;
                
                case "list":

                    ShowLoadedDeathrunModules();
                    break;
            }
            
        }

        if (command.ArgCount is 2)
        {
            var actionName = command.GetArg(1);
            var moduleName = command.GetArg(2);

            var deathrunModule = _deathrunModules
                                    .FirstOrDefault(m => 
                                                    m.Identifier.Contains(moduleName, StringComparison.OrdinalIgnoreCase));
            
            switch (actionName)
            {
                case "reload":
                    
                    if (deathrunModule is null)
                    {
                        Console.WriteLine($"Deathrun module with name: {moduleName} not found!");
                        return ECommandAction.Stopped;
                    }
                    
                    deathrunModule.Reload(shouldNotifyAllDeathrunModules: false);
                    
                    Log(ConsoleColor.Black, ConsoleColor.DarkMagenta, "Reloaded Deathrun module", deathrunModule.Identifier);
                    break;
                
                case "load":
                    
                    var deathrunModuleFolder = Path.Combine(_deathrunModulesDir, moduleName);
                    if (Directory.Exists(deathrunModuleFolder) is not true)
                    {
                        Console.WriteLine($"Can't load deathrun module: {moduleName} at {deathrunModuleFolder}");
                        return ECommandAction.Stopped;
                    }

                    if (DeathrunManager.ServiceProvider is null)
                    {
                        Console.WriteLine($"ServiceProvider is null!");
                        return ECommandAction.Stopped;
                    }

                    if (_deathrunModules.Any(dModule => dModule.Identifier.Contains(moduleName, StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine($"Deathrun module with name: {moduleName} already loaded!");
                        return ECommandAction.Stopped;
                    }
                    
                    LoadModule(deathrunModuleFolder);
                    break;
                
                case "unload":
                    
                    if (deathrunModule is null)
                    {
                        Console.WriteLine($"Deathrun module with name: {moduleName} not found!");
                        return ECommandAction.Stopped;
                    }
                    _deathrunModules.Remove(deathrunModule);
                    
                    deathrunModule.Shutdown(hotReload: false);
                    break;
            }
        }
        
        return ECommandAction.Stopped;
        void ShowLoadedDeathrunModules()
        {
            var result = 0;
            var builder = new StringBuilder();
            builder.Append("  Deathrun Modules:\n");
            for (var i = 0; i < _deathrunModules.Count; i++)
            {
                var index   = i + 1;
                var module  = _deathrunModules[i];
                var version = module.Version;

                builder.Append($"    #{index,-2}\n");
                builder.Append($"      Name: {module.Name}\n");
                builder.Append($"      Entry DLL: {module.Identifier}\n");
                builder.Append($"      Version: {version.Major}.{version.Minor}.{version.Build}\n");
                builder.Append($"      Author: {module.Author}\n");
                builder.Append($"      State: {module.State.ToString()}\n");
                result++;
            }

            if (result is 0) builder.Append("    No deathrun modules found.\n");
                    
            Console.WriteLine(builder.ToString());
        }
    }
    
    #endregion
    
    #region ConsoleLog
    
    private static void Log(ConsoleColor textColor, ConsoleColor backgroundColor, string header, string message)
    {
        Console.ForegroundColor = textColor;
        Console.BackgroundColor = backgroundColor;
        Console.Write($"         {header}:");
        Console.ResetColor();
        Console.Write($" {message} \n");
    }
    
    #endregion
}






