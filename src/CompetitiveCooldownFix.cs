using System.Runtime.CompilerServices;
using SwiftlyS2.Shared.Natives;

namespace Fixes;

public partial class Fixes
{
    // GC penalty reasons that mean "competitive cooldown", taken from CS2Fixes' CheckSteamBan
    // detour. These are not the ENetworkDisconnectionReason kick codes.
    private static readonly uint[] CompetitiveCooldownReasons = [20, 22, 23];

    private Guid? competitiveCooldownFixHookId;

    private void SetCompetitiveCooldownFixEnabled(bool enabled)
    {
        var isEnabled = competitiveCooldownFixHookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            EnableCompetitiveCooldownFix();
            return;
        }

        _CheckSteamBanDelegate!.RemoveHook(competitiveCooldownFixHookId!.Value);
        competitiveCooldownFixHookId = null;
    }

    private void EnableCompetitiveCooldownFix()
    {
        EnsureGcBanInfoResolved();

        competitiveCooldownFixHookId = _CheckSteamBanDelegate!.AddHook(next =>
        {
            unsafe
            {
                return () =>
                {
                    // Must run before the original: that is what kicks, so clearing afterwards
                    // would only tidy up after a player who has already been dropped.
                    ClearCompetitiveCooldowns();
                    next()();
                };
            }
        });
    }

    private unsafe void ClearCompetitiveCooldowns()
    {
        ref var gcBanInfoMap = ref Unsafe.AsRef<CUtlMap<uint, CGcBanInformation_t, uint>>((void*)addressGCBanInfo);

        if (gcBanInfoMap.Count == 0)
        {
            return;
        }

        var cooldownIndices = new List<uint>();

        // Bounded by Count as insurance - this walks a native tree on the game thread.
        var remaining = gcBanInfoMap.Count;
        for (var i = gcBanInfoMap.FirstInOrdered();
             gcBanInfoMap.IsValidIndex(i) && remaining-- > 0;
             i = gcBanInfoMap.NextInOrdered(i))
        {
            if (Array.IndexOf(CompetitiveCooldownReasons, gcBanInfoMap[i].Reason) >= 0)
            {
                cooldownIndices.Add(i);
            }
        }

        // Indices, not keys: Remove(key) goes through CUtlRBTree.Find, which calls the tree's
        // LFunc - a pointer SwiftlyS2 sets only on trees it allocated itself, so on this
        // game-owned map it is a wild call that crashes the server. RemoveAt is link arithmetic.
        // Indices survive removals, but freeing a node breaks NextInOrdered, hence the two passes.
        foreach (var index in cooldownIndices)
        {
            gcBanInfoMap.RemoveAt(index);
        }
    }
}
