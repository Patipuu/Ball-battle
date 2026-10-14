# BB-6f: WARDEN spikes only on walls that just moved

Date: 2026-10-14 (Asia/Bangkok).
Repo: E:/ball-battle. Branch: dev/step3-view. Account: phamphu422 (Git identity phamphu422@gmail.com).
HEAD: 4bb506122f7933013e6e534f0b37ac2efe9eec87. Rules: 9.

## Status

Implemented the WARDEN window, wall selection, placeholder drawing and short English hint. Four tuning iterations completed; **iteration 2 selected**. Versus is byte-identical to 6e.

**Acceptance misses remain:** Greedy wins 24.0% of 1000 runs (target 25-40%); the 800-run gate measures 23.875% and fails. The boss gate is 7/8 because of the same overall Greedy target. Existing assertions were retained. No further tuning after four iterations.

EditMode, weapon gate, trait gate, WARDEN rule tests and determinism checks pass. Nothing committed, pushed, stashed, or deleted.

## 1. Backup before any edit

| Item | Result |
|---|---|
| Tracked binary patch | E:/ball-battle-6e-out/bb6e-wip.patch |
| Patch size | **3,238,222 bytes** |
| Untracked manifest | E:/ball-battle-6e-out/bb6e-untracked.txt |
| Untracked files copied | **43**, all present in the backup |
| Relative-path copies | E:/ball-battle-6e-out/bb6e-untracked/ |

Used git diff --binary HEAD and git ls-files --others --exclude-standard. No existing out folder was removed. BB-6d/6e working changes remain in place.

## 2. Rule and presentation changes

- A WARDEN step compares the effective arena immediately before and after changing its own step count, at the **same ActiveTick**. Normal arena shrink movement is excluded from this comparison.
- Only inward-moving Left, Right and Top walls enter the spike mask. Bottom never enters it.
- A step that leaves all effective walls unchanged opens no window. No new steps/windows occur after WARDEN reaches its step floor.
- The window is **[step ActiveTick, step ActiveTick + SpikeWindowTicks)**. Hit-stop pauses it because ActiveTick does not advance.
- Normal shrink after 5400 never opens or refreshes the window. During an already-open window, spikes follow the active smaller wall.
- Damage remains player-only, shares the normal 10-ActiveTick immunity, chooses the higher overlapping spike damage, and produces one damaging bounce event per contact.
- Existing audio tests verify one spike_hit and no wall cue for that contact.
- Drawing reads the same moved-wall mask as damage. WARDEN strips/teeth disappear on expiry; normal arena corner spikes remain.
- Hint: **MOVING WALLS SPIKE YOU.** It fits the existing 195 px line and retains WARDEN's traits line; EditMode verifies both.
- WARDEN-only WallBounce.Variant metadata now identifies the damage source (bit 7) and exact contacted spiked walls (low bits: Left=1, Right=2, Top=8). Non-WARDEN/Versus events retain Variant 0. This supports source attribution without changing damage.

Changed implementation: BossTuning.cs, BossRule.cs, BossRules.cs, MatchSim.Boss.cs, MatchSim.Obstacles.cs, SimEvent.cs, ArenaFrameView.cs, ArenaView.cs, strings-en.csv. Test/report changes: BossTests.cs, BossBalanceReport.cs, Bb04BossTests.cs, Bb6dTests.cs, new Bb6fDiagnostics.cs.

## 3. Every value changed

| Value / contract | 6e | Final 6f | Reason |
|---|---|---|---|
| SpikeWindowTicks | absent; spikes permanent after first step | **144 ActiveTicks** | New rule; start 120 exceeded WARDEN's 70% win ceiling, 144 brings it to 69.9% |
| Spiked walls | sides + ceiling after first step | only sides/ceiling moved in the latest WARDEN step, during its window | Required wall-specific hazard |
| English WARDEN hint | WALLS CLOSE. SIDES AND TOP SPIKE YOU. | MOVING WALLS SPIKE YOU. | One line, traits retained |
| MAGNET boss gate upper bound | 80% | 77% | Align gate with the requested target; gameplay value unchanged |
| Boss gate case count | 7 | 8 | Add Greedy >=15% vs WARDEN on every arena |

