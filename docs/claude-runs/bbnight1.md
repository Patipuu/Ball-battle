# BB-NIGHT1 — overnight batch

Started 2026-10-15 (Asia/Bangkok). HEAD b60df20d230a614ba1aacc1f213aed383bd7dbf3; dev/step3-view; phamphu422@gmail.com. Assets were clean. Rules 12.

T0: c87e9675c4dbeaeaabd8aca1dcefe71c431c3f06.

Pre-existing untracked files (45) and five modified non-Assets files are inventoried in night1/preexisting-untracked.txt and night1/preexisting-modified.txt. They are preserved and excluded from phase implementation trees. Bookkeeping (this report, night1 evidence/index/diffs, plan) is also excluded to avoid recursive index/diff inclusion and cross-phase artifacts; implementation trees otherwise use the requested alternate-index git add -A/write-tree procedure. HEAD and the real index's staged blob/mode contents remain unchanged. The real index's metadata bytes changed; see deviations below.

Unity MCP endpoint localhost:8082 is unavailable. Unity batchmode is available, no Ball Battle editor process or Temp/UnityLockfile existed at entry. No lock removal is authorized or needed.

## Phase A — done

Replacing two obsolete silent-boss audio assumptions with non-null checks for all 15 real boss/skill clips and missing-id lists. RunFlow resume test now follows the actual win/loss screen; a separate case searches setup seeds 1..200 when a win is required.

Files: Assets/Tests/EditMode/Bb03AudioTests.cs, Bb04BossTests.cs, RunFlowTests.cs. No production changes.

SimTests: 357 discovered, 320 pass / 0 fail, 37 Explicit excluded (126.87s wall). EditMode: 344 total, 340 pass / 0 fail / 4 capture-only skips (96.76s wall). Goldens byte-identical to T0.

TA: d6c445031f562b90c3e06b0d04421b8e7681ba3f. Diff: night1/phaseA.diff. Forbidden build/cache paths in tree: none. Logs: night1/phaseA-sim.log/.trx/.done and phaseA-edit.log/.xml/.done.

## Phase B — done

Blank buttons were caused by PixelButton.Create receiving an empty string: it created no Label, so later SetText did nothing. TAKEN identity was re-read through RunState.OfferCard's transient force-skill projection: after a forced level2 pick became level3 it projected the same stored SkillUp slot to MaxHp. The View now reads stored identity for picked slots. No Sim changes.

- Branch buttons now create a label and show full branch name plus the existing one-line effect. Height 28 pixels fits two 13-pixel font cells. All eight weapons pass non-empty/bounds/overlap checks at upgrades to level2 and level3.
- PrepareView.ShownCard reads raw stored identity for TAKEN slots and retains the normal projection for unpicked offers. Tests cover 0/1/2 real rerolls, both with and without a forced skill.
- Start/retry copy is FREE PICK; flow is unchanged, and one retry pick starts Countdown.
- AudioDirector logs exactly one [Skill] line per SkillFired, preserving the existing RequestCue call, volume0.8 and pitch1. MIMIC boss-side fires are included. result=missing also describes a same-tick gated cue: the requested vocabulary has no gated option. No fallback sound.
- MIRROR reflect logs read existing Damage(Reflect) and ProjectileDeflected events, in audio log and trace. projectile=none for body/parry reflection, projectile=projectile for a deflected shot. The existing events contain no arrow/turret subtype, so no subtype is invented and no Sim event was added.

Files: View/SkillBranchButtons.cs, Run/PrepareView.cs, Run/StartPickView.cs, Audio/AudioLog.cs, Audio/AudioDirector.cs, ArenaView.cs; Tests/EditMode/Night1ViewTests.cs and metadata. No Sim file edits.

SimTests: 320 pass / 0 fail, 37 Explicit excluded (~115.31s wall). EditMode: 363 total, 359 pass / 0 fail / 4 existing capture skips (81.22s wall). New phase-B cases: 19 pass, including its capture test. Goldens unchanged from A.

