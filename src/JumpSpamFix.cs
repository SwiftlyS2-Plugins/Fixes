using System.Collections.Concurrent;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Misc;

namespace Fixes;

// Blocks the "jump macro" exploit where a single tick's subtick moves fire the jump
// button check more than once, letting a rebindable macro spam-jump for bhop-like speed.
public partial class Fixes
{
    private bool jumpSpamFixEnabled = false;
    private readonly ConcurrentDictionary<int, int> _lastJumpTick = new();

    private void InitJumpSpamFix()
    {
        jumpSpamFixEnabled = Config.CurrentValue.EnableJumpSpamFix;
        Config.OnChange((v, _) =>
        {
            jumpSpamFixEnabled = v.EnableJumpSpamFix;
        });

        Core.GameHooks.Movement.CheckJumpButtonModern.Pre += OnCheckJumpButtonModernPre;
        Core.GameHooks.Movement.CheckJumpButtonLegacy.Pre += OnCheckJumpButtonLegacyPre;
        Core.GameHooks.Movement.OnJumpModern.Post += OnJumpModernPost;
        Core.GameHooks.Movement.OnJumpLegacy.Post += OnJumpLegacyPost;
    }

    private void OnCheckJumpButtonModernPre(ref CheckJumpButtonModernMovementPreContext ctx)
    {
        if (!jumpSpamFixEnabled) return;

        var player = ctx.Params.Player;
        var moveData = ctx.Params.MoveData;
        if (player == null || moveData == null) return;

        if (_lastJumpTick.TryGetValue(player.Slot, out var lastTick) && lastTick == moveData.TickCount)
        {
            ctx.SetHookResult(HookResult.CancelOriginal);
        }
    }

    private void OnCheckJumpButtonLegacyPre(ref CheckJumpButtonLegacyMovementPreContext ctx)
    {
        if (!jumpSpamFixEnabled) return;

        var player = ctx.Params.Player;
        var moveData = ctx.Params.MoveData;
        if (player == null || moveData == null) return;

        if (_lastJumpTick.TryGetValue(player.Slot, out var lastTick) && lastTick == moveData.TickCount)
        {
            ctx.SetHookResult(HookResult.CancelOriginal);
        }
    }

    private void OnJumpModernPost(ref OnJumpModernMovementPostContext ctx)
    {
        if (!jumpSpamFixEnabled) return;

        var player = ctx.Params.Player;
        var moveData = ctx.Params.MoveData;
        if (player == null || moveData == null) return;

        _lastJumpTick[player.Slot] = moveData.TickCount;
    }

    private void OnJumpLegacyPost(ref OnJumpLegacyMovementPostContext ctx)
    {
        if (!jumpSpamFixEnabled) return;

        var player = ctx.Params.Player;
        var moveData = ctx.Params.MoveData;
        if (player == null || moveData == null) return;

        _lastJumpTick[player.Slot] = moveData.TickCount;
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnJumpSpamFixClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        _lastJumpTick.TryRemove(@event.PlayerId, out _);
    }
}
