# BB-6c: spike re-hit immunity + arena balance pass (Sim change, Rules 6 → 7)

Date 2026-10-14. Branch `dev/step3-view`, HEAD `7df2b1f` (BB-SFX2), unchanged. **Nothing committed, pushed or deleted.**
`SimVersion.Rules = 7`.

Preconditions checked first: HEAD = `7df2b1f`; no tracked changes under `BallBattleUnity/Assets`; Unity not running and no
`Temp/UnityLockfile` (also checked before each EditMode run).

## Outcome in one paragraph

Task 1 (spike immunity) is done and tested. Task 2 did **not** meet every target. Validation stopped after the one allowed
fix iteration (it7), as the spec requires. The final build is it7. On both 200-seed runs it passes side bias, ActiveTicks and
traits, and every weapon is 35–65% on 6 of 7 arenas. These targets still fail:

| target | original 200 | holdout 200 |
|---|---|---|
| wide MIRROR ≤ 65% | 65.5 ✗ | 65.6 ✗ |
| overall MIRROR ≤ 55% | 55.5 ✗ | 55.5 ✗ |
| tight MANTIS ≤ 65% | 64.4 ✓ | 65.6 ✗ |
| pair ≤ 80% | 79.5 ✓ (tight MANTIS>SPARKLER) | 83.2 ✗ (tight MANTIS>HIVE) |

Why: MIRROR is back-loaded (thickness growth, regen, rage). Tight fights are about 1850 ActiveTicks and wide fights about
3800, so any MIRROR value that moves it on tight moves it the other way on wide. At 7df2b1f the spread was 25% on tight
versus 64% on wide. Inside the ±25% limit the best I found is about 35% versus 65.5%. The spread is a shape problem, not a
number problem. Closing it needs an arena-aware rule, for example rage or regen timers scaled by arena size. That is a rule
change, and this task did not allow one.

All test suites and gates pass:

| suite | result |
|---|---|
| SimTests | 217/217 |
| EditMode | 277 pass, 0 fail, 3 skip out of 280 |
| Explicit weapon gate | 3/3 |
| Explicit trait gate | 2/2 |

Determinism holds: the CSVs are byte-identical.

## Task 1: spike re-hit immunity

- `ArenaTuning.SpikeImmunityTicks = 10`.
- `MatchSim.spikeActiveTick[]` records the `ActiveTick` of each ball's last spike damage (−1 = never).
- `SpikeImmune(i)` means `ActiveTick − last < 10`. `ActiveTick` does not advance during hit-stop, so hit-stop never shortens immunity.
- Only the spike-damage branch in `MatchSim.Obstacles` checks immunity. Bounces, obstacles, weapons and other hazards are unaffected.
- The per-ball state is mixed into `ComputeHash`.
- A test hook `internal ExtendHitstop(int)` was added. The existing parry and hit hit-stop code now calls it, with identical behaviour.
- New `SimTests/SpikeImmunityTests.cs` covers spec tests a–e, all passing:
  - a: a corner costs exactly one `SpikeWallDamage`;
  - b: new damage after ≥ 10 ActiveTicks;
  - c: hit-stop during immunity → immunity ends 10 ActiveTicks after the hit, not 10 Ticks;
  - d: two balls in the same tick each take damage;
  - e: pillar and bumper bounce normally right after a spike hit.
- Spec wording says "costs exactly 3 HP once". The balance pass then lowered `SpikeWallDamage` from 3 to 2.25, so the tests
  assert one `ArenaTuning.SpikeWallDamage` (2.25), not a literal 3. At Task 1 time (damage 3) they asserted 3 and passed.

## Every value changed (vs 7df2b1f)

All values are Fix (Q16.16) ratios. Every change is within ±25% of the 7df2b1f value.