TB: ca71f1139ceb770951442dc7fa7a48675a53285d. Diff: night1/phaseB.diff. Forbidden build/cache paths: none.

Screenshots (visually checked): phaseB-skill-up-level2.png, phaseB-skill-up-level3.png, phaseB-taken-after-two-rerolls.png, phaseB-retry-free-pick.png. Logs: phaseB-sim.log/.trx/.done, phaseB-edit.log/.xml/.done, phaseB-skills.txt, phaseB-skills-mimic.txt, phaseB-reflect.txt. All copied into night1/.

Deviation: total Skill lines equal total Sim fires; played Skill lines equal skill cue count. Total lines cannot equal played cues when MIMIC/mirror fires share a tick, because the existing same-id cue gate suppresses the second cue. Changing that gate would violate the explicit no-audio-behavior-change instruction.

## Phase C — done; cause c (no bug)

Measure 200 fixed-seed SPORE fights against all eight weapons: 100 Versus and 100 Run fights, pure Sim versus the actual RunFlow/ArenaView event path. Track >=3-stack ticks, cooldown-ready eligibility, Sim fire counts and Phase B [Skill] lines. No poison values or thresholds will be tuned.

Versus input seeds 220001..220100, classic; Run input seeds 330001..330100, generated arenas and enemy traits, one normal first fight each. Opponents cycle through all eight IDs. Both contexts have 52 level1 and 48 level3B samples. Versus overrides both balls; Run overrides only the player. Each Run fixture snapshots all unlocks, selects SPORE and limits its enemy weapon pool to the assigned opponent; the match itself uses unmodified RunFlow/RunState rules. Pure Sim and ArenaView final hashes matched in all 200 pairs.

The following counters refer to the player SPORE, ball0. Enemy DRAIN fires in mirrors are not included in the player counters. >=3-stack ticks are sampled before each Sim step; cooldown-ready eligibility excludes hit-stop.

| Context | Opponent | Matches | Matches with >=3 stacks | >=3-stack ActiveTicks | Sim fires | View fires / Skill lines | Zero-fire matches |
|---|---|---:|---:|---:|---:|---:|---:|
| Versus | blade | 13 | 9 | 682 | 11 | 11 / 11 | 6 |
| Versus | fang | 13 | 9 | 2,124 | 15 | 15 / 15 | 4 |
| Versus | pike | 13 | 9 | 1,735 | 12 | 12 / 12 | 6 |
| Versus | brawler | 13 | 13 | 4,418 | 21 | 21 / 21 | 0 |
| Versus | volley | 12 | 9 | 1,487 | 14 | 14 / 14 | 3 |
| Versus | venom | 12 | 5 | 886 | 6 | 6 / 6 | 8 |
| Versus | aegis | 12 | 8 | 1,692 | 10 | 10 / 10 | 5 |
| Versus | rig | 12 | 9 | 1,779 | 13 | 13 / 13 | 4 |
| Run | blade | 13 | 10 | 2,942 | 16 | 16 / 16 | 3 |
| Run | fang | 13 | 10 | 2,093 | 14 | 14 / 14 | 4 |
| Run | pike | 13 | 9 | 1,873 | 12 | 12 / 12 | 4 |
| Run | brawler | 13 | 12 | 4,003 | 19 | 19 / 19 | 1 |
| Run | volley | 12 | 10 | 2,661 | 13 | 13 / 13 | 3 |
| Run | venom | 12 | 6 | 897 | 6 | 6 / 6 | 7 |
| Run | aegis | 12 | 12 | 4,478 | 22 | 22 / 22 | 0 |
| Run | rig | 12 | 8 | 1,618 | 11 | 11 / 11 | 4 |

Totals: 200 matches; 148 had >=3 stacks at least once; 35,368 / 679,638 ActiveTicks = **5.20395%** met the stack condition. **215 Sim fires = 215 View fires = 215 Skill lines**, average **1.075 per fight**; all 215 player cues played. **Zero eligible ticks missed** (eligible = active, stacks>=3, cooldown ready). 62 matches (31%) had no fire: 52 never reached three stacks, and 10 reached them only outside ready windows.

