# BB-BAL3: Run-side balance for SPORE (venom) and MIRROR (aegis)

Branch `dev/step3-view`, base `bc3c460`. **Not committed.** **Rules 12 → 13** (Run outcomes change). Versus is bit-identical (masked goldens unchanged).

## Method
- **Harness:** outside the repo (`E:\ball-battle-bal3-out\harness`; copy in `bbbal3/Bal3.cs.txt`, `harness.csproj.txt`). It compiles the repo's `Assets/Scripts/Sim/**` and the existing `SimTests/RunBot.cs`.
- **Bot and runs:** Greedy RunBot with the start weapon forced, 8 weapons.
- **Seeds:** fixed list `1..500` per weapon (4,000 runs), the same seeds before and after.
- **Metrics:**
  - Run win %, average fights won, and loss % per fight 1–8. Attempts include the retry: losses ÷ attempts at that fight.
  - Boss beaten %.
  - Normal-fight loss % by enemy weapon.
  - Cards taken per run.
  - Share of the player's damage that the DAMAGE card scales: weapon/projectile hits vs poison/reflect.
- **Raw output:** `bbbal3/baseline.txt|csv` and `bbbal3/it1.txt|csv`.

## 1. Baseline (Rules 12): where SPORE and MIRROR lose

| weapon | Run win % | avg fights won | loss % fight 1..8 | boss beaten % (SK / MAG / MIM / WAR) | dmg scaled by DAMAGE card | avg DAMAGE % |
|---|---|---|---|---|---|---|
| EMBER (blade) | 16.4 | 3.46 | 41 41 27 40 19 6 12 56 | 57 / 63 / 40 / 49 | 88% | 15.5 |
| MANTIS (fang) | 15.0 | 3.23 | 43 45 32 35 16 15 16 53 | 60 / 69 / 42 / 55 | 87% | 14.3 |
| ANGLER (pike) | 15.0 | 3.52 | 38 39 29 44 18 9 19 57 | 51 / 62 / 40 / 46 | 88% | 15.3 |
| WRECKER (brawler) | 39.8 | 4.71 | 27 37 21 35 2 1 1 32 | 72 / 59 / 68 / 69 | 91% | 18.3 |
| SPARKLER (volley) | 18.6 | 3.59 | 36 42 28 42 20 9 13 48 | 56 / 60 / 56 / 48 | 89% | 15.8 |
| **SPORE (venom)** | **3.0** | 2.48 | 44 42 38 **67** 22 11 16 **85** | **33 / 32 / 20 / 10** | **41%** | 11.4 |
| **MIRROR (aegis)** | **6.8** | 3.01 | 39 44 34 **54** 13 10 17 **76** | 51 / 42 / 24 / 23 | **61%** | 13.1 |
| HIVE (rig) | 22.2 | 3.85 | 37 40 23 40 10 4 6 52 | 65 / 56 / 53 / 44 | 89% | 16.4 |

Findings:
- **Normal fights:** in fights 1–3 and 5–7, SPORE and MIRROR lose about as often as the other weapons. SPORE fights 1–3: 44/42/38 vs EMBER 41/41/27.
- **Bosses are the gap.** Fight 4: SPORE loses 67%, MIRROR 54%, the others 35–44%. Fight 8: 85% / 76% vs 32–57%.
  - SPORE beats WARDEN 10% of the time and MIMIC 20%.
  - MIRROR beats both about 23%.
- **Why: the DAMAGE card doesn't help them.** The DAMAGE card ("MELEE BODY AND SHOT HITS +15%", "NOT POISON SPIKES OR THORNS") only multiplies weapon/projectile hits.
  - **Share it reaches:** 41% of SPORE's damage and 61% of MIRROR's, against 87–91% for every other weapon.
  - **The bot still takes it:** the Greedy bot (and a normal player) takes it 1.2–1.4 times per SPORE/MIRROR run, so those picks are mostly wasted.
  - **Bosses magnify this:** boss fights are long, high-HP fights (156–275 HP), so damage that does not scale costs most there.
- **Enemy HP in fights 1–4 is not the cause:** normal fights are fine, so that lever would also lift every other weapon.

## 2. Iteration 1 (final): the DAMAGE card also scales SPORE's poison and MIRROR's reflects

