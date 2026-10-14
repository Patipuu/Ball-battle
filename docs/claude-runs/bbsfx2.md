# BB-SFX2: spike_hit for every spike damage, audio debug log, -bbForceDraw (View/audio only)

Date 2026-10-14. Branch `dev/step3-view`, HEAD `a23151c` (BB-FONT1), unchanged. **Nothing committed, pushed or deleted.**
Preconditions checked first: HEAD = BB-FONT1 commit `a23151c` (not `f83add2`); no tracked changes under `BallBattleUnity/Assets` (only the hook log and untracked docs/plans drafts); Unity closed. No file under `Assets/Scripts/Sim` or `SimTests` changed; `SimVersion.Rules` = 6; goldens untouched.

## Files changed

| File | Change |
|---|---|
| `Assets/Resources/Fonts/pixelfont_5x7.json` | Overwritten with Art's `pixelfont_5x7_v2.json` (same SHA-256 `E30FEF13…1477`; Art's file untouched). `.meta` unchanged (guid `14b1f9198ff507a41a3228143d7c7a13`). v2 = v1 + `,` only (2 px wide; tail in rows 9–11); no other glyph changed. 113 glyphs. |
| `View/PixelFont.cs` | `MinGlyphWidth` 3 → 2 (the comma). |
| `View/Audio/AudioDirector.cs` | `HazardCue(SimEventType, float damage, Func<string,bool> hasClip)`, a pure helper used for WallBounce and ObstacleHit (`WallBounceCue` kept as a wrapper). Volume/pitch constants. Removed the 0.7 pitch-down. Per-ball "HP last changed" tick tracked in the View. `LogLines`. `AudioLog.AnnounceOnce()` on create. |
| `View/Audio/AudioLog.cs` (new) | Debug log, off by default: `-bbAudioLog` or `AudioLog.ForceOn`. Console always; standalone builds also append to `Application.persistentDataPath/audio_log.txt` (path printed once at startup). |
| `View/DebugOptions.cs` | `-bbAudioLog`, `-bbForceDraw` (parsed once, cached; `ParseAudioLog`/`ParseForceDraw` for tests). |
| `View/MatchFlow.cs` | `ForceDraw`, `EndedInDraw`, `ReportedWinner`: with the flag, the first resolved round is reported as a draw (`Series.ReportRound(-1)`, an existing public API) and ends the match → Result after the normal pause. |
| `View/OverlayView.cs` | Round banner and result panel show DRAW when `EndedInDraw`; `ResultTitleText` for tests. |
| `View/GameController.cs` | `MatchFlow.ForceDraw = DebugOptions.ForceDraw`; audio gets `AudioWinner(winner, Run.IsOpen, Flow)`; static `VersusScreen(...)` (the music mapping, unchanged, moved out so tests use it). |
| `View/Run/RunStrings.cs`, `View/StringTable.cs`, `Resources/Strings/strings-en.csv`, `strings-vi.csv` | New key `versus.draw`: EN `DRAW`, VI `HÒA`. |
| `Tests/EditMode/BbFont1Tests.cs` | 112 → 113 (comma in the expected set); row 11 may hold ink only for `,`; the missing-glyph test now uses `#` (the comma exists now). |
| `Tests/EditMode/BbSfx2Tests.cs` (new) | Tests a–h + HazardCue + log format. |

## Final volume and pitch per cue

Volumes are before the SFX slider curve (Played.Volume = base × `VolumeCurve.Linear(sfx)`). Jitter = ±3% (`PitchJitter`) on wall / spike_hit / hit / parry.

| Event | Cue | Volume | Pitch |
|---|---|---|---|
| WallBounce, damage > 0 | `spike_hit` | **0.20** (= wall bounce volume) | 1.0 ± 3% |
| WallBounce, no damage | `wall` | 0.20 (unchanged) | 1.0 ± 3% |
| ObstacleHit, damage > 0 | `spike_hit` | **0.25** (= obstacle wall volume) | **1.0** ± 3% (was wall at 0.7) |
| ObstacleHit, no damage (bumper/pillar) | `wall` | 0.25 (unchanged) | 1.2 ± 3% (unchanged) |
| either, damage > 0 but no spike_hit clip | `wall` | same as the row above | 1.0 ± 3% |

Exactly one cue per event (`HazardCue` returns one id; the per-tick gate may drop a repeat in the same tick, logged `allowed=no`).

## Debug log

Line format (one per WallBounce / ObstacleHit cue):
```
[Audio] WallBounce tick=1699 active=1597 ball=0 dmg=3.00 hpChangedTick=1699 cue=spike_hit allowed=yes
```
Fields:
- event type and event tick (`e.Tick`);
- `active` = `MatchSim.ActiveTick` (`-` without a sim);
- ball index (`e.A`) and damage (`e.Value`);
- `hpChangedTick` = the tick that ball's HP last changed as seen by the View (AudioDirector compares HP at every event; −1 = not since the match started);
- the cue id chosen, and whether the per-tick gate allowed it.

## -bbForceDraw

Versus only, off by default. The match plays normally; when the first round resolves, the View reports it as a draw:
- `MatchFlow` ends the match as a draw → RoundOver pause → Result screen titled DRAW;
- the audio gets winner −1, so no fight_win / fight_lose;
- the Result screen maps to `music_result`, which starts exactly once.

