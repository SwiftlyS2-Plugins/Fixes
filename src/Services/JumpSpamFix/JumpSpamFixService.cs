using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;

namespace Fixes.Services.JumpSpamFix;

// Blocks the "jump macro" exploit where a single tick's subtick moves fire the jump
// button check more than once, letting a rebindable macro spam-jump for bhop-like speed.
internal class JumpSpamFixService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private bool jumpSpamFixEnabled = false;

    public JumpSpamFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableJumpSpamFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableJumpSpamFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        if (enabled == jumpSpamFixEnabled)
        {
            return;
        }

        jumpSpamFixEnabled = enabled;

        if (enabled)
        {
            EnableJumpSpamFix();
            return;
        }

        DisableJumpSpamFix();
    }

    private void EnableJumpSpamFix()
    {
        _core.GameHooks.Movement.CheckJumpButtonModern.Pre += OnCheckJumpButtonModernPre;
        _core.GameHooks.Movement.CheckJumpButtonLegacy.Pre += OnCheckJumpButtonLegacyPre;
        _core.GameHooks.Movement.OnJumpModern.Post += OnJumpModernPost;
        _core.GameHooks.Movement.OnJumpLegacy.Post += OnJumpLegacyPost;
    }

    private void DisableJumpSpamFix()
    {
        _core.GameHooks.Movement.CheckJumpButtonModern.Pre -= OnCheckJumpButtonModernPre;
        _core.GameHooks.Movement.CheckJumpButtonLegacy.Pre -= OnCheckJumpButtonLegacyPre;
        _core.GameHooks.Movement.OnJumpModern.Post -= OnJumpModernPost;
        _core.GameHooks.Movement.OnJumpLegacy.Post -= OnJumpLegacyPost;
    }

    private void OnCheckJumpButtonModernPre(ref CheckJumpButtonModernMovementPreContext ctx)
    {
        var player = ctx.Params.Player;
        var moveData = ctx.Params.MoveData;
        if (player == null || moveData == null) return;

        if (player.FixesData.LastJumpTick == moveData.TickCount)
        {
            ctx.SetHookResult(HookResult.CancelOriginal);
        }
    }

    private void OnCheckJumpButtonLegacyPre(ref CheckJumpButtonLegacyMovementPreContext ctx)
    {
        var player = ctx.Params.Player;
        var moveData = ctx.Params.MoveData;
        if (player == null || moveData == null) return;

        if (player.FixesData.LastJumpTick == moveData.TickCount)
        {
            ctx.SetHookResult(HookResult.CancelOriginal);
        }
    }

    private void OnJumpModernPost(ref OnJumpModernMovementPostContext ctx)
    {
        var player = ctx.Params.Player;
        var moveData = ctx.Params.MoveData;
        if (player == null || moveData == null) return;

        player.FixesData.LastJumpTick = moveData.TickCount;
    }

    private void OnJumpLegacyPost(ref OnJumpLegacyMovementPostContext ctx)
    {
        var player = ctx.Params.Player;
        var moveData = ctx.Params.MoveData;
        if (player == null || moveData == null) return;

        player.FixesData.LastJumpTick = moveData.TickCount;
    }
}
