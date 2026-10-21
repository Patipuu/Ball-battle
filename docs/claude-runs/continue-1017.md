# Codex continuation, 17/10/2026

Continued from `HANDOFF-2026-10-16-CLAUDE-TO-CODEX.md` on `dev/step3-view`. No commit or push was made. All pre-existing worktree changes were preserved.

## Completed

- Verified UI-FIX2 in Unity and in Vietnamese captures: the `KHO BÓNG` alert pill and tile upgrade labels have their own space.
- Verified the BB-18 orbs and brightened the unlit colours for legibility. The five orbs remain at their Sim positions; no Rules tuning was changed.
- Localized the pick panel's roles and level-two branches for all ten balls. Condensed three skill/guide lines that exceeded the panel width. Kept `HUNTER` as the temporary proper name while translating its role as `THỢ SĂN`.
- Kept boss hints in the column left of the arena preview and shortened three Vietnamese hints to preserve their meaning in the available space.
- Replaced the opaque `TTTTTTTT` result row with the longest winning streak (also labelled in English).

## Verification

- Final Unity EditMode: `continue-1017-hunter-fix-editmode.xml` — **557 total, 541 passed, 16 skipped, 0 failed**. The new pick-panel test checks the Vietnamese role, both branches, and pixel width for each of ten balls.
- Captures: `continue-1017-hunter-fix/balls/`, `continue-1017-hunter-fix/tribute/`, `continue-1017-hunter-fix/font/`. Inspected the VI menu, BALLS grid, pick panel, boss preparation and run summary, plus the 3× BGLGXLQ fight shot.
- `git diff --check` on the touched files: clean (Git noted only LF/CRLF normalization warnings).

## Awaiting direction

- BB-18 Versus balance: Run is in range, while head-to-head rates span 11.3% against WRECKER to 95.8% against MIRROR. Phu was asked to choose a Versus-only lever, retain the matchup spread, or let orbs absorb projectiles. No third tuning pass was attempted.
- UI-2 minimal-roguelite mock remains awaiting approval. TUT-2 remains a plan, as requested; no tutorial implementation was started.
- WebGL portrait template and persistent IDB save flush remain open from `audit-web-1016.md`.
