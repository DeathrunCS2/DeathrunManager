namespace DeathrunManager.Shared.Objects.Tokens;
 
public enum TokenConsumeResult
{
    InvalidRequest,
    SkippedInvalidSteamId,
    Missing,
    Inactive,
    Expired,
    InsufficientUses,
    Consumed,
    Unlimited
}
