# Basic example

The deathrun manager features a built-in loader allowing to write modules that cut the overhead when using the ModSharp modules [Shared API](https://docs.modsharp.net/docs/en-us/examples/module-api.html) implementation while still allowing to fully interact with the ModSharp's API itself

## Basic module utilizing both ModSharp's and DeathrunManager Shared API

> [!WARNING]
> The deathrun modules must follow this folder structure. The module's folder and .dll/deps.json files **must have the same name to load correctly**!

```csharp

└── Deathrun.Manager/
    └── modules/
        └── ExampleModule/
            ├── ExampleModule.dll
            └── ExampleModule.deps.json
```

`ExampleModule.cs`
```csharp

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

```

`ExampleModule.csproj`
```csharp

﻿<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>disable</ImplicitUsings>
        <LangVersion>13</LangVersion>
        <Nullable>enable</Nullable>
        <AssemblyName>ExampleModule</AssemblyName>
        <RootNamespace>ExampleModule</RootNamespace>
        <PlatformTarget>x64</PlatformTarget>
        <Version>0.0.1</Version>
    </PropertyGroup>
    
    <ItemGroup>
      <PackageReference Include="DeathrunManager.Shared" Version="*" />
      <PackageReference Include="ModSharp.Sharp.Shared" Version="*" />
    </ItemGroup>
</Project>

```

