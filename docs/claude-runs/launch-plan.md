# Ball Battle — launch plan (v1.0)

Written 16/10/2026 by Claude after reading Codex's commit `7c49d16` and `HANDOFF-2026-10-16-CODEX-TO-CLAUDE.md`.

## 1. Where we are

**Codex's commit `7c49d16`** ("integrate skill trees, tribute balls, and Run balance", 284 files) holds all of Claude's uncommitted work up to BB-14:
- BB-12g/h/i, balance tuning 1, BB-UI1, BB-SFX4, BB-MUSIC1, BB-14;
- the tail-bead fix (HEAD matches the last deployed files);
- plus Codex's own **BB-15**.

**BB-15 (Codex), Rules 15 → 16.** These changes apply only in Run when that ball is the player; Versus and enemies keep the BB-14 numbers.
- WRECKER Run damage ×0.85 at the start, growth ×0.90.
- BGLGXLQ CHORUS heal 1% / 1.5%.

**LV10 gates now pass:**
- best build 70.0% (gate ≤ 70);
- spread 10 (gate ≤ 15);
- A/B gap ≤ 4.5 (gate ≤ 8);
- the holdout seeds 3201–3400 agree.

**Tests (Codex run):** SimTests 351/351; EditMode 531 passed, 0 failed.

**Branch:**
- `dev/step3-view` is in sync with `origin`.
- `master` is 47 commits behind; it has not been touched.
- The pre-existing untracked files are still out of git, as required.

**The game today:**
- Run: 8 fights, 2 bosses, retry, cards.
- Versus best-of-3.
- 10 balls, each with a signature skill (A/B branches) and a skill tree.
- Chests, shards, ball levels.
- Codex, menu with How to Play, tap-to-strike.
- Music and SFX with volume settings; save v5.
- English UI, 270×480 portrait pixel art.
- Builds: Linux (current) and WebGL (11/10, old).
- The Android module is installed and the app id `com.patipuu.ballbattle` is set, but **no Android build has ever been made**.

**Open from the handoff:**
1. LV1 gate (BGLGXLQ 10.5–14.5%, SPORE 14%, HUNTER 17.5%, WRECKER ~41%).
2. FINALE nodes are often skipped by the best builds although they cost 3 points.
3. Tribute art and the friend's name.
4. BB-13 research (prediction / deeds): not for v1.

## 2. Launch target (Phu's decisions, 16/10)
- **Platform: web on itch.io first.**
  - No store review and no 14-day closed test.
  - The link plays on desktop and phone browsers.
  - The WebGL build target already exists in `BuildScript`; its last build is from 11/10.
  - Android / Google Play comes later. Phu has no Play Console account yet. A new personal account must run a closed test with **12 testers × 14 days** before production, so creating it early starts that clock.
- **Language: English + Vietnamese in 1.0**, with an in-game language switch.
- **Business model: free, no ads, no purchases, no analytics.** The game makes no network calls; the save stays in the browser.

## 3. Milestones

### M0 — Baseline (16–17/10)
- Re-run SimTests + EditMode + a WebGL build on HEAD to confirm Codex's numbers on the same machine.
- Sample-check the BB-15 sweep: WRECKER and BGLGXLQ, 200 seeds.
- Freeze the Rules version unless a gate fails.

### M1 — Content lock (by ~24/10)
- **BB-14b: tribute art.**
  - Phu's logo → BGLGXLQ sprite 32×32 plus 38/43/48 sizes and icons.
  - The friend's photo → HUNTER face.
  - The friend's name replaces "HUNTER".
  - *Needs Phu's files.*
- **BB-16: LV1 gate.**
  - Define the target first, e.g. every ball's fresh LV1 Run win between 20% and 45%, with LV10 still ≤ 70%.
  - Then tune the weak openers (BGLGXLQ, SPORE, HUNTER) through LV1-only levers, and recheck WRECKER.
  - At most 2 tuning passes.
