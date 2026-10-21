# BB-BAL2: SPORE (venom) / MIRROR (aegis) balance pass

Branch `dev/step3-view`, base `b2f9fad`. **Not committed.** Rules 12 → 13.
Seeds: `docs/claude-runs/bbbal2/seeds.txt`, fixed before any change and reused for every run:
- Run: seeds 1–300, Greedy RunBot, start weapon forced, all 8 weapons (2,400 runs).
- Versus: indexes 0–99 × 7 arenas × both seat orders, so 1,400 matches per pair.

Harness: `SimTests/BbBal2Report.cs` (`[Explicit] Measure`). Raw output: `bbbal2/baseline.*`, `bbbal2/it1.*`, `bbbal2/it2.*` (it2 is final).

## Summary against the targets

| target | result | |
|---|---|---|
| SPORE Run win 8–12 % | 3.0 → **3.0** | ✗ |
| MIRROR Run win 8–12 % | 5.0 → **6.0** | ✗ |
| No other weapon's Run win drops > 3 pts | worst: pike −2.3 | ✓ |
| No SPORE vs non-MIRROR pair moves > 5 pts toward SPORE | blade +6.1, pike +5.4, brawler +7.4, volley +6.0, rig +5.1 (fang +4.0) | ✗ (small) |
| No SPORE vs non-MIRROR pair ends above 65 % for SPORE | highest is 54.1 % (fang) | ✓ |
| No pair with SPORE or MIRROR ends worse than 35/65 | SPORE v MIRROR 69.4/30.6 (baseline 63.3); brawler v MIRROR 33.4 (unchanged from baseline); pike v MIRROR 35.2 (just inside) | ✗ |
| No 45–55 pair leaves 40–60 | none | ✓ |
| Determinism tests pass | all pass | ✓ |

**Bottom line:** both iterations were used and the Run targets are not met. The allowed Versus-side levers barely move Run win rate:
- SPORE stays at 3.0%. Its average fights won rises from 2.42 to 2.73.
- MIRROR goes from 5.0% to 6.0%.

Meanwhile SPORE's poison +10% moves SPORE's Versus results by +4 to +7.4 points. Section 9 has the recommendation.

Sampling noise:
- Run win % over 300 runs has a standard error of about 1–3 points.
- A Versus pair over 1,400 matches has a standard error of about 1.3 points.

## 1. Baseline vs final

**Run (300 forced-weapon Greedy runs per weapon, same seeds)**

| weapon | win % base | it1 | final (it2) | Δ final | avg fights won base → final | died at fight 1..8 base | final |
|---|---|---|---|---|---|---|---|
| blade | 16.7 | 16.0 | 16.0 | -0.7 | 3.36 → 3.27 | 67 51 28 45 12 2 6 39 | 68 53 29 46 11 2 6 37 |
| fang | 15.3 | 14.0 | 14.0 | -1.3 | 3.29 → 3.10 | 64 63 26 35 13 6 11 36 | 69 66 32 27 14 6 10 34 |
| pike | 15.3 | 13.0 | 13.0 | -2.3 | 3.49 → 3.31 | 59 54 29 42 11 3 10 46 | 62 57 28 44 12 6 7 45 |
| brawler | 41.0 | 36.0 | 39.3 | -1.7 | 4.84 → 4.72 | 41 41 16 37 3 0 2 37 | 43 42 16 39 4 0 2 36 |
| volley | 17.7 | 16.3 | 16.3 | -1.3 | 3.49 → 3.41 | 55 57 26 49 13 2 10 35 | 58 57 26 50 11 2 10 37 |
| SPORE (venom) | 3.0 | 3.0 | 3.0 | +0.0 | 2.42 → 2.73 | 75 60 33 71 13 3 5 31 | 61 59 30 75 13 6 9 38 |
| MIRROR (aegis) | 5.0 | 6.0 | 6.0 | +1.0 | 2.85 → 2.98 | 67 57 32 64 5 3 12 45 | 68 52 28 65 7 3 10 49 |
| rig | 19.7 | 20.3 | 20.3 | +0.7 | 3.68 → 3.71 | 58 42 31 52 9 1 4 44 | 59 43 31 47 9 1 4 45 |

