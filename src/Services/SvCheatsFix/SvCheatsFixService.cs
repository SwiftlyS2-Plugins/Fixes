using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Fixes.Services.SvCheatsFix;

internal class SvCheatsFixService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private bool svCheatsFixEnabled = false;

    public SvCheatsFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableSvCheatsFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableSvCheatsFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        if (enabled == svCheatsFixEnabled)
        {
            return;
        }

        svCheatsFixEnabled = enabled;

        if (enabled)
        {
            _core.Event.OnConVarValueChanged += OnConVarValueChanged;
            return;
        }

        _core.Event.OnConVarValueChanged -= OnConVarValueChanged;
    }

    public void OnConVarValueChanged(IOnConVarValueChanged @event)
    {
        if (@event.ConVarName == "sv_cheats")
        {
            if (bool.TryParse(@event.NewValue, out var svCheatsValue) && svCheatsValue == false)
            {
                var players = _core.PlayerManager.GetAllValidPlayers();
                foreach (var player in players)
                {
                    var pawn = player.Pawn;
                    if (pawn != null && pawn.IsValid && (pawn.MoveType == MoveType_t.MOVETYPE_NOCLIP || pawn.ActualMoveType == MoveType_t.MOVETYPE_NOCLIP))
                    {
                        pawn.MoveType = MoveType_t.MOVETYPE_WALK;
                        pawn.ActualMoveType = MoveType_t.MOVETYPE_WALK;
                        pawn.MoveTypeUpdated();
                    }
                }
            }
        }
    }
}
