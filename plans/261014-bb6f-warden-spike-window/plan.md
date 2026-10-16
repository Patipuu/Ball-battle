# BB-6f WARDEN spike window

Date: 2026-10-14. Repo E:/ball-battle. HEAD 4bb5061, dev/step3-view, Rules 9.

1. Verify preconditions and back up the dirty 6e tree before editing (done: 3238222-byte patch, 43 untracked files).
2. Track the walls moved by WARDEN itself and an end-exclusive ActiveTick window; keep normal shrink separate.
3. Drive placeholder rendering from the same mask; shorten the English hint and keep traits.
4. Add rule, drawing, warning timing and per-arena gate coverage.
5. Measure at most four tuning candidates, 1000 runs/bot and 100 seeds/weapon/boss. Preserve Versus, base stats and other bosses.
6. Run default SimTests, Unity EditMode, explicit weapon/trait/boss gates; compare two 50-seed all-arena CSV runs with 6e.
7. Save evidence to E:/ball-battle-6f-out and the full final report to docs/claude-runs/bb6f.md; print the full report.

No commit, push, stash, or file deletion. Never remove a lock to unblock Unity. Preserve prior output folders.

Known target conflict: with unchanged MIMIC (110 wins) and 186 runs reaching WARDEN, WARDEN <=70% bounds total Greedy wins at 240/1000, below the requested 25%. Keep all gate assertions and report misses.
Final: four candidates measured, iteration 2 selected (HP 207, window 144). Versus byte-identical, EditMode 303/0/3, weapon 3/0, trait 2/0. SimTests 252/1 and boss gate 7/1 retain the Greedy failures. Report saved; stop without further tuning or Git mutations.