| Context / skill | Matches | Zero fires | Total fires | Average fires |
|---|---:|---:|---:|---:|
| Versus level1 | 52 | 20 | 51 | 0.980769 |
| Versus level3B | 48 | 16 | 51 | 1.062500 |
| Run level1 | 52 | 15 | 55 | 1.057692 |
| Run level3B | 48 | 11 | 58 | 1.208333 |

SPORE mirror level3B specifically: seeds 220014, 220030, 220046, 220062, 220078, 220094 produced **0,0,0,0,1,0** player fires. Seed220062 had 179 three-stack ActiveTicks but none while cooldown-ready; the other four zero-fire mirrors never reached three stacks. This explains a zero-fire playtest without an implementation failure; exact playtest seeds were not supplied. The older 10a average is from Rules10 and a different sample, so it is not a per-match guarantee.

Changes: diagnostic-only Night1DrainTests.cs and metadata; no production code changed. No Sim fix, threshold/value change, Rules bump or golden regeneration. Rules remains12; goldens unchanged from B.

SimTests: 320 pass / 0 fail, 37 Explicit excluded (116.20s wall). EditMode: 364 total, 359 pass / 0 fail / 5 skips (168.24s wall); phase-B capture is now skipped, phase-C diagnosis passed.

TC: a8fb9dff046919ff82204e9d1b947df5663f0372. Diff: night1/phaseC.diff. Forbidden build/cache paths: none. Evidence: phaseC-drain-matches.csv (one row per match), phaseC-drain-summary.csv, phaseC-drain-distribution.csv, phaseC-drain-skill-lines.txt, phaseC-sim.log/.trx/.done and phaseC-edit.log/.xml/.done, copied to night1/.

## Phase D — done

Held source inspection found all eight requested role icons and the v2 boss overlays/bodies. Copy only the specified subset; preserve source and old repository assets/metas. Existing 64px SHELL KING/MIMIC base bodies remain because the held v2 folder supplies their overlays, not replacement base bodies; 48px held bodies are excluded.

Copied **26 PNGs**, all byte-identical to their held source after import, with 26 new unique sprite metas. No target PNG already existed, so no old PNG or meta GUID needed replacement. Held source hashes are unchanged. Copy manifest: night1/phaseD-art-copy-manifest.csv.

- boss/v2: magnet_body, warden_body, shellking_shell_on/warn, mimic_flash_1/2/3, magnet_ring_warn/pull.
- boss: warden_spike_left/right/top_on/fade, warden_wallflash, bumper_rings.
- role_icons: only the eight role-name icons below.
- arena_thumbs: thumb_spike-walls_v3.png only.

Every copied sprite imports with Point, Compression None on all eight serialized platforms, no mipmaps, PPU1 (matching existing boss/ball art), FullRect geometry. All new boss/role/ring/thumbnail pivots are centered; spike pivots sit on their wall side. Imported sizes are tested: boss64×64, role12×12, rings112×112, strips6×32/32×6, wallflash64×8, bumper14×14, thumbnail48×48.

BossArtEnabled is true. Art2Linker and the generator link new boss art from Assets/Art/Sprites/boss/v2. Prepare and Arena use dedicated bodies; MAGNET/WARDEN use the new bodies, SHELL KING/MIMIC retain the existing 64px base bodies. Shell/ring overlays read existing boss state; MIMIC flash frames react to existing hits. Existing telegraph dots remain. All new art draws at native1x; boss bodies round visual scale to an integer if traits change the physical radius fractionally, with no Sim radius change.

WARDEN strips follow the active arena and moved-wall mask only. The **inner tip edge equals the rounded collision-wall line**, with wall-side pivots anchored6px outside it. Final tiles are cropped to the remaining wall length instead of scaled; no overrun past corners. Fade artwork shows during the last8 ActiveTicks of the unchanged spike window, and wallflash during its first6. No floor strips, and all strips hide when the window ends. Standalone ArenaFrame retains its placeholder fallback; actual ArenaView enables ART-2 strips.

