using System;
using DeathrunManager.Config;
using DeathrunManager.Extensions;
using DeathrunManager.Managers;
using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.DeathrunObjects;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.GameObjects;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace DeathrunManager.Objects;

public class DeathrunPlayer : IDeathrunPlayer
{
    public DeathrunPlayer(IGameClient client)
    {
        Client = client;
        
        StartPlayerThink();
        
        //try initializing the lives system if we've enabled it in the config
        if (LivesSystemManager.LivesSystemConfig?.EnableLivesSystem is true)
        {
            if (InitLivesSystem() is not true)
            {
               Console.WriteLine("[DeathrunPlayer][InitLivesSystem] Failed to initialize lives system for player {0}!", Client.SteamId);
            }
        }
        
        //try initializing the economy system if we've enabled it in the config
        if (EconomyManager.EconomySystemConfig?.EnableEconomySystem is true)
        {
            if (InitEconomySystem() is not true)
            {
                Console.WriteLine("[DeathrunPlayer][InitEconomySystem] Failed to initialize economy for player {0}!", Client.SteamId);
            }
        }
    }
    
    #region DeathrunPlayer

    public IGameClient Client { get; }
    public IPlayerController? Controller => Client.GetPlayerController();
    public IPlayerPawn? PlayerPawn => Client.GetPlayerController()?.GetPlayerPawn();
    public IObserverService? ObserverPawnService => Client.GetPlayerController()?.GetObserverPawn()?.GetObserverService();
    public IDeathrunPlayer? ObservedDeathrunPlayer { get; private set; }
    
    public DPlayerClass Class { get; set; } = DPlayerClass.Contestant;
    public bool InitLivesSystem()
    {
        LivesSystem = new LivesSystem(this);
        return LivesSystem is not null;
    }
    public ILivesSystem? LivesSystem { get; private set; }
    public bool InitEconomySystem()
    {
        EconomySystem = new EconomySystem(this);
        return EconomySystem is not null;
    }
    public IEconomySystem? EconomySystem { get; private set; }
    public bool IsValid => Controller?.IsValidEntity is true && PlayerPawn?.IsValidEntity is true && Controller.IsConnected() is true;
    public bool IsValidAndAlive => IsValid is true && PlayerPawn?.IsAlive is true;
    public bool SkipNextGameMasterPickUp { get; set; } = false;
    public bool IsThinking { get; private set; } = true;
    public bool RenderDeathrunHud { get; set; } = true;
    
    #endregion
    
    #region Change Class Method
    
