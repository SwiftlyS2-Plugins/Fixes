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