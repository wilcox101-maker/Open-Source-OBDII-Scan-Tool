#!/usr/bin/env python3
"""Seeded material backgrounds for OBDLink Diag v0.6.0. Not a vehicle cabin."""
import math
import random
from pathlib import Path
from PIL import Image

W, H = 1920, 1080
ROOT = Path(__file__).resolve().parents[2] / "assets" / "backgrounds"
RNG = random.Random(6060)

def field():
    return [RNG.randrange(256) for _ in range(W * H)]

def save(name, img):
    path = ROOT / name
    for quality in (82, 72, 62, 90, 52):
        img.save(path, format="JPEG", quality=quality, optimize=True, progressive=False)
        size = path.stat().st_size
        if 40_000 <= size <= 1_500_000:
            print(f"{path} {size} q={quality}")
            return
    raise SystemExit(f"{name} is {path.stat().st_size} bytes, outside 40000..1500000")

def paint(fn):
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        for x in range(W):
            px[x, y] = fn(x, y)
    return img

def wood():
    n = field()
    def fn(x, y):
        g = n[y * W + x]
        band = int(22 * (1 + math.sin(y / 34.0)))
        return (96 + band + g // 4, 62 + band // 2 + g // 6, 34 + g // 9)
    save("wood-grain.jpg", paint(fn))

def aluminum():
    n = field()
    def fn(x, y):
        g = n[y * W + x] // 8
        streak = 8 if (y + g) % 3 == 0 else 0
        v = 150 + streak + (g // 2)
        return (v, v, v + 4)
    save("brushed-aluminum.jpg", paint(fn))

def gloss():
    n = field()
    def fn(x, y):
        dx, dy = x - 480, y - 180
        shine = max(0, 70 - int(math.sqrt(dx * dx + dy * dy) / 8))
        v = 8 + shine + n[y * W + x] // 32
        return (v, v, min(255, v + 2))
    save("gloss-black.jpg", paint(fn))

def diamond():
    def fn(x, y):
        col, row = 28, 16
        cx, cy = (x % col) - col / 2, (y % row) - row / 2
        if (x // col + y // row) % 2:
            cy = (y % row) - row / 2
        d = abs(cx) / (col / 2) + abs(cy) / (row / 2)
        v = 70 + int(40 * max(0, 1 - d))
        return (v, v, v + 3)
    save("diamond-plate.jpg", paint(fn))

def carbon():
    n = field()
    def fn(x, y):
        cell = 10
        ix, iy = (x // cell) % 4, (y // cell) % 4
        on = (ix + iy) % 4 in (0, 1)
        v = (28 if on else 12) + n[y * W + x] // 20
        return (v, v, v)
    save("carbon-fiber.jpg", paint(fn))

def leather():
    n = field()
    def fn(x, y):
        g = n[y * W + x]
        bump = 18 if (x % 14 < 4 and y % 11 < 3) else 0
        v = 46 + bump + g // 16
        return (v + 12, v, v - 6)
    save("sports-leather.jpg", paint(fn))

if __name__ == "__main__":
    ROOT.mkdir(parents=True, exist_ok=True)
    wood(); aluminum(); gloss(); diamond(); carbon(); leather()