Temporary HP candidates were 207 -> 225 -> 222 -> **207 restored**. Window candidates were 120 -> 144 -> 120 -> 120 -> **144 selected**. No other gameplay value was changed.

| WARDEN value | 6e -> final | BB-04 it3 reference | Deviation from reference |
|---|---|---|---|
| HP | 207 -> 207 | 275 | -24.73% |
| First step | 450 -> 450 | 360 | +25% |
| Step percentage | 9% -> 9% | 12% | -25% |
| Step interval | 480 -> 480 | 480 | 0% |
| Size floor setting | 56% -> 56% | 45% | +24.44% |
| Spike damage | 2 -> 2 HP | allowed 2-3 HP | within range |

The existing step-count calculation is retained: (100 - 56) / 9 = 4 complete steps, so the actual WARDEN size limit stops at 64% of the original size. The 56% field is the minimum setting, not a newly introduced partial fifth step.

Locked values remain HP 100, radius 16, max speed 10, WRECKER starting speed bonus -3. Weapon/trait/arena/Run tuning and SHELL KING, MAGNET, MIMIC gameplay values remain as in 6e. All production Sim math remains Fix. Vietnamese resources were untouched.

### Tuning iterations

Each candidate measured 1000 runs per bot (seeds 1..1000) and 100 seeds per weapon/boss (10000..10099). No fifth candidate was tried.

| Iteration | WARDEN HP | Window | Greedy run % | Random run % | WARDEN first / with retry % | Pillar fight win % | Greedy 800-run % | Decision |
|---|---:|---:|---:|---:|---|---:|---:|---|
| 6e baseline | 207 | permanent | 17.3 | 1.8 | 31.2 / 33.9 | 6.1 | 17.3 | Prior report |
| 1 | 207 | 120 | 24.8 | 2.1 | 68.3 / 74.2 | 53.659 | not run | WARDEN above 70% |
| **2** | **207** | **144** | **24.0** | **2.0** | **64.5 / 69.9** | **50.000** | **23.875** | **Selected** |
| 3 | 225 | 120 | 23.9 | 2.0 | 63.4 / 69.4 | 50.000 | 23.625 | Lower overall Greedy |
| 4 | 222 | 120 | 24.0 | 2.0 | 64.0 / 69.9 | 50.000 | 23.750 | Same overall, lower 800-run rate |

Selected iteration 2 retains 6e HP, reaches the maximum achievable 1000-run Greedy rate while keeping WARDEN <=70%, and has the better 800-run result among the tied candidates.

### Why the Greedy targets remain unmet

WARDEN is the final fight. Changes restricted to WARDEN cannot affect how many runs reach it or the unchanged MIMIC outcomes.

On these 1000 seeds, **186 runs reach WARDEN; MIMIC contributes 110 completed run wins**. The WARDEN <=70% target permits at most floor(186 * 0.70) = 130 WARDEN wins.

Therefore the maximum Greedy total under these fixed outcomes is **(110 + 130) / 1000 = 24.0%**. Achieving 25% would require at least 140/186 = **75.269%** WARDEN wins if MIMIC remains unchanged, exceeding the WARDEN ceiling. The selected candidate has exactly 130 WARDEN wins and reaches that 24.0% bound.

The separate 800-run gate remains below 25% in the tested candidates. Its threshold was not weakened or made Explicit to hide the failure.

## 4. Final RunBot results

Overall: **Greedy 240/1000 = 24.0%**, **Random 20/1000 = 2.0%**.

“With retry” counts a boss beaten per run that met it; “first try” counts first attempts. These boss outcomes for SHELL KING, MAGNET and MIMIC are unchanged from 6e.

| Boss | Greedy first % | Greedy with retry % | Target | Random first % | Random with retry % |
|---|---:|---:|---|---:|---:|
| SHELL KING | 63.9 | 75.9 | 45-80: pass | 40.5 | 53.2 |
| MAGNET | 67.1 | 75.5 | 45-77: pass | 39.3 | 47.1 |
| MIMIC | 56.4 | 58.5 | 35-70: pass | 51.5 | 54.5 |
| WARDEN | 64.5 | 69.9 | 35-70: pass | 9.1 | 9.1 |

