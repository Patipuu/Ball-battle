# BB-FIX2: SHELL KING shield arc edge + WARDEN-buried bumpers (Sim, Rules 10 → 11)

Base: HEAD `ca2e8b9` (BB-10a, Rules 10), branch `dev/step3-view`; nothing changed under `BallBattleUnity/Assets` before starting. No balance values changed; no tuning runs.

## Bug C: SHELL KING shield arc

**Root cause.** The Sim's block test was already the spec's test: the contact point's angle from the SHELL KING centre to its facing, checked against the 60° half-arc with a cosine. It measured that angle **at the moment of the hit**. The QA trace (`View/ShellTrace.cs`, BB-6d) did not reuse that number. It recomputed the angle in the View from the event's contact point and the boss position **at the end of the step**. Each step runs several substeps, and after the contact the balls keep moving: more substeps, ball-ball collision, obstacles, walls. So the logged angle drifted from the judged one.

Measured over 200 real SHELL KING fights (8 weapons × 25 seeds, `BbFix2Diagnostics.ShellTraceDrift`):
- 2,727 of 4,149 judged hits had a logged angle more than 1° off; the largest drift was 35.9°.
- 34 hits were **not blocked** but were logged **under 60°**, e.g. logged 57.5 / judged 65.9, logged 58.6 / judged 68.1, logged 59.1 / judged 61.1. These are the "57.6 and 59.7 not blocked" reports: the edge only looked like 56–57°.

**Fix.**
- **One measured angle:** `ShellKingRule.ContactAngleTenths` turns (contact − boss centre) into the shell's own frame (along Facing, across Facing) and measures it with one `FixedTrig.Atan2`. The result is in tenths of a degree, rounded once to the nearest 0.1°, all in Fix.
- **Block test:** `InArc` blocks when that angle is ≤ 600, i.e. ≤ 60.0°.
- **Same number on the event:** the angle travels `BossBlocks` → `ComputeHitDamage` → `ApplyHit` / projectile hit → the event, as the new fields `SimEvent.ArcMeasured` and `ArcAngleTenths`.
- **Trace:** `ShellTrace` now prints `ArcAngleTenths / 10` and no longer recomputes anything, so the log and the block use the same number by construction.

Blocked hits already skipped weapon growth (`OnHit`), on-hit hooks, `HitCount` and `StatChanged`; that is unchanged and now covered by tests for EMBER and WRECKER.

## Bug B: obstacles buried by WARDEN walls

**Root cause.** The drawing and the collision code already read the same flag (`MatchSim.ObstacleActive`). The flag was set by a centre-outside rule, and only on WARDEN steps.

In the seed 23 repro (bumpers arena, 230 × 230), step 1 at tick 450 moves the walls to ±104.65. The corner bumpers sit at ±95 with radius 6.75, so their outer edge (101.75) is still inside and only 2.9 px from the wall. They stayed solid until step 2 at tick 930 (walls ±94.3, centre outside). Before the fix, the last corner-bumper hit in each of the six fights fell between tick 792 and tick 924.

**Fix (rule chosen by you: "gap smaller than a ball").** `MatchSim.CoverObstacles`, boss fights only:
- On any ActiveTick where a wall **moves in** (a WARDEN step, or the normal shrink after `ShrinkStartTick`), every obstacle is switched off that same tick if the moved wall now overlaps it or leaves a gap behind it smaller than a ball diameter. The diameter is that of the smallest ball, 32 px.
- It never comes back.
- Walls that didn't move are not checked. At full size the corner bumpers are already only 13.25 px from the walls, so checking every tick would remove them at tick 1.
- The check runs in `BossPreTick`, before any movement, so nothing collides with a buried obstacle on that tick.
- New hashed state: `coverArena`, the rect the obstacles were last checked against.
- This replaces the centre-outside rule; buried obstacles stay hidden by `ArenaFrameView.ShowObstacles` as before.

**Result in the repro:** the four corner bumpers switch off at tick 450 in all six fights. The last corner-bumper hit is at tick 444 or earlier.

## Files changed

