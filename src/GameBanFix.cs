using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;

namespace Fixes;

[StructLayout(LayoutKind.Sequential)]
public struct CGcBanInformation_t
{
    public uint Reason;
    public double Unknown;
    public double Expiration;
    public uint AccountId;
}

public partial class Fixes
{
    private delegate void CheckSteamBanDelegate();

    private IUnmanagedFunction<CheckSteamBanDelegate>? _CheckSteamBanDelegate;
    private nint addressGCBanInfo;
    private Guid? steamBanFixHookId;

    private void SetSteamBanFixEnabled(bool enabled)
    {
        var isEnabled = steamBanFixHookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            EnableSteamBanFix();
            return;
        }

        _CheckSteamBanDelegate!.RemoveHook(steamBanFixHookId!.Value);
        steamBanFixHookId = null;
    }

    private void EnableSteamBanFix()
    {
        if (_CheckSteamBanDelegate == null)
        {
            var checkSteamBanAddress = Core.GameData.GetSignature("CheckSteamBan");
            var gcBanInfoSignature = Core.GameData.GetSignature("CCSGameRules::m_mapGcBanInformation");

            _CheckSteamBanDelegate = Core.Memory.GetUnmanagedFunctionByAddress<CheckSteamBanDelegate>(checkSteamBanAddress);
            addressGCBanInfo = Core.Memory.ResolveXrefAddress(gcBanInfoSignature);
        }

        steamBanFixHookId = _CheckSteamBanDelegate.AddHook(next =>
        {
            unsafe
            {
                return () =>
                {
                    next()();

                    ref var gcBanInfoMap = ref Unsafe.AsRef<CUtlMap<uint, CGcBanInformation_t, uint>>((void*)addressGCBanInfo);

                    if (gcBanInfoMap.Count > 0)
                        gcBanInfoMap.RemoveAll();
                };
            }
        });
    }
}
