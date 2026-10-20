using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

namespace Fixes.Services.TeamLimitFix;

internal class TeamLimitFixService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private Guid? _hookId;

    public TeamLimitFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableTeamLimitFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableTeamLimitFix)));
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
            _hookId = _core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
            return;
        }

        _core.GameEvent.Unhook(_hookId!.Value);
        _hookId = null;
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        int maxPlayers = _core.Engine.GlobalVars.MaxClients;

        var gameRules = _core.EntitySystem.GetGameRules();
        if (gameRules != null && gameRules.IsValid)
        {
            gameRules.NumSpawnableTerrorist = maxPlayers;
            gameRules.MaxNumTerrorists = maxPlayers;
            gameRules.SpawnPointCount_Terrorist = maxPlayers;

            gameRules.NumSpawnableCT = maxPlayers;
            gameRules.MaxNumCTs = maxPlayers;
            gameRules.SpawnPointCount_CT = maxPlayers;
        }

        return HookResult.Continue;
    }
}