Boss beaten % (runs that reached the boss and beat it), base → final:

| weapon | shellking | magnet | mimic | warden |
|---|---|---|---|---|
| blade | 71.0 → 69.7 | 70.6 → 69.0 | 53.2 → 50.0 | 59.5 → 64.1 |
| fang | 72.2 → 79.1 | 80.0 → 80.3 | 49.0 → 53.3 | 66.7 → 58.1 |
| pike | 66.3 → 63.8 | 81.3 → 79.5 | 44.9 → 43.5 | 55.8 → 50.0 |
| brawler | 88.5 → 86.3 | 74.5 → 74.2 | 76.9 → 77.3 | 76.8 → 75.8 |
| volley | 65.9 → 65.0 | 73.8 → 72.2 | 62.7 → 59.6 | 56.8 → 52.9 |
| SPORE (venom) | 46.2 → 51.4 | 46.3 → 48.7 | 26.1 → 21.4 | 17.6 → 15.8 |
| MIRROR (aegis) | 61.2 → 62.0 | 50.6 → 53.1 | 29.7 → 34.2 | 17.4 → 17.2 |
| rig | 73.6 → 77.6 | 64.6 → 65.9 | 67.9 → 67.2 | 44.7 → 45.8 |

## 2. SPORE vs MIRROR (Step 0.2) and the DRAIN relaxation

At baseline, **"MIRROR hard-counters SPORE" is false; the reverse is true**:
- **Versus:** SPORE wins 63.3% (61.4% as ball 0, 65.1% as ball 1).
- **Run:** a SPORE player beats a MIRROR enemy 83.8% of the time (n = 130).

DRAIN fires **1.32 times per match against MIRROR in Versus**, more than the 1.14 average across all opponents and the second highest after brawler. In Run it fires 1.19 times, also above the 1.17 average. DRAIN is not rare against MIRROR, so per the rule **the MIRROR-only 3 → 2 DRAIN relaxation was not applied**. The threshold stays at 3 stacks for every opponent.

Poison stacks on MIRROR while DRAIN is off cooldown and waiting:
- Mean: 0.68 stacks.
- 2 or more stacks on 13% of waiting ticks; 3 on 0.16%.
- The 3-stack moments are short but frequent enough to fire about once per match.

Reflected damage is about 27% of the damage SPORE takes from MIRROR (26.7% / 27.1% Versus, 26.4% Run).

Detail, baseline → final:

**SPORE vs MIRROR detail**

| context | matches | spore_win_pct | fires_per_match | mean_stacks_ready | ready_ge2_pct | ready_ge3_pct | damage_taken_per_match | reflect_share_pct |
|---|---|---|---|---|---|---|---|---|
| versus SPORE ball0 | 700.00 → 700.00 | 61.43 → 67.57 | 1.29 → 1.24 | 0.68 → 0.67 | 13.03 → 12.95 | 0.16 → 0.16 | 84.08 → 80.53 | 26.68 → 28.99 |
| versus SPORE ball1 | 700.00 → 700.00 | 65.14 → 71.29 | 1.35 → 1.30 | 0.69 → 0.69 | 13.79 → 13.77 | 0.17 → 0.17 | 82.85 → 79.27 | 27.13 → 29.46 |
| run SPORE player vs MIRROR enemy | 130.00 → 134.00 | 83.85 → 85.82 | 1.19 → 1.13 | 0.69 → 0.68 | 13.70 → 13.53 | 0.16 → 0.16 | 76.98 → 70.92 | 26.41 → 29.32 |

After the pass, MIRROR's reflect share against SPORE rises to about 29%. SPORE's +10% poison still wins out, so the pair widens from 63.3 to 69.4 for SPORE.

## 3. Exact numeric changes (final diff)

