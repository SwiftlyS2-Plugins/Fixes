using System.Runtime.InteropServices;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;

namespace Fixes;

[StructLayout(LayoutKind.Sequential)]
public struct InputData_t
{
    public nint Activator;
    public nint Caller;
    public CVariant Value;
    public int OutputID;
}

public partial class Fixes
{
    private unsafe delegate void CBaseFilter_InputTestActivatorDelegate(nint pEntity, InputData_t* inputData);

    private IUnmanagedFunction<CBaseFilter_InputTestActivatorDelegate>? _CBaseFilter_InputTestActivatorDelegate;

    public void InitInputActivatorCrashFix()
    {
        _CBaseFilter_InputTestActivatorDelegate = Core.Memory.GetUnmanagedFunctionByAddress<CBaseFilter_InputTestActivatorDelegate>(
            Core.GameData.GetSignature("CBaseFilter::InputTestActivator")
        );

        _CBaseFilter_InputTestActivatorDelegate.AddHook(next =>
        {
            unsafe
            {
                return (pEntity, inputData) =>
                {
                    if (Config.CurrentValue.EnableInputActivatorCrashFix)
                    {
                        if (inputData->Activator == 0) return;
                    }

                    next()(pEntity, inputData);
                };
            }
        });
    }

}