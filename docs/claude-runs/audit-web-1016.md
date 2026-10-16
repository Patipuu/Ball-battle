# Web audit, 16/10/2026: first WebGL build of HEAD played in a browser

## How
- **Build:** HEAD (`7c49d16`) plus the BB-14b art, built with `BuildScript.BuildWebGL` and the new `-bbBuildOut <dir>` option, into `Builds/WebGL-audit-1016`. The old `Builds/WebGL` was kept.
  - Result: **15.2 MB compressed** (gzip) — data 7.0 MB, wasm 8.8 MB, framework 0.07 MB. Build time 31 min (first IL2CPP/wasm build on this machine).
- **Played in headless Chromium (Playwright)** from a fresh profile, served by a local server that sends the same gzip headers as itch.io:
  - desktop 1280×800;
  - phone 390×844 with touch;
  - a forced 540×960 portrait canvas, to go through the game.
  - The scripts are in `docs/claude-runs/audit/`.
- **Route:** menu → RUN → NEW RUN → pick a ball → start card → Prepare → fight 1. Also Settings and Versus, plus reloads to test the save.

## Blockers for a web launch
1. **Wrong page layout.**
   - The page uses Unity's default template with a fixed **960×600 landscape** canvas (`defaultScreenWidthWeb/HeightWeb`).
   - On desktop, the 270×480 game draws at 1× in the middle, surrounded by black.
   - On a phone (390×844), the canvas shrinks to 380×240 and the game to about 108×192 px: unreadable. A tap on RUN did not register, possibly because the audit script could not place taps on the scaled-down game.
   - Unity's logo/footer is shown.
   - **Fix (BB-WEB2):**
     - our own portrait template that fills the viewport;
     - integer pixel scale where possible (2× at 540×960, else fit), letterboxed;
     - no page scroll or zoom;
     - a fullscreen button;
     - no Unity branding.
2. **The save never reaches the browser's storage.**
   - At Prepare the game shows "THE RUN IS SAVED", but the browser storage (`/idbfs` FILE_DATA) is **empty**.
   - After a reload there is no CONTINUE: the run, chests, levels and unlocks are all lost when the tab closes.
   - **Cause:** `FileSaveStore` writes with `System.IO.File` to `persistentDataPath`, which on WebGL is an in-memory file system until it is synced to IndexedDB.
   - **Fix (BB-WEB3):**
     - a small `.jslib` (`FS.syncfs(false)`) called after every save write on WebGL;
     - load once the file system is mounted;
     - a reload test in this harness that checks the save survives.

## Should fix before launch
3. **"ESC: MENU"** on Prepare (and possibly other screens) is a keyboard hint; phones have no Esc. Needs an on-screen MENU/pause button on Prepare and in fights.
4. **Settings:**
   - the EN/VI language boxes are drawn only (no switch yet);
   - no reset-progress option;
   - no Credits page (planned for the tributes) — BB-SET1.
5. **Six tutorial tip boxes**, each blocking input until OK. A first-time player taps OK six times before the first fight. Fewer, non-blocking tips would be better (BB-UX3).
6. **Free reroll by reload:** reloading the page during the start pick shows a different set of 3 balls, so a player can reroll by refreshing. Minor; pin the offer to the run seed once shown.
7. **Arena preview box on Prepare is an empty grey square in this browser.** It may be the software renderer (SwiftShader). Check on a real Chrome/Safari in M4.

## Checked and fine
- **Loads** in about 1–2 s locally; the UnityCache stores the data, so later visits are fast.
- **Fight 1 plays correctly:**
  - hits, numbers, statuses;
  - "TAP TO STRIKE";
  - HP bars and the ×1 speed button;
  - obstacles (pillar arena).
- **Menu, Run Mode, ball pick (with stats and the strong/weak text), start card, Prepare, Versus grid** (10 balls with the new BGLGXLQ/HUNTER sprites) and Settings all render at 2× without layout problems.
- **Vietnamese font:** the 5×7 pixel font already has **all 67 Vietnamese capitals** with tone marks and hats (`pixelfont_5x7.json`), and the EN/VI string table exists. The M2 font risk is mostly gone; the work left is moving ~600–700 player-facing strings into the table and translating them.
- **Audio:** this Chromium build cannot decode Unity's WebGL audio (58 "Unable to decode audio data" lines). This is expected for an open-source Chromium (no AAC). It must be checked on real Chrome and iOS Safari in M4, including that audio starts after the first tap.
- **Do not upload `Ball Battle_BurstDebugInformation_DoNotShip/`**, which the build writes next to `index.html`.

## Other open items (not web-specific)
- HUNTER face and name: on hold (Phu, 16/10). Version 5 is in the game.
- LV1 gate (BB-16) and FINALE value (BB-17) from Codex's handoff.
