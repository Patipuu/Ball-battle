# BB-FONT1: 5x7 variable-width pixel font (EN+VI), layout pass, activeTicks in round-robin, UnlockedNames

Date 2026-10-14. Branch `dev/step3-view`, HEAD `f83add2` (BB-FIX1b, parent `4a8ef80`), unchanged. No commit, no push, no file deleted.
Preconditions checked first: HEAD `f83add2` with parent `4a8ef80`; `git status` only out-of-scope items; no Unity process, no `Temp/`; `E:\ball-battle-art-hold\font\pixelfont_5x7.json` present (14538 bytes).
Sim rules unchanged: `SimVersion.Rules` = 6, no golden file touched (`git diff` empty for SimVersion/CrossPlatform/Versus/SpikeCorner goldens).

## Files

New
- `Assets/Resources/Fonts/pixelfont_5x7.json`: byte-identical copy of Art's file (same SHA-256; the original is untouched). `.meta` and `Resources/Fonts.meta` were made by Unity.
- `Assets/Scripts/View/PixelFont.cs`: loader and validator (13 rows, equal row widths, 3..7 px, only `.`/`#`, one NFC char per key, space glyph required), a minimal JSON reader for this flat shape, a runtime point-filtered atlas, `Measure`/`MeasureWidth`, `Normalize` (NFC then uppercase; ASCII fast path), and the fallback (blank space glyph, logged once in the Editor only).
- `Assets/Tests/EditMode/BbFont1Tests.cs`: A1, A2, A3, A4 (screen catalogue + audit + `BBFONT_CAPTURE` screenshots), A5 and C.

Changed (View): `PixelText`, `TextWrap`, `PixelButton`, `DamagePopups`, `ConfirmDialog`, `MenuView`, `SettingsView`, `OverlayView`, `DebugOptions`, `GameController`, `Run/NextGoalBox`, `Run/RunTitleView`, `Run/PickWeaponView`, `Run/StartPickView`, `Run/PrepareView`, `Run/RunResultView`, `Run/RunSummaryView`, `Run/RunOverlayView`, `Run/CardText`, `Run/FightReport`, `Run/RunStrings`.
Changed (tool): `Assets/Scripts/Sim/Tools/RoundRobin.cs` (output only, see B).
Changed (tests): `SimTests/RoundRobinTests.cs`; EditMode `ArtV1Tests`, `Bb03AudioTests`, `BbFix1bTests`, `InputAndReflectFxTests`, `MarkerSecondWindAndPopupTests`, `PrepareAndFightUiTests`, `Prompt3dTests`, `ViewDataTests` (see Deviations 8).
Kept, no longer drawn: `PixelFontData.cs` (old 3x5 glyph table, still used by the Editor generator for `font_3x5.png`) and `ArtLibrary.Font`. `PixelText.Create` keeps its texture parameter so its ~125 callers are unchanged.

## A. Font

