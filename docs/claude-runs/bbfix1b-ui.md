# BB-FIX1b: UI polish + spike_hit SFX

Date 2026-10-14. Branch `dev/step3-view`, HEAD `4a8ef80` (unchanged). No commit, no push, no file deleted.
Preconditions checked first: HEAD `4a8ef80`; `git status` showed only the listed out-of-scope items; no `Temp/`, no Unity process.
`spike_hit.wav` was **absent** at the precondition check (11:03). It appeared at 11:05 during the run (added by the user), so item 11 was done (see Deviations 4).

Decisions asked before editing (answers from the user):
- No EN/VI string table existed (no `strings-vi.csv`; all text hard-coded in `RunStrings.cs`; the language row is drawn only) and the 3x5 pixel font has no Vietnamese letters → **minimal table, EN live**: VI rows stored and tested, not drawn.
- `UnlockRules.UnlockedNames` is in `Sim/**` (forbidden) → **View-side wrapper**, Sim untouched.
- Item 7 list says BRAWLER, but WeaponNames/ArtV1Tests use WRECKER → **keep WRECKER**.

Paths below are under `BallBattleUnity/Assets/`.

## Per item

| # | What changed | Files |
|---|---|---|
| — | New EN/VI string table: `StringTable` (`Get`, `Format`, `Has`, `Parse`; `Lang { En, Vi }`, `Current = En`), `StringKeys` (all new keys), CSVs in `Resources/Strings/`. Format `key,text`, split at the first comma, header/blank/`#` lines skipped, UTF-8 (NFC). VI falls back to EN; missing everywhere returns the key. | `Scripts/View/StringTable.cs` (new), `Resources/Strings/strings-en.csv`, `Resources/Strings/strings-vi.csv` (new) |
| 1 | The reroll reason line moved out of the button: scale 1, top edge 2 px below REROLL (`RerollReasonY = RerollArea.yMin - 2 - 5`); "NOT ENOUGH COINS" stays `Palette.Hot` (red). The dim "AGAIN n  COINS n" note uses the same line. | `Scripts/View/Run/PrepareView.cs` |
| 2 | Start pick footer "NO COINS  NO REROLL" → `RunStrings.StartPickNoReroll` (EN "NO REROLL ON START PICK" / VI "KHÔNG ĐỔI THẺ Ở LƯỢT ĐẦU"). | `Scripts/View/Run/StartPickView.cs`, `RunStrings.cs` |
| 3 | `RunStrings.BossAtFor(bossFights, lang)`: 1 → "BOSS AT FIGHT {0}" / "BOSS Ở TRẬN {0}"; 2 → "BOSS AT FIGHTS {0} AND {1}" / "BOSS Ở TRẬN {0} VÀ {1}"; 3+ → numbers joined by two spaces, no AND/VÀ; never "/". `BossAt` = `BossAtFor(RunTuning.BossFights, Current)` → now "BOSS AT FIGHTS 4 AND 8" on Run Title. | `Scripts/View/Run/RunStrings.cs` |
| 4 | `LoseOnceIn(lang)` from the table; VI = "THUA LẦN 1: CHỌN THẺ MIỄN PHÍ  ĐẤU LẠI VỚI 60% HP" (60 from `RunTuning.RetryHealPct`). EN unchanged. | `RunStrings.cs`, CSVs |
| 5 | `AudioDirector.OnMatchEnded(winner < 0, playerBall < 0)` (Versus draw): no fight_win/fight_lose is queued; the result screen's `music_result` is the only sound. Run-mode draws unchanged (fight_lose). | `Scripts/View/Audio/AudioDirector.cs` |
| 6 | Hit / ProjectileHit play the weapon's own clip at pitch `1 + cap × heat` (heat = `Palette.Heat(attacker weapon, StatValue)`, same as the blade tint). Cap +12%, +8% for volley (SPARKLER) and rig (HIVE). The ±3% jitter still applies, then the pitch is clamped to `1 + cap`. `RequestHit` has an optional `heat`. | `AudioIds.cs` (`HeatPitchCap`, `HeatPitchCapRapid`, `HeatPitchCapFor`, `HeatPitch`), `AudioDirector.cs` (`HeatOf`, `RequestCue(..., maxPitch)`) |
| 7 | `RunStrings.UnlockedNames(achievement)`: ids from `UnlockRules.NewRewards` mapped through `RewardName` → current names (EMBER, MANTIS, ANGLER, WRECKER, SPARKLER, SPORE, MIRROR, HIVE). The one caller (a test) now uses it. `Sim/Run/UnlockRules.UnlockedNames` is untouched (still registry names, now unused). | `RunStrings.cs`, `Tests/EditMode/PrepareAndFightUiTests.cs` |
| 8 | The summary's goal titles come from `RunStrings.GoalTitle`; the WIN-n-RUNS count was `row.Max` with a hard-coded `3` fallback. The fallback is gone, so the number now comes only from `UnlockRules.GoalTable`. BOSSES x/y already used `RunTuning.BossFights.Length` (verified by test). | `RunStrings.cs` (used by `RunSummaryView.cs`, which needed no change) |
| 9 | Settings BACK = primary: fill and border `#F4C542` (`Palette.PrimaryFill`), dark text `Palette.PrimaryText` (30,30,46). Every BACK label uses `RunUi.BackTextScale = 2` (10 px tall; was 1 = 5 px). Buttons stay 120x22. | `Palette.cs`, `Run/RunScreens.cs`, `SettingsView.cs`, `MenuView.cs`, `Run/RunTitleView.cs`, `PixelButton.cs` (`LabelScale`, `LabelColor` for tests) |
| 10 | `ShowOldRunNotice` no longer hides the hub. The hub stays drawn, the notice sorts above it (`NoticeOrder = Order + 10`; the hub tops out at Order+5), and hub buttons are disabled (input only, still drawn) until OK/`ShowHub`. Once-only logic (`OldRunNotice.Pending/Dismiss`) unchanged. | `Scripts/View/MenuView.cs` |
| 11 | `spike_hit` added to `AudioIds.Cues`. Unity imported the wav (the .meta was created by Unity) and `AudioLibraryLinker.LinkAndExit` ran in batchmode → "Audio library linked: 26 clips" (25 before). `SfxLibrary.asset` gained one line (spike_hit GUID `9e765996…`); no other audio .meta changed. A WallBounce with spike damage plays `spike_hit` only; a plain bounce plays `wall` only. | `AudioIds.cs`, `Audio/SfxLibrary.asset`, `Audio/spike_hit.wav.meta` (Unity) |
| 12 | Trailing newline added to every touched file that lacked one: `Bb0102Tests.cs`, `RunStrings.cs`, `MenuView.cs`, `RunTitleView.cs`; all new files end with one. BOM and CRLF kept. | — |

