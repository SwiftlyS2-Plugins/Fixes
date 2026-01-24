using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Fixes;

public partial class Fixes
{
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerTeamEvent(EventPlayerTeam @event)
    {
        if (@event.Disconnect) return HookResult.Continue;

        var player = @event.UserIdPlayer;
        var team = @event.Team;

        core.Scheduler.NextTick(() =>
        {
            if (player == null) return;
            if (!player.IsValid) return;

            var pawn = player.Pawn;
            if (pawn != null && pawn.IsValid)
            {
                pawn.TeamNum = team;
                pawn.TeamNumUpdated();
            }
        });

        return HookResult.Continue;
    }
}