| value | 7df2b1f | final | % | why |
|---|---|---|---|---|
| `ArenaTuning.SpikeWallDamage` | 3 (int) | 2.25 (`Fix.Ratio(9,4)`) | −25.0 | SPORE was 65.1% on spike-walls after Task 1. Its poison wins the chip war while both take ~26 spike HP per match. |
| `ArenaTuning.SpikeZoneNum/Den` | 1/5 | 3/20 | −25.0 | Same: fewer spike hits on spike-walls. |
| `ArenaTuning.BumperRadius` | 9 | 6.75 | −25.0 | HIVE was 34.9% on bumpers (drones and shots wasted on bumpers); SPORE>SPARKLER 84 there. |
| `FangSpinGainDecay` | 0.98 | 0.94 | −4.1 | it7: curb MANTIS spin build-up in hit-dense tight fights. |
| `FangDamagePerHit` | 0.4 | 0.4 | 0 | it1–it6 used 0.36; reverted in it7 because 0.36 put parry-master L1 at +15.3, over the trait gate's +15 ceiling. |
| `BrawlerSpeedBonusPerWall` | 0.2 | 0.15 | −25.0 | WRECKER>MIRROR 89 (tight) and WRECKER>MANTIS 81 (wide). |
| `VolleySpin` | 3.5 | 4.375 | +25.0 | SPARKLER was 34.6% on tight (blade too slow to cover itself up close). |
| `VolleyMeleeDamage` | 1 | 1.25 | +25.0 | Same, for tight. |
| `VenomDamagePerHit` | 0.25 | 0.1875 | −25.0 | SPORE 65.1 on spike-walls; SPORE>SPARKLER 84 on bumpers; SPORE>MIRROR 89 on tight. |
| `AegisBodyReflectPct` | 0.5 | 0.5625 | +12.5 | MIRROR tight: front-loaded reflect. |
| `AegisWidthPerHit` | 0.25 | 0.3125 | +25.0 | MIRROR tight: shield grows faster in short fights. |
| `AegisReflectPct` | 0.4 | 0.45 | +12.5 | MIRROR tight: front-loaded. |
| `AegisShotAbsorbPct` | 0.2 | 0.15 | −25.0 | Offset the MIRROR buffs on wide and classic (shooters). |
| `AegisRegenPerSecond` | 0.25 | 0.1875 | −25.0 | Back-loaded; trimmed to pull wide MIRROR down. |
| `AegisRageStartTicks` | 900 | 675 | −25.0 | Rage earlier, so it matters in short (tight) fights. it1–it6 used 700. |
| `AegisRageRampTicks` | 600 | 750 | +25.0 | Slower ramp, so long (wide) fights gain less. |
| `TraitTuning.GlassCannonDamageMul[L1]` | 1.5 | 1.55 | +3.3 | GlassCannon floor margin (+3.3 at it6). 1.575 was measured in it7 at +8.9 L1 vs +8.7 L2, which breaks L2 > L1, so 1.55 was used. |

Base ball stats are unchanged: HP 100, radius 16, max speed 10, WRECKER −3 start speed. No mass stat.

Tried and reverted (net 0% change):
- `AegisMaxThickness` 14 → 11 (it2): tight MIRROR fell to 25.7.
- `FangMaxSpin` 40 → 34 (it3): MANTIS 40.0 overall, WRECKER>MANTIS 85 on three arenas.
- `AegisChipDamage` / `AegisDamagePerWidth` shape change (it4): fixed SPORE spike-walls but hurt MIRROR tight.
- `BrawlerWallSpeedBoost` 0.08 → 0.06 (it5): WRECKER crushed, 34.9 overall.
- `VenomDps` / `VenomSeconds`: no measurable effect.

## Iteration table (50 seeds per side, 7 arenas, mirrors excluded)

Note: the per-iteration change lists for it1–it6 come from my working notes. The session context was compacted mid-task,
so intermediate values are summarised, not quoted from a log. Results come from the saved `itN/summary.json`.