| file:line | value | old | new |
|---|---|---|---|
| `Assets/Scripts/Sim/Weapons/WeaponTuning.cs:67` | `VenomDps` (SPORE poison dps per stack) | `1` | `Fix.Ratio(11, 10)` = 1.1 (+10%) |
| `Assets/Scripts/Sim/Weapons/WeaponTuning.cs:79` | `AegisReflectPct` (MIRROR parry reflect, no skill active) | `Fix.Ratio(9, 20)` = 0.45 | `Fix.Ratio(99, 200)` = 0.495 (+10%) |
| `Assets/Scripts/Sim/Skills/SkillState.cs:38` | `ParryReflect`: L1 and branch A | `Fix.Ratio(3, 5)` = 0.6 | `Fix.Ratio(33, 50)` = 0.66 (+10%) |
| `Assets/Scripts/Sim/Skills/SkillState.cs:38` | `ParryReflect`: L2 B | `Fix.Ratio(23, 30)` = 0.7667 | `Fix.Ratio(253, 300)` = 0.8433 (+10%) |
| `Assets/Scripts/Sim/Skills/SkillState.cs:38` | `ParryReflect`: L3 B | `Fix.Ratio(69, 80)` = 0.8625 | `Fix.Ratio(759, 800)` = 0.94875 (+10%) |
| `Assets/Scripts/Sim/SimVersion.cs:16` | `Rules` | `12` | `13` (the old line is kept as a history comment) |

Iteration history:
- **Iteration 1** applied the levers above plus MIRROR **body** reflect ×1.1:
  - `WeaponTuning.cs:76` `AegisBodyReflectPct` 9/16 → 99/160.
  - `AegisRule.cs:61` active-skill body reflect 3/4 → 33/40.

  Body reflect only changes the brawler matchups. It pushed brawler v MIRROR from 33.4 to **21.0** and brawler's Run win rate down 5.0 points, both well outside the guard rails.
- **Iteration 2 (one adjustment):** body reflect reverted to 9/16 and 3/4. Neither file has a net diff. All other values stay. Brawler v MIRROR is back to 33.4 and brawler's Run drop is −1.7.

Levers not used, and why:
- **DRAIN relaxation:** not eligible (section 2).
- **Poison stack duration:** `VenomSeconds` is a whole-second int (4). `ApplyPoison` rounds durations to whole seconds, so a ≤10% change is impossible. The only poison lever is dps.
- **MIRROR HP / contact damage:** lever 5 was held back. Iteration 2's single adjustment had to fix the brawler guard-rail breach.
- **Shield arc** ±60° and **blocked-melee-does-not-grow** (BB-FIX2): untouched.
- **Consistency:** the levers apply equally to every skill level and branch. No branch was singled out as dead.

## 4. Versus matrix before / after

**Versus matrix, baseline: row weapon win % vs column (1,400 matches per pair, 7 arenas)**

| | blade | fang | pike | brawler | volley | venom | aegis | rig | overall |
|---|---|---|---|---|---|---|---|---|---|
| **blade** | - | 55.1 | 46.1 | 49.6 | 45.0 | 54.9 | 48.9 | 42.0 | 48.8 |
| **fang** | 44.7 | - | 43.0 | 28.3 | 54.2 | 49.9 | 50.4 | 59.1 | 47.1 |
| **pike** | 53.9 | 57.0 | - | 51.1 | 50.4 | 62.2 | 41.4 | 46.6 | 51.8 |
| **brawler** | 50.2 | 71.7 | 48.9 | - | 47.1 | 55.9 | 33.4 | 56.9 | 52.0 |
| **volley** | 55.0 | 45.8 | 49.6 | 52.9 | - | 61.5 | 57.1 | 49.4 | 53.1 |
| **venom** | 45.1 | 50.1 | 37.8 | 44.1 | 38.5 | - | 63.3 | 39.4 | 45.4 |
| **aegis** | 50.7 | 49.6 | 58.4 | 66.4 | 42.8 | 36.7 | - | 53.0 | 51.1 |
| **rig** | 58.0 | 40.9 | 53.4 | 43.1 | 50.6 | 60.6 | 46.9 | - | 50.5 |