## New string keys

| Key | EN | VI |
|---|---|---|
| `startpick.no_reroll` | NO REROLL ON START PICK | KHÔNG ĐỔI THẺ Ở LƯỢT ĐẦU |
| `run.boss_at.one` | BOSS AT FIGHT {0} | BOSS Ở TRẬN {0} |
| `run.boss_at.two` | BOSS AT FIGHTS {0} AND {1} | BOSS Ở TRẬN {0} VÀ {1} |
| `run.boss_at.many` | BOSS AT FIGHTS {0} | BOSS Ở TRẬN {0} |
| `run.lose_once` | LOSE ONCE: FREE PICK  RETRY AT {0}% HP | THUA LẦN 1: CHỌN THẺ MIỄN PHÍ  ĐẤU LẠI VỚI {0}% HP |

`{0}` of `boss_at.many` = the fight numbers joined by two spaces.

## Screens with BACK

All 120x22, label scale 2:
1. Settings (`SettingsView`, from the menu gear): primary yellow.
2. Versus pick (`MenuView` Versus page): dark style kept.
3. Run Title (`RunTitleView`): dark style kept.

## Tests

- **SimTests** (`dotnet test BallBattleUnity/SimTests/SimTests.csproj`, default run): **210 pass, 0 fail, 0 skip**, 47 s. Explicit tests not run, as before. Goldens untouched: no file in `Scripts/Sim/**` or `SimTests/` changed; `SimVersion.Rules` = 6. TRX: `bbfix1b-simtests.trx`. This ran before the item-11 edits, which touch only View/EditMode files that SimTests doesn't compile.
- **EditMode** (Unity 6000.5.1f1 batchmode, Editor closed): **258 total, 256 pass, 0 fail, 2 skip** (`CaptureScreens`, `CaptureBb01Screens`, as before). That is 241 existing + 17 new in `BbFix1bTests`. No compiler errors or warnings in the changed code. Results: `bbfix1b-editmode.xml`, log `bbfix1b-editmode.log` (ignored by git).
- New `Tests/EditMode/BbFix1bTests.cs`:
  - 1: `NotEnoughCoinsIsOneSmallRedLineTwoPixelsBelowReroll`
  - 2 + table: `StartPickHintComesFromTheStringTable`, `EveryNewKeyIsInBothTables`, `ParserSplitsAtTheFirstCommaAndSkipsHeaderAndComments`
  - 3: `BossLineEnglishForOneTwoAndThreeBosses`, `BossLineVietnameseForOneTwoAndThreeBosses`, `BossLineUsesTheTuningListAndNeverASlash`
  - 4: `VietnameseRetryLineIsExact`
  - 5: `VersusDrawPlaysNoWinOrLoseCueOnlyResultMusic`
  - 6: `HeatPitchCapsAreTwelveAndEightPercent`, `HotHitsUseTheWeaponClipAndNeverExceedTheCapWithJitter`, `HitEventPitchFollowsTheAttackersHeat`
  - 7: `UnlockedNamesUseTheCurrentBallNames`
  - 8: `SummaryGoalCountComesFromTheGoalTable`
  - 9: `SettingsBackIsPrimaryAndEveryBackHasTheSameReadableSize`
  - 10: `OldRunNoticeShowsOverTheDrawnMenuAndOnlyOnce`
  - 11: `SpikeContactPlaysSpikeHitAndNotWall`
