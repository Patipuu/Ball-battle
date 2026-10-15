# BB-FIX4: View/log fixes from GT QA of b2f9fad

Branch `dev/step3-view`, base `b2f9fad`. **Not committed.** View code and tests only: no Sim change, `SimVersion.Rules` is still 12, goldens unchanged.

**Precheck:** the first `git status` still showed the BB-BAL2 edits, so I stopped and reported. After Dev cleaned the tree I re-checked: none of the BB-BAL2 code or test files remained; only the untracked BB-BAL2 docs (`docs/claude-runs/bbbal2.md`, `bbbal2/`) were left, and they stay out of this diff.

## 1. Items: root cause, fix, file and line

### Item 1: boss hint missing on the loss result and CHOOSE YOUR RETRY screens

**Root cause:** the hint (`BossText.Hint`) was only drawn by `PrepareView` (ENEMY box, `PrepareView.BossLines`). A retry goes from the result screen to the retry pick and straight into the fight, so Prepare is never shown for a retry and the player never sees the hint again.

**Fix:** one shared provider, then each screen draws its lines in the Prepare style.
- **Provider:** `BossText.NameAndHint(bossId)` (`View/Run/BossText.cs:36`) returns the boss name, then the hint wrapped by the same `PrepareView.BossLines(null, id, PrepareView.TraitsWidth)` that Prepare uses. No string is duplicated; the text comes from the same string-table rows.
- **Width:** `PrepareView.TraitsWidth` is now public (`PrepareView.cs:26`) so the wrap width is shared.
- **Style:** name in the Prepare boss color (`Palette.WallWarning`), hint in `Palette.Text`, 1x.
- **Loss result screen** (`View/Run/RunResultView.cs`):
  - 3 lines at y −62 / −75 / −88. That is under the NEXT box (which ends at −38) and above NEXT FIGHT (which starts at −118).
  - Shown only when the fight was not won and had a boss (`RunResultView.BossHint`, line 156; fields at line 32).
- **Retry screen** (`View/Run/StartPickView.cs`):
  - 3 lines at y −117 / −130 / −143, under the footer band (which ends at −97).
  - Shown only on the retry pick of a boss fight (`StartPickView.BossHint`, line 138; fields at line 16).

Screenshots 1–4 show both screens at 1x and 3x with no overlap.

### Item 2: SKILL UP branch button top border clips the card icon

