# Tutorial takeover — 20 October 2026

## Scope

Continued the unfinished first-run tutorial polish without resetting Claude's working tree. The existing TUT-2 flow, tutorial Wrecker opponent, 2.5-second slowdown cap, and Wrecker two-tap prompt remain in place.

## Changes

- The end-of-run hint now chooses an action the player can take: open a pending chest, upgrade an affordable ball, or play again for more shards. The previous unconditional upgrade instruction could be impossible after the first run.
- Added English and Vietnamese lines for the two new outcomes.
- Strengthened the Wrecker lesson test to wait until the charge ring reaches 80%, verify the second-tap prompt, then release the ram.
- The screenshot test now captures the first-skill prompt when the skill is actually ready. Its old capture timing produced a fight image with no prompt.

## Verification

- `BbTut2Tests`: 13 passed, 0 failed, 1 skipped (capture disabled) on an isolated Unity project copy. `tut2-takeover-editmode.xml`.
- `CaptureFirstStepsInBothLanguages`: 1 passed, 0 failed. Twelve EN/VI screenshots in `tut2-takeover-shots/`. Inspected the pick, card, skill-ready fight, and result screens at native 270 × 480; the prompt fits without covering fight controls.
- `BbTap1Tests` and `BbViTests`: 31 passed, 0 failed, 1 skipped. `tut2-takeover-adjacent.xml`.
- `dotnet build BallBattleUnity/SimTests/SimTests.csproj -c Release --no-restore`: 0 warnings, 0 errors.
- `git diff --check` for the touched source/test/string files: no whitespace errors.

The original Unity project was locked by another process, so the EditMode runs used `Builds/tut2-unity-check` (ignored by Git). A direct `dotnet test` attempt aborted because VSTest could not connect to testhost after 90 seconds; no SimTests pass is claimed. No commit or push was made.

## Open validation

- Five first-time players still need to try the tutorial without coaching, as required by `tut2-plan.md`.
- Unity MCP was unavailable in this session. The user withdrew the request to use it; the isolated Unity batch runs above are the validation source.
