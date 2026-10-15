# BB-12a: ball progression data — chests, shards, gold, ball levels (Sim/Meta + save v5)

Status: **implemented, uncommitted.** No match rules change (Rules stays 13; goldens untouched).

## Phu's decisions (16/10 01:50)

| Question | Answer |
|---|---|
| Source of ball levels | Chests → shards + gold (the BB-12 plan) |
| What a level gives | **Only +1 skill-tree point** (no automatic HP or damage) |
| Re-spending tree points | Free, outside a run |
| HEAT | Later, after measuring balance |

The other plan defaults still apply:
- **D2:** LV10, the cost table below.
- **D3:** WOOD / SILVER / GOLD, one chest per run, no timers.
- **D4:** shards go only to unlocked balls; achievements keep unlocking balls.
- **D5:** gold is only for upgrades.
- **D6:** Versus and PvP ignore levels.
- **D7:** old saves get welcome chests.
- **D8:** no real money; chests are never sold.

## What was built
**`Sim/Meta/MetaRules.cs`** (pure C#, no Unity, no floating point):
- `ChestTier`:
  - WOOD: the run ended without beating boss 1;
  - SILVER: it beat boss 1 (fight 4);
  - GOLD: it won the run.
  - No chest if no fight was played.
- **Chest contents** (`MetaTuning`):
  - WOOD: 4 shards for the played ball, 2 random shards, 20 gold;
  - SILVER: 8 / 6 / 50;
  - GOLD: 15 / 15 / 120.
- **Random shards** go to unlocked balls with equal odds (checked over 2,000 chests: every ball within ±10% of the mean). Shards of a LV10 ball turn into 2 gold each.
- **Seeded draw:** the seed mixes the run seed with `ChestsOpened`. The result is applied to the progress immediately and saved with it, so quitting during the (future) chest animation cannot re-roll it.
- **Levels:** LV1 to LV10.
  - Upgrade costs: shards 5/10/15/25/35/50/65/85/110 (total 400); gold 10/20/35/50/70/90/120/150/190 (total 735).
  - `Upgrade` spends both or nothing.
  - `TreePoints(level)` = level (LV1 = 1 point … LV10 = 10, as in the Persona Web tree).
- **Welcome chests:** `GrantWelcomeChests` gives 1 SILVER per run already won, at most 5, kept in `PendingChests`. `OpenPending` opens one; all its shards are random.

**Save:**
- **v5:** `ProgressData` gains `Balls` [{Id, Level, Shards}], `Gold`, `ChestsOpened` and `PendingChests`.
- **Migration v4 → v5:** keeps the run in progress, starts every ball at LV1 with 0 shards and 0 gold, and adds the welcome chests. Older versions (v1–v3) also pass through it. A v5 save never gets them again.

**Run flow:**
- When a run ends (`FinishFight` with `Run.Over`), its chest is opened and saved, and the result is kept in `RunFlow.LastChest` for the chest screen (BB-12b).
- A saved run that a new run replaces (abandoned) also earns its chest if it played at least one fight (`RunFlow.AwardAbandoned`).

**Not yet:**
- the CHEST screen (BB-12b);
- the BALLS screen and UPGRADE button (BB-12c);
- the skill tree spending its points in the Run, which replaces SKILL UP;
- HEAT.

Until BB-12b/c, chests fill the save silently: nothing is shown yet.

## Tests
- **SimTests `MetaRulesTests`** (11):
  - cost table;
  - tiers;
  - every tier's contents applied;
  - deterministic draw;
  - even odds over unlocked balls only;
  - maxed ball → gold;
  - exact upgrades and the LV10 cap;
  - both currencies needed;
  - welcome chests and opening pending chests;
  - `AwardRun`.
- **EditMode `BbMeta1Tests`** (5):
  - v4 → v5 migration keeps the run and adds welcome chests;
  - migration runs once;
  - levels, gold and chests survive save and load;
  - abandoning a saved run awards its chest once;
  - no chest for a run without fights.
- **Version expectations** in older tests (`Bb04BossTests`, `Prompt3dTests`, `SaveFixTests`) moved from 4 to 5.

## Results
- **SimTests:** 336 tests, 335 passed on the first run. The 1 failure was my own test starting a run with a ball outside the seed's start choices; after the fix, `MetaRulesTests` passed 13/13. Goldens are unchanged.
- **EditMode, full run:** 501 tests, 486 passed, 14 skipped, 1 failed. The failure was `Bb10aTests` still expecting save version 4; it now compares with `SaveData.CurrentVersion`. Re-run of `Bb10aTests` + `BbMeta1Tests`: 19/19 passed.