### Player weapon versus boss

100 seeds per cell; Greedy build at the boss slot, earlier fights counted as wins at full HP. Internal weapon IDs map to the current displayed names.

| Weapon | SHELL KING % | MAGNET % | MIMIC % | WARDEN % |
|---|---:|---:|---:|---:|
| EMBER (blade) | 78 | 83 | 51 | 85 |
| MANTIS (fang) | 87 | 91 | 52 | 84 |
| ANGLER (pike) | 81 | 83 | 46 | 76 |
| WRECKER (brawler) | 95 | 91 | 67 | 87 |
| SPARKLER (volley) | 83 | 87 | 56 | 78 |
| SPORE (venom) | 75 | 58 | 42 | 64 |
| MIRROR (aegis) | 90 | 72 | 50 | 76 |
| HIVE (rig) | 89 | 82 | 60 | 77 |

Minimum over every weapon/boss cell: **42% (SPORE vs MIMIC)**, above 25%. WARDEN's weapon minimum is **64% (SPORE)**. All measured boss time-cap rates are 0.0%.

## 5. WARDEN per arena

1000 Greedy runs, same seeds 1..1000. Rows count actual WARDEN fight attempts, including retries; this fight win rate differs from the per-run “with retry” rate above. Side and ceiling counts include damage attributed to WARDEN by the exact event metadata; an overlapping normal spike with higher damage is attributed to the normal arena instead. A corner with a side and ceiling contact is counted once in the side column.

| Arena | Fights | Greedy wins | Fight win % | Side spike hits/fight | Ceiling spike hits/fight | Time-cap fights % |
|---|---:|---:|---:|---:|---:|---:|
| classic | 38 | 22 | 57.895 | 13.868 | 4.895 | 0.000 |
| pillar | 42 | 21 | 50.000 | 19.524 | 7.476 | 0.000 |
| bumpers | 26 | 18 | 69.231 | 12.692 | 5.462 | 0.000 |
| spike-walls | 21 | 12 | 57.143 | 11.857 | 5.048 | 0.000 |
| low-gravity | 28 | 15 | 53.571 | 13.036 | 6.143 | 0.000 |
| tight | 25 | 18 | 72.000 | 10.680 | 4.480 | 0.000 |
| wide | 36 | 24 | 66.667 | 13.583 | 6.750 | 0.000 |

All seven arenas exceed the required 15%. Pillar improves from 6e's 6.1% to **50.0%**.

### Pillar damage and confinement

42 WARDEN fights; gross damage event amounts averaged per fight (healing is not subtracted):

| Source | Average damage/fight, HP |
|---|---:|
| WARDEN spikes | **54.000000** |
| Pillar / obstacle direct damage | **0.000000** |
| Boss body / attacks / combat effects | **124.685355** |
| Normal arena wall spikes | **0.000000** |

The pillar is a harmless obstacle; it can redirect the ball without dealing damage.

**No sustained pillar-to-wall pin was observed under the measured criterion.** A pin required the player to stay simultaneously within radius + 2 px of the pillar surface and a side wall for 30 consecutive ActiveTicks. There were 0 qualifying fights and a maximum consecutive proximity streak of 0. The minimum horizontal corridor clearance after accounting for the player's diameter was **16.399796 px**, so these samples did not create a zero-width trap.

There were **6140 rapid wall/obstacle alternating contact transitions** across the 42 fights (within 10 ActiveTicks between surfaces), about 146.19 per fight. This demonstrates repeated rebounds, rather than proving immobility. The diagnostic does not claim to rule out every possible seed or another definition of pinning.

## 6. Art handoff and boss warn timing

**Final SpikeWindowTicks = 144 ActiveTicks = 2.4 seconds at 60 ActiveTicks/second**, excluding hit-stop. Fade/drawing should follow the active mask and the end-exclusive boundary.

The current SFX library contains **no boss cue clips**, as the pre-existing missing-clip test also verifies. Consequently the warn events are requested at the ticks below, but **do not currently play audible sounds**; the AudioDirector records result=missing without a fallback. No audio assets were added in this Sim task.

The timing test stages a fight without hit-stop and verifies event -> AudioDirector request in the same view tick:

