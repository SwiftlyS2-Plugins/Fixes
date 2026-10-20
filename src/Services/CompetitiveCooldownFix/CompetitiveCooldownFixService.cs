using System.Runtime.CompilerServices;
using Fixes.Models;
using Fixes.Services.GameBanFix;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;

namespace Fixes.Services.CompetitiveCooldownFix;

internal class CompetitiveCooldownFixService : IDisposable
{
    private static readonly uint[] CompetitiveCooldownReasons = [20, 22, 23];

    private readonly GameBanFixService _gameBanFixService;
    private readonly IDisposable? _configChangeSubscription;
    private Guid? _hookId;

    public CompetitiveCooldownFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config, GameBanFixService gameBanFixService)
    {
        _gameBanFixService = gameBanFixService;
        SetEnabled(config.CurrentValue.EnableCompetitiveCooldownFix);
        _configChangeSubscription = config.OnChange(cfg => core.Scheduler.NextTick(() => SetEnabled(cfg.EnableCompetitiveCooldownFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        var isEnabled = _hookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            EnableCompetitiveCooldownFix();
            return;
        }

        _gameBanFixService.CheckSteamBan!.RemoveHook(_hookId!.Value);
        _hookId = null;
    }

    private void EnableCompetitiveCooldownFix()
    {
        _gameBanFixService.EnsureGcBanInfoResolved();

        _hookId = _gameBanFixService.CheckSteamBan!.AddHook(next =>
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
        ref var gcBanInfoMap = ref Unsafe.AsRef<CUtlMap<uint, CGcBanInformation_t, uint>>((void*)_gameBanFixService.GcBanInfoAddress);

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
