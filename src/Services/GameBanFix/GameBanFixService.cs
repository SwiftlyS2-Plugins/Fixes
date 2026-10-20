using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;

namespace Fixes.Services.GameBanFix;

[StructLayout(LayoutKind.Sequential)]
public struct CGcBanInformation_t
{
    public uint Reason;
    public double Unknown;
    public double Expiration;
    public uint AccountId;
}

internal class GameBanFixService : IDisposable
{
    internal delegate void CheckSteamBanDelegate();

    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private Guid? _hookId;

    internal IUnmanagedFunction<CheckSteamBanDelegate>? CheckSteamBan { get; private set; }
    internal nint GcBanInfoAddress { get; private set; }

    public GameBanFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableSteamBanFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableSteamBanFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    internal void EnsureGcBanInfoResolved()
    {
        if (CheckSteamBan != null)
        {
            return;
        }

        var checkSteamBanAddress = _core.GameData.GetSignature("CheckSteamBan");
        var gcBanInfoSignature = _core.GameData.GetSignature("CCSGameRules::m_mapGcBanInformation");

        CheckSteamBan = _core.Memory.GetUnmanagedFunctionByAddress<CheckSteamBanDelegate>(checkSteamBanAddress);
        GcBanInfoAddress = _core.Memory.ResolveXrefAddress(gcBanInfoSignature);
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
            EnableGameBanFix();
            return;
        }

        CheckSteamBan!.RemoveHook(_hookId!.Value);
        _hookId = null;
    }

    private void EnableGameBanFix()
    {
        EnsureGcBanInfoResolved();

        _hookId = CheckSteamBan!.AddHook(next =>
        {
            unsafe
            {
                return () =>
                {
                    next()();

                    ref var gcBanInfoMap = ref Unsafe.AsRef<CUtlMap<uint, CGcBanInformation_t, uint>>((void*)GcBanInfoAddress);

                    if (gcBanInfoMap.Count > 0)
                        gcBanInfoMap.RemoveAll();
                };
            }
        });
    }
}
