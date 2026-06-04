using System;

namespace DeathrunManager.Shared.Objects.Tokens;

public sealed record TokenGrant(
    string Token,
    int? RemainingUses = null,
    DateTime? ActiveTillUtc = null,
    bool ReplaceExisting = true,
    string? MetadataJson = null)
{
    public static TokenGrant Permanent(string token, string? metadataJson = null)
        => new(token, null, null, true, metadataJson);

    public static TokenGrant Limited(string token, int remainingUses, string? metadataJson = null)
        => new(token, remainingUses, null, true, metadataJson);

    public static TokenGrant Temporary(string token, DateTime activeTillUtc, string? metadataJson = null)
        => new(token, null, activeTillUtc, true, metadataJson);

    public static TokenGrant LimitedTemporary(string token, int remainingUses, DateTime activeTillUtc, string? metadataJson = null)
        => new(token, remainingUses, activeTillUtc, true, metadataJson);
}
