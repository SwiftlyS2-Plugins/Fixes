using Fixes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;

namespace Fixes.Services.SlowAnimationFix;

// curtime is a 32-bit float; after a day or two on one map it has lost enough precision for
// animation, movement and lag compensation to go sluggish, and nothing in the game resets it.
// Every reload interval, while no human is connected, the current map is reloaded; the
// remaining mp_timelimit is carried over to the fresh map.
// Ported from GameFixes_mm by Slynx ( https://github.com/SlynxCZ/GameFixes_mm ).
internal class SlowAnimationFixService : IDisposable
{
    // Anything shorter would just keep flipping the map around.
    private const float MinReloadIntervalSeconds = 60f;

    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private CancellationTokenSource? _reloadTimer;
    private float _reloadInterval;

    // Map name and start time from the last map load; null until one is seen (plugin loaded mid-map).
    private string? _mapName;
    private long _mapStartTicks;

    // Remaining mp_timelimit (minutes) to restore after a reload; null = none.
    private float? _pendingTimelimit;

    public SlowAnimationFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        _core.Event.OnMapLoad += OnMapLoad;
        Apply(config.CurrentValue);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => Apply(cfg)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        _core.Event.OnMapLoad -= OnMapLoad;
        StopTimer();
    }

    private void Apply(Configuration cfg)
    {
        var interval = Math.Max(cfg.SlowAnimationReloadInterval, MinReloadIntervalSeconds);
        if (cfg.EnableSlowAnimationFix == (_reloadTimer != null) && interval == _reloadInterval)
        {
            return;
        }

        StopTimer();
        if (!cfg.EnableSlowAnimationFix)
        {
            return;
        }

        _reloadInterval = interval;
        _reloadTimer = _core.Scheduler.DelayAndRepeatBySeconds(interval, interval, OnReloadTimer);
        _core.Logger.LogInformation("Slow animation fix: checking for an empty server every {Interval:0} s.", interval);
    }

    private void StopTimer()
    {
        _reloadTimer?.Cancel();
        _reloadTimer = null;
        _reloadInterval = 0f;
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _mapName = @event.MapName;
        _mapStartTicks = Environment.TickCount64;

        if (_pendingTimelimit is not { } remaining)
        {
            return;
        }
        _pendingTimelimit = null;

        // The map's own cfg runs after the map loads and would overwrite the value; one tick later is past that.
        _core.Scheduler.NextTick(() =>
        {
            var timelimit = _core.ConVar.Find<float>("mp_timelimit");
            if (timelimit == null)
            {
                return;
            }

            var value = remaining > 0f ? remaining : 0.1f;
            _core.Logger.LogInformation("Slow animation fix: restoring the remaining timelimit -> mp_timelimit {Value:0.0}.", value);
            timelimit.Value = value;
        });
    }

    private void OnReloadTimer()
    {
        if (string.IsNullOrEmpty(_mapName))
        {
            _core.Logger.LogInformation("Slow animation fix: reload skipped, no map load seen yet (plugin loaded mid-map).");
            return;
        }

        var humans = _core.PlayerManager.GetAllPlayers().Count(p => !p.IsFakeClient && !p.ServerSideClient.IsHLTV);
        if (humans > 0)
        {
            return;
        }

        var elapsedMinutes = (Environment.TickCount64 - _mapStartTicks) / 60_000f;
        var timelimit = _core.ConVar.Find<float>("mp_timelimit")?.Value ?? 0f;
        if (timelimit > 0f)
        {
            _pendingTimelimit = timelimit - elapsedMinutes;
        }

        // Not a stock map -> a workshop one, which only changelevels by name through ds_workshop_changelevel.
        var command = _core.Engine.IsMapValid(_mapName) ? $"changelevel {_mapName}" : $"ds_workshop_changelevel {_mapName}";
        _core.Logger.LogInformation("Slow animation fix: server empty -> '{Command}'.", command);
        _core.Engine.ExecuteCommand(command);
    }
}
