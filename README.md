<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>Fixes</strong></h2>
  <h3>Some game fixes.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/SwiftlyS2-Plugins/Fixes/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/SwiftlyS2-Plugins/Fixes?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/SwiftlyS2-Plugins/Fixes" alt="License">
</p>

## What this plugin does

CS2 carries a number of long-standing bugs, leftover restrictions and exploits
that mostly hurt community servers. This plugin collects the common workarounds
into one place, each one individually switchable.

## Features

| Fix | Setting | Default |
|---|---|---|
| Game bans spreading to others | `EnableSteamBanFix` | On |
| Competitive cooldown kicks | `EnableCompetitiveCooldownFix` | Off |
| Map entity crash | `EnableInputActivatorCrashFix` | On |
| Team size limit | `EnableTeamLimitFix` | On |
| Blank map | `EnableBlankMapFix` | On |
| Noclip after cheats are disabled | `EnableSvCheatsFix` | On |
| Voice chat | `EnableVoiceFix` | On |
| Fake player chat spam | `EnableFakeMessagesFix` | On |
| Jump macro abuse | `EnableJumpSpamFix` | On |
| Ramp bug | `EnableRampFix` | Off |
| Ghost players | `EnablePlayerGhostFix` | On |
| CSTV removed by bot quota | `EnableCstvBotQuotaFix` | Off |

## What each fix does

### Game bans spreading to others

When a banned player connects, CS2 can leak their record onto everybody else —
innocent players then get kicked as they join, for a ban that was never theirs.
This fix clears the leftover record so the ban stops with the account it
belongs to. The banned player is still kicked as normal.

### Competitive cooldown kicks

Players serving a temporary competitive cooldown get kicked on connect, even
though a community server has no reason to care about a matchmaking timeout.
This fix lets them play. Game bans and untrusted accounts are untouched and
still get kicked.

Off by default — turn it on if you are happy to host someone who picked up a
cooldown in official matchmaking.

### Map entity crash

Some maps contain a broken trigger that can take the whole server down
mid-round. This fix catches it and keeps the server running. Linux only.

### Team size limit

CS2 caps how many players fit on each team based on the spawn points the map
author placed. On a busy server that shows up as players being refused a team
or moved to the side they did not pick. This fix removes the cap.

Turn it off if your game mode relies on the map's own team sizes.

### Blank map

If a match ends with no next map set, the server can drop into an empty,
unplayable state that needs a manual restart. This fix restarts the current map
instead.

### Noclip after cheats are disabled

Anyone who switched into noclip while cheats were on keeps flying through walls
after cheats are turned back off. This fix puts them back on their feet.

### Voice chat

Some players cannot be heard at all, usually those with a Steam communication
restriction or anyone the listener once blocked on Steam. The game filters them
out before the server gets a say. This fix makes voice work the same for
everyone connected. In-game mutes still work as normal.

### Fake player chat spam

Advertising bots connect just long enough to post a message, without ever
joining the game or showing on the scoreboard. This fix only lets chat through
from players who actually joined.

### Jump macro abuse

A keybind trick lets some players register several jumps in a single instant,
giving them speed nobody playing normally can match. This fix limits everyone to
one jump at a time.

Turn it off on servers where that movement is the point, such as bhop servers.

### Ramp bug

The classic Source movement bug: running onto or landing on a slope stops you
dead or throws you sideways instead of letting you slide. Most noticeable on
surf, bhop and KZ maps. This fix makes ramps behave predictably.

Off by default because it changes how movement feels for everyone.

### Ghost players

A player can end up listed as a spectator while their body stays alive on the
map, letting them walk around and kill people who have no way to fight back.
This fix clears the leftover body at the start of each round, putting the
player properly into spectate.

### CSTV removed by bot quota

Automatic bot quota management (`bot_quota_mode = fill`) can select the in-game SourceTV client for
removal, interrupting demo recording and preventing later recordings from
starting normally. This fix excludes HLTV controllers only while the engine
selects a bot to remove, allowing it to continue to an ordinary bot.

Bot settings, explicit kicks and normal map/server shutdown remain unchanged.

## Configuration

Settings live in the plugin's `config.jsonc`:

```jsonc
{
  "Main": {
    "EnableSteamBanFix": true,
    "EnableCompetitiveCooldownFix": false,
    "EnableInputActivatorCrashFix": true,
    "EnableTeamLimitFix": true,
    "EnableBlankMapFix": true,
    "EnableSvCheatsFix": true,
    "EnableVoiceFix": true,
    "EnableFakeMessagesFix": true,
    "EnableJumpSpamFix": true,
    "EnableRampFix": false,
    "EnablePlayerGhostFix": true,
    "EnableCstvBotQuotaFix": false
  }
}
```

Changes apply as soon as you save the file — no map change or restart needed.

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.

## Acknowledgements

Thanks to CS2Fixes for providing the fixes for Game Bans, Competitive Cooldowns
and Input Activator Crash.

Thanks to [CS2VoiceFix](https://github.com/Source2ZE/CS2VoiceFix) for the voice chat fix.

Thanks to zer0.k for the original ramp-bug fix logic, and to Nukoooo for the ModSharp
port it was ported to SwiftlyS2 from ( https://github.com/Nukoooo/RampFix ).

Thanks to Shmitzas for AntiPlayerGhost, which the ghost player fix is based on.
