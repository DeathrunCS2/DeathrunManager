
using System.Collections.Generic;

namespace DeathrunManager.Shared.Objects.Tokens;

public sealed record TokenConsumeBatchResult(
    bool Success,
    IReadOnlyDictionary<string, TokenConsumeResult> Results)
{
    public static TokenConsumeBatchResult Empty { get; } = new(
        false,
        new Dictionary<string, TokenConsumeResult>());
}
