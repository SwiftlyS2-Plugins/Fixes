using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Trace;

namespace Fixes;

// Every field participates in cache invalidation, including live no-block rules.
internal readonly record struct RampFixTracePolicy(ulong InteractsWith, ulong InteractsExclude,
    ulong InteractsAs, ushort HierarchyId, bool IgnoreTeammates, bool IgnoreEnemies, bool TeammatesAreEnemies)
{
    internal static bool TransparentPlayers(int? solidity) => solidity == 0;

    internal bool ShouldHitPlayer(int ownTeam, int otherTeam)
    {
        if (IgnoreTeammates && IgnoreEnemies) return false;
        if (ownTeam <= (int)Team.Spectator || otherTeam <= (int)Team.Spectator) return true;
        bool teammate = ownTeam == otherTeam && !TeammatesAreEnemies;
        return teammate ? !IgnoreTeammates : !IgnoreEnemies;
    }

    internal bool ShouldHitEntity(CCSPlayerPawn pawn, CEntityInstance? entity) =>
        entity is not CCSPlayerPawn other || (!(IgnoreTeammates && IgnoreEnemies)
            && ShouldHitPlayer(pawn.TeamNum, other.TeamNum));

    internal TraceParams Build(CCSPlayerPawn pawn)
    {
        var policy = this;
        var exclude = (MaskTrace)InteractsExclude;
        if (IgnoreTeammates && IgnoreEnemies) exclude |= MaskTrace.Player;
        return TraceParams.Builder()
            .WithIterateEntities(true)
            .WithCollisionGroup(CollisionGroup.PlayerMovement)
            .WithInteraction((MaskTrace)InteractsWith, exclude, (MaskTrace)InteractsAs)
            .WithHierarchyIds(HierarchyId)
            .IgnoreEntity(pawn)
            .WithShouldHitEntity(entity => policy.ShouldHitEntity(pawn, entity))
            .Build();
    }
}
