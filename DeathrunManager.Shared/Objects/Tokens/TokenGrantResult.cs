namespace DeathrunManager.Shared.Objects.Tokens;

public enum TokenGrantResult
{
    InvalidRequest,
    SkippedInvalidSteamId,
    Created,
    Replaced,
    Refreshed,
    Failed
}