| it | changes | weapon-arena min / max | overall | worst pair | side A−B | nominal fails |
|---|---|---|---|---|---|---|
| it0 | Task 1 only (baseline) | tight MIRROR 24.3 / tight MANTIS 69.0 | 48.3–52.7 | tight WRE>MIR 89 | −1.13 | 7: bumpers HIVE 34.9, spike SPORE 65.1, tight MANTIS 69.0, tight SPARKLER 34.6, tight MIRROR 24.3, bumpers SPO>SPA 84, tight WRE>MIR 89 |
| it1 | spike damage, zone, bumper radius, Venom, Volley, Brawler bonus, first MIRROR pass | tight MIR 31.9 / wide MIR 66.4 | 46.0–53.0 | tight WRE>MIR 87 | −0.43 | 4 |
| it2 | + AegisMaxThickness 11 (reverted) | tight MIR 25.7 / wide MIR 67.0 | 46.4–53.7 | tight SPO>MIR 89 | −1.16 | 5 |
| it3 | + FangMaxSpin 34 (reverted) | bumpers MANTIS 30.1 / wide MIR 69.1 | 40.0–56.5 | WRE>MAN 85 (x3) | −0.52 | 9 |
| it4 | MIRROR chip/width shape (reverted later), rage/regen | tight MIR 35.1 / wide MIR 68.4 | 46.9–55.5 | wide MIR>ANG 83 | −0.67 | 4 |
| it5 | + BrawlerWallSpeedBoost 0.06 (reverted) | wide WRE 30.3 / wide MIR 70.1 | 34.9–57.7 | wide MIR>WRE 93 | −0.79 | 13 |
| it6 | best weapon set (Fang 0.36, final MIRROR set, rage 700) | tight MIR 33.9 / wide MIR 67.6 | 46.8–54.5 | tight MAN>SPA 81 | −0.70 | 3: tight MIR 33.9, wide MIR 67.6, tight MAN>SPA 81 |
| val1 | it6 at 200 seeds | orig: tight MIR 33.8 / wide 64.9; holdout: 33.8 / 65.1 | 45.9–54.9 | 81.2 / 80.0 | −0.12 / +0.64 | orig 2, holdout 2; trait gate fails (parry-master L1 +15.3 > 15) |
| it7 | fix iteration: Fang dmg 0.36→0.4, FangSpinGainDecay 0.94, rage 675, GlassCannon L1 1.55 | tight MIR 33.9 / wide MIR 68.1 | 46.0–55.1 | wide MIR>SPO 81 | −0.19 | 5 |
| val2 | it7 at 200 seeds (**final**) | see below | 45.6–55.5 | 79.5 / 83.2 | −0.05 / +0.36 | orig 2, holdout 4 |

Why it7 is final, not it6:
- it6 failed 2 nominal targets on each 200-seed run.
- it6 also failed the Explicit trait gate (parry-master L1 +15.3). Task 3 requires all gates to pass.
- it7 passes all gates. It fails 2 targets on the original seeds and 4 on holdout.
- The spec allows one fix iteration and then one validation, so I stopped there.
- If you prefer weapon spread over the gate, it6 differs only by: FangDamagePerHit 0.36, FangSpinGainDecay 0.98,
  AegisRageStartTicks 700, GlassCannon L1 1.5. With it6, parry-master needs its ceiling raised, and the spec does not
  justify that.

## Final validation (it7), original seeds, 200 per side (`E:\ball-battle-6c-out\final-orig\`)

| weapon | classic | pillar | bumpers | spike-walls | low-gravity | tight | wide | overall |
|---|---|---|---|---|---|---|---|---|
| EMBER | 45.6 | 46.2 | 54.5 | 45.0 | 50.1 | 57.8 | 44.6 | 49.1 |
| MANTIS | 48.1 | 55.4 | 43.3 | 44.3 | 42.8 | 64.4 | 43.9 | 48.9 |
| ANGLER | 48.0 | 44.2 | 57.2 | 46.4 | 50.4 | 58.0 | 47.6 | 50.3 |
| WRECKER | 44.4 | 46.9 | 52.8 | 44.9 | 50.0 | 49.1 | 43.1 | 47.3 |
| SPARKLER | 56.0 | 52.9 | 46.8 | 56.2 | 51.9 | 39.3 | 58.2 | 51.6 |
| SPORE | 42.1 | 40.6 | 50.9 | 50.2 | 43.6 | 52.4 | 39.7 | 45.6 |
| MIRROR | 59.5 | 59.1 | 55.2 | 55.1 | 59.2 | 35.1 | **65.5** | **55.5** |
| HIVE | 56.1 | 54.3 | 39.2 | 57.7 | 51.8 | 43.8 | 57.2 | 51.4 |

Worst pair (both sides): classic MIR>ANG 72.0 · pillar MIR>ANG 70.0 · bumpers WRE>MAN 71.2 · spike-walls SPA>MIR 69.5 ·
low-gravity WRE>MAN 73.5 · tight MAN>SPA 79.5 · wide MIR>ANG 74.8. Side A−B −0.05 pp.

