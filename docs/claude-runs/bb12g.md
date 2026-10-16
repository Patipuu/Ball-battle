# BB-12g: SKILL TREE per ball (10 points, saved, free reset outside a run)

Status: **implemented, uncommitted.** Sim change: **Rules 13 → 14**. Save stays v5 (the new `BallProgress` fields default to 0).

## What the player sees
- **BALLS → ball detail:** a new **SKILL TREE** button, with the line "SKILL POINTS: n FREE OF L" above it.
- **SKILL TREE page:** ten boxes in a fixed layout.
  - **Root row:** VITALITY, POWER, INSTINCT (the ball's own name: KINDLING, MOMENTUM, MORE LINE …). Each costs 1 point per rank, max 2 ranks.
  - **Middle row:**
    - OPENER (the ball's own name: EMBERS, SPIN UP, FIRST CAST …), 2 points;
    - skill LV2 on branch A or B, 1 point.
    - Opens with INSTINCT ≥ 1 and at least 2 root points.
  - **Skill LV3:** 1 point; needs 5 points spent below it.
  - **FINALE A or B:** 3 points (REKINDLE / FIRST BLOOD …); needs skill LV3 on the same branch.
  - The first skill level picks the branch. The other branch is then locked ("OTHER BRANCH CHOSEN. RESET TO SWITCH").
  - **Info panel:** the node name in yellow, two lines of what it does for this ball (numbers match the Sim), and a state line (OWNED, NEEDS …, NO POINTS LEFT).
  - **ADD:** spends and **saves at once**.
  - **RESET:** refunds everything for free (Phu's decision: free respec outside a run).
  - **BACK** returns to the ball detail.
- **Points:** each ball level gives 1 point (Phu's decision), so LV10 = 10 points.
  - There are 18 different full LV10 builds, 16 of them with a FINALE.
- **Runs:**
  - a run started with a ball uses that ball's tree;
  - the tree is saved with the run (resume keeps it);
  - Runs no longer offer the **SKILL UP** card: skill levels now come from the tree. The card code stays behind `RunTuning.SkillUpCards` (default off).

## Sim (Rules 14)
- **New:**
  - `Sim/Tree/TreeLoadout.cs` (`TreeLoadout`, `TreeTuning`);
  - `Sim/Meta/MetaTree.cs` (nodes, costs, prerequisites, `CanAdd`/`Add`/`Reset`/`Valid`/`Spent`/`Free`, `ApplyToRun`).
- **`BallProgress`:** new fields `Vit`, `Pow`, `Growth`, `Start`, `SkillUps`, `Branch`, `Finale`.
- **Run start:** `RunState.Start` applies the tree to the run build.
  - Max HP: +HpPct per VITALITY rank.
  - DamagePct: +DamagePct per POWER rank.
  - Skill level: 1 + SkillUps, on the tree's branch.
  - `TreeGrowth`, `TreeStart` and `TreeFinale` are copied into the build.
  - An invalid saved tree (more points than the level allows) counts as empty.
- **Fight:** `BallLoadout.Tree` and `BallState.Tree` carry the tree into the fight; `TreeTuning.ApplyAtSpawn` runs at the end of `MatchSim.Spawn`.
  - The 8 weapon rules read `Self?.Tree` for INSTINCT, OPENER and FINALE.
  - These rules were ported from Codex's harness-v6. The port was checked bit-identical on 6 builds × 8 balls × 25 seeds, and re-checked on 2 builds after the fixes.
  - **Enemies and Versus balls have no tree.** Versus matches are hash-identical to Rules 13 when the Rules field is masked (`ExpectedBySeedMasked` unchanged).
- **Golden digests** (`CrossPlatformGoldenTests.ExpectedBySeed`, seeds 1001–1020), Rules 13 → 14:

| seed | Rules 13 | Rules 14 |
|---|---|---|
| 1001 | 0x07587B313D420384 | 0x9E50742DA7669D6C |
| 1002 | 0x244CB1F9C4409EA9 | 0xD725BD29BC476DBD |
| 1003 | 0x7E4F4F266704C875 | 0xEFD6C99CE31C9F65 |
| 1004 | 0x3F0FC129E10A6537 | 0x8BC55B5CA8771DEF |
| 1005 | 0x68FBDF8871966702 | 0x851E8A5A802FFEE6 |
| 1006 | 0x0DD505E7CCA82473 | 0xC132495C9401A96F |
| 1007 | 0x406ED75FE702DF60 | 0x597D57587E058EE8 |
| 1008 | 0x24292B80CB91F80B | 0x235501535BA4786B |
| 1009 | 0x4FEC23821F8204CA | 0x9A4213EAFF608E5A |
| 1010 | 0xA3A71A9C7C0792D8 | 0x0FEF36E7B896AF68 |
| 1011 | 0xD1D8913E8EE76BCA | 0xED64044064E76E02 |
| 1012 | 0x8E8E58413AB777F7 | 0xE93742412830316B |
| 1013 | 0xDB9FAC3D441FE65A | 0xA87E52B80DDBEF42 |
| 1014 | 0x4265F0602269CE60 | 0x8DD8798F9E516E54 |
| 1015 | 0x584B42B093C14AA7 | 0x07BE331E824F7203 |
| 1016 | 0x6464B23ABCFEA0AB | 0x58FFBD549A51AD1F |
| 1017 | 0xDEA042267A7AC96F | 0x38B4D00F420BAFFF |
| 1018 | 0x6A3292AF97DC0050 | 0x43F45697702DE948 |
| 1019 | 0xB72A7BFDCA62EC4B | 0x6FF67070AAC1A293 |
| 1020 | 0x65A6A1798D9CD195 | 0x45935FB5C9792F35 |

`ExpectedBySeedMasked` is unchanged, which shows this is a Rules-only change for these matches.

## Balance (greedy bot, no tapping, seeds 3001–3200, 200 runs per row)
- **Builds:** LV1 = empty tree; LV10 = all 18 full builds per ball.
- **Gates** (handoff): best LV10 build ≤ 70%, spread of best builds between balls ≤ 15 points, best A vs best B ≤ 8 points.
- **First sweep:** HIVE best 73.5% and spread 15.5, so two gates failed.
- **Tuning iteration 1** (1 of the 2 allowed, HIVE only):
  - HIVE POWER +3% → **+2%** per rank;
  - BULLET RAIN (HIVE FINALE B) shots −10% → **−13%** damage. The tree text was updated.
  - Tree-only numbers: no golden change.

| Ball | LV1 | LV10 mean (18 builds) | LV10 best | best build | best A | best B |
|---|---|---|---|---|---|---|
| EMBER | 26.5 | 54.1 | 63.0 | V2 P2 I1 O0 A3 FA | 63.0 | 61.5 |
| FANG | 25.0 | 49.8 | 64.0 | V2 P2 I2 O1 A3 F- | 64.0 | 63.5 |
| ANGLER | 23.5 | 48.7 | 58.0 | V2 P2 I2 O1 A3 F- | 58.0 | 54.5 |
| WRECKER | 42.0 | 62.3 | 68.5 | V2 P2 I2 O1 B3 F- | 68.5 | 68.5 |
| SPARKLER | 26.5 | 54.8 | 64.5 | V2 P2 I2 O1 B3 F- | 63.5 | 64.5 |
| SPORE | 13.0 | 60.4 | 68.0 | V2 P2 I2 O1 A3 F- | 68.0 | 67.5 |
| MIRROR | 27.5 | 49.1 | 64.5 | V2 P2 I2 O1 B3 F- | 62.5 | 64.5 |
| HIVE | 35.5 | 59.8 | **69.5** (was 73.5) | V2 P1 I2 O0 A3 FA | 69.5 (was 71.5) | 65.5 (was 73.5) |

(V/P/I = VITALITY/POWER/INSTINCT ranks, O = OPENER, A3/B3 = skill LV3 branch, F = FINALE, "-" = none.)

- **Gates now:**
  - best LV10 = 69.5% ✅
  - spread = 11.5 points (ANGLER 58.0 → HIVE 69.5) ✅
  - largest A/B gap = 4.0 points (HIVE) ✅
- **For Phu's playtest (no change made):**
  - On 5 of 8 balls the best full build skips the FINALE and buys every root and OPENER rank instead. FINALEs are side-grades rather than power, which matches their design, but players may find them weak for 3 points.
  - LV1 is still uneven: SPORE wins 13%, WRECKER 42%. This is the pre-tree baseline; the tree mostly closes the gap at LV10.

## Tests
- **SimTests (`MetaTreeTests`, 8):** LV1 buys one root rank; middle-row and LV3 prerequisites; branch lock and RESET; the 18 / 16 full builds; an invalid tree counts as empty; a run takes the ball's tree (enemies have none); no SKILL UP offers; an empty tree plays the same fight.
- **`CrossPlatformGoldenTests`:** Rules == 14, digests re-pinned (table above), masked digests unchanged.
- **EditMode (`BbTree1Tests`, 6):**
  - one point at LV1, saved at once;
  - a branch path to the FINALE locks the other branch until RESET;
  - BALLS → TREE → BACK;
  - boxes and texts fit on all 8 balls;
  - a run started after spending uses the tree and keeps it in the save;
  - screenshots.
- **Full EditMode run before tuning:** 522 tests, 505 passed, 1 failed, 16 skipped.
  - The failure: `PrepareAndFightUiTests.BlockedCardsAndRerollSayWhy`. Seed 77's start pick now gives THORNS, because the start offers changed when SKILL UP left, so "TRAITS FULL" read "ALREADY HAVE IT".
  - Fixed in the test: it clears the start trait before filling the 3 trait slots.
  - **Final runs after tuning** (together with SFX4 and UI1):
    - EditMode: **531 tests, 518 passed, 0 failed, 13 skipped**;
    - SimTests (Debug): **344/344 passed**.
- **Screenshots** (`bb12g/`):
  1. detail with the SKILL TREE button;
  2. empty tree;
  3. middle row bought;
  4. FINALE B bought;
  5. the other branch locked.

## Files
- **Sim:** `Sim/Tree/TreeLoadout.cs`, `Sim/Meta/MetaTree.cs`, `Sim/Meta/MetaRules.cs`; Weapons Blade/Fang/Pike/Brawler/Volley/Venom/Aegis/Rig; `Skills/SkillState.cs`, `Arena/MatchSim.Obstacles.cs`, `Loadout/BallLoadout.cs`, `BallState.cs`, `MatchSim.cs`; Run `RunBuild`, `RunState`, `CardOffer`, `RunTuning`; `SimVersion.cs`.
- **View:** `View/TreeView.cs`, `View/TreeText.cs`, `View/BallsView.cs`, `View/Run/RunSave.cs`.
- **Tests:** `Tests/EditMode/BbTree1Tests.cs`, `Tests/EditMode/PrepareAndFightUiTests.cs` (one line), `SimTests/MetaTreeTests.cs`, `SimTests/CrossPlatformGoldenTests.cs`.
- Plus the new `.meta` files.
- `BallsView.cs` and `TreeView.cs` also carry SFX4 lines (level_up / tree cues). Commit them with whichever task Phu approves first; the report for the other task will say so.
