# Balance — first pass (2026-10-09)

Pulled forward from Phase 7 at user request after the raw Phase 3 matrix (`phase-03-raw-matchups.md`).
Method: Blade = reference; grid-search each other weapon vs Blade (`SimTests/BalanceSweep.cs`), then full matrix (`MatchupReport`), then confirm at 500 seeds/side. `SimVersion.Rules` 1 → 2.

## Rule changes (MatchSim / WeaponRule)
- **Body vs body:** when both balls are bodies (no blades) and touch, only the faster one lands the blow (equal speed → both). Brawler mirror draws 33.5% → 0%.
- **`KnockbackScale` per weapon** (Fang 0.3: target stays close, combos).
- **Brawler damage factor grows per hit** (`DamageFactor`, hashed) instead of a flat 0.6 × speed.
- **Fang damage creeps up per hit** (`CurrentDamage`, hashed).
- `WeaponTuning` constants → static fields so sweeps run without rebuilds (only changed between matches).

## Tuning changes
| Value | Before | After | Why (sweep) |
|---|---|---|---|
| BrawlerStartDamagePerSpeed | 0.6 | 0.1 | 0.6 → 99% wins; 0.1 + 0.01/hit → 48.5% vs Blade |
| BrawlerDamagePerSpeedPerHit | — | 0.01 | growth instead of front-loaded damage |
| PikeGrowthPerHit | 0.5 | 0.75 | 0.5 → 27%; 0.75 → 53% vs Blade |
| FangLength | 12 | 16 | at 12 Blade always outreaches it (≤21% even at +0.5 dmg/hit) |
| FangDamagePerHit | — | 0.4 | 16/0.35 → 43%, 16/0.5 → 59% |
| FangHitCooldownTicks | 6 | 3 | combo identity (cooldown alone had no effect at length 12) |
| FangKnockbackScale | — | 0.3 | keeps target in reach |

## Result — 500 seeds/side (10,000 matches, 34 s on 16 threads)
| A vs B | A win % | B win % | Draw % | Median s | P90 s | Cap % |
|---|---|---|---|---|---|---|
| blade vs blade | 49.8 | 49.8 | 0.4 | 58.5 | 79.0 | 0.0 |
| blade vs fang | 51.5 | 48.5 | 0.0 | 55.4 | 73.0 | 0.0 |
| blade vs pike | 46.9 | 53.0 | 0.1 | 58.2 | 74.5 | 0.0 |
| blade vs brawler | 56.0 | 43.8 | 0.2 | 38.8 | 45.7 | 0.0 |
| fang vs fang | 50.0 | 50.0 | 0.0 | 57.0 | 75.5 | 0.0 |
| fang vs pike | 52.2 | 47.8 | 0.0 | 54.3 | 71.9 | 0.0 |
| fang vs brawler | 33.2 | 66.8 | 0.0 | 39.9 | 46.4 | 0.0 |
| pike vs pike | 50.0 | 50.0 | 0.0 | 61.0 | 77.9 | 0.0 |
| pike vs brawler | 56.9 | 43.0 | 0.1 | 39.3 | 46.0 | 0.0 |
| brawler vs brawler | 50.0 | 50.0 | 0.0 | 44.5 | 52.5 | 0.0 |

All plan thresholds met: non-mirror 30–70% (tightest: Fang vs Brawler 33/67 — intended counter, body can't be parried), median 20–90 s (39–61 s), draws < 5%, cap 0%.
Gate: `SimTests/BalanceThresholdTests.cs` (200 seeds/side, part of the normal `dotnet test`).

## Open
- Brawler matches are shorter (~39–45 s) than blade matches (~55–61 s). Fine for now; revisit after playtest.
- Fang vs Brawler has only ~3 points of margin to the 30% floor.

## Review follow-ups
- WeaponTuning fingerprint (reflection, ordinal name order) mixed into MatchSim hash: same seed + different tuning → different hash (test `TuningChangeChangesHash`).
- Balance fixtures marked `[NonParallelizable]`.
- Baseline platform: Windows x64, .NET 10 CoreCLR, Ryzen 7 7840HS. Other runtimes (IL2CPP/ARM) may shift results slightly; the threshold gate is meant for this dotnet suite.
- If IL2CPP managed stripping is raised above Minimal, keep WeaponTuning fields (link.xml) because Fingerprint reflects over them.