| Cue | First request Tick / ActiveTick | Action Tick / ActiveTick | Delay, ActiveTicks | Current playback |
|---|---|---|---:|---|
| boss_shellking_warn | 60 / 60 | shell ON: 90 / 90 | 30 | missing clip |
| boss_magnet_warn | 120 / 120 | pull begins: 180 / 180 | 60 | missing clip |
| boss_warden_warn | 390 / 390 | shrink step: 450 / 450 | 60 | missing clip |

Repeat schedule in ActiveTicks: SHELL warn 60 + 360*k -> ON 90 + 360*k; MAGNET warn 120 + 300*k -> pull 180 + 300*k. WARDEN warnings are **390, 870, 1350, 1830**, followed by steps **450, 930, 1410, 1890**. No warning/step follows the fourth step at this floor. MIMIC has no boss_mimic_warn cue; its copy announcement is on ActiveTick 1.

With hit-stop, wall-clock/Sim Tick gaps may be longer; all mechanic delays above use ActiveTick. Evidence: E:/ball-battle-6f-out/boss-warn-cues-final.csv.

## 7. Tests and goldens

| Suite / gate | Passed | Failed | Skipped / excluded | Result |
|---|---:|---:|---:|---|
| Default SimTests | **252** | **1** | 32 Explicit excluded | Greedy 800-run gate fails |
| Unity EditMode | **303** | **0** | 3 capture skips | exit 0 |
| Explicit weapon gate | **3** | **0** | 0 | pass |
| Explicit trait gate | **2** | **0** | 0 | pass |
| Explicit boss gate | **7** | **1** | 0 | overall Greedy fails |
| Focused BossTests, initial | 34 | 0 | 0 | pass |
| BossTests in final default suite | 35 | 0 | 0 | pass, including tuning-range case |
| Each iteration diagnostic (1-4) | 1 each | 0 | 0 | measurements completed |
| Round-robin determinism run A | 1 | 0 | 0 | pass |
| Round-robin determinism run B | 1 | 0 | 0 | pass |

Default SimTests TRX: total 285, executed 253, passed 252, failed 1. The adapter excludes 32 Explicit cases; notExecuted in the aggregate counter is 0. The only failure is GreedyBotWins25To40Percent: **191/800 = 23.875%**.

Boss gate RunBotsStayInTheirBands fails at **24.0%** Greedy. All four boss bands, weapon minimum, time-cap gate and the new every-arena WARDEN gate pass. The fixture now has eight actual cases: the prior seven plus the requested per-arena qualification. MAGNET's upper bound was tightened from 80 to 77.

EditMode skips: CaptureBb01Screens (BB01_CAPTURE unset), CaptureFontScreens (BBFONT_CAPTURE unset), CaptureScreens (BB_CAPTURE unset). Final XML has total 306, passed 303, failed 0, skipped 3. The aggregate is skipped/ignored due to the captures, with process exit 0.

### WARDEN regression coverage

New cases cover exact window expiry and hit-stop pause; each unmoved axis staying harmless; an effective arena already at minimum; no reopening at WARDEN's floor or by normal shrink after 5400; and the 60-240 tuning range.

All existing WARDEN tests pass. Updated assumptions:
- AfterTheNormalShrinkWardenSpikesSitOnTheActiveSmallerWall now stages a WARDEN step at 5401, then faster normal shrink during that open window. It verifies the active smaller wall still spikes; permanent spikes after expiry are no longer expected.
- WardenSpikedWallsReuseTheSpikeTeeth now also verifies that teeth/placeholder strips disappear at expiry.
- PrepareShowsTheBossNameAndItsHint's WARDEN case uses the new hint and verifies one line plus the traits line.

Existing player-only, floor-with/without-gravity, shared immunity, higher-overlap damage, push-in, obstacle disabling and spike_hit/no-wall-cue tests remain active and pass. Added three moved-wall placeholder cases and three warn timing cases.

No goldens were regenerated. All existing CrossPlatform, masked Rules, seven arena digests, Versus outcome and fixed-operation goldens pass unchanged in the final default run. No changed pinned Run golden required regeneration. Rules stays 9.

## 8. Versus byte-identical check

