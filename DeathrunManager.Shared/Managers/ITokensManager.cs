using System.Collections.Generic;
using System.Threading.Tasks;
using DeathrunManager.Shared.Objects;

namespace DeathrunManager.Shared.Managers;

/// <summary>
/// Provides player token management backed by persistent MySQL storage.
/// Tokens are stored per player's SteamID64 and can be used by Deathrun modules
/// to gate features, permissions, rewards, cosmetics, missions, and similar logic.
///
/// Tokens may be permanent or limited-use.
/// Permanent tokens have no remaining-use limit.
/// Limited-use tokens lose uses through TryUseTokenAsync / TryUseTokensAsync and are marked as used when uses reach 0.
/// </summary>
public interface ITokensManager
{
    /// <summary>
    /// Loads and caches all active token names for the supplied player.
    /// Players with SteamID64 equal to 0 are ignored and return an empty set.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetTokensAsync(IDeathrunPlayer deathrunPlayer);

    /// <summary>
    /// Loads and caches all active token names for the supplied SteamID64.
    /// SteamID64 equal to 0 is ignored and returns an empty set.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetTokensAsync(ulong steamId64);

    /// <summary>
    /// Adds a permanent token to the supplied player if it does not already exist.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> AddTokenAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Adds a permanent token to the supplied SteamID64 if it does not already exist.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> AddTokenAsync(ulong steamId64, string token);

    /// <summary>
    /// Adds a limited-use token to the supplied player if it does not already exist.
    /// usesLeft must be greater than 0. Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> AddTokenAsync(IDeathrunPlayer deathrunPlayer, string token, int usesLeft);

    /// <summary>
    /// Adds a limited-use token to the supplied SteamID64 if it does not already exist.
    /// usesLeft must be greater than 0. SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> AddTokenAsync(ulong steamId64, string token, int usesLeft);

    /// <summary>
    /// Adds a range of permanent tokens to the supplied player.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<int> AddTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens);

    /// <summary>
    /// Adds a range of permanent tokens to the supplied SteamID64.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<int> AddTokensAsync(ulong steamId64, IEnumerable<string> tokens);

    /// <summary>
    /// Adds a range of limited-use tokens to the supplied player.
    /// usesLeftPerToken must be greater than 0. Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<int> AddTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens, int usesLeftPerToken);

    /// <summary>
    /// Adds a range of limited-use tokens to the supplied SteamID64.
    /// usesLeftPerToken must be greater than 0. SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<int> AddTokensAsync(ulong steamId64, IEnumerable<string> tokens, int usesLeftPerToken);

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
    /// The old token's remaining uses are preserved on the new token.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> UpdateTokenAsync(IDeathrunPlayer deathrunPlayer, string oldToken, string newToken);

    /// <summary>
    /// Replaces an old token with a new token for the supplied SteamID64.
    /// The old token's remaining uses are preserved on the new token.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> UpdateTokenAsync(ulong steamId64, string oldToken, string newToken);

    /// <summary>
    /// Replaces all existing tokens for the supplied player with the provided permanent token set.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> SetTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens);

    /// <summary>
    /// Replaces all existing tokens for the supplied SteamID64 with the provided permanent token set.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> SetTokensAsync(ulong steamId64, IEnumerable<string> tokens);

    /// <summary>
    /// Sets a token to permanent uses. The token must already exist.
    /// Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> SetTokenUnlimitedUsesAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Sets a token to permanent uses. The token must already exist.
    /// SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> SetTokenUnlimitedUsesAsync(ulong steamId64, string token);

    /// <summary>
    /// Sets remaining uses for an existing token.
    /// usesLeft must be greater than 0. Players with SteamID64 equal to 0 are ignored.
    /// </summary>
    Task<bool> SetTokenUsesAsync(IDeathrunPlayer deathrunPlayer, string token, int usesLeft);

    /// <summary>
    /// Sets remaining uses for an existing token.
    /// usesLeft must be greater than 0. SteamID64 equal to 0 is ignored.
    /// </summary>
    Task<bool> SetTokenUsesAsync(ulong steamId64, string token, int usesLeft);

    /// <summary>
    /// Gets remaining uses for a token.
    /// Returns -1 for active permanent tokens and 0 when the player does not have the token or the token is already used.
    /// Players with SteamID64 equal to 0 return 0.
    /// </summary>
    Task<int> GetTokenUsesLeftAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Gets remaining uses for a token.
    /// Returns -1 for active permanent tokens and 0 when the SteamID64 does not have the token or the token is already used.
    /// SteamID64 equal to 0 returns 0.
    /// </summary>
    Task<int> GetTokenUsesLeftAsync(ulong steamId64, string token);

