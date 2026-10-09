# Phase 7 — run difficulty report

Bots (`SimTests/RunBot.cs`), 2000 runs each, arenas now vary per fight (Phase 5), fixed seeds.

| change | Random win | Greedy win |
|---|---|---|
| before Phase 5 arenas (Phase 2 numbers) | 6.4% | 41.4% |
| with arenas, WinHealPct 0.30 | 5.3% | 33.0% |
| with arenas, WinHealPct 0.35 (final) | 7.1% | 35.8% |

Final: random bots reach boss stages 0/1/2/3 = 36.9/41.2/18.7/3.3%; greedy 14.8/31/32.8/21.5%. Targets: random 5-20%, greedy 30-60%, last giant boss beats the random bot >= 40% (it beats ~83% of random runs that reach it). Gate: `SimTests/RunDifficultyThresholdTests.cs` (800 runs per policy, Category Balance).

Only RunTuning.WinHealPct changed (0.30 -> 0.35); weapon/trait tuning untouched.

Open items (need a human or later work):
- Playtest by the user (>= 3 runs): rhythm, clarity, card meaning. Not done yet.
- Trait x weapon combos are not gated; worst known 3-trait combo 85.7% (Brawler + Vampire2 + Second Wind2 + Thorns2). Run bots do not reach it often enough to matter.
- Average HP before boss and "slow death" losses are not tracked by the bots.
- Giant boss / Heavy scale the ball sprite non-integer (pixel alignment); Versus menu icons are scaled 0.5.