## Final validation (it7), holdout seeds (`-seedOffset 1000`), 200 per side (`E:\ball-battle-6c-out\final-holdout\`)

| weapon | classic | pillar | bumpers | spike-walls | low-gravity | tight | wide | overall |
|---|---|---|---|---|---|---|---|---|
| EMBER | 45.8 | 47.5 | 54.5 | 44.9 | 52.1 | 59.2 | 45.5 | 49.9 |
| MANTIS | 46.6 | 55.0 | 45.4 | 43.9 | 42.8 | **65.6** | 42.4 | 48.8 |
| ANGLER | 47.4 | 45.0 | 57.5 | 46.5 | 49.7 | 57.6 | 46.6 | 50.0 |
| WRECKER | 44.6 | 48.2 | 52.6 | 44.8 | 48.0 | 50.5 | 44.0 | 47.5 |
| SPARKLER | 54.6 | 51.6 | 44.3 | 54.0 | 53.4 | 39.0 | 58.1 | 50.7 |
| SPORE | 42.4 | 40.5 | 51.4 | 50.8 | 44.3 | 50.4 | 40.4 | 45.8 |
| MIRROR | 59.5 | 58.8 | 55.3 | 55.6 | 58.9 | 35.1 | **65.6** | **55.5** |
| HIVE | 59.0 | 53.3 | 38.6 | 59.3 | 50.5 | 42.5 | 57.1 | 51.5 |

Worst pair (both sides): classic WRE>MAN 72.0 · pillar MIR>ANG 72.0 · bumpers SPO>SPA 74.0 · spike-walls WRE>MAN 71.8 ·
low-gravity WRE>MAN 74.0 · tight **MAN>HIVE 83.2** · wide MIR>ANG 78.0. Side A−B +0.36 pp.

## ActiveTicks per arena (median / max)

| arena | orig 200 | holdout 200 | target |
|---|---|---|---|
| classic | 3398 / 7247 | 3383 / 7231 | ✓ |
| pillar | 3298 / 6797 | 3281 / 6434 | ✓ |
| bumpers | 2647 / 5599 | 2652 / 5687 | ✓ |
| spike-walls | 3031 / 7018 | 3033 / 6837 | ✓ |
| low-gravity | 3218 / 10800 | 3234 / 10800 | ✓ (cap = 10800, allowed) |
| tight | 1851 / 5562 | 1852 / 10800 | ✓ (median 1851 is close to the 1800 floor; unchanged from 7df2b1f) |
| wide | 3800 / 10800 | 3794 / 10800 | ✓ |

## Trait deltas (classic, base weapons, 200 seeds; from the Explicit trait gate run)

Spec target: GlassCannon ≥ +3 and no trait below its floor. Gate floors: +3, heavy +1.5.

| trait | L1 | L2 |
|---|---|---|
| heavy | +4.4 | +8.6 |
| vampire | +10.9 | +16.0 |
| spiky | +11.9 | +17.6 |
| thorns | +10.9 | +14.9 |
| second-wind | +12.3 | +16.9 |
| glass-cannon | **+7.0** (was +2.7 at 4a8ef80) | +8.7 |
| parry-master | +14.4 (ceiling 15) | +24.6 |
| bubble | +10.6 | +15.8 |
| twin-blade | +6.0 | +15.9 |
| poison-tip | +10.5 | +20.6 |

All L1 values are within +3..+15 and every L2 > L1. Glass-cannon L2 is only 1.7 above L1. Parry-master is 0.6 under its
ceiling, and that depends on `FangDamagePerHit` (0.36 → +15.3).

## Tests and gates

| suite | result |
|---|---|
| SimTests (`dotnet test`, default set) | **217 total, 217 pass** (114 s); includes 6 new SpikeImmunity cases (A–D, E pillar, E bumper) |
| EditMode (Unity 6000.5.1f1 batchmode) | **280 total, 277 pass, 0 fail, 3 skip** (`CaptureScreens`, `CaptureBb01Screens`, `CaptureFontScreens`: need env vars). Results: `bb6c-editmode.xml` |
| Explicit weapon gate `BalanceThresholdTests` | **3/3 pass** (52 s) |
| Explicit trait gate `TraitBalanceThresholdTests` | **2/2 pass** (416 s) |

