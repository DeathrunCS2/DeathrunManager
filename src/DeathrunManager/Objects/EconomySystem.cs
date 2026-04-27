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
        return $"<font class='fontSize-m stratum-font fontWeight-Bold' color='#A7A7A7'> | </font>"
               + $"<font class='fontSize-m stratum-font fontWeight-Bold' color='limegreen'>Credits: </font>"
               + $"<font class='fontSize-m stratum-font fontWeight-Bold' color='gold'>{Owner.EconomySystem?.Credits}</font>";
    }
    
}