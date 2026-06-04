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

    private readonly ConcurrentDictionary<ulong, HashSet<string>> _tokensCache = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _playerLocks = new();

    private static TokensManagerConfig TokensManagerConfig { get; set; } = null!;

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
            return tokens.ToArray();
        }
    }

    public Task<bool> AddTokenAsync(IDeathrunPlayer deathrunPlayer, string token)
        => AddTokenAsync(GetSteamId64(deathrunPlayer), token);

    public async Task<bool> AddTokenAsync(ulong steamId64, string token)
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
                if (tokens.Contains(normalizedToken)) return false;
            }

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();

            await dbConnection.ExecuteAsync($@"
                INSERT IGNORE INTO `{TokensManagerConfig.TableName}` (`steamid64`, `token`)
                VALUES (@SteamId64, @Token);", new { SteamId64 = steamId64, Token = normalizedToken });

            lock (tokens)
            {
                tokens.Add(normalizedToken);
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

    public Task<int> AddTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens)
        => AddTokensAsync(GetSteamId64(deathrunPlayer), tokens);

    public async Task<int> AddTokensAsync(ulong steamId64, IEnumerable<string> tokens)
    {
        if (CanUseSteamId64(steamId64) is not true) return 0;

        var normalizedTokens = NormalizeTokens(tokens);
        if (normalizedTokens.Count is 0) return 0;

        var added = 0;
        foreach (var token in normalizedTokens)
        {
            if (await AddTokenAsync(steamId64, token) is true) added++;
        }

        return added;
    }

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
                if (tokens.Contains(normalizedToken) is not true) return false;
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

            lock (tokens)
            {
                if (tokens.Contains(normalizedOldToken) is not true) return false;
            }

            await using var dbConnection = CreateDbConnection();
            await dbConnection.OpenAsync();
            await using var transaction = await dbConnection.BeginTransactionAsync();

            await dbConnection.ExecuteAsync($@"
                DELETE FROM `{TokensManagerConfig.TableName}`
                WHERE `steamid64` = @SteamId64 AND `token` = @OldToken;", new { SteamId64 = steamId64, OldToken = normalizedOldToken }, transaction);

            await dbConnection.ExecuteAsync($@"
                INSERT IGNORE INTO `{TokensManagerConfig.TableName}` (`steamid64`, `token`)
                VALUES (@SteamId64, @NewToken);", new { SteamId64 = steamId64, NewToken = normalizedNewToken }, transaction);

            await transaction.CommitAsync();

            lock (tokens)
            {
                tokens.Remove(normalizedOldToken);
                tokens.Add(normalizedNewToken);
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
                    INSERT IGNORE INTO `{TokensManagerConfig.TableName}` (`steamid64`, `token`)
                    VALUES (@SteamId64, @Token);", new { SteamId64 = steamId64, Token = token }, transaction);
            }

            await transaction.CommitAsync();

            _tokensCache[steamId64] = new HashSet<string>(normalizedTokens, TokenComparer);

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
            return tokens.Contains(normalizedToken);
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
            return normalizedTokens.All(cachedTokens.Contains);
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
            return normalizedTokens.Any(cachedTokens.Contains);
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
            if (normalizedExcludedTokens.Count > 0 && normalizedExcludedTokens.Any(cachedTokens.Contains))
            {
                return false;
            }

            if (normalizedRequiredTokens.Count is 0) return true;

            return requireAllRequiredTokens
                ? normalizedRequiredTokens.All(cachedTokens.Contains)
                : normalizedRequiredTokens.Any(cachedTokens.Contains);
        }
    }

    public void ClearCachedTokens(ulong steamId64)
        => _tokensCache.TryRemove(steamId64, out _);

    public void ClearCache()
        => _tokensCache.Clear();

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
                        `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

                        PRIMARY KEY (`id`),
                        UNIQUE KEY `uq_{TokensManagerConfig.TableName}_steamid64_token` (`steamid64`, `token`),
                        KEY `idx_{TokensManagerConfig.TableName}_steamid64` (`steamid64`),
                        KEY `idx_{TokensManagerConfig.TableName}_token` (`token`)
                    );");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to create Tokens Manager database table.");
            }
        });
    }

    #endregion

    #region Cache / DB helpers

    private Task<HashSet<string>> GetOrLoadTokensAsync(ulong steamId64)
        => GetOrLoadTokensAsync(steamId64, false);

    private async Task<HashSet<string>> GetOrLoadTokensAsync(ulong steamId64, bool playerLockAlreadyHeld)
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

    private async Task<HashSet<string>> LoadTokensFromDatabaseAsync(ulong steamId64)
    {
        await using var dbConnection = CreateDbConnection();
        await dbConnection.OpenAsync();

        var tokens = await dbConnection.QueryAsync<string>($@"
            SELECT `token`
            FROM `{TokensManagerConfig.TableName}`
            WHERE `steamid64` = @SteamId64;", new { SteamId64 = steamId64 });

        return new HashSet<string>(tokens, TokenComparer);
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
}

public sealed class TokensManagerConfig
{
    public bool EnableTokensManager { get; init; } = true;
    public string TableName { get; init; } = "deathrun_player_tokens";
    public string Spacer { get; init; } = "If EnableTokensManager is true, you have to configure the database.json details too.";
}
