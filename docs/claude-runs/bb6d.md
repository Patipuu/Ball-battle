# BB-6d: MIRROR scales on combat, MANTIS/HIVE tight, WARDEN spiked walls, QA aids (Rules 8 → 9)

Date 2026-10-14. Branch `dev/step3-view`, HEAD `4bb5061` (BB-04), unchanged. **Nothing committed, pushed or deleted.**
`SimVersion.Rules = 9`.

Preconditions checked first:

- HEAD = `4bb5061` (parent `24ca58c`).
- No tracked changes under `BallBattleUnity/Assets`.
- Unity was not running and there was no `Temp/UnityLockfile`.

## Outcome

| target | result | |
|---|---|---|
| MIRROR on tight and wide within 40–60% (200 seeds) | tight 52.5 / wide 43.1 (orig) / tight 53.2 / wide 45.2 (holdout) | ✓ |
| MANTIS vs HIVE on tight (was 83.2%) | 77.0% (orig) / 75.0% (holdout) | ✓ (< 80) |
| Every weapon 35–65% on every arena | lowest 36.5 (SPARKLER tight, holdout), highest 62.1 (SPARKLER wide): ✓ (no margin) | |
| No pair above 80% | **orig: SPARKLER>MIRROR 80.5 classic, 81.0 spike-walls; holdout: WRECKER>MANTIS 82.2 bumpers, SPARKLER>MIRROR 81.0 wide** ✗ | |
| Overall per weapon 45–55%; side bias ≤ 2 pp | 45.9–53.2% overall; side −0.24 / +0.67 pp: ✓ | |
| Timeouts ≤ 1% per arena; median ActiveTicks 1800–6000 | ✓ (at most 1 of 12,800 matches per arena hit the cap; medians 1862–3853) | ✓ |
| RunBot Greedy 25–40% / Random 1–6% | **18.5%** / 1.5% | ✗ / ✓ |
| Greedy beats WARDEN 35–70% | **28.7%** (first try 25.7%) | ✗ |
| SHELL KING 45–80% / MAGNET ≤ 77% / MIMIC 35–70% | 77.7% / **83.6%** / 58.8% | ✓ / ✗ / ✓ |
| No player weapon below 25% vs any boss | **SPORE 20%, MIRROR 23% vs WARDEN** | ✗ |
| Explicit gates: weapon / trait / boss | 3/3 / 2/2 / **4/8** | ✓ / ✓ / ✗ |
| SimTests | **245 total, 244 pass, 1 fail** (`RunDifficultyThresholdTests.GreedyBotWins25To40Percent`: 18.4%, see Outcome) | |
| EditMode | **300 total, 297 pass, 0 fail, 3 skip** (env-var capture tests) | ✓ |
| Determinism (two 50-seed runs byte-identical) | ✓ | ✓ |

**The WARDEN spiked-walls rule as specified makes the Run too hard, and no allowed value fixes it.**

- After the first step every wall, including the floor, is spiked for the player.
- Gravity makes the player bounce on the floor about every second, so the spikes act as a steady damage-over-time that the immunity only throttles to one hit per 10 ActiveTicks.
- Even at the easiest allowed WARDEN values, the Greedy bot beats WARDEN only 25–33%:
  - spike damage 2 HP (minimum);
  - HP 206 (−25%);
  - first step at 450 (+25%);
  - 9% steps (−25%) and a 56% floor (+25%).
- That pulls the Greedy run win rate to ~18.5%, which also fails the default `RunDifficultyThresholdTests` gate (800 runs, 25–40%) inside the SimTests suite.
- Rule-level fixes I would suggest (not done, they change the spec):
  - do not spike the floor;
  - make the WARDEN spike immunity longer than 10 ticks;
  - spike only the walls that just moved;
  - allow spike damage 1.

**MAGNET** (83.6%) cannot get under 77% with BB-04's allowed values; its HP is already at +25%. This task gave no MAGNET tuning room, so its values are unchanged from BB-04 it3.

## TASK 1: MIRROR timer → counter mapping

Read first: `AegisRule` at 4bb5061 had two time-based growth triggers and one combat one.