| file | change |
|---|---|
| `Assets/Scripts/Sim/Boss/BossRules.cs` | `ShellKingRule.ContactAngleTenths` (Atan2 in the shell frame); `InArc` ≤ 60.0°; `HitArcAngleTenths` override |
| `Assets/Scripts/Sim/Boss/BossRule.cs` | virtual `HitArcAngleTenths` (−1 = not judged) |
| `Assets/Scripts/Sim/Boss/MatchSim.Boss.cs` | `BossBlocks` returns the judged angle; `CoverObstacles` + `SmallestBallRadius` replace `DisableObstaclesOutside`; `coverArena` state + hash |
| `Assets/Scripts/Sim/MatchSim.Damage.cs` | `ComputeHitDamage` passes the angle out |
| `Assets/Scripts/Sim/MatchSim.Collisions.cs` | melee hits carry the angle to the Hit event |
| `Assets/Scripts/Sim/Projectiles/MatchSim.Projectiles.cs` | shots carry it to ProjectileHit |
| `Assets/Scripts/Sim/MatchSim.Rules.cs` | `Emit` fills `ArcMeasured` / `ArcAngleTenths` |
| `Assets/Scripts/Sim/SimEvent.cs` | new fields `ArcMeasured`, `ArcAngleTenths` |
| `Assets/Scripts/Sim/SimVersion.cs` | Rules 10 → **11** |
| `Assets/Scripts/View/ShellTrace.cs` | logs the event's judged angle |
| `SimTests/BbFix2Tests.cs` (new) | 8 tests (10 cases), below |
| `SimTests/BossTests.cs` | centre-outside obstacle test rewritten for the buried rule |
| `SimTests/CrossPlatformGoldenTests.cs` | Rules 11 pin |
| `Assets/Tests/EditMode/BbFix2Tests.cs` (+ `.meta`) (new) | 3 trace tests |
| `SimTests/BbFix2Diagnostics.cs` (new, Explicit) | repro + trace-drift report (not part of any suite) |

## New and changed tests

SimTests `BbFix2Tests`:
- `ArcEdgeIsSixtyDegreesAndTheEventCarriesTheJudgedAngle`: body blow at 59.9° is blocked, 60.0° is blocked, 60.1° is not; the event's angle equals the input.
- `ContactAngleIsMeasuredFromTheBossCentreAgainstTheFacing`: 59.9 / 60.1 / 90 / 180 and `InArc` at the edge.
- `BlockedWreckerBlowDoesNotGrowItsDamageOrSpeed`: `DamageFactor` and `MaxSpeedBonus` are unchanged after a blocked blow; control at 60.1° grows them.
- `BlockedEmberHitDoesNotGrowItsDamage`: `CurrentDamage` is unchanged at 30°; control at 90° grows it.
- `InRealFightsBlockedEqualsLoggedAngleAtMostSixty`: 32 real fights, melee and shots; blocked ⇔ logged angle ≤ 60.0 for every judged hit.
- `WardenSeed23BumpersBuriedAtStepOne`: the repro (every start weapon, fight + retry). Bumpers off at 450; last corner-bumper hit ≤ 450.
- `PillarCoveredByAWardenStepStopsCollidingThatTick`: the pillar collides at tick 449 (control) but not at 450; the centre pillar is never buried.
- `NormalShrinkBuriesObstaclesOnTheSameTick`: SHELL KING fight, shrink from 5400. Each obstacle goes off on exactly the tick predicted from `MatchConfig.ArenaAt` (corner bumpers 5401).

Other test changes:
- SimTests `BossTests.ObstaclesBuriedByAWardenStepAreDisabledOnThatTick`: replaces `ObstaclesWhoseCentreIsOutsideTheNewWallsAreDisabled`. (100,100) r5 now goes at step 1, (0,−50) r8 at step 3.
- EditMode `BbFix2Tests`:
  - `TraceLogsTheJudgedAngleAtTheArcEdge` (59.9 → `angle=59.9 … blocked=true`, 60.1 → `angle=60.1 … blocked=false`).
  - `TraceAngleIsTheJudgedOneEvenWhenTheBossMovesAfterTheContact`: logs 54.7, blocked; the old end-of-step method gave 47.9.
- The existing BB-6d trace test (0° / 90°) still passes.

## Test counts

