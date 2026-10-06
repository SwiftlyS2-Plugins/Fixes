using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.NetMessages;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Fixes;

public partial class Fixes
{
    private ulong GlobalSeed = 0;
    private bool enableVoiceFix = false;

    private Guid? voiceDataSendHookId;
    private Guid? clientVoiceHookId;

    private void SetVoiceFixEnabled(bool enabled)
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
        Core.Event.OnClientConnected += ConnectListener;
        Core.Event.OnMapLoad += MapLoadListener;
        voiceDataSendHookId = Core.NetMessage.HookServerMessageInternal<CSVCMsg_VoiceData>(OnVoiceDataSend);
        clientVoiceHookId = Core.NetMessage.HookClientMessage<CCLCMsg_VoiceData>(OnClientVoice);
    }

    private void DisableVoiceFix()
    {
        Core.NetMessage.Unhook(voiceDataSendHookId!.Value);
        Core.NetMessage.Unhook(clientVoiceHookId!.Value);
        Core.Event.OnClientConnected -= ConnectListener;
        Core.Event.OnMapLoad -= MapLoadListener;
        voiceDataSendHookId = null;
        clientVoiceHookId = null;
        ResetVoiceState();
    }

    private void ResetVoiceState()
    {
        foreach (var player in Core.PlayerManager.GetAllPlayers())
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
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null) return;

        player.FixesData.VoiceSeed = GetSeed();
    }

    void MapLoadListener(IOnMapLoadEvent @event) => ResetVoiceState();

    public HookResult OnVoiceDataSend(CSVCMsg_VoiceData msg, int playerid)
    {
        var player = Core.PlayerManager.GetPlayer(playerid);
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
