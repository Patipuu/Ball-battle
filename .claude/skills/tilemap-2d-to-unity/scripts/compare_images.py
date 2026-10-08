#!/usr/bin/env python3
"""Pixel-compare an engine capture with a reference render; exit 1 above the allowed difference.

A pixel differs when any channel differs by more than --tolerance. Prints the differing
fraction, the worst cells (grid of --cell px) and writes a diff image (red = differing) so a
draw-order or anchor bug shows up where it is, e.g. whole rows of tall sprites.

    python compare_images.py capture.png reference.png [--max 0.001] [--tolerance 24] [--cell 40] [--diff out.png]
"""
from __future__ import annotations

import argparse
import sys
from collections import Counter

from PIL import Image, ImageChops


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("capture")
    ap.add_argument("reference")
    ap.add_argument("--max", type=float, default=0.001, help="max fraction of differing pixels")
    ap.add_argument("--tolerance", type=int, default=24, help="per-channel delta still counted as equal")
    ap.add_argument("--cell", type=int, default=40)
    ap.add_argument("--diff", help="write a diff image here")
    a = ap.parse_args()
    cap = Image.open(a.capture).convert("RGB")
    ref = Image.open(a.reference).convert("RGB")
    if cap.size != ref.size:
        print(f"FAIL size {cap.size} != {ref.size} (crop the capture to the map rect first)")
        return 1
    w, h = cap.size
    diff = ImageChops.difference(cap, ref)
    bad = [max(p) > a.tolerance for p in diff.getdata()]
    frac = sum(bad) / len(bad)
    cells = Counter((i % w // a.cell, i // w // a.cell) for i, b in enumerate(bad) if b)
    if a.diff:
        out = ref.copy()
        px = out.load()
        for i, b in enumerate(bad):
            if b:
                px[i % w, i // w] = (255, 0, 0)
        out.save(a.diff)
    verdict = "OK" if frac <= a.max else "FAIL"
    print(f"{verdict} {frac:.4%} differing pixels (max {a.max:.4%})")
    for (cx, cy), n in cells.most_common(8):
        print(f"  cell {cx},{cy}: {n} px")
    return 0 if verdict == "OK" else 1


if __name__ == "__main__":
    sys.exit(main())
