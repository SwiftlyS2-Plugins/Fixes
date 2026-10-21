using Fixes.Models;
using Fixes.Services.BlankMapFix;
using Fixes.Services.CompetitiveCooldownFix;
using Fixes.Services.CstvBotQuotaFix;
using Fixes.Services.FakeMessagesFix;
using Fixes.Services.GameBanFix;
using Fixes.Services.InputActivatorCrashFix;
using Fixes.Services.JumpSpamFix;
using Fixes.Services.PlayerGhostFix;
using Fixes.Services.RampFix;
using Fixes.Services.SlowAnimationFix;
using Fixes.Services.SvCheatsFix;
using Fixes.Services.TeamLimitFix;
using Fixes.Services.VoiceFix;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace Fixes;

[PluginMetadata(Id = "Fixes", Version = "1.1.0", Name = "Fixes", Author = "Swiftly Development Team", Description = "No description.")]
public class Fixes : BasePlugin
{
    private ServiceProvider? _serviceProvider;

    public Fixes(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<Configuration>("config.jsonc", "Main")
            .Configure(builder =>
            {
                builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
            });

        ServiceCollection services = new();

        services.AddSwiftly(Core)
            .AddSingleton<BlankMapFixService>()
            .AddSingleton<CompetitiveCooldownFixService>()
            .AddSingleton<CstvBotQuotaFixService>()
            .AddSingleton<FakeMessagesFixService>()
            .AddSingleton<GameBanFixService>()
            .AddSingleton<InputActivatorCrashFixService>()
            .AddSingleton<JumpSpamFixService>()
            .AddSingleton<PlayerGhostFixService>()
            .AddSingleton<RampFixService>()
            .AddSingleton<SlowAnimationFixService>()
            .AddSingleton<SvCheatsFixService>()
            .AddSingleton<TeamLimitFixService>()
            .AddSingleton<VoiceFixService>()
            .AddOptions<Configuration>()
            .BindConfiguration("Main");

        _serviceProvider = services.BuildServiceProvider();

        _ = _serviceProvider.GetRequiredService<BlankMapFixService>();
        _ = _serviceProvider.GetRequiredService<CompetitiveCooldownFixService>();
        _ = _serviceProvider.GetRequiredService<CstvBotQuotaFixService>();
        _ = _serviceProvider.GetRequiredService<FakeMessagesFixService>();
        _ = _serviceProvider.GetRequiredService<GameBanFixService>();
        _ = _serviceProvider.GetRequiredService<InputActivatorCrashFixService>();
        _ = _serviceProvider.GetRequiredService<JumpSpamFixService>();
        _ = _serviceProvider.GetRequiredService<PlayerGhostFixService>();
        _ = _serviceProvider.GetRequiredService<RampFixService>();
        _ = _serviceProvider.GetRequiredService<SlowAnimationFixService>();
        _ = _serviceProvider.GetRequiredService<SvCheatsFixService>();
        _ = _serviceProvider.GetRequiredService<TeamLimitFixService>();
        _ = _serviceProvider.GetRequiredService<VoiceFixService>();
    }

    public override void Unload()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }
}