| file:line | change |
|---|---|
| `Assets/Scripts/Sim/Weapons/VenomRule.cs:39` | poison dps `VenomDps` → `VenomDps * (1 + Bonus.DamagePct)` (SPORE's own weapon poison only; not Poison Tip, spikes or Thorns) |
| `Assets/Scripts/Sim/Weapons/AegisRule.cs:62` | body reflect `damage * pct` → `damage * pct * (1 + Bonus.DamagePct)` |
| `Assets/Scripts/Sim/Weapons/AegisRule.cs:73` | parry reflect `blow * pct` → `blow * pct * (1 + Bonus.DamagePct)` |
| `Assets/Scripts/Sim/Loadout/BallLoadout.cs:8` | doc comment only |
| `Assets/Scripts/Sim/SimVersion.cs:16` | `Rules` 12 → 13 (old line kept as history comment) |
| `Assets/Scripts/View/Run/CardText.cs:63` | DAMAGE card line 2: "NOT POISON SPIKES OR THORNS" → "ALSO SPORE POISON, MIRROR REFLECT" (fits: test) |

Only the Run gives `DamagePct` (DAMAGE cards). Versus balls have 0, and `x * (1 + 0)` is exact in Fix, so **every Versus result is unchanged**. Proof:
- All Versus goldens pass with the change.
- `ExpectedBySeedMasked` is unchanged.
- `VersusGoldenTests` and the 7 `SpikeCornerTests` arena digests are unchanged.
- `WithoutCardsAWholeMatchIsUnchanged` passes: a SPORE vs MIRROR match is bit-identical with and without an explicit 0 bonus.

Bosses get no `DamagePct` either; MIMIC copies weapon, skill and traits, not stat cards.

| weapon | Run win % base → it1 | avg fights won | loss % fight 1..8 (it1) | boss beaten % (SK / MAG / MIM / WAR) it1 |
|---|---|---|---|---|
| EMBER | 16.4 → 16.4 | 3.46 → 3.46 | unchanged | unchanged |
| MANTIS | 15.0 → 15.0 | 3.23 → 3.23 | unchanged | unchanged |
| ANGLER | 15.0 → 15.0 | 3.52 → 3.52 | unchanged | unchanged |
| WRECKER | 39.8 → 39.8 | 4.71 → 4.71 | unchanged | unchanged |
| SPARKLER | 18.6 → 18.6 | 3.59 → 3.59 | unchanged | unchanged |
| **SPORE** | **3.0 → 9.6** | 2.48 → 2.76 | 43 42 35 **61** 15 8 12 **61** | 39 / 39 / **44** / **34** |
| **MIRROR** | **6.8 → 7.8** | 3.01 → 3.16 | 39 42 32 **51** 13 9 10 77 | 57 / 43 / 24 / 22 |
| HIVE | 22.2 → 22.2 | 3.85 → 3.85 | unchanged | unchanged |

The other 6 weapons are identical to the last digit: their runs never use SPORE/MIRROR with a DAMAGE card (enemy balls have no cards).

**Targets:**

| target | result | met? |
|---|---|---|
| SPORE Run win ~8–12% | 9.6% | ✓ |
| MIRROR Run win ~8–12% | 7.8%, 0.2 below the band | Small deviation (accepted). Sample SE about 1.2 pt. |
| No other weapon drops more than 3 points | 0.0 change | ✓ |
| Versus balance | untouched | ✓ |

**Why I stopped after one iteration.**
- **SPORE:** in its band.
- **MIRROR:** only 0.2 below its band.
- **No Versus-neutral lever left for MIRROR:** the only lever left for it in the GD list is the BAL2 parry reflect ×1.1 (`AegisReflectPct` 0.45 → 0.495; `ParryReflect` 0.6 → 0.66, 23/30 → 253/300, 69/80 → 759/800). That one **changes Versus**:
  - In BAL2 it moved MIRROR v ANGLER 58.4 → 64.8, at the edge of 65.
  - MIRROR's Versus overall went +1.4.
  - It would re-pin the masked goldens.

  It gave MIRROR about +1 point of Run win in BAL2. That is a design choice for Phu (section 6), so I did not run it as iteration 2.

## 3. Goldens (Rules 12 → 13)
Only `CrossPlatformGoldenTests.ExpectedBySeed` changes. Its digest includes the Rules field; the matches are the same.

| golden | seed | old (Rules 12) | new (Rules 13) |
|---|---|---|---|
| ExpectedBySeed | 1001 | 0xEF52DEA026BF8E54 | 0x07587B313D420384 |
| ExpectedBySeed | 1002 | 0xD66ADB60E13DF335 | 0x244CB1F9C4409EA9 |
| ExpectedBySeed | 1003 | 0x44ADC495C8F211A1 | 0x7E4F4F266704C875 |
| ExpectedBySeed | 1004 | 0x98C3BDB4E76AC2D7 | 0x3F0FC129E10A6537 |
| ExpectedBySeed | 1005 | 0x69C19665F766C756 | 0x68FBDF8871966702 |
| ExpectedBySeed | 1006 | 0xC53623F1C01196C7 | 0x0DD505E7CCA82473 |
| ExpectedBySeed | 1007 | 0xA3085EFB4D8B3124 | 0x406ED75FE702DF60 |
| ExpectedBySeed | 1008 | 0x2A626D3D0672AF67 | 0x24292B80CB91F80B |
| ExpectedBySeed | 1009 | 0x7323175FFF8A6C1E | 0x4FEC23821F8204CA |
| ExpectedBySeed | 1010 | 0x15AA4CABA3A6DF0C | 0xA3A71A9C7C0792D8 |
| ExpectedBySeed | 1011 | 0x16502A9C0A90CE0A | 0xD1D8913E8EE76BCA |
| ExpectedBySeed | 1012 | 0x1B62BBF63D3DE747 | 0x8E8E58413AB777F7 |
| ExpectedBySeed | 1013 | 0xAD350D500B37B452 | 0xDB9FAC3D441FE65A |
| ExpectedBySeed | 1014 | 0x41AD448818F5E5E8 | 0x4265F0602269CE60 |
| ExpectedBySeed | 1015 | 0x2725CE1735EF7CC7 | 0x584B42B093C14AA7 |
| ExpectedBySeed | 1016 | 0xCD8D662F59FDD30B | 0x6464B23ABCFEA0AB |
| ExpectedBySeed | 1017 | 0x7D9A87A7E5AD1CA3 | 0xDEA042267A7AC96F |
| ExpectedBySeed | 1018 | 0x5512E9990F301C28 | 0x6A3292AF97DC0050 |
| ExpectedBySeed | 1019 | 0xFC33DEB355AC4E53 | 0xB72A7BFDCA62EC4B |
| ExpectedBySeed | 1020 | 0x994231179209EE99 | 0x65A6A1798D9CD195 |

**Unchanged:** `ExpectedBySeedMasked` (20), `VersusGoldenTests.ExpectedDigest` (0x39B83A07907096FF), and the 7 `SpikeCornerTests` arena digests.

**Test updates:** `CrossPlatformGoldenTests`: the Rules assertion 12 → 13, the label "rules12" → "rules13", and the `ExpectedBySeed` summary line.

## 4. Tests
- **New `SimTests/BbBal3Tests.cs` (3 tests):**
  - `SporePoisonScalesWithTheDamageCard`: no card means exactly `VenomDps`; +30% gives `VenomDps × 1.3`.
  - `MirrorReflectsScaleWithTheDamageCard`: body reflect and parry reflect, without and with +15%.
  - `WithoutCardsAWholeMatchIsUnchanged`.
- **New `Assets/Tests/EditMode/BbBal3Tests.cs`** (+ `.meta`): the new DAMAGE card text, and that both lines fit `CardText.MaxDetailWidth`.

| suite | total | passed | failed | skipped |
|---|---|---|---|---|
| SimTests | 323 | 323 | 0 | 0 |
| Unity EditMode | 420 | 412 | 0 | 8 |

The 8 skips are the 7 old env-gated capture/diagnostic tests plus FIX4's `CaptureFix4Screens` (skipped when `BB_FIX4_OUT` is not set). Unity was closed before the run.

## 5. Files (for the commit, after approval)

```
 M BallBattleUnity/Assets/Scripts/Sim/Loadout/BallLoadout.cs
 M BallBattleUnity/Assets/Scripts/Sim/SimVersion.cs
 M BallBattleUnity/Assets/Scripts/Sim/Weapons/AegisRule.cs
 M BallBattleUnity/Assets/Scripts/Sim/Weapons/VenomRule.cs
 M BallBattleUnity/Assets/Scripts/View/Run/CardText.cs
 M BallBattleUnity/SimTests/CrossPlatformGoldenTests.cs
?? BallBattleUnity/SimTests/BbBal3Tests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbBal3Tests.cs (+ .meta)
?? docs/claude-runs/bbbal3.md, docs/claude-runs/bbbal3/ (data + harness copy)
```

Pre-existing modified/untracked files are untouched and excluded. `docs/claude-runs/bbqa-bc3c460*` is the QA report from the previous step (separate).

## 6. Decision for Phu
1. **Approve BAL3 as is** ("duyệt commit BB-BAL3"): SPORE 9.6%, MIRROR 7.8%, Versus untouched.
2. **Or add iteration 2 first:** MIRROR parry reflect ×1.1. Expected MIRROR Run about 9%. Costs: Versus changes (MIRROR v ANGLER about 65/35, masked goldens re-pinned) and a Versus matrix re-check.

Recommendation: option 1. The remaining MIRROR gap is mostly MIMIC/WARDEN (24% / 22%). It fits better in the combined balance pass after BB-10, together with the 3 old band misses (one of them MIRROR v WARDEN).
