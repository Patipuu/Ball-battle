# UI-CLEAN1: remove note-style filler lines from every screen

Status: **done, uncommitted.** View, strings and tests only; no Sim change.

Phu (16/10): screens carried AI-style notes ("BÓNG TỰ ĐỘNG GIAO CHIẾN" and the like) that make the game feel unnatural; a game screen should not explain itself line by line. Every screen was checked in Vietnamese and English (BbFont1 captures, 37 screens per language).

**Rule used:**
- **Kept:** labels, numbers, prices, states and reasons ("NOT ENOUGH COINS"), the HOW TO PLAY page and the first-run tips.
- **Removed:** taglines, captions under buttons, keyboard hints, footers that explain the screen, the match code.

## Removed
| Screen | Line (EN / VI as shown) |
|---|---|
| Menu | "WEAPON BALL AUTO BATTLER" / "BÓNG TỰ ĐỘNG GIAO CHIẾN" |
| Menu | "NEW? START HERE" with its arrows (the whole feature is gone) |
| Menu | "8 FIGHTS 2 BOSSES 1 RETRY" under RUN |
| Menu | "LEVEL UP WITH CHEST SHARDS", "WATCH ANY TWO BALLS FIGHT", "WHAT EVERY BALL DOES" under the buttons |
| Run mode | rules subtitle and the 4-line rules band |
| Weapon pick | "ONE SIGNATURE SKILL. TWO BRANCHES"; "TAP A BALL TO SEE HOW IT PLAYS" (see below) |
| Start pick | footer band "NO REROLL ON START PICK" / "YOUR FIRST (RETRY) FIGHT STARTS AFTER THIS" |
| Prepare | "ESC: MENU   THE RUN IS SAVED" (see below) |
| Skill tree | "HOLD A NODE TO SEE AND UPGRADE IT", "EVERY RUN WITH THIS BALL USES IT", "RESET IS FREE OUTSIDE A RUN" |
| Balls | "SHARDS AND GOLD LEVEL A BALL UP" |
| Chest odds | "CHESTS ARE NEVER SOLD" |
| Versus | "VERSUS: ANY WEAPON  BEST OF 3"; the page title is now VERSUS / SO TÀI instead of BALL BATTLE |
| Versus | "SEED n" / "MÃ TRẬN n" during and after a match; "REMATCH REPLAYS THE SAME FIGHT" |
| Run summary | "THEY CAN APPEAR IN LATER RUNS" |

## Replaced
- **HOW TO PLAY step 3:** "BALLS FIGHT ON THEIR OWN" → "TAP TO STRIKE" / "CHẠM ĐỂ ĐÁNH" (something the player can do).
- **First tip:** "EACH BALL FIGHTS ON ITS OWN" → "PICK YOUR BALL" / "CHỌN BÓNG CỦA BẠN".
- **Weapon pick:** the first ball is explained as soon as the screen opens, so there is no empty panel and no hint line. A second tap or PICK still picks it.
- **Prepare:** a **MENU / VỀ MENU** button (24 px, bottom right under FIGHT) replaces the keyboard-only "ESC: MENU" line. Phones have no Esc; it does what Esc does (the run is saved).
- **Vietnamese fixes found on the way:**
  - "SÂN SÂN RỘNG" → "SÂN RỘNG": the arena names TIGHT/WIDE no longer repeat "SÂN".
  - "WINS 1/3" is now translated: a full-line pattern was added for "BEST FIGHT … WINS …".
  - 11 ball-guide lines (HOW / GOOD / WEAK) that ran past the info panel were shortened, keeping their meaning. All 30 lines now fit the panel (≤ 240 px), measured with the game font.
- **The VI table** (`ui-vi.txt`) lost the 20 rows of removed lines and gained the 2 new ones.

## Tests
- **Updated:**
  - `BbMenu1Tests`: a clean menu for new and returning players, with no removed line;
  - `BbFix1bTests`: the start pick has no footer; the string table is still checked;
  - `BbUx1Tests`: the first ball is selected at once;
  - `BbViTests`: the shortened EMBER line.
- **Full EditMode before the last string fixes:** 556 tests, 537 passed, 1 failed (the pinned EMBER line, fixed since), 18 skipped.
- **Then:** `BbViTests` + `BbFont1Tests` + `BbUx1Tests` + `BbMenu1Tests` + `BbFix1bTests`: 53 passed, 0 failed.

## Left for the Vietnamese pass (in progress, not changed here)
- **Weapon pick panel:** the role (e.g. "POISONER") and the "LV2 A / LV2 B" branch lines are still English; the skill line of SPORE runs to the panel edge.
- **Start pick card:** "… ĐẦU TRẬN GIỮ NGUYÊN" runs past the card border.
- **Boss prepare:** the boss hint wraps under the arena preview.
- **Run summary:** the per-fight result string ("TTTTTTTT") reads oddly in Vietnamese.
