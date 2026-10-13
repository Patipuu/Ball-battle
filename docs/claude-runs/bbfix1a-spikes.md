# BB-FIX1a + SPIKE-CORNERS

Date 2026-10-13. Branch `dev/step3-view`, HEAD `ff7ab50` (BB-03, parent `e0d6935`). No commit, no push, no file deleted.
Preconditions checked first: HEAD `ff7ab50`; `git status` showed only the listed out-of-scope items; Unity closed.

## Files

| File | Change |
|---|---|
| `Sim/Arena/ArenaTuning.cs` | `SpikeWallDamage` 2f → `int 3`; new `SpikeCorners = TopLeft \| BottomRight`, `SpikeZoneNum/SpikeZoneDen = 1/5`. |
| `Sim/Arena/ArenaLayout.cs` | `SpikeCorner` flags enum (`All` = 4 corners), `ArenaWall`, `SpikeZones.Contains` (pure, integer fraction compared by cross-multiplying), `ArenaLayout.SpikeCorners`. The layout hash mixes the corners **only when `WallDamage > 0`**, so every other layout hash is unchanged. |
| `Sim/Arena/MatchSim.Obstacles.cs` | `ResolveWalls`: spike damage only if a wall the ball bounced off is touched inside a spike zone of the current rect; corner = one hit; at most one spike hit per ball per tick (`spikeTick[]`, not part of the state hash). Push-out, reflect-type damage (`DamageKind.Hazard`, `DealDamage(..., false)`: no shields, no hit hooks) and the `WallBounce` event (`Value` = spike damage, 0 outside a zone) are unchanged. |
| `Sim/MatchSim.cs` | Allocates `spikeTick` (-1). |
| `Sim/Arena/ArenaRegistry.cs` | spike-walls passes `ArenaTuning.SpikeCorners`. |
| `Sim/SimVersion.cs` | `Rules` 4 → **5**. |
| `View/ArenaFrameView.cs` | `ZoneSegments(corners, l, b, r, t)`; `spike_tooth` only along the zones, recomputed from the current rect each frame; walls keep the arena's normal colour (without tooth art: red strips over the zones only). |
| `View/Run/PrepareView.cs` | The arena preview draws the 1 px spike band only along the zones as well (consistency; not asked). |
| `View/Run/SaveStore.cs` | `SaveData.oldRunNotice`; `SaveMigration.Upgrade` (used by `FileSaveStore` and `MemorySaveStore`); `OldRunNotice.Pending/Dismiss`. |
| `View/MenuView.cs` | One-time notice page (box at (8,200) 254x64, OK 80x22); `SettingsClosed` event. |
| `View/GameController.cs` | Store kept as a field; shows the notice on the menu when pending; `SettingsAudio.Bind`. |
| `View/SettingsView.cs` | `Released` event (tap, or the end of a drag even off the track); `ReleasePointer()` test hook. |
| `View/Audio/AudioMath.cs` | `VolumeSaver` (writes only when values differ from the last save), `SettingsAudio.Bind`. |
| `View/Audio/AudioDirector.cs` | `Played` bounded to 256; `WallBounceCue` (spike_hit if the clip exists, else wall). |
| `View/Audio/AudioIds.cs` | `SpikeHit = "spike_hit"` (no clip added). |
| `View/Run/RunStrings.cs` | `OldRunEnded = "OLD RUN ENDED BY UPDATE"`, `Ok`. |
| `SimTests/SpikeCornerTests.cs` (new) | Spike rules + per-arena outcome digests. |
| `SimTests/ObstacleTests.cs` | `SpikeWallsDamageOnWallBounce` → `SpikeWallsDamageOnlyInSpikeZones` (the old test asserted the old rule: any wall hurts). |
| `SimTests/CrossPlatformGoldenTests.cs` | Expected values updated for SimVersion 5 (see Golden). |
| `Tests/EditMode/BbFix1aTests.cs` (new) | A, B, C and the view side of D. |
| `Tests/EditMode/ArtV1CardsBossArenaTests.cs` | Teeth count now = teeth along the zones (it asserted teeth along all four walls). |
| `Tests/EditMode/RunFlowTests.cs` | The full-run test also plays through a retry pick (see Tests). |

## A. v1 run discarded silently

