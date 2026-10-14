# BB-VIEW3: UI bug sweep + audio log (View only, no Sim change)

Base: HEAD `9099ce4` (BB-BAL1), branch `dev/step3-view`, nothing changed under `BallBattleUnity/Assets` at the start, Rules 12.
- No Sim file and no SimTests file changed.
- No localization / Vietnamese changes, and no boss art paths or `BossArtEnabled` changes.
- Outputs: `E:\ball-battle-view3-out\`. Screenshots and logs are also copied to `docs/claude-runs/bbview3-shots/`.

## TASK 1: Run UI bugs

### a. Boss loss screen shows the real boss name
- **Cause:** `FightReport.NextHeader` printed a fixed "GIANT BOSS" for every boss fight. The top line in `RunResultView` ("FIGHT 4/8  GIANT BOSS  VS …") did the same.
- **Change:** `FightReport` now carries `NextBossId` (the next fight's boss, `run.CurrentBossId`) and `BossId` (the boss just fought, taken in the `Snapshot` before the result is applied).
  - Header: **"NEXT: FIGHT 4/8  SHELL KING  RETRY"**.
  - Top line: "FIGHT 4/8  SHELL KING  VS MIRROR".
  - "GIANT BOSS" is kept only as a fallback when a report has no boss id.
  - Same character count as before (SHELL KING = GIANT BOSS = 10 letters), so no layout change.

### b. Continue after a boss loss: the saved retry boss wins over -bbForceBoss
- **Cause:** `RunFlow.Continue` called `RunState.ApplyForcedBoss` whenever `-bbForceBoss` differed from the saved force. That replaces the boss of its slot, or the forced extra fight, so a saved retry could change boss. Examples: lose to SHELL KING at fight 4, restart with `-bbForceBoss magnet`, and the retry is MAGNET. Lose to a forced MAGNET at fight 1, restart with `-bbForceBoss warden`, and the retry is WARDEN.
- **Change (View: `RunFlow`, `RunState` untouched):** new `ApplyForceBossToNewFight()`.
  - It does nothing while `Run.IsRetry`: a saved retry, boss or not, keeps its saved enemy.
  - `Continue` uses it, and so does `FinishFight` right after `ApplyResult`. A deferred force therefore starts with the next new fight (after the retry), never on the retry itself.
  - Without `-bbForceBoss` nothing changes.
- **Screen:** Continue lands on the retry's own pre-fight screen. In this build that is StartPick ("CHOOSE YOUR RETRY" + one card), and the fight starts after the pick; see Deviation 1.

### c. Retry card arena label = the arena actually used (Bug A)
- **Cause:** the NEXT card on the result screen ("HP 156   ARENA …") used `run.ArenaId`, the run's own roll, while fights use `ForceArenaId ?? run.ArenaId`. With `-bbForceArena` the card showed the run's own arena (the reported "ARENA WIDE") while the fight used the forced one.
- **Change:** `FightReport.Build(…, nextArenaOverride)`. `RunFlow.FinishFight` passes `ForceArenaId`, so the card shows the arena the next fight (the retry) really uses. Without the flag it is still `run.ArenaId`.

### d. Goal title for fight 4 and fight 8 names the rolled boss
- **Cause:** `RunStrings.GoalTitle` returned a fixed "BEAT SHELL KING" for the fight-4 goal (`boss1`), and "WIN A RUN" for the fight-8 goal (`win1`).
- **Change:** `GoalTitle(goalId, bossId)` gives "BEAT MAGNET" / "BEAT WARDEN" …; `NextGoalBox.Show(progress, run)` with `NextGoalBox.GoalBoss`.
  - **A run in progress** (menu and Run title with a saved run): the boss that run meets at fight 4 / 8 (`RunState.BossFor`: its roll, or a forced boss).
  - **No run** (summary, or a menu without a saved run): the next run's boss as the progress already decides it. `BossSelection`: the first run gets SHELL KING / MIMIC; later runs never repeat the last run's boss, so with two per slot it is the other one. Checked against `BossSelection.Choose` for several seeds.
  - **Not decided** (played before bosses were remembered): "BEAT THE BOSS" / "WIN A RUN".
  - The run summary's fight-4 milestone also names the boss that run beat. The fight-8 milestone keeps "WIN A RUN" (Deviation 2).

## TASK 2: Audio

### spike_hit verify result: **check FAILED, fixed**
- **Synthetic, spike then wall:** spike_hit only, the wall cue is dropped. ✓ (the BB-6d rule)
- **Real matches before the fix** (120 matches: SPIKES arena with all 64 weapon pairs, plus WARDEN fights with all 8 weapons on all 7 arenas; 27,473 hazard events, 27,210 ball-ticks):
  - **6 ball-ticks got 2 hazard cues.**
  - All 6 were a plain wall bounce followed by a spike for the **same ball in the same tick** (two contacts in one tick, e.g. side wall then a spiked corner).
  - The BB-6d rule only drops a wall that comes *after* a spike_hit.
- **Fix:**
  - `AudioDirector` remembers each ball's wall cue (tick, voice).
  - When a spike_hit for that ball passes the gate in the same tick, it **replaces** the wall cue: the wall voice is stopped in the same frame (`SfxPlayer.Stop(voice, clip)`), its `Played` entry is removed, and the log line ends with `replaced=wall`.
  - `SfxPlayer.Play` now returns the voice it used.
- **After the fix**, the same 120 matches: 0 ball-ticks with more than one cue, max 1, and 6 of 6 wall cues replaced (`hazard-verify.txt`).

### Music lines in -bbAudioLog
`AudioLog.MusicLine` adds these lines (`t` = the director clock, real seconds):
- `[Music] track track=<id> screen=<screen>|reason=match-start result=start|same t=…` for every track request;
- `[Music] start track=<id> slot=<n> t=…` when a slot starts;
- `[Music] stop track=<id> slot=<n> t=…` when a slot stops (end of a crossfade or fade-out);
- `[Music] fadeout track=<id> cue=<fight_win|fight_lose|run_win|run_over> t=…` when a result cue ends the fight music.

They are off without `-bbAudioLog` (tested).

## Files changed (View only)

| file | change |
|---|---|
| `Scripts/View/Run/FightReport.cs` | `NextBossId`, `BossId`, `Snapshot.BossId`; `Build(…, nextArenaOverride)`; header with the boss name |
| `Scripts/View/Run/RunResultView.cs` | top line with the boss name |
| `Scripts/View/Run/RunFlow.cs` | `ApplyForceBossToNewFight` (Continue + after each result); report gets `ForceArenaId` |
| `Scripts/View/Run/RunStrings.cs` | `GoalTitle(goalId, bossId)`; boss1 fallback "BEAT THE BOSS" |
| `Scripts/View/Run/NextGoalBox.cs` | `Show(progress, run)`, `GoalBoss` |
| `Scripts/View/MenuView.cs`, `Scripts/View/Run/RunTitleView.cs` | pass the saved run to the goal box |
| `Scripts/View/Run/RunSummaryView.cs` | fight-4 milestone names the boss beaten |
| `Scripts/View/Audio/AudioDirector.cs` | music log lines; spike_hit replaces a same-tick wall cue |
| `Scripts/View/Audio/AudioLog.cs` | `MusicLine` |
| `Scripts/View/SfxPlayer.cs` | `Play` returns the voice; `Stop(voice, clip)` |
| `Tests/EditMode/BbView3Tests.cs` (+ `.meta`) (new) | 20 tests (below) |

## Screenshots and logs (`E:\ball-battle-view3-out\`, copied to `docs/claude-runs/bbview3-shots/`)

| file | shows |
|---|---|
| `1-boss-loss-shellking.png` (+ `.txt`) | fight 4 lost to SHELL KING: "FIGHT 4/8  SHELL KING  VS MIRROR", "NEXT: FIGHT 4/8  SHELL KING  RETRY" |
| `2-retry-card-forced-arena-tight.png` (+ `.txt`) | `-bbForceArena tight`, run's own arena spike-walls: retry card "HP 100   ARENA TIGHT" |
| `3-goal-fight4-magnet.png` | menu with a saved run that rolled MAGNET: NEXT GOAL "BEAT MAGNET" |
| `4-goal-fight8-warden.png` | same run, fight-4 goal done: "BEAT WARDEN" |
| `audio-log-forcedraw.txt` | Versus with force-draw + audio log (249 lines) |
| `audio-log-match.txt` | one normal Versus match with the audio log |
| `hazard-verify.txt` | the real-match hazard check numbers (after the fix) |

**Force-draw log, from the match end:**
```
--- match end: real winner 0, reported -1 ---
[Music] start track=music_result slot=0 t=6.00
[Music] track track=music_result screen=VersusResult result=start t=6.00
[Music] stop track=music_battle slot=1 t=6.50
--- cues played after the match end (Played):
```
Only `music_result` starts, and there is no win/lose stinger and no fadeout; `music_battle` just ends its crossfade.

**One match, music lines:**
```
[Music] start track=music_menu slot=0 t=0.00
[Music] track track=music_menu screen=VersusPick result=start t=0.00
[Music] start track=music_battle slot=1 t=0.10
[Music] track track=music_battle screen=Fight result=start t=0.10
[Music] stop track=music_menu slot=0 t=0.60
[Music] track track=music_battle reason=match-start result=same t=4.00
--- match end: real winner 0, reported 0 ---
[Music] fadeout track=music_battle cue=fight_win t=4.50
[Music] stop track=music_battle slot=1 t=4.80
```

## Tests

| suite | result |
|---|---|
| SimTests (default) | **320 total, 320 pass** (`bbview3-simtests.trx`) |
| Goldens | `CrossPlatformGoldenTests.cs`, `VersusGoldenTests.cs`, `SpikeCornerTests.cs` byte-identical to HEAD (same git blob hashes) and passing; `git diff HEAD -- Assets/Scripts/Sim SimTests` is empty |
| EditMode | **343 total, 337 pass, 3 fail, 3 skip** (the 3 env-var screen captures). The 3 failures also fail on an untouched copy of HEAD `9099ce4` (Deviation 3). `bbview3-editmode.xml` |
| New `BbView3Tests` | **20 / 20 pass** |

New tests:
- **Boss loss screen:** `BossLossScreenNamesTheRealBoss` ×3 (SHELL KING / MAGNET at fight 4, WARDEN at fight 8; header and top line).
- **Retry boss vs `-bbForceBoss`:**
  - `ContinueAfterBossLossKeepsTheSavedRetryBossOverForceBoss` ×2 (force MAGNET in the same slot, WARDEN in the other): the retry is the saved SHELL KING, and the force starts at the next new fight.
  - `ContinueAfterBossLossWithoutForceBossRetriesTheSameBossNormally`: same boss; a second loss ends the run, then the Summary appears.
  - `ARetryOfAForcedBossKeepsItsBossWhenAnotherForceIsGiven`.
- **Retry card arena:** `RetryCardShowsTheForcedArena`, `RetryCardShowsTheRunsArenaWithoutForce`.
- **Goal titles:** `GoalTitleShowsMagnetAtFight4AndWardenAtFight8WhenRolled`, `GoalTitleForTheNextRunUsesTheBossItWillGet`, `ForcedBossNamesTheGoalOfTheRunItIsIn`.
- **Hazard cue:** `VerifySpikeHitReplacesTheWallCueInItsTick`, `SpikeAfterAWallInTheSameTickReplacesTheWallCue`, `VerifyOneHazardCuePerBallPerTickInRealMatches`.
- **Music log:** `AudioLogHasMusicStartStopAndTrackLines`, `MusicLinesAreOffWithoutTheSwitch`.
- **Real-flow logs:** `RealLogForceDrawShowsOnlyMusicResultAtMatchEnd`, `RealLogOneMatchShowsTheMusicLines`.
- **Screenshots:** `CaptureView3Screens`.

All Run tests drive the real `RunFlow` with a memory save; a fight's result is forced by keeping the loser's HP tiny.

## Deviations

1. **"Shows its Prepare screen":** in this build a retry has no Prepare step. After a loss the flow is StartPick ("CHOOSE YOUR RETRY", one free card), then the fight starts, and Continue on a retry goes to that same screen.
   - I kept the flow. Continue now lands there with the saved boss, and the retry fight is that boss (tested).
   - Adding a boss Prepare screen to the retry flow would be a flow change; say if you want it.
2. **Fight-8 goal:** the NEXT GOAL box shows "BEAT WARDEN" / "BEAT MIMIC" as asked.
   - In the run summary, a *reached* fight-8 milestone keeps "WIN A RUN": it means the run was won, and an existing test pins it.
   - The fight-4 milestone names the boss beaten. When the next run's boss isn't decided yet, the fight-4 title is "BEAT THE BOSS" instead of the old fixed "BEAT SHELL KING".
3. **Three EditMode failures predate this task.** I checked by exporting HEAD with `git archive` into `E:\ball-battle-view3-out\head-check\` and running the same three tests there: all 3 fail identically (`head-check-editmode.xml`).
   - `Bb03AudioTests.OnlyTheBb04BossCuesAreReferenced` and `Bb04BossTests.MissingBossClipPlaysNothingDoesNotThrowAndIsLogged` still expect the boss cues to have no clips; BB-SFX3 linked them.
   - `RunFlowTests.NewRunPickWeaponFightAndResume` (line 99) expects the player to win fight 1 with seed 42; after BB-BAL1's values that fight is lost. BB-BAL1's EditMode run never happened, because the Editor was open.
   - These tests were not changed here (out of scope).
4. **TASK 2 code change:** the hazard check failed in real matches (6 cases), so the code was changed as allowed. The change also needed `SfxPlayer.Play` to return its voice.
5. **"Real log" checks run the real View code in Unity (EditMode batchmode):** `MatchFlow`, `OverlayView`, `GameController.VersusScreen` / `AudioWinner`, the real `SfxLibrary`, `AudioLog` on, a real `MatchSim`. They don't run a standalone player.
   - The game has no auto-start switch, so a player build would need manual menu input.
   - Like the BB-SFX2 force-draw check, the log lines are exactly those `-bbAudioLog` prints. The files are written by the test from the director's lines.
6. **`docs/claude-runs/trace-unity-*.txt` / `.fields.txt` (4 files) show as modified.** An existing EditMode trace test rewrites them on every run; they were already modified before BB-FIX2. Not restored (no deletes).
7. During the real-match hazard scan the test switches the Unity logger off. Its ~27k audio-log lines would otherwise make the batchmode log about 260 MB, which is what happened on one intermediate run (`editmode.log` in the out folder).

## Confirmation

- Nothing committed, pushed, stashed or deleted, including `Temp/UnityLockfile` (left by a batchmode run that stopped on a compile error, then reused; not deleted), lock files and out folders.
- HEAD unchanged: **`9099ce4`**; `git stash list` empty.
- `SimVersion.Rules` = **12** (unchanged).
- Goldens byte-identical to HEAD.
