# TokensPerformanceExample

Example Deathrun module showing the recommended pattern for using the refactored `ITokensManager` without spamming MySQL and without letting temporary/used token state stay stale in hot paths.

## Pattern

1. **Prefetch once per player** on player creation/spawn.
2. **Use cache-only checks** inside `ThinkPost`, HUD rendering, center menus, and any other per-frame code.
3. **Use `ITokensManager` for mutations only**: grant, consume, revoke, rename, delete.
4. **Refresh the local snapshot immediately after mutation** so the player sees changes right away.
5. **Use short snapshot max-age** to pick up changes from other modules without checking MySQL every frame.
6. **Reject expired temporary tokens locally** by tracking the next token expiry timestamp in the snapshot.

## Important examples

```csharp
// Hot path: no DB, safe for per-frame checks.
if (HasVipFast(player))
{
    // show VIP HUD, unlock menu row, etc.
}
```

```csharp
// Normal path: refreshes once per configured SnapshotMaxAge, then checks memory.
var canOpen = await CanOpenVipMenuAsync(player);
```

```csharp
// Consume path: atomic DB update + immediate local refresh.
var ok = await TryUseRespawnTokenAsync(player);
```

```csharp
// Grant path: DB grant + immediate local refresh.
await GrantWeekendVipAsync(player);
```

## Why this avoids stale state

The cache entry stores `NextTokenExpiryUtc`. Even if no DB refresh happens, a temporary token that expires at `12:00:00 UTC` will stop passing `HasTokenCached` after that exact time.

For limited-use tokens, all consuming goes through `ITokensManager.ConsumeTokenAsync`, which performs an atomic database update. The module refreshes the local snapshot after every consume/grant/revoke.

## Config

Generated at:

```text
sharp/configs/Deathrun.Manager/modules/TokensPerformanceExample/config.json
```

Recommended defaults:

```json
{
  "SnapshotMaxAge": "00:00:02",
  "OnlineRefreshInterval": "00:00:10",
  "ShowVipHudIndicator": false,
  "VipToken": "vip",
  "RespawnToken": "free_respawn",
  "WeekendVipToken": "weekend_vip"
}
```
