using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;

namespace Fixes.Services.InputActivatorCrashFix;

internal class InputActivatorCrashFixService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private bool _enabled;

    public InputActivatorCrashFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableInputActivatorCrashFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableInputActivatorCrashFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        if (enabled == _enabled)
        {
            return;
        }

        _enabled = enabled;

        if (enabled)
        {
            _core.GameHooks.Entities.AcceptInput.Pre += OnAcceptInputPre;
            return;
        }

        _core.GameHooks.Entities.AcceptInput.Pre -= OnAcceptInputPre;
    }

    private void OnAcceptInputPre(ref AcceptInputEntityPreContext ctx)
    {
        var p = ctx.Params;

        if (p.Activator == null
            && p.InputName == "TestActivator"
            && p.DesignerName.StartsWith("filter_", StringComparison.Ordinal))
        {
            ctx.SetHookResult(HookResult.CancelOriginal);
        }
    }
}
