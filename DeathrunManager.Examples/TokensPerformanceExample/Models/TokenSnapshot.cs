using System;
using System.Collections.Generic;
using DeathrunManager.Shared.Objects.Tokens;

namespace TokensPerformanceExample.Models;

public sealed record TokenSnapshot(
    ulong SteamId64,
    IReadOnlyDictionary<string, PlayerTokenInfo> ActiveTokens,
    DateTime LoadedAtUtc,
    DateTime? NextTokenExpiryUtc)
{
    public static TokenSnapshot Empty(ulong steamId64)
        => new(
            steamId64,
            new Dictionary<string, PlayerTokenInfo>(StringComparer.OrdinalIgnoreCase),
            DateTime.UtcNow,
            null);

    public bool IsFresh(DateTime nowUtc, TimeSpan maxAge)
    {
        if (nowUtc - LoadedAtUtc > maxAge) return false;
        if (NextTokenExpiryUtc is not null && NextTokenExpiryUtc.Value <= nowUtc) return false;

        return true;
    }

    public bool TryGetUsableToken(string token, int requiredUses, out PlayerTokenInfo? tokenInfo)
    {
        tokenInfo = null;

        if (string.IsNullOrWhiteSpace(token) || requiredUses <= 0) return false;
        if (ActiveTokens.TryGetValue(token.Trim(), out var playerToken) is not true) return false;
        if (playerToken.CanBeUsed is not true) return false;
        if (playerToken.RemainingUses is not null && playerToken.RemainingUses.Value < requiredUses) return false;

        tokenInfo = playerToken;
        return true;
    }

    public bool HasToken(string token, int requiredUses = 1)
        => TryGetUsableToken(token, requiredUses, out _);
}
