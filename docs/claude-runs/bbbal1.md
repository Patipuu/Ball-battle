# BB-BAL1: Combined balance pass after skills (Sim values only, Rules 11 → 12)

Base: HEAD `eaf7650` (BB-SFX3, after BB-FIX2 `eb3c8a4`), Rules 11, branch `dev/step3-view`, nothing changed under `BallBattleUnity/Assets` at the start. Ball base stats untouched; all Sim math in Fix; no rule changes. Outputs: `E:\ball-battle-bal1-out\`.

## Outcome

**The accept rule is not met: 4 targets miss after validation, and one of them by more than 3 pp.**

| target | result | miss |
|---|---|---|
| Greedy run 25–40% | **22.6%** (1000 runs; the 800-run test 22.9%) | −2.4 pp |
| No player weapon below 25% vs any boss (forced-weapon Greedy, 125 runs each) | **11.1%**, MIRROR vs WARDEN (only 9 runs reached it) | −13.9 pp |
| Traits L1 within +3..+15 | parry-master **+15.5** | +0.5 pp |
| No Versus pair above 80% | original ✓ (worst 79.2); holdout: WRECKER vs MANTIS classic **80.2** | +0.2 pp (holdout only) |

Everything else is in its band:
- **Versus (both seed sets):** every weapon 35–65% per arena, 45–55% overall, side bias ≤ 2 pp, timeouts ≤ 1%, median ActiveTicks 1800–6000.
- **Bots:** Random 3.1%.
- **Bosses (with retry):** SHELL KING 73.6%, MAGNET 75.0%, MIMIC 62.5%, WARDEN 67.8%.
- **Fixed:** five of the six known misses (all but parry-master). WRECKER vs MANTIS was ~85%, now 79.2% (holdout 80.2%); WRECKER overall 59%, now 52.7%; MIRROR overall 42.8%, now 51.3%; WARDEN 82.4%, now 67.8%; MAGNET 77.0%, now 75.0%.

**Why Greedy and the weapon-vs-boss floor miss:**
- Greedy starts with one of the base four weapons. At HEAD, WRECKER carried it: WRECKER runs won 59%, the other three 14–21%.
- Bringing WRECKER into the Versus band (60.4% → 52.7%) cost Greedy about 6.6 pp (it0 27.4% → it1 20.8%). The level 2–3 skill buffs (Run only) and the boss values got some of that back, but not all.
- SPORE and MIRROR are balanced in Versus but weak in Run: their runs win 3–4%, and they rarely reach fight 8 (4–9 runs out of 125). So their late-boss cells are both low and very noisy.
- The boss gate's own proxy, which arrives at full HP with 100 seeds per cell, puts every weapon at **≥ 44%** against every boss.

## TASK 1: Greedy floor restored

- `RunDifficultyThresholdTests`: `GreedyBotWins22To40Percent` → `GreedyBotWins25To40Percent`, band 22–40 → **25–40**; "temporary floor" comment removed.
- `BossBalanceThresholdTests.RunBotsStayInTheirBands`: Greedy band 22–40 → **25–40**; comment removed.

## TASK 2: Greedy branch choice by measurement (RunBot only)

`RunBot.GreedyBranch`, used for every Greedy pick:
- Only the level-1 SKILL UP chooses a branch; later levels keep it.
- The bot clones its current build, applies the card with branch A and then with branch B, and plays each against the next fight's known enemy on that fight's arena.
- It uses 8 seeds, `RunTuning.Derive(runSeed, 7000 + fightIndex, k)`.
- More wins decides; on a tie, less total damage taken; on a second tie, A.
- Deterministic, with no shared random state.
- `SkillChoice` (Sim) is untouched; RunBot no longer uses it.

**A/B split at the level-1 SKILL UP** (forced-weapon Greedy, 125 runs each, final values):

| weapon | before (fixed score rule) | now A / B |
|---|---|---|
| EMBER (blade) | always B | 18 / 73 |
| MANTIS (fang) | always A (tie) | 39 / 54 |
| ANGLER (pike) | always A (tie) | 49 / 41 |
| WRECKER (brawler) | always B | 70 / 33 |
| SPARKLER (volley) | always B | 51 / 47 |
| SPORE (venom) | always A | 62 / 24 |
| MIRROR (aegis) | always A | 60 / 25 |
| HIVE (rig) | always B | 50 / 50 |
| Greedy snapshot (1000 runs) | – | 354 / 402 |

## Values changed (old → new, % vs HEAD) and why

| value | HEAD | new | % | why |
|---|---|---|---|---|
| `WeaponTuning.BrawlerStartDamagePerSpeed` | 0.1 | 0.09 | −10% | WRECKER overall 60.4 / vs MANTIS ~85 |
| CHARGE body multiplier, L1 and B branch (`SkillState.BodyMultiplier`) | 1.5 | 1.35 | −10% | same |
| CHARGE A branch, L2 / L3 | 1.75 / 2 | 2.0125 / 2.3 | +15% | Run only: Greedy floor |
| CHARGE B duration per rank | 60 | 69 | +15% | Run only: Greedy floor |
| `WeaponTuning.FangStartDamage` | 1 | 1.15 | +15% | MANTIS overall 42.2 / vs WRECKER |
| `WeaponTuning.FangDamagePerHit` | 0.4 | 0.44 | +10% | same |
| FRENZY A duration per rank | 90 | 103 | +14.4% | Run only: Greedy floor |
| FRENZY B spin-gain multiplier L2 / L3 | 1.5 / 2 | 1.725 / 2.3 | +15% | Run only |
| `WeaponTuning.VolleyArrowDamage` | 0.9 | 0.86 | −4.4% | SPARKLER overall 57.8 (−7.8% overshot: tight 34.9) |
| `WeaponTuning.AegisChipDamage` | 0.7 | 0.8 | +14.3% | MIRROR overall 41.8 |
| `WeaponTuning.AegisShotAbsorbPct` | 0.15 | 0.17 | +13.3% | MIRROR vs SPARKLER / HIVE |
| `WeaponTuning.AegisMendPerGuard` | 0.12 | 0.138 | +15% | MIRROR vs SPORE (83–89%) |
| `WeaponTuning.AegisRageStartGuards` | 18 | 15.5 | −13.9% | same |
| BULWARK A duration per rank | 40 | 46 | +15% | Run only: MIRROR vs bosses |
| BULWARK B parry reflect L2 / L3 | 2/3 / 3/4 | 23/30 / 69/80 | +15% | Run only |
| `WeaponTuning.VenomMeleeDamage` | 1.5 | 1.65 | +10% | SPORE overall 44.4 |
| DRAIN A fraction L2 / L3 | 2/3 / 3/4 | 23/30 / 69/80 | +15% | Run only: SPORE vs bosses |
| ROT B poison extension per rank | 60 | 69 ticks | +15% | Run only (see Deviation 4) |
| WHETSTONE B hit bonus (L2 / L3) | Level/2 | Level × 23/40 | +15% | Run only: Greedy floor |
| LUNGE A length bonus (L2 / L3) | (Level+1)/6 | (Level+1) × 23/120 | +15% | Run only: Greedy floor |
| LUNGE B duration per rank | 60 | 69 | +15% | Run only |
| `WeaponTuning.RigShotDamage` | 1.1 | 1.15 | +4.5% | HIVE bumpers 34.9 (+9.1% overshot: HIVE vs EMBER wide 82) |
| `BossTuning.ShellKingHp` | 150 | 156 | +4% | SHELL KING 80–81% after the other changes |
| `BossTuning.WardenHp` | 207 | 230 | +11.1% | WARDEN 82.4% (250 overshot to 61.8%; 220 gave 73.5%) |
| `BossTuning.WardenSpikeDamage` | 2 | 3 | (range 2–3) | same |
| `SimVersion.Rules` | 11 | 12 | – | values changed outcomes |

Tried in iterations and returned to the HEAD value: `MagnetHp` (245 / 235 → 225), `MimicHp` (330 / 310 / 290 → 275), `VenomDps` (1.1 / 1.05 → 1).

Unchanged: all trait values (parry-master's only value is an integer, so it can't move within ±25%; see Deviation 7), `SpikeWindowTicks` 144, and every other value.

"Run only" means a level-2 or level-3 skill value. Versus, the trait gate and the weapon gate play skills at level 1, so these values don't affect them.

## Iteration table (Versus 50 seeds per side; Run 500 runs per bot + forced Greedy 125 runs per weapon)

| it | changes | Versus nominal misses | Greedy | Random | SHELL / MAGNET / MIMIC / WARDEN (retry) | weapon vs boss min | parry-master |
|---|---|---|---|---|---|---|---|
| it0 (HEAD + TASK 1/2) | – | 11: WRECKER 60.4, SPARKLER 57.8, MANTIS 42.2, SPORE 44.4, MIRROR 41.8 overall; HIVE bumpers 34.9; WRECKER>MANTIS 84–87 on 5 arenas | 27.4 | 4.6 | 76.4 / 72.8 / 75.0 / **81.6** | 16.7 (MIRROR vs WARDEN) | +15.3 |
| it1 | WRECKER −10% ×2, MANTIS +15/+10%, SPARKLER −7.8%, MIRROR chip/absorb, SPORE melee/DPS +10%, WARDEN HP 250 + spike 3, MIMIC 330, MAGNET 245 | SPORE>MIRROR 89/83, SPARKLER>MIRROR 82, HIVE bumpers 32.4 | **20.8** | 3.0 | **81.0** / 66.9 / 59.0 / 61.8 | 0.0 (SPORE vs WARDEN, n=9) | +15.5 |
| it2 | Run-only L2/L3 skill +15% (blade, fang, pike, venom, aegis), MIRROR mend +15%, HIVE shot +9.1%, bosses eased (SHELL 156, MAGNET 235, MIMIC 310, WARDEN 235) | SPARKLER tight 34.9, SPORE>MIRROR 87/83, HIVE>EMBER wide 82, tight median 1794 | 20.4 | 1.6 | **80.2** / 69.9 / 60.2 / 66.7 | 0.0 (SPORE vs WARDEN) | +15.5 |
| it3 | WRECKER Run-only skill +15%, SPORE DPS 1.05, HIVE shot 1.15, SPARKLER arrow 0.86, MIRROR rage start 15.5, MAGNET 225, MIMIC 290, WARDEN 220 | SPORE>MIRROR spike-walls 83 | 22.8 | 3.2 | 78.5 / 76.2 / 57.6 / **73.5** | 11.1 (MIRROR vs WARDEN) | +15.5 |
| it4 (final) | SPORE DPS 1, MIMIC 275, WARDEN 230 | **none** | 23.4 | 3.2 | 77.3 / 74.8 / 62.4 / 70.2 | 11.1 (MIRROR vs WARDEN) | (inputs unchanged → +15.5) |

## Validation (once; final values, Rules 12)

### Versus, 200 seeds per side: original seeds and holdout (`-seedOffset 1000`, an existing flag)

| weapon | overall (orig) | overall (holdout) | per-arena range (orig) | per-arena range (holdout) |
|---|---|---|---|---|
| EMBER | 48.8 | 49.0 | 44.2–55.0 | 45.3–54.0 |
| MANTIS | 46.8 | 47.3 | 41.0–58.8 | 40.6–58.6 |
| ANGLER | 51.8 | 50.9 | 46.9–57.0 | 47.1–57.0 |
| WRECKER | 52.7 | 52.8 | 48.5–54.7 | 46.9–58.4 |
| SPARKLER | 52.6 | 51.8 | **35.8**–60.8 | 37.8–60.9 |
| SPORE | 45.0 | 45.2 | 39.8–50.4 | 40.5–50.6 |
| MIRROR | 51.3 | 52.1 | 45.8–59.1 | 47.8–59.6 |
| HIVE | 50.8 | 50.5 | 37.0–58.9 | **37.0**–58.8 |

Worst pair per arena:

| arena | original | holdout |
|---|---|---|
| classic | WRECKER>MANTIS 79.2 | WRECKER>MANTIS **80.2** |
| pillar | HIVE>SPORE 70.5 | WRECKER>MANTIS 69.2 |
| bumpers | MIRROR>HIVE 78.5 | WRECKER>MANTIS 79.8 |
| spike-walls | WRECKER>MANTIS 78.5 | WRECKER>MANTIS 77.5 |
| low-gravity | WRECKER>MANTIS 77.8 | WRECKER>MANTIS 77.5 |
| tight | MANTIS>SPARKLER 78.5 | MANTIS>SPARKLER 77.2 |
| wide | WRECKER>MANTIS 78.8 | WRECKER>MANTIS 79.5 |

Timeouts per arena (draws, an upper bound for time-capped matches, of 12,800 matches each):

| arena | original | holdout |
|---|---|---|
| classic | 0.04% | 0.08% |
| pillar | 0.11% | 0.05% |
| bumpers | 0.14% | 0.07% |
| spike-walls | 0.09% | 0.07% |
| low-gravity | 0.08% | 0.08% |
| tight | 0.05% | 0.16% |
| wide | 0.08% | 0.09% |

- Side bias: −0.36 pp (original), +0.35 pp (holdout).
- Median ActiveTicks: 1808–3701 (original), 1811–3711 (holdout).
- Over 80% before (HEAD, 50 seeds): WRECKER>MANTIS 85 / 78 / 85 / 84 / 87 / 86 on classic / pillar / bumpers / spike-walls / low-gravity / wide.

### Run, 1000 runs per bot (original snapshot: BotProgress, seeds 1–1000)

Greedy **22.6%** (the 800-run test: 22.9%); Random **3.1%**.

| boss | Greedy first try | Greedy with retry | target | Random first try | Random with retry |
|---|---|---|---|---|---|
| SHELL KING | 63.8 | **73.6** | 45–80 ✓ | 42.1 | 53.8 |
| MAGNET | 66.1 | **75.0** | 45–77 ✓ | 46.2 | 56.8 |
| MIMIC | 58.5 | **62.5** | 35–70 ✓ | 51.2 | 53.5 |
| WARDEN | 58.5 | **67.8** | 35–70 ✓ | 12.5 | 16.7 |

Weapon vs boss, forced-weapon Greedy (all 8 weapons, 125 runs each; % beaten including retry, per run that met the boss; n = runs that met it):

| weapon | run win % | SHELL KING | MAGNET | MIMIC | WARDEN |
|---|---|---|---|---|---|
| EMBER | 16.0 | 72.7 (33) | 82.4 (34) | 43.5 (23) | 52.6 (19) |
| MANTIS | 16.8 | 73.3 (30) | 80.0 (30) | 52.4 (21) | 66.7 (15) |
| ANGLER | 15.2 | 63.2 (38) | 84.6 (26) | 52.4 (21) | 61.5 (13) |
| WRECKER | 44.0 | 93.5 (46) | 84.6 (39) | 73.9 (46) | 72.4 (29) |
| SPARKLER | 17.6 | 65.9 (41) | 74.1 (27) | 65.2 (23) | 50.0 (14) |
| SPORE | 4.0 | 39.3 (28) | 48.0 (25) | 44.4 (9) | 25.0 (4) |
| MIRROR | 3.2 | 50.0 (26) | 41.7 (36) | **21.4** (14) | **11.1** (9) |
| HIVE | 20.0 | 67.4 (46) | 67.6 (34) | 67.9 (28) | 28.6 (21) |

- **Minimum: 11.1%, MIRROR vs WARDEN** (n = 9). Also under 25%: MIRROR vs MIMIC 21.4%.
- The boss gate's proxy (`DuelAtSlot`: arrives at full HP, 100 seeds per cell) has a minimum of **44%** (SPORE vs WARDEN) and passes `NoWeaponBelow25PercentAgainstAnyBoss`.

### Boss warn lead times (ActiveTicks from the `_warn` cue's event to the event it warns of)

| boss | warn → event | lead | changed? |
|---|---|---|---|
| SHELL KING | ShellWarn @60 → ShellOn @90 | **30** | no |
| MAGNET | MagnetWarn @120 → MagnetPull @180 | **60** | no |
| WARDEN | WardenWarn @390 → WardenStep @450 | **60** | no |

### Trait gate values (200 seeds; band L1 +3..+15, heavy +1.5..+15; L2 > L1)

| trait | L1 | L2 |
|---|---|---|
| heavy | +5.4 | +11.4 |
| vampire | +9.8 | +13.9 |
| spiky | +11.5 | +17.3 |
| thorns | +9.7 | +17.3 |
| second-wind | +11.7 | +16.4 |
| glass-cannon | +7.0 | +9.3 |
| parry-master | **+15.5** | +25.1 |
| bubble | +10.8 | +14.8 |
| twin-blade | +6.2 | +16.6 |
| poison-tip | +10.2 | +22.3 |

## Tests

| suite | result |
|---|---|
| SimTests (default) | **320 total, 319 pass, 1 fail**. Band miss `RunDifficultyThresholdTests.GreedyBotWins25To40Percent`: N=800, measured **22.9%** (25–40). All functional tests pass |
| EditMode | **not run**: the Unity Editor is open on this project and holds `Temp/UnityLockfile` (Deviation 10) |
| Explicit weapon gate | **3/3 pass** |
| Explicit boss gate (+ BossSweep, 1000 runs) | **8/9**. Band miss `RunBotsStayInTheirBands`: Greedy **22.6%** (25–40). All four boss bands, the per-arena WARDEN floor, the weapon floor (proxy) and timeouts pass |
| Explicit trait gate | **1/2**. Band miss `EveryTraitAtLevel1AddsThreeToFifteenPoints`: parry-master **+15.5** |
| Goldens | re-pinned once at the end; the 11 golden tests pass |
| Determinism | two 50-seed round-robins byte-identical, SHA-256 `31202B762643EBB49E11F0F1B841A9104CE1ADACCB4BBD931FEBAC06A645B598`; also identical to the it4 run |

Goldens re-pinned:
- `CrossPlatformGoldenTests.ExpectedBySeed` (rules12): seed 1001 `0x59396B36A67AF523` → `0xEF52DEA026BF8E54`.
- `ExpectedBySeedMasked`: seed 1001 `0xB08A3E8532EB75EB` → `0x556BAF253A2154FC`.
- `VersusGoldenTests`: `0x49966C16672E04CD` → `0x39B83A07907096FF`.
- `SpikeCornerTests` arena digests:

| arena | old | new |
|---|---|---|
| classic | `0x7086F4ABD59EE32B` | `0x0B58C2EAD389BF51` |
| pillar | `0xE71BE338CA076227` | `0xC84B23394E4702B6` |
| bumpers | `0x874AC199717945E3` | `0xF9A14CAB0C72A98D` |
| spike-walls | `0x27C4EED9159FC1D1` | `0x74530116D1B32E7A` |
| low-gravity | `0x98EC101962AC8887` | `0x6DD30CD8AC3D7FCB` |
| tight | `0x1684331AD46763C3` | `0x2F7B7E1E52DCB7B3` |
| wide | `0x7FAE7C234750222B` | `0xEE6C04BC717E9420` |

## Deviations

1. **Accept rule not met** (Outcome). I stopped after validation as specified, with no further iterations.
2. **Skill values have no tuning class.** They are inline literals in `SkillState.cs`, so I edited those literals.
   - `SkillTests` pins the authored amounts, so 20 expectations were updated to the new values (durations, branch amounts, Frenzy ×2.3, Charge ×1.35, Rot 300 ticks).
   - These are functional pins of the values, updated like goldens; no behaviour test was loosened.
3. **Level-2/3 skill buffs (+15%) were my Run-only lever.** Versus and the gates play level 1 and aren't affected. The level-1 values changed only for WRECKER's CHARGE multiplier (−10%).
4. **ROT poison extension:** the value moved +15% (60 → 69 ticks per rank). `ApplyPoison` rounds durations up to whole seconds, so in play an applied poison lasts 1 s longer at L2 and L3 (+25–33% effective). Validation already included it, and no more iterations were allowed, so it stays; flagged for review.
5. **Forced-weapon Greedy runs** use the bot's usual progress (base traits unlocked) and seeds 1–125. They swap the start weapon before the first pick, as the boss sweep's `DuelAtSlot` does, because `RunState.Start` only accepts a weapon from that run's start choices.
   - New Explicit report `SimTests/BbBal1Report.cs` (Run report, forced runs, A/B split, warn leads, trait table).
   - New `RunBot.Play(…, forcedWeapon)` overload.
6. **Small samples** for weak weapons at fight 8: MIRROR vs WARDEN has n = 9, SPORE vs WARDEN n = 4. The 25% floor is mostly decided by how often those weapons survive to fight 8.
7. **parry-master** has one value, `ParryMasterEvery` = 2 (an integer: every 2nd parry). ±25% allows only 1.5–2.5, so 2 is the only legal integer.
   - Its delta comes from the base weapons' growth per hit. Nerfing that would break Versus, so the +15.5 stays.
8. **it4 skipped the trait table.** Its changes (SPORE DPS, boss HP) don't touch the four base weapons at level 1 that the trait measurement uses. The final trait table and the gate both measured +15.5, the same as it1–it3.
9. **Holdout** used the existing `-seedOffset` flag (1000); no new flag.
10. **EditMode was not run.** Your Unity Editor is open on `E:\ball-battle\BallBattleUnity` (Hub session, with import workers) and holds `Temp/UnityLockfile`.
    - A batchmode run can't open a locked project, and the open editor has no MCP bridge to run tests through.
    - I asked at 00:25 and didn't close the editor or touch the lockfile.
    - No View code changed in this task. The EditMode tests that touch the changed values (`Bb6dTests`, WARDEN spike damage) read the tuning constants, not literals, so no break is expected.
    - Please run EditMode once the editor is closed.
11. **Untracked files outside `Assets` from this task:**
    - `SimTests/BbBal1Report.cs`.
    - Outputs in `E:\ball-battle-bal1-out\`.

## Confirmation

- Nothing committed, pushed, stashed or deleted, including `Temp/UnityLockfile`, lock files and out folders.
- HEAD unchanged: **`eaf7650`**; `git stash list` empty.
- `SimVersion.Rules` = **12**.
