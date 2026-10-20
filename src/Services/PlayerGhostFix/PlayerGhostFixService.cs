using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace Fixes.Services.PlayerGhostFix;

internal class PlayerGhostFixService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private Guid? _hookId;

    public PlayerGhostFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnablePlayerGhostFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnablePlayerGhostFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        var isEnabled = _hookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            _hookId = _core.GameEvent.HookPre<EventRoundFreezeEnd>(OnRoundFreezeEnd);
            return;
        }

        _core.GameEvent.Unhook(_hookId!.Value);
        _hookId = null;
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
    {
        var ghosts = _core.PlayerManager.GetAllValidPlayers().Where(p =>
            p.IsAlive &&
            p.PlayerPawn != null &&
            p.PlayerPawn.Team == Team.Spectator);

        foreach (var player in ghosts)
        {
            if (!player.IsValid) continue;
            if (player.PlayerPawn == null) continue;

            player.ChangeTeam(Team.None);
            player.ChangeTeam(Team.Spectator);
        }

        return HookResult.Continue;
    }
}
