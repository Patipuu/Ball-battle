#!/usr/bin/env python3
"""Independent reference render of a tile map from the neutral baked format (no Unity).

Its only job is to be a second, simple implementation of "where does every sprite go", so a
Unity capture can be pixel-compared against it (compare_images.py). Keep it dumb and obvious.

Baked format (see references/baked-format.md):
  <baked>/objects.json        {object: {state: [{"layers": [{"png", "ox", "oy"}], "dur"}]}}
  <baked>/maps/<Map>.json     {"width","height","tile_px","layers","grid","palette"|"cells", "empty"}
                              grid = one list of layer ids per cell, row-major (y*width + x)
Placement: each layer PNG is drawn with its anchor (ox, oy; px from its top-left, y down) on the
bottom-left corner of the cell. Draw order = the Unity sort: layer 0 first; within a layer rows
top -> bottom, then x left -> right. Frame 0 of the "Default" state (else the first state).

    python reference_render.py <baked> <Map> [--grid other.json] [--out Map.png] [--state Default]
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image

EMPTY = 0xFFFFFFFF


def first_frame(objects: dict, name: str, state: str):
    states = objects.get(name) or {}
    frames = states.get(state) or (next(iter(states.values())) if states else None)
    return frames[0] if frames else None


def render(baked: Path, map_doc: dict, grid_doc: dict, state: str) -> tuple[Image.Image, list[str]]:
    objects = json.loads((baked / "objects.json").read_text(encoding="utf-8"))
    palette = map_doc.get("palette") or map_doc.get("cells") or {}
    w, h = grid_doc["width"], grid_doc["height"]
    tile = map_doc.get("tile_px") or grid_doc.get("tile_px") or 40
    layers = grid_doc.get("layers") or len(grid_doc["grid"][0])
    empty = grid_doc.get("empty", EMPTY)
    img = Image.new("RGBA", (w * tile, h * tile), (0, 0, 0, 255))
    missing = set()
    cache: dict[str, Image.Image] = {}
    for layer in range(layers):
        for y in range(h):
            for x in range(w):
                cid = grid_doc["grid"][y * w + x][layer]
                if cid == empty:
                    continue
                name = palette.get(str(cid))
                frame = first_frame(objects, name, state) if name else None
                if frame is None:
                    missing.add(name or f"id {cid}")
                    continue
                for lay in frame["layers"]:
                    if lay["png"] not in cache:
                        cache[lay["png"]] = Image.open(baked / lay["png"]).convert("RGBA")
                    spr = cache[lay["png"]]
                    # anchor (ox, oy) sits on the cell's bottom-left pixel (x*tile, (y+1)*tile - 1)
                    left = x * tile - lay["ox"]
                    top = (y + 1) * tile - 1 - lay["oy"]
                    img.paste(spr, (left, top), spr)  # mask paste also clips sprites hanging off the map
    return img, sorted(missing)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("baked", type=Path)
    ap.add_argument("map")
    ap.add_argument("--grid", type=Path, help="map JSON holding width/height/grid when the baked map only has the palette")
    ap.add_argument("--state", default="Default")
    ap.add_argument("--out", type=Path)
    a = ap.parse_args()
    map_doc = json.loads((a.baked / "maps" / f"{a.map}.json").read_text(encoding="utf-8"))
    grid_doc = json.loads(a.grid.read_text(encoding="utf-8")) if a.grid else map_doc
    img, missing = render(a.baked, map_doc, grid_doc, a.state)
    out = a.out or Path(f"{a.map}-reference.png")
    img.convert("RGB").save(out)
    print(f"{out}  {img.width}x{img.height}  missing {missing}")


if __name__ == "__main__":
    main()
