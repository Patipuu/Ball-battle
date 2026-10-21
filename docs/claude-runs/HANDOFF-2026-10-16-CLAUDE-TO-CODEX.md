# Handoff — Claude → Codex (16/10/2026, 22:30 ICT)

- **Repo:** `E:\ball-battle`, branch `dev/step3-view`.
- **HEAD:** `75e5fb2` (UI-CLEAN1 + Vietnamese UI + BB-14b), pushed, `origin` = HEAD. `master` is untouched.
- **Commits:** none have been made since 75e5fb2. Everything below is **uncommitted in the working tree**.

## 0. Rules from Phu (still in force)
- **Never delete files.** That includes lock files (`Temp/UnityLockfile`, `.git/index.lock`). If one blocks you, stop and ask Phu.
- **Git:**
  - Commit, push or change git history only after Phu says **"duyệt commit <NAME>"**.
  - Then commit only that task's files on `dev/step3-view`, using a temporary index (see §6).
  - Never touch `master`; push only when Phu says so.
- **Keep these out of every commit:**
  - the pre-existing untracked files: `.claude` hook log, `docs/claude-runs/trace-unity-*`, the old `docs/claude-runs/*` run folders, `docs/design/bb12/`;
  - the 5 modified non-Assets files;
  - `BallBattleHANDOFF/`.
- **Unity:** it must be closed before batch builds or EditMode runs. Phu currently has it **open on this project** (the window title shows "Web"). Ignore the other project's Unity (boom-online-handoff-260922).
- **Language and reports:** speak Vietnamese to Phu; code, commits and reports in English. Write a report for every task in `docs/claude-runs/`.
- **HUNTER** (serpent) is on hold. Do not use it in the trailer, and do not store the friend's photo in the repo.
- **Balance:** a Rules change needs golden regeneration with old → new hashes, and at most 2 tuning passes per balance task.

## 1. UI-FIX2 — done, needs EditMode + screenshots (report: `docs/claude-runs/uifix2.md`)
- **Bug** (Phu): the "LÊN CẤP!" badge sat inside the BALLS button and covered the "CÁC BÓNG" label. On the BALLS grid, each tile's `LÊN CẤP!` overlapped `CẤP 10`.
- **Fix:**
  - `MenuView`: the badge is now a 14 px gold pill with "!" at the right end of the button (`BallsBadgeArea`).
  - `BallsView`: the tile state moved to its own line (`a.yMax - 46`, left-aligned at `xMin + 40`); `ShowGrid` hides the LEVEL UP flash.
  - `BbBalls1Tests`: the badge must sit right of the label (EN + VI) and inside the button.
- **To do:**
  - Run EditMode `BbBalls1Tests`, `BbMenu1Tests`, `BbFont1Tests` and `BbViTests`.
  - Capture the VI menu and the BALLS grid with an upgradeable ball (`BB_BALLS1_OUT`, `BBFONT_CAPTURE`) and check them by eye.

## 2. BB-18 — BGLGXLQ orbs, Rules 16 → 17 (report: `docs/claude-runs/bb18.md`)
- **Phu's spec:** 5 colour orbs circle the ball.
  - An unlit orb touching an enemy lights its colour, in any order; a lit colour does not count again.
  - Each colour gives its buff: PINK heal per melee hit, GREEN speed, BLUE 1 shield, YELLOW damage, RED burn.
  - All 5 lit → UNITY burst, then the ring resets.
- **Sim:**
  - `ChorusRule` was rewritten: bitmask, `Orb(c)`, `OrbAngleDeg`, `Touch()` in `OnTick`, `nextLightTick`.
  - `ChorusLit`: Value = colour, Variant = count lit, B = enemy (-1 for HYMN/OPENER), Point = orb.
  - HYMN lights the first two unlit colours.
- **New tuning** in `TributeTuning`: `ChorusOrbGap 9`, `ChorusOrbRadius 4`, `ChorusOrbSpin 3`, `ChorusOrbDamage 0.25`, `ChorusLightGapTicks 60`, `ChorusDarkTicks 90`.
- **Pass-1 values (kept):** `ChorusDamagePerRing 1.0`, `ChorusUnityBase 4`, `ChorusUnityPerRing 1`.
- **View and text:**
  - `TributeView` draws the orbs at the Sim positions (lit 10 px, unlit 7 px).
  - Guide line and HYMN text are updated in EN and `ui-vi.txt`. Only 2 rows changed; the file was hash-checked.