Bumper rings overlay the existing obstacle body, preserving the visible collision outline. Role icons appear beside role text in PickWeapon, Versus profiles and Codex. The v3 thumbnail is visible on the existing arena selector; START was narrowed to140px to leave the 48px thumbnail at1x. The repository has a cycle selector rather than a separate arena-list page; its selection screen is captured.

| Role | Internal weapon | Copied file |
|---|---|---|
| BRUISER | blade | role_bruiser.png |
| DUELIST | fang | role_duelist.png |
| LANCER | pike | role_lancer.png |
| RAMMER | brawler | role_rammer.png |
| RANGER | volley | role_ranger.png |
| POISONER | venom | role_poisoner.png |
| GUARDIAN | aegis | role_guardian.png |
| BUILDER | rig | role_builder.png |

No requested role icon was missing. Exact excluded-source list: night1/phaseD-left-out.txt (27 files):

- boss/: magnet_body.png, warden_body.png, shellking_body.png, mimic_body.png; magnet_ring_pull/warn.png, mimic_flash_1/2/3.png, shellking_shell_on/warn.png (root versions ignored); preview_boss_4x.png and preview_spikes_bumper_4x.png.
- boss/v2/: preview_boss_v2_4x.png.
- role_icons/: preview_roles_4x.png; role_aegis, role_blade, role_brawler, role_fang, role_fang_v2, role_pike, role_rig, role_rig_v2, role_venom, role_volley (.png).
- arena_thumbs/: thumb_spike-walls.png and thumb_spike-walls_v2.png.

Files changed: **75 paths**, listed exactly in night1/phaseD-files.txt. These comprise the26 PNGs/metas and three folder metas, ArtLibrary.asset, ArtLibrary.cs, new Editor/Art2Linker.cs and View/BossArtView.cs with metas, PlaceholderArtGenerator.cs, ArenaFrameView.cs, ArenaView.cs, BallView.cs, MenuView.cs, Run/PrepareView.cs, Run/RunController.cs, WeaponProfileView.cs, new Night1ArtTests.cs/meta, and existing art tests ArtV1CardsBossArenaTests.cs, Bb6dTests.cs, PrepareAndFightUiTests.cs, Prompt3dTests.cs. No Sim or ProjectSettings content diff.

SimTests: 320 pass / 0 fail, 37 Explicit excluded (119.02s wall). Initial EditMode: 398 total, 387 pass / 5 fail / 6 skips; failures were obsolete art expectations and standalone-frame fallback. Corrected those expectations and the actual fallback behavior. Final full EditMode: **398 total, 392 pass / 0 fail / 6 skips**, exit0 (84.96s wall), including all34 new ART-2 cases. The initial Unity process exited0xC0000005 after writing its test results; the final process exited normally. Initial logs/XML are retained separately.

TD: **a813364caf2198b97ac4010d69a9ddea80dabfcf**. Diff: night1/phaseD.diff. Forbidden build/cache paths: none. Goldens unchanged from C.

## Screenshots and logs

All outputs are in E:/ball-battle-night1-out and copied to E:/ball-battle/docs/claude-runs/night1. **20 screenshots** are retained and visually checked:

| Phase | Files |
|---|---|
| B | phaseB-skill-up-level2.png; phaseB-skill-up-level3.png; phaseB-taken-after-two-rerolls.png; phaseB-retry-free-pick.png |
| D SHELL KING | phaseD-shellking-fight-1x.png / phaseD-shellking-fight-3x.png |
| D MAGNET | phaseD-magnet-fight-1x.png / phaseD-magnet-fight-3x.png |
| D MIMIC | phaseD-mimic-fight-1x.png / phaseD-mimic-fight-3x.png |
| D WARDEN | phaseD-warden-fight-1x.png / phaseD-warden-fight-3x.png |
| D spike window | phaseD-warden-spike-window-1x.png / phaseD-warden-spike-window-3x.png |
| D bumpers | phaseD-bumpers-1x.png / phaseD-bumpers-3x.png |
| D role pick | phaseD-pick-role-icons-1x.png / phaseD-pick-role-icons-3x.png |
| D arena selector | phaseD-arena-spike-walls-1x.png / phaseD-arena-spike-walls-3x.png |