- Runs: EditMode run 1 was green (before item 11). Run 2 (after linking spike_hit) had 1 failure: `BbFix1aTests.SpikeBounceUsesSpikeHitOnlyWhenTheClipExists` asserted "no spike_hit clip added", the BB-FIX1a state that item 11 deliberately ends. Updated (see Deviations 5). Run 3 green. Each run overwrote the XML.

## Deviations

1. **String table did not exist.** Built a minimal one (user's choice): EN drawn, VI rows stored and tested only. The 3x5 font has no Vietnamese glyphs (Ô, Ở, Đ, Ầ … would draw as blanks), so VI can't be shown until the glyphs and the EN/VI switch exist. Only the new strings are in the table; older strings are still constants in `RunStrings`. EN `LoseOnce` and `BossAt` moved into the table because their VI forms are new.
2. **Item 7 in View, not Sim** (user's choice): `RunStrings.UnlockedNames`; `Sim/Run/UnlockRules.UnlockedNames` is left as is (registry names, no caller left). WRECKER kept for brawler (user's choice; the task's list said BRAWLER).
3. **Item 8 interpretation.** RunSummaryView had no hard-coded goal count of its own (the GDD's "boss count hard-coded 3" was already fixed). The only typed goal number was the `3` fallback in `GoalTitle(Win3)`, which the summary uses; it was removed.
4. **Item 11 done although the file was missing at the precondition check.** The wav appeared at 11:05 (after the check, before any build); its `.meta` and the SfxLibrary entry were made by Unity / AudioLibraryLinker. `spike_hit.wav` and its `.meta` are untracked: commit them with this change.
5. **Existing tests updated** for spec-driven changes, with no assert dropped: `Bb0102Tests` boss line "BOSS AT FIGHT 4 AND 8" → "BOSS AT FIGHTS 4 AND 8" (item 3 wording). `BbFix1aTests.SpikeBounce…` now expects `spike_hit` from the linked library and keeps the no-clip → `wall` case with a library-less director. `BbFix1aTests.MenuShowsTheNotice…` runs its text-overlap check on the notice page only, since the hub is now drawn under the notice on purpose (layering and covered input are asserted by the new item-10 test). `PrepareAndFightUiTests` calls the View `UnlockedNames`.
6. Item 1: while REROLL blinks after a denied tap, the line flashes white for 0.45 s (existing blink feedback), then returns to red.
7. Item 6 also applies to ProjectileHit (turret shots / arrows use the shooter's heat), matching how BB-03 picks the hit clip.
8. Item 9: only the Settings BACK became primary yellow (as asked); the other two keep their dark style but share the label size.
9. `Resources/` is now used: `Assets/Resources.meta` (was in the out-of-scope list, untracked) and `Resources/Strings.meta` must be committed with the CSVs.
10. Line endings: the repo uses `core.autocrlf=true`. A Linux-side `git status` showed every line changed and the `trace-unity-*` files as modified (stat/CRLF only). Windows git, which is authoritative, shows 16 files, +219/−47, and the trace files unchanged.

## Final git status (Windows git)

```
 M .claude/hooks/.logs/hook-log.jsonl
 M BallBattleUnity/Assets/Audio/SfxLibrary.asset
 M BallBattleUnity/Assets/Scripts/View/Audio/AudioDirector.cs
 M BallBattleUnity/Assets/Scripts/View/Audio/AudioIds.cs
 M BallBattleUnity/Assets/Scripts/View/MenuView.cs
 M BallBattleUnity/Assets/Scripts/View/Palette.cs
 M BallBattleUnity/Assets/Scripts/View/PixelButton.cs
 M BallBattleUnity/Assets/Scripts/View/Run/PrepareView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunScreens.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunStrings.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunTitleView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/StartPickView.cs
 M BallBattleUnity/Assets/Scripts/View/SettingsView.cs
 M BallBattleUnity/Assets/Tests/EditMode/Bb0102Tests.cs
 M BallBattleUnity/Assets/Tests/EditMode/BbFix1aTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/PrepareAndFightUiTests.cs
?? BallBattleUnity/Assets/Audio/spike_hit.wav
?? BallBattleUnity/Assets/Audio/spike_hit.wav.meta
?? BallBattleUnity/Assets/Resources.meta
?? BallBattleUnity/Assets/Resources/Strings.meta
?? BallBattleUnity/Assets/Resources/Strings/strings-en.csv
?? BallBattleUnity/Assets/Resources/Strings/strings-en.csv.meta
?? BallBattleUnity/Assets/Resources/Strings/strings-vi.csv
?? BallBattleUnity/Assets/Resources/Strings/strings-vi.csv.meta
?? BallBattleUnity/Assets/Scripts/View/StringTable.cs
?? BallBattleUnity/Assets/Scripts/View/StringTable.cs.meta
?? BallBattleUnity/Assets/Tests/EditMode/BbFix1bTests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbFix1bTests.cs.meta
?? docs/claude-runs/bb01-02-editmode.xml
?? docs/claude-runs/bbfix1b-editmode.xml
?? docs/claude-runs/bbfix1b-simtests.err.txt
?? docs/claude-runs/bbfix1b-simtests.out.txt
?? docs/claude-runs/bbfix1b-simtests.trx
?? docs/claude-runs/bbfix1b-ui.md
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

HEAD still `4a8ef80`. Logs `bbfix1b-editmode.log` and `bbfix1b-link.log` sit next to this file (ignored by git).

## Unresolved questions

- When should VI be drawn? It needs Vietnamese glyphs in the pixel font (diacritics do not fit a 3x5 cell; probably a taller VI font, as the NextGoalBox comment anticipates) plus the EN/VI toggle behaviour.
- Remove the now-unused `Sim/Run/UnlockRules.UnlockedNames` in a task that allows Sim edits?