**Versus matrix, final (it2): row weapon win % vs column (1,400 matches per pair, 7 arenas)**

| | blade | fang | pike | brawler | volley | venom | aegis | rig | overall |
|---|---|---|---|---|---|---|---|---|---|
| **blade** | - | 55.1 | 46.1 | 49.6 | 45.0 | 48.7 | 45.1 | 42.0 | 47.4 |
| **fang** | 44.7 | - | 43.0 | 28.3 | 54.2 | 45.8 | 47.1 | 59.1 | 46.0 |
| **pike** | 53.9 | 57.0 | - | 51.1 | 50.4 | 56.9 | 35.2 | 46.6 | 50.2 |
| **brawler** | 50.2 | 71.7 | 48.9 | - | 47.1 | 48.4 | 33.4 | 56.9 | 50.9 |
| **volley** | 55.0 | 45.8 | 49.6 | 52.9 | - | 55.5 | 56.6 | 49.4 | 52.1 |
| **venom** | 51.2 | 54.1 | 43.1 | 51.5 | 44.5 | - | 69.4 | 44.4 | 51.2 |
| **aegis** | 54.7 | 52.9 | 64.8 | 66.4 | 43.3 | 30.5 | - | 54.7 | 52.5 |
| **rig** | 58.0 | 40.9 | 53.4 | 43.1 | 50.6 | 55.6 | 45.2 | - | 49.5 |

Deltas (final − baseline). Pairs without SPORE or MIRROR are exactly unchanged (+0.0), as expected for a values-only pass on two weapons:

| | blade | fang | pike | brawler | volley | venom | aegis | rig | overall |
|---|---|---|---|---|---|---|---|---|---|
| **blade** | - | +0.0 | +0.0 | +0.0 | +0.0 | -6.1 | -3.8 | +0.0 | -1.4 |
| **fang** | +0.0 | - | +0.0 | +0.0 | +0.0 | -4.1 | -3.3 | +0.0 | -1.1 |
| **pike** | +0.0 | +0.0 | - | +0.0 | +0.0 | -5.4 | -6.2 | +0.0 | -1.7 |
| **brawler** | +0.0 | +0.0 | +0.0 | - | +0.0 | -7.6 | +0.0 | +0.0 | -1.1 |
| **volley** | +0.0 | +0.0 | +0.0 | +0.0 | - | -6.0 | -0.5 | +0.0 | -0.9 |
| **venom** | +6.1 | +4.0 | +5.4 | +7.4 | +6.0 | - | +6.1 | +5.1 | +5.7 |
| **aegis** | +4.0 | +3.3 | +6.4 | +0.0 | +0.5 | -6.2 | - | +1.7 | +1.4 |
| **rig** | +0.0 | +0.0 | +0.0 | +0.0 | +0.0 | -5.1 | -1.7 | - | -1.0 |

Pairs involving SPORE or MIRROR, plus any pair flagged by the checks:

**Pair checks**

| pair | base | it1 | final | check |
|---|---|---|---|---|
| blade v venom | 54.9 | 48.7 | 48.7 | SPORE +6.1 → 51.2 ✗ >5 |
| blade v aegis | 48.9 | 45.1 | 45.1 | ✓ |
| fang v venom | 49.9 | 45.8 | 45.8 | SPORE +4.0 → 54.1 ✓ |
| fang v aegis | 50.4 | 47.1 | 47.1 | ✓ |
| pike v venom | 62.2 | 56.9 | 56.9 | SPORE +5.4 → 43.1 ✗ >5 |
| pike v aegis | 41.4 | 35.2 | 35.2 | ✓ |
| brawler v venom | 55.9 | 48.4 | 48.4 | SPORE +7.4 → 51.5 ✗ >5 |
| brawler v aegis | 33.4 | 21.0 | 33.4 | outside 35/65 (already at baseline) |
| volley v venom | 61.5 | 55.5 | 55.5 | SPORE +6.0 → 44.5 ✗ >5 |
| volley v aegis | 57.1 | 56.6 | 56.6 | ✓ |
| venom v aegis | 63.3 | 69.4 | 69.4 | outside 35/65 |
| venom v rig | 39.4 | 44.4 | 44.4 | SPORE +5.1 → 44.4 ✗ >5 |
| aegis v rig | 53.0 | 54.7 | 54.7 | ✓ |

