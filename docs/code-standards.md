# Code Standards — Ball Battle

## Assembly boundaries
| Assembly | Path | May reference | Rule |
|---|---|---|---|
| `BallBattle.Sim` | `Assets/Scripts/Sim/` | nothing | Pure C#, `noEngineReferences: true`. No `UnityEngine`, no `System.Random`, no wall-clock time. Deterministic from seed. |
| `BallBattle.View` | `Assets/Scripts/View/` | Sim, URP, UI | Reads Sim snapshots + events. Never writes back into Sim state. |
| `BallBattle.Editor` | `Assets/Scripts/Editor/` | Sim, View | Editor-only tools: settings bootstrap, placeholder art/sfx, scene build. Batch-runnable via `-executeMethod`. |
| `BallBattle.Tests.EditMode` | `Assets/Tests/EditMode/` | Sim, View | Unity tests (NUnit). |
| `SimTests` (dotnet) | `BallBattleUnity/SimTests/` | links Sim sources | `dotnet test`, NUnit, LangVersion 9.0. |

## Sim rules
- Fixed tick 60 Hz; all time in ticks.
- Randomness only through the seeded Sim RNG.
- Any rule change that alters outcomes → bump `SimVersion.Rules`.
- Every tick emits `SimEvent`s; View/audio react to events only.
- Code must compile under Unity (.NET Standard 2.1 API). SimTests runs on net10.0, so Unity compile is the final gate.

## Naming
- C#: PascalCase types/methods/files (Unity convention), `_camelCase` not used; private fields camelCase.
- Docs, plans, reports, scripts: kebab-case, long descriptive names.
- Files > 200 lines → consider splitting along real boundaries.

## Pixel rendering
- Native 270x480, Assets PPU 1, Point filter, no compression.
- Camera: URP `UnityEngine.Rendering.Universal.PixelPerfectCamera` (not the Built-in `UnityEngine.U2D` one).
- Screen shake and UI positions in whole native pixels.

## Verification gates
1. `dotnet test BallBattleUnity/SimTests`
2. Unity compile 0 errors + EditMode tests (batch `-runTests -testPlatform EditMode` when Editor closed; Unity MCP `run_tests` when open).

## Git
- Personal account: Patipuu <phamphu422@gmail.com> (repo-local config). Remote `github.com/Patipuu/ball-battle`.
- Conventional commits, ask before committing.
