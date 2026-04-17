using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DeathrunManager.Config;
using DeathrunManager.Extensions;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Objects;
using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.Managers;
using DeathrunManager.Shared.DeathrunObjects;
using MySqlConnector;
using Sharp.Shared;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace DeathrunManager.Managers;

internal class PlayersManager(
    IModSharp modSharp,
    IClientManager clientManager,
    ManagerBaseConfig baseConfig) : IManager, IPlayersManager, IClientListener
{
    public static PlayersManager Instance = null!;

    public readonly ConcurrentDictionary<ulong, DeathrunPlayer> DeathrunPlayers = new();

    #region Events

    public event IPlayersManager.DeathrunPlayerChangedClassDelegate? ChangedClass;
    internal void InvokeChangedClass(IDeathrunPlayer deathrunPlayer, DPlayerClass newClass)
        => ChangedClass?.Invoke(deathrunPlayer, newClass);

    public event IPlayersManager.DeathrunDeathrunPlayerThinkPostDelegate? ThinkPost;
    internal void InvokeDeathrunPlayerThinkPost(IDeathrunPlayer deathrunPlayer)
        => ThinkPost?.Invoke(deathrunPlayer);

    public event IPlayersManager.DeathrunDeathrunPlayerSendChatMessageDelegate? SendChatMessage;
    internal PlayerSendChatMessageEventArgs InvokeDeathrunPlayerSendChatMessage(IDeathrunPlayer deathrunPlayer, string coloredChatMessage)
    {
        var args = new PlayerSendChatMessageEventArgs(coloredChatMessage);
        SendChatMessage?.Invoke(deathrunPlayer, args);
        return args;
    }

    public event IPlayersManager.DeathrunPlayerCreatedDelegate? Created;
    internal void InvokeCreated(IDeathrunPlayer deathrunPlayer)
        => Created?.Invoke(deathrunPlayer);

    public event IPlayersManager.DeathrunPlayerRemovedDelegate? Removed;
    internal void InvokeRemoved(IDeathrunPlayer deathrunPlayer)
        => Removed?.Invoke(deathrunPlayer);

    #endregion
    
    #region IModule
    
    public bool Init()
    {
        Instance = this;
        
        modSharp.InstallGameFrameHook(null, OnGameFramePost);
        clientManager.InstallClientListener(this);
        
        clientManager.InstallCommandCallback("kill", OnClientKillCommand);
        
        return true;
    }

    public static void OnPostInit() { }

    public void Shutdown()
    {
        ClearDeathrunPlayers();
        
        modSharp.RemoveGameFrameHook(null, OnGameFramePost);
        clientManager.RemoveClientListener(this);
        
        clientManager.RemoveCommandCallback("kill", OnClientKillCommand);
    }

    #endregion

    #region Hooks

    private readonly List<IDeathrunPlayer> _deathrunPlayersBuffer = new(64);
    
    private void OnGameFramePost(bool simulating, bool bFirstTick, bool bLastTick)
    {
        GetAllValidDeathrunPlayersZAlloc(_deathrunPlayersBuffer);

        foreach (var iDeathrunPlayer in _deathrunPlayersBuffer)
        {
            //skip bots here
            if (iDeathrunPlayer.Client.SteamId == 0) continue;
            
            if (iDeathrunPlayer is not DeathrunPlayer {} deathrunPlayer) continue;
            
            if (deathrunPlayer.IsThinking is not true) continue;
            
            //call the player think method
            deathrunPlayer.PlayerThink();
            InvokeDeathrunPlayerThinkPost(deathrunPlayer);
        }
    }

    #endregion
    
    #region Listeners
    
    public void OnClientConnected(IGameClient client)
    {
        if (client?.IsValid is not true) return;
        
        //skip if we couldn't add the client to the deathrun players dictionary
        DeathrunPlayer? deathrunPlayer;
        if (DeathrunPlayers.TryAdd(client.SteamId != 0 ? client.SteamId : client.Slot,
                                 deathrunPlayer = new DeathrunPlayer(client)) is not true)
                                                                                   return;
        if (deathrunPlayer?.LivesSystem is null) return;
  
        InvokeCreated(deathrunPlayer);
        
        //skip getting data from the database if we've set the SaveLivesToDatabase to false
        if (LivesSystemManager.LivesSystemConfig?.SaveLivesToDatabase is not true)
        {
            deathrunPlayer.LivesSystem?.SetLivesNum(LivesSystemManager.LivesSystemConfig?.StartLivesNum ?? 1);
        }
        else
        {
            Task.Run(async () =>
            {
                ulong steamId64 = deathrunPlayer.Client.SteamId;
                var livesNumFromDb = await GetSavedLives(steamId64);
                
                deathrunPlayer.LivesSystem?.SetLivesNum(livesNumFromDb);
            });
        }
    }
    
    public void OnClientPutInServer(IGameClient client)
    {
        if (client?.IsValid is not true) return;
        
        //skip if we couldn't add the client to the deathrun players dictionary
        DeathrunPlayer? deathrunPlayer;
        if (DeathrunPlayers.TryAdd(client.SteamId != 0 ? client.SteamId : client.Slot,
                deathrunPlayer = new DeathrunPlayer(client)) is not true)
            return;
        
        if (deathrunPlayer?.LivesSystem is null) return;
        
        InvokeCreated(deathrunPlayer);
    }
    
    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (client?.IsValid is not true) return;

        if (DeathrunPlayers.TryRemove(client.SteamId != 0 ? client.SteamId : client.Slot, out var removedDeathrunPlayer) is true)
        {
            //remove thinking functions
            removedDeathrunPlayer.StopPlayerThink();
            
            InvokeRemoved(removedDeathrunPlayer);

            //skip bots here
            if (client.SteamId == 0) return;
            
            //check if the lives system is enabled and we are saving the lives to the database
            if (LivesSystemManager.LivesSystemConfig?.EnableLivesSystem is true
                && LivesSystemManager.LivesSystemConfig.SaveLivesToDatabase is true)
            {
                if (removedDeathrunPlayer.LivesSystem is null) return;
                
                ulong steamId64 = removedDeathrunPlayer.Client.SteamId;
                var livesNum = removedDeathrunPlayer.LivesSystem.GetLivesNum;
                
                Task.Run(() => SaveLivesToDatabase(steamId64, livesNum));
            }
        }
    }
    
    public ECommandAction OnClientSayCommand(IGameClient client, bool teamOnly, bool isCommand, string commandName, string message)
    {
        var deathrunPlayer = GetDeathrunPlayer(client);
        if (deathrunPlayer is null) return  ECommandAction.Skipped;
        
        if (deathrunPlayer.IsValid is not true
            || deathrunPlayer.Controller is null) return ECommandAction.Stopped;
        
        //intercept say/say_team commands
        if (commandName is "say" or "say_team")
        {
            if (message.StartsWith('/') is not true && message.StartsWith('`') is not true)
            {
                var deadIcon = deathrunPlayer.PlayerPawn?.IsAlive is not true ? $"{ChatColor.White}☠{ChatColor.White}" : "";
                
                var team = deathrunPlayer.Controller.Team;
                var teamColor = team switch
                {
                    CStrikeTeam.CT => "{LIGHTBLUE}",
                    CStrikeTeam.TE => "{GOLD}",
                    _ => ""
                };

                var chatTargetTeamName 
                    = teamOnly is not true ? "[ALL]" 
                                            : $"{teamColor}[{team.ToString().ToUpper()}]";
                var chatMessage 
                    = $"{chatTargetTeamName} "
                      + $"{deadIcon} {teamColor}{deathrunPlayer.Client.Name}{{DEFAULT}}: "
                      + $"{message}";

                var filter = teamOnly ? new RecipientFilter(team) : new RecipientFilter();
                deathrunPlayer.SendChatMessage(chatMessage, filter, false);
            }
            
            return ECommandAction.Stopped;
        }
        
        return ECommandAction.Skipped;
    }
    
    #endregion

    #region Commands
    
    private ECommandAction OnClientKillCommand(IGameClient client, StringCommand command)
    {
        var deathrunPlayer = GetDeathrunPlayer(client);
        if (deathrunPlayer is null) return ECommandAction.Stopped;

        if (deathrunPlayer.IsValidAndAlive is not true) return ECommandAction.Stopped;
        
        if (baseConfig.EnableKillCommandForCTs is true
            && deathrunPlayer.Controller?.Team is CStrikeTeam.CT)
        {
            deathrunPlayer.PlayerPawn?.Slay();
        }
        
        if (baseConfig.EnableKillCommandForTs is true
            && deathrunPlayer.Controller?.Team is CStrikeTeam.TE)
        {
            deathrunPlayer.PlayerPawn?.Slay();
        }
        
        return ECommandAction.Stopped;
    }
    
    #endregion
    
    #region DeathrunPlayer Async

    private static async Task SaveLivesToDatabase(ulong steamId64, int newLivesNum)
    {
        try
        {
            await using var connection = new MySqlConnection(LivesSystemManager.ConnectionString);
            await connection.OpenAsync();
            
            var insertUpdateLivesQuery 
                = $@" INSERT INTO `{(LivesSystemManager.LivesSystemConfig?.TableName ?? "deathrun_players")}` 
                      ( steamid64, `lives` )  
                      VALUES 
                      ( @SteamId64, @NewLives ) 
                      ON DUPLICATE KEY UPDATE 
                                       `lives`  = '{newLivesNum}'
                    ";
    
            await connection.ExecuteAsync(insertUpdateLivesQuery,
                new {
                            SteamId64        = steamId64, 
                            NewLives         = newLivesNum
                          });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
    }
    
    private static async Task<int> GetSavedLives(ulong steamId64)
    {
        try
        {
            await using var connection = new MySqlConnection(LivesSystemManager.ConnectionString);
            await connection.OpenAsync();
    
            //fast check if the player has saved lives data
            var hasSavedLivesData = await HasSavedLivesData(steamId64);
            if (hasSavedLivesData is not true) return 0;
            
            //take the lives num from the database
            var livesNum = await connection.QueryFirstOrDefaultAsync<int>
            ($@"SELECT
                       `lives`
                    FROM `{(LivesSystemManager.LivesSystemConfig?.TableName ?? "deathrun_players")}`
                    WHERE steamid64 = @SteamId64
                 ",
                new { SteamId64 = steamId64 }
            
            );
            
            return livesNum;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return 0;
    }

    private static async Task<bool> HasSavedLivesData(ulong steamId64)
    {
        try
        {
            await using var connection = new MySqlConnection(LivesSystemManager.ConnectionString);
            await connection.OpenAsync();
    
            var hasSavedLivesData 
                = await connection.QueryFirstOrDefaultAsync<bool>
                                    ($@"SELECT EXISTS(SELECT 1 FROM `{(LivesSystemManager.LivesSystemConfig?.TableName ?? "deathrun_players")}`
                                            WHERE steamid64 = @SteamId64 LIMIT 1)
                                         ",
                                        new { SteamId64 = steamId64 }
                                    
                                    );
            
            return hasSavedLivesData;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return false;
    }

    #endregion
    
    #region DeathrunPlayer
    
    public IDeathrunPlayer? GetDeathrunPlayer(IGameClient client)
    {
        if (client?.IsValid is not true) return null;
        
        var deathrunPlayer = DeathrunPlayers.GetValueOrDefault(client.SteamId != 0 ? client.SteamId : client.Slot);
        return deathrunPlayer?.IsValid is not true ? null : deathrunPlayer;
    }
    
    public IDeathrunPlayer? GetDeathrunPlayer(ulong steamId64)
    {
        var client = clientManager.GetGameClient(steamId64);
        return client?.IsValid is not true ? null : GetDeathrunPlayer(client);
    }

    public IDeathrunPlayer? GetDeathrunPlayer(PlayerSlot slot)
    {
        var client = clientManager.GetGameClient(slot);
        return client?.IsValid is not true ? null : GetDeathrunPlayer(client);
    }
    
    public IDeathrunPlayer? GetDeathrunPlayer(CEntityHandle<IPlayerPawn> playerPawnHandle)
        => GetAllValidDeathrunPlayers()
            .FilterPlayers(dPlayer => dPlayer.PlayerPawn?.Handle == playerPawnHandle)
            .FirstOrDefault();
    
    #endregion
    
    #region DeathrunPlayers
    
    private void ClearDeathrunPlayers() => DeathrunPlayers.Clear();
    
    public IReadOnlyCollection<IDeathrunPlayer> GetAllValidDeathrunPlayers()
    {
        var deathrunPlayers = new List<IDeathrunPlayer>(DeathrunPlayers.Count);

        foreach (var deathrunPlayer in DeathrunPlayers.Values)
            if (deathrunPlayer.IsValid)
                deathrunPlayers.Add(deathrunPlayer);
        
        return deathrunPlayers;
    }
    
    public int ValidDeathrunPlayersNum => GetAllValidDeathrunPlayers().Count;

    public IReadOnlyCollection<IDeathrunPlayer> GetAllAliveDeathrunPlayers()
    {
        var deathrunPlayers = new List<IDeathrunPlayer>(DeathrunPlayers.Count);

        foreach (var deathrunPlayer in DeathrunPlayers.Values)
            if (deathrunPlayer.IsValidAndAlive)
                deathrunPlayers.Add(deathrunPlayer);
        
        return deathrunPlayers;
    }
   
    public IReadOnlyCollection<IDeathrunPlayer> GetAllDeadDeathrunPlayers()
    {
        var deathrunPlayers = new List<IDeathrunPlayer>(DeathrunPlayers.Count);

        foreach (var deathrunPlayer in DeathrunPlayers.Values)
            if (deathrunPlayer.IsValidAndAlive is not true)
                deathrunPlayers.Add(deathrunPlayer);
        
        return deathrunPlayers;
    }
    
    #endregion
    
    #region DeathrunPlayers(Zero allocations)
    
    public void GetAllValidDeathrunPlayersZAlloc(List<IDeathrunPlayer> buffer)
    {
        buffer.Clear();

        foreach (var deathrunPlayer in DeathrunPlayers.Values)
            if (deathrunPlayer.IsValid) 
                buffer.Add(deathrunPlayer);
    }

    public void GetAllAliveDeathrunPlayersZAlloc(List<IDeathrunPlayer> buffer)
    {
        buffer.Clear();

        foreach (var deathrunPlayer in DeathrunPlayers.Values)
            if (deathrunPlayer.IsValidAndAlive) 
                buffer.Add(deathrunPlayer);
    }
    
    #endregion
    
    int IClientListener.ListenerVersion => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 8;
}




