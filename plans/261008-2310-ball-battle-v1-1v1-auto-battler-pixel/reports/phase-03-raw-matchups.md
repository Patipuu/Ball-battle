# Phase 3 — raw matchup matrix (untuned, 2026-10-09)

Tool: `SimTests/MatchupReport.cs` (`dotnet test --filter "FullyQualifiedName~MatchupReport" --logger "console;verbosity=detailed"`).
200 seeds × both side assignments = 400 matches per pair. Default MatchConfig, `WeaponTuning` as first written. Runtime 154 s (~38 ms/match).

| A vs B | A win % | B win % | Draw % | Median s | P90 s | Cap % |
|---|---|---|---|---|---|---|
| blade vs blade | 49.5 | 50.0 | 0.5 | 58.2 | 80.3 | 0.0 |
| blade vs fang | 100.0 | 0.0 | 0.0 | 63.4 | 83.6 | 0.0 |
| blade vs pike | 73.0 | 27.0 | 0.0 | 64.5 | 83.8 | 0.0 |
| blade vs brawler | 1.0 | 98.8 | 0.3 | 21.8 | 26.7 | 0.0 |
| fang vs fang | 46.5 | 53.5 | 0.0 | 152.0 | 162.7 | 1.0 |
| fang vs pike | 0.0 | 100.0 | 0.0 | 76.5 | 97.3 | 0.0 |
| fang vs brawler | 0.0 | 100.0 | 0.0 | 21.2 | 26.7 | 0.0 |
| pike vs pike | 54.0 | 46.0 | 0.0 | 76.3 | 96.4 | 0.0 |
| pike vs brawler | 0.3 | 99.8 | 0.0 | 22.0 | 27.2 | 0.0 |
| brawler vs brawler | 33.5 | 33.0 | 33.5 | 16.1 | 20.4 | 0.0 |

## Findings (input for Phase 7)
1. **Brawler far too strong** (~99% vs everything, ~21 s). Cause: damage = 0.6 × own speed ≈ 3–6 from the first hit while blades start at 1; it can't be parried. Levers: lower damage factor, start damage near 1 (scale with speed above a threshold), lower start speed cap.
2. **Fang far too weak** (0% vs Blade/Pike/Brawler; Fang mirror 152 s). Cause: damage fixed 1 → ~100 hits to kill; cooldown 6 ticks + hitstop limits it. Levers: shorter cooldown, damage +small per N hits, parry gives a stat (it parries a lot by design).
3. **Brawler mirror 33.5% draws.** Two body attackers always trade symmetrically (both hit on the same contact) → frequent double KO. Rule option: in body-vs-body contact only the faster ball deals damage, or damage = speed difference.
4. Blade vs Pike 73/27 — closest to target band, small tuning.
5. Balance runs in Phase 7 (12,000 matches) ≈ 7–8 min single-threaded → parallelize seeds.