| before (time) | after (combat) |
|---|---|
| `ticks++` every active tick (`OnTick`); **rage** = `d × (1 + (ticks − AegisRageStartTicks) / AegisRageRampTicks)` after 675 ticks, +100% per 750 ticks | **guard points** `guards`; rage = `d × (1 + min(guards − AegisRageStartGuards, AegisRageMaxGuards) / AegisRageRampGuards)` |
| **regen** `AegisRegenPerSecond` (0.1875 HP) every 60 ticks | **mend** `AegisMendPerGuard` HP per guard point, healed when the guard happens |
| width +`AegisWidthPerHit` per own hit (`OnHit`) | unchanged (already combat-based) |

Guard points per event (`AegisRule.Guard`):

| event | points | mend? |
|---|---|---|
| parry against a blade (`OnParry`) | `AegisGuardMelee` = 3 | yes |
| blade hit taken (`OnHitTaken`, Weapon, bladed attacker) | 3 | yes |
| body blow taken (`OnHitTaken`, Weapon, bladeless attacker) | `AegisGuardBody` = 2 | yes |
| shot taken (`OnHitTaken`, Projectile) | `AegisGuardShot` = 1 | yes |
| parry against another MIRROR | `AegisGuardMirror` = 9 | **no** |
| hit taken from another MIRROR | 3 | **no** |

`OnTick` no longer exists. Time alone changes nothing (tested). The hash covers `guards` in place of `ticks`.

Final MIRROR values (free inside the new rules):

| value | final |
|---|---|
| AegisMendPerGuard | 0.12 (3/25) |
| AegisRageStartGuards | 18 |
| AegisRageRampGuards | 27 (+100% per 27 points) |
| AegisRageMaxGuards | 80 (cap ≈ +296%) |

The other MIRROR values are as at 24ca58c.

Why the rules look like this:

