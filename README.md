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
| `sp_dm_upload_interval` | Seconds between periodic stats uploads for every mod (min 1) |
| `sp_developer` | Dump payloads / damage events |

`sp_send_stats` (server / RCON) forces an upload regardless of timer / round timing.

String FakeConVars (`sp_mod`, `sp_game_mode`, `sp_api_round_address`, `sp_server_id`, `sp_match_id`) must have **nothing after the closing quote** on the cfg line. A trailing `// comment` is stored as part of the value and breaks the POST URL.

Unknown `sp_mod` values fall back to matchmaking. `aim` is team scoring with respawn (`resetStateOnSpawn`); it is not FFA. Add a row in `CS2SP.Logic/ModProfiles.cs` for a new mode — handlers only read the flags.
