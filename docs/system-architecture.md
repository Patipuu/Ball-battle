# System Architecture — Ball Battle

## Layers
```
BallBattle.Sim (pure C#)  ──snapshots + SimEvent list──▶  BallBattle.View (Unity)  ──▶ screen, audio
        ▲                                                         │
        └──────────── MatchSim.Step() called 60x/s by View ───────┘   (View never writes Sim state)
```

## Sim (`Assets/Scripts/Sim/`)
| File | Role |
|---|---|
| `MatchSim.cs` | Match lifecycle: spawn from seed, `Step()` per tick, substeps, speed limits, cooldowns, time cap, end, `ComputeHash()` |
| `MatchSim.Collisions.cs` | Blade-blade block/parry, two-phase hit detect→apply (symmetric trades, double KO = draw), ball-ball elastic bounce, walls |
| `MatchConfig.cs` | Every tunable constant + `ArenaAt(activeTick)` shrink schedule |
| `BallState.cs` | Per-ball mutable state; blade endpoints |
| `WeaponRule.cs` | Base for weapons: shape fields, `Damage`, `OnHit/OnParry/OnWall`, abstract `HashState`; one instance per ball per match (enforced) |
| `SimEvent.cs` | Event types, outcome, end reason |
| `Geometry.cs`, `Vec2.cs`, `ArenaRect.cs`, `SimRandom.cs`, `SimHash.cs` | Math, xorshift32 RNG, FNV-1a hash |

### Tick (1/60 s)
1. Hitstop active → count down, nothing moves, return.
2. `ActiveTick++`, arena = `ArenaAt(ActiveTick)` (full 230x230 until 90 s, linear to 110x110 by 120 s).
3. Substeps N = max(2, spin/4°, speed/(r/2), blade-tip sweep / parry reach), ≤ 32. Each: integrate → weapon contacts → ball-ball → walls.
4. Horizontal speed floor, then speed cap. Cooldowns. Time cap (180 s active ticks → higher HP% wins, tie = draw).

Timers count active ticks: hitstop pauses the shrink and the cap (a match with ~50 hits/parries runs ~4 s longer in real time).

### Determinism
Same build + same seed + same weapons → identical `ComputeHash()`. Float math; not guaranteed across CPU/runtime (Mono vs IL2CPP, x86 vs ARM). Cross-device determinism (online, shared replays) would need fixed-point or table sin/cos — out of v1 scope.

### Combat rules
- Blades touching = blocked (no hits for that pair). Parry effects (both spins flip, hitstop 6, push apart, `OnParry`) at most every 10 ticks per pair.
- Hit = blade segment within target radius + half thickness, or body contact for body attackers. Cooldown 15 ticks per attacker→target. Hitstop 3. Knockback away from contact point.

## Tests
`BallBattleUnity/SimTests` (dotnet, NUnit) links Sim sources. `Category=Diagnostic` = pacing report (not a gate).
