using System;

namespace TokensPerformanceExample.Config;

public sealed class TokensPerformanceExampleConfig
{
    /// <summary>
    /// Cache freshness window for non-critical reads.
    /// Keep this small enough to avoid stale external changes, but large enough to avoid per-frame DB spam.
    /// </summary>
    public TimeSpan SnapshotMaxAge { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Optional periodic refresh while players are online. This is useful when other modules may grant/revoke tokens.
    /// Set to TimeSpan.Zero to disable the periodic refresh loop.
    /// </summary>
    public TimeSpan OnlineRefreshInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Demonstration only: when true, the module writes a tiny center-menu indicator from cache-only checks.
    /// No DB calls are made from ThinkPost.
    /// </summary>
    public bool ShowVipHudIndicator { get; init; } = false;

    public string VipToken { get; init; } = "vip";
    public string RespawnToken { get; init; } = "free_respawn";
    public string WeekendVipToken { get; init; } = "weekend_vip";
}
