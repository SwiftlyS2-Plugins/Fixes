using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace Fixes;

public partial class Fixes
{
    private Guid? playerGhostFixHookId;

    private void SetPlayerGhostFixEnabled(bool enabled)
    {
        var isEnabled = playerGhostFixHookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            playerGhostFixHookId = Core.GameEvent.HookPre<EventRoundFreezeEnd>(OnRoundFreezeEnd);
            return;
        }

        Core.GameEvent.Unhook(playerGhostFixHookId!.Value);
        playerGhostFixHookId = null;
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
    {
        var ghosts = Core.PlayerManager.GetAllValidPlayers().Where(p =>
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
