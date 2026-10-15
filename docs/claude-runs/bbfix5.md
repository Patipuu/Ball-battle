# BB-FIX5: level-3 skill kept on CONTINUE, spike zone drawn to 35 px, spike-walls thumbnail contrast

Branch `dev/step3-view`, base `72b7805` (BB-BAL3). **Not committed.** View code and tests only: no Sim change, Rules stays 13, goldens unchanged.

## 1. Level-3 skill dropped to level 2 on CONTINUE

**Repro.** `-bbForceSkill 2a`, take the SKILL UP (LV3) card, quit, CONTINUE: the skill is back to level 2.

**Root cause.** `-bbForceSkill` lives only in `RunState.SkillOverride`, which by design (BB-10a test `ForceSkillRunIsPlayerOnlyAndNeverSerialized`) is never saved. A SKILL UP taken under the flag:
- raised the unsaved override 2 → 3;
- raised the saved build by one level (1 → 2).

On CONTINUE, `RunFlow.Continue` re-applied the flag's level (`SkillOverride = ForceSkill`, i.e. 2a), so the taken level was lost.

**Without the flag:** normal play was not affected. The build is the real level and is saved; this is now covered by a test.

**Fix:** `View/Run/RunFlow.cs`. `Continue()` uses a new `RunFlow.ResumedSkillOverride(force, build)`:
- **Level:** the forced level plus the SKILL UPs really taken since (build level − 1), capped at 3.
- **Branch:** the forced branch, or the build's branch when the flag is `1`.

The flag is still never written to the save. CONTINUE *without* the flag keeps only the real picks (2a run plus one LV3 pick resumes at level 2). That is consistent with BB-10a: forced levels are debug-only.

| flag on CONTINUE | saved build | resumed level |
|---|---|---|
| 2a | 2A (took LV3 under 2a) | **3A** (was 2A) |
| 2a | 1 | 2A |
| 3b | 1 | 3B |
| 1 | 2B / 3B | 2B / 3B |
| none | 3A (normal run) | 3A |
| none | 2A (took LV3 under 2a) | 2A (flag never saved) |

## 2. Spike zone drawn about 3 px short

**Cause.**
- A spike zone is 3/20 of the current wall: 34.5 px on the 230 px arena.
- `ArenaFrameView.Edge` drew `floor(34.5 / 8)` = 4 teeth (6 px wide, every 8 px) as a block centred in the zone.
- That covered 30 px and left about 2.25 px of bare wall at each end.
- The Sim zone (collision) was right; only the drawing was short.

**Fix:** `View/ArenaFrameView.cs`.
- **Rule:** teeth now span the zone rounded out to whole px, so a 34.5 px zone draws 35 px. The outer two teeth sit at the zone ends and the rest are spread evenly.
- **Count:** `ToothCount(from, to)` = round((span − 6) / 8) + 1. That is 5 teeth for 35 px, 3 for 24 px, 2 for 17 px (fully shrunk).
- **Gaps:** between teeth 1–5 px, no overlap.
- **Where it applies:** the same drawing serves the shrinking zones and WARDEN's full-wall spikes.

**Tests:** `BbFix1aTests` and `ArtV1CardsBossArenaTests` counted `floor(length / 8)` teeth. They now use `ToothCount` (intended change).

**Evidence:**
- `bbfix5/spike-walls-frame-1x.png`, `-3x.png`, `spike-teeth-top-left-zoom.png`.
- `spike-zones.txt`: 4 zones of 34.5 px, 5 teeth each.

## 3. spike-walls thumbnail low contrast (art: waiting for approval)

**v4 candidate:** `bbfix5/thumb_spike-walls_v4-candidate.png`, also at `E:\ball-battle-art-hold\arena_thumbs\thumb_spike-walls_v4.png`. It is 48x48, same layout as v3, colours only:
- **Floor:** 32,30,34 → 58,54,68 (was nearly the screen background).
- **Walls:** 110,100,110 → 176,166,182 and 120,126,150 → 196,206,236.
- **Spikes:** 255,70,50 → 255,104,72, with a 1 px dark-red rim (110,30,36) where they meet the floor, so they read at 1x.

**Comparison:** `bbfix5/thumb-spike-walls-v3-vs-v4.png` (8x, then 1x and 3x on the menu background).

**Not in the repo yet.** On approval it is copied over `Assets/Art/Sprites/arena_thumbs/thumb_spike-walls_v3.png` (same name, `.meta` kept) and shot in the Versus page at 1x/3x.

## 4. Tests

| suite | total | passed | failed | skipped |
|---|---|---|---|---|
| SimTests | 323 | see below | | |
| Unity EditMode | 433 | 425 | 0 | 8 |

New `Assets/Tests/EditMode/BbFix5Tests.cs` (+ `.meta`), 13 test cases:
- `LevelThreeTakenUnderForceSkill2aSurvivesContinue`
- `LevelThreeWithoutTheFlagSurvivesContinue`
- `ForceSkillStillRaisesALowerSavedRun`
- `ResumedOverrideIsForcedLevelPlusRealPicks` (8 cases)
- `SpikeTeethSpanTheWholeZoneRoundedOut`
- `CaptureFix5SpikeTeeth` (`BB_FIX5_OUT`)

The 8 skips are the 7 old env-gated captures plus `CaptureFix4Screens`.

## 5. Files

```
 M BallBattleUnity/Assets/Scripts/View/ArenaFrameView.cs
 M BallBattleUnity/Assets/Scripts/View/Run/RunFlow.cs
 M BallBattleUnity/Assets/Tests/EditMode/ArtV1CardsBossArenaTests.cs
 M BallBattleUnity/Assets/Tests/EditMode/BbFix1aTests.cs
?? BallBattleUnity/Assets/Tests/EditMode/BbFix5Tests.cs (+ .meta)
?? docs/claude-runs/bbfix5.md, docs/claude-runs/bbfix5/
(after art approval) M BallBattleUnity/Assets/Art/Sprites/arena_thumbs/thumb_spike-walls_v3.png
```

Rules 13 and goldens unchanged (no file under `Sim/` or `SimTests/` touched).

## 6. Already done in FIX4 (re-checked)
The boss hint on CHOOSE YOUR RETRY (FIX5 list item 4) was done in FIX4 and passed QA on the bc3c460 build (`bbqa-bc3c460.md`).
