# Phase 2 — pacing sweep (2026-10-08)

Goal (plan acceptance #3): median match 20–90 s, <1% time cap. Probe weapon: blade +1 dmg per hit (Blade-like), 300 seeds per row, 2 identical balls.
Tool: `SimTests/DiagnosticTests.cs` (`dotnet test --filter Category=Diagnostic --logger "console;verbosity=detailed"`).

| Config | Median | P90 | Cap | Hits/min | Parries/min |
|---|---|---|---|---|---|
| Plan original: 250x380, r12, speed 3–5 / max 8 | 118.7 s | 132.3 s | 0 | 12.5 | 6.5 |
| A: r16, 250x320 | 102.5 | 118.8 | 0 | 14.8 | 6.2 |
| B: r16, 250x250 | 84.8 | 111.7 | 0 | 17.1 | 7.3 |
| C: B + gravity 0.08 | 78.3 | 107.5 | 0 | 18.4 | 7.9 |
| D: r16, 230x230, min 110x110 | 74.0 | 100.2 | 0 | 19.3 | 8.4 |
| F: D + speed 5–7 / max 10 | 66.0 | 88.9 | 0 | 21.9 | 9.1 |
| **G: F + min floor bounce 5, min horizontal 1.5** | **54.5** | **72.7** | 1 | 26.2 | 11.6 |
| H: G + blade 30 | 47.4 | 65.1 | 0 | 29.9 | 15.4 |
| I: G + hit cooldown 10 | 51.8 | 70.6 | 1 | 27.4 | 11.7 |
| J: G + shrink at 45 s | 52.2 | 62.1 | 1 | 27.8 | 12.4 |

Decision: **G** becomes MatchConfig default. Final check with defaults: median 53.8 s, p90 72.4 s, cap 0/300.
Blade length (H) is a per-weapon knob → Phase 3/7. Shrink start kept at 90 s (validated plan); J shows it barely matters for the median.

Why the original was slow: arena much larger than the balls (r12 in 250x380), slow floaty movement, vertical-only bounces → balls rarely met.

Layout consequence: square 230x230 arena centred in the 270x480 screen, ~125 px free above and below for names, HP, stats (good fit for Shorts framing). Ball sprite 32x32 instead of 24x24.
