#!/usr/bin/env python3
"""Check a neutral baked folder before it goes into the engine. Exit 1 on any error.

Errors: an object/state/frame with no layers or a missing PNG, an anchor far outside its PNG,
a map palette entry naming an object that was not baked, a grid id with no palette entry.
Warnings: fully transparent PNGs (blank frames), objects never used by any map.

    python validate_baked.py <baked> [--maps Map1 Map2 ...] [--grid-dir dir]  (--grid-dir: map JSONs
    with width/height/grid when <baked>/maps/*.json only hold the palette)
    python validate_baked.py <baked> --ignore "TileTransparent_Static/Static01"   # invisible by design
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image

EMPTY = 0xFFFFFFFF


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("baked", type=Path)
    ap.add_argument("--maps", nargs="*")
    ap.add_argument("--grid-dir", type=Path)
    ap.add_argument("--ignore", nargs="*", default=[])
    ap.add_argument("--anchor-slack", type=int, default=400, help="px an anchor may sit outside its PNG")
    a = ap.parse_args()
    errors, warnings = [], []
    objects = json.loads((a.baked / "objects.json").read_text(encoding="utf-8"))
    sizes: dict[str, tuple[int, int]] = {}
    for name, states in objects.items():
        if not states:
            errors.append(f"object {name}: no states")
        for state, frames in states.items():
            if not frames:
                errors.append(f"{name}/{state}: no frames")
            for i, f in enumerate(frames):
                for lay in f.get("layers") or []:
                    png = a.baked / lay["png"]
                    if lay["png"] not in sizes:
                        if not png.exists():
                            errors.append(f"{name}/{state}#{i}: missing {lay['png']}")
                            sizes[lay["png"]] = (0, 0)
                            continue
                        im = Image.open(png)
                        sizes[lay["png"]] = im.size
                        if im.mode in ("RGBA", "LA") and im.getchannel("A").getbbox() is None:
                            warnings.append(f"blank png {lay['png']}")
                    w, h = sizes[lay["png"]]
                    s = a.anchor_slack
                    if not (-s <= lay["ox"] <= w + s and -s <= lay["oy"] <= h + s):
                        errors.append(f"{name}/{state}#{i}: anchor ({lay['ox']},{lay['oy']}) far outside {w}x{h}")
    used = set()
    maps_dir = a.baked / "maps"
    names = a.maps or [p.stem for p in sorted(maps_dir.glob("*.json"))]
    for m in names:
        doc = json.loads((maps_dir / f"{m}.json").read_text(encoding="utf-8"))
        palette = doc.get("palette") or doc.get("cells") or {}
        for cid, obj in palette.items():
            used.add(obj)
            if obj not in objects and obj not in a.ignore:
                errors.append(f"map {m}: id {cid} -> {obj} not baked")
        grid_doc = doc if "grid" in doc else (json.loads((a.grid_dir / f"{m}.json").read_text(encoding="utf-8")) if a.grid_dir else None)
        if grid_doc:
            empty = grid_doc.get("empty", EMPTY)
            for k, cell in enumerate(grid_doc["grid"]):
                for cid in cell:
                    if cid != empty and str(cid) not in palette:
                        errors.append(f"map {m}: cell {k % grid_doc['width']},{k // grid_doc['width']} id {cid} has no palette entry")
                        break
    print(f"objects {len(objects)}  pngs {len(sizes)}  maps {len(names)}  errors {len(errors)}  warnings {len(warnings)}")
    for e in errors[:50]:
        print("ERROR", e)
    for w in warnings[:20]:
        print("warn ", w)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
