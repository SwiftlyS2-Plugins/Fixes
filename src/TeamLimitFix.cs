using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace Fixes;

public partial class Fixes
{
    [GameEventHandler(HookMode.Post)]
    private HookResult OnRoundStart(EventRoundStart @event)
    {
        if (!Config.CurrentValue.EnableTeamLimitFix) return HookResult.Continue;

        int maxPlayers = Core.Engine.GlobalVars.MaxClients;
        int limit = maxPlayers;

        var gameRules = Core.EntitySystem.GetGameRules();
        if (gameRules != null && gameRules.IsValid)
        {
            gameRules.NumSpawnableTerrorist = limit;
            gameRules.MaxNumTerrorists = limit;
            
            gameRules.NumSpawnableCT = limit;
            gameRules.MaxNumCTs = limit;
        }

        return HookResult.Continue;
    }
}