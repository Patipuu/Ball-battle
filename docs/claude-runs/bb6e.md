# BB-6e: WARDEN no floor spikes, MAGNET retune, the four pairs over 80% (Sim, stays Rules 9)

Base: HEAD `4bb5061` + the uncommitted BB-6d + Task 4 work tree (Rules 9). Nothing committed, pushed, stashed or deleted.

## 6d backup (taken before any edit)

| item | value |
|---|---|
| patch | `E:\ball-battle-6d-out\bb6d-wip.patch`, **3,229,139 bytes** (`git diff --binary HEAD`) |
| untracked list | `E:\ball-battle-6d-out\bb6d-untracked.txt`, **39 files** |
| untracked copies | all 39 copied with relative paths to `E:\ball-battle-6d-out\bb6d-untracked\` |

## Outcome against the targets

| target | result | |
|---|---|---|
| WARDEN floor never spikes; sides + ceiling do (gravity and no-gravity) | rule + 2 new test cases; all 6d WARDEN tests pass | ✓ |
| No Versus pair above 80% (200 seeds, original + holdout) | worst: orig 79.0 (tight SPORE>SPARKLER), holdout 78.0 (tight MANTIS>SPARKLER) | ✓ |
| The four 6d pairs over 80% | all now 69–78 (table below) | ✓ |
| Every weapon 35–65% per arena | 36.6–61.6 (orig), 37.4–61.6 (holdout) | ✓ |
| Overall per weapon 45–55% | 46.4–52.9 (orig), 46.2–52.7 (holdout) | ✓ |
| MIRROR 40–60% on tight and wide | 54.9 / 45.7 (orig), 55.0 / 46.5 (holdout) | ✓ |
| MANTIS vs HIVE tight ≤ 80% | 77 (orig), 75 (holdout) | ✓ |
| Side bias ≤ 2 pp | −0.35 / +0.39 pp | ✓ |
| Timeouts ≤ 1% per arena | ≤ 0.13% draws per arena (upper bound); 1 capped match in low-gravity and wide | ✓ |
| Median ActiveTicks 1800–6000 | 1863–3857 | ✓ |
| Mirror matches ≤ 90 s | weapon gate `MedianMatchLengthBetween20And90Seconds` passes; but see Deviations (MIRROR-vs-MIRROR in the all-arena CSV) | ✓ gate / note |
| MAGNET 45–77% | **75.5%** (first try 67.1%) | ✓ |
| SHELL KING 45–80% | 75.9% | ✓ |
| MIMIC 35–70% | 58.5% | ✓ |
| No player weapon < 25% vs any boss | min **28** (SPORE vs WARDEN) | ✓ |
| Random 1–6% | 1.8% | ✓ |
| WARDEN 35–70% with retry | **33.9%** (first try 31.2%) | ✗ |
| Greedy 25–40% | **17.3%** | ✗ |
| Default 800-run difficulty test (SimTests) | **fails**: Greedy 17.3% | ✗ |
| Boss gate 8/8 | **6/8** (WARDEN band, RunBots band) | ✗ |
| Determinism (two 50-seed runs) | byte-identical, SHA-256 `C24F17A3…44D03543` | ✓ |

**Main miss: WARDEN, and with it the Greedy run rate.** Taking the floor out removed only ~1 spike hit per fight (see the per-arena table): side walls give ~23–44 hits per fight and the ceiling ~10–17. WARDEN is already at the easiest values the ±25% window allows (HP −24.7%, first step +25%, step −25%, floor +24.4%, spike 2), and Greedy still beats it only 33.9%. Because WARDEN is the last boss, the Greedy run win rate stays at ~17%, and both the default 800-run test and 2 of 8 boss-gate tests fail, as in 6d. A value change inside the allowed windows cannot fix this; it needs a rule decision (see Recommendation).

## TASK 1: WARDEN, no floor spikes

- `MatchSim.Obstacles.cs` `ResolveWalls`: WARDEN's spike now applies only when the bounce is off a side wall (`xWall`) or the top wall (`yWall == ArenaWall.Top`). The bottom wall is skipped on every arena, with or without gravity.
- Unchanged from 6d: player-only, shared 10-ActiveTick immunity with normal spikes, higher damage on corner overlap, spikes follow the active smaller wall after tick 5400, one `spike_hit` and no `wall` cue per contact.
- View: `ArenaFrameView.FullWallSegments` now draws the WARDEN spike strip on Top, Left and Right only (no Bottom).
- Hint: `boss.warden.hint` = `WALLS CLOSE. SIDES AND TOP SPIKE YOU.` (strings-en.csv).
  - It measures 205 px, which is wider than the 195 px prepare-box line. `PrepareView.BossLines` now drops the traits line when the hint is wider than the box, so the hint wraps onto both lines instead of being cut.
  - `Bb04BossTests` hint case updated.
- Tests (SimTests `BossTests`):
  - new `WardenFloorNeverDamagesSidesAndCeilingDo_Gravity` and `_NoGravity`: walk the ball through Bottom, Left, Top, Right, Bottom contacts. Bottom deals 0; the others deal `WardenSpikeDamage`.
  - the 6d immunity test's second contact moved from the floor to the ceiling.
  - all other 6d WARDEN tests are unchanged and pass.
- QA report: new Explicit `WardenArenaReport.WardenPerArena` (1000 Greedy runs). It counts WARDEN spike hits per fight, split by the wall nearest the ball after the bounce.

## Values changed (old 6d → new)

| value | 6d | 6e | vs reference | why |
|---|---|---|---|---|
| `BossTuning.WardenHp` | 206 | **207** | −24.7% vs BB-04 it3 (275) | 206 is −25.1%, just outside the ±25% window; otherwise WARDEN stays at its easiest allowed values |
| `BossTuning.WardenFirstStepTick` | 450 | 450 | +25% vs 360 | easiest allowed |
| `BossTuning.WardenStepPct` | 9 | 9 | −25% vs 12 | easiest allowed |
| `BossTuning.WardenMinPct` | 56 | 56 | +24.4% vs 45 | easiest allowed |
| `BossTuning.WardenSpikeDamage` | 2 | 2 | spec 2–3 | lowest allowed |
| `BossTuning.MagnetHp` | 200 | **225** | +12.5% vs BB-04 (200) | MAGNET 83.6% → 75.5%; 220 gave 78.9%, 230 gave 73.1% |
| `BossTuning.MagnetPullPerTick` / `MagnetPullTicks` | 1/50, 45 | unchanged | | HP alone was enough |
| `WeaponTuning.AegisGuardShot` | 1 (int) | **1.25** (`Fix.Ratio(5,4)`) | Fix fraction allowed | SPARKLER vs MIRROR: classic 80.5 → 76.2, spike-walls 81.0 → 78.2, wide holdout 81.0 → 77.0. 1.375 and 1.5 pushed MIRROR vs HIVE on bumpers to 81–84 |
| `WeaponTuning.BrawlerWallSpeedBoost` | 0.08 | **0.076** (`Fix.Ratio(19,250)`) | −5% | WRECKER vs MANTIS bumpers holdout 82.2 → 77.5. −10% (0.072) cut WRECKER overall to 43.8 |
| `AegisGuardMelee/Body/Mirror`, `AegisRageStartGuards/RampGuards/MaxGuards` | int 3/2/9, 18/27/80 | same values as `Fix` | type only | needed so the shot guard can be a fraction; `AegisRule.guards` is now `Fix`. Structure unchanged: same points, same mend-per-point, same rage formula, same mirror rule |

Tried and reverted: `BrawlerSpeedBonusPerWall` 0.15 → 0.135 (it1; WRECKER got stronger on bumpers), `FangKnockbackScale` 0.3 → 0.27 (it3; MANTIS>HIVE tight 83, over the 6d limit). SPARKLER values were not changed: SPARKLER is already the weakest weapon on tight (36.6%), so any nerf would push tight under 35%.

## Iteration table (50 seeds Versus; 1000 runs per bot)

| it | changes | Versus nominal fails | MIRROR overall (tight / wide) | Greedy run | Random | WARDEN retry (first) | MAGNET retry (first) | SHELL | MIMIC | min weapon vs boss |
|---|---|---|---|---|---|---|---|---|---|---|
| 6d final | – | 4 pairs over 80 (200 seeds) | 46.1 (52.5 / 43.1) | 18.5 | 1.5 | 28.7 (25.7) | 83.6 | 77.7 | 58.8 | 20 (SPORE vs WARDEN) |
| it1 | floor spikes off; WARDEN BB-04 it3 values, spike 2; MAGNET HP 230; shot guard 1.5; Brawler per-wall 0.135 | 2 (bumpers MIRROR>HIVE 84, spike SPORE>MIRROR 81) | 48.5 (55.3 / 49.3) | 16.2 | 1.5 | 25.1 (24.1) | 75.6 (67.1) | 76.4 | 62.6 | 17 (SPORE vs WARDEN) |
| it2 | WARDEN easiest allowed (207/450/9/56); shot guard 1.25; per-wall back to 0.15; wall boost 0.072 | 2 (spike SPORE>MIRROR 81, WRECKER overall 43.8) | 47.4 (54.1 / 48.3) | 17.1 | 1.5 | 33.7 (32.6) | 73.1 (65.1) | 75.5 | 57.8 | 31 |
| it3 | wall boost back to 0.08; MANTIS knockback 0.27; MAGNET HP 220 | 3 (spike SPORE>MIRROR 81, low-grav WRECKER>MANTIS 81, tight MANTIS>HIVE 83) | 47.3 (52.7 / 47.9) | 19.4 | 1.5 | 35.9 (33.3) | **78.9** (71.6) | 76.3 | 66.1 | 31 |
| it4 | knockback back to 0.3; wall boost 0.076; shot guard 1.375; MAGNET HP 225 | 2 (bumpers MIRROR>HIVE 81, spike SPORE>MIRROR 81) | 47.6 (54.7 / 49.4) | 17.3 | 1.8 | 33.9 (31.2) | 75.5 (67.1) | 75.9 | 58.5 | 28 |
| **it5 (final)** | shot guard back to 1.25 | 1 (spike SPORE>MIRROR 81 at 50 seeds; 76.0 / 74.5 at 200) | 46.9 (54.1 / 48.6) | 17.3 | 1.8 | 33.9 (31.2) | 75.5 (67.1) | 75.9 | 58.5 | 28 |

The it3 WARDEN 35.9% depended on the MANTIS knockback change, which broke MANTIS vs HIVE on tight, so it was not kept. it5's 50-seed CSV is byte-identical to the two determinism runs.

## Versus, final (200 seeds per side)

Original (`E:\ball-battle-6e-out\final-orig\rr.csv`):

| weapon | classic | pillar | bumpers | spike-walls | low-gravity | tight | wide | overall |
|---|---|---|---|---|---|---|---|---|
| EMBER | 48.2 | 50.1 | 54.9 | 48.4 | 52.9 | 59.0 | 47.5 | 51.6 |
| MANTIS | 46.5 | 52.2 | 40.7 | 43.0 | 42.5 | 58.5 | 42.9 | 46.6 |
| ANGLER | 50.8 | 47.2 | 56.9 | 49.2 | 52.9 | 56.8 | 51.6 | 52.2 |
| WRECKER | 45.1 | 47.7 | 53.9 | 44.1 | 45.7 | 42.9 | 45.8 | 46.4 |
| SPARKLER | 58.1 | 54.9 | 46.4 | 57.7 | 54.8 | 36.6 | 61.6 | 52.9 |
| SPORE | 46.7 | 45.5 | 53.1 | 56.2 | 48.3 | 51.4 | 44.8 | 49.4 |
| MIRROR | 45.3 | 45.1 | 55.3 | 41.4 | 48.1 | 54.9 | 45.7 | 48.0 |
| HIVE | 59.2 | 56.9 | 38.5 | 59.8 | 54.5 | 39.6 | 60.2 | 52.7 |

Worst pair per arena: classic SPARKLER>MIRROR 76.2; pillar SPARKLER>WRECKER 70.2; bumpers WRECKER>MANTIS 76.5; spike-walls SPARKLER>MIRROR 78.2; low-gravity WRECKER>MANTIS 73.2; tight SPORE>SPARKLER 79.0; wide WRECKER>MANTIS 78.2.
Median/max ActiveTicks: classic 3428/7311; pillar 3330/6943; bumpers 2664/5730; spike-walls 3060/7178; low-gravity 3248/10800; tight 1866/6127; wide 3857/10800. Side A−B −0.35 pp. Draws per arena 0.05–0.12%.

Holdout (`-seedOffset 1000`, `E:\ball-battle-6e-out\final-holdout\rr.csv`):

| weapon | classic | pillar | bumpers | spike-walls | low-gravity | tight | wide | overall |
|---|---|---|---|---|---|---|---|---|
| EMBER | 47.9 | 50.1 | 56.0 | 48.9 | 55.5 | 60.1 | 48.9 | 52.5 |
| MANTIS | 46.1 | 53.5 | 40.1 | 43.0 | 41.1 | 57.5 | 41.8 | 46.2 |
| ANGLER | 49.4 | 47.9 | 57.9 | 49.1 | 51.7 | 56.2 | 49.2 | 51.6 |
| WRECKER | 44.6 | 48.2 | 54.0 | 44.5 | 48.1 | 42.9 | 46.0 | 46.9 |
| SPARKLER | 56.9 | 53.8 | 43.6 | 56.4 | 55.0 | 37.4 | 61.6 | 52.1 |
| SPORE | 46.8 | 45.0 | 53.8 | 55.9 | 48.3 | 51.2 | 45.1 | 49.4 |
| MIRROR | 46.9 | 46.2 | 55.8 | 41.1 | 47.2 | 55.0 | 46.5 | 48.4 |
| HIVE | 61.3 | 55.2 | 38.4 | 61.0 | 52.8 | 39.4 | 61.0 | 52.7 |

Worst pair per arena: classic WRECKER>MANTIS 74.0; pillar WRECKER>MANTIS 68.2; bumpers WRECKER>MANTIS 77.5; spike-walls SPORE>MIRROR 74.5; low-gravity WRECKER>MANTIS 73.2; tight MANTIS>SPARKLER 78.0; wide SPARKLER>MIRROR 77.0.
Median/max ActiveTicks: classic 3421/7361; pillar 3319/6918; bumpers 2661/5814; spike-walls 3054/7177; low-gravity 3256/10800; tight 1863/6843; wide 3840/10800. Side A−B +0.39 pp. Draws per arena 0.02–0.13%.

### The 6d pairs over 80%, before and after

| pair | arena | 6d orig | 6d holdout | 6e orig | 6e holdout |
|---|---|---|---|---|---|
| SPARKLER > MIRROR | classic | **80.5** | 75 | 76.2 | 69 |
| SPARKLER > MIRROR | spike-walls | **81.0** | 76 | 78.2 | 71 |
| SPARKLER > MIRROR | wide | 79 | **81.0** | 74 | 77.0 |
| WRECKER > MANTIS | bumpers | 77.5 | **82.2** | 76.5 | 77.5 |

(Values without a decimal come from the per-opponent table, rounded to the whole %.)

## RunBot (1000 runs per bot, final values)

| bot | run win | | | | |
|---|---|---|---|---|---|
| Greedy | **17.3%** ✗ | | | | |
| Random | 1.8% ✓ | | | | |

| boss | Greedy first try | Greedy with retry | target | Random first try | Random with retry |
|---|---|---|---|---|---|
| SHELL KING | 63.9 | 75.9 | 45–80 ✓ | 40.5 | 53.2 |
| MAGNET | 67.1 | 75.5 | 45–77 ✓ | 39.3 | 47.1 |
| MIMIC | 56.4 | 58.5 | 35–70 ✓ | 51.5 | 54.5 |
| WARDEN | 31.2 | **33.9** | 35–70 ✗ | 0.0 | 0.0 |

Player weapon vs boss (100 seeds each; Greedy build, arrives at full HP):

| weapon | SHELL KING | MAGNET | MIMIC | WARDEN |
|---|---|---|---|---|
| EMBER | 78 | 83 | 51 | 55 |
| MANTIS | 87 | 91 | 52 | 58 |
| ANGLER | 81 | 83 | 46 | 49 |
| WRECKER | 95 | 91 | 67 | 52 |
| SPARKLER | 83 | 87 | 56 | 42 |
| SPORE | 75 | 58 | 42 | **28** |
| MIRROR | 90 | 72 | 50 | 32 |
| HIVE | 89 | 82 | 60 | 45 |

The minimum is 28 (SPORE vs WARDEN; 6d had 20), so the 25% floor is met. Boss timeouts are 0.0%.

## WARDEN per arena (Greedy, 1000 runs, final values)

| arena | WARDEN fights | Greedy win % | spike hits per fight: side walls | ceiling | floor (normal corner zones only) |
|---|---|---|---|---|---|
| classic | 41 | 26.8 | 29.00 | 12.56 | 0.98 |
| pillar | 49 | 6.1 | 43.65 | 17.22 | 1.41 |
| bumpers | 31 | 35.5 | 31.71 | 12.81 | 0.45 |
| spike-walls | 22 | 40.9 | 28.59 | 12.36 | 0.77 |
| low-gravity | 31 | 22.6 | 29.65 | 12.81 | 0.52 |
| tight | 29 | 37.9 | 23.24 | 9.66 | 0.76 |
| wide | 39 | 28.2 | 33.59 | 16.05 | 0.64 |

- The "floor" column is only normal corner spike zones on the floor; WARDEN spikes never hit there (covered by tests).
- **Low gravity:** 12.81 ceiling hits per fight, the same as classic (12.56). Low gravity does not cause extra ceiling hits; the most ceiling hits are on pillar (17.2) and wide (16.1).
- **Pillar** is the worst arena (6.1% wins, 43.7 side hits): the pillar keeps bouncing the ball back into the closing side walls.
- Side walls give about 70% of all WARDEN hits.
- Counting: a damaging `WallBounce` of the player ball, assigned to the wall nearest the ball after the bounce. Arena is chosen per fight by the run, so samples are 22–49 fights per arena.

### Recommendation (rule level, needs your decision)

A value change inside the allowed windows cannot get WARDEN to 35%. Options, mildest first:
1. Spikes only on a wall in the ticks right after it moves (e.g. the first 60 ticks of each step).
2. A longer WARDEN-spike immunity (e.g. 30 ActiveTicks instead of the shared 10).
3. Spike damage 1 (below the 2–3 window).
4. Side walls only (no ceiling): removes ~30% of hits.

## Tests

| suite | result |
|---|---|
| SimTests (default `dotnet test`) | **247 total, 246 pass, 1 fail**: `RunDifficultyThresholdTests.GreedyBotWins25To40Percent` (N=800, measured 17.3%; the WARDEN miss). Results: `bb6e-simtests.trx` |
| EditMode (Unity 6000.5.1f1 batchmode) | **300 total, 297 pass, 0 fail, 3 skip** (env-var capture tests, as in 6d). Results: `bb6e-editmode.xml` |
| Explicit weapon gate `BalanceThresholdTests` | **3/3 pass** |
| Explicit trait gate `TraitBalanceThresholdTests` | **2/2 pass** (321 s) |
| Explicit boss gate `BossBalanceThresholdTests` + `BossSweep` | **6/8 pass** (6d: 4/8). Fail: `GreedyBeatsEachBossInItsBand("warden")` 33.9%, `RunBotsStayInTheirBands` Greedy 17.3%. Report: `bb6e-boss.md` |
| Determinism | two 50-seed round-robins (`det-a`, `det-b`) byte-identical, SHA-256 `C24F17A31D3C6B1D79043C7508BD81974B1C534222741E9C8AEB0C7F44D03543`; also identical to the it5 run |

Goldens re-pinned once at the end (still Rules 9):
- `CrossPlatformGoldenTests.ExpectedBySeed` (rules9) and `ExpectedBySeedMasked`: all 20 each. Seed 1001: `0x78D5E8519E72740F` → `0x2263C40910E58196` (rules9), `0x7F7EB3B97E651D43` → `0x7B733571A0A174EE` (masked).
- `VersusGoldenTests.ExpectedDigest`: `0x6045CAC4FB5E7C2E` → `0x6DD06D6CCE44FA99`.
- `SpikeCornerTests.ExpectedByArena`: classic `0x07EFC29D33B5A1FF` → `0xE2F0DCC26B635C9A`; pillar `0x0BA0463FFC8C6612` → `0x3304994C3D467FCA`; bumpers `0xCBB6A8AEBB0E15EE` → `0x55D70DE67781E84A`; spike-walls `0xA7F61CA78E5B5A68` → `0x1A27AF4835C886F0`; low-gravity `0x133461F6056DDE15` → `0x11B638762D9DF817`; tight `0xD1A84467BE2C7236` → `0x23624466AFBE95DF`; wide `0x077D1761F0A481B0` → `0x96AD5272F0D8137F`.
- These change because the MIRROR shot guard and the WRECKER wall boost change Versus outcomes. The WARDEN floor rule doesn't affect Versus.

Final CSVs: `E:\ball-battle-6e-out\final-orig\`, `final-holdout\`, `det-a\`, `det-b\`, and `it1`–`it5\`.

## Deviations

1. **WARDEN 33.9% < 35%, Greedy run 17.3% < 25%**, so the default 800-run test fails in SimTests and the boss gate is 6/8. Values are at the edge of the allowed windows; a rule decision is needed (Recommendation).
2. `WardenHp` is 207, not 206 as in 6d: 206 is 25.1% below 275, just outside ±25%.
3. MIRROR guard-point fields changed type from `int` to `Fix` (values unchanged) so that the shot value can be 1.25, as the spec allows. The structure is unchanged: same rules, same mirror rule.
4. **MIRROR vs MIRROR in the all-arena CSV:** median match length is 101.5 s on wide and 91.8 s on classic (orig; holdout 101.4 / 95.8). This is unchanged from 6d (6e did not touch MIRROR-vs-MIRROR behaviour), and the weapon gate's mirror check passes. I'm noting it because "mirror matches ≤ 90 s" read per arena would fail on wide.
5. The PrepareView hint wrap: the new hint is 10 px too wide for one line, so the boss panel shows the hint on two lines without the traits line (View only).
6. No SPARKLER value was changed (the spec said to try them first, together with the shot guard). The shot guard alone brought all three SPARKLER>MIRROR cells under 80, and SPARKLER is already 36.6% on tight, so a nerf would risk the 35% floor there.

## Confirmation

- Nothing committed, pushed, stashed or deleted (incl. `Temp/UnityLockfile`, lock files, and `E:\ball-battle-6d-out\final-orig2\` / `final-holdout2\`).
- `git rev-parse --short HEAD` = **4bb5061**; `git stash list` empty.
- `SimVersion.Rules` = **9**.