Pairs outside the targets at the end:
- **SPORE v MIRROR 69.4 / 30.6.** Baseline was already 63.3. The +10% poison widened it.
- **brawler v MIRROR 33.4 / 66.6.** Already outside at baseline and unchanged.
- **SPORE vs non-MIRROR:** five pairs moved more than 5 points toward SPORE:
  - brawler +7.4
  - blade +6.1
  - volley +6.0
  - pike +5.4
  - rig +5.1

  Fang moved +4.0. None ends above 65%; the highest is 54.1% vs fang. SPORE's Versus overall goes from 45.4 to 51.2.
- **MIRROR v pike** went from 58.4 to 64.8 (inside, but near the 65 edge) because of the parry reflect.

## 5. DRAIN fires per match

**DRAIN fires per match**

| context | opponent | base | it1 | final |
|---|---|---|---|---|
| versus | ALL | 1.140 | 1.103 | 1.103 |
| versus | aegis | 1.319 | 1.274 | 1.274 |
| versus | blade | 1.082 | 1.049 | 1.049 |
| versus | brawler | 1.511 | 1.461 | 1.461 |
| versus | fang | 1.160 | 1.119 | 1.119 |
| versus | pike | 1.012 | 0.986 | 0.986 |
| versus | rig | 0.939 | 0.914 | 0.914 |
| versus | venom | 1.154 | 1.106 | 1.106 |
| versus | volley | 0.948 | 0.919 | 0.919 |
| run | ALL | 1.172 | 1.149 | 1.149 |
| run | aegis | 1.192 | 1.127 | 1.127 |
| run | blade | 0.896 | 0.890 | 0.890 |
| run | boss:magnet | 2.128 | 2.123 | 2.123 |
| run | boss:mimic | 2.806 | 2.842 | 2.842 |
| run | boss:shellking | 1.260 | 1.202 | 1.202 |
| run | boss:warden | 1.593 | 1.593 | 1.593 |
| run | brawler | 1.290 | 1.288 | 1.288 |
| run | fang | 1.042 | 0.976 | 0.976 |
| run | pike | 1.006 | 0.965 | 0.965 |
| run | rig | 0.902 | 0.846 | 0.846 |
| run | venom | 1.000 | 0.986 | 0.986 |
| run | volley | 0.883 | 0.859 | 0.859 |

DRAIN rate barely changes (−3% overall). Matches are slightly shorter because SPORE kills faster.

## 6. Rules, goldens, tests

`SimVersion.Rules` 12 → 13. Goldens were regenerated once on the final values:

