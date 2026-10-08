# Neutral baked format (the contract between extractor and engine)

One folder, game-independent. The extractor writes it; `validate_baked.py`, `reference_render.py`
and the Unity templates read it.

```
<baked>/
  objects.json
  sprites/<any/path>/<n>.png
  maps/<Map>.json
  ui/...                     (optional: HUD/menu pieces, see below)
```

## objects.json

```json
{
  "Tile02_Box/Box01": {
    "Default": [ { "layers": [ { "png": "sprites/Tile02/Box01/0.png", "ox": 0, "oy": 51 } ], "dur": 1 } ]
  },
  "Game/SmallMonster": {
    "00 In":        [ { "layers": [ ... ], "dur": 5 }, ... ],
    "02 Walk Left": [ ... ]
  }
}
```

- object -> state -> frames (in order) -> layers (drawn in order, later on top).
- `png` is relative to `<baked>`. Keep blank frames (empty `layers` or a transparent PNG) so animation timing stays right.
- `(ox, oy)` = anchor inside the PNG in pixels, origin top-left, **y down**. The anchor lands on the
  **bottom-left corner of the owner's cell**. Anchors may lie outside the PNG (tall or offset sprites).
- `dur` = frame duration in simulation ticks (document the tick length of the game).
- Tiles use state `Default` (or the first state). Name everything as the original tables do.

## maps/<Map>.json

```json
{
  "width": 15, "height": 13, "layers": 2, "tile_px": 40,
  "grid": [ [1738, 4294967295], [1738, 1729], ... ],
  "palette": { "1738": "Tile02_Ground/Ice00", "1729": "Tile02_Box/Box01" },
  "empty": 4294967295,
  "spawns": [[2, 1], [12, 11]]
}
```

- `grid`: one array of layer ids per cell, row-major (`y * width + x`), `y = 0` is the TOP row.
- `palette`: cell id -> object name in `objects.json` (`cells` is accepted as an alias).
- `grid` may live in another file (e.g. the raw map export); pass `--grid` / `--grid-dir` / `GridRoot`.
- Gameplay classes (floor/wall/box/water...) go in a separate table keyed by object name, read by the game rules, not by the renderer.

## Conventions the whole pipeline relies on

| Item | Convention |
|---|---|
| Unity pivot | `(ox / W, (H - 1 - oy) / H)` |
| Unity cell | `(x, height - 1 - y)`, pixels-per-unit = cell size |
| Draw order | layer ascending; inside a layer rows top -> bottom, then x left -> right |
| Missing object | reported, never silently replaced |

## UI pieces (optional)

`ui/<member>/<record>.png` + `ui.json` (`member -> [png per record]`) + `ui-origin.json`
(`"member#record": [ox, oy]`). Place a UI piece with its top-left at `screen pos - origin`.
Full-screen backgrounds are often plain JPEG/BMP members: export them as PNG as they are.
