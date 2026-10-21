"""Ảnh giao diện cho 3 nhân vật người (RED, PINK, BLUE), cắt từ tấm bộ phận Docs/nhan_vat_moi/<red|pink|blue>/<tên>.png.

  - Chân dung màn chọn nhân vật: cái đầu rời (góc trên phải tấm bộ phận), đặt giữa khung 512 x 512, nền trong suốt.
  - Biểu tượng tạm 3 nút chiêu (tròn 256 px, cùng kiểu Tools/art/skill_icons.py): 1 = vũ khí + vệt chém (đánh thường),
    2 = một phần vũ khí phóng to (kĩ năng), 3 = cả con ráp sẵn trên nền đỏ rực (kĩ năng đặc biệt, cần nộ).

Chạy:  python3 Tools/art/hero_ui.py [thư mục Docs]   (mặc định: Docs cạnh Assets)
Ra:    Assets/_Project/Art/UI/Portrait_Red.png, Portrait_Pink.png, Portrait_Blue.png,
       Skill_Red_1..3.png, Skill_Pink_1..3.png, Skill_Blue_1..3.png
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[2]
DOCS = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "Docs"
OUT = ROOT / "Assets/_Project/Art/UI"
ICON = 256
PORTRAIT = 512

# Vùng (trái, trên, phải, dưới) trên tấm 1086 x 1448 của từng con
HEROES = {
    "Red": dict(
        sheet="red/red.png",
        head=(540, 0, 960, 380),
        figure=(0, 0, 545, 1015),
        weapon=(845, 770, 1086, 1448),      # thanh kiếm rời
        detail=(850, 775, 1086, 1060),      # chuôi + hộ thủ có viên pha lê đỏ
        weapon_angle=-40,
        color=(1.0, 0.22, 0.2),
        edge=(0.45, 0.02, 0.05),
    ),
    "Pink": dict(
        sheet="pink/pink.png",
        head=(560, 0, 900, 370),
        figure=(0, 20, 610, 915),
        weapon=(885, 0, 1086, 905),         # trượng rời
        detail=(885, 0, 1086, 330),         # viên pha lê đầu trượng
        weapon_angle=-35,
        color=(0.78, 0.42, 1.0),
        edge=(0.3, 0.05, 0.45),
    ),
    "Blue": dict(
        sheet="blue/blue.png",
        head=(700, 0, 1030, 290),
        figure=(0, 0, 650, 895),
        weapon=(0, 875, 660, 1400),         # lưỡi hái rời
        detail=(0, 875, 420, 1200),         # lưỡi hái lửa xanh
        weapon_angle=10,
        color=(0.3, 0.78, 1.0),
        edge=(0.02, 0.2, 0.45),
    ),
}


def rgba_array(img):
    return np.asarray(img.convert("RGBA")).astype(np.float32)


def largest_part(rgba, grow=4):
    """Chỉ giữ mảng lớn nhất (kèm viền mờ quanh nó): bỏ mẩu bộ phận khác lọt vào vùng cắt."""
    labels, _ = ndi.label(rgba[..., 3] > 40)
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    keep = ndi.binary_dilation(labels == sizes.argmax(), iterations=grow)
    out = rgba.copy()
    out[..., 3] *= keep
    return out


def crop_alpha(rgba, margin=2):
    ys, xs = np.nonzero(rgba[..., 3] > 8)
    y0, y1 = max(0, ys.min() - margin), min(rgba.shape[0], ys.max() + margin + 1)
    x0, x1 = max(0, xs.min() - margin), min(rgba.shape[1], xs.max() + margin + 1)
    return rgba[y0:y1, x0:x1]


def to_image(rgba):
    return Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8), "RGBA")


def fit(img, size):
    """Thu nhỏ (khử răng cưa qua alpha nhân trước) cho cạnh dài nhất = size."""
    scale = size / max(img.size)
    w, h = max(1, round(img.width * scale)), max(1, round(img.height * scale))
    a = np.asarray(img).astype(np.float32) / 255
    pre = np.dstack([a[..., :3] * a[..., 3:], a[..., 3:]])
    small = np.asarray(Image.fromarray((pre * 255).astype(np.uint8), "RGBA").resize((w, h), Image.LANCZOS)).astype(np.float32) / 255
    alpha = small[..., 3:]
    rgb = np.where(alpha > 1e-4, small[..., :3] / np.maximum(alpha, 1e-4), 0)
    return Image.fromarray((np.dstack([np.clip(rgb, 0, 1), alpha]) * 255).astype(np.uint8), "RGBA")


def part(sheet, box, keep_largest=True, fade=0.0):
    """Cắt vùng box. keep_largest: chỉ giữ mảng lớn nhất. fade > 0: mép vùng cắt mờ dần (phần vũ khí bị cắt ngang)."""
    rgba = rgba_array(sheet.crop(box))
    if keep_largest:
        rgba = largest_part(rgba)
    if fade > 0:
        h, w = rgba.shape[:2]
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        d = np.minimum(np.minimum(xx, w - 1 - xx) / (w * fade), np.minimum(yy, h - 1 - yy) / (h * fade))
        rgba[..., 3] *= np.clip(d, 0, 1) ** 1.5
    return to_image(crop_alpha(rgba))


def radial(inner, outer, size=ICON):
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32) + 0.5
    r = np.clip(np.hypot(xx - size / 2, yy - size / 2) / (size / 2), 0, 1)[..., None]
    rgb = np.array(inner) * (1 - r) + np.array(outer) * r
    return Image.fromarray((np.dstack([rgb, np.ones(r.shape)]) * 255).astype(np.uint8), "RGBA")


def circle_mask(img, rim_color=None, rim=9):
    size = img.width
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32) + 0.5
    r = np.hypot(xx - size / 2, yy - size / 2)
    a = np.clip(size / 2 - 1 - r, 0, 1)
    arr = np.asarray(img).astype(np.float32) / 255
    if rim_color is not None:
        d = np.abs(r - (size / 2 - rim - 2))
        ring = np.clip(rim * 0.5 - d, 0, 1)[..., None]
        glow = (0.45 * np.exp(-d * d / (rim * rim)))[..., None]
        c = np.array(rim_color)[None, None, :]
        arr[..., :3] = arr[..., :3] * (1 - ring) + c * ring
        arr[..., :3] = np.clip(arr[..., :3] + c * glow * (1 - ring), 0, 1)
    arr[..., 3] *= a
    return Image.fromarray((arr * 255).astype(np.uint8), "RGBA")


def paste_center(base, art, offset=(0, 0)):
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    layer.alpha_composite(art, ((base.width - art.width) // 2 + offset[0], (base.height - art.height) // 2 + offset[1]))
    out = base.copy()
    out.alpha_composite(layer)
    return out


def glow_behind(base, art, color, radius=10, strength=0.9, offset=(0, 0)):
    """Quầng sáng màu color theo hình art, đặt dưới art."""
    pad = radius * 3
    a = np.pad(np.asarray(art)[..., 3].astype(np.float32) / 255, pad)  # chừa chỗ cho quầng loang ra ngoài khung hình
    halo = Image.fromarray((a * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(radius))
    h = np.asarray(halo).astype(np.float32) / 255 * strength
    col = np.dstack([np.ones_like(h) * c for c in color] + [h])
    return paste_center(base, Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), "RGBA"), offset)


def slash(base, color, start, end, radius=96, width=18):
    """Vệt chém sáng hình cung."""
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    c = base.width / 2
    box = (c - radius, c - radius, c + radius, c + radius)
    for w, a in ((width * 2.2, 70), (width, 200), (width * 0.35, 255)):
        col = tuple(int(255 * v) for v in color) if a < 255 else (255, 255, 255)
        draw.arc(box, start, end, fill=col + (a,), width=int(w))
    layer = layer.filter(ImageFilter.GaussianBlur(1.2))
    out = base.copy()
    out.alpha_composite(layer)
    return out


def rays(base, color, count=12):
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    c = base.width / 2
    for i in range(count):
        a = np.deg2rad(i * 360 / count + 8)
        b = np.deg2rad(i * 360 / count + 8 + 9)
        pts = [(c, c), (c + np.cos(a) * c * 1.1, c + np.sin(a) * c * 1.1), (c + np.cos(b) * c * 1.1, c + np.sin(b) * c * 1.1)]
        draw.polygon(pts, fill=tuple(int(255 * v) for v in color) + (60,))
    layer = layer.filter(ImageFilter.GaussianBlur(3))
    out = base.copy()
    out.alpha_composite(layer)
    return out


def dark(color, k):
    return tuple(v * k for v in color)


def portrait(sheet, h):
    head = part(sheet, h["head"])
    head = fit(head, PORTRAIT - 40)
    canvas = Image.new("RGBA", (PORTRAIT, PORTRAIT), (0, 0, 0, 0))
    return paste_center(canvas, head, (0, 6))


def icons(sheet, h):
    color, edge = h["color"], h["edge"]
    out = []

    # 1 = đánh thường: vũ khí chéo + vệt chém
    weapon = part(sheet, h["weapon"]).rotate(h["weapon_angle"], Image.BICUBIC, expand=True)
    weapon = fit(weapon, 236)
    base = radial(dark(color, 0.32), (0.06, 0.03, 0.1))
    base = slash(base, color, 200, 330, radius=92, width=16)
    base = glow_behind(base, weapon, color, 8, 0.8)
    base = paste_center(base, weapon)
    out.append(circle_mask(base, dark(color, 0.75)))

    # 2 = chiêu: phần sáng nhất của vũ khí phóng to, quầng sáng màu nhân vật
    detail = fit(part(sheet, h["detail"], fade=0.12), 210)
    base = radial(dark(color, 0.55), dark(edge, 0.5))
    base = glow_behind(base, detail, color, 14, 1.0)
    base = paste_center(base, detail)
    out.append(circle_mask(base, (0.85, 0.85, 0.9)))

    # 3 = chiêu cuối: cả con ráp sẵn trên nền đỏ tím rực, tia sáng toả ra
    figure = fit(part(sheet, h["figure"]), 226)
    base = radial((0.55, 0.12, 0.2), (0.15, 0.03, 0.08))
    base = rays(base, (1.0, 0.55, 0.4))
    base = glow_behind(base, figure, color, 10, 0.9, (0, 8))
    base = paste_center(base, figure, (0, 8))
    out.append(circle_mask(base, (0.95, 0.4, 0.35)))
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, h in HEROES.items():
        sheet = Image.open(DOCS / "nhan_vat_moi" / h["sheet"]).convert("RGBA")
        portrait(sheet, h).save(OUT / f"Portrait_{name}.png")
        print("->", (OUT / f"Portrait_{name}.png").relative_to(ROOT))
        for i, icon in enumerate(icons(sheet, h)):
            path = OUT / f"Skill_{name}_{i + 1}.png"
            icon.save(path)
            print("->", path.relative_to(ROOT))


if __name__ == "__main__":
    main()
