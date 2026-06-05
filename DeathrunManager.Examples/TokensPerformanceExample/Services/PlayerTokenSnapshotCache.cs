using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeathrunManager.Shared.Managers;
using DeathrunManager.Shared.Objects;
using DeathrunManager.Shared.Objects.Tokens;
using Microsoft.Extensions.Logging;
using TokensPerformanceExample.Models;

namespace TokensPerformanceExample.Services;

/// <summary>
/// Module-local read cache for gameplay code that checks tokens frequently.
///
/// Important rule:
/// - Frequent checks use this snapshot only.
/// - Mutations still go through ITokensManager because it owns DB consistency, locks, and atomic consume logic.
/// - After every mutation, this cache refreshes or invalidates the affected player's snapshot.
/// </summary>
public sealed class PlayerTokenSnapshotCache(
    ITokensManager tokensManager,
    ILogger logger,
    TimeSpan maxAge)
{
    private static readonly StringComparer TokenComparer = StringComparer.OrdinalIgnoreCase;

    private readonly ConcurrentDictionary<ulong, TokenSnapshot> _snapshots = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _refreshLocks = new();

    public bool TryGetSnapshot(ulong steamId64, out TokenSnapshot snapshot)
        => _snapshots.TryGetValue(steamId64, out snapshot!);

    public bool TryGetSnapshot(IDeathrunPlayer player, out TokenSnapshot snapshot)
        => TryGetSnapshot(GetSteamId64(player), out snapshot);

    /// <summary>
    /// Zero-DB fast path. Use from ThinkPost / HUD / per-frame code only after players have been prefetched.
    /// This method also locally rejects tokens that expired after the snapshot was loaded, so temp tokens do not stay usable in memory.
    /// </summary>
    public bool HasTokenCached(IDeathrunPlayer player, string token, int requiredUses = 1)
    {
        var steamId64 = GetSteamId64(player);
        if (steamId64 == 0) return false;
        if (_snapshots.TryGetValue(steamId64, out var snapshot) is not true) return false;

        var now = DateTime.UtcNow;
        if (snapshot.NextTokenExpiryUtc is not null && snapshot.NextTokenExpiryUtc.Value <= now) return false;

        return snapshot.TryGetUsableToken(token, requiredUses, out _);
    }

    /// <summary>
    /// Read-mostly path. Uses memory when fresh; otherwise performs one refresh for the whole player's active token set.
    /// Prefer this over calling HasTokenAsync repeatedly for many tokens.
    /// </summary>
    public async ValueTask<bool> HasTokenAsync(IDeathrunPlayer player, string token, int requiredUses = 1)
    {
        var snapshot = await GetFreshSnapshotAsync(player);
        return snapshot.TryGetUsableToken(token, requiredUses, out _);
    }

    public async ValueTask<bool> MatchesAsync(IDeathrunPlayer player, TokenRequirement requirement)
    {
        var steamId64 = GetSteamId64(player);
        if (steamId64 == 0 || requirement.RequiredUses <= 0) return false;

        var snapshot = await GetFreshSnapshotAsync(steamId64);
        return MatchesSnapshot(snapshot, requirement);
    }

    public async ValueTask<TokenSnapshot> GetFreshSnapshotAsync(IDeathrunPlayer player)
        => await GetFreshSnapshotAsync(GetSteamId64(player));

    public async ValueTask<TokenSnapshot> GetFreshSnapshotAsync(ulong steamId64)
    {
        if (steamId64 == 0) return TokenSnapshot.Empty(0);

        var now = DateTime.UtcNow;
        if (_snapshots.TryGetValue(steamId64, out var existing) && existing.IsFresh(now, maxAge))
        {
            return existing;
        }

        return await RefreshAsync(steamId64);
    }

    public async Task<TokenSnapshot> RefreshAsync(IDeathrunPlayer player)
        => await RefreshAsync(GetSteamId64(player));

    /// <summary>
    /// Performs the actual DB-backed refresh. It refreshes token states first so expired/depleted tokens are marked inactive.
    /// Then it loads active tokens only once and stores a module-local immutable snapshot.
    /// </summary>
    public async Task<TokenSnapshot> RefreshAsync(ulong steamId64)
    {
        if (steamId64 == 0) return TokenSnapshot.Empty(0);

        var refreshLock = _refreshLocks.GetOrAdd(steamId64, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync();

        try
        {
            var now = DateTime.UtcNow;
            if (_snapshots.TryGetValue(steamId64, out var existing) && existing.IsFresh(now, maxAge))
            {
                return existing;
            }

            await tokensManager.RefreshTokenStatesAsync(steamId64);

            var activeTokens = await tokensManager.GetTokensAsync(steamId64, TokenQuery.ActiveOnly);
            var dictionary = activeTokens
                .Where(token => token.CanBeUsed)
                .ToDictionary(token => token.Token, token => token, TokenComparer);

            var expiringTokens = dictionary.Values
                .Where(token => token.ActiveTillUtc is not null)
                .Select(token => token.ActiveTillUtc!.Value)
                .ToArray();

            var snapshot = new TokenSnapshot(
                steamId64,
                dictionary,
                DateTime.UtcNow,
                expiringTokens.Length == 0 ? null : expiringTokens.Min());

            _snapshots[steamId64] = snapshot;
            return snapshot;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to refresh token snapshot for {SteamId64}.", steamId64);
            return _snapshots.TryGetValue(steamId64, out var existing)
                ? existing
                : TokenSnapshot.Empty(steamId64);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    /// <summary>
    /// Atomic consume path. Never consume from the local snapshot manually.
    /// ITokensManager performs the DB-guarded update and this cache refreshes afterwards.
    /// </summary>
    public async Task<TokenConsumeResult> ConsumeAndRefreshAsync(IDeathrunPlayer player, string token, int uses = 1)
    {
        var steamId64 = GetSteamId64(player);
        if (steamId64 == 0) return TokenConsumeResult.SkippedInvalidSteamId;

        var result = await tokensManager.ConsumeTokenAsync(steamId64, token, uses);
        await ForceRefreshAfterMutationAsync(steamId64);
        return result;
    }

    public async Task<TokenConsumeBatchResult> ConsumeBatchAndRefreshAsync(IDeathrunPlayer player, IEnumerable<TokenSpend> spends, bool requireAll = true)
    {
        var steamId64 = GetSteamId64(player);
        if (steamId64 == 0) return TokenConsumeBatchResult.Empty;

        var result = await tokensManager.ConsumeTokensAsync(steamId64, spends, requireAll);
        await ForceRefreshAfterMutationAsync(steamId64);
        return result;
    }

    public async Task<TokenGrantResult> GrantAndRefreshAsync(IDeathrunPlayer player, TokenGrant grant)
    {
        var steamId64 = GetSteamId64(player);
        if (steamId64 == 0) return TokenGrantResult.SkippedInvalidSteamId;

        var result = await tokensManager.GrantTokenAsync(steamId64, grant);
        await ForceRefreshAfterMutationAsync(steamId64);
        return result;
    }

    public async Task<bool> RevokeAndRefreshAsync(IDeathrunPlayer player, string token, string? reason = null)
    {
        var steamId64 = GetSteamId64(player);
        if (steamId64 == 0) return false;

        var result = await tokensManager.RevokeTokenAsync(steamId64, token, reason);
        await ForceRefreshAfterMutationAsync(steamId64);
        return result;
    }

    public void Invalidate(IDeathrunPlayer player)
        => Invalidate(GetSteamId64(player));

    public void Invalidate(ulong steamId64)
    {
        if (steamId64 == 0) return;
        _snapshots.TryRemove(steamId64, out _);
    }

    public void RemovePlayer(IDeathrunPlayer player)
    {
        var steamId64 = GetSteamId64(player);
        if (steamId64 == 0) return;

        _snapshots.TryRemove(steamId64, out _);
        if (_refreshLocks.TryRemove(steamId64, out var refreshLock))
        {
            refreshLock.Dispose();
        }
    }

    public void Clear()
    {
        _snapshots.Clear();

        foreach (var refreshLock in _refreshLocks.Values)
        {
            refreshLock.Dispose();
        }

        _refreshLocks.Clear();
    }

    private async Task ForceRefreshAfterMutationAsync(ulong steamId64)
    {
        _snapshots.TryRemove(steamId64, out _);
        await RefreshAsync(steamId64);
    }

    private static bool MatchesSnapshot(TokenSnapshot snapshot, TokenRequirement requirement)
    {
        var requiredTokens = NormalizeTokens(requirement.RequiredTokens);
        var excludedTokens = NormalizeTokens(requirement.ExcludedTokens);

        foreach (var token in excludedTokens)
        {
            if (snapshot.HasToken(token)) return false;
        }

        if (requiredTokens.Count == 0) return true;

        return requirement.RequiredMatchMode is TokenMatchMode.All
            ? requiredTokens.All(token => snapshot.HasToken(token, requirement.RequiredUses))
            : requiredTokens.Any(token => snapshot.HasToken(token, requirement.RequiredUses));
    }

    private static List<string> NormalizeTokens(IEnumerable<string>? tokens)
    {
        if (tokens is null) return [];

        return tokens
            .Where(token => string.IsNullOrWhiteSpace(token) is not true)
            .Select(token => token.Trim().ToLowerInvariant())
            .Distinct(TokenComparer)
            .ToList();
    }

    private static ulong GetSteamId64(IDeathrunPlayer? player)
        => player?.Client is null ? 0 : Convert.ToUInt64(player.Client.SteamId);
}
