using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Fixes;

public partial class Fixes
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte CstvSelectAndKickBotDelegate(int team);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte CstvTeamCandidateDelegate(nint filter, nint controller, nint pawn);

    [ThreadStatic]
    private static int cstvSelectionDepth;
    private IUnmanagedFunction<CstvSelectAndKickBotDelegate>? cstvSelectAndKickBot;
    private IUnmanagedFunction<CstvTeamCandidateDelegate>? cstvTeamCandidate;
    private Guid? cstvSelectionHookId;
    private Guid? cstvCandidateHookId;
    private volatile bool cstvQuotaActive;
    private bool cstvMapEventsSubscribed;
    private int cstvReportedSkip;
    private long cstvNextErrorLogAt;

    private void SetCstvBotQuotaFixEnabled(bool enabled)
    {
        var isEnabled = cstvSelectionHookId.HasValue && cstvCandidateHookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            EnableCstvBotQuotaFix();
            return;
        }

        DisableCstvBotQuotaFix();
    }

    private void EnableCstvBotQuotaFix()
    {
        try
        {
            if (!Core.GameData.TryGetSignature("CstvBotQuotaFix::SelectAndKickBot", out var selectionAddress)
                || !Core.GameData.TryGetSignature("CstvBotQuotaFix::TeamCandidate", out var candidateAddress)
                || selectionAddress == 0 || candidateAddress == 0 || selectionAddress == candidateAddress)
            {
                throw new InvalidOperationException("CSTV quota gamedata is missing or invalid.");
            }

            cstvTeamCandidate = Core.Memory.GetUnmanagedFunctionByAddress<CstvTeamCandidateDelegate>(candidateAddress);
            var candidateHookId = cstvTeamCandidate.AddHook(next => (filter, controller, pawn) =>
            {
                try
                {
                    // The quota selector can choose a pawn-less HLTV client as a bot.
                    // This predicate is shared: only filter inside selection on this thread,
                    // so the engine continues looking for an ordinary bot to remove.
                    if (cstvQuotaActive && cstvSelectionDepth > 0 && controller != 0
                        && Core.Memory.ToSchemaClass<CCSPlayerController>(controller).IsHLTV)
                    {
                        if (Interlocked.Exchange(ref cstvReportedSkip, 1) == 0)
                        {
                            Core.Logger.LogInformation("CSTV quota fix skipped HLTV during native bot removal (first occurrence this map).");
                        }
                        return 0;
                    }
                    return next()(filter, controller, pawn);
                }
                catch (Exception ex)
                {
                    ReportCstvQuotaError(ex);
                    return 0;
                }
            });
            if (candidateHookId == Guid.Empty)
            {
                throw new InvalidOperationException("Could not hook the CSTV quota candidate predicate.");
            }
            cstvCandidateHookId = candidateHookId;

            cstvSelectAndKickBot = Core.Memory.GetUnmanagedFunctionByAddress<CstvSelectAndKickBotDelegate>(selectionAddress);
            var selectionHookId = cstvSelectAndKickBot.AddHook(next => team =>
            {
                ++cstvSelectionDepth;
                try
                {
                    return next()(team);
                }
                catch (Exception ex)
                {
                    ReportCstvQuotaError(ex);
                    return 0; // Do not claim that a bot was removed.
                }
                finally
                {
                    --cstvSelectionDepth;
                }
            });
            if (selectionHookId == Guid.Empty)
            {
                throw new InvalidOperationException("Could not hook native bot selection.");
            }
            cstvSelectionHookId = selectionHookId;

            Core.Event.OnMapLoad += OnCstvQuotaMapLoad;
            Core.Event.OnMapUnload += OnCstvQuotaMapUnload;
            cstvMapEventsSubscribed = true;
            Interlocked.Exchange(ref cstvReportedSkip, 0);
            cstvQuotaActive = true;
            Core.Logger.LogInformation("CSTV quota fix enabled: selection={Selection:X}, candidate={Candidate:X}. Bot settings are unchanged.", selectionAddress, candidateAddress);
        }
        catch (Exception ex)
        {
            DisableCstvBotQuotaFix();
            Core.Logger.LogError(ex, "CSTV quota fix unavailable. Check the Fixes plugin gamedata; automatic bot removal may disconnect SourceTV.");
        }
    }

    private void DisableCstvBotQuotaFix()
    {
        cstvQuotaActive = false;
        if (cstvMapEventsSubscribed)
        {
            Core.Event.OnMapLoad -= OnCstvQuotaMapLoad;
            Core.Event.OnMapUnload -= OnCstvQuotaMapUnload;
            cstvMapEventsSubscribed = false;
        }

        // Remove selection scope first. Also rolls back a partially installed pair.
        if (cstvSelectionHookId.HasValue)
        {
            cstvSelectAndKickBot!.RemoveHook(cstvSelectionHookId.Value);
            cstvSelectionHookId = null;
        }
        if (cstvCandidateHookId.HasValue)
        {
            cstvTeamCandidate!.RemoveHook(cstvCandidateHookId.Value);
            cstvCandidateHookId = null;
        }
        cstvSelectAndKickBot = null;
        cstvTeamCandidate = null;
    }

    private void OnCstvQuotaMapLoad(IOnMapLoadEvent @event)
    {
        Interlocked.Exchange(ref cstvReportedSkip, 0);
        cstvQuotaActive = true;
    }

    private void OnCstvQuotaMapUnload(IOnMapUnloadEvent @event)
    {
        cstvQuotaActive = false;
    }

    private void ReportCstvQuotaError(Exception ex)
    {
        // Do not propagate managed exceptions through a reverse native callback.
        var now = Environment.TickCount64;
        var next = Interlocked.Read(ref cstvNextErrorLogAt);
        if (now >= next && Interlocked.CompareExchange(ref cstvNextErrorLogAt, now + 30_000, next) == next)
        {
            Core.Logger.LogError(ex, "CSTV quota fix callback failed; returning false (limited to once per 30 seconds).");
        }
    }
}
