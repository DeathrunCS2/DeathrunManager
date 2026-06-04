using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DeathrunManager.Shared.Objects;

namespace DeathrunManager.Shared.Managers;

/// <summary>
/// Persistent player token service used by modules to grant, query, consume, revoke, and expire
/// string-based entitlements/flags such as VIP access, cosmetics, mission unlocks, one-time rewards,
/// temporary boosts, shop permissions, event passes, and similar in-game state.
///
/// Design rules:
/// - SteamID64 0 is never persisted and always returns safe empty/false results.
/// - Active tokens are the only tokens that satisfy checks.
/// - Temporary tokens become inactive after ActiveTillUtc.
/// - Limited-use tokens become inactive after their remaining uses reach 0.
/// - Revoked/expired/consumed tokens remain in the database for history unless explicitly deleted.
/// </summary>
public interface ITokensManager
{
    Task<PlayerTokenInfo?> GetTokenAsync(IDeathrunPlayer deathrunPlayer, string token, bool includeInactive = false);
    Task<PlayerTokenInfo?> GetTokenAsync(ulong steamId64, string token, bool includeInactive = false);

    Task<IReadOnlyCollection<PlayerTokenInfo>> GetTokensAsync(IDeathrunPlayer deathrunPlayer, TokenQuery? query = null);
    Task<IReadOnlyCollection<PlayerTokenInfo>> GetTokensAsync(ulong steamId64, TokenQuery? query = null);

    Task<TokenGrantResult> GrantTokenAsync(IDeathrunPlayer deathrunPlayer, TokenGrant tokenGrant);
    Task<TokenGrantResult> GrantTokenAsync(ulong steamId64, TokenGrant tokenGrant);

    Task<int> GrantTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<TokenGrant> tokenGrants);
    Task<int> GrantTokensAsync(ulong steamId64, IEnumerable<TokenGrant> tokenGrants);

    Task<bool> RenameTokenAsync(IDeathrunPlayer deathrunPlayer, string oldToken, string newToken);
    Task<bool> RenameTokenAsync(ulong steamId64, string oldToken, string newToken);

    /// <summary>
    /// Soft-removes a token by marking it inactive. The row remains stored for audit/history.
    /// </summary>
    Task<bool> RevokeTokenAsync(IDeathrunPlayer deathrunPlayer, string token, string? reason = null);
    Task<bool> RevokeTokenAsync(ulong steamId64, string token, string? reason = null);

    /// <summary>
    /// Hard-deletes a token row. Prefer RevokeTokenAsync for gameplay state unless you intentionally want no history.
    /// </summary>
    Task<bool> DeleteTokenAsync(IDeathrunPlayer deathrunPlayer, string token);
    Task<bool> DeleteTokenAsync(ulong steamId64, string token);

    Task<bool> SetTokenUsesAsync(IDeathrunPlayer deathrunPlayer, string token, int? remainingUses);
    Task<bool> SetTokenUsesAsync(ulong steamId64, string token, int? remainingUses);

    Task<bool> SetTokenActiveTillAsync(IDeathrunPlayer deathrunPlayer, string token, DateTime? activeTillUtc);
    Task<bool> SetTokenActiveTillAsync(ulong steamId64, string token, DateTime? activeTillUtc);

    Task<TokenConsumeResult> ConsumeTokenAsync(IDeathrunPlayer deathrunPlayer, string token, int uses = 1);
    Task<TokenConsumeResult> ConsumeTokenAsync(ulong steamId64, string token, int uses = 1);

    /// <summary>
    /// Consumes multiple tokens in one operation. When requireAll is true, no token is consumed unless every requested token can be consumed.
    /// </summary>
    Task<TokenConsumeBatchResult> ConsumeTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<TokenSpend> tokens, bool requireAll = true);
    Task<TokenConsumeBatchResult> ConsumeTokensAsync(ulong steamId64, IEnumerable<TokenSpend> tokens, bool requireAll = true);

    Task<bool> HasTokenAsync(IDeathrunPlayer deathrunPlayer, string token, int requiredUses = 1);
    Task<bool> HasTokenAsync(ulong steamId64, string token, int requiredUses = 1);

    Task<bool> MatchesAsync(IDeathrunPlayer deathrunPlayer, TokenRequirement requirement);
    Task<bool> MatchesAsync(ulong steamId64, TokenRequirement requirement);

    Task<int> RefreshTokenStatesAsync(IDeathrunPlayer deathrunPlayer);
    Task<int> RefreshTokenStatesAsync(ulong steamId64);
    Task<int> RefreshExpiredTokensAsync();

    void ClearCachedTokens(ulong steamId64);
    void ClearCache();
}

public enum TokenGrantResult
{
    InvalidRequest,
    SkippedInvalidSteamId,
    Created,
    Replaced,
    Refreshed,
    Failed
}

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

public enum TokenInactiveReason
{
    None,
    Consumed,
    Expired,
    Revoked,
    Replaced
}

public enum TokenMatchMode
{
    All,
    Any
}

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

public sealed record TokenSpend(string Token, int Uses = 1);

public sealed record TokenRequirement(
    IEnumerable<string>? RequiredTokens = null,
    IEnumerable<string>? ExcludedTokens = null,
    TokenMatchMode RequiredMatchMode = TokenMatchMode.All,
    int RequiredUses = 1)
{
    public static TokenRequirement RequireAll(params string[] tokens)
        => new(tokens, null, TokenMatchMode.All);

    public static TokenRequirement RequireAny(params string[] tokens)
        => new(tokens, null, TokenMatchMode.Any);
}

public sealed record TokenQuery(
    bool IncludeInactive = false,
    bool OnlyActive = true,
    bool IncludeExpired = false,
    IEnumerable<string>? Tokens = null)
{
    public static TokenQuery ActiveOnly { get; } = new();
    public static TokenQuery All { get; } = new(IncludeInactive: true, OnlyActive: false, IncludeExpired: true);
}

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

public sealed record TokenConsumeBatchResult(
    bool Success,
    IReadOnlyDictionary<string, TokenConsumeResult> Results)
{
    public static TokenConsumeBatchResult Empty { get; } = new(false, new Dictionary<string, TokenConsumeResult>());
}
