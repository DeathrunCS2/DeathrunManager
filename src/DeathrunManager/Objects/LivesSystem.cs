using DeathrunManager.Managers;
using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.DeathrunObjects;
using Sharp.Shared.Enums;

namespace DeathrunManager.Objects;

public class LivesSystem(IDeathrunPlayer deathrunPlayer) : ILivesSystem
{
    public IDeathrunPlayer? Owner { get; } = deathrunPlayer;
    
    private int LivesNum { get; set; } = 0;
    public int GetLivesNum => LivesNum;
    
    public void SetLivesNum(int amount) => LivesNum = amount;
    
    public void AddLivesNum(int amount) => LivesNum += amount;
    
    public void RemoveLife() => LivesNum -= LivesNum >= 1 ? 1 : 0;
    
    public void RemoveLives(int amount = 0, bool allLives = false)
    {
        if (amount is 0 && allLives is not true)
        {
            RemoveLife();
            return;
        }

        if (amount >= 1 && allLives is not true)
        {
            if (LivesNum - amount >= 0)
                LivesNum -= amount;
            else
                SetLivesNum(0);
        }

        if (amount is 0 && allLives is true)
        {
            SetLivesNum(0);
        }
    }
    
    public bool Respawn(bool useLife = true)
    {
        //skip if the lives system is disabled
        if (LivesSystemManager.LivesSystemConfig?.EnableLivesSystem is not true)
        {
            Owner?.SendChatMessage("Extra Lives System is {RED}disabled{DEFAULT}!");
            return false;
        }

        //skip if the deathrun round state is not >= DRoundState.PickedGameMaster && < DRoundState.EndPre
        if (GameplayManager.Instance.GetRoundState()
            is >= DRoundState.PickedGameMaster and < DRoundState.EndPre)
        {
            if (Owner?.Controller?.Team is CStrikeTeam.Spectator or CStrikeTeam.UnAssigned or CStrikeTeam.TE)
            {
                Owner?.SendChatMessage("Only {GREEN}CTs {DEFAULT}can redeem extra lives!");
                return false;
            }

            if (Owner?.IsValidAndAlive is true)
            {
                Owner?.SendChatMessage("You are already {GREEN}alive{DEFAULT}!");
                return false;
            }

            if (LivesNum <= 0 && useLife is true)
            {
                //in-game-msg: "You don't have enough lives to respawn!"
                Owner?.SendChatMessage("You don't have enough {GREEN}extra lives {DEFAULT}to respawn!");
                return false;
            }

            DeathrunManager.Bridge.ModSharp.PushTimer(() =>
            {
                if (useLife is true)
                    RemoveLife();
                Owner?.Controller?.Respawn();
                Owner?.SendChatMessage("Successfully {GREEN}respawned{DEFAULT}! You have {GREEN}"
                                              + LivesNum
                                              + " {DEFAULT}extra lives left!");
            },
            0.015625f);
            return true;
        }

        Owner?.SendChatMessage("You can't redeem extra lives right now!");
        return false;
    }
    
    public string? GetLivesCounterHtmlString()
    {
        if (LivesSystemManager.LivesSystemConfig?.ShowLivesCounter is not true) return null;
            
        return $"<font class='fontSize-s stratum-font fontWeight-Bold' color='#A7A7A7'>EXTRA LIVES: </font>"
               + $"<font class='fontSize-sm stratum-font fontWeight-Bold' color='gold'>{Owner?.LivesSystem?.GetLivesNum}</font>";
    }
}