- **BB-17: FINALE value.** Measure how often the best and median builds take FINALE. If it is a trap, raise it or cut its cost (3 → 2), keeping the gates.
- **Feature freeze** after M1.

### M2 — Vietnamese (~20/10–31/10, the biggest task)
- **BB-LOC1: font.**
  - Add Vietnamese glyphs to the pixel font: lower and upper case with every diacritic (about 130 glyphs).
  - Line height for stacked marks (Ế, Ộ ...).
  - Glyph tests.
- **BB-LOC2: text system.**
  - Move every UI string into an EN/VI table.
  - Language setting, saved; the first-run default follows the browser language.
  - Vietnamese skill and ball names from the approved `docs/design/bb12-persona-web-v5.md`, not word-for-word translation.
- **BB-LOC3: fit.**
  - Screenshot tests of every screen in both languages at 270×480; no clipped or overflowing text.
  - Phu reviews the Vietnamese wording.

### M3 — Web port and launch features (~27/10–7/11)
- **BB-WEB1: build.**
  - WebGL build with compression that itch.io serves correctly (gzip or a decompression fallback);
  - music in Vorbis instead of WAV, for a smaller download;
  - a loading screen;
  - target a total download under ~30 MB.
- **BB-WEB2: browser fit.**
  - Portrait canvas scaled pixel-perfect inside the itch.io frame, plus a fullscreen button;
  - touch on phone browsers (iOS Safari, Android Chrome);
  - audio starts after the first tap (browser autoplay rule);
  - no page scroll or zoom while playing.
- **BB-WEB3: save.**
  - The save goes to the browser (IndexedDB). It must be flushed after every write, so that closing the tab loses nothing.
  - Keep a backup copy for recovery.
  - Note for players: clearing browser data deletes progress.
- **BB-SET1: settings.**
  - Music / SFX (they exist) and language;
  - reset progress with a 2-step confirm;
  - **Credits** page with the BGLGXLQ and friend tributes.
- **BB-UX3: first 5 minutes.** A fresh profile reaches the first Run without confusion, and the next unlock is visible (GDD §20.1).
- **Store page** (Claude drafts, Phu publishes from his own itch.io account):
  - title, EN + VI description, 4–8 screenshots, cover image 630×500, tags, embed size;
  - a short privacy note ("no data collected").

### M4 — Private playtest (~7/11–14/11)
- Upload as a **restricted itch.io page** (password or draft) and share it with the church group and friends.
- Observe 5 first-time players without explaining (GDD §20.2). The target: 4 of 5 can say the goal and the next step.
- Collect bugs on phone and desktop browsers. Claude ships fixes as new uploads; balance hotfixes only if a gate breaks.

### M5 — Public launch (~mid-November)
- Make the itch.io page public.
- **Release branch:** merge `dev/step3-view` → `master` and tag `v1.0.0`, **only with Phu's explicit approval**.
- After launch:
  - patch window;
  - Android (create the Play account early so the 12×14 test can run alongside);
  - BB-13 prediction / deeds as the first content update.

## 4. Risks
| Risk | Effect | Mitigation |
|---|---|---|
| Vietnamese font glyphs | Biggest workload; can slip a week | Start right after M0; glyph tests; Phu reviews the text early |
| Browser saves (IndexedDB flush, cleared data) | Lost progress | Flush after each save, backup copy, tell players |
| Audio on mobile browsers | Silent game until a tap | Start audio on the first tap; test on iOS Safari |
| WebGL download size / load time | Players leave | Vorbis music, compression, loading screen |
| Balance shifts from LV1 / FINALE work | Gates fail again | Same sweep harness, holdout seeds, 2-pass limit |
| Tribute art late | Placeholders at launch | Acceptable; art can ship in 1.0.1 |

## 5. Still needed from Phu
1. The tribute art files and the friend's name (M1).
2. An itch.io account (Phu creates it; Claude never handles the password).
3. A review of the Vietnamese text (M2).
4. Later: approval to merge into `master` and tag v1.0.0.

Sources:
- [Play Console Help — app testing requirements for new personal developer accounts](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en-GB)