- The file has **112 glyphs**: space, `! % + - . / : ?`, 0-9, A-Z, Đ, Ă Â Ê Ô Ơ Ư and the 60 toned Vietnamese capital vowels (all 72 VI vowel forms). Widths: 3 px ×4, 5 px ×80, 6 px ×24, 7 px ×4. All NFC. See Deviations 1.
- Rows: 11 is always blank. Row 3 is blank except in the 12 horn letters (Ơ Ớ Ờ Ở Ỡ Ợ Ư Ứ Ừ Ử Ữ Ự), where Art's horn reaches into it. The test asserts exactly that.
- Drawing: real glyph widths, 1 px spacing, cell 13×scale px (position = cell bottom). Lowercase → uppercase, input → NFC (decomposed `bở trần` draws as `BỞ TRẦN`). Missing character → the blank space glyph (the old font's fallback), logged once per character in the Editor only.
- Hidden VI switch (A5): `-bbLangVi` on the command line (`DebugOptions.ParseLangVi`, applied in `GameController.Awake` before any screen is built). Off by default, so EN stays default. Test hook: `StringTable.Current = Lang.Vi`.

### Callers switched to MeasureWidth (no fixed character width left)

| Caller | Before | After |
|---|---|---|
| `PixelText.Set` / `Bounds` | `i * Advance`, `Length*4-1`, height 5 | sums real glyph widths + 1 px; height 13×scale |
| `TextWrap.MaxCharsOnScreen` | chars = 263 / (4×scale) | removed → `MaxWidthOnScreen(scale)` (px) + `FitsScreen` |
| `TextWrap.Items` | `Length <= maxChars`, cut by chars, indent = prefix length | pixel width, `Cut` by pixels, `IndentFor` (spaces as wide as the prefix); new `SplitToFit` |
| `RunSummaryView.MilestoneLines` | `(254-12+1)/4` chars | 242 px |
| `RunSummaryView.UnlockText` | `MaxCharsOnScreen(1)` | `MaxWidthOnScreen(1)` |
| `RunTitleView.StatsLine` | `full.Length <= MaxCharsOnScreen(1)` | `TextWrap.FitsScreen(full, 1)` |
| `CardText.MaxTitleChars` 18 / `MaxDetailChars` 44 | character limits | `MaxTitleWidth` / `MaxDetailWidth` = 209 px (x −86 to 123) |
| `PrepareView.FitTitle` | old `MeasureWidth` (4 px/char) | `PixelFont.MeasureWidth`; also wraps when the 2x title would pass the card edge |
| `DamagePopups.StartRect`, `ClampToArena`, wall clamp | 4 px/char, 5 px height | real width; ink box (rows 4–10) |
| `PixelButton` label y, `NextGoalBox.TextAt`, `SettingsView` (3), `MenuView` notice, `PrepareView.RerollReasonY` | `GlyphHeight` 5 | `PixelFont.CellHeight` 13 |
| `ConfirmDialog.Open` (new) | — | question x2 only if `MeasureWidth×2 ≤ 216` |
| Tests | `PixelFontData.IndexOf/MeasureWidth/GlyphHeight`, `MaxCharsOnScreen`, `.Length` limits | `PixelFont.Default.Has`, `PixelFont.MeasureWidth`, `MaxWidthOnScreen`, pixel limits |

### Layouts changed (local px, y up, 270×480 centred; "cell" = 13×scale)

| Screen | Before | After |
|---|---|---|
| NextGoalBox (menu, Run Title, Summary) | 254×52; label/title x2/sub at 2/14/36 px | 254×**60**; cells at 2/15/43; count at 15, bar under it at 31 |
| Menu hub | RUN label y48 x3, CONTINUE y63 x2, line y41; rules y21; gear 12×12 | RUN y36, CONTINUE y52, line y37; rules y17; gear tap target **24×24** (drawing unchanged) |
| Old-run notice | text x2 (258 px, wider than the box); OK 80×22 x2 | text **x1** y17; OK **80×24** x1 at y −18 |
| Versus pick | BALL 1/2 labels x2 overlapping the subtitle; name right of the icon (WRECKER/SPARKLER cut); ARENA 18 px, RANDOM 22, START x3, BACK 22 | labels **x1**; icon on top, name **centred under it**; ARENA/RANDOM **24 px**; START **x2**; BACK **32 px** |
| Settings | MUSIC/SFX tracks 16 px, labels overlapping; LANGUAGE over EN/VI; BACK 120×22 | tracks **200×24** at 150/214 (screen), labels at 122/186; LANGUAGE 250, EN/VI boxes 40×**28** at 280; BACK **120×32** (primary yellow, x2 label kept) |
| Run Title | rules band 4 lines @10 px; BACK 120×22 | band lines @**13 px**; a line wider than 246 px **wraps at its "  "** and the band grows (VI: 5 lines); BACK **120×32** |
| Confirm dialog (all 3) | question x2 at top−22, info at top−34 (overlapping) | question x2 at top−32 (**x1** when x2 is wider than 216 px, e.g. ABANDON CURRENT RUN?), info at top−49, YES/NO unchanged |
| Pick weapon | — | unchanged (fits) |
| Start pick | CHOOSE/YOUR START x3 (off screen); cards 66 px; text x −83 | titles **x2** at 212/186; cards **70 px** at 92/16/−60; text x **−86**, icon −101; footer band at −97..−64 |
| Prepare | header 210, boxes 126..188 and 58..120; cards **36 px** at 0/−42/−84; title x2 22, tag 25, details 12/4; REROLL/FIGHT −130 (FIGHT x3); reason inside REROLL; ESC −150 | header 212; ENEMY box 113..196 and YOU 26..109, each with **two traits rows** (wrapped between traits) and stats; pick hint 11; cards **58 px** at −49/−110/−171 (title x2 30, tag + wrapped line 1 at 43, wrapped line 2 at 30, details 16/2); text x −86, right limit 123; REROLL/FIGHT −205 (FIGHT **x2**); reason −220; ESC −235; arena name **right-aligned** at 121 |
| Fight result | title x4; rows @11/9 px; run line x3; next box −10..58 | title **x3** 177; stat rows @**13** (HP 145, DEALT 132, FOE HEALED 119, TAKEN 106, COINS 93); lines 75/62/49; run line **x2** 14; next box −38..46 with **two traits rows**; NEXT FIGHT/SUMMARY unchanged |
| Summary | stats @10 px; milestone lines @9; goal at 248; NEW RUN −122, MENU −166 | title 190, fights 175, W/L x2 148, bosses 134, best 121, retry 108; milestone lines @**13** (frame top at screen 136, still 10 lines); goal at screen **300**; NEW RUN **−160**, MENU **−200** |
| Fight HUD / Run overlay | x2 speed 44×18 at −234; banner band 24 px; retry token at (115,210) over the HP digits; retry notice 150×62; countdown digit drawn under the notice | x2 **44×24** at −238; band **28 px** (x2 banner centred); token at (115,**229**); notice **160×76** (RETRY x3 0, lines −14/−28); countdown **hidden while the retry notice is up** |
| Versus overlay | banner band 24; result title 120 x3 over the x4 score | band **28**; title at **128** |
| Damage numbers | box = 5×scale; StackStep 12 | box = ink rows 4–10 (7×scale, 2×scale above position); StackStep **15** |

Buttons ≥ 24 px tall everywhere (the audit checks it). Raised from below 24: gear 12, sliders 16, BACK 22 → 32 (×3 screens), notice OK 22, ARENA 18, RANDOM 22, x2 18.

### Strings that do not fit

None left. Nothing was shortened. One string needed a layout change:

| Lang | String | Width | Space | Fix |
|---|---|---|---|---|
| VI | `THUA LẦN 1: CHỌN THẺ MIỄN PHÍ  ĐẤU LẠI VỚI 60% HP` | 272 px | 246 (Run Title band; screen 262) | wraps at "  " onto 2 lines: `THUA LẦN 1: CHỌN THẺ MIỄN PHÍ` / `ĐẤU LẠI VỚI 60% HP` |

Other VI rows fit on one line: `KHÔNG ĐỔI THẺ Ở LƯỢT ĐẦU` 138 px (StartPick footer, 246 px space), `BOSS Ở TRẬN 4 VÀ 8` 98 px. Widest EN texts: card detail `ONLY OFFERED IF ALL TRAITS STILL WORK` 209 px = exactly the card text column; `LOSE ONCE: FREE PICK  RETRY AT 60% HP` 203 px.
Only 5 strings are in the VI table (BB-FIX1b keys); every other screen text is still EN in VI mode. The VI audit therefore checks those 5 in place, plus all EN text, with VI selected.

## B. Round-robin activeTicks

`RoundRobin.PlayDetailed` returns `(winner, ticks, activeTicks)`; `MatchSim.ActiveTick` is read once after the last step. `Play` keeps its signature and result. New columns go after the existing tick columns:
- pair CSV: `avgActiveTicks,medianActiveTicks,maxActiveTicks`
- weapon CSV: `avgActiveTicks,medianActiveTicks,maxActiveTicks` (median over all of the weapon's matches, both sides)

Integer arithmetic only. Same args twice → byte-identical.

Sample (`-seeds 20 -arena all`, run twice via `BB_RR_ARGS … --filter Name=RoundRobinCli`; both files identical, SHA-256 `5130d889…ec07` main / `9c08fc66…7a5f` weapons):
```
arena,weaponA,weaponB,matches,winsA,winsB,draws,winRateA,avgTicks,medianTicks,maxTicks,avgActiveTicks,medianActiveTicks,maxActiveTicks
classic,blade,blade,20,13,7,0,0.6500,3640.8,3357.5,5510,3510.2,3222.5,5246
classic,blade,fang,20,12,8,0,0.6000,3781.1,3629.0,5279,3629.0,3463.5,5153
spike-walls,blade,fang,20,13,7,0,0.6500,2608.9,2637.5,3912,2511.7,2570.0,3798

weapon,matches,wins,draws,winRate,avgTicks,avgActiveTicks,medianActiveTicks,maxActiveTicks
blade,2240,1128,0,0.5036,3090.2,2967.6,2869.0,6446
venom,2240,1172,2,0.5232,3194.3,3065.8,2924.5,10800
aegis,2240,1110,1,0.4955,3788.2,3630.4,3554.0,6815
```
`venom` 10800 = one match that hit the 180 s cap (ActiveTick never exceeds `CapTicks`).

## C. UnlockedNames

`RunStrings.UnlockedNames(id)`: a goal id resolves through its `UnlockRules.GoalTable` row and that row's `Rewards`. A plain achievement without a goal row (boss2, boss3) falls back to its own unlock row. Weapons show their ball name (WeaponNames), traits their registry name. Ids that are neither a weapon nor a trait are skipped, so it never returns an id.
Test: boss1 → GLASS CANNON; win1 → SPARKLER, POISON TIP, MIRROR, SPORE, BUBBLE, PARRY MASTER; win3 → HIVE, TWIN BLADE; boss2 → SPARKLER, POISON TIP; unknown id → empty. The test fails if a new goal with rewards has no expected entry.

## Tests

- **SimTests** (default `dotnet test BallBattleUnity/SimTests/SimTests.csproj`): **211 pass, 0 fail, 0 skip** (210 + `ActiveTickColumnsArePresentBoundedAndDeterministic`), 1 m 37 s. Explicit tools not run (as before). TRX: `bbfont1-simtests-final.trx`.
- **EditMode** (Unity 6000.5.1f1 batchmode, Editor closed): **270 total, 268 pass, 0 fail, 2 skip** (`CaptureScreens`, `CaptureBb01Screens`). The final run had `BBFONT_CAPTURE` set, so `CaptureFontScreens` ran and passed; without it, it is skipped (267 pass / 3 skip). 12 new tests in `BbFont1Tests`. No compiler warnings in changed code. Results: `bbfont1-editmode.xml` (log next to it, ignored by git).
- A4 audit: **37 screens/states × EN and VI**, 0 issues. Checks: every text cell inside 270×480, no two text cells overlapping, every button label inside its button, every button ≥ 24 px. Covers menu (new/continue/all done), notice and hub under it, Versus pick, Settings, Run Title (+ABANDON dialog), pick weapon, start pick (start/retry), Prepare (fresh/boss/blocked/taken/reroll again/SKIP/SWAP dialogs), result (win/lost+retry/draw/run over/run won), Summary (4), Versus countdown/banner/result, Run countdown/retry countdown/retry notice/playing/fight over (real RunController + ArenaView). The first audit found 230 EN / 232 VI issues. Screenshots of every state (EN and VI) were reviewed by eye. They are outside the repo in `E:\ball-battle-bbfont1-out\run5\shots`.

## Deviations

1. **112 glyphs, not 113.** Compared with the full Vietnamese capital set, the only missing character is the comma, which the old 3x5 font never had (texts avoid commas). I did not add a glyph (Art's file must not be edited). The test asserts the exact 112-character set.
2. **Row 3** is used by the horn letters in Art's data (see A). The test asserts the real usage instead of "row 3 always blank". Validation on load checks only what the spec asks (13 rows, equal widths) plus width range and characters.
3. **Fallback** = the blank space glyph (the old font's existing behaviour: unknown → space).
4. **24 px tap targets** applied to buttons that were already smaller (list above). The 12×12 gear drawing is unchanged inside a 24×24 target.
5. **BACK keeps the x2 label** (BB-FIX1b: same readable size on all 3 screens). The buttons grew to 32 px so the 26 px cell fits with the margin BB-FIX1b's test asks for.
6. **Damage numbers** use the ink box (body rows) instead of the 13-row cell: digits have no marks or dot. With full cells, stacked numbers would sit 26+ px apart in the arena. StackStep 12 → 15 (> 14 px of a x2 number).
7. **Behaviour touches beyond layout:** the countdown digit is hidden while the RETRY notice is up (it was drawn under it, the countdown is held anyway); `ConfirmDialog` drops the question to x1 when x2 does not fit; `PickWeaponView` uses `DestroyImmediate` outside play mode (it logged an error when shown twice in EditMode).
8. **Existing tests updated for the new geometry/font** (intent kept, no assert dropped):
   - `Bb03AudioTests` gear rect → 24×24 at (242,4);
   - `InputAndReflectFxTests` YES-over-card point computed from the two rects (was fixed y −40);
   - popup tests use `DamagePopups.InkTop` (the mechanical replacement had made them use the full cell);
   - `PrepareAndFightUiTests` / `Prompt3dTests` width checks in pixels;
   - `ViewDataTests` / `ArtV1Tests` font coverage against the 5x7 font, card limits in pixels;
   - `PrepareAndFightUiTests.SummaryWithEveryUnlockFitsTheScreen` de-duplicates the names it collects (with C, win1 names its goal rewards, which include boss2's).
   - `ViewDataTests.FontGlyphsAreWellFormedAndUnique` still checks the kept 3x5 data.
9. **`Sim/Tools/RoundRobin.cs`** (a tool under `Sim/`) was edited for B. It only reads `ActiveTick` after a match ends; no rule, hash or golden changed. `SimSourceScanTests` passes (no float).
10. **`CardText.MaxTitleChars` / `MaxDetailChars`** (public consts) were replaced by pixel limits `MaxTitleWidth` / `MaxDetailWidth`.
11. Audit output and screenshots were written outside the repo (`E:\ball-battle-bbfont1-out`, folder access granted for this run), so git status stays clean.

## Final git status (Windows git)

```
 M .claude/hooks/.logs/hook-log.jsonl
 M BallBattleUnity/Assets/Scripts/Sim/Tools/RoundRobin.cs
 M BallBattleUnity/Assets/Scripts/View/ConfirmDialog.cs
 M BallBattleUnity/Assets/Scripts/View/DamagePopups.cs
 M BallBattleUnity/Assets/Scripts/View/DebugOptions.cs
 M BallBattleUnity/Assets/Scripts/View/GameController.cs
 M BallBattleUnity/Assets/Scripts/View/MenuView.cs
 M BallBattleUnity/Assets/Scripts/View/OverlayView.cs
 M BallBattleUnity/Assets/Scripts/View/PixelButton.cs
 M BallBattleUnity/Assets/Scripts/View/PixelText.cs
 M BallBattleUnity/Assets/Scripts/View/Run/CardText.cs
 M BallBattleUnity/Assets/Scripts/View/Run/FightReport.cs
 M BallBattleUnity/Assets/Scripts/View/Run/NextGoalBox.cs
 M BallBattleUnity/Assets/Scripts/View/Run/PickWeaponView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/PrepareView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunOverlayView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunResultView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunStrings.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunSummaryView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunTitleView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/StartPickView.cs
 M BallBattleUnity/Assets/Scripts/View/SettingsView.cs
 M BallBattleUnity/Assets/Scripts/View/TextWrap.cs
 M BallBattleUnity/Assets/Tests/EditMode/ArtV1Tests.cs
 M BallBattleUnity/Assets/Tests/EditMode/Bb03AudioTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/BbFix1bTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/InputAndReflectFxTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/MarkerSecondWindAndPopupTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/PrepareAndFightUiTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/Prompt3dTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/ViewDataTests.cs
 M BallBattleUnity/SimTests/RoundRobinTests.cs
?? BallBattleUnity/Assets/Resources/Fonts.meta
?? BallBattleUnity/Assets/Resources/Fonts/pixelfont_5x7.json
?? BallBattleUnity/Assets/Resources/Fonts/pixelfont_5x7.json.meta
?? BallBattleUnity/Assets/Scripts/View/PixelFont.cs
?? BallBattleUnity/Assets/Scripts/View/PixelFont.cs.meta
?? BallBattleUnity/Assets/Tests/EditMode/BbFont1Tests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbFont1Tests.cs.meta
?? docs/claude-runs/bb01-02-editmode.xml
?? docs/claude-runs/bbfix1b-editmode.xml
?? docs/claude-runs/bbfix1b-simtests.err.txt
?? docs/claude-runs/bbfix1b-simtests.out.txt
?? docs/claude-runs/bbfix1b-simtests.trx
?? docs/claude-runs/bbfont1-editmode.xml
?? docs/claude-runs/bbfont1-simtests-final.err.txt
?? docs/claude-runs/bbfont1-simtests-final.out.txt
?? docs/claude-runs/bbfont1-simtests-final.trx
?? docs/claude-runs/bbfont1-simtests.err.txt
?? docs/claude-runs/bbfont1-simtests.out.txt
?? docs/claude-runs/bbfont1-simtests.trx
?? docs/claude-runs/bbfont1.md
?? docs/claude-runs/fx-err.txt
?? docs/claude-runs/fx-test.txt
?? docs/claude-runs/src6a6.tgz
?? docs/design/art/hud_retry_v2.png
?? docs/design/art/start_pick_v1.png
?? docs/design/boss-and-loss-rules.bak-20261011.md
?? docs/design/card-pool-v2.bak-20261011.md
?? docs/design/game-design-document.md
?? docs/design/weapon-bible.bak-20261011.md
?? plans/261013-core-loop-audio-team/plan.md
?? plans/261013-core-loop-audio-team/team-briefs.md
```
32 tracked files changed (+586 / −267). HEAD still `f83add2`.

## Unresolved questions

- Should Art add a comma glyph (113th), or was 113 a miscount?
- Only 5 strings are in the EN/VI table; translating the remaining screen text is a separate i18n task. With the font and wrapping in place, those translations still need the same width check (the audit catches overflow).
