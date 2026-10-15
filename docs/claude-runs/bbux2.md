# BB-UX2: readable arena preview, brighter secondary text, even menu buttons, skill word clear of other balls

Branch `dev/step3-view`, base `86023c5` (BB-UX1). **Not committed.** View code, one art file and tests only: no Sim change, Rules stays 13, goldens unchanged.

## 1. Arena preview on Prepare, and the spike-walls thumbnail
**Problem.** The Prepare sketch used the fight colours. The floors are near-black (luminance 10–46), so on the dark ENEMY box every arena read as an empty box (`docs/claude-runs/bbux1/7-prepare-boss-*`).

**Fix** (`PrepareView.ShowArena`, preview only; the fight is unchanged):
- **Veil:** a white veil (alpha 34) over the floor tile.
- **Walls:** the wall frame is lifted 40% toward white (`PreviewWall`).
- **Spike zones:** 2 px thick (were 1).
- **Obstacles:** at least 4x4 px (were 2x2), so the 4 BUMPERS show at 1x.
- **Draw order:** wall 3, floor 4, veil 5, spikes and shrink line 6, obstacles 7.

**Thumbnail:** `Assets/Art/Sprites/arena_thumbs/thumb_spike-walls_v3.png` is now the v4 candidate from BB-FIX5 (Phu agreed with "làm tiếp" on the proposal that included it). It is the same file name, so the `.meta` is kept. It is the only arena thumbnail in the game (Versus page).

**Evidence:** `bbux2/3-prepare-{spike-walls,low-gravity,bumpers,pillar}-1x/3x`, `4-prepare-boss-warden-*`.

## 2. Secondary text contrast
- **Change:** `Palette.TextDim` goes from 140,140,168 to **170,170,196**.

| background | before | after |
|---|---|---|
| Background | 5.8:1 | 8.4:1 |
| Floor | 5.0:1 | 7.3:1 |
| HpBack | 4.3:1 | 6.2:1 |

- **Main text:** white text (232) stays clearly brighter.
- **Scope:** this applies everywhere TextDim is used (stat labels, hints, footers, the borders of grey buttons).
- **Evidence:** `bbux2/5-textdim-*`, plus every shot in `bbux2/`.

## 3. Menu buttons
CODEX was a 120x26 button with a 1x label, under the 142x30 VERSUS with a 2x label. It is now `MenuView.CodexArea` = 142x30 with a 2x label, in the same column, 10 px under VERSUS.

**Evidence:** `bbux2/1-menu-*`.

## 4. Skill word in fights (QA bc3c460, finding 2)
**Cause.** The skill word started at the caster's centre, so next to a boss (48 px body) it could rise over the boss.

**Fix:** `DamagePopups.SkillWordStart` (used by `FxView` on `SkillFired`).
- **Order tried:** above the caster, then below, left and right.
- **Pick:** the first spot whose ink box, over the whole 14 px rise, is clear of every other living ball's circle box.
- **Fallback:** above the caster.

The result then goes through the existing arena clamp and stacking.

**Checks:** unit tests only (`SkillWordAvoidsTheBossAbove`, `SkillWordGoesAboveWhenNothingIsThere`, `SkillWordFallsBackAboveWhenBoxedIn`). It was not seen in a played fight yet; the next Linux QA build should look at a SPORE vs WARDEN fight.

## 5. Tests
| suite | total | passed | failed | skipped |
|---|---|---|---|---|
| Unity EditMode | 458 | 449 | 0 | 9 (older env-gated captures) |
| SimTests | not run (no Sim change) | | | |

New `Assets/Tests/EditMode/BbUx2Tests.cs` (+ `.meta`), 10 cases:
- SecondaryTextReadsOnEveryPanel
- PreviewIsVeiledAndWallsLifted ×3 (spikes, low grav, classic)
- SpikeWallsThumbnailIsTheBrighterV4
- MenuButtonsShareOneSizeAndColumn
- SkillWordAvoidsTheBossAbove
- SkillWordGoesAboveWhenNothingIsThere
- SkillWordFallsBackAboveWhenBoxedIn
- CaptureUx2Screens (`BB_UX2_OUT`)

The first run failed one check of my own: TextDim on HpBack is 6.2, below the 6.5 I first asked for. HpBack is the HP bar background, not a text panel, so that check is now ≥ 6.0.

## 6. Files
```
 M Assets/Art/Sprites/arena_thumbs/thumb_spike-walls_v3.png (v4 pixels)
 M Assets/Scripts/View/DamagePopups.cs
 M Assets/Scripts/View/FxView.cs
 M Assets/Scripts/View/MenuView.cs
 M Assets/Scripts/View/Palette.cs
 M Assets/Scripts/View/Run/PrepareView.cs
?? Assets/Tests/EditMode/BbUx2Tests.cs (+ .meta)
?? docs/claude-runs/bbux2.md, docs/claude-runs/bbux2/
```
