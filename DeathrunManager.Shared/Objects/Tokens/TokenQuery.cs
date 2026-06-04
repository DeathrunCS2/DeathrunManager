using System.Collections.Generic;

namespace DeathrunManager.Shared.Objects.Tokens;

public sealed record TokenQuery(
    bool IncludeInactive = false,
    bool OnlyActive = true,
    bool IncludeExpired = false,
    IEnumerable<string>? Tokens = null)
{
    public static TokenQuery ActiveOnly { get; } = new();

    public static TokenQuery All { get; } = new(
        IncludeInactive: true,
        OnlyActive: false,
        IncludeExpired: true);
}