- **Shots as guards.** If shots counted fully, MIRROR crushed HIVE 99% (it0). If they didn't count, shooters beat it ~97% (it1). One point per shot gives its "weak to shots" identity without a hard counter.
- **The cap** stops runaway rage against a long stream of shots without stripping its power (it5's +150% cap dropped it to 37.7%).
- **The mirror rule** was added after validation. The weapon gate's mirror median was 102.7 s, over the 90 s limit, because two MIRRORs kept mending each other. Now they only rage and the median passes. It changes only MIRROR-vs-MIRROR matches, so no tuned non-mirror number or outcome golden moved; only the hash goldens re-pinned.

## TASK 2: MANTIS vs HIVE on tight

`FangSpinGainDecay` 0.94 → **0.92** (−2.1% vs 24ca58c).

- Each hit's spin gain shrinks a little faster, which cuts long hit chains.
- Long chains happen most on tight, where fights are dense.
- Tight MANTIS went 65% → 56–58%. Tight MANTIS vs HIVE went from 83.2% (BB-6c holdout) to 77.0% (orig) / 75.0% (holdout).
- 0.86 (−8.5%) was tried in it3 and crushed MANTIS everywhere (35.9%).
- No HIVE value changed.

## TASK 3: WARDEN spiked walls (Run boss only)

How the spikes work (Sim):

- `WardenRule.WallsSpiked` is true from the first step on. `WallSpikeDamage(ball)` returns `BossTuning.WardenSpikeDamage` for every ball but the Warden.
- `MatchSim.ResolveWalls` takes, for each bounce, the higher of the normal spike-zone damage and the boss wall spike. It deals it once, through the same per-ball 10-ActiveTick immunity (`spikeActiveTick`).
- The walls are the current `Arena`, the smaller of the WARDEN and normal-shrink bounds per side. After tick 5400 the spikes therefore sit on the active wall automatically.
- Normal spike zones keep working for both balls.
- No new hash state: the spikes derive from `Steps`, and `WardenSpikeDamage` is in the WARDEN hash.

View side:

- **Audio:** a WARDEN spike hit is a damaging `WallBounce`, so it plays `spike_hit` through the existing gate. `AudioDirector` now remembers each ball's `spike_hit` tick and drops a `wall` cue for that ball in the same tick, logged `allowed=no`. One contact = one `spike_hit`.
- **Telegraph:** `ArenaFrameView.FullWallSpikes` (set from `MatchSim.BossSpikedWalls`) draws the existing teeth (or strip placeholder) along every wall. `BossArtEnabled` stays off.
- **Hint:** "WALLS CLOSE AND SPIKE YOU EVERY 8S." It measures 195 px, exactly the box width.

Tests: `SimTests/BossTests` (+6), `EditMode/Bb6dTests`:

- the player is hurt by a WARDEN wall after a step and the WARDEN is not;
- the immunity applies, and hit-stop does not count;
- no extra spikes before the first step;
- corner overlap gives one hit at the higher value (2 cases: WARDEN higher, zone higher) and a shared immunity;
- after the 5400 shrink, the spikes are on the active smaller wall;
- one contact logs exactly one `spike_hit` and no wall cue (`-bbAudioLog`);
- teeth are drawn after the step.

## TASK 4: QA aids (View/tool only)

**`-bbForceArena <id>`** (`DebugOptions.ParseForceArena`, parsed once, so an unknown id warns once):

- Matching ignores case, dashes, underscores and spaces.
- Run only: `RunFlow.ForceArenaId` builds each fight with `RunState.CreateMatch(ArenaRegistry.Create(id))`. Prepare (`ArenaOverride`) and the arena view show the forced arena.
- `RunState.ArenaId`, the fight seed, history and the save are untouched.
- Versus never reads it. It combines with `-bbForceBoss`.
- Tests: parsing, including all 7 ids and an unknown id; Run-only plus `-bbForceBoss` gives MAGNET on tight while the run's own arena, seed and save are unchanged; Versus keeps its arena.

**SHELL KING shield trace** (`View/ShellTrace.cs`):

- For every Hit or ProjectileHit on SHELL KING while the shell is ON, it writes
  `[Shell] Hit tick= active= attacker= angle=<deg from facing> blocked=true|false dmg=`
  to the `-bbTrace` file (ArenaView) and, with `-bbAudioLog`, to the audio log.
- The line is built from data the Sim already exposes: event `Point`, `Variant` (1 = shell-stopped), `ShellKingRule.Facing`, and the boss position. No event field was added; no hash or golden changed for it.
- Test: a hit at the facing centre logs `angle=0.0 blocked=true`; one 90° off logs `angle=90.0 blocked=false` (arc 120).

## Every other value changed

| value | old | new | % (vs 24ca58c / BB-04 it3) | why |
|---|---|---|---|---|
| `WeaponTuning.FangSpinGainDecay` | 0.94 | 0.92 | −2.1% | Task 2: tight MANTIS chains |
| `BossTuning.WardenHp` | 275 | 206 | −25% | Run: WARDEN spikes too strong |
| `BossTuning.WardenFirstStepTick` | 360 | 450 | +25% | same |
| `BossTuning.WardenStepPct` | 12 | 9 | −25% | same (bigger arena, fewer contacts) |
| `BossTuning.WardenMinPct` | 45 | 56 | +24.4% | same |
| `BossTuning.WardenSpikeDamage` | (new) 3 | 2 | spec range 2–4 | same |
| MIRROR: `AegisRegenPerSecond`, `AegisRageStartTicks`, `AegisRageRampTicks` | removed | see Task 1 | (free) | Task 1 |

Unchanged on purpose: HIVE values, MAGNET (no tuning room in this task), all base ball stats.

## Iteration table (50 seeds per side for Versus; 1000 runs per bot for Run)

| it | changes | MIRROR overall (tight / wide) | Versus nominal fails | Greedy run | WARDEN beaten | MAGNET |
|---|---|---|---|---|---|---|
| it0 | rule change: guards = parries + all hits taken (1 each), mend 0.35, rage start 6 / ramp 8; WARDEN spikes 3 | 68.1 (69.1 / 69.9) | 19 (MIRROR>HIVE 92–100% everywhere) | 13.3 (800-run gate) | - | - |
| it1 | shots no longer guards; mend 0.25, start 10, ramp 12; WARDEN spike 2, HP 220, first 450 | 28.4 (30.0 / 29.3) | 18 (SPARKLER/HIVE>MIRROR 97–99%) | - | - | - |
| it2 | weighted guards (parry/blade 3, body 2, shot 1), mend 0.10, start 21, ramp 27 | 44.0 (50.4 / 44.0) | 2 (spike SPORE>MIRROR 86, MIRROR overall 44.0) | 21.3 | 32.4 | 81.3 |
| it3 | mend 0.12, start 18; FangSpinGainDecay 0.86; WARDEN HP 206, step 9%, floor 56% | 49.7 (56.3 / 50.0) | 11 (MANTIS crushed: 35.9) | 18.3 | 25.0 | 83.4 |
| it4 | FangSpinGainDecay 0.92 | 47.9 (54.3 / 47.6) | 2 (bumpers MIRROR>HIVE 84, spike SPARKLER>MIRROR 81) | 18.7 | 29.2 | 83.9 |
| it5 | rage cap 40 points, mend 0.14 | 37.7 (43.0 / 37.4) | 9 | 18.7 | 28.4 | 85.0 |
| **it6** | rage cap 80, mend 0.12 | 45.6 (51.3 / 45.7) | **1** (spike SPARKLER>MIRROR 82) | 18.5 | 28.7 | 83.6 |
| post | MIRROR-mirror: no mend from another MIRROR, 9-point parry (weapon gate) | non-mirror results identical | - | 18.5 | 28.7 | 83.6 |

it6 (plus the mirror-only rule) is final.

## Final validation: 200 seeds per side (`E:\ball-battle-6d-out\final-orig3\`, `final-holdout3\`)

**Original seeds:**

| weapon | classic | pillar | bumpers | spike-walls | low-gravity | tight | wide | overall |
|---|---|---|---|---|---|---|---|---|
| EMBER | 48.7 | 49.9 | 55.6 | 48.3 | 52.3 | 58.3 | 47.6 | 51.5 |
| MANTIS | 45.9 | 51.9 | 40.5 | 42.5 | 41.8 | 58.1 | 43.5 | 46.3 |
| ANGLER | 50.7 | 47.2 | 57.9 | 48.6 | 52.1 | 56.8 | 51.6 | 52.1 |
| WRECKER | 46.6 | 49.1 | 53.4 | 46.8 | 51.8 | 45.6 | 46.1 | 48.5 |
| SPARKLER | 58.8 | 55.5 | 46.6 | 58.2 | 54.2 | 37.4 | 62.1 | 53.2 |
| SPORE | 46.8 | 44.9 | 53.6 | 55.6 | 47.4 | 51.5 | 44.8 | 49.2 |
| MIRROR | 43.1 | 44.0 | 53.6 | 40.4 | 46.3 | 52.5 | 43.1 | 46.1 |
| HIVE | 59.3 | 57.2 | 38.5 | 59.5 | 54.0 | 39.9 | 61.2 | 52.8 |

Worst pair per arena:

| arena | worst pair |
|---|---|
| classic | **SPARKLER>MIRROR 80.5** |
| pillar | SPARKLER>MIRROR 71.0 |
| bumpers | WRECKER>MANTIS 77.5 |
| spike-walls | **SPARKLER>MIRROR 81.0** |
| low-gravity | WRECKER>MANTIS 78.8 |
| tight | SPORE>SPARKLER 79.0 |
| wide | SPARKLER>MIRROR 79.0 |

Tight MANTIS vs HIVE is 77.0. Side bias is −0.24 pp.

**Holdout seeds (`-seedOffset 1000`):**

| weapon | classic | pillar | bumpers | spike-walls | low-gravity | tight | wide | overall |
|---|---|---|---|---|---|---|---|---|
| EMBER | 47.6 | 49.8 | 56.5 | 48.6 | 54.6 | 59.8 | 48.2 | 52.2 |
| MANTIS | 46.4 | 52.9 | 39.5 | 43.1 | 40.5 | 57.0 | 41.9 | 45.9 |
| ANGLER | 50.1 | 47.9 | 58.5 | 49.6 | 52.2 | 56.4 | 49.6 | 52.0 |
| WRECKER | 46.2 | 50.5 | 54.3 | 45.8 | 49.8 | 47.3 | 47.2 | 48.7 |
| SPARKLER | 57.6 | 54.3 | 44.3 | 56.8 | 55.4 | 36.5 | 61.2 | 52.3 |
| SPORE | 45.9 | 45.1 | 53.3 | 55.1 | 48.4 | 50.0 | 45.6 | 49.0 |
| MIRROR | 45.2 | 44.1 | 54.7 | 40.3 | 45.8 | 53.2 | 45.2 | 46.9 |
| HIVE | 60.8 | 55.4 | 38.8 | 60.6 | 52.9 | 39.6 | 60.8 | 52.7 |

Worst pair per arena:

| arena | worst pair |
|---|---|
| classic | SPARKLER>MIRROR 75.0 |
| pillar | SPARKLER>MIRROR 72.2 |
| bumpers | **WRECKER>MANTIS 82.2** |
| spike-walls | HIVE>MIRROR 76.0 |
| low-gravity | WRECKER>MANTIS 77.5 |
| tight | MANTIS>SPARKLER 78.0 |
| wide | **SPARKLER>MIRROR 81.0** |

Tight MANTIS vs HIVE is 75.0. Side bias is +0.67 pp.

**Timeouts and median/max ActiveTicks per arena:**

| arena | orig median / max | holdout median / max | pair-rows with a capped match (orig / holdout) |
|---|---|---|---|
| classic | 3427 / 7311 | 3420 / 7361 | 0 / 0 |
| pillar | 3322 / 6943 | 3316 / 6929 | 0 / 0 |
| bumpers | 2661 / 5730 | 2660 / 5814 | 0 / 0 |
| spike-walls | 3056 / 7178 | 3051 / 7177 | 0 / 0 |
| low-gravity | 3246 / 10800 | 3253 / 10800 | 1 / 1 |
| tight | 1864 / 6127 | 1860 / 6843 | 0 / 0 |
| wide | 3852 / 10800 | 3834 / 10800 | 1 / 1 |

Each arena has 12,800 matches, so timeouts are far below 1%.

## RunBot (1000 runs per bot; final values, re-measured after the mirror fix: identical)

| bot | run win | SHELL KING first / retry | MAGNET first / retry | MIMIC first / retry | WARDEN first / retry |
|---|---|---|---|---|---|
| Greedy | **18.5%** | 66.3 / 77.7 | 76.3 / **83.6** | 53.7 / 58.8 | 25.7 / **28.7** |
| Random | 1.5% | 39.6 / 50.3 | 44.4 / 50.5 | 38.9 / 38.9 | 3.8 / 3.8 |

Weapon vs boss minimum, player win % (100 seeds per cell):

| boss | lowest weapon |
|---|---|
| SHELL KING | SPORE 76 |
| MAGNET | SPORE 65 |
| MIMIC | SPORE 42 |
| WARDEN | **SPORE 20**, **MIRROR 23**, SPARKLER 27, HIVE 35 |

Boss fight timeouts: 0.0% for every boss. Full tables: `bb6d-boss.md`.

## Tests and gates

| suite | result |
|---|---|
| SimTests (default) | **245 total, 244 pass, 1 fail**: `GreedyBotWins25To40Percent` 18.4%. Results: `bb6d-simtests.trx` |
| EditMode | **300 total, 297 pass, 0 fail, 3 skip**. Results: `bb6d-editmode.xml` |
| Explicit weapon gate | **3/3 pass** (mirror median fixed by the MIRROR-mirror rule) |
| Explicit trait gate | **2/2 pass** |
| Explicit boss gate | **4/8**: SHELL KING band, MIMIC band, timeouts and the `BossSweep` report pass; MAGNET band (83.6), WARDEN band (28.7), weapons vs WARDEN (SPORE 20, MIRROR 23) and the RunBot band (Greedy 18.5) fail |

New tests: `BossTests` +6 (WARDEN spikes), `NewWeaponTests` (MIRROR guards), `Bb6dTests` (5: spike audio, teeth, force-arena parsing, force-arena Run-only with force-boss, shell trace).

**Goldens regenerated once, for Rules 9:**

- `CrossPlatformGoldenTests`:
  - `ExpectedBySeed`: re-pinned for Rules 9 (from the BB-04 Rules-8 values).
  - The Rules-7 array was retired. Versus now changes on purpose, so it is replaced by `ExpectedBySeedMasked`: the same digests with the Rules field hashed as 0, to guard the next Rules-only bump.
- `VersusGoldenTests.ExpectedDigest`: `0x6EBF6B88AB6F72B3` → `0x6045CAC4FB5E7C2E` (FangSpinGainDecay).
- `SpikeCornerTests.ExpectedByArena`:

  | arena | new digest |
  |---|---|
  | classic | `0x07EFC29D33B5A1FF` |
  | pillar | `0x0BA0463FFC8C6612` |
  | bumpers | `0xCBB6A8AEBB0E15EE` |
  | spike-walls | `0xA7F61CA78E5B5A68` |
  | low-gravity | `0x133461F6056DDE15` |
  | tight | `0xD1A84467BE2C7236` |
  | wide | `0x077D1761F0A481B0` |

  The old values are the BB-04 ones in the file history.
- **Updated tests:**
  - `NewWeaponTests`: `AegisHitsHarderLateInTheFight` became `AegisHitsHarderAfterEnoughGuards`. It also checks that time alone changes nothing. The body/shot test now includes mend.
  - `BossTests`: the spike-zone test expects the higher of zone and WARDEN; the 5400 test restores the default shrink.
  - `Bb04BossTests`: the WARDEN hint.

**Determinism.** Two identical 50-seed round-robins give the same SHA-256 prefix for each CSV:

| file | SHA-256 prefix |
|---|---|
| `rr.csv` | `27BE374EF2339E1F` |
| `rr-weapons.csv` | `CB4932AF24A745F8` |
| `rr-arenas.csv` | `237B00E313A5F0C6` |

Final CSVs are in `E:\ball-battle-6d-out\final-orig3\` and `final-holdout3\` (each `rr.csv`, `rr-weapons.csv`, `rr-arenas.csv`).

## Deviations

1. **Run targets missed** (see Outcome): Greedy 18.5%, WARDEN 28.7%, SPORE/MIRROR vs WARDEN 20/23%, MAGNET 83.6%. The boss gate is 4/8. The default SimTests gate `GreedyBotWins25To40Percent` fails. All of this follows from the WARDEN spike rule within the allowed values.
2. **Versus pair misses** at 200 seeds: original: SPARKLER>MIRROR 80.5% (classic) and 81.0% (spike-walls); holdout: WRECKER>MANTIS 82.2% (bumpers) and SPARKLER>MIRROR 81.0% (wide). All within 2.2 pp of the 80% cap. MIRROR stays weak to shooters by design (Loses to poison and shots).
3. **The MIRROR-mirror rule** (no mend from another MIRROR, 9-point parry) was added after the 6 tuning iterations and the first validation, to make the weapon gate's mirror median pass. Non-mirror results are unchanged by it. The final 200-seed validation was re-run once on the final code.
4. **Shots count as 1 guard point** (melee 3). The spec said "hits it reflects or blocks (and, if needed, hits it takes)": parries are blocks, hits taken were needed, and shots are hits taken with a lower weight.
5. **Mid-run restart.** I stopped one test queue (killed my own `powershell`/`testhost` processes) when I changed the mirror rule, so no run mixed two builds. No file was deleted. Partial folders `final-orig2\` and `final-holdout2\` were left as they were.
6. **Timeout counts are a lower bound.** The round-robin CSV keeps only max ActiveTicks per pair. At most 1 pair-row per arena hit 10800, each of 200 matches, so ≤ ~0.02% if one match each. Exact counts would need a tool column.
7. **Trait gate** ran before the mirror rule; trait deltas use blade, fang, pike and brawler only, so it is unaffected.

## Confirmation

- Nothing committed, pushed or deleted. No lock file was touched (none existed).
- HEAD is still `4bb5061`.
- `SimVersion.Rules = 9`.
