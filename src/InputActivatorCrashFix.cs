using System.Runtime.InteropServices;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;

namespace Fixes;

[StructLayout(LayoutKind.Sequential)]
public struct InputData_t
{
    public nint Activator;
    public nint Caller;
    public CVariant<CVariantDefaultAllocator> Value;
    public int OutputID;
}

public partial class Fixes
{
    private unsafe delegate nint CBaseFilter_InputTestActivatorDelegateLinux(nint pEntity, InputData_t* inputData);

    private IUnmanagedFunction<CBaseFilter_InputTestActivatorDelegateLinux>? _CBaseFilter_InputTestActivatorDelegateLinux;
    private Guid? inputActivatorCrashFixHookId;

    private void SetInputActivatorCrashFixEnabled(bool enabled)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var isEnabled = inputActivatorCrashFixHookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            EnableInputActivatorCrashFix();
            return;
        }

        _CBaseFilter_InputTestActivatorDelegateLinux!.RemoveHook(inputActivatorCrashFixHookId!.Value);
        inputActivatorCrashFixHookId = null;
    }

    private void EnableInputActivatorCrashFix()
    {
        if (_CBaseFilter_InputTestActivatorDelegateLinux == null)
        {
            var inputTestActivatorAddress = Core.GameData.GetSignature("CBaseFilter::InputTestActivator");

            _CBaseFilter_InputTestActivatorDelegateLinux = Core.Memory.GetUnmanagedFunctionByAddress<CBaseFilter_InputTestActivatorDelegateLinux>(inputTestActivatorAddress);
        }

        inputActivatorCrashFixHookId = _CBaseFilter_InputTestActivatorDelegateLinux.AddHook(next =>
        {
            unsafe
            {
                return (pEntity, inputData) =>
                {
                    if (inputData->Activator == 0) return 0;

                    return next()(pEntity, inputData);
                };
            }
        });
    }
}