Confirmed: `FileSaveStore.Load` turned a v1 save into v2 by setting `HasRun = false` and a fresh `RunState`, with no notice. Now `SaveMigration.Upgrade` still drops the run (it cannot be converted after the fixed-point change), but:
- compensation: k = `Run.FightIndex + 1` (clamped 1..8); if k > `BestFight`, `BestFight = k`. Not counted in `RunsPlayed`/`RunsWon`; achievements, unlocks and every other record unchanged. "Re-evaluate unlocks/goals": unlocks come only from achievements (unchanged), and `UnlockRules.NextGoal` reads `BestFight` on demand, so the next goal reflects the new value with no stored state to update (test: boss1 + run at fight 5 → WIN A RUN 4/8).
- notice: `oldRunNotice = true` only when a run was dropped. The menu shows "OLD RUN ENDED BY UPDATE" + OK once; OK clears the flag and saves (that save also persists the migrated data). Reloading the same save afterwards shows nothing.
- **Scope note:** this only helps saves still at v1. Saves already migrated to v2 (opened with build 5232d1c or later) have lost their run already and get no notice or compensation; accepted for this internal prototype.

## B. Played grows forever

Option chosen: **bounded log, always on**: `Played` keeps the last 256 entries (oldest dropped), so tests and debugging keep working without a hook. Test: 1000 plays → 256 entries, newest kept, order kept.

## C. Volume slider rewrites the save

Volume changes live while dragging; the save is written on pointer release and when Settings closes, each only if a value differs from the last saved one (`VolumeSaver`). Test (counting `MemorySaveStore`): 5 drag steps → 0 writes; release → 1; close without a change → 0 more; a change closed without release → 1 more.

## D. Spike corners

Rules as specified: TL + BR, 1/5 of the current wall length from the corner on both walls, recomputed every tick from the shrinking rect; 3 damage; corner touching both spiked walls = one hit / one event; at most one spike hit per ball per tick; contact point = ball centre projected on the wall it bounced off. `SpikeCorner.All` spikes all four corners. Audio: `spike_hit` if such a clip exists, else `wall` (never both); no clip added.

## Golden values

| Golden | Old | New | Why |
|---|---|---|---|
| `VersusGoldenTests` (classic, outcomes) | `0xEE6798E60B605BCF` | unchanged | — |
| `CrossPlatformGoldenTests` (classic only, `ComputeHash`) | 20 values `0xD2A88D3A96477721` … | 20 new values (table below) | `ComputeHash` mixes `SimVersion.Rules` (`MatchSim.Rules.cs:132`); the required 4 → 5 bump changes every value by itself. |
| `FixedMathTests` | `0xB938B2578807CC32` | unchanged | — |

**Proof that only the version, not the physics, changed for non-spike arenas:** with all BB-FIX1a code in place and `Rules` temporarily set back to 4, `CrossPlatformGoldenTests` passed with all 20 old v4 values, and `VersusGoldenTests` and the new per-arena digests passed (22/22 in that filtered run). Then `Rules` was set to 5 and only the CrossPlatform constants were replaced.

Per-arena outcome digests (`SpikeCornerTests.ArenaDigest`: winner, ticks and both final HPs; 8 weapons vs blade; seeds 1–2; independent of `SimVersion`), recorded from a `git archive` copy of `ff7ab50` and now asserted by `NonSpikeArenasPlayExactlyAsBefore`:

| Arena | ff7ab50 | now |
|---|---|---|
| classic | `0x6F2265EA3A4812DF` | same |
| pillar | `0x3C8F87B27D8DB9B5` | same |
| bumpers | `0x447293047E50C7E1` | same |
| **spike-walls** | `0xFCE4FB1B969DAAF0` | **`0x8B87E9F0305CA4ED`**: corner-only zones, 3 damage, 1 per tick (intended) |
| low-gravity | `0x26A829E997CF5D92` | same |
| tight | `0xA8649BD7FB44D43C` | same |
| wide | `0x56C07FC05082E5A8` | same |

CrossPlatform v5 values, seeds 1001–1020: `0xEEF73442912C5F45, 0x6C9CB1F3F47585BC, 0x0D583A4B652498D4, 0xE10BE86758682CAA, 0x615720C12CA952DD, 0x5CC4EF5B8454F3C7, 0x77D845F46A6EED88, 0xA5809087BE6321EE, 0x0939F4085273C6D9, 0xBCBB361DF229BEC7, 0x7539F734E141493E, 0xE4625F6355701BE0, 0xE1FDD36872D3E8AD, 0x0451B5F480D2F852, 0x39EED389C95D132D, 0xDC267EEC4A98C1E8, 0xDEAA56E5352B12FD, 0x49A9C47C5F4DFE72, 0x838AED19B6A6037F, 0x0F586DA5838887A4`.

There was no spike-walls golden before this task.

`SimVersion.Rules`: **4 → 5**.

## Tests

