using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DeathrunManager.Config;
using DeathrunManager.Extensions;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Interfaces.Managers.Native;
using DeathrunManager.Objects;
using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.Managers;
using DeathrunManager.Shared.DeathrunObjects;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.HookParams;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using IEventManager = DeathrunManager.Interfaces.Managers.Native.IEventManager;
using IHookManager = Sharp.Shared.Managers.IHookManager;

namespace DeathrunManager.Managers;

public class GameplayManager(
    ILogger<GameplayManager> logger,
    IModSharp modSharp,
    IHookManager hookManager,
    IEntityManager entityManager,
    IClientManager clientManager,
    IEventManager eventManager,
    IConVarManager conVarManager,
    IPlayersManager playersManager,
    ManagerBaseConfig baseConfig) : IManager, IGameplayManager, IClientListener, IGameListener, IEntityListener
{
    public static GameplayManager Instance = null!;
    
    private DRoundState _deathrunRoundState = DRoundState.Unset;
    private void SetRoundState(DRoundState newState) { _deathrunRoundState = newState; }
    public DRoundState GetRoundState() => _deathrunRoundState;
    
    private DeathrunPlayer? _gameMasterDeathrunPlayer = null;
    private void SetGameMaster(DeathrunPlayer? gameMaster) { _gameMasterDeathrunPlayer = gameMaster; }
    public IDeathrunPlayer? GetGameMaster() => _gameMasterDeathrunPlayer;

    public event IGameplayManager.GameMasterPickedDelegate? GameMasterPicked;
    
    public event IGameplayManager.DeathrunPlayerSpawnPostDelegate? DeathrunPlayerSpawned;
    public event IGameplayManager.DeathrunPlayerDeathPostDelegate? DeathrunPlayerKilled;

    public event IGameplayManager.RoundStartDelegate? RoundStarted;
    public event IGameplayManager.RoundEndDelegate? RoundEnded;
    
    public event IGameplayManager.GameStartDelegate? GameStarted;
    public event IGameplayManager.MapStartDelegate? MapStarted;
    public event IGameplayManager.MapEndDelegate? MapEnded;

    
    // ReSharper disable once InconsistentNaming
    private static DeathrunGameModeVarsConfig GameVarsConfig = null!;
    
    private IConVar? _autoBunnyHopCvar  = null;
    
    private bool _mapStarted = false;
    private bool _gameStarted = false;
    
    #region IModule
    
    public bool Init()
    {
        LoadGameVarsConfig();
        
        Instance = this;

        if (baseConfig.EnableAutoBunnyHopping is true)
        {
            _autoBunnyHopCvar = conVarManager.FindConVar("sv_autobunnyhopping");
            if (_autoBunnyHopCvar is not null) _autoBunnyHopCvar.Flags &= ~ConVarFlags.Replicated;
            
            hookManager.PlayerRunCommand.InstallHookPre(OnPlayerRunCommandPre);
        }
        
        hookManager.PlayerSpawnPost.InstallForward(PlayerSpawnPost);
        hookManager.PlayerKilledPost.InstallForward(PlayerKilledPost);
        hookManager.HandleCommandJoinTeam.InstallHookPre(CommandJoinTeamPre);
        
        clientManager.InstallClientListener(this);
        modSharp.InstallGameListener(this);
        entityManager.InstallEntityListener(this);
        
        eventManager.HookEvent("round_start", OnRoundStart);
        eventManager.HookEvent("round_end", OnRoundEnd);
        
        return true;
    }
    
    public void Shutdown()
    {
        hookManager.PlayerSpawnPost.RemoveForward(PlayerSpawnPost);
        hookManager.PlayerKilledPost.RemoveForward(PlayerKilledPost);
        hookManager.HandleCommandJoinTeam.RemoveHookPre(CommandJoinTeamPre);
        
        if (baseConfig.EnableAutoBunnyHopping is true)
        {
            hookManager.PlayerRunCommand.RemoveHookPre(OnPlayerRunCommandPre);
        }
        
        clientManager.RemoveClientListener(this);
        modSharp.RemoveGameListener(this);
        entityManager.RemoveEntityListener(this);
    }

    #endregion

    #region Hooks
    
    private HookReturnValue<EmptyHookReturn> OnPlayerRunCommandPre(IPlayerRunCommandHookParams parms, HookReturnValue<EmptyHookReturn> returnValue)
    {
        var client = parms.Client;
        if (client?.IsValid is not true) return new();
        
        _autoBunnyHopCvar?.ReplicateToClient(client, "1");
        _autoBunnyHopCvar?.Set(1);
        
        return new();
    }
    
    private HookReturnValue<bool> CommandJoinTeamPre(IHandleCommandJoinTeamHookParams parms, HookReturnValue<bool> result)
    {
        var deathrunPlayer = playersManager.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return new ();
        
        //block team change for game masters
        if (deathrunPlayer.Class is DPlayerClass.GameMaster) return new (EHookAction.SkipCallReturnOverride);
        
        //allow the player to join the spectators or CT team freely
        if (parms.Team is (int) CStrikeTeam.Spectator or (int) CStrikeTeam.CT) return new ();
    
        //if the player didn't click on any team option, auto-assign to CT
        if (parms.Team is (int)CStrikeTeam.UnAssigned) deathrunPlayer.Controller?.SwitchTeam(CStrikeTeam.CT);
        
        //block any other team change option/s
        return new (EHookAction.SkipCallReturnOverride);
    }
    
    private void PlayerSpawnPost(IPlayerSpawnForwardParams parms)
    {
        var deathrunPlayer = playersManager.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return;
        
        if (deathrunPlayer != GetGameMaster())
            deathrunPlayer.ChangeClass(DPlayerClass.Contestant);
        
        modSharp.PushTimer(() =>
        {
            var deathrunPlayerWeapons = deathrunPlayer.PlayerPawn?.GetWeaponService()?.GetMyWeapons();
            if (deathrunPlayerWeapons is null) return;

            if (deathrunPlayerWeapons.Count is 0)
            {
                var defaultKnife = deathrunPlayer.Class is DPlayerClass.Contestant
                    ? EconItemId.KnifeCt
                    : EconItemId.KnifeTe;
                deathrunPlayer.PlayerPawn?.GiveNamedItem(defaultKnife);
                
                if (deathrunPlayer.Class is DPlayerClass.Contestant && baseConfig.GiveWeaponToCTs is true)
                    deathrunPlayer.PlayerPawn?.GiveNamedItem(EconItemId.UspSilencer);
            }

            DeathrunPlayerSpawned?.Invoke(deathrunPlayer);

        }, 0.015625f * 2);
    }
    
    private void PlayerKilledPost(IPlayerKilledForwardParams parms)
    {
        var victimDPlayer = playersManager.GetDeathrunPlayer(parms.Client);
        if (victimDPlayer is null) return;

        var attackerDPlayer = playersManager.GetDeathrunPlayer(parms.AttackerPawnHandle);
        if (attackerDPlayer is null) return;

        var attackerDPlayerWeaponEntity = entityManager.FindEntityByHandle(parms.AbilityHandle);
        if (attackerDPlayerWeaponEntity?.IsValidEntity is not true) return;
        
        DeathrunPlayerKilled?.Invoke(victimDPlayer, 
                                   attackerDPlayer, 
                                   attackerDPlayerWeaponEntity, 
                                   parms.DamageTaken, 
                                   parms.Damage);
    }

    #endregion
    
    #region Listeners
    
    //Game Listeners
    public void OnGameInit()
    {
        //skip if the game has already started
        if (_gameStarted) return;
        
        GameStarted?.Invoke(modSharp.GetMapName() ?? "error");

        //set that the game started
        _gameStarted = true;
    }
    
    public void OnGameActivate()
    {
        //skip if the map has already started
        if (_mapStarted) return;
        
        StartMapThinker();
        
        //fire that the map started
        MapStarted?.Invoke(modSharp.GetMapName() ?? "error");
        
        ExecGameVars();
        
        //set that the map started
        _mapStarted = true;
    }
    
    public void OnGameDeactivate()
    {
        //set that the current map has ended
        _mapStarted = false;
        
        //set that the current game has ended
        _gameStarted = false;
        
        //fire that the map ended
        MapEnded?.Invoke(modSharp.GetMapName() ?? "error");
    }

    //Client Listeners
    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (client?.IsValid is not true) return;
        
        var deathrunPlayer = playersManager.GetDeathrunPlayer(client);
        if (deathrunPlayer is null) return;

        if (deathrunPlayer.Class is DPlayerClass.GameMaster)
        {
            modSharp.GetGameRules().TerminateRound(3, RoundEndReason.CTsWin);
        }
    }
    
    //Entity Listeners
    public void OnEntitySpawned(IBaseEntity entity)
    {
        if (entity.IsValidEntity is not true) return;
        
        //skip if RemoveBuyZones is disabled in the config
        if (baseConfig.RemoveBuyZones is not true) return;
        
        if (entity.Classname.Contains("buyzone", StringComparison.OrdinalIgnoreCase))
        {
            modSharp.InvokeFrameAction(() =>
            {
                if (entity?.IsValidEntity is not true) return;
                entity.AcceptInput("Kill");
            });
        }
    }
    
    #endregion
    
    #region Events
    
    private HookReturnValue<bool> OnRoundStart(EventHookParams evParms)
    {
        StartDeathrunRound();
        RoundStarted?.Invoke();
        return new ();
    }

    private HookReturnValue<bool> OnRoundEnd(EventHookParams evParms)
    {
        EndDeathrunRound();
        RoundEnded?.Invoke();
        return new ();
    }

    #endregion

    #region Deathrun Round

    private void StartDeathrunRound()
    {
        SetRoundState(DRoundState.StartPre);
        
        //skip if we are in a warmup period;
        if (modSharp.GetGameRules().IsWarmupPeriod is true)
        {
            logger.LogInformation("[GameplayManager][OnRoundStart] {colorMessage}", "Game mode stopped during warmup period!");
            return;
        }
        
        SetRoundState(DRoundState.CheckGameModeRequirements);
        
        //check if we have enough players to start the deathrun;
        //we need at least 2 live players
        if (playersManager.GetAllAliveDeathrunPlayers().Count < 2)
        {
            //ingame-msg: "Not enough players to start the deathrun"
            return;
        }
        
        //delay the picking of the game master by one tick
        modSharp.PushTimer(() =>
        {
            SetRoundState(DRoundState.PickingGameMaster);

            //return if pick game master failed
            if (PickGameMaster() is not true)
            {
                logger.LogInformation("[GameplayManager][OnRoundStart] {colorMessage}", "Failed picking game master!");
                modSharp.GetGameRules().TerminateRound(2, RoundEndReason.RoundDraw);
                return;
            }
        
            SetRoundState(DRoundState.PickedGameMaster);
            
            //
            
            SetRoundState(DRoundState.StartPost);
            
        }, 0.015625f);
    }
    
    private void EndDeathrunRound()
    {
        SetRoundState(DRoundState.EndPre);

        //reset the game master
        SetGameMaster(null);
        
        SetRoundState(DRoundState.EndPost);
    }
    
    #endregion
    
    #region Game Master

    private bool PickGameMaster()
    {
        var candidateGameMasterDeathrunPlayers 
            = playersManager
                .GetAllAliveDeathrunPlayers()
                .Where(deathrunPlayer =>
                {
                    if (deathrunPlayer is not DeathrunPlayer { } ) return false;
                    
                    //if SkipNextGameMasterPickUp is false - keep the candidate in the list
                    if (deathrunPlayer.SkipNextGameMasterPickUp is not true) return true;
                    
                    //remove the candidate from predicate and pre-add for next check
                    deathrunPlayer.SkipNextGameMasterPickUp = false;
                    return false;
                })
                .ToList();
        
        var gameMasterDeathrunPlayer = candidateGameMasterDeathrunPlayers.Count is 1 ? 
                                        candidateGameMasterDeathrunPlayers.FirstOrDefault() 
                                        : candidateGameMasterDeathrunPlayers.ElementAtOrDefault(Random.Shared.Next(candidateGameMasterDeathrunPlayers.Count));
        
        if (gameMasterDeathrunPlayer is null) return false;

        gameMasterDeathrunPlayer.ChangeClass(DPlayerClass.GameMaster);
        gameMasterDeathrunPlayer.SkipNextGameMasterPickUp = true;
        SetGameMaster(gameMasterDeathrunPlayer as DeathrunPlayer);
        GameMasterPicked?.Invoke(gameMasterDeathrunPlayer);
        return true;
    }

    #endregion
    
    #region GameVars

    private static void LoadGameVarsConfig()
    {
        if (!Directory.Exists(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager")) 
            Directory.CreateDirectory(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager");
        
        var configPath = Path.Combine(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager/game_cvars.json");
        if (!File.Exists(configPath)) CreateGameVarsConfig(configPath);

        var config = JsonSerializer.Deserialize<DeathrunGameModeVarsConfig>(File.ReadAllText(configPath))!;
        GameVarsConfig = config;
    }
    
    private static void CreateGameVarsConfig(string configPath)
    {
        var config = new DeathrunGameModeVarsConfig
        {
            Cash = new List<string> 
            {
                //disable cash
                "cash_player_bomb_defused 0",
                "cash_player_bomb_planted 0",
                "cash_player_damage_hostage -30",
                "cash_player_interact_with_hostage 0",
                "cash_player_killed_enemy_default 0",
                "cash_team_win_by_time_running_out_bomb 0",
                "cash_player_killed_enemy_factor 0",
                "cash_player_killed_hostage -1000",
                "cash_player_killed_teammate -300",
                "cash_player_rescued_hostage 0",
                "cash_team_elimination_bomb_map 0",
                "cash_team_elimination_hostage_map_t 0",
                "cash_team_elimination_hostage_map_ct 0",
                "cash_team_hostage_alive 0",
                "cash_team_hostage_interaction 0",
                "cash_team_loser_bonus 0",
                "cash_team_bonus_shorthanded 0",
                "cash_team_loser_bonus_consecutive_rounds 0",
                "cash_team_planted_bomb_but_defused 0",
                "cash_team_rescued_hostage 0",
                "cash_team_terrorist_win_bomb 0",
                "cash_team_win_by_defusing_bomb 0",
                "cash_team_win_by_hostage_rescue 0",
                "cash_team_win_by_time_running_out_hostage 0",
                "mp_playercashawards 0",
                "mp_teamcashawards 0",
                "mp_startmoney 0",
                "mp_maxmoney 0",
                "mp_afterroundmoney 0"  
            },
            Teams = new List<string> 
            {
                //config teams behavior
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
            },
            Movement = new List<string> 
            {
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
            },
            RoundTimer = new List<string> 
            {
                //roundtimer cvars
                "mp_roundtime 60",
                "mp_roundtime_defuse 60",
                "mp_roundtime_hostage 60"
            },
            PlayerClipping = new List<string> 
            {
                "mp_solid_teammates 2"
            }
        };
            
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }
    
    public static void ReloadGameVarsConfig() { LoadGameVarsConfig(); }

    private void ExecGameVars()
    {
        logger.LogInformation("[Deathrun][GameplayManager] {colorMessage}", "Start executing game mode cvars!");

        ExecuteGameVarsChunks(GetGameVarsChunks(GameVarsConfig.Teams, 6));
        ExecuteGameVarsChunks(GetGameVarsChunks(GameVarsConfig.Movement, 6));
        
        if (baseConfig.RemoveMoneyFromGameAndHud is true)
            ExecuteGameVarsChunks(GetGameVarsChunks(GameVarsConfig.Cash, 6));
        if (baseConfig.SetRoundTimeOneHour is true)
            ExecuteGameVarsChunks(GetGameVarsChunks(GameVarsConfig.RoundTimer, 6));
        if (baseConfig.EnableClippingThroughTeamMates is true)
            ExecuteGameVarsChunks(GetGameVarsChunks(GameVarsConfig.PlayerClipping, 6));
    }

    private static void ExecuteGameVarsChunks(IEnumerable<string> gameVarsChunks)
    {
        foreach (var gameVarsChunk in gameVarsChunks)
        {
            DeathrunManager.Bridge.ModSharp.ServerCommand(gameVarsChunk);
        }
    }
    
    private static IEnumerable<string> GetGameVarsChunks(IReadOnlyList<string> commands, int chunkSize)
    {
        for (var i = 0; i < commands.Count; i += chunkSize)
        {
            var builder = new StringBuilder();
            var end = Math.Min(i + chunkSize, commands.Count);

            for (var j = i; j < end; j++)
            {
                if (j > i) builder.Append("; ");

                builder.Append(commands[j]);
            }
            yield return builder.ToString();
        }
    }

    #endregion

    #region Thinkers

    private void StartMapThinker()
    {
        //game teams unstuck logic
        modSharp.PushTimer(() =>
        {
            //skip if we are already ending the round
            if (GetRoundState() >= DRoundState.EndPre
                || GetRoundState() is >= DRoundState.PickingGameMaster) return;
            
            var validDeathrunPlayers 
                = playersManager
                    .GetAllAliveDeathrunPlayers().FilterPlayers(deathrunPlayer => deathrunPlayer.Controller?.Team 
                                                                                  is not CStrikeTeam.Spectator 
                                                                                  and CStrikeTeam.UnAssigned);

            if (validDeathrunPlayers.Count is 0 or 1) return;
            
            var hasTSidePlayer = false;

            foreach (var validDeathrunPlayer in validDeathrunPlayers)
            {
                if (validDeathrunPlayer.Controller?.Team is CStrikeTeam.TE)
                {
                    hasTSidePlayer = true;
                    break;
                }
            }

            if (hasTSidePlayer is not true)
            {
                modSharp.ServerCommand("bot_add t");
                modSharp.ServerCommand("bot_kick t");
            }

            _deathrunRoundState = DRoundState.Unset;

        }, 5f, GameTimerFlags.Repeatable | GameTimerFlags.StopOnMapEnd);
    }
    
    #endregion
    
    #region Listener's overrides
    
    int IClientListener.ListenerVersion => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 9;
    int IGameListener.ListenerVersion => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 9;
    int IEntityListener.ListenerVersion => IEntityListener.ApiVersion;
    int IEntityListener.ListenerPriority => 9;
    
    #endregion
}

public class DeathrunGameModeVarsConfig
{
    public List<string> Cash { get; init; } = [];
    public List<string> Teams { get; init; } = [];
    public List<string> Movement { get; init; } = [];
    public List<string> RoundTimer { get; init; } = [];
    public List<string> PlayerClipping { get; init; } = [];
}



