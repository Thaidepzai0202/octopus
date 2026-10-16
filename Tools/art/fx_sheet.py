"""Cắt bảng hiệu ứng vẽ tay (ChatGPT, trắng / xám trên nền đen, lưới 3x3) thành ảnh hiệu ứng tô màu được trong engine.

Chạy:  python3 Tools/art/fx_sheet.py [đường dẫn bảng]   (mặc định Docs/vfx/cg_vfx_01_energy.png)
Ra:    Assets/_Project/Art/Generated/Fx*.png (CombatBuilder đặt kiểu import + gán vào Combat FX)

Bảng: hàng 1 = vệt chém lưỡi liềm dày, vệt chém mảnh, 3 vết cào; hàng 2 = sao toé tia lửa, chớp tròn, sóng dẹt có tia
bắn lên; hàng 3 = xoáy nước, cụm khói cuộn, nền nứt vỡ có mảnh đá (nhìn ngang).

Cách cắt mỗi ô: tìm khe tối giữa các ô, cắt sát vùng sáng, mép ngoài mờ dần (không lộ vết cắt). Alpha lấy từ độ sáng
(nhân gain cho thân đặc hơn), màu = độ sáng / alpha nên phần đặc giữ được sắc độ (khói, đá) còn phần mờ là trắng; tô màu
bằng màu đỉnh / màu SpriteRenderer. Dùng được cả hoà trộn thường lẫn cộng sáng.

  FxSlashPaint  vệt chém dày, trải phẳng theo cung (u dọc vệt, v từ mép trong ra mép ngoài): ảnh cho mesh vệt chém
  FxSlashThin   vệt chém mảnh, trải phẳng như trên
  FxClawPaint   3 vết cào, xoay nằm ngang (u dọc vết, v ngang qua 3 vết): ảnh cho mesh vết cào
  FxBurst       sao toé tia lửa (tâm ảnh = tâm chớp)
  FxImpact      chớp tròn có tia (tâm ảnh = tâm chớp)
  FxShock       sóng dẹt có tia bắn lên (tâm ảnh = tâm vòng)
  FxVortex      xoáy nước (tâm ảnh = mắt xoáy, quay quanh tâm được)
  FxSmoke       cụm khói cuộn (giữ sắc độ: lõi tối, mép sáng)
  FxCrackBurst  nền nứt vỡ + mảnh đá bay (gốc ở chỗ nổ trên mặt đất, xem CRACK_PIVOT_Y in ra)
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Project/Art/Generated"
SHEET = ROOT / "Docs/vfx/cg_vfx_01_energy.png"


# ---------------------------------------------------------------- tiện ích

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def valley(profile, guess, win=80):
    """Vị trí tối nhất (khe giữa hai ô) quanh guess."""
    p = np.convolve(profile, np.ones(9) / 9, mode="same")
    return guess - win + int(np.argmin(p[guess - win:guess + win]))


def split_cells(lum):
    """9 ô (x0, y0, x1, y1) theo thứ tự hàng: tìm khe tối thay vì chia đều (hình có thể lấn sang ô bên)."""
    h, w = lum.shape
    ys = [0, valley(lum.mean(1), h // 3), valley(lum.mean(1), 2 * h // 3), h]
    cells = []
    for r in range(3):
        band = lum[ys[r]:ys[r + 1]]
        xs = [0, valley(band.mean(0), w // 3), valley(band.mean(0), 2 * w // 3), w]
        cells += [(xs[c], ys[r], xs[c + 1], ys[r + 1]) for c in range(3)]
    return cells


def isolate(lum, cell, pad=10):
    """Cắt sát vùng sáng trong ô (lum ngoài ô = 0)."""
    x0, y0, x1, y1 = cell
    sub = lum[y0:y1, x0:x1]
    yy, xx = np.nonzero(sub > 0.04)
    bx0, by0 = max(0, xx.min() - pad), max(0, yy.min() - pad)
    bx1, by1 = min(sub.shape[1], xx.max() + pad + 1), min(sub.shape[0], yy.max() + pad + 1)
    return sub[by0:by1, bx0:bx1].copy(), (x0 + bx0, y0 + by0)


def centered(lum, cell, center, pad=10):
    """Cắt ô sao cho center (toạ độ bảng) nằm giữa ảnh; phần thiếu đệm đen."""
    sub, (ox, oy) = isolate(lum, cell, pad)
    cx, cy = center[0] - ox, center[1] - oy
    hw = int(math.ceil(max(cx, sub.shape[1] - cx)))
    hh = int(math.ceil(max(cy, sub.shape[0] - cy)))
    out = np.zeros((2 * hh, 2 * hw), np.float32)
    dx, dy = hw - int(round(cx)), hh - int(round(cy))
    out[dy:dy + sub.shape[0], dx:dx + sub.shape[1]] = sub
    return out


def soft_edges(lum, border=0.06):
    """Mờ dần về 0 sát mép ảnh để không lộ vết cắt / phần hình ô bên lấn sang."""
    h, w = lum.shape
    y = np.minimum(np.arange(h), np.arange(h)[::-1]) / (h * border)
    x = np.minimum(np.arange(w), np.arange(w)[::-1]) / (w * border)
    return lum * np.clip(y, 0, 1)[:, None] * np.clip(x, 0, 1)[None, :]


def resize(a, size):
    return np.asarray(Image.fromarray(a.astype(np.float32), "F").resize(size, Image.LANCZOS), np.float32)


def save(name, lum, size, gain):
    """Ghi PNG cỡ size (w, h): alpha = độ sáng × gain, màu = độ sáng / alpha (trắng ở phần mờ)."""
    lum = np.clip((lum - 0.015) / 0.985, 0, 1)
    alpha = np.clip(lum * gain, 0, 1)
    premul = lum  # màu × alpha = độ sáng gốc
    a = np.clip(resize(alpha, size), 0, 1)
    p = np.clip(resize(premul, size), 0, 1)
    gray = np.where(a > 1e-3, p / np.maximum(a, 1e-3), 1.0)
    top = max(1e-3, float(np.percentile(gray[a > 0.3], 99.5)) if (a > 0.3).any() else 1.0)
    gray = np.clip(gray / top, 0, 1)
    gray = np.where(a > 1e-3, gray, 1.0)  # chỗ trong suốt để trắng: lọc song tuyến không ra viền đen
    rgba = np.dstack([gray, gray, gray, a])
    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA").save(OUT / f"{name}.png", optimize=True)
    print("ghi", name, size[0], "x", size[1])


def fit_size(shape, longest):
    h, w = shape
    s = longest / max(h, w)
    return max(8, int(round(w * s))), max(8, int(round(h * s)))


# ---------------------------------------------------------------- trải phẳng vệt chém, xoay vết cào

def fit_circle(lum):
    """Đường tròn khớp nhất (Kåsa, trọng số độ sáng) qua phần sáng của vệt cong."""
    yy, xx = np.nonzero(lum > 0.3)
    w = lum[yy, xx]
    A = np.column_stack([xx, yy, np.ones_like(xx)]).astype(np.float64)
    b = (xx ** 2 + yy ** 2).astype(np.float64)
    sol, *_ = np.linalg.lstsq(A * w[:, None], b * w, rcond=None)
    cx, cy = sol[0] / 2, sol[1] / 2
    r = math.sqrt(sol[2] + cx * cx + cy * cy)
    return cx, cy, r


def unwrap_arc(lum, size):
    """
    Trải vệt cong thành dải thẳng: u = góc quanh tâm cung (đuôi mảnh → đầu dày), v = bán kính quanh đường giữa vệt (trong
    → ngoài). Đường giữa tính riêng từng góc nên vệt hình elip / lệch tâm vẫn ra dải thẳng.
    """
    cx, cy, _ = fit_circle(lum)
    yy, xx = np.nonzero(lum > 0.06)
    ang = np.arctan2(yy - cy, xx - cx)
    order = np.sort(ang)
    gaps = np.diff(np.concatenate([order, order[:1] + 2 * math.pi]))
    k = int(np.argmax(gaps))
    a0 = order[(k + 1) % len(order)]
    span = 2 * math.pi - gaps[k]

    # lấy mẫu dày theo (góc, bán kính) rồi tìm đường giữa vệt ở từng góc
    n_ang, n_rad = 720, 400
    r_max = float(np.hypot(lum.shape[0], lum.shape[1]))
    angles = a0 - span * 0.02 + (np.arange(n_ang) + 0.5) / n_ang * span * 1.04
    radii = np.linspace(0, r_max, n_rad)
    A, R = np.meshgrid(angles, radii)  # hàng = bán kính, cột = góc
    polar = bilinear(lum, cx + R * np.cos(A), cy + R * np.sin(A))
    weight = polar ** 2
    total = weight.sum(0) + 1e-6
    mid = (weight * radii[:, None]).sum(0) / total
    spread = np.sqrt((weight * (radii[:, None] - mid) ** 2).sum(0) / total)
    strength = total / total.max()
    # chỗ vệt mờ (hai đầu) thì đường giữa dựa vào chỗ đậm bên cạnh, rồi làm mượt
    mid = np.where(strength > 0.05, mid, np.nan)
    idx = np.arange(n_ang)
    good = ~np.isnan(mid)
    mid = np.interp(idx, idx[good], mid[good])
    mid = np.convolve(np.pad(mid, 40, mode="edge"), np.ones(81) / 81, mode="same")[40:-40]
    half = float(np.percentile(spread[strength > 0.2], 90)) * 2.6

    w, h = size
    cols = np.interp((np.arange(w) + 0.5) / w * n_ang, idx, idx)
    a = np.interp(cols, idx, angles)
    m = np.interp(cols, idx, mid)
    v = (np.arange(h) + 0.5) / h * 2 - 1
    Av, V = np.meshgrid(a, v)
    Rv = np.interp(np.tile(cols, (h, 1)), idx, mid) + V * half
    out = bilinear(lum, cx + Rv * np.cos(Av), cy + Rv * np.sin(Av))
    # đầu dày nằm ở u = 1 (đầu vệt quét), mép ngoài ở hàng trên cùng (v = 1)
    thick = (out ** 2).sum(0)
    if thick[: w // 2].sum() > thick[w // 2:].sum():
        out = out[:, ::-1]
    return out[::-1]


def bilinear(img, x, y):
    h, w = img.shape
    inside = (x >= 0) & (x <= w - 1) & (y >= 0) & (y <= h - 1)
    x = np.clip(x, 0, w - 1.001)
    y = np.clip(y, 0, h - 1.001)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - x0, y - y0
    top = img[y0, x0] * (1 - fx) + img[y0, x0 + 1] * fx
    bot = img[y0 + 1, x0] * (1 - fx) + img[y0 + 1, x0 + 1] * fx
    return (top * (1 - fy) + bot * fy) * inside


def straighten(lum):
    """Xoay để trục chính của phần sáng nằm ngang (vết cào → dải ngang)."""
    yy, xx = np.nonzero(lum > 0.2)
    w = lum[yy, xx]
    mx, my = np.average(xx, weights=w), np.average(yy, weights=w)
    cov = np.cov(np.vstack([xx - mx, yy - my]), aweights=w)
    vals, vecs = np.linalg.eigh(cov)
    major = vecs[:, int(np.argmax(vals))]
    angle = math.degrees(math.atan2(major[1], major[0]))
    img = Image.fromarray(lum.astype(np.float32), "F").rotate(angle, Image.BICUBIC, expand=True)
    out = np.asarray(img, np.float32)
    yy, xx = np.nonzero(out > 0.04)
    return out[max(0, yy.min() - 8):yy.max() + 9, max(0, xx.min() - 8):xx.max() + 9]


# ---------------------------------------------------------------- chạy

def main():
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else SHEET
    rgb = np.asarray(Image.open(path).convert("RGB"), np.float32) / 255
    lum = rgb.max(axis=2)
    cells = split_cells(lum)

    def centroid(cell):
        x0, y0, x1, y1 = cell
        sub = lum[y0:y1, x0:x1] ** 2
        yy, xx = np.mgrid[0:sub.shape[0], 0:sub.shape[1]]
        return x0 + (sub * xx).sum() / sub.sum(), y0 + (sub * yy).sum() / sub.sum()

    # Hàng 1: vệt chém, vết cào → ảnh dải cho mesh
    thick, _ = isolate(lum, cells[0])
    save("FxSlashPaint", soft_edges(unwrap_arc(thick, (512, 128)), 0.03), (512, 128), 1.9)
    thin, _ = isolate(lum, cells[1])
    save("FxSlashThin", soft_edges(unwrap_arc(thin, (512, 96)), 0.03), (512, 96), 1.8)
    claw, _ = isolate(lum, cells[2])
    claw = soft_edges(straighten(claw), 0.04)
    save("FxClawPaint", claw, fit_size(claw.shape, 512), 1.7)

    # Hàng 2: chớp, sóng
    burst = soft_edges(centered(lum, cells[3], centroid(cells[3])))
    save("FxBurst", burst, (256, 256), 1.3)
    impact = soft_edges(centered(lum, cells[4], centroid(cells[4])))
    save("FxImpact", impact, (256, 256), 1.2)
    shock = soft_edges(centered(lum, cells[5], centroid(cells[5])))
    save("FxShock", shock, fit_size(shock.shape, 512), 1.4)

    # Hàng 3: xoáy, khói, nền nứt
    vortex = soft_edges(centered(lum, cells[6], centroid(cells[6])))
    save("FxVortex", vortex, (512, 512), 1.5)
    smoke, _ = isolate(lum, cells[7])
    smoke = soft_edges(smoke, 0.05)
    save("FxSmoke", smoke, fit_size(smoke.shape, 256), 3.4)
    # Nền nứt: tâm vụ nổ ở giữa bề ngang; in tỉ lệ cao của gốc (tính từ đáy) cho CombatBuilder
    cx, cy = centroid(cells[8])
    crack, (ox, oy) = isolate(lum, cells[8])
    half = int(math.ceil(max(cx - ox, crack.shape[1] - (cx - ox))))
    wide = np.zeros((crack.shape[0], 2 * half), np.float32)
    dx = half - int(round(cx - ox))
    wide[:, dx:dx + crack.shape[1]] = crack
    print("CRACK_PIVOT_Y =", round((crack.shape[0] - (cy - oy)) / crack.shape[0], 3))
    crack = soft_edges(wide, 0.04)
    save("FxCrackBurst", crack, fit_size(crack.shape, 512), 2.8)


if __name__ == "__main__":
    main()