    public void ChangeClass(DPlayerClass newClass, bool force = false)
    {
        if (force is true)
        {
            if (newClass is DPlayerClass.GameMaster)
            {
                //change class of the calling deathrun player to game master
                ChangeClass(DPlayerClass.GameMaster);
                
                //change class of the game master to contestant and respawn to go CT side
                GameplayManager.Instance.GetGameMaster()?.ChangeClass(DPlayerClass.Contestant);
            }
        }
        else
        {
            //skip changing class if we are already a game master
            //or if the game master is already set
            if (this == GameplayManager.Instance.GetGameMaster()
                || GameplayManager.Instance.GetGameMaster() is not null)
            {
                return;
            }
        }
        
        switch (newClass)
        {
            case DPlayerClass.Contestant:
                MakeContestantInternal();
                break;
            case DPlayerClass.GameMaster:
                MakeGameMasterInternal();
                break;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    #endregion
    
    #region Internal Make* Methods
    
    private void MakeGameMasterInternal()
    {
        if (IsValidAndAlive is not true) return;
        
        if (Controller?.Team is not CStrikeTeam.TE)
        {
            Controller?.SwitchTeam(CStrikeTeam.TE);
            DeathrunManager.Bridge.ModSharp.PushTimer(() =>
            {
                Controller?.Respawn();
                RemoveWeapons();
            }, 0.015625f);
        }
        
        Class = DPlayerClass.GameMaster;
        PlayersManager.Instance.InvokeChangedClass(this, Class);

        RemoveWeapons();
    }

    private void MakeContestantInternal()
    {
        if (IsValidAndAlive is not true) return;

        if (Controller?.Team is not CStrikeTeam.CT)
        {
            Controller?.SwitchTeam(CStrikeTeam.CT);
            DeathrunManager.Bridge.ModSharp.PushTimer(() =>
            {
                Controller?.Respawn();
                RemoveWeapons();
            }, 0.015625f);
        }
        
        Class = DPlayerClass.Contestant;
        PlayersManager.Instance.InvokeChangedClass(this, Class);

        RemoveWeapons();
    }
    
    #endregion
    
    #region Weapons

    private void RemoveWeapons()
    {
        for (var i = Enum.GetValues<GearSlot>().Length; i >= 0; i--)
        {
            //skip knife
            if ((GearSlot)i == GearSlot.Knife) continue;
        
            //skip pistol if contestant
            if ((GearSlot)i == GearSlot.Pistol 
                && (Class is DPlayerClass.Contestant && ManagerConfig.BaseConfig.GiveWeaponToCTs)) continue;
        
            //check if we are on the grenade slot
            if ((GearSlot)i == GearSlot.Grenades)
            {
                //iterate the grenade slot 12 times to ensure all grenades are dropped
                for (var j = 12; j >= 0; j--)
                {
                    var grenade = PlayerPawn?.GetWeaponBySlot(GearSlot.Grenades);
                    if (grenade?.IsValidEntity is not true) continue;
        
                    PlayerPawn?.DropWeapon(grenade);
                    grenade.AcceptInput("Kill");
                }
            }
        
            var weapon = PlayerPawn?.GetWeaponBySlot((GearSlot)i);
            if (weapon?.IsValidEntity is not true) continue;
        
            PlayerPawn?.DropWeapon(weapon);
            weapon.AcceptInput("Kill");
        }
        
    }
    
    #endregion
    
    #region DeathrunPlayer Thinkers
    
    private void StartPlayerThink()
    {
        IsThinking = true;
    }
    
    public void StopPlayerThink()
    {
        SetCenterMenuTopRowHtml(null);
        SetCenterMenuMiddleRowHtml(null);
        SetCenterMenuBottomRowHtml(null);
        
        IsThinking = false;
    }
    
    public void PlayerThink()
    {
        if (IsThinking is not true) return;

        //when `this`'s PlayerPawn is alive we print the center menu to self
        if (PlayerPawn?.IsAlive is true)
        {
            if (LivesSystem is not null)
                SetCenterMenuBottomRowHtml(LivesSystem.GetLivesCounterHtmlString());
            
            PrintToCenterHtml
            (
                (   _topRowHtml is not null     ? _topRowHtml     + "<br/>" : "") 
                + (_middleRowHtml       is not null     ? _middleRowHtml  + "<br/>" : "") 
                + _bottomRowHtml        ?? ""
            );
        }
        else 
        {
            if (ObserverPawnService is null) return;
        
            var observedGameClient = DeathrunManager
                                        .Bridge
                                        .EntityManager
                                        .FindEntityByHandle(ObserverPawnService.ObserverTarget)?
                                        .As<IPlayerPawn>()
                                        .GetOriginalController()?
                                        .GetGameClient();
            
            if (observedGameClient?.IsValid is not true) return;
            
            var observedDeathrunPlayer = PlayersManager.Instance.GetDeathrunPlayer(observedGameClient);
            if (observedDeathrunPlayer is null) return;
            
            //cache the observed client if it's different from the current client
            if (observedDeathrunPlayer != ObservedDeathrunPlayer)
                ObservedDeathrunPlayer = observedDeathrunPlayer;
            
            if (observedDeathrunPlayer.LivesSystem is not null)
                SetCenterMenuBottomRowHtml(observedDeathrunPlayer.LivesSystem.GetLivesCounterHtmlString());
            
            PrintToCenterHtml
            (
                $"<font class='fontSize-m stratum-font fontWeight-Bold' color='{(observedDeathrunPlayer?.Class is DPlayerClass.Contestant ? "#ADD8E6" : "#ffb09c")}'>[{observedDeathrunPlayer?.Client.Name}]</font><br/>"
                + ( _topRowHtml       is not null       ? _topRowHtml     + "<br/>" : "") 
                + (_middleRowHtml     is not null       ? _middleRowHtml  + "<br/>" : "") 
                + _bottomRowHtml      ?? ""
            );
        }
    }
    
    #endregion
    
    #region Chat
    
    public void SendChatMessage(string message, bool addPrefix = true)
    {
        if (string.IsNullOrEmpty(message) is true)
        {
            message = "Global message placeholder!";
            return;
        }

        var rawChatMessage = " " + (addPrefix is true ? ManagerConfig.BaseConfig.Prefix : "") + " " + message;

        var args = PlayersManager.Instance.InvokeDeathrunPlayerSendChatMessage(this, rawChatMessage);

        var coloredChatMessage = GameChatExtensions.ProcessColorCodes(args.Message);

        DeathrunManager.Bridge.ModSharp.PrintChannelFilter(HudPrintChannel.Chat, coloredChatMessage, new RecipientFilter(Client.Slot));
    }

    public void SendChatMessage(string message, RecipientFilter recipientFilter, bool addPrefix = true)
    {
        if (string.IsNullOrEmpty(message) is true)
        {
            message = "Global message placeholder!";
            return;
        }

        var rawChatMessage = " " + (addPrefix is true ? ManagerConfig.BaseConfig.Prefix : "") + " " + message;
        
        var args = PlayersManager.Instance.InvokeDeathrunPlayerSendChatMessage(this, rawChatMessage);

        var coloredChatMessage = GameChatExtensions.ProcessColorCodes(args.Message);

        DeathrunManager.Bridge.ModSharp.PrintChannelFilter(HudPrintChannel.Chat, coloredChatMessage, recipientFilter);
    }
    
    #endregion
    
    #region Html Center Menu
    
    public void PrintToCenterHtml(string message)
    {
        //ensure that the message is not null
        if (string.IsNullOrEmpty(message) is true) return;
        
        //ensure the client is valid, not a fake-client/hltv or bot
        if (Client.IsValid is not true 
            || Client.SteamId == 0 
            || Client.IsFakeClient is true 
            || Client.IsHltv is true) return;
        
        if (IsValid is not true) return;
        
        if (RenderDeathrunHud is not true) return;
        
        var e = DeathrunManager.Bridge.EventManager.CreateEvent("show_survival_respawn_status", true);
        if (e is null) return;
        
        e.SetString("loc_token", message);
        e.SetInt("duration", 5);
        e.SetInt("userid", Client.UserId);
        e.FireToClient(Client);
        e.Dispose();
    }
    
    private string? _topRowHtml;
    private string? _middleRowHtml;
    private string? _bottomRowHtml;
    
    public void SetCenterMenuTopRowHtml(string? htmlString) => _topRowHtml = htmlString;

    public void SetCenterMenuMiddleRowHtml(string? htmlString) => _middleRowHtml = htmlString;
    
    //reserved for lives counter
    private void SetCenterMenuBottomRowHtml(string? htmlString) => _bottomRowHtml = htmlString;
    
    #endregion
}
