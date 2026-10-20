using System.Runtime.InteropServices;
using Fixes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Fixes.Services.CstvBotQuotaFix;

internal class CstvBotQuotaFixService : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte CstvSelectAndKickBotDelegate(int team);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte CstvTeamCandidateDelegate(nint filter, nint controller, nint pawn);

    [ThreadStatic]
    private static int cstvSelectionDepth;

    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private IUnmanagedFunction<CstvSelectAndKickBotDelegate>? cstvSelectAndKickBot;
    private IUnmanagedFunction<CstvTeamCandidateDelegate>? cstvTeamCandidate;
    private Guid? cstvSelectionHookId;
    private Guid? cstvCandidateHookId;
    private volatile bool cstvQuotaActive;
    private bool cstvMapEventsSubscribed;
    private int cstvReportedSkip;
    private long cstvNextErrorLogAt;

    public CstvBotQuotaFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableCstvBotQuotaFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableCstvBotQuotaFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
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
            // SelectAndKickBot: hook to limit HLTV filtering to bot removal on this thread.
            // TeamCandidate: hook to reject HLTV candidates and let selection continue.
            if (!_core.GameData.TryGetSignature("CstvBotQuotaFix::SelectAndKickBot", out var selectionAddress)
                || !_core.GameData.TryGetSignature("CstvBotQuotaFix::TeamCandidate", out var candidateAddress)
                || selectionAddress == 0 || candidateAddress == 0 || selectionAddress == candidateAddress)
            {
                throw new InvalidOperationException("CSTV quota gamedata is missing or invalid.");
            }

            cstvTeamCandidate = _core.Memory.GetUnmanagedFunctionByAddress<CstvTeamCandidateDelegate>(candidateAddress);
            var candidateHookId = cstvTeamCandidate.AddHook(next => (filter, controller, pawn) =>
            {
                try
                {
                    // The quota selector can mistake a pawn-less HLTV client for a bot.
                    // Reject it here so the iterator can continue looking for an ordinary bot.
                    if (cstvQuotaActive && cstvSelectionDepth > 0 && controller != 0
                        && _core.Memory.ToSchemaClass<CCSPlayerController>(controller).IsHLTV)
                    {
                        if (Interlocked.Exchange(ref cstvReportedSkip, 1) == 0)
                        {
                            _core.Logger.LogInformation("CSTV quota fix skipped HLTV during native bot removal (first occurrence this map).");
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

            cstvSelectAndKickBot = _core.Memory.GetUnmanagedFunctionByAddress<CstvSelectAndKickBotDelegate>(selectionAddress);
            var selectionHookId = cstvSelectAndKickBot.AddHook(next => team =>
            {
                // TeamCandidate is shared; limit its HLTV filter to bot selection on this thread.
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

            _core.Event.OnMapLoad += OnCstvQuotaMapLoad;
            _core.Event.OnMapUnload += OnCstvQuotaMapUnload;
            cstvMapEventsSubscribed = true;
            Interlocked.Exchange(ref cstvReportedSkip, 0);
            cstvQuotaActive = true;
            _core.Logger.LogInformation("CSTV quota fix enabled: selection={Selection:X}, candidate={Candidate:X}. Bot settings are unchanged.", selectionAddress, candidateAddress);
        }
        catch (Exception ex)
        {
            DisableCstvBotQuotaFix();
            _core.Logger.LogError(ex, "CSTV quota fix unavailable. Check the Fixes plugin gamedata; automatic bot removal may disconnect SourceTV.");
        }
    }

    private void DisableCstvBotQuotaFix()
    {
        cstvQuotaActive = false;
        if (cstvMapEventsSubscribed)
        {
            _core.Event.OnMapLoad -= OnCstvQuotaMapLoad;
            _core.Event.OnMapUnload -= OnCstvQuotaMapUnload;
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
            _core.Logger.LogError(ex, "CSTV quota fix callback failed; returning false (limited to once per 30 seconds).");
        }
    }
}
