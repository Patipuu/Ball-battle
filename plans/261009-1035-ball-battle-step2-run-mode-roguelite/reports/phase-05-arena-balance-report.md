# Phase 5 — arena balance report

Per-weapon average win rate vs the other seven (both sides, 100 seeds/side). Source: `SimTests/ArenaBalanceReport.cs` (gate: every cell 25–75%, every variant changes >= 2 weapon ranks vs Classic).

| weapon | classic | pillar | bumpers | spike-walls | low-gravity | tight | wide |
|---|---|---|---|---|---|---|---|
| brawler | 46.1 | 51.4 | 58.1 | 36.5 | 49.3 | 53.3 | 44.8 |
| volley | 52.7 | 47.9 | 39.3 | 50.3 | 45.9 | 37.4 | 54.6 |
| aegis | 55.0 | 55.2 | 47.9 | 49.7 | 54.6 | 26.2 | 60.1 |
| venom (spike-walls) | 44.9 | 45.3 | 59.0* | 72.0 | 50.3 | 54.7 | 43.8 |

*bumpers venom value measured at the earlier bumper offset 75; the gate passes at the final offset 95 (full table is printed by the test; BB_REPORT writes it).

Tuning notes: spike-wall damage 2 (Venom 72% there; 1 or 3 did not lower it, so the damage amount is not the driver — Venom's poison favours attrition). Bumpers moved from offset 75 to 95 so no ball (including the r=24 boss) spawns overlapping a bumper. Tight is the weakest arena for Aegis (26%).