- **Tests:**
  - **SimTests 352/352 pass** (dotnet on the laptop: `cd BallBattleUnity\SimTests; dotnet test`).
  - `CrossPlatformGoldenTests` re-pinned: a Rules-only change, the masked digests are unchanged, the old values are in the comment.
  - `WeaponRuleTests`: the chorus hash now changes by `OnTick`.
  - `TributeBallTests`: an any-order test was added.
- **Balance** (both passes used, so it is blocked on Phu):
  - **Run:** fine. LV1 15.5% (was 14.5%); best LV10 68.5% (gate ≤ 70).
  - **Versus:** out of range. Mean 62.7%; aegis 95.8, venom 92.8, blade 88.5, brawler 11.3.
  - The same shape existed before (brawler 18 / aegis 77), now it is stronger. The grids in `ChorusOrbReport.OrbTuningGrid` show no single knob fixes both ends.
  - **Phu must choose:**
    - (a) a Versus-only lever, keeping the Run values;
    - (b) accept the spread;
    - (c) let the orbs also absorb projectiles.
- **Reports** (explicit tests):
  - `ChorusOrbReport.ChorusVersusEveryBall` (`BB_REPORT`, `BB_BAL_SEEDS`);
  - `Bb15TreeBalanceSweep` with `BB_BAL_BALLS=chorus`;
  - `ChorusRingsDiag` (rings per minute, event breakdown).
  - Before/after tables are in `Builds/bb18/`. `Builds/bb18/base/` is a git-archive copy of HEAD's Sim, kept for baseline runs.
- **To do:**
  - Run EditMode `BbTribute1Tests`, `BbTree2Tests`, `Bb0102Tests`, `BbViTests` (the orb overlay test uses `OrbsLit`).
  - Look at a fight capture to check that the orbs read well.
  - Apply Phu's balance decision.

## 3. UI-2 — "minimal roguelite" redesign: waiting for Phu's approval
- **Phu's brief:** the layout still has many bugs, the buttons are plain, and it "looks too AI". Style chosen: **minimal roguelite** (Balatro / Slay the Spire) — few borders, big type, whitespace, strong colour accents.
- **Mock:** `docs/claude-runs/ui2/ui2-mock-v2.png` (menu + start-card pick), from `docs/claude-runs/ui2/ui2-mock.py`. It uses the real 5×7 font and sprites. Its paths point at Claude's sandbox; adjust before running.
- **Rules of the mock:**
  - **One gold slab** per screen for the main action: flat fill, a 3 px darker lip, no outline, label centred by its **real ink bbox including Vietnamese marks**. Phu caught the first version sitting low.
  - **Secondary actions** are a plain list: big scale-2 text, a 4×8 colour tick, a dim `>` chevron; the hovered row gets a panel and a 3 px colour bar.
  - **Next goal** has no box: a dim label, a scale-2 line, a 2 px progress bar, and the unlock in the reward colour.
  - **Cards** stand tall side by side (74×132): a 5 px colour band on top, the icon, the name, a big value, the rarity at the bottom; the selected card is raised 10 px with a 2 px gold outline. Under the cards the chosen card is explained in plain text, with numbers in gold.
  - The reroll and MENU are text-only at the bottom.
- **Next:**
  - After Phu approves, apply the style to every screen: menu, Run title, pick ball, start pick, prepare, chest, balls, tree, overlay, summary, codex, versus.
  - Start with a shared helper in `RunUi`/`PixelButton` for the slab, the list row and centring by ink; then do it screen by screen, with VI screenshots each time.

## 3b. Added after the handoff (16/10, 23:40)
- **KHO BÓNG.** The VI name of BALLS is now **KHO BÓNG** (`ui-vi.txt` row `BALLS`). `BbBalls1Tests` measures `KHO BÓNG`.
- **TUT-2, the first-time tutorial.** The plan is `docs/claude-runs/tut2-plan.md`; Phu's decisions are in it.
  - No demo fight: the menu opens with VƯỢT ẢI pulsing.
  - The fixed first run offers EMBER, MANTIS and WRECKER.
  - SO TÀI and SỔ TAY stay hidden until the first run is finished.
  - Not started.

## 4. Other open items
- **Web blockers** (from `audit-web-1016.md`):
  - a portrait WebGL template, replacing the 960×600 default;
  - the IDB save flush (`.jslib` `FS.syncfs` after each save, plus a reload test);
  - a real-browser audio check.
- **VI issues:**
  - roles and the LV2 branch lines are still in English in the pick panel;
  - the "GIỮ NGUYÊN" card line overflows;
  - the boss hint wraps under the arena preview;
  - "TTTTTTTT" on the summary.