1x images are270×480; 3x images are810×1440, rendered at the same camera framing. Initial D images were replaced by corrected final D captures, as authorized for this phase.

Logs/results: phaseA/B/C/D-sim.log/.trx/.done; phaseA/B/C/D-edit.log/.xml/.done; phaseD-final-edit.log/.xml/.done; phaseB-skills.txt, phaseB-skills-mimic.txt, phaseB-reflect.txt; phaseC-drain-matches.csv, phaseC-drain-summary.csv, phaseC-drain-distribution.csv, phaseC-drain-skill-lines.txt. Tree JSONs, phase diffs, index warning logs, import manifest, excluded-source list, final-suite-counts.csv, final-git-status.txt, final-integrity.json and goldens-final.csv are also retained. Full output inventory: night1/output-inventory.txt.

## Final test summary

| Phase | Sim pass / fail / Explicit excluded | EditMode pass / fail / skip |
|---|---|---|
| A | 320 / 0 / 37 | 340 / 0 / 4 |
| B | 320 / 0 / 37 | 359 / 0 / 4 |
| C | 320 / 0 / 37 | 359 / 0 / 5 |
| D final | 320 / 0 / 37 | 392 / 0 / 6 |

The four existing capture-only skips require BB01_CAPTURE, BBFONT_CAPTURE, BB_CAPTURE and BB_VIEW3_OUT. In C, the already-completed B capture is skipped. In D, B capture and C diagnosis are skipped; their completed evidence is retained. Explicit balance/slow reports remain excluded by the default SimTests run; no balance gates or tuning sweeps were requested for NIGHT1.

Rules remains **12**. No golden was regenerated. CrossPlatformGoldenTests.cs, VersusGoldenTests.cs and SpikeCornerTests.cs remain byte-identical to T0 through all four phases; exact original/final SHA256s are in night1/goldens-final.csv. Every default golden assertion passes after each phase.

## Deviations and final integrity

1. Unity MCP was unavailable; tests/captures used Unity batchmode. No standalone player executable was built or launched. Phase C uses the same RunFlow/ArenaView/AudioDirector code paths as the player, verified against pure Sim, rather than claiming a packaged-player test.
2. Phase trees exclude pre-existing dirt/untracked files and bookkeeping. This is necessary to make each diff contain only that phase and avoid staging the alternate index or recursively generated report/diffs. GIT_INDEX_FILE was set only for the authorized snapshot operations; its environment variable was then removed. No Library/Temp/Builds/Logs/obj paths appeared in any tree.
3. MIMIC/mirror fires sharing a cue and tick cannot all play through the unchanged audio gate. Logs remain one per fire; played lines equal played cue requests. A non-played fire is labelled missing, per the required two-value vocabulary.
4. Existing reflection events do not contain arrow-versus-turret subtype. The log records projectile or none; no event or gameplay rule was added.
5. Held v2 lacks new SHELL KING/MIMIC base bodies, so existing64px bases remain. New26 sprites meet the specified import/pivot settings. Old files/metas are preserved. Existing fractional physical boss sizes use rounded integer visual art scales, leaving physics unchanged.
6. The arena UI is a cycle selector, so the requested thumbnail screenshot shows that screen rather than introducing a new arena-list flow.
7. Five D test failures were corrected through art expectation updates and the standalone-frame fallback fix; no assertion bound or Sim value changed. The first D process's native shutdown failure is retained in its logs; final full EditMode exited0.
8. **Real index metadata is not byte-identical to entry.** Its SHA256 changed from EF0D4ADE5D566C1317E808EDAEB3535CD3B1BB907833AA73B080F53FAFA7B363 to 4C11A42028C12FDD0D78FB41BA6B908BD91BF2D3AC808F24384DDBF8246D1992. All **2,555 staged paths, blob IDs, modes and stages exactly equal HEAD**, with zero staging-content differences. No staging command used the real index. Metadata/cache refresh during ordinary Git reads is the likely cause; original index bytes were not backed up, so byte restoration is not claimed. No reset or index rewrite was attempted to conceal this deviation.