- EditMode needed a second run. The first run (280 tests, 276 pass, 1 fail) failed
  `BbFix1aTests.ZoneSegmentsAreOneFifthOfTheCurrentWalls`, which hard-coded the 1/5 zone.
- I renamed it `ZoneSegmentsAreTheirFractionOfTheCurrentWalls`. It now checks against `SpikeZones.ZoneLength` and
  `SpikeZoneNum/SpikeZoneDen`.
- The second run is the one reported above.

**Gate threshold change, weapon gate only.**
- `NonMirrorPairsWinBetween30And70Percent` became `NonMirrorPairsWinBetween20And80Percent`, which mirrors the 6c target
  "no weapon pair above 80% (both sides combined)".
- This drops the temporary 30–70 band and its fang-vs-rig 72% exception, which was marked "tạm, xét lại ở 6c".
- I checked whether the old band would still pass, using `MatchupReport.WinRateMatrix`, 200 seeds, classic. It would fail on 3 pairs:
  - blade vs aegis 27.0 / 73.0
  - pike vs aegis 27.0 / 73.0
  - fang vs brawler 27.5 / 72.5

  All three are inside the 6c target.
- The trait gate thresholds are unchanged: L1 +3..+15, heavy floor +1.5, L2 > L1.

**Goldens regenerated once, for Rules 7 (final tuning):**

- `CrossPlatformGoldenTests.ExpectedBySeed`, seeds 1001–1020, old → new:
  - 1001: D431BAA2B27A7F85 → 811EE019E6FF9EA5
  - 1002: 09B6D8FAB34726B1 → C1EBDB28D8A39134
  - 1003: 9DE2E81275E19CAA → B594B3FC1A893861
  - 1004: 8D34C8605AE00592 → 77292940629549A5
  - 1005: BB96BFE8B8B74FB0 → 6490DE3218A0494B
  - 1006: AFFD9CF6EBF43C3B → A3B0EC99568C8285
  - 1007: 31AAB7BFB1C8432A → 7B5D0CF71CE95692
  - 1008: 17CD3635A56F18EF → 9518F1861721ABC0
  - 1009: BD291ECDF617ECD2 → 208606C6110A95B7
  - 1010: 4C72CBBB5438199C → 2D13DAD3E301EC0D
  - 1011: F18F3DA3E030682B → 3A2DB9CEBB2D1F72
  - 1012: 13C44905533C4034 → EA097C749277748D
  - 1013: A771D79EF8434B06 → B7B4CFCD4E373D85
  - 1014: 39117D5FD64FA57E → CB1A5E6CF7CFB24B
  - 1015: 1EBB04D09D742B79 → B9E763B351ABC249
  - 1016: 616561F76BC4B6F6 → 64C75AB957AD6F77
  - 1017: 18395BBA0F443050 → C1E7633A073D60B4
  - 1018: 3FBCA3642292576F → E5C04579EFC31AD3
  - 1019: 22884B6DAC023333 → 0ED005F15E3BB30B
  - 1020: D75E9E34F512B2BA → 7544B05225D4ECFD
- `VersusGoldenTests.ExpectedDigest`: DA949C380918B81D → 6EBF6B88AB6F72B3.
- `SpikeCornerTests.ExpectedByArena`:
  - classic 7BEA200668ED5694 → 786EB4ED8E600275
  - pillar 9BAA96D99356D870 → 8BAD41929FA62F8D
  - bumpers B76BDBC50DCFB6D0 → 49524358E9163D8B
  - spike-walls 81D75579528D171B → 59CFF559865DCA94
  - low-gravity 265057B248B104E2 → 95930D9564206FD7
  - tight 87CD89A941BEDBEA → CDB2F85B20F4826D
  - wide BBFD43849E96D984 → 322577563C60A74A
- `RunTests.SameSeedAndChoicesReplayTheWholeRun`: run seed 1234 → 1236.
  - Under Rules 7, seed 1234's run ends after 2 fights, below the test's ≥ 6 log-line precondition. 1236 plays 8 fights.
  - The replay assertion itself is unchanged.
- `FixedMathTests` (pure Fix math) is unaffected.

**Determinism:** the 50-seed round-robin ran twice with identical args (`-seeds 50 -arena all`). SHA-256 of all three CSVs
matches:

