"""
iOS UIActivityIndicatorView-style spinner GIF.

Matches open-source MIT recreations of the classic indicator:
- SpinKit line-spin-fade-loader (https://github.com/tobiasahlin/SpinKit) MIT
- NVActivityIndicatorView LineSpinFadeLoader MIT

Uses opaque grayscale blades (GIF-safe) with discrete 12-step opacity cycle.
"""
from __future__ import annotations

import math
import os

from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), "ios_activity_indicator.gif")
SIZE = 120
BLADE_COUNT = 12


def main() -> None:
    cx = cy = SIZE / 2
    inner = SIZE * 0.30
    outer = SIZE * 0.48
    thickness = SIZE * 0.075
    frames: list[Image.Image] = []

    for frame in range(BLADE_COUNT):
        # Transparent background via palette index 0 later
        img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        for i in range(BLADE_COUNT):
            dist = (i - frame) % BLADE_COUNT
            # Brightest near `frame`, fading around the ring (iOS trail)
            t = ((BLADE_COUNT - dist) % BLADE_COUNT) / BLADE_COUNT
            if dist == 0:
                level = 255
            else:
                level = int(255 * (0.18 + 0.82 * (t ** 1.5)))
            angle = math.radians(i * (360 / BLADE_COUNT) - 90)
            x0 = cx + inner * math.cos(angle)
            y0 = cy + inner * math.sin(angle)
            x1 = cx + outer * math.cos(angle)
            y1 = cy + outer * math.sin(angle)
            px = -(y1 - y0)
            py = x1 - x0
            plen = math.hypot(px, py) or 1.0
            px = px / plen * (thickness / 2)
            py = py / plen * (thickness / 2)
            poly = [
                (x0 + px, y0 + py),
                (x0 - px, y0 - py),
                (x1 - px, y1 - py),
                (x1 + px, y1 + py),
            ]
            color = (level, level, level, 255)
            draw.polygon(poly, fill=color)
            r = thickness / 2
            draw.ellipse([x0 - r, y0 - r, x0 + r, y0 + r], fill=color)
            draw.ellipse([x1 - r, y1 - r, x1 + r, y1 + r], fill=color)

        # Convert with real transparency for GIF
        alpha = img.split()[-1]
        rgb = img.convert("RGB").convert("P", palette=Image.ADAPTIVE, colors=64)
        # Anything near-black transparent? Use mask from alpha
        mask = alpha.point(lambda a: 255 if a < 8 else 0)
        rgb.paste(0, mask=mask)
        rgb.info["transparency"] = 0
        frames.append(rgb)

    frames[0].save(
        OUT,
        save_all=True,
        append_images=frames[1:],
        duration=75,
        loop=0,
        disposal=2,
        transparency=0,
        optimize=False,
    )
    print(f"wrote {OUT} ({os.path.getsize(OUT)} bytes)")


if __name__ == "__main__":
    main()
