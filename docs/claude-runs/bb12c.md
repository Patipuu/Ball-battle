# BB-12c: BALLS page — levels, shards, UPGRADE; BALLS button on the menu

Status: **implemented, uncommitted.** View only; no Sim, Rules or save change (Rules 13, save v5).

## What the player sees
- **Menu hub:** a new **BALLS** button (yellow) at the top of the button column, with "LEVEL UP WITH CHEST SHARDS" under it.
  - A green **UP!** badge sits on the button when any unlocked ball can level up now.
  - The four hub buttons (BALLS, VERSUS, CODEX, HOW TO PLAY) are now 26 px tall and 40 px apart, so all four fit under the NEXT GOAL box; the explanation lines stay.
- **BALLS page:**
  - **Header:** title, gold (top right), and the line "SHARDS AND GOLD LEVEL A BALL UP".
  - **Tiles:** 8, two columns. Each tile shows the ball sprite, name, `LV n`, a shard bar in the ball's colour, and at the end `have/need`, **UP!** (green tile border) or **MAX**.
  - **Locked balls:** a dark sprite and "LOCKED"; the tile cannot be pressed.
  - **BACK** returns to the hub.
- **Ball detail** (tap a tile):
  - large sprite, name in the ball's colour, "LEVEL n / 10";
  - a 10-pip level track;
  - "SHARDS have / need" with a bar;
  - "NEXT LEVEL: N SHARDS  G GOLD";
  - a status line: READY TO LEVEL UP, NEED x MORE SHARDS, NEED x MORE GOLD, or THIS BALL IS MAXED.
- **UPGRADE** (yellow when ready, greyed out otherwise):
  - spends and **saves at once**, and refreshes the page;
  - shows a green "LEVEL UP!" for 1.2 s;
  - does nothing when the ball cannot level;
  - is hidden at LV10.
- **Skill points:** the line "SKILL POINTS: n   SKILL TREE COMING SOON" shows the tree points (= level). The tree itself is the next task.

## Code
- **New:** `View/BallsView.cs`. It lives inside `MenuView` like CODEX and HOW TO PLAY, and the hub is hidden while it is open.
- **`MenuView`:**
  - `BallsArea` and the new positions of the other buttons;
  - `BallsButton`, the UP! badge (`AnyUpgrade`), `ShowBalls`, `Store`, `BallsClosed`.
- **`GameController`:** gives the menu the save store and refreshes the hub (gold, UP! badge, chest button) when BALLS closes.

## Tests (`BbBalls1Tests`, EditMode)
- one tile per ball; locked tiles say LOCKED and are switched off;
- tile endings `have/need` and UP!;
- UPGRADE spends exactly, is saved in the store, refreshes the page, shows LEVEL UP!, and does nothing when it cannot;
- a maxed ball hides UPGRADE and its tile says MAX;
- all text stays on screen and tiles do not overlap (grid and detail);
- the menu's UP! badge, no overlap between hub buttons and texts, and BALLS opening and closing;
- screenshots (`BB_BALLS1_OUT`).

## Results
- **Full EditMode run:** 516 tests, 501 passed, 0 failed, 15 skipped.
- **Screenshots** (`bb12c/`):
  1. menu with BALLS and its UP! badge;
  2. grid;
  3. detail, ready to level;
  4. detail after the level-up;
  5. detail needing shards.
- **Capture fix:** the first capture's EMBER did not have enough shards (20/25), so the "ready" shot was not ready. It now has 27; the re-run of `BbBalls1Tests` passed 7/7 and the shots show READY → LEVEL UP! (LV4 → LV5, gold 160 → 110).