- SimTests: **185 pass, 0 fail, 0 skip** (178 before + 7 new in `SpikeCornerTests`; `dotnet test`, 12 min 21 s; `Explicit` tests not run, as before).
- EditMode (Unity 6000.5.1f1 batchmode, Editor closed): **228 total, 226 pass, 0 fail, 2 skip** (`CaptureScreens`, `CaptureBb01Screens`: they need BB_CAPTURE / BB01_CAPTURE). That is 218 at BB-03 plus 10 new in `BbFix1aTests`. No compiler warnings in the changed code. Results: `docs/claude-runs/bbfix1a-editmode.xml`.
- The first EditMode run had 1 failure: `RunFlowTests.FullRunEndsInSummaryAndClearsTheSavedRun`. Its loop only handled the Prepare state. With seed 7 the run used to win all 8 fights, but now loses a fight on the spike arena and goes to the free retry pick (StartPick), so the loop left early. The test now also plays through the retry pick; its intent (a full run ends on the Summary and clears the saved run) is unchanged. The second run overwrote that run's XML (a file I generated).

## Deviations

1. **Golden vs. version bump conflict.** The spec requires both "bump SimVersion.Rules once" and "every non-spike golden identical". `ComputeHash` includes the version, so `CrossPlatformGoldenTests` (classic arena only) cannot stay identical after the bump. I bumped and replaced its 20 constants, with the proof above (old values pass with Rules = 4). If you would rather keep the constants, the alternative is to leave `Rules` at 4, which breaks the bump rule instead.
2. **"No new float math in Sim".** The Sim is float-based (`Vec2`, `ArenaRect`; `Fix` is not used in this code yet). New numbers are integers or an integer fraction (3, 1/5, corner flags). The zone test cross-multiplies the existing float positions by those integers (`(Top - y) * 5 <= Height * 1`), so there is no division and no new float constant. The only float arithmetic is on values that were already float.
3. `SpikeWallDamage` changed type from `float` to `int` (the field it feeds, `ArenaLayout.WallDamage`, is still float).
4. `MemorySaveStore.Load` now runs the same migration as the file store, so the test path matches production.
5. The Prepare-screen arena preview was also changed to show spikes only in the zones (otherwise it would contradict the fight).
6. No-art fallback (no `spike_tooth` sprite): red strips over the zones instead of a fully red wall.
7. Two existing tests encoded the old rules and were updated (not just to go green): `ObstacleTests` (any wall bounce hurt) and `ArtV1CardsBossArenaTests` (teeth along all four walls). `RunFlowTests` was made robust to a retry (see Tests).
8. `NonSpikeArenasPlayExactlyAsBefore` is a new golden: the digests of the 6 non-spike arenas are pinned to their ff7ab50 values.

## Final git status

```
 M .claude/hooks/.logs/hook-log.jsonl
 M BallBattleUnity/Assets/Scripts/Sim/Arena/ArenaLayout.cs
 M BallBattleUnity/Assets/Scripts/Sim/Arena/ArenaRegistry.cs
 M BallBattleUnity/Assets/Scripts/Sim/Arena/ArenaTuning.cs
 M BallBattleUnity/Assets/Scripts/Sim/Arena/MatchSim.Obstacles.cs
 M BallBattleUnity/Assets/Scripts/Sim/MatchSim.cs
 M BallBattleUnity/Assets/Scripts/Sim/SimVersion.cs
 M BallBattleUnity/Assets/Scripts/View/ArenaFrameView.cs
 M BallBattleUnity/Assets/Scripts/View/Audio/AudioDirector.cs
 M BallBattleUnity/Assets/Scripts/View/Audio/AudioIds.cs
 M BallBattleUnity/Assets/Scripts/View/Audio/AudioMath.cs
 M BallBattleUnity/Assets/Scripts/View/GameController.cs
 M BallBattleUnity/Assets/Scripts/View/MenuView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/PrepareView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunStrings.cs
 M BallBattleUnity/Assets/Scripts/View/Run/SaveStore.cs
 M BallBattleUnity/Assets/Scripts/View/SettingsView.cs
 M BallBattleUnity/Assets/Tests/EditMode/ArtV1CardsBossArenaTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/RunFlowTests.cs
 M BallBattleUnity/SimTests/CrossPlatformGoldenTests.cs
 M BallBattleUnity/SimTests/ObstacleTests.cs
?? BallBattleUnity/Assets/Resources.meta
?? BallBattleUnity/Assets/Tests/EditMode/BbFix1aTests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbFix1aTests.cs.meta
?? BallBattleUnity/SimTests/SpikeCornerTests.cs
?? docs/claude-runs/bb01-02-editmode.xml
?? docs/claude-runs/bbfix1a-editmode.xml
?? docs/claude-runs/bbfix1a-spikes.md
?? docs/design/art/hud_retry_v2.png
?? docs/design/art/start_pick_v1.png
?? docs/design/boss-and-loss-rules.bak-20261011.md
?? docs/design/card-pool-v2.bak-20261011.md
?? docs/design/game-design-document.md
?? docs/design/weapon-bible.bak-20261011.md
?? plans/261013-core-loop-audio-team/
```

HEAD is still `ff7ab50`. The `bbfix1a-editmode.log` log sits next to this file (ignored by git).
