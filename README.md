## CS2 Deathrun Manager for ModSharp | [Modules](https://github.com/CS2-Deathrun-Modules)

### Description

Deathrun Manager for CS2 servers. This is port of [this](https://forums.alliedmods.net/showthread.php?t=78197) plugin with few changes and improvements.

## Commands
### Chat
```config

!uselife, /uselife - Respawns the caller if they are dead and have enough extra lives

```

```config

!kill, /kill - Kills the caller if they are alive

```

### Server
```config

ms_dr_modules "argument" "value" - Manage Deathrun modules

```
`Modules path: sharp/modules/Deathrun.Manager/modules`

#### Available arguments:
- **(no argument/value)** - Defaults to showing all loaded deathrun modules;
- **list** - Show all loaded deathrun modules;
- **load <module_folder_name>** - Tries to loads a deathrun module with the provided deathrun module's folder name;
- **unload <module_(partial)name>** - Unloads a specific deathrun module by the given name/partial name;
- **reload <empty/module_(partial)name>** - Reloads all modules if empty, otherwise try to reload a specific deathrun module by the given name/partial name;

#### Example: ms_dr_modules load "Speedometer" ` 
- Following the example above, the deathrun module entry dll and folder structure must look like this:
- `sharp/modules/Deathrun.Manager/modules/Speedometer/Speedometer.dll`

### Installation

1. Download the latest release from [Releases](https://github.com/aquevadis/ms-deathrun-manager-release/releases).
2. Extract the `.zip` archive or Copy-Paste it directly into your server's root directory.
3. Restart server to generate config files.
4. Adjust the configuration files to your linking and restart the server again to reflect all changes.

### Configuration

The configurations values do exactly what they are labeled, so I believe it doesn't need further explanation.

#### deathrun.json(default)
```json
{
  "GiveWeaponToCTs": true,
  "RemoveBuyZones": true,
  "RemoveMoneyFromGameAndHud": true,
  "SetRoundTimeOneHour": true,
  "EnableClippingThroughTeamMates": true,
  "EnableAutoBunnyHopping": true,
  "EnableKillCommandForCTs": true,
  "EnableKillCommandForTs": false,
  "Prefix": "{GREEN}[Deathrun]{DEFAULT}"
}
```

#### lives_system.json(default)
```json
{
  "EnableLivesSystem": false,
  "StartLivesNum": 1,
  "ShowLivesCounter": true,
  "SaveLivesToDatabase": false,
  "Spacer": "// If SaveLivesToDatabase is true, you have to configure the database connection details below too.",
  "Host": "localhost",
  "Database": "database_name",
  "User": "database_user",
  "Password": "database_password",
  "Port": 3306,
  "TableName": "deathrun_players"
}
```

#### game_cvars.json(default)
```json
{
  "Cash": [
    //commands from this block are only executed if config var RemoveMoneyFromGameAndHud = true
  ],
  "Teams": [
    "mp_limitteams 0",
    "mp_autoteambalance false",
    "mp_autokick 0",
    "bot_quota_mode fill",
    "bot_join_team ct",
    "mp_ct_default_melee weapon_knife",
    "mp_ct_default_secondary weapon_usp_silencer",
    "mp_ct_default_primary",
    "mp_t_default_melee weapon_knife",
    "mp_t_default_secondary",
    "mp_t_default_primary",
    "sv_alltalk 0"
  ],
  "Movement": [
    "sv_enablebunnyhopping 1",
    "sv_airaccelerate 99999",
    "sv_wateraccelerate 50",
    "sv_accelerate_use_weapon_speed 0",
    "sv_maxspeed 9999",
    "sv_stopspeed 0",
    "sv_backspeed 0.1",
    "sv_accelerate 50",
    "sv_staminamax 0",
    "sv_maxvelocity 9000",
    "sv_staminajumpcost 0",
    "sv_staminalandcost 0",
    "sv_staminarecoveryrate 0"
  ],
  "RoundTimer": [
    //commands from this block are only executed if config var SetRoundTimeOneHour = true
  ],
  "PlayerClipping": [
    //commands from this block are only executed if config var EnableClippingThroughTeamMates = true
  ]
}
```

# Basic module example

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

 
