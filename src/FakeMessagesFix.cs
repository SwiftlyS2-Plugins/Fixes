using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Fixes;

public partial class Fixes
{
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
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            player.FixesData.InGame = IsPlayerPutInServer(player);
        }

        Core.Event.OnClientPutInServer += OnClientPutInServer;
        fakeMessagesFixHookId = Core.Command.HookClientChat(OnClientChat);
    }

    private void DisableFakeMessagesFix()
    {
        Core.Command.UnhookClientChat(fakeMessagesFixHookId!.Value);
        Core.Event.OnClientPutInServer -= OnClientPutInServer;
        fakeMessagesFixHookId = null;
    }

    private static bool IsPlayerPutInServer(IPlayer player)
    {
        var signonState = player.ServerSideClient.SignonState;

        return signonState >= SignonState_t.SIGNONSTATE_SPAWN;
    }

    public HookResult OnClientChat(int playerId, string text, bool teamonly)
    {
        if (playerId == -1) return HookResult.Continue;

        var player = Core.PlayerManager.GetPlayer(playerId);
        if (player == null || !player.FixesData.InGame) return HookResult.Stop;

        return HookResult.Continue;
    }

    public void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player != null) player.FixesData.InGame = true;
    }
}