| suite | result | BB-10a |
|---|---|---|
| SimTests (default) | **320 total, 320 pass, 0 fail** | 310 run, 309 pass (1 fail: `SameSeedAndChoicesReplayTheWholeRun`; its recovery run passed) |
| EditMode | **323 total, 320 pass, 0 fail, 3 skip** (the 3 screen-capture tests, as before) | 0 fail |
| Explicit weapon gate | **2/3**: band miss `NonMirrorPairsWinBetween20And80Percent`, MANTIS vs WRECKER **15.0%** (band 20–80) | same miss, same 15.0% |
| Explicit boss gate (+ BossSweep) | **7/9**: band misses MAGNET **77.003%** (band 45–77) and WARDEN **82.4%** (band 35–70) | MAGNET 77.003%, WARDEN 83.0% |
| Explicit trait gate | **1/2**: band miss `EveryTraitAtLevel1AddsThreeToFifteenPoints`, parry-master **+15.3** (band 3–15) | same miss, same +15.3 |
| Determinism | two 50-seed round-robins byte-identical, SHA-256 `481DCB86A5EF303D0EEAC8375581289082524CE057503182EE896BE5B73D4AFE`, also identical to BB-10a's run | |

All functional tests pass. The only failures are the balance bands that already missed in 10a.

Boss sweep (1000 runs per bot):
- Greedy run win 26.6%, Random 5.2%.
- Greedy beats each boss (with retry): SHELL KING 77.4, MAGNET 77.0, MIMIC 67.7, WARDEN 82.4.
- The WARDEN move from 83.0 to 82.4 comes from the buried-bumper rule (bumpers arena only).

Goldens, re-pinned once:
- `CrossPlatformGoldenTests.ExpectedBySeed`: all 20 values changed, because the Rules field changed. Seed 1001 went `0xA518CFAAA4AB71F3` → `0x59396B36A67AF523`.
- Unchanged: the masked golden (`ExpectedBySeedMasked`), the Versus digest and all 7 arena digests.
- So Versus results are untouched; both fixes act only in boss fights. The 50-seed round-robin CSV is byte-identical to BB-10a's.

Outputs: `E:\ball-battle-fix2-out\`. Copied into `docs/claude-runs/`: `bbfix2-simtests.trx`, `bbfix2-editmode.xml`, `bbfix2-boss.md`.

## Deviations

1. **Bug B premise.** In the code at `ca2e8b9` the drawing did not hide the bumpers at tick 450; both drawing and collision used the same flag and switched at 930. I asked, and you chose the "gap smaller than a ball" rule.
   - The rule is checked only for walls that moved on that tick. Otherwise the corner bumpers (13.25 px from the walls at full size) would vanish at tick 1.
   - It applies in boss fights only. Versus and normal Run fights keep their obstacles during the normal shrink, so Versus results don't change.
2. **The rule also reaches non-WARDEN boss fights after 5400**, as the spec's "normal post-5400 shrink follows the same rule" implies. On bumpers, the corner bumpers go at tick 5401 in a SHELL KING, MAGNET or MIMIC fight that lasts that long. Boss fights' median length is about 2,500 ticks, so this is rare.
3. **0.1° resolution.** The arc edge is judged to 0.1° (`Atan2`, nearest tenth), so 60.04° counts as 60.0 and is blocked, and 60.06° as 60.1 and is not. Before, it was a cosine comparison at full Fix precision, which differs by at most 0.05° at the edge.
4. **15 untracked WAV files** appeared in `Assets/Audio/` during my EditMode run, at 10:48:40. They are `boss_*.wav` × 7 and `skill_*.wav` × 8, have no `.meta`, and keep a write time of 10:26:28.
   - They are not from this task: no code here writes them, and the EditMode log treats those cues as missing.
   - They look like another process copying in audio (perhaps the audio-team work in `plans/261013-core-loop-audio-team`).
   - Left untouched (no deletes). Please check before committing.
5. `SimTests/BbFix2Diagnostics.cs` is an Explicit, report-only file left untracked, like the earlier diagnostics files.

## Confirmation

- Nothing committed, pushed, stashed or deleted (incl. `Temp/UnityLockfile`, lock files, out folders).
- HEAD unchanged: **`ca2e8b9`** (BB-10a); `git stash list` is empty.
- `SimVersion.Rules` = **11**.
