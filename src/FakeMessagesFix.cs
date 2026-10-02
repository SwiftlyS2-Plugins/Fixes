using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Fixes;

public partial class Fixes
{
    private static List<int> inGameClients = [];
    private static Lock _inGameClientsLock = new();
    private Guid? fakeMessagesFixHookId;

    private void SetFakeMessagesFixEnabled(bool enabled)
    {
        var isEnabled = fakeMessagesFixHookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            EnableFakeMessagesFix();
            return;
        }

        DisableFakeMessagesFix();
    }

    private void EnableFakeMessagesFix()
    {
        var inGamePlayerIds = GetInGamePlayerIds();

        lock (_inGameClientsLock)
        {
            inGameClients = inGamePlayerIds;
        }

        Core.Event.OnClientPutInServer += OnClientPutInServer;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
        fakeMessagesFixHookId = Core.Command.HookClientChat(OnClientChat);
    }

    private void DisableFakeMessagesFix()
    {
        Core.Command.UnhookClientChat(fakeMessagesFixHookId!.Value);
        Core.Event.OnClientPutInServer -= OnClientPutInServer;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        fakeMessagesFixHookId = null;

        lock (_inGameClientsLock)
        {
            inGameClients.Clear();
        }
    }

    private List<int> GetInGamePlayerIds()
    {
        var players = Core.PlayerManager.GetAllPlayers();
        var inGamePlayers = players.Where(IsPlayerPutInServer);
        var playerIds = inGamePlayers.Select(player => player.PlayerID);

        return playerIds.ToList();
    }

    private static bool IsPlayerPutInServer(IPlayer player)
    {
        var signonState = player.ServerSideClient.SignonState;

        return signonState >= SignonState_t.SIGNONSTATE_SPAWN;
    }

    public HookResult OnClientChat(int playerId, string text, bool teamonly)
    {
        if (playerId == -1) return HookResult.Continue;

        lock (_inGameClientsLock)
        {
            if (!inGameClients.Contains(playerId)) return HookResult.Stop;
        }

        return HookResult.Continue;
    }

    public void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        lock (_inGameClientsLock)
        {
            inGameClients.Add(@event.PlayerId);
        }
    }

    public void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        lock (_inGameClientsLock)
        {
            inGameClients.Remove(@event.PlayerId);
        }
    }
}