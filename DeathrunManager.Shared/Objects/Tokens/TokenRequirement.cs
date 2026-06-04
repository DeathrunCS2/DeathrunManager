using System.Collections.Generic;

namespace DeathrunManager.Shared.Objects.Tokens;

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
