using System;
using System.Threading;
using System.Threading.Tasks;
using DeathrunManager.Shared;
using DeathrunManager.Shared.Config;
using DeathrunManager.Shared.Managers;
using DeathrunManager.Shared.Objects;
using DeathrunManager.Shared.Objects.Tokens;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using TokensPerformanceExample.Config;
using TokensPerformanceExample.Services;

namespace TokensPerformanceExample;

/// <summary>
/// Example module that shows the recommended high-performance pattern for using ITokensManager.
///
/// Pattern:
/// 1. Prefetch a player's active token snapshot on player-created/spawn events.
/// 2. Use cache-only reads inside hot paths such as ThinkPost/HUD/menu rendering.
/// 3. Use ITokensManager directly only for authoritative operations: grant, consume, revoke, refresh.
/// 4. Refresh the local snapshot immediately after any mutation.
/// 5. Keep a short max-age refresh window to pick up changes made by other modules without per-frame DB calls.
/// </summary>
public sealed class TokensPerformanceExample : IDeathrunModule, IDeathrunModuleConfig<TokensPerformanceExampleConfig>
{
    public string Name => "Tokens Performance Example";
    public string Author => "AquaVadis";

    public IDeathrunManager DeathrunManager { get; }
    public TokensPerformanceExampleConfig? Config { get; set; }
    public ConfigOptions ConfigOptions { get; set; }

    private readonly ILogger<TokensPerformanceExample> _logger;
    private readonly IPlayersManager _players;
    private readonly IGameplayManager _gameplay;
    private readonly PlayerTokenSnapshotCache _tokenSnapshots;

    private CancellationTokenSource? _refreshLoopCts;

    public TokensPerformanceExample(ISharedSystem sharedSystem, IDeathrunManager deathrunManagerApi)
    {
        DeathrunManager = deathrunManagerApi;
        Config = new TokensPerformanceExampleConfig();
        ConfigOptions = new ConfigOptions
        {
            FileName = "config",
            AllowReloadCommand = true
        };

        _logger = sharedSystem.GetLoggerFactory().CreateLogger<TokensPerformanceExample>();
        _players = DeathrunManager.Managers.PlayersManager;
        _gameplay = DeathrunManager.Managers.GameplayManager;
        _tokenSnapshots = new PlayerTokenSnapshotCache(
            DeathrunManager.Managers.TokensManager,
            _logger,
            Config.SnapshotMaxAge);
    }

    public void OnConfigParsed(TokensPerformanceExampleConfig config)
    {
        Config = config;
        _logger.LogInformation("[{ModuleName}] Config parsed. SnapshotMaxAge={SnapshotMaxAge}, OnlineRefreshInterval={OnlineRefreshInterval}.",
            Name,
            config.SnapshotMaxAge,
            config.OnlineRefreshInterval);
    }

    public bool Init(bool hotReload)
    {
        _players.Created += OnPlayerCreated;
        _players.Removed += OnPlayerRemoved;
        _players.ThinkPost += OnPlayerThinkPost;
        _gameplay.DeathrunPlayerSpawned += OnPlayerSpawned;
        _gameplay.MapEnded += OnMapEnded;

        PrefetchOnlinePlayers();
        StartRefreshLoop();

        return true;
    }

    public void Shutdown(bool hotReload)
    {
        _players.Created -= OnPlayerCreated;
        _players.Removed -= OnPlayerRemoved;
        _players.ThinkPost -= OnPlayerThinkPost;
        _gameplay.DeathrunPlayerSpawned -= OnPlayerSpawned;
        _gameplay.MapEnded -= OnMapEnded;

        _refreshLoopCts?.Cancel();
        _refreshLoopCts?.Dispose();
        _refreshLoopCts = null;

        _tokenSnapshots.Clear();
    }

    #region Example API your module would call

    /// <summary>
    /// Hot path example: use this from HUD, menus updated every frame, damage modifiers, etc.
    /// No database call is made here.
    /// </summary>
    public bool HasVipFast(IDeathrunPlayer player)
        => _tokenSnapshots.HasTokenCached(player, Config?.VipToken ?? "vip");

    /// <summary>
    /// Normal read path example: safe for command handlers/menu open events.
    /// It refreshes at most once per SnapshotMaxAge per player, not once per token check.
    /// </summary>
    public async ValueTask<bool> CanOpenVipMenuAsync(IDeathrunPlayer player)
    {
        var config = Config ?? new TokensPerformanceExampleConfig();

        return await _tokenSnapshots.MatchesAsync(
            player,
            new TokenRequirement(
                RequiredTokens: [config.VipToken],
                ExcludedTokens: ["vip_blocked", "restricted"],
                RequiredMatchMode: TokenMatchMode.All));
    }

