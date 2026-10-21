# TUT-2 implementation handoff

17 October 2026. The first-time player tutorial from `tut2-plan.md` is implemented in the Unity project.

## Flow

- Save version 6 stores menu stage, strike lesson state, seen hint IDs and the Settings hint toggle. Version 5 progress and active runs migrate without being dropped. A fresh profile begins at stage 0; starting a run advances to stage 1; finishing it advances to stage 2.
- Stage 0 menu shows Run and How to Play. Run pulses and a pixel arrow appears after three seconds. Stage 1 adds Balls and the next goal. Stage 2 adds Versus and Codex, each tagged NEW until opened.
- The first run pins Ember, Mantis and Wrecker to the choice screen, then uses a fixed seed, a low-HP Angler without traits and Classic arena for fight 1. The flag survives save/resume. Later runs retain normal generation.
- `HintPointer` replaces the active blocking `TutorialCoach` overlay. It draws one short line and a pixel arrow without adding a button or disabling screen input. Hints cover ball choice, first card, carry-over HP, coins, first boss and the next step after a run. Balls explains shards on its first visit. The old class remains in the repository for historical reference, but is not instantiated.
- Fight 1 slows when the first strike is ready. Its strike prompt is shown once; a strike restores normal speed and records the lesson. The result shows the 8-fight goal, one retry and an animated 1/8 progress bar. Settings can turn tutorial hints off.

## Verification

- Final Unity EditMode suite: 564 total, 539 passed, 0 failed, 25 skipped (including the retired `BbTut1Tests` fixture and optional screenshot captures). `tut2-editmode-final.xml`.
- TUT-2 targeted EditMode suite with screenshot capture: 6 passed, 0 failed. `tut2-targeted-final2.xml`. The final targeted run after the Settings toggle test: 6 passed, 0 failed, 1 capture test skipped without `BB_TUT2_OUT`; `tut2-targeted-final3.xml`.
- English and Vietnamese screenshots of the first six screens are in `tut2-shots/`; checked at native 270 × 480 size for overlap and legibility.
- SimTests project builds with 0 warnings and 0 errors. Full SimTests suite: 355 passed, 0 failed; `BallBattleUnity/SimTests/TestResults/tut2-simtests-final.trx`. The initial sandboxed invocation could not connect vstest to testhost; elevated execution completed the suite.

## Remaining validation

- The TUT-2 plan asks for the first tap to be guaranteed PERFECT, but the current strike is still determined by actual combat contact. The tutorial prompt and slow motion are view-only and cannot promise a hit when the enemy is out of range. A deterministic assisted first strike would require a gameplay rule change and Sim golden updates.
- The plan's five-player usability check requires human playtesting.

No commit or push was made.
