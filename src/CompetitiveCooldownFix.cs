using System.Runtime.CompilerServices;
using SwiftlyS2.Shared.Natives;

namespace Fixes;

public partial class Fixes
{
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

        foreach (var index in cooldownIndices)
        {
            gcBanInfoMap.RemoveAt(index);
        }
    }
}
