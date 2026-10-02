using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

namespace Fixes;

public partial class Fixes
{
    private Guid? teamLimitFixHookId;

    private void SetTeamLimitFixEnabled(bool enabled)
    {
        var isEnabled = teamLimitFixHookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            teamLimitFixHookId = Core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
            return;
        }

        Core.GameEvent.Unhook(teamLimitFixHookId!.Value);
        teamLimitFixHookId = null;
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        int maxPlayers = Core.Engine.GlobalVars.MaxClients;

        var gameRules = Core.EntitySystem.GetGameRules();
        if (gameRules != null && gameRules.IsValid)
        {
            gameRules.NumSpawnableTerrorist = maxPlayers;
            gameRules.MaxNumTerrorists = maxPlayers;
            
            gameRules.NumSpawnableCT = maxPlayers;
            gameRules.MaxNumCTs = maxPlayers;
        }

        return HookResult.Continue;
    }
}
