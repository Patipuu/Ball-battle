#!/usr/bin/env python3
"""Probe an unknown raw pixel buffer: render it in every common layout to one contact sheet.

Use it the first time you meet a sprite/tile format: dump one decoded (or undecoded) pixel
buffer and look which tile of the sheet shows a sensible picture. That tells you the row
length (w vs h swapped = transposed image), the channel order (RGB vs BGR) and the pixel
format (8888 / 888 / 565 / 555 / 8-bit palette index).

    python raw_image_probe.py buffer.bin --size 40x52 [--offset 0] [--out probe.png] [--scale 4]
    python raw_image_probe.py buffer.bin --guess-width 16..128   # unknown size: try row lengths

Each tile is labelled "<format> <w>x<h>". Upright + natural colours = the right one.
"""
from __future__ import annotations

import argparse
import struct
from pathlib import Path

from PIL import Image, ImageDraw


def decode(buf: bytes, w: int, h: int, fmt: str) -> Image.Image | None:
    n = w * h
    img = Image.new("RGBA", (w, h))
    px = img.load()
    try:
        for i in range(n):
            if fmt in ("RGBA8888", "BGRA8888"):
                b = buf[i * 4:i * 4 + 4]
                r, g, bl, a = (b[0], b[1], b[2], b[3]) if fmt == "RGBA8888" else (b[2], b[1], b[0], b[3])
            elif fmt in ("RGB888", "BGR888"):
                b = buf[i * 3:i * 3 + 3]
                r, g, bl = (b[0], b[1], b[2]) if fmt == "RGB888" else (b[2], b[1], b[0])
                a = 255
            elif fmt in ("RGB565", "BGR565", "RGB555"):
                v = struct.unpack_from("<H", buf, i * 2)[0]
                if fmt == "RGB555":
                    r, g, bl = (v >> 10) & 31, (v >> 5) & 31, v & 31
                    r, g, bl = r << 3, g << 3, bl << 3
                else:
                    r, g, bl = (v >> 11) & 31, (v >> 5) & 63, v & 31
                    r, g, bl = r << 3, g << 2, bl << 3
                    if fmt == "BGR565":
                        r, bl = bl, r
                a = 255
            elif fmt == "GRAY8":
                r = g = bl = buf[i]
                a = 255
            else:
                return None
            px[i % w, i // w] = (r, g, bl, a)
    except (IndexError, struct.error):
        return None
    return img


FORMATS = ["RGBA8888", "BGRA8888", "RGB888", "BGR888", "RGB565", "BGR565", "RGB555", "GRAY8"]


def sheet(tiles: list[tuple[str, Image.Image]], scale: int) -> Image.Image:
    cols = 4
    cw = max(t.width for _, t in tiles) * scale + 8
    ch = max(t.height for _, t in tiles) * scale + 20
    rows = (len(tiles) + cols - 1) // cols
    out = Image.new("RGBA", (cols * cw, rows * ch), (255, 0, 255, 255))
    d = ImageDraw.Draw(out)
    for k, (label, t) in enumerate(tiles):
        x, y = (k % cols) * cw, (k // cols) * ch
        out.paste(t.resize((t.width * scale, t.height * scale), Image.NEAREST), (x + 4, y + 16))
        d.text((x + 4, y + 2), label, fill=(0, 0, 0, 255))
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("buffer")
    ap.add_argument("--size", help="WxH as stored (the probe also tries HxW)")
    ap.add_argument("--guess-width", help="lo..hi row lengths to try with RGB565/BGR888")
    ap.add_argument("--offset", type=int, default=0)
    ap.add_argument("--scale", type=int, default=4)
    ap.add_argument("--out", default="probe.png")
    a = ap.parse_args()
    buf = Path(a.buffer).read_bytes()[a.offset:]
    tiles = []
    if a.size:
        w, h = (int(v) for v in a.size.lower().split("x"))
        for fmt in FORMATS:
            for ww, hh in ((w, h), (h, w)):
                img = decode(buf, ww, hh, fmt)
                if img:
                    tiles.append((f"{fmt} {ww}x{hh}", img))
    elif a.guess_width:
        lo, hi = (int(v) for v in a.guess_width.split(".."))
        for ww in range(lo, hi + 1):
            for fmt in ("RGB565", "BGR888", "RGBA8888"):
                bpp = {"RGB565": 2, "BGR888": 3, "RGBA8888": 4}[fmt]
                hh = min(256, len(buf) // (ww * bpp))
                img = decode(buf, ww, hh, fmt) if hh > 0 else None
                if img:
                    tiles.append((f"{fmt} w={ww}", img))
    else:
        ap.error("give --size or --guess-width")
    sheet(tiles, a.scale).save(a.out)
    print(f"{len(tiles)} variants -> {a.out}")


if __name__ == "__main__":
    main()