| golden | index | old (Rules 12) | new (Rules 13) |
|---|---|---|---|
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1001 | 0xEF52DEA026BF8E54 | 0x3079A188FB884FEB |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1002 | 0xD66ADB60E13DF335 | 0x47EA5A823C3473B9 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1003 | 0x44ADC495C8F211A1 | 0x5A92BE0F79DB276B |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1004 | 0x98C3BDB4E76AC2D7 | 0x89FA005461F1AF96 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1005 | 0x69C19665F766C756 | 0x47CEB8494E77A462 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1006 | 0xC53623F1C01196C7 | 0x5C0BAD2816AED31C |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1007 | 0xA3085EFB4D8B3124 | 0xCCB9F5393F3CCE35 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1008 | 0x2A626D3D0672AF67 | 0xAE48579E9B99CDAA |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1009 | 0x7323175FFF8A6C1E | 0x087A69AB8E4EA560 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1010 | 0x15AA4CABA3A6DF0C | 0xF659F46AF5893AE3 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1011 | 0x16502A9C0A90CE0A | 0x8622DBE54D64C3D5 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1012 | 0x1B62BBF63D3DE747 | 0xF04914CA3F5A4E29 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1013 | 0xAD350D500B37B452 | 0x8F98D2782F277A4E |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1014 | 0x41AD448818F5E5E8 | 0xCD6E0C24BBB470F2 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1015 | 0x2725CE1735EF7CC7 | 0xC456488E487744AF |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1016 | 0xCD8D662F59FDD30B | 0xEE4C1E96CBA5C515 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1017 | 0x7D9A87A7E5AD1CA3 | 0x37B91AE48869717B |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1018 | 0x5512E9990F301C28 | 0x33C34F7FCC57DFD1 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1019 | 0xFC33DEB355AC4E53 | 0x76A9CAC2E20017C2 |
| CrossPlatformGoldenTests.ExpectedBySeed | seed 1020 | 0x994231179209EE99 | 0xA6E0850E7165AE9B |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1001 | 0x556BAF253A2154FC | 0xAE7374FCAFF08A6F |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1002 | 0x8266C02873BC4CC5 | 0xAD241AF2F9F5AAC1 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1003 | 0x6BCE88E2863FF991 | 0x2CA58D9960FFC163 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1004 | 0xE4C23C4CB9F5F767 | 0xFA74EFFE42ADE3B2 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1005 | 0x7D6E5D7818072BF6 | 0xED5F4B7DCE118D1E |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1006 | 0xD31FC1CC6C6091FF | 0xBEB5D0E0FB448B1C |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1007 | 0xB5164B18310B9F94 | 0xF2D1663E018F59D1 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1008 | 0xCF80BA671B3E3B1F | 0xA587D49E9BE05F7A |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1009 | 0x09FF6668B4393D26 | 0xAA63726D6BEEE6B8 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1010 | 0xF3182FCF0C30D13C | 0xB5EA52EB3D66008B |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1011 | 0x986481D242A1D1D2 | 0x7D9B5A37D9BB94FD |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1012 | 0xC873C192C572B5BF | 0x308A562478A212D5 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1013 | 0x216CA5EF04EC1072 | 0x11DB9C64917DD8D2 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1014 | 0xE5C5EFABD73047D0 | 0x66D1902B624CB6CE |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1015 | 0x9F6BEF0A41EC3957 | 0xF7BF6CF368C1D09F |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1016 | 0x794573E6B0A58033 | 0xC812372E4041F67D |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1017 | 0x32089F649AA33643 | 0xE78B8AEFAF8225E7 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1018 | 0x711E97B9F59E2D48 | 0x9ECA13B689BA5AB5 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1019 | 0x7B39BD1379F55673 | 0x6049180AC548CA62 |
| CrossPlatformGoldenTests.ExpectedBySeedMasked | seed 1020 | 0x56AE5105EE29D931 | 0x6C164361EAD660FF |
| SpikeCornerTests.ExpectedByArena | classic | 0x0B58C2EAD389BF51 | 0x906C2AFD8C177FD6 |
| SpikeCornerTests.ExpectedByArena | pillar | 0xC84B23394E4702B6 | 0xF2BB03317F69C42B |
| SpikeCornerTests.ExpectedByArena | bumpers | 0xF9A14CAB0C72A98D | 0xAE08CE27AFF0F50D |
| SpikeCornerTests.ExpectedByArena | spike-walls | 0x74530116D1B32E7A | 0x59988E7A202AF46B |
| SpikeCornerTests.ExpectedByArena | low-gravity | 0x6DD30CD8AC3D7FCB | 0xDF60906A6C77FC0F |
| SpikeCornerTests.ExpectedByArena | tight | 0x2F7B7E1E52DCB7B3 | 0x2B3AAFA870F4974D |
| SpikeCornerTests.ExpectedByArena | wide | 0xEE6C04BC717E9420 | 0x4E659D145E3FE9C9 |
| VersusGoldenTests.ExpectedDigest | - | 0x39B83A07907096FF | 0x39B83A07907096FF (unchanged: Step 1 weapons only) |

