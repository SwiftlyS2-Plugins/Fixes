using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;

namespace Fixes;

// Blocks the "jump macro" exploit where a single tick's subtick moves fire the jump
// button check more than once, letting a rebindable macro spam-jump for bhop-like speed.
public partial class Fixes
{
    private bool jumpSpamFixEnabled = false;

    private void SetJumpSpamFixEnabled(bool enabled)
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
        Core.GameHooks.Movement.CheckJumpButtonModern.Pre += OnCheckJumpButtonModernPre;
        Core.GameHooks.Movement.CheckJumpButtonLegacy.Pre += OnCheckJumpButtonLegacyPre;
        Core.GameHooks.Movement.OnJumpModern.Post += OnJumpModernPost;
        Core.GameHooks.Movement.OnJumpLegacy.Post += OnJumpLegacyPost;
    }

    private void DisableJumpSpamFix()
    {
        Core.GameHooks.Movement.CheckJumpButtonModern.Pre -= OnCheckJumpButtonModernPre;
        Core.GameHooks.Movement.CheckJumpButtonLegacy.Pre -= OnCheckJumpButtonLegacyPre;
        Core.GameHooks.Movement.OnJumpModern.Post -= OnJumpModernPost;
        Core.GameHooks.Movement.OnJumpLegacy.Post -= OnJumpLegacyPost;
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
