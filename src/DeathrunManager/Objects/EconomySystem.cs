using DeathrunManager.Managers;
using DeathrunManager.Shared.DeathrunObjects;

namespace DeathrunManager.Objects;

public class EconomySystem(IDeathrunPlayer deathrunPlayer) : IEconomySystem
{
    public IDeathrunPlayer Owner => deathrunPlayer;
    
    public int Credits { get; set; }
    
    public void SetCreditsNum(int amount) => Credits = amount;
    
    public void AddCreditsNum(int amount) => Credits += amount;

    public void DeductCreditsNum(int amount)
    {
        if (Credits - amount <= 0) Credits = 0;
        else Credits -= amount;
    }

    public void ResetCredits() => Credits = 0;
    
    public string? GetCreditsNumHtmlString()
    {
        if (EconomyManager.EconomySystemConfig?.ShowCreditsHud is not true) return null;
        
        return $"<font class='fontSize-m stratum-font fontWeight-Bold' color='#A7A7A7'> | </font>"
               + $"<font class='fontSize-s stratum-font fontWeight-Bold' color='#A7A7A7'>CREDITS: </font>"
               + $"<font class='fontSize-sm stratum-font fontWeight-Bold' color='limegreen'>{Owner.EconomySystem?.Credits}</font>";
    }
    
}