Tests updated:
- **`SimTests/SkillTests.cs:97-98`:** the `ParryReflect` L2 B / L3 B pins move to the new ×1.1 values (253/300, 759/800). These are direct pins of the tuned value.
- **`SimTests/CrossPlatformGoldenTests.cs`:**
  - The Rules assertion goes 12 → 13 and the digest label "rules12" → "rules13".
  - `ExpectedBySeed` and `ExpectedBySeedMasked` are re-pinned. The masked array changes too, because this is a values change, not a Rules-only bump.
- **`SimTests/SpikeCornerTests.cs`:** the 7 arena digests are re-pinned. The fixture plays all 8 weapons vs blade.
- **`SimTests/VersusGoldenTests.cs`:** unchanged. It covers only the four Step 1 weapons, and its digest is identical.

New tests:
- **`Assets/Tests/EditMode/BbBal2Tests.cs`** (+ `.meta`), 6 cases:
  - `DrainNeedsThreeStacksOnEveryOpponentIncludingMirror`, ×4 (aegis, blade, brawler, rig): 2 stacks do not fire DRAIN, 3 stacks do.
    - **Deviation from the brief:** the brief asked for "MIRROR = 2, others = 3". The relaxation was not applied (section 2), so the test pins 3 for MIRROR as well.
  - `MirrorReflectValuesAreTheBal2Values`: pins the final reflect values, the unchanged body reflect, ParryReflect at L1, L2 A, L2 B and L3 B, and `VenomDps`.
  - `SporeVsMirrorIsDeterministicForAFixedSeed`: SPORE L3 A vs MIRROR L3 B, seed 4242, played twice. Per-tick hashes and the winner must be identical, and seed 4243 must differ.
- **`SimTests/BbBal2Report.cs`:** the `[Explicit]` measurement harness that produced `bbbal2/*.csv`. It does not run in the default suite. Dev can drop it from the commit if measurement tools are not kept.

## 7. Test results (final code)

| suite | total | passed | failed | skipped |
|---|---|---|---|---|
| SimTests (`dotnet test`, 154 s) | 320 | 320 | 0 | 0 |
| Unity EditMode (6000.5.1f1 batchmode) | 404 | 397 | 0 | 7 |

- The 7 EditMode skips are the same env-gated capture/diagnostic tests as before (BB01/BBFONT/VIEW3/BB captures, Night1 phase B/C/D).
- Determinism:
  - `CrossPlatformGoldenTests` (both arrays), `SpikeCornerTests.EveryArenaMatchesItsDigest`, `VersusGoldenTests`, `ShooterMatchesAreDeterministic` and `SpikeArenaIsDeterministic` all pass.
  - The new `SporeVsMirrorIsDeterministicForAFixedSeed` passes.
- Unity was not open; batchmode ran without a lock.

## 8. Files in the diff

`git status --porcelain`, the BB-BAL2 entries only. The 5 pre-existing modified non-Assets files (`.claude/hooks/.logs/hook-log.jsonl` and 4 `docs/claude-runs/trace-unity-*`) and the pre-existing untracked files are unchanged and left out of the diff.

```
 M BallBattleUnity/Assets/Scripts/Sim/SimVersion.cs
 M BallBattleUnity/Assets/Scripts/Sim/Skills/SkillState.cs
 M BallBattleUnity/Assets/Scripts/Sim/Weapons/WeaponTuning.cs
 M BallBattleUnity/SimTests/CrossPlatformGoldenTests.cs
 M BallBattleUnity/SimTests/SkillTests.cs
 M BallBattleUnity/SimTests/SpikeCornerTests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbBal2Tests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbBal2Tests.cs.meta
?? BallBattleUnity/SimTests/BbBal2Report.cs
?? docs/claude-runs/bbbal2/
?? docs/claude-runs/bbbal2.md   (this report)
```

Temp-index diff: a copy of `.git/index` was used through `GIT_INDEX_FILE`, the paths above were `git add`ed into the copy, then `git diff --cached --stat`. The real index is untouched. The stat is taken before this report existed; `docs/claude-runs/bbbal2.md` is one more new file.

