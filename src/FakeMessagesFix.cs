using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace Fixes;

public partial class Fixes
{
    private static List<int> inGameClients = [];
    private static Lock _inGameClientsLock = new();

    [ClientChatHookHandler]
    public HookResult OnClientChat(int playerId, string text, bool teamonly)
    {
        if (!Config.CurrentValue.EnableFakeMessagesFix) return HookResult.Continue;

        if (playerId == -1) return HookResult.Continue;

        lock (_inGameClientsLock)
        {
            if (!inGameClients.Contains(playerId)) return HookResult.Stop;
        }

        return HookResult.Continue;
    }

    [EventListener<EventDelegates.OnClientPutInServer>]
    public void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        lock (_inGameClientsLock)
        {
            inGameClients.Add(@event.PlayerId);
        }
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        lock (_inGameClientsLock)
        {
            inGameClients.Remove(@event.PlayerId);
        }
    }
}