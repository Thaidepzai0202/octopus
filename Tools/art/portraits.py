"""Ảnh chân dung cho màn chọn nhân vật: cắt sát nhân vật, đặt giữa khung cùng cỡ, nền trong suốt.

Chạy:  python3 Tools/art/portraits.py
Ra:    Assets/_Project/Art/UI/Portrait_Octopus.png, Portrait_Spider.png
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

from rig_mesh import crop_box, premultiplied_resize

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Project/Art/UI"
SIZE = (640, 512)      # khung chân dung (rộng, cao)
MARGIN = 24

SOURCES = {
    "Portrait_Octopus": (ROOT / "Docs/octopus/octopus.png", None),
    # Con nhện ráp sẵn ở nửa trên tấm bộ phận
    "Portrait_Spider": (ROOT / "Docs/spider/Spider_pink.png", (0, 0, 1448, 625)),
}


def largest_part(rgba):
    """Chỉ giữ mảng lớn nhất (kèm viền mờ quanh nó): bỏ mẩu bộ phận khác lọt vào vùng cắt."""
    labels, _ = ndi.label(rgba[..., 3] > 40)
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    keep = ndi.binary_dilation(labels == sizes.argmax(), iterations=3)
    out = rgba.copy()
    out[..., 3] *= keep
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, (path, region) in SOURCES.items():
        art = Image.open(path).convert("RGBA")
        if region:
            art = art.crop(region)
        rgba = largest_part(np.asarray(art).astype(np.float32))
        x0, y0, x1, y1 = crop_box(rgba[..., 3], margin=2)
        rgba = rgba[y0:y1, x0:x1]
        scale = min((SIZE[0] - 2 * MARGIN) / rgba.shape[1], (SIZE[1] - 2 * MARGIN) / rgba.shape[0])
        small = premultiplied_resize(rgba, scale)
        canvas = Image.new("RGBA", SIZE, (0, 0, 0, 0))
        canvas.alpha_composite(Image.fromarray(small, "RGBA"),
                               ((SIZE[0] - small.shape[1]) // 2, (SIZE[1] - small.shape[0]) // 2))
        canvas.save(OUT / f"{name}.png")
        print("->", (OUT / f"{name}.png").relative_to(ROOT))


if __name__ == "__main__":
    main()
