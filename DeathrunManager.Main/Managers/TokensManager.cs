using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DeathrunManager.Interfaces.Managers;
using DeathrunManager.Shared.Managers;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace DeathrunManager.Managers;

internal sealed class TokensManager(
    ILogger<TokensManager> logger,
    IDatabaseManager databaseManager) : IManager, ITokensManager
{
    private static readonly StringComparer TokenComparer = StringComparer.OrdinalIgnoreCase;

    private readonly ConcurrentDictionary<ulong, Dictionary<string, PlayerTokenInfo>> _tokensCache = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _playerLocks = new();

    public static TokensManagerConfig TokensManagerConfig { get; private set; } = null!;

    #region IManager

    public bool Init()
    {
        TokensManagerConfig = LoadTokensManagerConfig();

        if (TokensManagerConfig.EnableTokensManager is not true)
        {
            logger.LogWarning("[TokensManager] {message}", "The Tokens Manager is disabled!");
            return true;
        }

        try
        {
            SetupDatabaseTablesAsync().GetAwaiter().GetResult();
            RefreshExpiredTokensAsync().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to initialize Tokens Manager.");
            return false;
        }
    }

    public void Shutdown()
    {
        ClearCache();

        foreach (var playerLock in _playerLocks.Values)
        {
            playerLock.Dispose();
        }

        _playerLocks.Clear();
    }

    #endregion

    #region Query API

    public Task<PlayerTokenInfo?> GetTokenAsync(IDeathrunPlayer deathrunPlayer, string token, bool includeInactive = false)
        => GetTokenAsync(GetSteamId64(deathrunPlayer), token, includeInactive);

    public async Task<PlayerTokenInfo?> GetTokenAsync(ulong steamId64, string token, bool includeInactive = false)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return null;
        }

        await RefreshTokenStatesAsync(steamId64);
        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            if (tokens.TryGetValue(normalizedToken, out var playerToken) is not true) return null;
            if (includeInactive is not true && playerToken.CanBeUsed is not true) return null;

            return playerToken;
        }
    }

    public Task<IReadOnlyCollection<PlayerTokenInfo>> GetTokensAsync(IDeathrunPlayer deathrunPlayer, TokenQuery? query = null)
        => GetTokensAsync(GetSteamId64(deathrunPlayer), query);

    public async Task<IReadOnlyCollection<PlayerTokenInfo>> GetTokensAsync(ulong steamId64, TokenQuery? query = null)
    {
        if (CanUseSteamId64(steamId64) is not true) return Array.Empty<PlayerTokenInfo>();

        await RefreshTokenStatesAsync(steamId64);
        var normalizedQuery = query ?? TokenQuery.ActiveOnly;
        var requestedTokens = NormalizeTokens(normalizedQuery.Tokens ?? Array.Empty<string>());
        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            return tokens.Values
                .Where(playerToken => MatchesQuery(playerToken, normalizedQuery, requestedTokens))
                .OrderBy(playerToken => playerToken.Token, TokenComparer)
                .ToArray();
        }
    }

    public Task<bool> HasTokenAsync(IDeathrunPlayer deathrunPlayer, string token, int requiredUses = 1)
        => HasTokenAsync(GetSteamId64(deathrunPlayer), token, requiredUses);

    public async Task<bool> HasTokenAsync(ulong steamId64, string token, int requiredUses = 1)
    {
        if (requiredUses <= 0) return false;

        var playerToken = await GetTokenAsync(steamId64, token);
        return playerToken is not null && HasEnoughUses(playerToken, requiredUses);
    }

    public Task<bool> MatchesAsync(IDeathrunPlayer deathrunPlayer, TokenRequirement requirement)
        => MatchesAsync(GetSteamId64(deathrunPlayer), requirement);

    public async Task<bool> MatchesAsync(ulong steamId64, TokenRequirement requirement)
    {
        if (CanUseSteamId64(steamId64) is not true || requirement.RequiredUses <= 0) return false;

        var requiredTokens = NormalizeTokens(requirement.RequiredTokens ?? Array.Empty<string>());
        var excludedTokens = NormalizeTokens(requirement.ExcludedTokens ?? Array.Empty<string>());

        await RefreshTokenStatesAsync(steamId64);
        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            if (excludedTokens.Any(token => tokens.TryGetValue(token, out var playerToken) && playerToken.CanBeUsed))
            {
                return false;
            }

            if (requiredTokens.Count is 0) return true;

            return requirement.RequiredMatchMode is TokenMatchMode.All
                ? requiredTokens.All(token => tokens.TryGetValue(token, out var playerToken) && playerToken.CanBeUsed && HasEnoughUses(playerToken, requirement.RequiredUses))
                : requiredTokens.Any(token => tokens.TryGetValue(token, out var playerToken) && playerToken.CanBeUsed && HasEnoughUses(playerToken, requirement.RequiredUses));
        }
    }

    #endregion

    #region Mutation API

    public Task<TokenGrantResult> GrantTokenAsync(IDeathrunPlayer deathrunPlayer, TokenGrant tokenGrant)
        => GrantTokenAsync(GetSteamId64(deathrunPlayer), tokenGrant);

    public async Task<TokenGrantResult> GrantTokenAsync(ulong steamId64, TokenGrant tokenGrant)
    {
        if (CanUseSteamId64(steamId64) is not true) return TokenGrantResult.SkippedInvalidSteamId;
        if (TryCreateGrant(steamId64, tokenGrant, out var playerToken) is not true) return TokenGrantResult.InvalidRequest;

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            var tokens = await GetOrLoadTokensAsync(steamId64, true);
            var exists = false;
            var existingWasInactive = false;

            lock (tokens)
            {
                if (tokens.TryGetValue(playerToken.Token, out var existingToken))
                {
                    exists = true;
                    existingWasInactive = existingToken.Active is not true;
                }
            }

            if (exists && tokenGrant.ReplaceExisting is not true)
            {
                return TokenGrantResult.InvalidRequest;
            }

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            await dbConnection.ExecuteAsync($@"
                INSERT INTO `{TokensManagerConfig.TableName}`
                    (`steamid64`, `token`, `active`, `remaining_uses`, `active_till`, `inactive_reason`, `metadata_json`)
                VALUES
                    (@SteamId64, @Token, @Active, @RemainingUses, @ActiveTillUtc, @InactiveReason, @MetadataJson)
                ON DUPLICATE KEY UPDATE
                    `active` = VALUES(`active`),
                    `remaining_uses` = VALUES(`remaining_uses`),
                    `active_till` = VALUES(`active_till`),
                    `inactive_reason` = VALUES(`inactive_reason`),
                    `metadata_json` = VALUES(`metadata_json`);", ToDbParams(playerToken));

            ClearCachedTokens(steamId64);

            if (exists is not true) return TokenGrantResult.Created;
            return existingWasInactive ? TokenGrantResult.Refreshed : TokenGrantResult.Replaced;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to grant token {token} to {steamId64}.", playerToken.Token, steamId64);
            ClearCachedTokens(steamId64);
            return TokenGrantResult.Failed;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<int> GrantTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<TokenGrant> tokenGrants)
        => GrantTokensAsync(GetSteamId64(deathrunPlayer), tokenGrants);

    public async Task<int> GrantTokensAsync(ulong steamId64, IEnumerable<TokenGrant> tokenGrants)
    {
        if (CanUseSteamId64(steamId64) is not true) return 0;

        var grants = tokenGrants
            .Where(grant => TryCreateGrant(steamId64, grant, out _))
            .GroupBy(grant => NormalizeToken(grant.Token), TokenComparer)
            .Select(group => group.Last())
            .ToArray();

        if (grants.Length is 0) return 0;

        var granted = 0;
        foreach (var grant in grants)
        {
            var result = await GrantTokenAsync(steamId64, grant);
            if (result is TokenGrantResult.Created or TokenGrantResult.Replaced or TokenGrantResult.Refreshed) granted++;
        }

        return granted;
    }

    public Task<bool> RenameTokenAsync(IDeathrunPlayer deathrunPlayer, string oldToken, string newToken)
        => RenameTokenAsync(GetSteamId64(deathrunPlayer), oldToken, newToken);

    public async Task<bool> RenameTokenAsync(ulong steamId64, string oldToken, string newToken)
    {
        if (CanUseSteamId64(steamId64) is not true
            || TryNormalizeToken(oldToken, out var normalizedOldToken) is not true
            || TryNormalizeToken(newToken, out var normalizedNewToken) is not true)
        {
            return false;
        }

        if (TokenComparer.Equals(normalizedOldToken, normalizedNewToken)) return true;

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            var tokens = await GetOrLoadTokensAsync(steamId64, true);
            PlayerTokenInfo? existingToken;

            lock (tokens)
            {
                tokens.TryGetValue(normalizedOldToken, out existingToken);
            }

            if (existingToken is null) return false;

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();
            await using var transaction = await dbConnection.BeginTransactionAsync();

            await dbConnection.ExecuteAsync($@"
                UPDATE `{TokensManagerConfig.TableName}`
                SET `active` = 0, `inactive_reason` = @InactiveReason
                WHERE `steamid64` = @SteamId64 AND `token` = @OldToken;", new
            {
                SteamId64 = steamId64,
                OldToken = normalizedOldToken,
                InactiveReason = TokenInactiveReason.Replaced.ToString()
            }, transaction);

            await dbConnection.ExecuteAsync($@"
                INSERT INTO `{TokensManagerConfig.TableName}`
                    (`steamid64`, `token`, `active`, `remaining_uses`, `active_till`, `inactive_reason`, `metadata_json`)
                VALUES
                    (@SteamId64, @Token, @Active, @RemainingUses, @ActiveTillUtc, @InactiveReason, @MetadataJson)
                ON DUPLICATE KEY UPDATE
                    `active` = VALUES(`active`),
                    `remaining_uses` = VALUES(`remaining_uses`),
                    `active_till` = VALUES(`active_till`),
                    `inactive_reason` = VALUES(`inactive_reason`),
                    `metadata_json` = VALUES(`metadata_json`);", ToDbParams(existingToken with { Token = normalizedNewToken }), transaction);

            await transaction.CommitAsync();
            ClearCachedTokens(steamId64);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to rename token {oldToken} to {newToken} for {steamId64}.", normalizedOldToken, normalizedNewToken, steamId64);
            ClearCachedTokens(steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<bool> RevokeTokenAsync(IDeathrunPlayer deathrunPlayer, string token, string? reason = null)
        => RevokeTokenAsync(GetSteamId64(deathrunPlayer), token, reason);

    public async Task<bool> RevokeTokenAsync(ulong steamId64, string token, string? reason = null)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return false;
        }

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync($@"
                UPDATE `{TokensManagerConfig.TableName}`
                SET `active` = 0,
                    `inactive_reason` = @InactiveReason
                WHERE `steamid64` = @SteamId64 AND `token` = @Token;", new
            {
                SteamId64 = steamId64,
                Token = normalizedToken,
                InactiveReason = string.IsNullOrWhiteSpace(reason) ? TokenInactiveReason.Revoked.ToString() : reason.Trim()
            });

            ClearCachedTokens(steamId64);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to revoke token {token} for {steamId64}.", normalizedToken, steamId64);
            ClearCachedTokens(steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<bool> DeleteTokenAsync(IDeathrunPlayer deathrunPlayer, string token)
        => DeleteTokenAsync(GetSteamId64(deathrunPlayer), token);

    public async Task<bool> DeleteTokenAsync(ulong steamId64, string token)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return false;
        }

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync($@"
                DELETE FROM `{TokensManagerConfig.TableName}`
                WHERE `steamid64` = @SteamId64 AND `token` = @Token;", new
            {
                SteamId64 = steamId64,
                Token = normalizedToken
            });

            ClearCachedTokens(steamId64);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete token {token} for {steamId64}.", normalizedToken, steamId64);
            ClearCachedTokens(steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<bool> SetTokenUsesAsync(IDeathrunPlayer deathrunPlayer, string token, int? remainingUses)
        => SetTokenUsesAsync(GetSteamId64(deathrunPlayer), token, remainingUses);

    public async Task<bool> SetTokenUsesAsync(ulong steamId64, string token, int? remainingUses)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true || IsValidRemainingUses(remainingUses) is not true)
        {
            return false;
        }

        var inactiveReason = remainingUses <= 0 ? TokenInactiveReason.Consumed.ToString() : null;
        var active = remainingUses is null or > 0;

        return await UpdateTokenFieldsAsync(steamId64, normalizedToken, new
        {
            Active = active,
            RemainingUses = remainingUses,
            InactiveReason = inactiveReason
        }, $@"
            `active` = @Active,
            `remaining_uses` = @RemainingUses,
            `inactive_reason` = @InactiveReason");
    }

    public Task<bool> SetTokenActiveTillAsync(IDeathrunPlayer deathrunPlayer, string token, DateTime? activeTillUtc)
        => SetTokenActiveTillAsync(GetSteamId64(deathrunPlayer), token, activeTillUtc);

    public async Task<bool> SetTokenActiveTillAsync(ulong steamId64, string token, DateTime? activeTillUtc)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true || IsValidActiveTill(activeTillUtc) is not true)
        {
            return false;
        }

        var active = activeTillUtc is null || activeTillUtc.Value > DateTime.UtcNow;
        var inactiveReason = active ? null : TokenInactiveReason.Expired.ToString();

        return await UpdateTokenFieldsAsync(steamId64, normalizedToken, new
        {
            Active = active,
            ActiveTillUtc = activeTillUtc,
            InactiveReason = inactiveReason
        }, $@"
            `active` = @Active,
            `active_till` = @ActiveTillUtc,
            `inactive_reason` = @InactiveReason");
    }

    public Task<TokenConsumeResult> ConsumeTokenAsync(IDeathrunPlayer deathrunPlayer, string token, int uses = 1)
        => ConsumeTokenAsync(GetSteamId64(deathrunPlayer), token, uses);

    public async Task<TokenConsumeResult> ConsumeTokenAsync(ulong steamId64, string token, int uses = 1)
    {
        if (CanUseSteamId64(steamId64) is not true) return TokenConsumeResult.SkippedInvalidSteamId;
        if (uses <= 0 || TryNormalizeToken(token, out var normalizedToken) is not true) return TokenConsumeResult.InvalidRequest;

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            await RefreshTokenStatesAsync(steamId64);
            var tokens = await GetOrLoadTokensAsync(steamId64, true);
            PlayerTokenInfo? playerToken;

            lock (tokens)
            {
                tokens.TryGetValue(normalizedToken, out playerToken);
            }

            if (playerToken is null) return TokenConsumeResult.Missing;
            if (playerToken.ActiveTillUtc is not null && playerToken.ActiveTillUtc.Value <= DateTime.UtcNow) return TokenConsumeResult.Expired;
            if (playerToken.Active is not true) return TokenConsumeResult.Inactive;
            if (playerToken.RemainingUses is null) return TokenConsumeResult.Unlimited;
            if (playerToken.RemainingUses < uses) return TokenConsumeResult.InsufficientUses;

            var newUses = playerToken.RemainingUses.Value - uses;
            var newActive = newUses > 0;
            var inactiveReason = newActive ? null : TokenInactiveReason.Consumed.ToString();

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync($@"
                UPDATE `{TokensManagerConfig.TableName}`
                SET `remaining_uses` = @RemainingUses,
                    `active` = @Active,
                    `inactive_reason` = @InactiveReason
                WHERE `steamid64` = @SteamId64
                  AND `token` = @Token
                  AND `active` = 1
                  AND `remaining_uses` IS NOT NULL
                  AND `remaining_uses` >= @Uses
                  AND (`active_till` IS NULL OR `active_till` > UTC_TIMESTAMP());", new
            {
                SteamId64 = steamId64,
                Token = normalizedToken,
                Uses = uses,
                RemainingUses = newUses,
                Active = newActive,
                InactiveReason = inactiveReason
            });

            if (affectedRows <= 0)
            {
                ClearCachedTokens(steamId64);
                return TokenConsumeResult.Inactive;
            }

            ClearCachedTokens(steamId64);
            return TokenConsumeResult.Consumed;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to consume token {token} for {steamId64}.", normalizedToken, steamId64);
            ClearCachedTokens(steamId64);
            return TokenConsumeResult.InvalidRequest;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<TokenConsumeBatchResult> ConsumeTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<TokenSpend> tokens, bool requireAll = true)
        => ConsumeTokensAsync(GetSteamId64(deathrunPlayer), tokens, requireAll);

    public async Task<TokenConsumeBatchResult> ConsumeTokensAsync(ulong steamId64, IEnumerable<TokenSpend> tokens, bool requireAll = true)
    {
        if (CanUseSteamId64(steamId64) is not true)
        {
            return new TokenConsumeBatchResult(false, new Dictionary<string, TokenConsumeResult>());
        }

        var spends = NormalizeSpends(tokens);
        if (spends.Count is 0) return TokenConsumeBatchResult.Empty;

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            await RefreshTokenStatesAsync(steamId64);
            var cachedTokens = await GetOrLoadTokensAsync(steamId64, true);
            var results = new Dictionary<string, TokenConsumeResult>(TokenComparer);
            var spendableTokens = new List<(PlayerTokenInfo Token, int Uses)>();

            lock (cachedTokens)
            {
                foreach (var spend in spends)
                {
                    if (cachedTokens.TryGetValue(spend.Token, out var playerToken) is not true)
                    {
                        results[spend.Token] = TokenConsumeResult.Missing;
                        continue;
                    }

                    var validation = ValidateSpend(playerToken, spend.Uses);
                    results[spend.Token] = validation;

                    if (validation is TokenConsumeResult.Consumed or TokenConsumeResult.Unlimited)
                    {
                        spendableTokens.Add((playerToken, spend.Uses));
                    }
                }
            }

            if (requireAll && results.Values.Any(result => result is not TokenConsumeResult.Consumed and not TokenConsumeResult.Unlimited))
            {
                return new TokenConsumeBatchResult(false, results);
            }

            if (spendableTokens.Count is 0) return new TokenConsumeBatchResult(false, results);

            var limitedTokens = spendableTokens
                .Where(entry => entry.Token.RemainingUses is not null)
                .ToArray();

            if (limitedTokens.Length is 0)
            {
                return new TokenConsumeBatchResult(true, results);
            }

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();
            await using var transaction = await dbConnection.BeginTransactionAsync();

            foreach (var (playerToken, uses) in limitedTokens)
            {
                var newUses = playerToken.RemainingUses!.Value - uses;
                var newActive = newUses > 0;
                var affectedRows = await dbConnection.ExecuteAsync($@"
                    UPDATE `{TokensManagerConfig.TableName}`
                    SET `remaining_uses` = @RemainingUses,
                        `active` = @Active,
                        `inactive_reason` = @InactiveReason
                    WHERE `steamid64` = @SteamId64
                      AND `token` = @Token
                      AND `active` = 1
                      AND `remaining_uses` IS NOT NULL
                      AND `remaining_uses` >= @Uses
                      AND (`active_till` IS NULL OR `active_till` > UTC_TIMESTAMP());", new
                {
                    SteamId64 = steamId64,
                    playerToken.Token,
                    Uses = uses,
                    RemainingUses = newUses,
                    Active = newActive,
                    InactiveReason = newActive ? null : TokenInactiveReason.Consumed.ToString()
                }, transaction);

                if (affectedRows <= 0)
                {
                    await transaction.RollbackAsync();
                    ClearCachedTokens(steamId64);
                    results[playerToken.Token] = TokenConsumeResult.Inactive;
                    return new TokenConsumeBatchResult(false, results);
                }
            }

            await transaction.CommitAsync();
            ClearCachedTokens(steamId64);
            return new TokenConsumeBatchResult(true, results);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to consume token batch for {steamId64}.", steamId64);
            ClearCachedTokens(steamId64);
            return new TokenConsumeBatchResult(false, spends.ToDictionary(spend => spend.Token, _ => TokenConsumeResult.InvalidRequest, TokenComparer));
        }
        finally
        {
            playerLock.Release();
        }
    }

    #endregion

    #region State refresh

    public Task<int> RefreshTokenStatesAsync(IDeathrunPlayer deathrunPlayer)
        => RefreshTokenStatesAsync(GetSteamId64(deathrunPlayer));

    public async Task<int> RefreshTokenStatesAsync(ulong steamId64)
    {
        if (CanUseSteamId64(steamId64) is not true) return 0;

        await using var dbConnection = CreateDbConnection();
        await dbConnection.OpenAsync();

        var affectedRows = await dbConnection.ExecuteAsync($@"
            UPDATE `{TokensManagerConfig.TableName}`
            SET `active` = 0,
                `inactive_reason` = CASE
                    WHEN `remaining_uses` IS NOT NULL AND `remaining_uses` <= 0 THEN @Consumed
                    WHEN `active_till` IS NOT NULL AND `active_till` <= UTC_TIMESTAMP() THEN @Expired
                    ELSE `inactive_reason`
                END
            WHERE `steamid64` = @SteamId64
              AND `active` = 1
              AND ((`remaining_uses` IS NOT NULL AND `remaining_uses` <= 0)
                   OR (`active_till` IS NOT NULL AND `active_till` <= UTC_TIMESTAMP()));", new
        {
            SteamId64 = steamId64,
            Consumed = TokenInactiveReason.Consumed.ToString(),
            Expired = TokenInactiveReason.Expired.ToString()
        });

        if (affectedRows > 0) ClearCachedTokens(steamId64);
        return affectedRows;
    }

    public async Task<int> RefreshExpiredTokensAsync()
    {
        await using var dbConnection = CreateDbConnection();
        await dbConnection.OpenAsync();

        var affectedRows = await dbConnection.ExecuteAsync($@"
            UPDATE `{TokensManagerConfig.TableName}`
            SET `active` = 0,
                `inactive_reason` = CASE
                    WHEN `remaining_uses` IS NOT NULL AND `remaining_uses` <= 0 THEN @Consumed
                    WHEN `active_till` IS NOT NULL AND `active_till` <= UTC_TIMESTAMP() THEN @Expired
                    ELSE `inactive_reason`
                END
            WHERE `active` = 1
              AND ((`remaining_uses` IS NOT NULL AND `remaining_uses` <= 0)
                   OR (`active_till` IS NOT NULL AND `active_till` <= UTC_TIMESTAMP()));", new
        {
            Consumed = TokenInactiveReason.Consumed.ToString(),
            Expired = TokenInactiveReason.Expired.ToString()
        });

        if (affectedRows > 0) ClearCache();
        return affectedRows;
    }

    #endregion

    #region Cache

    public void ClearCachedTokens(ulong steamId64)
        => _tokensCache.TryRemove(steamId64, out _);

    public void ClearCache()
        => _tokensCache.Clear();

    private Task<Dictionary<string, PlayerTokenInfo>> GetOrLoadTokensAsync(ulong steamId64)
        => GetOrLoadTokensAsync(steamId64, false);

    private async Task<Dictionary<string, PlayerTokenInfo>> GetOrLoadTokensAsync(ulong steamId64, bool playerLockAlreadyHeld)
    {
        if (_tokensCache.TryGetValue(steamId64, out var cachedTokens)) return cachedTokens;

        if (playerLockAlreadyHeld)
        {
            cachedTokens = await LoadTokensFromDatabaseAsync(steamId64);
            _tokensCache[steamId64] = cachedTokens;
            return cachedTokens;
        }

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            if (_tokensCache.TryGetValue(steamId64, out cachedTokens)) return cachedTokens;

            cachedTokens = await LoadTokensFromDatabaseAsync(steamId64);
            _tokensCache[steamId64] = cachedTokens;

            return cachedTokens;
        }
        finally
        {
            playerLock.Release();
        }
    }

    private async Task<Dictionary<string, PlayerTokenInfo>> LoadTokensFromDatabaseAsync(ulong steamId64)
    {
        await using var dbConnection = CreateDbConnection();
        await dbConnection.OpenAsync();

        var rows = await dbConnection.QueryAsync<PlayerTokenRow>($@"
            SELECT `steamid64` AS SteamId64,
                   `token` AS Token,
                   `active` AS Active,
                   `remaining_uses` AS RemainingUses,
                   `active_till` AS ActiveTillUtc,
                   `inactive_reason` AS InactiveReason,
                   `metadata_json` AS MetadataJson,
                   `created_at` AS CreatedAtUtc,
                   `updated_at` AS UpdatedAtUtc
            FROM `{TokensManagerConfig.TableName}`
            WHERE `steamid64` = @SteamId64;", new { SteamId64 = steamId64 });

        return rows
            .Select(ToTokenInfo)
            .ToDictionary(token => token.Token, token => token, TokenComparer);
    }

    #endregion

    #region Tables / migrations

    private async Task SetupDatabaseTablesAsync()
    {
        await using var dbConnection = CreateDbConnection();
        await dbConnection.OpenAsync();

        await dbConnection.ExecuteAsync($@"
            CREATE TABLE IF NOT EXISTS `{TokensManagerConfig.TableName}`
            (
                `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                `steamid64` BIGINT UNSIGNED NOT NULL,
                `token` VARCHAR(128) NOT NULL,
                `active` TINYINT(1) NOT NULL DEFAULT 1,
                `remaining_uses` INT NULL DEFAULT NULL,
                `active_till` DATETIME NULL DEFAULT NULL,
                `inactive_reason` VARCHAR(64) NULL DEFAULT NULL,
                `metadata_json` LONGTEXT NULL DEFAULT NULL,
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_{TokensManagerConfig.TableName}_steamid64_token` (`steamid64`, `token`),
                KEY `idx_{TokensManagerConfig.TableName}_steamid64` (`steamid64`),
                KEY `idx_{TokensManagerConfig.TableName}_token` (`token`),
                KEY `idx_{TokensManagerConfig.TableName}_active` (`active`),
                KEY `idx_{TokensManagerConfig.TableName}_active_till` (`active_till`)
            );");

        await EnsureColumnAsync(dbConnection, "active", "ADD COLUMN `active` TINYINT(1) NOT NULL DEFAULT 1 AFTER `token`");
        await EnsureColumnAsync(dbConnection, "remaining_uses", "ADD COLUMN `remaining_uses` INT NULL DEFAULT NULL AFTER `active`");
        await EnsureColumnAsync(dbConnection, "active_till", "ADD COLUMN `active_till` DATETIME NULL DEFAULT NULL AFTER `remaining_uses`");
        await EnsureColumnAsync(dbConnection, "inactive_reason", "ADD COLUMN `inactive_reason` VARCHAR(64) NULL DEFAULT NULL AFTER `active_till`");
        await EnsureColumnAsync(dbConnection, "metadata_json", "ADD COLUMN `metadata_json` LONGTEXT NULL DEFAULT NULL AFTER `inactive_reason`");

        await BackfillFromLegacyColumnsAsync(dbConnection);
    }

    private static async Task EnsureColumnAsync(MySqlConnection dbConnection, string columnName, string alterSql)
    {
        var columnExists = await dbConnection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @TableName
              AND COLUMN_NAME = @ColumnName;", new
        {
            TokensManagerConfig.TableName,
            ColumnName = columnName
        });

        if (columnExists > 0) return;

        await dbConnection.ExecuteAsync($@"
            ALTER TABLE `{TokensManagerConfig.TableName}`
            {alterSql};");
    }

    private static async Task BackfillFromLegacyColumnsAsync(MySqlConnection dbConnection)
    {
        var hasUsesLeft = await ColumnExistsAsync(dbConnection, "uses_left");
        var hasIsUsed = await ColumnExistsAsync(dbConnection, "is_used");

        if (hasUsesLeft)
        {
            await dbConnection.ExecuteAsync($@"
                UPDATE `{TokensManagerConfig.TableName}`
                SET `remaining_uses` = `uses_left`
                WHERE `remaining_uses` IS NULL AND `uses_left` IS NOT NULL;");
        }

        if (hasIsUsed)
        {
            await dbConnection.ExecuteAsync($@"
                UPDATE `{TokensManagerConfig.TableName}`
                SET `active` = CASE WHEN `is_used` = 1 THEN 0 ELSE `active` END,
                    `inactive_reason` = CASE WHEN `is_used` = 1 THEN @Consumed ELSE `inactive_reason` END
                WHERE `is_used` = 1;", new { Consumed = TokenInactiveReason.Consumed.ToString() });
        }

        await dbConnection.ExecuteAsync($@"
            UPDATE `{TokensManagerConfig.TableName}`
            SET `active` = 0,
                `inactive_reason` = @Consumed
            WHERE `remaining_uses` IS NOT NULL AND `remaining_uses` <= 0;", new { Consumed = TokenInactiveReason.Consumed.ToString() });
    }

    private static async Task<bool> ColumnExistsAsync(MySqlConnection dbConnection, string columnName)
    {
        var columnExists = await dbConnection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @TableName
              AND COLUMN_NAME = @ColumnName;", new
        {
            TokensManagerConfig.TableName,
            ColumnName = columnName
        });

        return columnExists > 0;
    }

    #endregion

    #region Helpers

    private async Task<bool> UpdateTokenFieldsAsync(ulong steamId64, string token, object parameters, string setClause)
    {
        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            var dynamicParameters = new DynamicParameters(parameters);
            dynamicParameters.Add("SteamId64", steamId64);
            dynamicParameters.Add("Token", token);

            var affectedRows = await dbConnection.ExecuteAsync($@"
                UPDATE `{TokensManagerConfig.TableName}`
                SET {setClause}
                WHERE `steamid64` = @SteamId64 AND `token` = @Token;", dynamicParameters);

            ClearCachedTokens(steamId64);
            return affectedRows > 0;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update token {token} for {steamId64}.", token, steamId64);
            ClearCachedTokens(steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    private static bool TryCreateGrant(ulong steamId64, TokenGrant tokenGrant, out PlayerTokenInfo playerToken)
    {
        playerToken = null!;

        if (TryNormalizeToken(tokenGrant.Token, out var normalizedToken) is not true
            || IsValidRemainingUses(tokenGrant.RemainingUses) is not true
            || IsValidActiveTill(tokenGrant.ActiveTillUtc) is not true)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var active = tokenGrant.ActiveTillUtc is null || tokenGrant.ActiveTillUtc.Value > now;

        playerToken = new PlayerTokenInfo(
            steamId64,
            normalizedToken,
            active,
            tokenGrant.RemainingUses,
            tokenGrant.ActiveTillUtc,
            active ? TokenInactiveReason.None : TokenInactiveReason.Expired,
            tokenGrant.MetadataJson,
            now,
            now);

        return true;
    }

    private static bool MatchesQuery(PlayerTokenInfo playerToken, TokenQuery query, IReadOnlyCollection<string> requestedTokens)
    {
        if (requestedTokens.Count > 0 && requestedTokens.Contains(playerToken.Token, TokenComparer) is not true) return false;
        if (query.IncludeExpired is not true && playerToken.IsExpired) return false;
        if (query.OnlyActive && playerToken.CanBeUsed is not true) return false;
        if (query.IncludeInactive is not true && playerToken.Active is not true) return false;

        return true;
    }

    private static bool HasEnoughUses(PlayerTokenInfo playerToken, int requiredUses)
        => playerToken.CanBeUsed && requiredUses > 0 && (playerToken.RemainingUses is null || playerToken.RemainingUses >= requiredUses);

    private static TokenConsumeResult ValidateSpend(PlayerTokenInfo playerToken, int uses)
    {
        if (uses <= 0) return TokenConsumeResult.InvalidRequest;
        if (playerToken.ActiveTillUtc is not null && playerToken.ActiveTillUtc.Value <= DateTime.UtcNow) return TokenConsumeResult.Expired;
        if (playerToken.Active is not true) return TokenConsumeResult.Inactive;
        if (playerToken.RemainingUses is null) return TokenConsumeResult.Unlimited;
        if (playerToken.RemainingUses < uses) return TokenConsumeResult.InsufficientUses;

        return TokenConsumeResult.Consumed;
    }

    private static IReadOnlyCollection<TokenSpend> NormalizeSpends(IEnumerable<TokenSpend> spends)
    {
        return spends
            .Where(spend => spend.Uses > 0 && TryNormalizeToken(spend.Token, out _))
            .GroupBy(spend => NormalizeToken(spend.Token), TokenComparer)
            .Select(group => new TokenSpend(group.Key, group.Sum(spend => spend.Uses)))
            .ToArray();
    }

    private static object ToDbParams(PlayerTokenInfo playerToken)
    {
        return new
        {
            playerToken.SteamId64,
            playerToken.Token,
            playerToken.Active,
            playerToken.RemainingUses,
            playerToken.ActiveTillUtc,
            InactiveReason = playerToken.InactiveReason is TokenInactiveReason.None ? null : playerToken.InactiveReason.ToString(),
            playerToken.MetadataJson
        };
    }

    private static PlayerTokenInfo ToTokenInfo(PlayerTokenRow row)
    {
        return new PlayerTokenInfo(
            row.SteamId64,
            row.Token,
            row.Active,
            row.RemainingUses,
            row.ActiveTillUtc,
            ParseInactiveReason(row.InactiveReason),
            row.MetadataJson,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);
    }

    private MySqlConnection CreateDbConnection()
    {
        if (string.IsNullOrWhiteSpace(databaseManager.ConnectionString) is true)
        {
            throw new InvalidOperationException("Database connection string is missing. Configure sharp/configs/Deathrun.Manager/database.json first.");
        }

        return new MySqlConnection(databaseManager.ConnectionString);
    }

    private SemaphoreSlim GetPlayerLock(ulong steamId64)
        => _playerLocks.GetOrAdd(steamId64, _ => new SemaphoreSlim(1, 1));

    private static ulong GetSteamId64(IDeathrunPlayer? deathrunPlayer)
        => deathrunPlayer?.Client is null ? 0 : Convert.ToUInt64(deathrunPlayer.Client.SteamId);

    private static bool CanUseSteamId64(ulong steamId64)
        => steamId64 != 0;

    private static bool TryNormalizeToken(string? token, out string normalizedToken)
    {
        normalizedToken = string.Empty;

        if (string.IsNullOrWhiteSpace(token) is true) return false;

        normalizedToken = NormalizeToken(token);
        return normalizedToken.Length is > 0 and <= 128;
    }

    private static string NormalizeToken(string token)
        => token.Trim().ToLowerInvariant();

    private static IReadOnlyCollection<string> NormalizeTokens(IEnumerable<string> tokens)
    {
        return tokens
            .Where(token => TryNormalizeToken(token, out _))
            .Select(NormalizeToken)
            .Distinct(TokenComparer)
            .ToArray();
    }

    private static bool IsValidRemainingUses(int? remainingUses)
        => remainingUses is null or >= 0;

    private static bool IsValidActiveTill(DateTime? activeTillUtc)
        => activeTillUtc is null || activeTillUtc.Value.Kind is DateTimeKind.Utc;

    private static TokenInactiveReason ParseInactiveReason(string? inactiveReason)
    {
        return Enum.TryParse<TokenInactiveReason>(inactiveReason, true, out var parsedReason)
            ? parsedReason
            : TokenInactiveReason.None;
    }

    #endregion

    #region Config

    private static TokensManagerConfig LoadTokensManagerConfig()
    {
        if (!Directory.Exists(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager"))
        {
            Directory.CreateDirectory(DeathrunManager.Bridge.ConfigPath + "/Deathrun.Manager");
        }

        var configPath = Path.Combine(DeathrunManager.Bridge.ConfigPath, "Deathrun.Manager/tokens_manager.json");
        if (!File.Exists(configPath)) return CreateTokensManagerConfig(configPath);

        var config = JsonSerializer.Deserialize<TokensManagerConfig>(File.ReadAllText(configPath))!;

        return config;
    }

    private static TokensManagerConfig CreateTokensManagerConfig(string configPath)
    {
        var config = new TokensManagerConfig();

        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        return config;
    }

    public static void ReloadConfig() => TokensManagerConfig = LoadTokensManagerConfig();

    #endregion

    private sealed class PlayerTokenRow
    {
        public ulong SteamId64 { get; init; }
        public string Token { get; init; } = string.Empty;
        public bool Active { get; init; }
        public int? RemainingUses { get; init; }
        public DateTime? ActiveTillUtc { get; init; }
        public string? InactiveReason { get; init; }
        public string? MetadataJson { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}

public sealed class TokensManagerConfig
{
    public bool EnableTokensManager { get; init; } = true;
    public string TableName { get; init; } = "deathrun_player_tokens";
    public string Spacer { get; init; } = "If EnableTokensManager is true, configure the database.json details too.";
}
