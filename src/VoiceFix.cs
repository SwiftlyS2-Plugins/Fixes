using System.Collections.Concurrent;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.NetMessages;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Fixes;

public partial class Fixes
{
    private ConcurrentDictionary<int, ulong> playerSeeds = [];
    private ulong GlobalSeed = 0;
    private bool enableVoiceFix = false;

    private void InitVoiceFix()
    {
        enableVoiceFix = Config.CurrentValue.EnableVoiceFix;
        Config.OnChange((v, _) =>
        {
            enableVoiceFix = v.EnableVoiceFix;
        });
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

    [EventListener<EventDelegates.OnClientConnected>]
    void ConnectListener(IOnClientConnectedEvent @event)
    {
        if (!enableVoiceFix)
            return;

        playerSeeds[@event.PlayerId] = GetSeed();
    }

    [EventListener<EventDelegates.OnMapLoad>]
    void MapLoadListener(IOnMapLoadEvent @event)
    {
        if (!enableVoiceFix)
            return;

        playerSeeds.Clear();
    }

    [ServerNetMessageInternalHandler]
    public HookResult OnVoiceDataSend(CSVCMsg_VoiceData msg, int playerid)
    {
        if (!enableVoiceFix)
            return HookResult.Continue;

        if (!playerSeeds.TryGetValue(playerid, out var seed))
        {
            seed = GetSeed();
            playerSeeds[playerid] = seed;
        }

        msg.Xuid = seed + (ulong)msg.Entity;
        return HookResult.Continue;
    }
}
