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

## View (`Assets/Scripts/View/`)
| File | Role |
|---|---|
| `ArenaView.cs` | Owns MatchSim; fixed 60 Hz step in Update, interpolated draw; `SimEventRaised`, `MatchEnded`; deferred `StartMatch`; `SimToWorld` |
| `BallView.cs` | Body sprite + 9-sliced blade on rotating pivot; pixel-snapped position/length; heat tint |
| `ArenaFrameView.cs` | Floor, walls (shrink warning), background mask above blades = clipping at arena edge |
| `HudView.cs` | Names, HP (lagging lost bar), weapon stat; strings rebuilt only on change |
| `PixelText.cs`, `PixelFontData.cs` | 3x5 pixel font (atlas generated in Editor, glyph sprites sliced at runtime) |
| `Palette.cs`, `ArtLibrary.cs` | Colors / per-weapon look; sprite references (swap art here) |

Rendering: 270x480 native, PPU 1, URP `PixelPerfectCamera` UpscaleRenderTexture + Windowbox; world units = native pixels = sim units; arena centred at origin. Sorting: floor 0, bodies 10+, blades 20+, (FX 25–29), mask 30, walls 31, HUD 40+.
Editor tools: `BallBattle/Generate Placeholder Art`, `BallBattle/Build Scenes`, `BallBattle/Apply Project Settings`.

### Effects & audio
`FxView` (same GameObject as ArenaView) maps SimEvents to: `BallView.Flash`, `PixelParticles` (pooled, whole-pixel), `DamagePopups` (pooled PixelText, cached strings), `ScreenShake` (integer px, re-captures base when idle), `SfxPlayer` (8 voices, same-clip 30 ms gap). Placeholder WAVs from `PlaceholderSfxSynth` via `BallBattle/Generate Placeholder Sfx`; clips referenced by `SfxLibrary`. Zero GC allocation per frame mid-match (Profiler Recorder).

### Match flow
`GameController` (GameObject "Game") owns `MatchFlow` (Menu → Countdown → Playing → RoundOver → Result) and the pixel UI: `MenuView` (weapon pick, sorting 60+), `OverlayView` (score, seed, countdown, banner, result panel 70+), `PixelButton` + `PixelInput` (screen → native via Windowbox integer zoom, legacy input). `Sim/Series` holds best-of-3 rules and per-round seeds: REMATCH replays the series exactly. Arena is `Paused` in menu/countdown. Esc / Android Back returns to the menu.
