using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

namespace Fixes.Services.BlankMapFix;

internal class BlankMapFixService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private Guid? _hookId;

    public BlankMapFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableBlankMapFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableBlankMapFix)));
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
            _hookId = _core.GameEvent.HookPre<EventNextlevelChanged>(OnNextLevelChangedEvent);
            return;
        }

        _core.GameEvent.Unhook(_hookId!.Value);
        _hookId = null;
    }

    public HookResult OnNextLevelChangedEvent(EventNextlevelChanged @event)
    {
        if (@event.NextLevel == "")
        {
            var mapName = _core.Engine.GlobalVars.MapName;

            if (_core.Engine.IsMapValid(mapName))
            {
                _core.Engine.ExecuteCommand($"changelevel {mapName}");
            }
            else
            {
                _core.Engine.ExecuteCommand($"ds_workshop_changelevel {mapName}");
            }
        }

        return HookResult.Continue;
    }
}
