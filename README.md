# CS2StatsPlugin Sharp

PlayCup CS2 stats plugin for [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp). C# port of the Metamod [CS2StatsPlugin](https://github.com/lucianene/cs2sp) (`cs2sp`): player stats POSTed to `/v1/mod-stats/` on a timer and on `round_end`.

Do **not** load this together with C++ CS2SP (`addons/metamod/cs2sp.vdf`). Both plugins would POST the same payload.

The `sp_*` cvars are unchanged, so existing `server.cfg` lines (`cs2dm`, `cs2aim`, `cs2zm`, `cs2casual`) keep working.

## Layout

| Project | Role |
|---|---|
| `CS2SP` | CounterStrikeSharp plugin (`CS2SP.dll`) |
| `CS2SP.Logic` | Engine-free rules (mod profiles, damage clamp, payload, upload URL) |
| `CS2SP.Tests` | xUnit tests for `CS2SP.Logic` |

Requires **.NET 8** and `CounterStrikeSharp.API` 1.0.355.

## Build

```bash
dotnet test CS2SP.Tests/CS2SP.Tests.csproj -c Release
dotnet publish CS2SP/CS2SP.csproj -c Release -o publish
```

Host boxes often have no `dotnet`. Prefer Docker: `make test` / `make compile`.

CI publishes a `latest` prerelease on every `main` push, and a versioned release for `vX.Y.Z` tags. Extract `cs2spsharp-*-linux.tar.gz` into `game/csgo/`.

Install next to CounterStrikeSharp:

```
game/csgo/addons/counterstrikesharp/plugins/CS2SP/CS2SP.dll
game/csgo/addons/counterstrikesharp/plugins/CS2SP/CS2SP.Logic.dll
```

`make setup` / `make plugins` on **cs2aim**, **cs2dm**, **cs2zm**, and **cs2casual** fetch the GitHub `latest` prerelease into that mod's `addons`. **cs2mm** does not — stats are built into CS2MM.

## Runtime

| Cvar | Role |
|---|---|
| `sp_enabled` | Master switch |
| `sp_mod` | Scoring profile (`matchmaking`, `deathmatch`, `aim`, …). Uploads are not gated on this. |
| `sp_game_mode` | Free-text label in the query string |
| `sp_api_round_address` | Full POST URL |
| `sp_server_id` | `X-Server-Id` header (game_servers container name) |
| `sp_match_id` | Query `match_id` |
| `sp_dm_upload_interval` | Seconds between periodic stats uploads (default **30**, unchanged). Round / connect / leave POSTs reset this timer so they do not stack. The presence 1s/5s window is only for join coalescing. |
| `sp_developer` | Dump payloads / damage events |

`sp_send_stats` (server / RCON) forces an upload regardless of timer / round timing.

Joins, `player_connect_full`, `round_announce_warmup`, and the GameStart `round_end` (warmup commencing) share a **presence** POST (`match.reason=connect`, 1s settle, 5s min interval). That is what refreshes the web live list and “Playing DM / Aim / …” activity during warmup, before the first scored round. The API skips mission credit on `connect`. Connect payloads are the same event-total object as every other POST.

Warmup is not scored. Kills/damage/utility during `WarmupPeriod` are ignored, and live totals are wiped on `warmup_end` / GameStart so they cannot stack into the match row. Periodic and round POSTs wait until warmup ends. Leave POSTs still run (live list).

A player who leaves is POSTed once (`match.reason=disconnect`, `left: true`) so mid-round kills land, then omitted from later heartbeats. The API stamps `left_at` so they drop off the live list immediately. Map-end still flushes remaining parked rows.

String FakeConVars (`sp_mod`, `sp_game_mode`, `sp_api_round_address`, `sp_server_id`, `sp_match_id`) must have **nothing after the closing quote** on the cfg line. A trailing `// comment` is stored as part of the value and breaks the POST URL.

Unknown `sp_mod` values fall back to matchmaking. PlayCup `sp_mod` values: `deathmatch`, `aim`, `casual`, `zombie`. `aim` is team scoring with respawn (`resetStateOnSpawn`); it is not FFA. Add a row in `CS2SP.Logic/ModProfiles.cs` for a new mode — handlers only read the flags.

## Stats captured

Core scoreboard (event totals, never shrunk on disconnect): K/D/A, damage, hits, HS%, ADR, MVPs, team kills. Money is not read from the engine.

Kill flavor (`player_death`): first/clutch, 2/3/4/5k, pistol/rifle/smg/shotgun/sniper/knife/zeus/HE/molotov/bomb, noscope, wallbang, through-smoke, air, blind, unique victims, chicken, longest distance (m), dominations, revenges, team wipes.

Utility: flash/HE/smoke/molotov/decoy thrown, flash assists, enemies flashed, utility/fire damage, dinks. Casual hostage maps: rescues.

Accuracy: event hits vs shots when those counters are present. Engine `ActionTrackingServices` / `InGameMoneyServices` are never read — they are raw heap pointers, and `Schema.GetRef` `AccessViolationException` aborts srcds (uncaught in .NET 8) on sign-on, kick, and round reset leftovers.

New keys need a `mod_player_stats` column + `ModPlayerStats` fillable. Do **not** add them to `match_player_stats` unless CS2MM also sends them.
