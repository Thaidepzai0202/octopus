"""Biểu tượng nút chiêu laze: cắt con mắt từ tranh gốc, vẽ thêm tia laze bắn sang phải và chớp sáng ở mắt, bo tròn.

Chạy:  python3 Tools/art/laser_icon.py
Ra:    Assets/_Project/Art/UI/LaserSkillIcon.png (game dùng), Docs/octopus/skill/LaserSkillIcon.png (bản xem)
"""
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Docs/octopus/octopus.png"
OUTPUTS = [ROOT / "Assets/_Project/Art/UI/LaserSkillIcon.png", ROOT / "Docs/octopus/skill/LaserSkillIcon.png"]

SIZE = 256
EYE = (735, 497)       # tâm con mắt trong tranh gốc (px)
HALF = 165             # nửa cạnh vùng cắt quanh mắt (px tranh gốc)
EYE_IN_ICON = (122, 128)

PURPLE = np.array([0.62, 0.3, 1.0])
BLUE = np.array([0.4, 0.75, 1.0])
WHITE = np.array([0.95, 0.98, 1.0])


def main():
    art = Image.open(SOURCE).convert("RGBA")
    x, y = EYE
    crop = art.crop((x - HALF, y - HALF, x + HALF, y + HALF)).resize((SIZE, SIZE), Image.LANCZOS)
    rgba = np.asarray(crop).astype(np.float32) / 255.0

    # Nền tím tối phía sau (chỗ tranh trong suốt)
    background = np.array([0.1, 0.06, 0.2])
    rgb = rgba[..., :3] * rgba[..., 3:4] + background * (1 - rgba[..., 3:4])

    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32) + 0.5
    cx, cy = EYE_IN_ICON
    r = np.hypot(xx - SIZE / 2, yy - SIZE / 2)

    # Tối dần ra mép cho mắt nổi lên
    rgb *= np.clip(1.15 - 0.55 * (r / (SIZE / 2)) ** 2, 0.35, 1.0)[..., None]

    # Tia laze: ba lớp sáng cộng dồn (quầng tím, giữa xanh, lõi trắng), to dần từ mắt ra
    along = np.clip((xx - cx) / 26.0, 0.0, 1.0)
    dy = yy - cy
    light = np.zeros_like(rgb)
    for color, sigma, strength in ((PURPLE, 24.0, 1.0), (BLUE, 11.0, 1.3), (WHITE, 4.2, 2.2)):
        band = strength * np.exp(-(dy ** 2) / (2 * (sigma * (0.35 + 0.65 * along)) ** 2)) * (xx > cx - 4)
        light += band[..., None] * color

    # Chớp sáng ở mắt: quầng tròn + ngôi sao 4 cánh
    d = np.hypot(xx - cx, yy - cy)
    halo = np.exp(-(d / 40.0) ** 2) * 1.6
    star = (np.exp(-np.abs(yy - cy) / 2.2) * np.exp(-np.abs(xx - cx) / 55.0) +
            np.exp(-np.abs(xx - cx) / 2.2) * np.exp(-np.abs(yy - cy) / 45.0)) * 0.9
    light += (halo + star)[..., None] * WHITE

    rgb = 1.0 - (1.0 - np.clip(rgb, 0, 1)) * np.exp(-light)  # cộng sáng kiểu "screen", không cháy màu

    # Bo tròn, mép khử răng cưa
    radius = SIZE / 2 - 2
    alpha = np.clip(radius - r + 0.5, 0.0, 1.0)
    out = np.dstack([np.clip(rgb, 0, 1), alpha])
    image = Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA")
    for path in OUTPUTS:
        path.parent.mkdir(parents=True, exist_ok=True)
        image.save(path)
        print("->", path.relative_to(ROOT))


if __name__ == "__main__":
    main()
