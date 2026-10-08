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

        var cooldownKeys = new List<uint>();

        // Bounded by Count as insurance - this walks a native tree on the game thread.
        var remaining = gcBanInfoMap.Count;
        for (var i = gcBanInfoMap.FirstInOrdered();
             gcBanInfoMap.IsValidIndex(i) && remaining-- > 0;
             i = gcBanInfoMap.NextInOrdered(i))
        {
            if (Array.IndexOf(CompetitiveCooldownReasons, gcBanInfoMap[i].Reason) >= 0)
            {
                cooldownKeys.Add(gcBanInfoMap.Key(i));
            }
        }

        // Removing inside the walk above would invalidate the iteration.
        foreach (var key in cooldownKeys)
        {
            gcBanInfoMap.Remove(key);
        }
    }
}
