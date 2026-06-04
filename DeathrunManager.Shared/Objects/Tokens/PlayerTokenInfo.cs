using System;

namespace DeathrunManager.Shared.Objects.Tokens;

public sealed record PlayerTokenInfo(
    ulong SteamId64,
    string Token,
    bool Active,
    int? RemainingUses,
    DateTime? ActiveTillUtc,
    TokenInactiveReason InactiveReason,
    string? MetadataJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public bool IsUnlimited => RemainingUses is null;

    public bool IsLimitedUse => RemainingUses is not null;

    public bool IsTemporary => ActiveTillUtc is not null;

    public bool IsExpired => ActiveTillUtc is not null && ActiveTillUtc.Value <= DateTime.UtcNow;

    public bool CanBeUsed => Active && !IsExpired && (RemainingUses is null or > 0);
}
