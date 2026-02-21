using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Fixes;

[PluginMetadata(Id = "Fixes", Version = "1.0.4", Name = "Fixes", Author = "Swiftly Development Team", Description = "No description.")]
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
public partial class Fixes(ISwiftlyCore core) : BasePlugin(core)
#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
{
  public static IOptionsMonitor<FixesConfig> Config { get; private set; } = null!;

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void Load(bool hotReload)
  {
    Core.Configuration
        .InitializeJsonWithModel<FixesConfig>("config.jsonc", "Main")
        .Configure(builder =>
        {
          builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
        });

    ServiceCollection services = new();
    services.AddSwiftly(Core)
        .AddOptions<FixesConfig>()
        .BindConfiguration("Main");

    var provider = services.BuildServiceProvider();
    Config = provider.GetRequiredService<IOptionsMonitor<FixesConfig>>();

    InitGameBanFixes();
    InitInputActivatorCrashFix();
  }

  public override void Unload()
  {
  }
}