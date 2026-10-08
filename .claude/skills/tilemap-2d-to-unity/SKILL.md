---
name: tilemap-2d-to-unity
description: "Rebuild the maps of an old 2D tile/grid game (Bomberman-likes, top-down RPGs, puzzle and arcade games) in Unity from the original client's own data: find and decode sprite/map formats, bake them to a neutral PNG+JSON format, auto-build Unity Tilemap scenes with exact anchors and draw order, then prove the result with an independent reference render and pixel diff. Also drives an open Unity Editor over MCP to build/capture scenes. Use when porting or remastering a 2D game's maps/sprites, re-baking assets, or checking that a map looks exactly like the original."
user-invocable: true
when_to_use: "Invoke when original 2D game data (packs, sprite sheets, map files) must become pixel-exact Unity scenes, or when a ported map must be verified against the original."
category: game-dev
keywords: [unity, tilemap, 2d, sprites, reverse-engineering, porting, remaster, pixel-diff, map]
argument-hint: "[discover | bake | build <map> | verify <map>]"
metadata:
  author: boom-online-handoff
  version: "2.0.0"
---

# 2D tile map -> Unity (from the original game's data)

Turn an old 2D game's own assets into Unity maps that match the original pixel for pixel, and
prove it. Game-agnostic; `references/example-boom-online-*.md` is a complete worked example.

## Core idea

Maps in grid games are **data, not images**: a grid of cell ids per layer + tile sprites with
anchors. The original client composes the picture at run time; do the same in Unity, and check
it against a second, dumb implementation of the same placement rule.

```
original data --(game-specific extractor)--> NEUTRAL BAKED FORMAT --(generic)--> Unity scenes
                                                   |                                  |
                                                   +--> reference_render.py --> compare_images.py (0% diff)
```

Only the extractor is per game. Everything after the baked format is reusable (this skill's
`scripts/` and `templates/unity/`).

## Phases

1. **Discover the formats** (`references/methodology.md`). Inventory the containers, find the
   decryption/compression, decode one sprite (use `scripts/raw_image_probe.py` to find row
   length, channel order and pixel format), find the per-sprite anchor/origin, the tile id
   table, and the map layout (fit header + `width*height*layers` exactly against every map file).
2. **Write the extractor** for this game: emit the baked format (`references/baked-format.md`):
   `objects.json` (object -> state -> frames -> layers with PNG + anchor), `sprites/**.png`,
   `maps/<Map>.json` (width, height, layers, grid, palette). Keep decoding fixes in the
   extractor, never hand-edit PNGs.
3. **Validate** with `python scripts/validate_baked.py <baked>` (missing PNGs, unbaked palette
   objects, grid ids without palette, wild anchors, blank frames).
4. **Reference render**: `python scripts/reference_render.py <baked> <Map> --out ref.png`. Look at
   it first. If it looks wrong, the extractor is wrong; Unity cannot fix that.
5. **Unity**: copy `templates/unity/Editor/*.cs` into the project (or adapt the importer you
   have), set `SourceRoot/ArtRoot/TilePx`, import, build the scene, capture
   (`references/unity-setup-and-verify.md`: import settings, sorting axis, camera, capture).
6. **Verify**: `python scripts/compare_images.py capture.png ref.png --diff diff.png`. Static map
   must be 0% (tolerance 24/channel). Then look at full-screen captures with gameplay running.
7. **Automate**: build/capture many maps through the open Editor with
   `scripts/unity_mcp_client.py` (one short call per map) or Unity batchmode when the Editor is closed.

## Scripts (this skill)

| Script | Use |
|---|---|
| `scripts/raw_image_probe.py buf.bin --size WxH` | Contact sheet of every pixel layout (RGBA/BGRA/888/565/555/gray x WxH/HxW) to identify an unknown sprite format |
| `scripts/validate_baked.py <baked> [--grid-dir d] [--ignore obj...]` | Consistency check of the baked format (exit 1 on errors) |
| `scripts/reference_render.py <baked> <Map> [--grid g.json]` | Independent PIL render of a map from the baked format |
| `scripts/compare_images.py cap.png ref.png [--diff d.png]` | Pixel diff, worst cells, diff image (exit 1 above `--max`) |
| `scripts/unity_mcp_client.py exec/refresh/wait/console/call` | Drive the OPEN Unity Editor via the MCP-for-Unity HTTP server (127.0.0.1:8082) |
| `templates/unity/Editor/BakedTilemapImporter.cs` | Copies baked PNGs in, Point filter, PPU = cell, pivot = anchor |
| `templates/unity/Editor/TilemapSceneBuilder.cs` | One Tilemap per layer (Individual sort), Tile per id, camera, capture |

## Rules that save days

- Decode once, correctly, in one place; every consumer (reference render, Unity, tools) reads the baked output.
- The reference render must stay independent and simple; if both it and Unity are wrong the same way, you learn nothing.
- Look at pictures (reference render, captures, diff) before claiming anything works.
- Read `references/pitfalls.md` before touching anchors, sorting, big sprites or the bake.
- Tag every gameplay number you port as data / client code / guess / server so guesses stay visible.
