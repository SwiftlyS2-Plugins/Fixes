using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Fixes.Services.VoiceFix;

internal class VoiceFixService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private ulong GlobalSeed = 0;
    private bool enableVoiceFix = false;

    private Guid? voiceDataSendHookId;
    private Guid? clientVoiceHookId;

    public VoiceFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableVoiceFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableVoiceFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        if (enabled == enableVoiceFix)
        {
            return;
        }

        enableVoiceFix = enabled;

        if (enabled)
        {
            EnableVoiceFix();
            return;
        }

        DisableVoiceFix();
    }

    private void EnableVoiceFix()
    {
        _core.Event.OnClientConnected += ConnectListener;
        _core.Event.OnMapLoad += MapLoadListener;
        voiceDataSendHookId = _core.NetMessage.HookServerMessageInternal<CSVCMsg_VoiceData>(OnVoiceDataSend);
        clientVoiceHookId = _core.NetMessage.HookClientMessage<CCLCMsg_VoiceData>(OnClientVoice);
    }

    private void DisableVoiceFix()
    {
        _core.NetMessage.Unhook(voiceDataSendHookId!.Value);
        _core.NetMessage.Unhook(clientVoiceHookId!.Value);
        _core.Event.OnClientConnected -= ConnectListener;
        _core.Event.OnMapLoad -= MapLoadListener;
        voiceDataSendHookId = null;
        clientVoiceHookId = null;
        ResetVoiceState();
    }

    private void ResetVoiceState()
    {
        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            player.FixesData.VoiceSeed = 0;
        }
    }

    private ulong GetSeed()
    {
        if (GlobalSeed == 0)
        {
            var rng = new Random();
            byte[] buffer = new byte[8];
            rng.NextBytes(buffer);

            GlobalSeed = BitConverter.ToUInt64(buffer, 0);
        }

        GlobalSeed += 66;
        return GlobalSeed;
    }

    void ConnectListener(IOnClientConnectedEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null) return;

        player.FixesData.VoiceSeed = GetSeed();
    }

    void MapLoadListener(IOnMapLoadEvent @event) => ResetVoiceState();

    public HookResult OnVoiceDataSend(CSVCMsg_VoiceData msg, int playerid)
    {
        var player = _core.PlayerManager.GetPlayer(playerid);
        if (player == null) return HookResult.Continue;

        var data = player.FixesData;
        if (data.VoiceSeed == 0) data.VoiceSeed = GetSeed();

        msg.Xuid = data.VoiceSeed + (ulong)msg.Entity;
        return HookResult.Continue;
    }

    public HookResult OnClientVoice(CCLCMsg_VoiceData msg, int playerid)
    {
        if(!msg.Accessor.HasField("audio")) return HookResult.Stop;

        return HookResult.Continue;
    }
}