```
 BallBattleUnity/Assets/Scripts/Sim/SimVersion.cs           |   3 +-
 BallBattleUnity/Assets/Scripts/Sim/Skills/SkillState.cs    |   2 +-
 BallBattleUnity/Assets/Scripts/Sim/Weapons/WeaponTuning.cs |   4 +-
 BallBattleUnity/Assets/Tests/EditMode/BbBal2Tests.cs       |  84 +++++++++++++++++++++++++++++++
 BallBattleUnity/Assets/Tests/EditMode/BbBal2Tests.cs.meta  |   2 +
 BallBattleUnity/SimTests/BbBal2Report.cs                   | 288 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 BallBattleUnity/SimTests/CrossPlatformGoldenTests.cs       |  22 ++++----
 BallBattleUnity/SimTests/SkillTests.cs                     |   4 +-
 BallBattleUnity/SimTests/SpikeCornerTests.cs               |  14 +++---
 docs/claude-runs/bbbal2/baseline.csv                       | 364 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 docs/claude-runs/bbbal2/baseline.md                        |  66 ++++++++++++++++++++++++
 docs/claude-runs/bbbal2/it1.csv                            | 364 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 docs/claude-runs/bbbal2/it1.md                             |  66 ++++++++++++++++++++++++
 docs/claude-runs/bbbal2/it2.csv                            | 364 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 docs/claude-runs/bbbal2/it2.md                             |  66 ++++++++++++++++++++++++
 docs/claude-runs/bbbal2/seeds.txt                          |   6 +++
 16 files changed, 1695 insertions(+), 24 deletions(-)
```

No net change: `Assets/Scripts/Sim/Weapons/AegisRule.cs` (changed in iteration 1, reverted in iteration 2) and `SimTests/VersusGoldenTests.cs` (digest unchanged).

Not committed, nothing deleted, no stash.

## 9. Residual imbalance and recommendation for the combined pass after BB-10

1. **The Run gap is a PvE problem, and this task's levers do not reach it.**
   - At baseline SPORE dies in fights 1–4 in 239 of 300 runs, and MIRROR in 220. Blade dies there in 191 and brawler in 135.
   - Against the late bosses they rarely win. Final MIMIC results are 21–34% (others 44–77%), and WARDEN 16–17% (others 46–76%).
   - A +10% Versus-strength change moved SPORE's Run win rate by 0.0 and MIRROR's by +1.0, while moving Versus by 4–7 points per pair.
   - Reaching 8–12% Run through Versus numbers alone would push SPORE well past the Versus guard rails.
   - Suggested levers for the combined pass:
     - Run-side: early-fight enemy roster or HP, card offers that suit DoT/reflect, or per-weapon boss tuning for MIMIC/WARDEN.
     - Or let RunBot play SPORE/MIRROR better, if the bot is the bottleneck. The Greedy bot does not value poison/reflect cards differently.
2. **Consider rolling `VenomDps` back to 1.0 (or 1.05) in the combined pass.** It bought +5.7 Versus overall and no Run win. It is behind the SPORE-v-MIRROR 69/31 result and the five small (+5.1 to +7.4) pair moves. The rolled-back state was not measured here.
3. **MIRROR parry reflect +10% is the cleaner change.** Its effects:
   - +1.4 Versus overall.
   - +1 Run win and better MIMIC (29.7 → 34.2) and MAGNET (50.6 → 53.1) results.
   - No guard-rail breach except moving MIRROR v pike to 64.8.

   Keep it, unless the combined pass wants pike v MIRROR back near 58.
4. **Lopsided matchups still open after this pass:**
   - SPORE v MIRROR 69/31 (63/37 before).
   - brawler v MIRROR 33/67 (unchanged since before this task).
   - The three BB-BAL1 deviations, which this task did not touch.
   - The brawler v MIRROR result comes from body reflect: iteration 1 showed that body reflect only affects brawler matchups. If the combined pass wants to fix it, a brawler-specific lever there is the place to look, not MIRROR's general numbers.