    /// <summary>
    /// Loads and caches all used token names for the supplied player.
    /// Used tokens are limited-use tokens that reached 0 uses and are kept in MySQL instead of deleted.
    /// Players with SteamID64 equal to 0 are ignored and return an empty set.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetUsedTokensAsync(IDeathrunPlayer deathrunPlayer);

    /// <summary>
    /// Loads and caches all used token names for the supplied SteamID64.
    /// Used tokens are limited-use tokens that reached 0 uses and are kept in MySQL instead of deleted.
    /// SteamID64 equal to 0 is ignored and returns an empty set.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetUsedTokensAsync(ulong steamId64);

    /// <summary>
    /// Checks whether a token exists but is already marked as used for the supplied player.
    /// Players with SteamID64 equal to 0 return false.
    /// </summary>
    Task<bool> IsTokenUsedAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Checks whether a token exists but is already marked as used for the supplied SteamID64.
    /// SteamID64 equal to 0 returns false.
    /// </summary>
    Task<bool> IsTokenUsedAsync(ulong steamId64, string token);

    /// <summary>
    /// Uses a token once.
    /// Permanent tokens remain unchanged. Limited-use tokens are decremented and marked as used at 0.
    /// Returns false if the player does not have the token or the player SteamID64 is 0.
    /// </summary>
    Task<bool> TryUseTokenAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Uses a token once.
    /// Permanent tokens remain unchanged. Limited-use tokens are decremented and marked as used at 0.
    /// Returns false if the SteamID64 does not have the token or is 0.
    /// </summary>
    Task<bool> TryUseTokenAsync(ulong steamId64, string token);

    /// <summary>
    /// Uses a token a specified amount of times.
    /// Permanent tokens remain unchanged. Limited-use tokens must have at least usesToSpend uses left.
    /// Returns false if the player does not have the token, usesToSpend is invalid, or the player SteamID64 is 0.
    /// </summary>
    Task<bool> TryUseTokenAsync(IDeathrunPlayer deathrunPlayer, string token, int usesToSpend);

    /// <summary>
    /// Uses a token a specified amount of times.
    /// Permanent tokens remain unchanged. Limited-use tokens must have at least usesToSpend uses left.
    /// Returns false if the SteamID64 does not have the token, usesToSpend is invalid, or the SteamID64 is 0.
    /// </summary>
    Task<bool> TryUseTokenAsync(ulong steamId64, string token, int usesToSpend);

    /// <summary>
    /// Uses a range of tokens once each.
    /// If requireAllTokens is true, all tokens must be usable before any token is consumed.
    /// If requireAllTokens is false, every currently usable token from the range is consumed once and the method returns true when at least one token was consumed.
    /// </summary>
    Task<bool> TryUseTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens, bool requireAllTokens = true);

    /// <summary>
    /// Uses a range of tokens once each.
    /// If requireAllTokens is true, all tokens must be usable before any token is consumed.
    /// If requireAllTokens is false, every currently usable token from the range is consumed once and the method returns true when at least one token was consumed.
    /// </summary>
    Task<bool> TryUseTokensAsync(ulong steamId64, IEnumerable<string> tokens, bool requireAllTokens = true);

    /// <summary>
    /// Checks whether the supplied player has a specific active token. Used tokens do not pass this check.
    /// Players with SteamID64 equal to 0 return false.
    /// </summary>
    Task<bool> HasTokenAsync(IDeathrunPlayer deathrunPlayer, string token);

    /// <summary>
    /// Checks whether the supplied SteamID64 has a specific active token. Used tokens do not pass this check.
    /// SteamID64 equal to 0 returns false.
    /// </summary>
    Task<bool> HasTokenAsync(ulong steamId64, string token);

    /// <summary>
    /// Checks whether the supplied player has an active token and enough remaining uses.
    /// Permanent tokens always pass when the token exists.
    /// Players with SteamID64 equal to 0 return false.
    /// </summary>
    Task<bool> HasTokenUsesAsync(IDeathrunPlayer deathrunPlayer, string token, int requiredUses);

    /// <summary>
    /// Checks whether the supplied SteamID64 has an active token and enough remaining uses.
    /// Permanent tokens always pass when the token exists.
    /// SteamID64 equal to 0 returns false.
    /// </summary>
    Task<bool> HasTokenUsesAsync(ulong steamId64, string token, int requiredUses);

    /// <summary>
    /// Checks whether the supplied player has every token in the provided range.
    /// Players with SteamID64 equal to 0 return false.
    /// </summary>
    Task<bool> HasAllTokensAsync(IDeathrunPlayer deathrunPlayer, IEnumerable<string> tokens);

    /// <summary>
    /// Checks whether the supplied SteamID64 has every token in the provided range.
    /// SteamID64 equal to 0 return false.
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
