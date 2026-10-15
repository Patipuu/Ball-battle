# BB-12b: CHEST screen, odds panel, welcome chests on the menu

Status: **implemented, uncommitted.** View only; no Sim, Rules or save change (Rules 13, save v5).

## What the player sees
- **After a run:** the Run summary appears with the CHEST screen on top of it.
  - Animation: the chest shakes for 0.55 s, the lid opens with three short rays of light, then one row per ball that got shards appears every 0.12 s, then the gold.
  - A tap anywhere skips the animation. COLLECT closes the screen and leaves the summary underneath.
- **Abandoned run:** a saved run that a new run replaces opens its chest over the start pick, with the caption "FROM YOUR LAST RUN".
- **Rows:**
  - ball icon and name, then **+N** (the played ball's number in yellow);
  - a bar toward the next level in the chest's colour;
  - at the end, either `have/need`, **UP!** (green: enough shards and gold to level now) or **MAX**.
- **Gold:** "+N GOLD" in large type, then the gold total (and how much came from LV10 balls).
- **Colours and captions:** WOOD, SILVER and GOLD chests each have their own colours. The caption is RUN OVER, BOSS 1 BEATEN or RUN WON.
- **ODDS** (top right): the drop table, generated from `MetaTuning` so it can never disagree with the game:
  - when each chest tier is given;
  - shards and gold per tier;
  - "1 IN N PER UNLOCKED BALL";
  - locked balls get none;
  - a LV10 shard becomes 2 gold;
  - chests are never sold.
- **Main menu:** when chests are waiting (welcome chests for old saves), a yellow **OPEN CHEST** / **CHESTS X3** button appears at the top left of the hub. Each press opens one chest: it is applied and saved first, then shown with the caption WELCOME BACK.
- **Input:** while the chest screen is open, the buttons of the screen under it are switched off, so a tap on COLLECT can never also press NEW RUN. Tutorial tips wait until the chest is collected.

## Code
- **New:** `View/Run/ChestView.cs`. The chest is drawn from pixel rectangles in each tier's colours; no art asset is needed.
- **`RunFlow`:** `ChestToShow`, `MarkChestShown()`, `LastChestAbandoned` and `ChestCaption`.
- **`RunController`:** shows the chest on the Summary or StartPick screen when one is waiting, before any tip.
- **`MenuView`:** `ChestButton`, `ChestRequested` and `ChestLabel`.
- **`GameController`:** `OpenWaitingChest` and a menu `ChestView`.

## Tests (`BbChest1Tests`, EditMode)
- shake → open → every row → COLLECT appears;
- a tap skips the animation; COLLECT closes and raises its event;
- the UP! and MAX row endings;
- the odds lines match `MetaTuning` and fit the panel (real font width);
- every text stays on screen and clear of COLLECT, for all three tiers;
- an abandoned run's chest opens over the start pick and blocks it, then unblocks, and is shown only once;
- the menu chest button appears only while chests wait and overlaps nothing;
- screenshots (`BB_CHEST1_OUT`).

## Screenshots (`bb12b/`)
1. WOOD chest, closed;
2. WOOD chest, open;
3. SILVER chest;
4. GOLD chest;
5. odds panel;
6. menu hub with CHESTS X3.

## Results
- **Full EditMode run:** 509 tests, 495 passed, 0 failed, 14 skipped.
- **After the screenshot review:**
  - the big glow box became three thin rays;
  - the odds lines were shortened (the font has no "=");
  - the odds panel now covers COLLECT.
- **BbChest1Tests re-run** (with screenshots): 8/8 passed.
