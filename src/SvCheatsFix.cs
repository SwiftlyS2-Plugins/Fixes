using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Fixes;

public partial class Fixes
{
    [EventListener<EventDelegates.OnConVarValueChanged>]
    public void OnConVarValueChanged(IOnConVarValueChanged @event)
    {
        if (@event.ConVarName == "sv_cheats")
        {
            if (bool.TryParse(@event.NewValue, out var svCheatsValue) && svCheatsValue == false)
            {
                var players = Core.PlayerManager.GetAllPlayers();
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