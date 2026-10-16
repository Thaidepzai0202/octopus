"""Biểu tượng 3 nút chiêu cho mỗi nhân vật (tròn 256 px), ghép từ chính tranh nhân vật.

Chạy:  python3 Tools/art/skill_icons.py
Ra:    Assets/_Project/Art/UI/Skill_Octopus_1..3.png, Skill_Spider_1..3.png
       1 = đánh thường, 2 = chiêu, 3 = chiêu cuối (nền đỏ tím rực)
"""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from rig_mesh import crop_box, premultiplied_resize

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Project/Art/UI"
OCTOPUS = ROOT / "Docs/octopus/octopus.png"
SHEET = ROOT / "Docs/spider/Spider_pink.png"
LASER_ICON = OUT / "LaserSkillIcon.png"
SIZE = 256


def background(inner, outer):
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32) + 0.5
    r = np.hypot(xx - SIZE / 2, yy - SIZE / 2) / (SIZE / 2)
    rgb = np.array(inner) * (1 - r[..., None]) + np.array(outer) * r[..., None]
    return np.dstack([np.clip(rgb, 0, 1), np.ones_like(r)])


def paste(canvas, art, box, fit, offset=(0, 0), rotate=0.0):
    """Đặt vùng box của ảnh art (cắt sát) vào giữa canvas, cỡ fit px."""
    rgba = np.asarray(art.crop(box).convert("RGBA")).astype(np.float32)
    x0, y0, x1, y1 = crop_box(rgba[..., 3], margin=2)
    rgba = rgba[y0:y1, x0:x1]
    scale = fit / max(rgba.shape[:2])
    small = Image.fromarray(premultiplied_resize(rgba, scale), "RGBA")
    if rotate:
        small = small.rotate(rotate, Image.BICUBIC, expand=True)
    layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    layer.alpha_composite(small, ((SIZE - small.width) // 2 + offset[0], (SIZE - small.height) // 2 + offset[1]))
    base = Image.fromarray((canvas * 255).astype(np.uint8), "RGBA")
    base.alpha_composite(layer)
    return np.asarray(base).astype(np.float32) / 255


def slash(canvas, color, start, end, radius=96, width=18):
    """Vệt chém sáng hình cung."""
    layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    box = (SIZE / 2 - radius, SIZE / 2 - radius, SIZE / 2 + radius, SIZE / 2 + radius)
    for w, a in ((width * 2.2, 70), (width, 200), (width * 0.35, 255)):
        c = tuple(int(255 * v) for v in color) if a < 255 else (255, 255, 255)
        draw.arc(box, start, end, fill=c + (a,), width=int(w))
    layer = layer.filter(ImageFilter.GaussianBlur(1.2))
    base = Image.fromarray((canvas * 255).astype(np.uint8), "RGBA")
    base.alpha_composite(layer)
    return np.asarray(base).astype(np.float32) / 255


def web(canvas, color, radius=112, spokes=10, rings=5, width=3):
    layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    c = SIZE / 2
    col = tuple(int(255 * v) for v in color) + (230,)
    for k in range(spokes):
        a = 2 * math.pi * k / spokes
        draw.line([(c, c), (c + radius * math.cos(a), c + radius * math.sin(a))], fill=col, width=width)
    for ring in range(1, rings + 1):
        r = radius * ring / rings
        pts = [(c + r * math.cos(2 * math.pi * k / spokes), c + r * math.sin(2 * math.pi * k / spokes)) for k in range(spokes + 1)]
        draw.line(pts, fill=col, width=width - 1 if ring < rings else width)
    base = Image.fromarray((canvas * 255).astype(np.uint8), "RGBA")
    base.alpha_composite(layer.filter(ImageFilter.GaussianBlur(0.6)))
    return np.asarray(base).astype(np.float32) / 255


def finish(canvas, rim, name):
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32) + 0.5
    r = np.hypot(xx - SIZE / 2, yy - SIZE / 2)
    canvas = canvas.copy()
    glow = np.exp(-((r - (SIZE / 2 - 10)) / 7) ** 2)[..., None] * np.array(rim)  # viền sáng mép trong
    canvas[..., :3] = 1 - (1 - np.clip(canvas[..., :3], 0, 1)) * np.exp(-glow)
    canvas[..., 3] = np.clip(SIZE / 2 - 2 - r + 0.5, 0, 1)
    path = OUT / f"{name}.png"
    Image.fromarray((np.clip(canvas, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(path)
    print("->", path.relative_to(ROOT))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    octopus = Image.open(OCTOPUS)
    sheet = Image.open(SHEET)

    # Bạch tuộc 1: xúc tu có giác hút + vệt quất
    c = background((0.16, 0.12, 0.3), (0.05, 0.03, 0.12))
    c = paste(c, octopus, (780, 640, 1180, 1120), 210, (10, 6), rotate=-20)
    c = slash(c, (0.45, 0.85, 1.0), 200, 330)
    finish(c, (0.45, 0.85, 1.0), "Skill_Octopus_1")

    # Bạch tuộc 2: mắt bắn laze (biểu tượng laze sẵn có)
    Image.open(LASER_ICON).convert("RGBA").resize((SIZE, SIZE), Image.LANCZOS).save(OUT / "Skill_Octopus_2.png")
    print("->", (OUT / "Skill_Octopus_2.png").relative_to(ROOT))

    # Bạch tuộc 3 (chiêu cuối): cả con trên nền đỏ tím rực
    c = background((0.75, 0.2, 0.35), (0.12, 0.02, 0.15))
    c = paste(c, octopus, (0, 0, octopus.width, octopus.height), 220, (0, 6))
    finish(c, (1.0, 0.45, 0.35), "Skill_Octopus_3")

    # Nhện 1: chân gai + vệt cào
    c = background((0.25, 0.1, 0.3), (0.07, 0.02, 0.1))
    c = paste(c, sheet, (22, 894, 261, 1077), 200, (0, 10))
    c = slash(c, (1.0, 0.45, 0.9), 210, 320, radius=90)
    finish(c, (1.0, 0.45, 0.9), "Skill_Spider_1")

    # Nhện 2: mạng tơ + cụm mắt
    c = background((0.2, 0.12, 0.28), (0.05, 0.03, 0.1))
    c = web(c, (0.95, 0.95, 1.0))
    c = paste(c, sheet, (815, 640, 1035, 800), 120)
    finish(c, (0.95, 0.95, 1.0), "Skill_Spider_2")

    # Nhện 3 (chiêu cuối): cả con trên nền đỏ tím rực
    c = background((0.75, 0.2, 0.35), (0.12, 0.02, 0.15))
    c = paste(c, sheet, (196, 14, 1244, 618), 236, (0, 4))
    finish(c, (1.0, 0.45, 0.35), "Skill_Spider_3")


if __name__ == "__main__":
    main()
