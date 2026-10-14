"""Tách bảng phân lớp AI thành từng lớp PNG nền trong suốt cho game."""
import sys
from PIL import Image, ImageFilter
import numpy as np

SRC = sys.argv[1]
OUT = sys.argv[2]
im = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)

def shift(a, dy, dx, fill):
    out = np.full_like(a, fill)
    h, w = a.shape[:2]
    ys, yd = (slice(dy, h), slice(0, h - dy)) if dy >= 0 else (slice(0, h + dy), slice(-dy, h))
    xs, xd = (slice(dx, w), slice(0, w - dx)) if dx >= 0 else (slice(0, w + dx), slice(-dx, w))
    out[yd, xd] = a[ys, xs]
    return out

def cut_out(rgb):
    """Nền ô caro xám sáng → trong suốt; mép: tính lại alpha theo độ sáng và lấy màu từ phần đá bên trong (khử viền sáng)."""
    mx, mn = rgb.max(2), rgb.min(2)
    background = (mn > 190) & (mx - mn < 22)
    # vùng lõi: cách nền ít nhất 3 px
    near = background.copy()
    for _ in range(3):
        near = near | shift(near, 1, 0, False) | shift(near, -1, 0, False) | shift(near, 0, 1, False) | shift(near, 0, -1, False)
    core = ~near
    # lan màu lõi ra vùng mép (trung bình các láng giềng đã có màu)
    color = np.where(core[..., None], rgb, 0.0)
    known = core.astype(np.float32)
    for _ in range(6):
        acc = np.zeros_like(color); cnt = np.zeros_like(known)
        for dy, dx in [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)]:
            acc += shift(color * known[..., None], dy, dx, 0.0); cnt += shift(known, dy, dx, 0.0)
        fill = (known == 0) & (cnt > 0)
        color[fill] = acc[fill] / cnt[fill][:, None]
        known[fill] = 1.0
    bg_lum = 242.0
    lum = rgb.mean(2)
    fg_lum = color.mean(2)
    alpha = np.clip((bg_lum - lum) / np.maximum(bg_lum - fg_lum, 30.0), 0.0, 1.0)
    alpha[core] = 1.0
    alpha[background] = 0.0
    out_rgb = np.where(core[..., None], rgb, color)
    return np.dstack([out_rgb, alpha * 255.0]).clip(0, 255).astype(np.uint8)

def upscale(rgba, factor=2):
    img = Image.fromarray(rgba, "RGBA").convert("RGBa")  # nhân sẵn alpha để phóng to không sinh viền
    img = img.resize((img.width * factor, img.height * factor), Image.LANCZOS).convert("RGBA")
    r, g, b, a = img.split()
    sharp = Image.merge("RGB", (r, g, b)).filter(ImageFilter.UnsharpMask(radius=2, percent=60, threshold=2))
    return Image.merge("RGBA", (*sharp.split(), a))

def fade_vertical(img, top, bottom):
    """Làm mờ dần mép trên / dưới của dải toàn cảnh để hoà vào phông nền."""
    a = np.asarray(img).astype(np.float32).copy()
    h = a.shape[0]
    ramp = np.ones(h, np.float32)
    t, b = int(h * top), int(h * bottom)
    if t: ramp[:t] = np.linspace(0, 1, t) ** 1.5
    if b: ramp[h - b:] = np.linspace(1, 0, b) ** 1.5
    a[..., 3] *= ramp[:, None]
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")

def save(img, name):
    img.save(f"{OUT}/{name}.png")
    print(f"  {name}.png  {img.width}x{img.height}")

# Dải (hàng, cột) đo được từ ảnh: hàng toàn nền ở 212-220, 398-419, 641-655, 813-823; khung trắng của 2 dải toàn cảnh
bands = {
    "1_tran_hang": (0, 212, 0, 1536),
    "2_dao_cau_day": (221, 408, 0, 1536),
    "3_lau_dai": (408, 641, 0, 1536),
}
for name, (y0, y1, x0, x1) in bands.items():
    save(upscale(cut_out(im[y0:y1, x0:x1])), name)

# Hai dải toàn cảnh: cắt vào trong khung trắng 3 px, kín nền
for name, (y0, y1, x0, x1) in {"4_toan_canh_thac": (660, 809, 18, 1518), "5_toan_canh_ho": (827, 1009, 19, 1518)}.items():
    rgba = np.dstack([im[y0:y1, x0:x1], np.full((y1 - y0, x1 - x0), 255.0)]).astype(np.uint8)
    save(upscale(rgba), name)