| file | SHA-256 prefix |
|---|---|
| `rr.csv` | A70EF6A2F199D4C8 |
| `rr-weapons.csv` | 3D7FFCFB620FB7B4 |
| `rr-arenas.csv` | 6F674B265324D513 |

The it7 50-seed CSV from earlier also has the same hash. Copies are in `E:\ball-battle-6c-out\det-a\` and `det-b\`.

## Wall-clock

| run | time (`dotnet test` incl. build) |
|---|---|
| 50-seed round-robin, all 7 arenas | 60–67 s |
| 200-seed round-robin | 213–252 s |
| trait deltas, 2 traits × 2 levels, 200 seeds | ~80 s |

## Files touched

Sim:
- `SimVersion.cs` (Rules 7)
- `Arena/ArenaTuning.cs`
- `Arena/MatchSim.Obstacles.cs`
- `MatchSim.cs`
- `MatchSim.Rules.cs` (hash)
- `MatchSim.Collisions.cs` (`ExtendHitstop`)
- `Weapons/WeaponTuning.cs`
- `Traits/TraitTuning.cs`
- `Tools/RoundRobin.cs` (tool only: `-seedOffset`, arenas CSV)

View:
- `View/HeadlessRoundRobin.cs` (writes the arenas CSV, prints seedOffset)

SimTests:
- new: `SpikeImmunityTests.cs`, `Bb6cDiagnostics.cs` (Explicit diagnostic `WeaponArenaBreakdown`)
- `SpikeCornerTests.cs`
- `CrossPlatformGoldenTests.cs`
- `VersusGoldenTests.cs`
- `RunTests.cs`
- `RoundRobinTests.cs`
- `BalanceThresholdTests.cs`

EditMode:
- `BbFix1aTests.cs`

Docs:
- this file
- `trace-unity-2686071142*.txt` and `trace-unity-4134000519*.txt`: rewritten by the EditMode trace-dump test, whose match traces change under Rules 7
- `bb6c-editmode.xml`
- `bb6c-simtests.trx`

## Deviations from the spec

1. **Targets not all met** (see the top table). I stopped after the single allowed fix iteration and one validation.
2. Round-robins ran via the SimTests `RoundRobinCli` test. It drives the same `RoundRobin` core as the player's `-roundrobin`
   mode, and is faster than booting the player.
3. Tool-only additions:
   - `-seedOffset N` (the seed index becomes `i + N`; holdout used 1000);
   - a per-arena CSV (`*-arenas.csv`) with median and max ActiveTicks.
   No gameplay effect.
4. `ArenaTuning.SpikeWallDamage` changed type from `const int` to `static readonly Fix`, needed for 2.25. Its callers use it as Fix.
5. Spike immunity state is part of `ComputeHash`, so hashes change even on arenas without spikes.
6. An `internal ExtendHitstop` hook was added for test c.
7. Margin reading: the 3 pp stop margin was applied to weapon-arena and pairs as the spec states. Overall 45–55, side ±2 and
   traits were checked without a margin.
8. `GlassCannonDamageMul` L1 was measured at 1.575 and then set to 1.55 inside the fix iteration. This was a trait-only
   check (~40 s), not a second weapon iteration.
9. `E:\ball-battle-6c-out` was not reachable from my file shell. All its reads and writes went through PowerShell.
10. Some older test names still say "Three" or "OneFifth" (`SpikedCornersHurtThreeOthersDoNot`,
    `FixDeterminismTests.SpikeZonesStayOneFifth…`, new `A_…CostsThreeOnce`). Their assertions use the constants; only the names are stale.
11. The diagnostic test file `Bb6cDiagnostics.cs` was added (Explicit, not run by default). Delete it before commit if unwanted.
12. The iteration change lists for it1–it6 are summarised from notes (see the iteration table).

## Confirmation

- Nothing committed, pushed or deleted. No lock file was touched.
- `git rev-parse --short HEAD` = `7df2b1f`.
- `SimVersion.Rules = 7`.
- Final CSVs:
  - `E:\ball-battle-6c-out\final-orig\` and `E:\ball-battle-6c-out\final-holdout\`, each with `rr.csv`, `rr-weapons.csv` and `rr-arenas.csv`;
  - `val1-*` (it6) and all `itN\` runs are kept alongside.
