# BB-QA: Linux build bc3c460 (BB-FIX4 + BB-ART-FADE2)

Build: commit `bc3c460` on `dev/step3-view` (BB-FIX4 `c56ed41` + BB-ART-FADE2), Rules 12. Built with `BuildScript.BuildLinux` (Unity 6000.5.1f1 batchmode; "Build Finished, Result: Success"; Unity was closed). Package:
- `E:\ball-battle-bbfont1-out\playtest\bc3c460\linux-bc3c460.tar.gz` (38.1 MB). `/workspace/playtest/` from the old GT box does not exist here.
- `linux-bc3c460-full.tar` (uncompressed) sits next to it.
- `linux-bc3c460.tar` in the same folder is a truncated first attempt (the shell timed out mid-write). Do not use it; it is kept because files are never deleted.

Smoke test: `./BallBattle.x86_64 -batchmode -nographics -roundrobin -seeds 2 -arena classic -out x.csv` gives 65 lines.

QA box: Linux x86_64 (Ubuntu 24.04), Xvfb + Mesa llvmpipe (OpenGL Core), `-screen-width 270/810 -screen-height 480/1440` for 1x/3x.
- Input: real mouse clicks via xdotool.
- Sandbox: every run uses its own HOME, so no real save is touched.
- Edited saves: test saves were edited only inside those sandboxes, to reach fight 4 / the fight-8 goal and to unlock SPORE.
- Audio: no audio device, so loudness checks were not possible.

Evidence: `docs/claude-runs/bbqa-bc3c460/` (images 1x/3x, a WARDEN video, logs).

## Results

| # | Check | Result | Evidence |
|---|---|---|---|
| FIX4-1 | Boss hint on loss result and CHOOSE YOUR RETRY, 1x and 3x, no overlap | **Pass** | `H2/H3-*-warden-1x/3x`, `N3-result-fight4-1x` (MAGNET) |
| FIX4-1b | No hint after a win | **Pass** | `H2-result-shellking-3x` (WIN, no hint) |
| FIX4-2 | SKILL UP button border does not cut the icon (start/retry pick 2x; Prepare 1x while buttons show) | **Pass** | `A1-*` (start pick), `A2-*` (Prepare), `H3-retry-*` (retry pick) |
| FIX4-3 | TAKEN card "SKILL UP - <branch>", also after a reroll | **Pass** | `B2`/`B3-*-seed12` ("SKILL UP - EDGE" after REROLL) |
| FIX4-4 | One [Reflect] line per reflect in Player.log with `-bbAudioLog -bbTrace`; [Skill] not doubled | **Pass** | `logs/reflect-1x.log` vs `logs/reflect-1x-audio_log.txt` |
| FIX4-5 | Level-3 SKILL UP in Run (`-bbSeed 2`) | **Pass** | `A1`, `A2`, `A3-*-seed2` |
| ART-FADE2 | WARDEN fade v2 frames in game | **Pass** | `fade-v2-frames-208-211.png`, `warden-bumpers.mp4` |
| 10a/FIX2 Bug A | Retry card arena = forced arena (`-bbForceArena bumpers`) | **Pass** | `W2-result-arena-label-1x` ("ARENA BUMPERS") |
| FIX2 Bug B | Bumpers buried by WARDEN walls disappear | **Pass** | `warden-bumpers-contact-sheet.png` |
| FIX2 Bug C | SHELL KING ±60°: trace angle and block agree | **Pass** | 55 shell-up hits, 0 mismatches |
| b2f9fad | Run without `-bbForceBoss`: real boss at fight 4 | **Pass** | `N1-prepare-fight4-natural-magnet-1x`, `N3-result-fight4-1x` |
| b2f9fad | Goal title WARDEN for fight 8 | **Pass** | `N-menu-goal8-1x/3x` ("BEAT WARDEN 0/8"); `N-menu-goal4-1x` ("BEAT MAGNET") |
| b2f9fad | DRAIN (SPORE) in Run, about 20 fights | **Pass** | `logs/drain-skill-and-fight-lines.txt` |
| SFX | Loudness after SFX4 | **Not run** | SFX4 is postponed; no audio device on the QA box |

### Details

**FIX4-1: boss hint.**
- **Command:** `-bbSeed 31 -bbForceBoss warden -bbTrace -bbAudioLog`, then RUN → NEW RUN → 1st weapon → card 1 → Prepare card 1 → FIGHT (X2).
- **Fight:** fight 1 lost vs WARDEN (tick 1648).
- **Result screen:** "WARDEN / MOVING WALLS SPIKE YOU." under the NEXT box, clear of NEXT FIGHT.
- **CHOOSE YOUR RETRY:** the same text under the footer.
- **Scales:** same at 3x.
- **Natural-boss run:** the loss to MAGNET shows "MAGNET / PULLS YOU IN EVERY 5S."
- **Win:** with `-bbForceBoss shellking` the same seed won, and the result screen shows no hint (correct).