    /// <summary>
    /// Authoritative consume example: do not decrement local cache yourself.
    /// Consume through ITokensManager, then refresh the local snapshot immediately.
    /// </summary>
    public async Task<bool> TryUseRespawnTokenAsync(IDeathrunPlayer player)
    {
        var token = Config?.RespawnToken ?? "free_respawn";
        var result = await _tokenSnapshots.ConsumeAndRefreshAsync(player, token);

        switch (result)
        {
            case TokenConsumeResult.Consumed:
                player.SendChatMessage($"{{GREEN}}Used token {{LIGHT_RED}}{token}{{DEFAULT}}.");
                return true;
            case TokenConsumeResult.Unlimited:
                player.SendChatMessage($"{{GREEN}}Token {{LIGHT_RED}}{token}{{GREEN}} is unlimited, no uses were consumed.");
                return true;
            case TokenConsumeResult.Missing:
                player.SendChatMessage($"{{RED}}You do not have token {{LIGHT_RED}}{token}{{RED}}.");
                return false;
            case TokenConsumeResult.Expired:
            case TokenConsumeResult.Inactive:
            case TokenConsumeResult.InsufficientUses:
                player.SendChatMessage($"{{RED}}Token {{LIGHT_RED}}{token}{{RED}} is not usable right now. Result: {result}.");
                return false;
            default:
                player.SendChatMessage($"{{RED}}Could not use token {{LIGHT_RED}}{token}{{RED}}. Result: {result}.");
                return false;
        }
    }

    /// <summary>
    /// Grant example: after grant, refresh local snapshot so the player can use the token immediately.
    /// </summary>
    public async Task<TokenGrantResult> GrantWeekendVipAsync(IDeathrunPlayer player)
    {
        var config = Config ?? new TokensPerformanceExampleConfig();
        var result = await _tokenSnapshots.GrantAndRefreshAsync(
            player,
            TokenGrant.Temporary(config.WeekendVipToken, DateTime.UtcNow.AddDays(2)));

        if (result is TokenGrantResult.Created or TokenGrantResult.Replaced or TokenGrantResult.Refreshed)
        {
            player.SendChatMessage($"{{GREEN}}Granted temporary token {{LIGHT_RED}}{config.WeekendVipToken}{{GREEN}} for 2 days.");
        }

        return result;
    }

    #endregion

    #region Events

    private void OnPlayerCreated(IDeathrunPlayer player)
        => PrefetchPlayer(player);

    private void OnPlayerSpawned(IDeathrunPlayer player)
        => PrefetchPlayer(player);

    private void OnPlayerRemoved(IDeathrunPlayer player)
        => _tokenSnapshots.RemovePlayer(player);

    private void OnMapEnded(string mapName)
        => _tokenSnapshots.Clear();

    /// <summary>
    /// Demonstrates the main rule: do NOT await TokensManager here.
    /// This is called frequently, so it only reads the module-local snapshot.
    /// </summary>
    private void OnPlayerThinkPost(IDeathrunPlayer player)
    {
        if (Config?.ShowVipHudIndicator is not true) return;
        if (player.IsValid is not true) return;

        var hasVip = HasVipFast(player);
        player.SetCenterMenuTopRowCellFourHtml(hasVip
            ? "<font color='#90EE90'>VIP token active</font>"
            : null);
    }

    #endregion

    #region Refresh / prefetch

    private void PrefetchOnlinePlayers()
    {
        var players = _players.GetAllValidDeathrunPlayers();
        foreach (var player in players)
        {
            PrefetchPlayer(player);
        }
    }

    private void PrefetchPlayer(IDeathrunPlayer player)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _tokenSnapshots.RefreshAsync(player);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to prefetch token snapshot for player.");
            }
        });
    }

    private void StartRefreshLoop()
    {
        var interval = Config?.OnlineRefreshInterval ?? TimeSpan.Zero;
        if (interval <= TimeSpan.Zero) return;

        _refreshLoopCts = new CancellationTokenSource();
        var cancellationToken = _refreshLoopCts.Token;

        _ = Task.Run(async () =>
        {
            while (cancellationToken.IsCancellationRequested is not true)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);

                    foreach (var player in _players.GetAllValidDeathrunPlayers())
                    {
                        await _tokenSnapshots.RefreshAsync(player);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Token snapshot refresh loop failed.");
                }
            }
        }, cancellationToken);
    }

    #endregion
}
