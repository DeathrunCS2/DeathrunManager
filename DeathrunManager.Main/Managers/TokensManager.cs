using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    private readonly ConcurrentDictionary<ulong, Dictionary<string, PlayerToken>> _tokensCache = new();
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

        SetupDatabaseTables();

        return true;
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

    #region Tokens API

    public Task<IReadOnlyCollection<string>> GetTokensAsync(IDeathrunPlayer deathrunPlayer)
        => GetTokensAsync(GetSteamId64(deathrunPlayer));

    public async Task<IReadOnlyCollection<string>> GetTokensAsync(ulong steamId64)
    {
        if (CanUseSteamId64(steamId64) is not true) return Array.Empty<string>();

        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            return tokens.Values
                .Where(playerToken => playerToken.IsUsed is not true)
                .Select(playerToken => playerToken.Token)
                .ToArray();
        }
    }

    public Task<bool> AddTokenAsync(IDeathrunPlayer deathrunPlayer, string token)
        => AddTokenAsync(GetSteamId64(deathrunPlayer), token);

    public Task<bool> AddTokenAsync(ulong steamId64, string token)
        => AddTokenInternalAsync(steamId64, token, null);

    public Task<bool> AddTokenAsync(IDeathrunPlayer deathrunPlayer, string token, int usesLeft)
        => AddTokenAsync(GetSteamId64(deathrunPlayer), token, usesLeft);

    public Task<bool> AddTokenAsync(ulong steamId64, string token, int usesLeft)
        => AddTokenInternalAsync(steamId64, token, usesLeft);

    public Task<int> AddTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens)
        => AddTokensAsync(GetSteamId64(deathrunPlayer), tokens);

    public Task<int> AddTokensAsync(ulong steamId64, IEnumerable<string> tokens)
        => AddTokensInternalAsync(steamId64, tokens, null);

    public Task<int> AddTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens, int usesLeftPerToken)
        => AddTokensAsync(GetSteamId64(deathrunPlayer), tokens, usesLeftPerToken);

    public Task<int> AddTokensAsync(ulong steamId64, IEnumerable<string> tokens, int usesLeftPerToken)
        => AddTokensInternalAsync(steamId64, tokens, usesLeftPerToken);

    public Task<bool> RemoveTokenAsync(IDeathrunPlayer deathrunPlayer, string token)
        => RemoveTokenAsync(GetSteamId64(deathrunPlayer), token);

    public async Task<bool> RemoveTokenAsync(ulong steamId64, string token)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return false;
        }

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            var tokens = await GetOrLoadTokensAsync(steamId64, true);

            lock (tokens)
            {
                if (tokens.ContainsKey(normalizedToken) is not true) return false;
            }

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync($@"
                DELETE FROM `{TokensManagerConfig.TableName}`
                WHERE `steamid64` = @SteamId64 AND `token` = @Token;", new { SteamId64 = steamId64, Token = normalizedToken });

            if (affectedRows <= 0) return false;

            lock (tokens)
            {
                tokens.Remove(normalizedToken);
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove token {token} from {steamId64}.", normalizedToken, steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<int> RemoveTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens)
        => RemoveTokensAsync(GetSteamId64(deathrunPlayer), tokens);

    public async Task<int> RemoveTokensAsync(ulong steamId64, IEnumerable<string> tokens)
    {
        if (CanUseSteamId64(steamId64) is not true) return 0;

        var normalizedTokens = NormalizeTokens(tokens);
        if (normalizedTokens.Count is 0) return 0;

        var removed = 0;
        foreach (var token in normalizedTokens)
        {
            if (await RemoveTokenAsync(steamId64, token) is true) removed++;
        }

        return removed;
    }

    public Task<bool> UpdateTokenAsync(IDeathrunPlayer deathrunPlayer, string oldToken, string newToken)
        => UpdateTokenAsync(GetSteamId64(deathrunPlayer), oldToken, newToken);

    public async Task<bool> UpdateTokenAsync(ulong steamId64, string oldToken, string newToken)
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
            PlayerToken oldPlayerToken;

            lock (tokens)
            {
                if (tokens.TryGetValue(normalizedOldToken, out oldPlayerToken) is not true) return false;
            }

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();
            await using var transaction = await dbConnection.BeginTransactionAsync();

            await dbConnection.ExecuteAsync($@"
                DELETE FROM `{TokensManagerConfig.TableName}`
                WHERE `steamid64` = @SteamId64 AND `token` = @OldToken;", new { SteamId64 = steamId64, OldToken = normalizedOldToken }, transaction);

            await dbConnection.ExecuteAsync($@"
                INSERT INTO `{TokensManagerConfig.TableName}` (`steamid64`, `token`, `uses_left`, `is_used`)
                VALUES (@SteamId64, @NewToken, @UsesLeft, @IsUsed)
                ON DUPLICATE KEY UPDATE `uses_left` = VALUES(`uses_left`), `is_used` = VALUES(`is_used`);", new
            {
                SteamId64 = steamId64,
                NewToken = normalizedNewToken,
                UsesLeft = oldPlayerToken.UsesLeft,
                IsUsed = oldPlayerToken.IsUsed
            }, transaction);

            await transaction.CommitAsync();

            lock (tokens)
            {
                tokens.Remove(normalizedOldToken);
                tokens[normalizedNewToken] = oldPlayerToken with { Token = normalizedNewToken };
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update token {oldToken} to {newToken} for {steamId64}.", normalizedOldToken, normalizedNewToken, steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<bool> SetTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens)
        => SetTokensAsync(GetSteamId64(deathrunPlayer), tokens);

    public async Task<bool> SetTokensAsync(ulong steamId64, IEnumerable<string> tokens)
    {
        if (CanUseSteamId64(steamId64) is not true) return false;

        var normalizedTokens = NormalizeTokens(tokens);
        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();
            await using var transaction = await dbConnection.BeginTransactionAsync();

            await dbConnection.ExecuteAsync($@"
                DELETE FROM `{TokensManagerConfig.TableName}`
                WHERE `steamid64` = @SteamId64;", new { SteamId64 = steamId64 }, transaction);

            foreach (var token in normalizedTokens)
            {
                await dbConnection.ExecuteAsync($@"
                    INSERT IGNORE INTO `{TokensManagerConfig.TableName}` (`steamid64`, `token`, `uses_left`, `is_used`)
                    VALUES (@SteamId64, @Token, NULL, 0);", new { SteamId64 = steamId64, Token = token }, transaction);
            }

            await transaction.CommitAsync();

            _tokensCache[steamId64] = normalizedTokens
                .ToDictionary(token => token, token => new PlayerToken(token, null, false), TokenComparer);

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to set tokens for {steamId64}.", steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<bool> SetTokenUnlimitedUsesAsync(IDeathrunPlayer deathrunPlayer, string token)
        => SetTokenUnlimitedUsesAsync(GetSteamId64(deathrunPlayer), token);

    public Task<bool> SetTokenUnlimitedUsesAsync(ulong steamId64, string token)
        => SetTokenUsesInternalAsync(steamId64, token, null);

    public Task<bool> SetTokenUsesAsync(IDeathrunPlayer deathrunPlayer, string token, int usesLeft)
        => SetTokenUsesAsync(GetSteamId64(deathrunPlayer), token, usesLeft);

    public Task<bool> SetTokenUsesAsync(ulong steamId64, string token, int usesLeft)
        => usesLeft <= 0 ? Task.FromResult(false) : SetTokenUsesInternalAsync(steamId64, token, usesLeft);

    public Task<int> GetTokenUsesLeftAsync(IDeathrunPlayer deathrunPlayer, string token)
        => GetTokenUsesLeftAsync(GetSteamId64(deathrunPlayer), token);

    public async Task<int> GetTokenUsesLeftAsync(ulong steamId64, string token)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return 0;
        }

        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            if (tokens.TryGetValue(normalizedToken, out var playerToken) is not true) return 0;

            return playerToken.UsesLeft ?? -1;
        }
    }

    public Task<IReadOnlyCollection<string>> GetUsedTokensAsync(IDeathrunPlayer deathrunPlayer)
        => GetUsedTokensAsync(GetSteamId64(deathrunPlayer));

    public async Task<IReadOnlyCollection<string>> GetUsedTokensAsync(ulong steamId64)
    {
        if (CanUseSteamId64(steamId64) is not true) return Array.Empty<string>();

        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            return tokens.Values
                .Where(playerToken => playerToken.IsUsed)
                .Select(playerToken => playerToken.Token)
                .ToArray();
        }
    }

    public Task<bool> IsTokenUsedAsync(IDeathrunPlayer deathrunPlayer, string token)
        => IsTokenUsedAsync(GetSteamId64(deathrunPlayer), token);

    public async Task<bool> IsTokenUsedAsync(ulong steamId64, string token)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return false;
        }

        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            return tokens.TryGetValue(normalizedToken, out var playerToken) && playerToken.IsUsed;
        }
    }

    public Task<bool> TryUseTokenAsync(IDeathrunPlayer deathrunPlayer, string token)
        => TryUseTokenAsync(GetSteamId64(deathrunPlayer), token);

    public Task<bool> TryUseTokenAsync(ulong steamId64, string token)
        => TryUseTokenAsync(steamId64, token, 1);

    public Task<bool> TryUseTokenAsync(IDeathrunPlayer deathrunPlayer, string token, int usesToSpend)
        => TryUseTokenAsync(GetSteamId64(deathrunPlayer), token, usesToSpend);

    public async Task<bool> TryUseTokenAsync(ulong steamId64, string token, int usesToSpend)
    {
        if (CanUseSteamId64(steamId64) is not true
            || usesToSpend <= 0
            || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return false;
        }

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            var tokens = await GetOrLoadTokensAsync(steamId64, true);
            PlayerToken playerToken;

            lock (tokens)
            {
                if (tokens.TryGetValue(normalizedToken, out playerToken) is not true) return false;
                if (CanSpendUses(playerToken, usesToSpend) is not true) return false;
            }

            if (playerToken.IsUnlimited) return true;

            var newUsesLeft = playerToken.UsesLeft!.Value - usesToSpend;

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            if (newUsesLeft <= 0)
            {
                var affectedRows = await dbConnection.ExecuteAsync($@"
                    UPDATE `{TokensManagerConfig.TableName}`
                    SET `uses_left` = 0, `is_used` = 1
                    WHERE `steamid64` = @SteamId64
                      AND `token` = @Token
                      AND `is_used` = 0
                      AND `uses_left` IS NOT NULL
                      AND `uses_left` >= @UsesToSpend;", new
                {
                    SteamId64 = steamId64,
                    Token = normalizedToken,
                    UsesToSpend = usesToSpend
                });

                if (affectedRows <= 0)
                {
                    ClearCachedTokens(steamId64);
                    return false;
                }

                lock (tokens)
                {
                    tokens[normalizedToken] = playerToken with { UsesLeft = 0, IsUsed = true };
                }
            }
            else
            {
                var affectedRows = await dbConnection.ExecuteAsync($@"
                    UPDATE `{TokensManagerConfig.TableName}`
                    SET `uses_left` = `uses_left` - @UsesToSpend
                    WHERE `steamid64` = @SteamId64
                      AND `token` = @Token
                      AND `is_used` = 0
                      AND `uses_left` IS NOT NULL
                      AND `uses_left` >= @UsesToSpend;", new
                {
                    SteamId64 = steamId64,
                    Token = normalizedToken,
                    UsesToSpend = usesToSpend
                });

                if (affectedRows <= 0)
                {
                    ClearCachedTokens(steamId64);
                    return false;
                }

                lock (tokens)
                {
                    tokens[normalizedToken] = playerToken with { UsesLeft = newUsesLeft, IsUsed = false };
                }
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to use token {token} for {steamId64}.", normalizedToken, steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<bool> TryUseTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens, bool requireAllTokens = true)
        => TryUseTokensAsync(GetSteamId64(deathrunPlayer), tokens, requireAllTokens);

    public async Task<bool> TryUseTokensAsync(ulong steamId64, IEnumerable<string> tokens, bool requireAllTokens = true)
    {
        if (CanUseSteamId64(steamId64) is not true) return false;

        var normalizedTokens = NormalizeTokens(tokens);
        if (normalizedTokens.Count is 0) return false;

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            var cachedTokens = await GetOrLoadTokensAsync(steamId64, true);
            List<PlayerToken> usableTokens;

            lock (cachedTokens)
            {
                usableTokens = normalizedTokens
                    .Where(token => cachedTokens.TryGetValue(token, out var playerToken) && CanSpendUses(playerToken, 1))
                    .Select(token => cachedTokens[token])
                    .ToList();

                if (requireAllTokens is true && usableTokens.Count != normalizedTokens.Count)
                {
                    return false;
                }

                if (requireAllTokens is not true && usableTokens.Count is 0)
                {
                    return false;
                }
            }

            var limitedTokensToSpend = usableTokens
                .Where(playerToken => playerToken.IsUnlimited is not true)
                .ToArray();

            if (limitedTokensToSpend.Length is 0) return true;

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();
            await using var transaction = await dbConnection.BeginTransactionAsync();

            foreach (var playerToken in limitedTokensToSpend)
            {
                var affectedRows = await dbConnection.ExecuteAsync($@"
                    UPDATE `{TokensManagerConfig.TableName}`
                    SET `uses_left` = GREATEST(`uses_left` - 1, 0),
                        `is_used` = CASE WHEN `uses_left` - 1 <= 0 THEN 1 ELSE 0 END
                    WHERE `steamid64` = @SteamId64
                      AND `token` = @Token
                      AND `is_used` = 0
                      AND `uses_left` IS NOT NULL
                      AND `uses_left` >= 1;", new
                {
                    SteamId64 = steamId64,
                    playerToken.Token
                }, transaction);

                if (affectedRows <= 0)
                {
                    await transaction.RollbackAsync();
                    ClearCachedTokens(steamId64);
                    return false;
                }
            }


            await transaction.CommitAsync();

            lock (cachedTokens)
            {
                foreach (var playerToken in limitedTokensToSpend)
                {
                    var newUsesLeft = playerToken.UsesLeft!.Value - 1;

                    cachedTokens[playerToken.Token] = newUsesLeft <= 0
                        ? playerToken with { UsesLeft = 0, IsUsed = true }
                        : playerToken with { UsesLeft = newUsesLeft, IsUsed = false };
                }
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to use tokens for {steamId64}.", steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    public Task<bool> HasTokenAsync(IDeathrunPlayer deathrunPlayer, string token)
        => HasTokenAsync(GetSteamId64(deathrunPlayer), token);

    public async Task<bool> HasTokenAsync(ulong steamId64, string token)
    {
        if (CanUseSteamId64(steamId64) is not true || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return false;
        }

        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            return tokens.TryGetValue(normalizedToken, out var playerToken) && playerToken.IsUsed is not true;
        }
    }

    public Task<bool> HasTokenUsesAsync(IDeathrunPlayer deathrunPlayer, string token, int requiredUses)
        => HasTokenUsesAsync(GetSteamId64(deathrunPlayer), token, requiredUses);

    public async Task<bool> HasTokenUsesAsync(ulong steamId64, string token, int requiredUses)
    {
        if (CanUseSteamId64(steamId64) is not true
            || requiredUses <= 0
            || TryNormalizeToken(token, out var normalizedToken) is not true)
        {
            return false;
        }

        var tokens = await GetOrLoadTokensAsync(steamId64);

        lock (tokens)
        {
            return tokens.TryGetValue(normalizedToken, out var playerToken)
                   && CanSpendUses(playerToken, requiredUses);
        }
    }

    public Task<bool> HasAllTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens)
        => HasAllTokensAsync(GetSteamId64(deathrunPlayer), tokens);

    public async Task<bool> HasAllTokensAsync(ulong steamId64, IEnumerable<string> tokens)
    {
        if (CanUseSteamId64(steamId64) is not true) return false;

        var normalizedTokens = NormalizeTokens(tokens);
        if (normalizedTokens.Count is 0) return true;

        var cachedTokens = await GetOrLoadTokensAsync(steamId64);

        lock (cachedTokens)
        {
            return normalizedTokens.All(token => cachedTokens.TryGetValue(token, out var playerToken) && playerToken.IsUsed is not true);
        }
    }

    public Task<bool> HasAnyTokenAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens)
        => HasAnyTokenAsync(GetSteamId64(deathrunPlayer), tokens);

    public async Task<bool> HasAnyTokenAsync(ulong steamId64, IEnumerable<string> tokens)
    {
        if (CanUseSteamId64(steamId64) is not true) return false;

        var normalizedTokens = NormalizeTokens(tokens);
        if (normalizedTokens.Count is 0) return false;

        var cachedTokens = await GetOrLoadTokensAsync(steamId64);

        lock (cachedTokens)
        {
            return normalizedTokens.Any(token => cachedTokens.TryGetValue(token, out var playerToken) && playerToken.IsUsed is not true);
        }
    }

    public Task<bool> MatchesTokensAsync(
        IDeathrunPlayer deathrunPlayer,
        IEnumerable<string>? requiredTokens = null,
        IEnumerable<string>? excludedTokens = null,
        bool requireAllRequiredTokens = true)
        => MatchesTokensAsync(GetSteamId64(deathrunPlayer), requiredTokens, excludedTokens, requireAllRequiredTokens);

    public async Task<bool> MatchesTokensAsync(
        ulong steamId64,
        IEnumerable<string>? requiredTokens = null,
        IEnumerable<string>? excludedTokens = null,
        bool requireAllRequiredTokens = true)
    {
        if (CanUseSteamId64(steamId64) is not true) return false;

        var normalizedRequiredTokens = NormalizeTokens(requiredTokens ?? Array.Empty<string>());
        var normalizedExcludedTokens = NormalizeTokens(excludedTokens ?? Array.Empty<string>());
        var cachedTokens = await GetOrLoadTokensAsync(steamId64);

        lock (cachedTokens)
        {
            if (normalizedExcludedTokens.Count > 0 && normalizedExcludedTokens.Any(token => cachedTokens.TryGetValue(token, out var playerToken) && playerToken.IsUsed is not true))
            {
                return false;
            }

            if (normalizedRequiredTokens.Count is 0) return true;

            return requireAllRequiredTokens
                ? normalizedRequiredTokens.All(token => cachedTokens.TryGetValue(token, out var playerToken) && playerToken.IsUsed is not true)
                : normalizedRequiredTokens.Any(token => cachedTokens.TryGetValue(token, out var playerToken) && playerToken.IsUsed is not true);
        }
    }

    public void ClearCachedTokens(ulong steamId64)
        => _tokensCache.TryRemove(steamId64, out _);

    public void ClearCache()
        => _tokensCache.Clear();

    #endregion

    #region Internal mutation helpers

    private async Task<bool> AddTokenInternalAsync(ulong steamId64, string token, int? usesLeft)
    {
        if (CanUseSteamId64(steamId64) is not true
            || TryNormalizeToken(token, out var normalizedToken) is not true
            || IsValidUsesLeft(usesLeft) is not true)
        {
            return false;
        }

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            var tokens = await GetOrLoadTokensAsync(steamId64, true);

            var shouldReactivateUsedToken = false;

            lock (tokens)
            {
                if (tokens.TryGetValue(normalizedToken, out var existingToken))
                {
                    if (existingToken.IsUsed is not true) return false;
                    shouldReactivateUsedToken = true;
                }
            }

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync($@"
                INSERT INTO `{TokensManagerConfig.TableName}` (`steamid64`, `token`, `uses_left`, `is_used`)
                VALUES (@SteamId64, @Token, @UsesLeft, 0)
                ON DUPLICATE KEY UPDATE
                    `uses_left` = CASE WHEN `is_used` = 1 THEN VALUES(`uses_left`) ELSE `uses_left` END,
                    `is_used` = CASE WHEN `is_used` = 1 THEN 0 ELSE `is_used` END;", new
            {
                SteamId64 = steamId64,
                Token = normalizedToken,
                UsesLeft = usesLeft
            });

            if (affectedRows <= 0 && shouldReactivateUsedToken is not true) return false;

            lock (tokens)
            {
                tokens[normalizedToken] = new PlayerToken(normalizedToken, usesLeft, false);
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add token {token} to {steamId64}.", normalizedToken, steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    private async Task<int> AddTokensInternalAsync(ulong steamId64, IEnumerable<string> tokens, int? usesLeft)
    {
        if (CanUseSteamId64(steamId64) is not true || IsValidUsesLeft(usesLeft) is not true) return 0;

        var normalizedTokens = NormalizeTokens(tokens);
        if (normalizedTokens.Count is 0) return 0;

        var added = 0;
        foreach (var token in normalizedTokens)
        {
            if (await AddTokenInternalAsync(steamId64, token, usesLeft) is true) added++;
        }

        return added;
    }

    private async Task<bool> SetTokenUsesInternalAsync(ulong steamId64, string token, int? usesLeft)
    {
        if (CanUseSteamId64(steamId64) is not true
            || TryNormalizeToken(token, out var normalizedToken) is not true
            || IsValidUsesLeft(usesLeft) is not true)
        {
            return false;
        }

        var playerLock = GetPlayerLock(steamId64);
        await playerLock.WaitAsync();

        try
        {
            var tokens = await GetOrLoadTokensAsync(steamId64, true);

            lock (tokens)
            {
                if (tokens.ContainsKey(normalizedToken) is not true) return false;
            }

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            var affectedRows = await dbConnection.ExecuteAsync($@"
                UPDATE `{TokensManagerConfig.TableName}`
                SET `uses_left` = @UsesLeft, `is_used` = 0
                WHERE `steamid64` = @SteamId64 AND `token` = @Token;", new
            {
                SteamId64 = steamId64,
                Token = normalizedToken,
                UsesLeft = usesLeft
            });

            if (affectedRows <= 0) return false;

            lock (tokens)
            {
                tokens[normalizedToken] = new PlayerToken(normalizedToken, usesLeft, false);
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to set token uses for {token} on {steamId64}.", normalizedToken, steamId64);
            return false;
        }
        finally
        {
            playerLock.Release();
        }
    }

    #endregion

    #region Tables

    private void SetupDatabaseTables()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var dbConnection = CreateDbConnection();
                await dbConnection.OpenAsync();

                await dbConnection.ExecuteAsync($@"
                    CREATE TABLE IF NOT EXISTS `{TokensManagerConfig.TableName}`
                    (
                        `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                        `steamid64` BIGINT UNSIGNED NOT NULL,
                        `token` VARCHAR(128) NOT NULL,
                        `uses_left` INT NULL DEFAULT NULL,
                        `is_used` TINYINT(1) NOT NULL DEFAULT 0,
                        `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

                        PRIMARY KEY (`id`),
                        UNIQUE KEY `uq_{TokensManagerConfig.TableName}_steamid64_token` (`steamid64`, `token`),
                        KEY `idx_{TokensManagerConfig.TableName}_steamid64` (`steamid64`),
                        KEY `idx_{TokensManagerConfig.TableName}_token` (`token`)
                    );");

                await EnsureUsesLeftColumnAsync(dbConnection);
                await EnsureIsUsedColumnAsync(dbConnection);
                await MarkDepletedLimitedTokensAsUsedAsync(dbConnection);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to create Tokens Manager database table.");
            }
        });
    }

    private static async Task EnsureUsesLeftColumnAsync(MySqlConnection dbConnection)
    {
        var columnExists = await dbConnection.ExecuteScalarAsync<int>($@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @TableName
              AND COLUMN_NAME = 'uses_left';", new { TokensManagerConfig.TableName });

        if (columnExists > 0) return;

        await dbConnection.ExecuteAsync($@"
            ALTER TABLE `{TokensManagerConfig.TableName}`
            ADD COLUMN `uses_left` INT NULL DEFAULT NULL AFTER `token`;");
    }

    private static async Task EnsureIsUsedColumnAsync(MySqlConnection dbConnection)
    {
        var columnExists = await dbConnection.ExecuteScalarAsync<int>($@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @TableName
              AND COLUMN_NAME = 'is_used';", new { TokensManagerConfig.TableName });

        if (columnExists > 0) return;

        await dbConnection.ExecuteAsync($@"
            ALTER TABLE `{TokensManagerConfig.TableName}`
            ADD COLUMN `is_used` TINYINT(1) NOT NULL DEFAULT 0 AFTER `uses_left`;");
    }

    private static async Task MarkDepletedLimitedTokensAsUsedAsync(MySqlConnection dbConnection)
    {
        await dbConnection.ExecuteAsync($@"
            UPDATE `{TokensManagerConfig.TableName}`
            SET `is_used` = 1
            WHERE `uses_left` IS NOT NULL
              AND `uses_left` <= 0;");
    }

    #endregion

    #region Cache / DB helpers

    private Task<Dictionary<string, PlayerToken>> GetOrLoadTokensAsync(ulong steamId64)
        => GetOrLoadTokensAsync(steamId64, false);

    private async Task<Dictionary<string, PlayerToken>> GetOrLoadTokensAsync(ulong steamId64, bool playerLockAlreadyHeld)
    {
        if (_tokensCache.TryGetValue(steamId64, out var cachedTokens)) return cachedTokens;

        if (playerLockAlreadyHeld is true)
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

    private async Task<Dictionary<string, PlayerToken>> LoadTokensFromDatabaseAsync(ulong steamId64)
    {
        await using var dbConnection = CreateDbConnection();
        await dbConnection.OpenAsync();

        var tokens = await dbConnection.QueryAsync<PlayerTokenRow>($@"
            SELECT `token` AS Token, `uses_left` AS UsesLeft, `is_used` AS IsUsed
            FROM `{TokensManagerConfig.TableName}`
            WHERE `steamid64` = @SteamId64;", new { SteamId64 = steamId64 });

        return tokens.ToDictionary(
            token => token.Token,
            token => new PlayerToken(token.Token, token.UsesLeft, token.IsUsed),
            TokenComparer);
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

        normalizedToken = token.Trim();
        return normalizedToken.Length > 0;
    }

    private static IReadOnlyCollection<string> NormalizeTokens(IEnumerable<string> tokens)
    {
        return tokens
            .Where(token => TryNormalizeToken(token, out _))
            .Select(token => token.Trim())
            .Distinct(TokenComparer)
            .ToArray();
    }

    private static bool IsValidUsesLeft(int? usesLeft)
        => usesLeft is null or > 0;

    private static bool CanSpendUses(PlayerToken playerToken, int usesToSpend)
        => playerToken.IsUsed is not true && usesToSpend > 0 && (playerToken.IsUnlimited || playerToken.UsesLeft >= usesToSpend);

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

    private readonly record struct PlayerToken(string Token, int? UsesLeft, bool IsUsed)
    {
        public bool IsUnlimited => UsesLeft is null && IsUsed is not true;
    }

    private sealed class PlayerTokenRow
    {
        public string Token { get; init; } = string.Empty;
        public int? UsesLeft { get; init; }
        public bool IsUsed { get; init; }
    }
}

public sealed class TokensManagerConfig
{
    public bool EnableTokensManager { get; init; } = true;
    public string TableName { get; init; } = "deathrun_player_tokens";
    public string Spacer { get; init; } = "If EnableTokensManager is true, you have to configure the database.json details too.";
}
