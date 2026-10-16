# BB-12h: SKILL TREE with icons, hold a node for its card, UPGRADE inside the card

Status: **implemented, uncommitted.** View only; the tree rules are BB-12g's (Rules 14).

## Phu's request
"ngoài skill node tree tôi muốn là vẽ icon và nhấn vào giữ hiện thông tin, và có nút nâng cấp trong thẻ pop up đó"

## What the player sees
- **Icon nodes:** each node is a tile with a 16×16 pixel icon drawn at 2×:
  - **heart** = VITALITY;
  - **sword** = POWER;
  - **lightning** = INSTINCT;
  - **star** = OPENER;
  - **gem** = skill level, with a small **2** or **3** in the corner;
  - **crown** = FINALE, with **A** or **B**.
- **Tile colours:**
  - bought: gold icon and border;
  - can buy now: white;
  - locked: grey.
  - Under the icon: the rank (`1/2`) or the cost (`1 PT`, `3 PTS`) or `OWNED`.
- **Tap a node:** it is selected (yellow frame) and its name and cost show in one line under the tree.
  - Under that line: "HOLD A NODE TO SEE AND UPGRADE IT".
- **Hold a node (0.35 s):** its **card** opens over a dark shade:
  - big icon (3×), name, rank or cost;
  - the two effect lines for this ball;
  - the state: "READY. COSTS 1 OF 4 FREE" (green), OWNED, or why it is locked;
  - **UPGRADE** (yellow when it can be bought; says OWNED when done) and **CLOSE**. Tapping outside the card also closes it.
  - UPGRADE saves at once and the card stays open, so the new rank shows.
  - The finger that opened the card never presses UPGRADE by accident.
- **Bottom of the page:** RESET (centred) and BACK stay. The old ADD button and info panel are gone; the card replaces them.
- **Sounds:** tree_add, ui_denied and tree_reset are unchanged from SFX4. Opening the card clicks; closing it plays ui_back.

## Code
- **New:** `View/TreeIcons.cs` (icon art as text grids, sprites made at runtime and tinted by state).
- **`View/TreeView.cs`:** rewritten layout, hold timer (`HoldSeconds`, `TickHold`, uses `PixelButton.Pressed`), and the card (`OpenCard` / `CloseCard`, `UpgradeButton`, `CloseButton`).
  - The public API from BB-12g still works: `AddButton` is now the card's UPGRADE and acts on the selected node.

## Tests
- **`BbTree2Tests`, 6 tests:**
  - every node has its icon, and the icon grids are 16 wide;
  - the card's UPGRADE buys the node, saves at once, stays open, then shows OWNED; CLOSE closes;
  - a locked node's card says why and UPGRADE does nothing;
  - no hold, no card;
  - the card's texts fit inside it for every node of every ball;
  - screenshots (`BB_TREE2_OUT`).
- **`BbTree1Tests`** (BB-12g) still pass unchanged: boxes and texts fit, branch lock, RESET, runs use the tree.

## Results
- **Full EditMode run** (BB-MUSIC1 + BB-12h on top of BB-12g, UI1 and SFX4): **543 tests, 526 passed, 0 failed, 17 skipped.**
  - The extra skips are capture tests whose output folder was not set in this run.
- **Screenshots** (`bb12h/`): 1 icons empty, 2 icons with the middle row bought, 3 card READY, 4 card OWNED after UPGRADE, 5 card of a locked branch.