Run mode uses `RunFlow`, which has no such switch, and `AudioWinner` passes the real winner whenever Run is open.

## Tests

- **EditMode** (Unity 6000.5.1f1 batchmode): **280 total, 277 pass, 0 fail, 3 skip** (`CaptureScreens`, `CaptureBb01Screens`, `CaptureFontScreens`: need env vars). 10 new in `BbSfx2Tests`, all pass. No compiler errors or warnings in changed code. Results `bbsfx2-editmode.xml`.
  - a. damaging WallBounce → only `spike_hit`, volume 0.2. b. damaging ObstacleHit ×50 → only `spike_hit`, pitch within 1.0 ± 3%, volume 0.25. c. harmless ObstacleHit ×50 → only `wall`, pitch 1.2 ± 3%, volume 0.25. d. harmless WallBounce → only `wall`.
  - e. Real `MatchSim` on `spike-walls` (brawler vs brawler, seed 4242): **27 spike-damage events, 27 chose and played `spike_hit`, 0 chose or played `wall`**. Each log line was checked for tick, ball, ActiveTick and HP-changed tick.
  - f. parsing; inert in Run mode. g. 113 glyphs, `,` 2 px, drawn (not the fallback). h. force-draw Versus: Result shown with DRAW, no fight_win / fight_lose / run_win / run_over, `music_result` started exactly once (and not again over 3 more seconds).
  - All tests use the real `Assets/Audio/SfxLibrary.asset` via AssetDatabase (`spike_hit` and `wall` both linked); none injects `hasClip`.
- **Layout audit** (`BbFont1Tests`): EN and VI, 37 screens each, 0 issues.
- **SimTests** (default `dotnet test`): **211 pass, 0 fail, 0 skip**. The 231 result entries (Explicit tools included) have the same names and outcomes as the BB-FONT1 run. Rules 6, goldens unchanged (no file in `Sim/` or `SimTests/` changed). TRX `bbsfx2-simtests.trx`.

## Deviations

1. **Root cause did not match the code.** No registered arena has a damaging obstacle: only pillar and bumpers have obstacles, both with `Damage = 0`. On SPIKES the spike damage comes from the walls as **WallBounce** events, which since BB-FIX1b already chose `spike_hit` (at 0.2). So a damaging `ObstacleHit` cannot happen in today's arenas. I implemented the requested change anyway (one helper for both events, no 0.7 pitch-down, 0.25), and test b uses a synthesized ObstacleHit. If the in-game symptom was "spike damage sounds like a wall", it is not explained by the ObstacleHit path. Possible causes: the quiet 0.2 volume, or a build whose `SfxLibrary` lacks `spike_hit`. The new `-bbAudioLog` line shows which cue each spike chose and whether it was gated.
2. "Do not use 0.2 for spike_hit specifically": no special volume is used; spike_hit takes the event's wall volume, which for WallBounce is 0.2, the same as before.
3. **Force-draw ends the whole match.** A Versus match normally cannot end in a draw (a drawn round is replayed), so the flag ends the match in `MatchFlow` (View) instead of only relabelling a round. A new string `versus.draw` (DRAW / HÒA) was added through the string table.
4. Test e stops after the first match that reaches ≥ 5 spike-damage events. Seed 4242 alone gave 27.
5. In the test, `Played` (capped at 256) is cleared before each hazard event so the cue of that one event can be read exactly. The first run of test e failed because of this cap (a bug in the test, not the game).
6. `PixelFont.MinGlyphWidth` 3 → 2 for the comma. BB-FONT1 tests were updated: 113 glyphs, row 11 used only by `,`, missing-glyph example `#`.

## Final git status (Windows git)

```
 M .claude/hooks/.logs/hook-log.jsonl
 M BallBattleUnity/Assets/Resources/Fonts/pixelfont_5x7.json
 M BallBattleUnity/Assets/Resources/Strings/strings-en.csv
 M BallBattleUnity/Assets/Resources/Strings/strings-vi.csv
 M BallBattleUnity/Assets/Scripts/View/Audio/AudioDirector.cs
 M BallBattleUnity/Assets/Scripts/View/DebugOptions.cs
 M BallBattleUnity/Assets/Scripts/View/GameController.cs
 M BallBattleUnity/Assets/Scripts/View/MatchFlow.cs
 M BallBattleUnity/Assets/Scripts/View/OverlayView.cs
 M BallBattleUnity/Assets/Scripts/View/PixelFont.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunStrings.cs
 M BallBattleUnity/Assets/Scripts/View/StringTable.cs
 M BallBattleUnity/Assets/Tests/EditMode/BbFont1Tests.cs
?? BallBattleUnity/Assets/Scripts/View/Audio/AudioLog.cs
?? BallBattleUnity/Assets/Scripts/View/Audio/AudioLog.cs.meta
?? BallBattleUnity/Assets/Tests/EditMode/BbSfx2Tests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbSfx2Tests.cs.meta
?? docs/claude-runs/bbsfx2-editmode.xml
?? docs/claude-runs/bbsfx2-simtests.out.txt
?? docs/claude-runs/bbsfx2-simtests.trx
?? docs/claude-runs/bbsfx2.md
   (+ the out-of-scope untracked docs/claude-runs drafts, docs/design/**, plans/** that were there before)
```
13 tracked files changed (+226 / −30). HEAD `a23151c`. Nothing committed, pushed or deleted.
