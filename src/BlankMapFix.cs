using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

namespace Fixes;

public partial class Fixes
{
    private Guid? blankMapFixHookId;

    private void SetBlankMapFixEnabled(bool enabled)
    {
        var isEnabled = blankMapFixHookId.HasValue;
        if (enabled == isEnabled)
        {
            return;
        }

        if (enabled)
        {
            blankMapFixHookId = Core.GameEvent.HookPre<EventNextlevelChanged>(OnNextLevelChangedEvent);
            return;
        }

        Core.GameEvent.Unhook(blankMapFixHookId!.Value);
        blankMapFixHookId = null;
    }

    public HookResult OnNextLevelChangedEvent(EventNextlevelChanged @event)
    {
        if (@event.NextLevel == "")
        {
            var mapName = Core.Engine.GlobalVars.MapName;

            if (Core.Engine.IsMapValid(mapName))
            {
                Core.Engine.ExecuteCommand($"changelevel {mapName}");
            }
            else
            {
                Core.Engine.ExecuteCommand($"ds_workshop_changelevel {mapName}");
            }
        }

        return HookResult.Continue;
    }
}
