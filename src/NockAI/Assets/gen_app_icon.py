"""Generate NockAI app icon - original Fluent calculator tile."""
from __future__ import annotations

import io
import struct
from pathlib import Path

from PIL import Image, ImageDraw

out_dir = Path(__file__).resolve().parent

# Win11-ish blues
BLUE = (0, 120, 212, 255)
BLUE_DEEP = (0, 90, 180, 255)
CYAN = (96, 205, 255, 255)
WHITE = (255, 255, 255, 255)
KEY = (236, 240, 245, 255)


def make_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = max(1, size // 20)
    radius = max(3, size * 22 // 100)

    # App tile
    d.rounded_rectangle(
        [pad, pad, size - pad - 1, size - pad - 1],
        radius=radius,
        fill=BLUE,
    )

    # Soft top sheen
    sheen = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sheen)
    sd.rounded_rectangle(
        [pad, pad, size - pad - 1, size - pad - 1],
        radius=radius,
        fill=(255, 255, 255, 36),
    )
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rectangle([0, 0, size, int(size * 0.42)], fill=255)
    img = Image.alpha_composite(
        img, Image.composite(sheen, Image.new("RGBA", (size, size), (0, 0, 0, 0)), mask)
    )
    d = ImageDraw.Draw(img)

    if size <= 24:
        # Tiny: big equals on plate (still reads as math)
        cx, cy = size / 2, size / 2 + size * 0.02
        w, h, gap = size * 0.38, max(1, size * 0.07), size * 0.08
        d.rounded_rectangle(
            [cx - w, cy - gap - h, cx + w, cy - gap],
            radius=max(1, int(h / 2)),
            fill=WHITE,
        )
        d.rounded_rectangle(
            [cx - w, cy + gap, cx + w, cy + gap + h],
            radius=max(1, int(h / 2)),
            fill=WHITE,
        )
        return img

    # Calculator body
    m = size * 0.17
    body = [m, m * 0.92, size - m, size - m * 0.78]
    br = max(2, int(size * 0.09))
    d.rounded_rectangle(body, radius=br, fill=WHITE)

    # Display
    sx0 = m + size * 0.055
    sy0 = m * 0.92 + size * 0.055
    sx1 = size - m - size * 0.055
    sy1 = sy0 + size * 0.145
    d.rounded_rectangle(
        [sx0, sy0, sx1, sy1],
        radius=max(1, int(size * 0.035)),
        fill=CYAN,
    )
    # equals on display
    eq_y = (sy0 + sy1) / 2
    eq_x0 = sx0 + (sx1 - sx0) * 0.58
    eq_x1 = sx0 + (sx1 - sx0) * 0.90
    th = max(1, int(size * 0.02))
    d.rectangle([eq_x0, eq_y - th * 1.7, eq_x1, eq_y - th * 0.35], fill=WHITE)
    d.rectangle([eq_x0, eq_y + th * 0.35, eq_x1, eq_y + th * 1.7], fill=WHITE)

    # 3x3 keys
    grid_top = sy1 + size * 0.045
    grid_bot = size - m * 0.78 - size * 0.05
    grid_left = m + size * 0.065
    grid_right = size - m - size * 0.065
    gap = size * 0.032
    cell_w = (grid_right - grid_left - gap * 2) / 3
    cell_h = (grid_bot - grid_top - gap * 2) / 3
    kr = max(1, int(size * 0.03))
    for row in range(3):
        for col in range(3):
            x0 = grid_left + col * (cell_w + gap)
            y0 = grid_top + row * (cell_h + gap)
            fill = BLUE if (row == 2 and col == 2) else KEY
            d.rounded_rectangle(
                [x0, y0, x0 + cell_w, y0 + cell_h],
                radius=kr,
                fill=fill,
            )

    return img


def png_bytes(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def write_ico(path: Path, images: list[Image.Image]) -> None:
    blobs = [png_bytes(im) for im in images]
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    directory = b""
    for im, blob in zip(images, blobs):
        w = 0 if im.width >= 256 else im.width
        h = 0 if im.height >= 256 else im.height
        directory += struct.pack("<BBBBHHII", w, h, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    path.write_bytes(header + directory + b"".join(blobs))


def main() -> None:
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = [make_icon(s) for s in sizes]
    images[-1].save(out_dir / "app-icon.png")
    write_ico(out_dir / "app.ico", images)
    print(f"wrote app.ico ({(out_dir / 'app.ico').stat().st_size} bytes)")
    print(f"wrote app-icon.png ({(out_dir / 'app-icon.png').stat().st_size} bytes)")


if __name__ == "__main__":
    main()
