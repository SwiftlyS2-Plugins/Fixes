using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Fixes;

public partial class Fixes
{
    private bool svCheatsFixEnabled = false;

    private void InitSvCheatsFix()
    {
        svCheatsFixEnabled = Config.CurrentValue.EnableSvCheatsFix;
        Config.OnChange(
            (v, _) =>
            {
                svCheatsFixEnabled = v.EnableSvCheatsFix;
            }
        );
    }

    [EventListener<EventDelegates.OnConVarValueChanged>]
    public void OnConVarValueChanged(IOnConVarValueChanged @event)
    {
        if (!svCheatsFixEnabled || @event.ConVarName != "sv_cheats")
        {
            return;
        }

        var cheatsDisabled = IsCheatsDisabled(@event.NewValue);

        if (!cheatsDisabled)
        {
            return;
        }

        var players = Core.PlayerManager.GetAllValidPlayers();

        foreach (var player in players)
        {
            var pawn = player.Pawn;

            if (pawn == null)
            {
                continue;
            }

            var isNoclip =
                pawn.MoveType == MoveType_t.MOVETYPE_NOCLIP
                || pawn.ActualMoveType == MoveType_t.MOVETYPE_NOCLIP;

            if (!isNoclip)
            {
                continue;
            }

            pawn.MoveType = MoveType_t.MOVETYPE_WALK;
            pawn.ActualMoveType = MoveType_t.MOVETYPE_WALK;
            pawn.MoveTypeUpdated();
        }
    }

    private static bool IsCheatsDisabled(string value)
    {
        if (int.TryParse(value, out var numericValue))
        {
            return numericValue == 0;
        }

        var isBoolean = bool.TryParse(value, out var booleanValue);
        return isBoolean && !booleanValue;
    }
}
