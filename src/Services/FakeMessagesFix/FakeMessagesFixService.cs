using Fixes.Models;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Fixes.Services.FakeMessagesFix;

internal class FakeMessagesFixService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly IDisposable? _configChangeSubscription;
    private Guid? _hookId;

    public FakeMessagesFixService(ISwiftlyCore core, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        SetEnabled(config.CurrentValue.EnableFakeMessagesFix);
        _configChangeSubscription = config.OnChange(cfg => _core.Scheduler.NextTick(() => SetEnabled(cfg.EnableFakeMessagesFix)));
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        var isEnabled = _hookId.HasValue;
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
        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            player.FixesData.InGame = IsPlayerPutInServer(player);
        }

        _core.Event.OnClientPutInServer += OnClientPutInServer;
        _hookId = _core.Command.HookClientChat(OnClientChat);
    }

    private void DisableFakeMessagesFix()
    {
        _core.Command.UnhookClientChat(_hookId!.Value);
        _core.Event.OnClientPutInServer -= OnClientPutInServer;
        _hookId = null;
    }

    private static bool IsPlayerPutInServer(IPlayer player)
    {
        var signonState = player.ServerSideClient.SignonState;

        return signonState >= SignonState_t.SIGNONSTATE_SPAWN;
    }

    public HookResult OnClientChat(int playerId, string text, bool teamonly)
    {
        if (playerId == -1) return HookResult.Continue;

        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player == null || !player.FixesData.InGame) return HookResult.Stop;

        return HookResult.Continue;
    }

    public void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player != null) player.FixesData.InGame = true;
    }
}