All45 pre-existing untracked files remain present; none is in a phase diff. All26 held source hashes are unchanged and copied PNGs match them. No Sim code content changed; no localization/Vietnamese work or tuning occurred. Git reports no deleted tracked files; diff --check passes.

Nothing was committed, pushed, stashed or reset. No file deletion command was issued and no lock was manually removed. **HEAD is still b60df20d230a614ba1aacc1f213aed383bd7dbf3**, branch dev/step3-view, identity phamphu422@gmail.com. The Ball Battle batch editor has stopped. All four phases are done; no input is pending.

## Pre-existing untracked inventory (excluded from all phases)

Paths below are relative to E:/ball-battle; exact entry inventory also exists in night1/preexisting-untracked.txt:

```text
BallBattleUnity/SimTests/Bb10aDiagnostics.cs
BallBattleUnity/SimTests/Bb6cDiagnostics.cs
BallBattleUnity/SimTests/Bb6fDiagnostics.cs
BallBattleUnity/SimTests/BbFix2Diagnostics.cs
docs/claude-runs/bb01-02-editmode.xml
docs/claude-runs/bb04-editmode.xml
docs/claude-runs/bb04-simtests.trx
docs/claude-runs/bb6c-editmode.xml
docs/claude-runs/bb6c-simtests.trx
docs/claude-runs/bb6d-editmode.xml
docs/claude-runs/bb6d-simtests.trx
docs/claude-runs/bb6e-editmode.xml
docs/claude-runs/bb6e-simtests.trx
docs/claude-runs/bbbal1-simtests.trx
docs/claude-runs/bbfix1b-editmode.xml
docs/claude-runs/bbfix1b-simtests.err.txt
docs/claude-runs/bbfix1b-simtests.out.txt
docs/claude-runs/bbfix1b-simtests.trx
docs/claude-runs/bbfix2-editmode.xml
docs/claude-runs/bbfix2-simtests.trx
docs/claude-runs/bbfont1-editmode.xml
docs/claude-runs/bbfont1-simtests-final.err.txt
docs/claude-runs/bbfont1-simtests-final.out.txt
docs/claude-runs/bbfont1-simtests-final.trx
docs/claude-runs/bbfont1-simtests.err.txt
docs/claude-runs/bbfont1-simtests.out.txt
docs/claude-runs/bbfont1-simtests.trx
docs/claude-runs/bbsfx2-editmode.xml
docs/claude-runs/bbsfx2-simtests.out.txt
docs/claude-runs/bbsfx2-simtests.trx
docs/claude-runs/bbview3-editmode.xml
docs/claude-runs/bbview3-simtests.trx
docs/claude-runs/fx-err.txt
docs/claude-runs/fx-test.txt
docs/claude-runs/src6a6.tgz
docs/design/art/hud_retry_v2.png
docs/design/art/start_pick_v1.png
docs/design/boss-and-loss-rules.bak-20261011.md
docs/design/card-pool-v2.bak-20261011.md
docs/design/game-design-document.md
docs/design/weapon-bible.bak-20261011.md
plans/261013-core-loop-audio-team/plan.md
plans/261013-core-loop-audio-team/team-briefs.md
plans/261014-bb10a-signature-skills/plan.md
plans/261014-bb6f-warden-spike-window/plan.md
```

Pre-existing modified paths: .claude/hooks/.logs/hook-log.jsonl and the four trace-unity-2686071142 / trace-unity-4134000519 .txt/.fields.txt files. Test/hook activity may append/regenerate those existing logs; they are excluded from all implementation trees.

Unresolved questions: none. Remaining limitation: index metadata bytes changed despite unchanged staging contents, as reported above.

No commit, push, stash, reset or file deletion commands have been issued. No tuning or localization changes.