**Root cause:** the branch buttons fill the bottom 2..30 px of the card. The icon stayed where a normal card has it:
- **Start / retry pick:** the 2x icon spans card y +26..+54, so the button's top border (at +30) covered its bottom 4 px. Because of the sprite's own margin, 1–2 px of the icon were visibly clipped (GT's report; `before-7-*`).
- **Prepare** (shorter 58 px card): the icon spans +15..+43, so the button covered most of it (`before-5-*`).

**Fix:** icon inset only while the branch buttons are shown. The buttons, `PixelButton` and every other button are unchanged.
- `SkillBranchButtons.Top = 30` (`View/SkillBranchButtons.cs:11`) is the buttons' top edge.
- **Start / retry pick:** the 2x icon moves up so its bottom sits 1 px above that edge (`StartPickView.cs:121`).
- **Prepare:** a 2x icon cannot fit a 58 px card above a 28 px button, so the icon is drawn at 1x, 1 px above the edge and left of the title (`PrepareView.cs:359`). When the buttons go away (TAKEN), the usual centred 2x icon returns.
- **Unaffected:** the 140 px START button is a different screen and class; it is unchanged (screenshot 8). The REROLL and FIGHT! buttons and the other cards are unchanged (screenshots 5 and 6).

### Item 3: TAKEN card after a branch pick showed the generic text

**Root cause:** a taken card is drawn from its stored offer identity (BB-FIX3), so a level-2 SKILL UP kept its offer text "SKILL UP" / "CHOOSE YOUR SKILL BRANCH" after the branch was chosen.

**Fix:**
- **Face:** `PrepareView.ShownFace` (`PrepareView.cs:241-257`, used at line 226). For a taken level-2 SKILL UP it sets the title to "SKILL UP - <branch>" (for example "SKILL UP - REACH", "SKILL UP - RAPID"). The first detail line becomes that branch's effect line from `SkillDefinition` (for example "REACH: LONGER LUNGE").
- **Branch source:** `RunState.EffectiveSkillBranch`, the run's real branch, which is also correct under `-bbForceSkill`.
- **Fit:**
  - Short names fit on the 2x title line.
  - Longer ones would have wrapped after the dash ("SKILL UP -" / "REACH"). `FitTitle` now keeps a " - " title on one 1x line beside TAKEN when it fits there (`PrepareView.cs:379`). No other card title contains " - ", so other cards are unaffected (SWAP TO still wraps as before).
- **After a reroll:** a reroll is only allowed before the first pick and re-deals the whole offer. The taken slot is looked up after the reroll, so the text follows the slot that was really picked.
- **Tests:** checked for all 8 weapons × A/B after one reroll, with the text inside the card and no overlap.

### Item 4: [Reflect] printed twice in Player.log

**Root cause:** two paths print the same line to the console, and with both `-bbAudioLog` and `-bbTrace` on, Player.log got each line twice (88 vs 44):
- **Audio-log path:** `AudioDirector.OnSimEvent` → `AudioLog.Write`, which does `Debug.Log` (to Player.log) and appends to `audio_log.txt`.
- **Trace path:** `ArenaView`'s fixed step, under `-bbTrace`, did its own `Debug.Log(AudioLog.ReflectLine(...))`.

`audio_log.txt` only gets the first path, hence 44.

**Fix:** `ArenaView.TraceReflect` (`View/ArenaView.cs:172`, called at line 282) skips the trace copy when the audio log is on. Player.log now gets each line once either way: from `AudioLog.Write` when `-bbAudioLog` is on, from the trace when only `-bbTrace` is on. `AudioLog.cs`, `AudioDirector.cs` and `audio_log.txt` are unchanged.

**[Skill] checked:** no duplicate. `[Skill] ...` lines come only from `AudioLog.Write`. The `-bbTrace` path prints a different line, `[BallBattle] SkillFired ball=.. skill=.. level=..` (plus `SkillEnded`), which does not match `[Skill]`. No change.

## 2. Item 5: SKILL UP level 3 in Run (investigation; no code change)

**When SKILL UP is offered.** It is not tied to a fight number. `CardOffer.Generate` (`Sim/Run/CardOffer.cs:46-47`) adds a SKILL UP for the next level to every offer while the effective skill level is below 3:
- the start pick, every Prepare offer (fights 1–8, each reroll), and the retry pick;
- weight 1.5 against traits 7.5, DAMAGE 2, SPEED 2, MAX HP 1 and HEAL 1–3;
- 3 cards are drawn without replacement.

Measured on seeds 1–2000:
- The start pick offers SKILL UP LV2 31.8% of the time.
- After it is taken, the first Prepare offers SKILL UP LV3 33.8% of the time.

**Is level 3 reachable?** Yes. Taking the LV2 card (branch A or B) makes the next offers contain the LV3 card. Picking it ("<BRANCH> LV 3", one button) gives level 3. After that no SKILL UP is offered.

**What `-bbForceSkill` does in Run.** It sets `RunState.SkillOverride` at run start (`RunFlow.cs:126`) and again on CONTINUE (`RunFlow.cs:143`). Offers, card projection (`RunState.OfferCard`) and the match all use the effective (forced) level.
- **`1`:** normal level 1, so LV2 cards are offered as usual.
- **`2a` / `2b`:** starts at level 2 with that branch. Every SKILL UP offered is the **LV3** card (the "second" SKILL UP), possibly already in the start pick. Picking it gives level 3.
- **`3a` / `3b`:** starts at level 3. **No SKILL UP is ever offered:** the generator skips it, and a stored SKILL UP is shown as MAX HP. That is why GT never saw it with 3a/3b. It is expected, not a bug.
- **With 2a in GT's 4 seeds:** the LV3 card is only in about one offer in three, so 4 seeds that all ended at fight 4 can easily miss it.

**Debug-flag quirk, not changed:** CONTINUE re-applies the flag's level, so after taking the LV3 card under `2a`, quitting and continuing drops the effective level back to 2. It only matters when resuming a forced run, it is not what GT hit, and it is arguable whether it's a bug, so I left it as is.

**Seeds that reach the second SKILL UP offer with no fight to win:**
- The LV2 card is in the start pick. Take it with branch A.
- The fight 1 Prepare offer then contains the LV3 card.
- Checked for both a fresh profile and a fully unlocked profile (same start choices). For other unlock states the offers can differ; use the forced 2a line in that case.

| seed | weapon (start choice) | start pick: SKILL UP LV2 | fight 1 Prepare: SKILL UP LV3 | with `-bbForceSkill 2a`: LV3 in start pick |
|---|---|---|---|---|
| 2 | MANTIS / fang (1st) | card 2 | card 2 | card 2 |
| 4 | EMBER / blade (1st) | card 3 | card 2 | card 3 |
| 17 | MANTIS / fang (1st) | card 1 | card 3 | card 1 |

Command lines (standalone build; same arguments in the editor's command line):
```
BallBattle.exe -bbSeed 2  -bbTrace -bbAudioLog          (RUN -> 1st weapon -> SKILL UP card 2, branch A -> Prepare fight 1: LV3 in card 2)
BallBattle.exe -bbSeed 4  -bbTrace -bbAudioLog          (RUN -> 1st weapon -> SKILL UP card 3, branch A -> Prepare fight 1: LV3 in card 2)
BallBattle.exe -bbSeed 17 -bbTrace -bbAudioLog          (RUN -> 1st weapon -> SKILL UP card 1, branch A -> Prepare fight 1: LV3 in card 3)
BallBattle.exe -bbSeed 2  -bbForceSkill 2a              (start pick card 2 is already SKILL UP LV3)
```

Notes for GT:
- Do not add `-bbForceTrait`, which replaces a card in the first offer.
- The LV3 card is only in the first (not rerolled) Prepare offer.

Computed by a throwaway scan over the real `RunState` / `CardOffer` (outside the repo, `E:\ball-battle-fix4-out\scan`; not in the diff).

## 3. Screenshots (`docs/claude-runs/bbfix4/`, 270x480 at 1x and 3x)

| # | screen | files |
|---|---|---|
| 1 | Loss result, SHELL KING (fight 4, retry) + hint | `1-result-loss-shellking-1x.png`, `-3x.png` |
| 2 | CHOOSE YOUR RETRY, SHELL KING + hint (two-line hint) | `2-retry-shellking-1x.png`, `-3x.png` |
| 3 | Loss result, WARDEN "MOVING WALLS SPIKE YOU." | `3-result-loss-warden-1x.png`, `-3x.png` |
| 4 | CHOOSE YOUR RETRY, WARDEN | `4-retry-warden-1x.png`, `-3x.png` |
| 5 | SKILL UP branch buttons, Prepare (icon 1x above the buttons) | `5-skillup-buttons-prepare-1x.png`, `-3x.png` (before: `before-5-skillup-buttons-prepare-3x.png`) |
| 6 | TAKEN card after picking branch A (after a reroll): "SKILL UP - REACH" | `6-skillup-taken-branch-a-1x.png`, `-3x.png` (before: `before-6-skillup-taken-branch-a-1x.png`) |
| 7 | SKILL UP branch buttons, start pick (2x icon clear of the border) | `7-skillup-buttons-startpick-1x.png`, `-3x.png` (before: `before-7-skillup-buttons-startpick-3x.png`) |
| 8 | 140 px START button (Versus page), unchanged | `8-versus-start-button-140px-1x.png`, `-3x.png` |

`8-pick-weapon-start-button-*.png` is an obsolete first capture of the weapon pick screen, which has no START button. Nothing may be deleted here, so it is still in the folder; Dev can leave it out of the commit.

## 4. Tests

| suite | total | passed | failed | skipped |
|---|---|---|---|---|
| SimTests (`dotnet test`) | 320 | 320 | 0 | 0 |
| Unity EditMode (6000.5.1f1 batchmode) | 419 | 412 | 0 | 7 |

- The 7 skips are the env-gated capture/diagnostic tests, the same as before.
- `CaptureFix4Screens` ran because `BB_FIX4_OUT` was set.
- Unity was not open.

New file `Assets/Tests/EditMode/BbFix4Tests.cs` (+ `.meta`):
- **Item 1:**
  - `LossResultAndRetryShowTheBossHint` ×3 (SHELL KING, MAGNET, WARDEN). Checks the provider text (name + Prepare hint, wrapped like Prepare), the drawn lines and colors, that nothing goes off screen or overlaps, and the hint stays clear of the NEXT box, NEXT FIGHT, the footer band and the cards.
  - `NoBossHintAfterANormalLossOrOnTheStartPick`.
- **Item 2:** `BranchButtonsLeaveThePrepareIconFullyVisible` ×3 slots and `BranchButtonsLeaveTheStartPickIconFullyVisible` ×3 slots. The icon must sit at least 1 px above the button top, inside the card and left of the title, and return to 2x and centred when TAKEN.
- **Item 3:** `TakenSkillUpNamesTheChosenBranchAfterAReroll` ×8 weapons, each with branches A and B. Checks the title, the effect line, that the title is one line and never split after the dash, and that it fits with no overlap.
- **Item 4:** `ReflectAndSkillLinesReachTheConsoleOnce` (audio log on / off). It drives both paths (AudioDirector and `ArenaView.TraceReflect` with trace on) and checks each `[Reflect]` line reaches the console exactly once. With the audio log on, the audio log keeps its line. `[Skill]` reaches the console once per SkillFired with the audio log on, and 0 times from the trace path.
- **Capture:** `CaptureFix4Screens` (`BB_FIX4_OUT`).

Updated test (intended change): `Night1ViewTests.TakenAfterRerolls` used to pin the taken SKILL UP title to "SKILL UP". It now expects "SKILL UP - <branch A>" for the branch pick. The forced case keeps "SKILL UP", because there the taken card is the LV3 card with no branch choice.

## 5. Rules and goldens

- `SimVersion.Rules` = 12, unchanged (`Assets/Scripts/Sim/SimVersion.cs`).
- No file under `Assets/Scripts/Sim/` or `SimTests/` changed.
- All goldens (CrossPlatform, Versus, SpikeCorner arena digests) are untouched, and all pass in the 320/320 SimTests run.

## 6. Files in the diff

`git status --porcelain`, BB-FIX4 entries only:

```
 M BallBattleUnity/Assets/Scripts/View/ArenaView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/BossText.cs
 M BallBattleUnity/Assets/Scripts/View/Run/PrepareView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunResultView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/StartPickView.cs
 M BallBattleUnity/Assets/Scripts/View/SkillBranchButtons.cs
 M BallBattleUnity/Assets/Tests/EditMode/Night1ViewTests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbFix4Tests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbFix4Tests.cs.meta
?? docs/claude-runs/bbfix4.md   (this report)
?? docs/claude-runs/bbfix4/
```

These are left out of the diff and unchanged:
- the 5 pre-existing modified non-Assets files (`.claude/hooks/.logs/hook-log.jsonl` and 4 `docs/claude-runs/trace-unity-*`);
- the pre-existing untracked files, including the BB-BAL2 docs.

Not touched: `AudioLog.cs`, `AudioDirector.cs`, any audio playback code, any Sim or SimTests file, and the art files (`warden_spike_*_fade.png`).

Temp-index diff: a copy of `.git/index` was used through `GIT_INDEX_FILE`; the paths above were `git add`ed into the copy, then I ran `git diff --cached --stat`. The real index is untouched. The stat is taken before this report existed; `docs/claude-runs/bbfix4.md` is one more new file.

```
 BallBattleUnity/Assets/Scripts/View/ArenaView.cs                  |  20 +++++--
 BallBattleUnity/Assets/Scripts/View/Run/BossText.cs               |  13 ++++
 BallBattleUnity/Assets/Scripts/View/Run/PrepareView.cs            |  35 ++++++++++-
 BallBattleUnity/Assets/Scripts/View/Run/RunResultView.cs          |  14 +++++
 BallBattleUnity/Assets/Scripts/View/Run/StartPickView.cs          |  16 +++++
 BallBattleUnity/Assets/Scripts/View/SkillBranchButtons.cs         |   2 +
 BallBattleUnity/Assets/Tests/EditMode/BbFix4Tests.cs              | 416 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 BallBattleUnity/Assets/Tests/EditMode/BbFix4Tests.cs.meta         |   2 +
 BallBattleUnity/Assets/Tests/EditMode/Night1ViewTests.cs          |   4 +-
 docs/claude-runs/bbfix4/1-result-loss-shellking-1x.png            | Bin 0 -> 8306 bytes
 docs/claude-runs/bbfix4/1-result-loss-shellking-3x.png            | Bin 0 -> 28461 bytes
 docs/claude-runs/bbfix4/2-retry-shellking-1x.png                  | Bin 0 -> 10130 bytes
 docs/claude-runs/bbfix4/2-retry-shellking-3x.png                  | Bin 0 -> 30954 bytes
 docs/claude-runs/bbfix4/3-result-loss-warden-1x.png               | Bin 0 -> 8165 bytes
 docs/claude-runs/bbfix4/3-result-loss-warden-3x.png               | Bin 0 -> 28310 bytes
 docs/claude-runs/bbfix4/4-retry-warden-1x.png                     | Bin 0 -> 10185 bytes
 docs/claude-runs/bbfix4/4-retry-warden-3x.png                     | Bin 0 -> 30920 bytes
 docs/claude-runs/bbfix4/5-skillup-buttons-prepare-1x.png          | Bin 0 -> 10996 bytes
 docs/claude-runs/bbfix4/5-skillup-buttons-prepare-3x.png          | Bin 0 -> 32654 bytes
 docs/claude-runs/bbfix4/6-skillup-taken-branch-a-1x.png           | Bin 0 -> 11263 bytes
 docs/claude-runs/bbfix4/6-skillup-taken-branch-a-3x.png           | Bin 0 -> 33242 bytes
 docs/claude-runs/bbfix4/7-skillup-buttons-startpick-1x.png        | Bin 0 -> 9503 bytes
 docs/claude-runs/bbfix4/7-skillup-buttons-startpick-3x.png        | Bin 0 -> 29124 bytes
 docs/claude-runs/bbfix4/8-pick-weapon-start-button-1x.png         | Bin 0 -> 8343 bytes
 docs/claude-runs/bbfix4/8-pick-weapon-start-button-3x.png         | Bin 0 -> 28499 bytes
 docs/claude-runs/bbfix4/8-versus-start-button-140px-1x.png        | Bin 0 -> 13912 bytes
 docs/claude-runs/bbfix4/8-versus-start-button-140px-3x.png        | Bin 0 -> 42002 bytes
 docs/claude-runs/bbfix4/before-5-skillup-buttons-prepare-3x.png   | Bin 0 -> 32624 bytes
 docs/claude-runs/bbfix4/before-6-skillup-taken-branch-a-1x.png    | Bin 0 -> 11290 bytes
 docs/claude-runs/bbfix4/before-7-skillup-buttons-startpick-3x.png | Bin 0 -> 29140 bytes
 30 files changed, 513 insertions(+), 9 deletions(-)
```

Not committed, nothing deleted, no stash.