- **Balance:** BB-16 (LV1 gate), BB-17 (FINALE value).
- **HUNTER** name and art: on hold.

## 5. Gotchas
- **Stale copies.** Claude's file bridge sometimes wrote stale content. Always check `Get-FileHash` after writing. `ui-vi.txt` was edited by both of us today, so diff before editing.
- **Batch Unity:**
  - Do not use `-nographics` for tests that capture screenshots (the sprite renderer crashes).
  - For long captures use `-force-d3d11` and one process per shot.
- **Golden.** `CrossPlatformGoldenTests` has no chorus. Golden matches that involve chorus are Rules-dependent: check `VersusGoldenTests` and the EditMode goldens when Unity runs.

## 6. Commit recipe (only after "duyệt commit <NAME>")
```
$env:GIT_INDEX_FILE='E:\ball-battle\Builds\tmp-index-X'
git read-tree HEAD
git add <task files only>
git commit -F msgfile
Remove-Item Env:GIT_INDEX_FILE
git reset -q -- <committed files>
```
Suggested task file sets:
- **UI-FIX2:** `MenuView.cs`, `BallsView.cs`, `BbBalls1Tests.cs`, `docs/claude-runs/uifix2.md`.
- **BB-18:**
  - `ChorusRule.cs`, `TributeTuning.cs`, `SimVersion.cs`, `SkillDefinition.cs`;
  - `TributeView.cs`, `WeaponGuide.cs`, `ui-vi.txt`;
  - `SimTests/{TributeBallTests, CrossPlatformGoldenTests, WeaponRuleTests, ChorusOrbReport, ChorusRingsDiag}.cs`;
  - `docs/claude-runs/bb18.md`.
- **UI-2 mock:** `docs/claude-runs/ui2/`.

## 7. BB-19 balance of the tapped skills (added 17/10/2026 19:20) — TASK FOR CODEX

Phu: "cân bằng 2 testbot nhưng để ở chỗ codex handoff, bạn (Claude) không phải test". So Claude did NOT balance or run this; it is yours.

**State:** HEAD `557ea69` on `dev/step3-view` holds BB-19/19b/19c (Rules 20). The 10 Run-player skills are tapped, timing-based
(see `docs/claude-runs/bb19.md`, `bb19b.md`, `bb19c.md`). Enemies, bosses and Versus keep the automatic skills, so only the Run player got stronger/different.
SimTests: 378 pass, 2 fail (expected, not balanced):
- `RunDifficultyThresholdTests.GreedyBotWins22To40Percent` — measured about 68.9% (needs 22–40).
- `RunDifficultyThresholdTests.RandomBotWins1To6Percent` — measured about 35% (needs 1–6).

**Why:** `RunBot.TapIfReady` taps every skill the moment it is ready, with no aiming, and the skills got strong. The bots are not a good model of a human timing the skills.

**What to do (max 2 tuning passes, per Phu's rule):**
1. Decide what the gates should mean now. Suggested: keep the bands but make the bot tap *worse than a player*, e.g. tap on a random 1-in-N chance each ready tick, or tap only when the foe is within some range, or skip the first N ticks of readiness. That models "tapping without timing" and is the cleanest fix (no rules change, no golden change).
2. If you must change numbers instead, the knobs are in `Sim/Skills/ActiveSkill.cs` (`SkillTuning`: `Cooldown`=480, dash/hook/bolt/cloud/parry/bee/coil numbers) and the A/B / level-3 / finale multipliers inside each skill class. A Sim number change needs `SimVersion.Rules` 21 and re-pinned goldens in `CrossPlatformGoldenTests`. Prefer pass 1 above.
3. `SimTests/ActCooldownGrid.cs` is an explicit grid for the cooldown (`BB_PCTS`, `BB_BAL_SEEDS`, `BB_REPORT`); `TapTuning.ManualCooldownPercent` = 200.
4. Report in `docs/claude-runs/bb19-balance.md`. Do not commit until Phu says "duyệt commit <NAME>".

**Also still open for the tapped skills:**
- Tutorial wording "TAP TO STRIKE" is old (`TapView.PromptText`, `HowToPlayView` step 3, `TutorialCoach`, pinned by `BbTap1Tests`).
- Nothing from BB-19/19b/19c has been run in Unity (EditMode, screenshots). Unity must be closed for EditMode runs/builds.
- Skill/level-3/finale numbers are a first pass; Phu may want to tune them after playing.
