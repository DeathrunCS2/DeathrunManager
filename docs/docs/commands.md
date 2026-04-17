# Commands

---

## Chat
```config

!uselife, /uselife - Respawns the caller if they are dead and have enough extra lives

```

```config

!kill, /kill - Kills the caller if they are alive

```

## Server
```config

ms_dr_modules "argument" "value" - Manage Deathrun modules

```
`Modules path: sharp/modules/Deathrun.Manager/modules`

- Available arguments:
  - **(no argument/value)** - Defaults to showing all loaded deathrun modules;
  - **list** - Show all loaded deathrun modules;
  - **load <module_folder_name>** - Tries to loads a deathrun module with the provided deathrun module's folder name;
  - **unload <module_(partial)name>** - Unloads a specific deathrun module by the given name/partial name;
  - **reload <empty/module_(partial)name>** - Reloads all modules if empty, otherwise try to reload a specific deathrun module by the given name/partial name;

#### Example: ms_dr_modules load Speedometer 
> [!NOTE]
> Following the example above, the deathrun module entry dll and folder structure must look like this: `sharp/modules/Deathrun.Manager/modules/Speedometer/Speedometer.dll`
