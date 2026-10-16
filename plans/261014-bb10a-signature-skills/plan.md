# BB-10a signature skills

Baseline: HEAD 62a384b, parent 4bb5061, dev/step3-view, Assets clean, phamphu422.

- Apply the accepted temporary Greedy floor 22% in exactly two gates.
- Implement one per-ball ActiveTick skill state and the eight authored skills/branches, reflection safety and MIMIC copying.
- Replace generated Swap offers with SKILL UP; migrate save v3 to v4 while retaining history.
- Add roles/bars, single HUD slot/cooldown, Prepare slots, branch controls, Codex, FX, trace and missing-safe audio hooks.
- Add the transient QA skill override, including both Versus balls and Run player/MIMIC.
- Qualify once: 50 seeds/pair/all arenas twice, 500 runs/bot, default tests and explicit gates. No tuning sweep.
- Re-pin goldens once after the authored implementation; no gross outlier found, so no skill value correction.
- Report all counts, band misses and evidence in docs/claude-runs/bb10a.md; print full report.

No commit, push, stash or filesystem deletion. Preserve Temp locks and untracked BB-6 diagnostic files. All outputs E:/ball-battle-10a-out.

Completed 2026-10-14. Authored values unchanged; no gross outlier. Three round-robin CSV pairs are byte-identical.
Default functional coverage is qualified after targeted recovery of two legacy fixtures. Explicit balance gates retain
four band misses (weapon: one; boss: two; trait: one), reported without tuning. See docs/claude-runs/bb10a.md.
