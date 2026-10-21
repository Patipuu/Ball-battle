# TUT-2 — first-time player tutorial (plan)

Phu, 16/10/2026: "lên plans tutorial chỉ người chơi khi vừa vào game".

## What exists today
- **First-run tips** (`TutorialCoach`, UI-TUT1): 6 blocking boxes during the first run — pick, startpick, enemy, cards, fight, result.
  - Each one dims the screen, shows two lines and waits for OK; there is also SKIP TIPS.
  - The web audit (16/10) flagged them: six stops before the first real fight is too many.
- **HOW TO PLAY:** a 4-step page reached from the menu. A new player has to find it.
- **Menu:** shows every button from the first launch (KHO BÓNG, SO TÀI, SỔ TAY, CÁCH CHƠI), so a new player meets five choices at once.
- **Tap-to-strike:** it exists, but it is only explained as a line of text ("CHẠM ĐỂ ĐÁNH").

## Goal
- **Time:** the first launch reaches a real fight in **under 30 seconds**.
- **Learn by doing:** every lesson is learned by doing it once; text is at most one short line on screen at a time.
- **Never blocking:** hints point at the thing (an arrow or a hand) and never cover or switch off the buttons.
- **Target (GDD §20.2):** 4 of 5 first-time players can say the goal ("win 8 fights") and the next step after their first run.

## Phu's decisions (16/10, 23:34)
1. **No demo fight.** The first launch opens the **menu, with VƯỢT ẢI pulsing**.
2. **The fixed first run offers EMBER, MANTIS and WRECKER.**
3. **SO TÀI and SỔ TAY stay hidden** until the first run is finished.

## The flow (a fresh profile, `RunsPlayed == 0`)
1. **First launch: menu stage 0.**
   - Visible: the title, the ball parade, the gear, **VƯỢT ẢI** and **CÁCH CHƠI**. No goal box, no other buttons.
   - VƯỢT ẢI pulses: a slow scale/brightness beat, and a pixel hand under it after 3 s idle.
2. **Choose your ball, 1 screen:**
   - The offer is pinned to EMBER, MANTIS, WRECKER.
   - One line on top: `CHỌN BÓNG CỦA BẠN`; the hand points at EMBER.
   - Tapping a ball opens its card; tapping again picks it.
3. **First card, 1 screen:** the hand points at the middle card; one line `THẺ GIỮ SUỐT VÁN`.
4. **Fight 1:**
   - The easiest opponent in the pool, with a fixed arena.
   - Tap-to-strike is taught here, once: at the first strike window the fight slows (view only), a pulsing hand says `CHẠM ĐỂ RA ĐÒN`, and the first tap lands a PERFECT strike.
   - After that, no more prompts.
5. **After fight 1 (result):**
   - One line under the HP bar: `MÁU CÒN LẠI MANG SANG TRẬN SAU`.
   - A progress strip `1/8` animates. The goal line is `THẮNG 8 TRẬN - CÓ 1 LẦN ĐẤU LẠI`.
6. **Prepare, fight 2:** the first time coins can buy something, one line points at the shop or reroll: `DÙNG XU MUA THÊM THẺ`.
7. **First boss** (fight 4): a one-time banner `TRÙM: ĐÁNH BẠI ĐỂ MỞ BÓNG MỚI` on the prepare screen.
8. **Run ends, win or lose:**
   - The summary shows the **next step** in big type, for example `RƯƠNG MỚI → MỞ Ở KHO BÓNG`, with a hand on KHO BÓNG.

## The menu shows itself step by step
- **Stage 0** (fresh): VƯỢT ẢI (pulsing) and CÁCH CHƠI.
- **Stage 1** (a run has been started; the first run is not finished): add **KHO BÓNG**, with the "!" pill when a chest is waiting, and the NEXT GOAL line.
- **Stage 2** (first run finished): add **SO TÀI** and **SỔ TAY**, each with a short "MỚI" tag until first opened.
- **KHO BÓNG** explains shards with one line the first time it is opened.
- **Returning players** (an existing save with `RunsPlayed > 0`) start at stage 2, which is today's menu.

## How it is built
- **`OnboardingState`** in the save (save v6, migrates from v5):
  - `MenuStage` (0–2), `StrikeLearned`, `SeenHints` (replaces `SeenTips`).
  - Pure data, with tests in SimTests.
- **`HintPointer`:**
  - A view piece with a pulsing pixel hand / arrow plus one line of text placed next to a target `Rect`.
  - No shade, no buttons, never disables input.
  - It replaces the 6 `TutorialCoach` boxes; `TutorialCoach` is deleted only after Phu approves and the tests move over.
- **Strike lesson:**
  - view-only slow motion and a hand at the first strike window of fight 1;
  - no Sim change.
- **Fixed first run:**
  - `RunState.Start` with a tutorial seed when `RunsPlayed == 0`: the ball offer, the first opponent and the arena are pinned.
  - This also removes the free reroll by reloading the page (audit item).
- **Hints are data:**
  - One table of `{ id, screen, target, line, condition }`, so a new hint is one row.
  - EN and VI in `ui-vi.txt`, each ≤ 240 px at scale 1, checked by test.
- **Skip:** hints can be turned off in Settings; they never block, so no skip button is needed.

## Tests and checks
- **EditMode walk-through:** a fresh profile goes menu stage 0 → pick → card → fight 1 → result → … → summary → stage 2 menu with simulated taps. Each hint appears once, and no button is ever disabled by a hint.
- **Screenshots:** every tutorial step in VI and EN (`BBFONT_CAPTURE` set), checked by eye for overlap.
- **Timing:** from launch to the first fight is at most 3 taps, plus picking the ball and the card.
- **Playtest (M4):** 5 first-time players, no explanation. Note where each one hesitates, and whether they can say the goal and the next step.

## Order of work (about 3–4 days)
1. `HintPointer` + the hint table + `OnboardingState` / save v6 (tests).
2. Move today's 6 tips to non-blocking hints (same moments, shorter lines).
3. Fixed first run (tutorial seed, easy offer and first opponent).
4. Menu stage 0 (pulsing VƯỢT ẢI) and the strike lesson in fight 1.
5. Menu stages 1–2 and the "MỚI" tags.
6. The end-of-run "next step" and the KHO BÓNG first-chest hint.
7. Screenshots EN/VI, the walk-through test, a report.

It builds on the UI-2 style (if Phu approves the mock): the hand and the hint lines use the same slab / colour rules.
