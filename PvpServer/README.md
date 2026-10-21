# Ball Battle ranked PvP beta

The self-hosted service stores human player profiles, returns three opponent offers, freezes a defender's roster and blind bans, runs the deterministic best-of-three simulation, and settles the active challenger's MMR exactly once. An unattended defender's MMR is unchanged. Defense wins and losses have their own counters.

## Run locally

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet run -c Release --project PvpServer/PvpServer.csproj --urls http://127.0.0.1:5088
```

The Unity client reads `Assets/Resources/pvp-server.txt` unless `PlayerPrefs` key `pvp_server_url` overrides it. Its default is the local service above. Four distinct player accounts are needed for a three-opponent offer. For a public host, set the resource to its HTTPS base URL before building Unity; put the service behind an HTTPS reverse proxy. Public HTTP URLs are rejected by the client.

`PVP_DATA_PATH` overrides the JSON data file path; otherwise it is `PvpServer/pvp-data.json`. Back up this file and keep it private. The service persists with an atomic temp-file rename under a process lock. Run a single service process for this beta; multi-instance hosting needs a transactional database.

House bots: on start the service creates (or refreshes) 40 bot defenses, `bot-01`..`bot-40`, four per ball level 1-10, named `BOT ...` (`Sim/Pvp/PvpBots.cs`). Offers always prefer real players; bots only fill slots that nearby real players cannot, their MMR never moves, and the 24 h rematch cooldown applies to them too. Players cannot take a name starting with `BOT `. Set `PVP_BOTS=0` to stop offering them (existing records stay).

## API

`POST /v1/accounts` creates an account and returns a one-time bearer token. Send it as `Authorization: Bearer <token>` for all `/v1` requests after registration. Unity stores it locally under `pvp_token`.

| Route | Action |
| --- | --- |
| `GET /v1/me` | Profile and MMR |
| `PUT /v1/me/name` | Change display name |
| `PUT /v1/me/defense` | Publish progress snapshots, three ordered defense balls, and blind bans |
| `GET /v1/offers` | Three real, eligible opponents or an empty set. The same three come back for 10 minutes (`refreshSeconds`), so leaving the page cannot reroll easy opponents |
| `POST /v1/challenges` | Select one offered opponent; freezes both profiles |
| `POST /v1/challenges/{id}/ban` | Lock the challenger's blind bans, then reveal both |
| `POST /v1/challenges/{id}/pick` | Pick a legal ball, simulate, settle once, return replay data |
| `GET /v1/challenges/{id}` | Recover the current challenge state (`ban`, `pick`, `done` or `expired`) |
| `GET /v1/history` | Last 20 settled challenges, with opponent name |

## Timeouts

A challenge lives 15 minutes. If it runs out before the challenger locks bans, it becomes `expired` with no MMR change (nothing was revealed). If it runs out after the reveal, the server picks the challenger's highest-level legal ball and settles the series as usual (`autoPicked: true`), so walking away from a bad matchup is not a free dodge. Expiry is applied lazily on the challenger's next request; no background worker is needed.

Run `dotnet test BallBattleUnity/SimTests/SimTests.csproj -c Release --filter "FullyQualifiedName~Pvp"` for the rules and persistence tests. `powershell -NoProfile -ExecutionPolicy Bypass -File PvpServer/Tests/run-smoke.ps1` launches an isolated local server and exercises the HTTP flow.

## Deploy

Public hosting (Oracle Cloud Always Free VM + Caddy HTTPS) and building the itch.io WebGL zip and the Android APK are scripted in `deploy/`; the step-by-step guide is `deploy/DEPLOY-ORACLE.md`. The service allows browser calls from itch.io's game hosts (`*.itch.zone`, `v6p9d9t4.ssl.hwcdn.net`), loopback, and any origin listed in `PVP_CORS_ORIGINS` (comma separated), and rate-limits each IP (120 requests/minute, 20 new accounts/hour).

## Before public ranked launch

This beta imports ball level and tree data from each Unity local save. The service checks legal bounds and tree allocation but cannot prove that a local save earned those upgrades. Public competitive MMR requires server-owned progression or validated signed Run replays. Anonymous bearer accounts also need recoverable sign-in and abuse controls. A public HTTPS host and a two-device Unity playtest are still required.