**FIX4-2 / 3 / 5: SKILL UP.**
- **Seed 2** (`-bbSeed 2 -bbTrace -bbAudioLog`): the start pick shows SKILL UP LV2 in card 2 (2x icon fully above the A/B buttons). Taking A (ENDURE) gives a fight-1 Prepare with the LV3 card in card 2 ("ENDURE LV 3", 1x icon above the button). Taking it shows YOU "FRENZY 3 ENDURE". The LV3 TAKEN card stays generic ("SKILL UP / STRENGTHEN YOUR BRANCH TO LV 3"), as designed: FIX4 only names the branch on the LV2 branch pick.
- **Seed 12** (`-bbSeed 12`): take start card 1, then on Prepare REROLL (1 coin) gives SKILL UP LV2 in card 2. Taking A shows the TAKEN card "SKILL UP - EDGE" with "EDGE: MORE HITS" (1x and 3x).

**FIX4-4: Player.log.**
- **Command:** Versus MIRROR vs WRECKER, `-bbSeed 7 -bbTrace -bbAudioLog`, round 1.
- **[Reflect]:** Player.log 36 = audio_log.txt 36, with identical sequences and no doubled lines. Before FIX4 GT counted 88 vs 44.
- **[Skill]:** Player.log 6 = audio_log 6, all `result=played`.
- **Trace lines:** the trace's own lines are `[BallBattle] SkillFired ...`, a different tag.
- **Run fights:** in the SPORE Run fights the counts also match per session (seed 21: 26/26 [Reflect], 14/14 [Skill]; seed 24: 17/17, 14/14; seed 76: 38/38, 15/15).

**ART-FADE2.**
- **Recording:** a WARDEN fight was recorded at 15 fps (`-bbSeed 31 -bbForceBoss warden -bbForceArena bumpers`).
- **Windows:** 3 spike windows of about 36 frames each (about 2.4 s = 144 ticks).
- **Fade:** each is followed by about 2 frames (about 8 ticks) of the grey v2 fade (palette 48/36/56, 76/58/88, 104/88/114, as in the PNG), then plain walls.
- **Mid-fade frame:** `fade-v2-frames-208-211.png` (frame 3 is the fade).

**Bug B: buried bumpers.**
- **Start:** on the bumpers arena with WARDEN, the 4 bumpers show.
- **Walls in:** once the walls close in they are no longer drawn and the balls pass where they were (contact sheet rows 2–4).
- **Note:** the exact gap rule is covered by the BB-FIX2 tests; this check is visual only.

**Bug C: SHELL KING.**
- **Runs:** 7 forced SHELL KING fights (`-bbForceBoss shellking -bbTrace`, seeds 31, 41, 42, 43, plus partial ones).
- **Hits:** 55 `[Shell] Hit` lines with the shell up, 15 blocked.
- **Consistency:** blocked is always true exactly when the angle is ≤ 60.0. Near the edge: 56.6 blocked, 60.4 not blocked.

**Natural boss / goal titles.** A sandbox save was set to fight 4 with bosses MAGNET / WARDEN, no forced boss:
- **Menu goal, no achievements:** "BEAT MAGNET 0/4".
- **Menu goal, with `boss1`:** "BEAT WARDEN 0/8 (UNLOCKS: SPARKLER, POISON TIP +4)".
- **CONTINUE without flags:** "FIGHT 4/8 BOSS" with MAGNET. The fight played with the MAGNET warn/pull cues (29 `[Audio] Boss` lines) and was lost at tick 4553. The retry card reads "NEXT: FIGHT 4/8 MAGNET RETRY".

**DRAIN in Run.**
- **Runs:** SPORE runs on a fully unlocked sandbox profile, seeds 1, 2, 21, 24, 36, 51, 55, 72, 76, 82, 84 (SPORE is the 2nd start choice), `-bbTrace -bbAudioLog`, X2.
- **Fights:** 28 SPORE fights (15 won) and 31 DRAIN fires = **1.11 per fight**, all `result=played`.
- **Comparison:** the BB-BAL2 Run baseline is 1.17 per fight, so this is consistent.
- **Zero-DRAIN fights:** 7 fights had no DRAIN. That is expected when the enemy never holds 3 poison stacks (BB-DRAIN1: the condition is 3 stacks, not a bug).
- **Not checked:** poison stacks are not in the log, so stacks cannot be compared one-to-one with fires from the build. That check stays covered by the BB-DRAIN1 EditMode tests.

## New findings (minor, not blocking)

1. **Boss portrait over the ENEMY label on Prepare.** The 64 px boss body overlaps the boss-name label ("WARDEN", "MAGNET", "SHELL KING") and the ENEMY box's top-left border. See `N1-prepare-fight4-natural-magnet-1x` and `H0` (in `/tmp/qa`, same look). This is the b2f9fad art layout; GT previously passed art at 1x/3x. Small UI, can wait.
2. **Skill popup text over the WARDEN body.** The SPORE/ANGLER skill popup word ("LUNGE") can draw over the WARDEN body for a few frames (`fade-v2-frames`, frame 1). Cosmetic.
3. **Expected design, not a bug.** After the LV3 SKILL UP is taken, its TAKEN card stays generic (see FIX4-5).

## Not run
- Loudness after SFX4 (SFX4 postponed; no audio on the QA box).
- Playing a full natural run from fight 1 to fight 4 by winning. The natural boss was reached through an edited sandbox save, which checks the same code path (CONTINUE, no flag, the run's own boss).
