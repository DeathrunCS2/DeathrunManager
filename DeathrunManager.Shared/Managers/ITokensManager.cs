using System.Collections.Generic;
using System.Threading.Tasks;
using DeathrunManager.Shared.Objects;

namespace DeathrunManager.Shared.Managers;

/// <summary>
/// Provides player token management backed by persistent MySQL storage.
/// Tokens are stored per player's SteamID64 and can be used by Deathrun modules
/// to gate features, permissions, rewards, cosmetics, missions, and similar logic.
/// </summary>
public interface ITokensManager
{
    /// <summary>
    /// Loads and caches all tokens for the supplied player.
    /// Players with SteamID64 equal to 0 are ignored and return an empty set.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetTokensAsync(IDeathrunPlayer deathrunPlayer);

    /// <summary>
    /// Loads and caches all tokens for the supplied SteamID64.
    /// SteamID64 equal to 0 is ignored and returns an empty set.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetTokensAsync(ulong steamId64);

    /// <summary>
    /// Adds a token to the supplied player if it does not already exist.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> AddTokenAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Adds a token to the supplied SteamID64 if it does not already exist.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> AddTokenAsync(ulong steamId64, string token);

    /// <summary>
    /// Adds a range of tokens to the supplied player.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<int> AddTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens);

    /// <summary>
    /// Adds a range of tokens to the supplied SteamID64.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<int> AddTokensAsync(ulong steamId64, IEnumerable<string> tokens);

    /// <summary>
    /// Removes a token from the supplied player if it exists.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> RemoveTokenAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Removes a token from the supplied SteamID64 if it exists.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> RemoveTokenAsync(ulong steamId64, string token);

    /// <summary>
    /// Removes a range of tokens from the supplied player.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<int> RemoveTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens);

    /// <summary>
    /// Removes a range of tokens from the supplied SteamID64.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<int> RemoveTokensAsync(ulong steamId64, IEnumerable<string> tokens);

    /// <summary>
    /// Replaces an old token with a new token for the supplied player.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> UpdateTokenAsync(IDeathrunPlayer deathrunPlayer, string oldToken, string newToken);

    /// <summary>
    /// Replaces an old token with a new token for the supplied SteamID64.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> UpdateTokenAsync(ulong steamId64, string oldToken, string newToken);

    /// <summary>
    /// Replaces all existing tokens for the supplied player with the provided token set.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> SetTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens);

    /// <summary>
    /// Replaces all existing tokens for the supplied SteamID64 with the provided token set.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> SetTokensAsync(ulong steamId64, IEnumerable<string> tokens);

    /// <summary>
    /// Checks whether the supplied player has a specific token.
    /// Players with SteamID64 equal to 0 return false.
    /// </summary>
    Task<bool> HasTokenAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Checks whether the supplied SteamID64 has a specific token.
    /// SteamID64 equal to 0 returns false.
    /// </summary>
    Task<bool> HasTokenAsync(ulong steamId64, string token);

    /// <summary>
    /// Checks whether the supplied player has every token in the provided range.
    /// Players with SteamID64 equal to 0 return false.
    /// </summary>
    Task<bool> HasAllTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens);

    /// <summary>
    /// Checks whether the supplied SteamID64 has every token in the provided range.
    /// SteamID64 equal to 0 returns false.
    /// </summary>
    Task<bool> HasAllTokensAsync(ulong steamId64, IEnumerable<string> tokens);

    /// <summary>
    /// Checks whether the supplied player has any token from the provided range.
    /// Players with SteamID64 equal to 0 return false.
    /// </summary>
    Task<bool> HasAnyTokenAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens);

    /// <summary>
    /// Checks whether the supplied SteamID64 has any token from the provided range.
    /// SteamID64 equal to 0 returns false.
    /// </summary>
    Task<bool> HasAnyTokenAsync(ulong steamId64, IEnumerable<string> tokens);

    /// <summary>
    /// Checks token requirements in one call.
    /// Required tokens may be matched as all-required or any-required.
    /// Excluded tokens fail the check if the player has any of them.
    /// Players with SteamID64 equal to 0 return false.
    /// </summary>
    Task<bool> MatchesTokensAsync(
        IDeathrunPlayer deathrunPlayer,
        IEnumerable<string>? requiredTokens = null,
        IEnumerable<string>? excludedTokens = null,
        bool requireAllRequiredTokens = true);

    /// <summary>
    /// Checks token requirements in one call.
    /// Required tokens may be matched as all-required or any-required.
    /// Excluded tokens fail the check if the SteamID64 has any of them.
    /// SteamID64 equal to 0 returns false.
    /// </summary>
    Task<bool> MatchesTokensAsync(
        ulong steamId64,
        IEnumerable<string>? requiredTokens = null,
        IEnumerable<string>? excludedTokens = null,
        bool requireAllRequiredTokens = true);

    /// <summary>
    /// Removes cached tokens for the supplied SteamID64. The database is not modified.
    /// </summary>
    void ClearCachedTokens(ulong steamId64);

    /// <summary>
    /// Removes all cached tokens. The database is not modified.
    /// </summary>
    void ClearCache();
}