Two fresh all-arena round-robins, **50 seeds per ordered pair**, seedOffset 0, compared byte-by-byte with each other and E:/ball-battle-6e-out/det-a/. Each run covers 7 arenas * 64 pairs * 50 = **22,400 matches**.

| File | Bytes | New A = new B | New A = 6e |
|---|---:|---|---|
| rr.csv | 34195 | true | true |
| rr-weapons.csv | 499 | true | true |
| rr-arenas.csv | 391 | true | true |

SHA-256:
- rr.csv: C24F17A31D3C6B1D79043C7508BD81974B1C534222741E9C8AEB0C7F44D03543
- rr-weapons.csv: D340CA1AEC4138FC35ED9174E6164ED2CD7343186804DF830718FD71BB14D9FD
- rr-arenas.csv: B553318DD9F4BF2F27648DDE9C889CDE5D87F7FD7F00FE309396E2A21A610F45

No Versus value, gameplay rule, or golden changed. Evidence: E:/ball-battle-6f-out/versus-byte-check.csv, det-a/, det-b/.

## 9. Deviations and execution notes

1. **Greedy targets and all-green acceptance were not achieved.** Four tuning candidates were tried, then the best valid boss-band candidate was selected as requested. The mathematical conflict and the two remaining failed tests are reported above. No out-of-scope tuning or assertion relaxation was applied.
2. Added exact WARDEN-only event source metadata for the requested wall/source damage diagnostics. It leaves Versus event values and CSVs unchanged.
3. Added the every-arena boss gate and tightened the MAGNET bound to the requested 77%; these strengthen qualification rather than changing gameplay.
4. A concurrent trait test held the standard test DLL while the initial iteration-3 build tried to replace it. No process/file/lock was deleted. An isolated candidate output directory recovered the build; the diagnostic was selected separately because an OR filter with ordinary tests excluded the Explicit diagnostic. Failed build/selection artifacts remain preserved.
5. The first EditMode run reported 300 pass / 3 fail / 3 skip: the three newly added timing tests wrongly assumed boss clips existed. They now verify the established missing-clip contract and record request timing. Existing missing-clip assertions were not weakened. That run also hit a native Unity allocator/AssetDatabase shutdown crash after writing XML (exit -1073741819). The final rerun passed 303/0/3 and exited normally.
6. Unity MCP tools were unavailable in this session; Unity 6000.5.1f1 batch CLI ran EditMode. An unlocked Temp/UnityLockfile marker was reused; no lock removal was needed or attempted.
7. Selected iteration-2 measurement files are retained as the final tuning evidence; final gates and default suites used the restored 207 HP / 144-tick source. Earlier candidate folders and all existing out folders remain.
8. Warn cue assets are still absent, so actual audible boss warnings remain unavailable. Only timing and existing audio dispatch behaviour were verified; no audio asset work was added.
9. The pinning conclusion is limited to the stated diagnostic and these seed samples.

Evidence root: **E:/ball-battle-6f-out/**. Final suite files: final-simtests.trx/log, editmode-final.xml/log/done, gate-weapon.trx, gate-trait.trx, gate-boss.trx. Tuning evidence: it1/ through it4/, with it2/ selected. Backup remains in E:/ball-battle-6e-out/.

## 10. Confirmation and stop

- HEAD remains **4bb506122f7933013e6e534f0b37ac2efe9eec87** (4bb5061).
- Branch remains **dev/step3-view**; Git identity remains **phamphu422@gmail.com**.
- SimVersion.Rules remains **9**.
- Ball base stats and WRECKER starting speed remain locked; Vietnamese untouched.
- BB-6d + BB-6e uncommitted changes were preserved.
- Nothing committed, pushed, stashed, or deleted, including Temp/UnityLockfile, other lock files and existing out folders. git stash list is empty.
- git diff --check passes. Final git status is saved to E:/ball-battle-6f-out/final-git-status.txt; no deleted-file entry.
- No task-owned Unity/testhost process remains.

**Stopped after four tuning iterations and reporting. Unresolved acceptance: permission to change a fixed constraint would be needed in a follow-up to reconcile Greedy >=25% with the current other-boss outcomes and WARDEN <=70%.**
