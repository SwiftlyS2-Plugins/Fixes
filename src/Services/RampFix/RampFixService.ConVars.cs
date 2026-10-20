using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;

namespace Fixes.Services.RampFix;

internal partial class RampFixService
{
    private IConVar<int>? rampFixSolidTeammates;
    private IConVar<int>? rampFixSolidEnemies;
    private IConVar<bool>? rampFixTeammatesAreEnemies;
    private int? rampFixSolidTeammatesValue;
    private int? rampFixSolidEnemiesValue;
    private bool rampFixTeammatesAreEnemiesValue;
    private bool rampFixConVarCacheSubscribed;

    private void StartRampFixConVarCache()
    {
        if (rampFixConVarCacheSubscribed) return;
        rampFixSolidTeammates = _core.ConVar.Find<int>("mp_solid_teammates");
        rampFixSolidEnemies = _core.ConVar.Find<int>("mp_solid_enemies");
        rampFixTeammatesAreEnemies = _core.ConVar.Find<bool>("mp_teammates_are_enemies");
        _core.Event.OnConVarValueChanged += OnRampFixConVarValueChanged;
        rampFixConVarCacheSubscribed = true;
        rampFixSolidTeammatesValue = rampFixSolidTeammates?.Value;
        rampFixSolidEnemiesValue = rampFixSolidEnemies?.Value;
        rampFixTeammatesAreEnemiesValue = rampFixTeammatesAreEnemies?.Value ?? false;
    }

    private void OnRampFixConVarValueChanged(IOnConVarValueChanged @event)
    {
        // Re-read only the changed typed convar, outside the movement hot path.
        // This also avoids parsing string payloads or changing missing-convar policy.
        switch (@event.ConVarName)
        {
            case "mp_solid_teammates":
                rampFixSolidTeammatesValue = rampFixSolidTeammates?.Value;
                break;
            case "mp_solid_enemies":
                rampFixSolidEnemiesValue = rampFixSolidEnemies?.Value;
                break;
            case "mp_teammates_are_enemies":
                rampFixTeammatesAreEnemiesValue = rampFixTeammatesAreEnemies?.Value ?? false;
                break;
        }
    }

    private void StopRampFixConVarCache()
    {
        if (!rampFixConVarCacheSubscribed) return;
        _core.Event.OnConVarValueChanged -= OnRampFixConVarValueChanged;
        rampFixConVarCacheSubscribed = false;
        rampFixSolidTeammates = null;
        rampFixSolidEnemies = null;
        rampFixTeammatesAreEnemies = null;
        rampFixSolidTeammatesValue = null;
        rampFixSolidEnemiesValue = null;
        rampFixTeammatesAreEnemiesValue = false;
    }
}
