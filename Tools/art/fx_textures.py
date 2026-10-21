"""Ảnh hiệu ứng chiêu (VFX) vẽ bằng code: ảnh xám / trắng + alpha để tô màu lúc chạy (CombatFx).

Chạy:  python3 Tools/art/fx_textures.py
Ra:    Assets/_Project/Art/Generated/Fx*.png (CombatBuilder đặt kiểu import + gán vào Combat FX)

  FxSpark    vệt tia lửa dài (đầu ở bên phải), kéo dãn theo hướng bay
  FxSlash    mặt cắt vệt chém (u dọc vệt, v ngang: mép trong → mép ngoài sáng), có sọc năng lượng
  FxRing     vòng sóng: mép ngoài sắc, quầng mờ dần vào trong
  FxCrack    vết nứt toả ra trên mặt đất (đã ép dẹt theo phối cảnh nhìn ngang)
  FxRock0..2 mảnh đá nhiều mặt (sáng trên trái)
  FxSpikes   cụm cọc đá nhọn trồi lên (gốc ở đáy ảnh)
  FxFlame    lưỡi lửa (gốc ở đáy ảnh)
  FxBubble   bong bóng bóng loáng
  FxDrop     giọt nước / mực (đầu tròn bên phải, đuôi thon)
  FxSplat    vũng bắn toé (độc)
  FxWeb      mạng nhện tự nhiên: nan cong, sợi xoắn võng giữa các nan, giọt sương
  FxCocoon   kén tơ quấn chéo nhiều lớp
  FxSpiderling, FxSpiderlingStep  nhện con nhìn ngang (2 dáng bước), ảnh màu: thân bóng tím sáng, viền + vạch + mắt hồng

Chạy riêng vài ảnh:  python3 Tools/art/fx_textures.py spiderling cocoon

Sao toé tia lửa, chớp tròn, sóng dẹt, xoáy nước, khói, nền nứt vỡ, vệt chém vẽ tay, vết cào: Tools/art/fx_sheet.py
(cắt từ bảng vẽ tay).
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Project/Art/Generated"


# ---------------------------------------------------------------- tiện ích

def grid(w, h, ss=1):
    """Toạ độ tâm điểm ảnh u, v trong 0..1 (v hướng lên), lấy mẫu dày ss lần mỗi chiều."""
    xs = (np.arange(w * ss) + 0.5) / (w * ss)
    ys = (np.arange(h * ss) + 0.5) / (h * ss)
    u, v = np.meshgrid(xs, ys)
    return u, v


def down(a, ss):
    """Gộp ss x ss mẫu thành một điểm ảnh (khử răng cưa)."""
    if ss == 1:
        return a
    h, w = a.shape[0] // ss, a.shape[1] // ss
    return a.reshape(h, ss, w, ss, *a.shape[2:]).mean(axis=(1, 3))


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def value_noise(h, w, cells_y, cells_x, seed, wrap_x=False):
    """Noise mượt kích thước (h, w): lưới ngẫu nhiên cells_y x cells_x nội suy bicubic."""
    rng = np.random.default_rng(seed)
    g = rng.random((cells_y + 3, cells_x + 3)).astype(np.float32)
    if wrap_x:
        g[:, -3:] = g[:, :3]
    img = Image.fromarray(g, "F").resize((w + 2 * w // cells_x, h + 2 * h // cells_y), Image.BICUBIC)
    a = np.asarray(img)
    oy, ox = h // cells_y, w // cells_x
    return np.clip(a[oy:oy + h, ox:ox + w], 0, 1)


def fbm(h, w, cells_y, cells_x, seed, octaves=4, wrap_x=False):
    total, amp, s = np.zeros((h, w), np.float32), 1.0, 0.0
    for i in range(octaves):
        total += amp * value_noise(h, w, cells_y * 2 ** i, cells_x * 2 ** i, seed + 17 * i, wrap_x)
        s += amp
        amp *= 0.5
    return total / s


def sample(img, x, y, wrap_x=False):
    """Lấy mẫu song tuyến ảnh img (h, w) tại toạ độ điểm ảnh thực x, y."""
    h, w = img.shape
    if wrap_x:
        x = np.mod(x, w)
    else:
        x = np.clip(x, 0, w - 1.001)
    y = np.clip(y, 0, h - 1.001)
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    x1 = (x0 + 1) % w if wrap_x else np.minimum(x0 + 1, w - 1)
    y1 = np.minimum(y0 + 1, h - 1)
    fx, fy = x - x0, y - y0
    top = img[y0, x0] * (1 - fx) + img[y0, x1] * fx
    bot = img[y1, x0] * (1 - fx) + img[y1, x1] * fx
    return top * (1 - fy) + bot * fy


def save(name, alpha, gray=None):
    """Lưu PNG: alpha (h, w) 0..1, gray = độ sáng (h, w) hoặc None (trắng). Mảng có v hướng lên → lật dọc."""
    alpha = np.clip(alpha, 0, 1)
    g = np.ones_like(alpha) if gray is None else np.clip(gray, 0, 1)
    rgba = np.dstack([g, g, g, alpha])[::-1]
    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA").save(OUT / f"{name}.png", optimize=True)
    print("ghi", name, alpha.shape[1], "x", alpha.shape[0])


def save_premul(name, color_alpha):
    """Lưu từ ảnh màu nhân sẵn alpha (h, w, 4): chia lại alpha để ra màu thẳng."""
    rgb, a = color_alpha[..., :3], color_alpha[..., 3]
    gray = np.where(a > 1e-4, rgb[..., 0] / np.maximum(a, 1e-4), 1.0)
    save(name, a, gray)


def blur(a, sigma):
    """Làm mờ Gauss (tách hai chiều) cho mảng float."""
    radius = int(sigma * 3)
    k = np.exp(-(np.arange(-radius, radius + 1) / sigma) ** 2 / 2)
    k /= k.sum()
    for axis in (0, 1):
        pad = [(0, 0), (0, 0)]
        pad[axis] = (radius, radius)
        p = np.pad(a, pad)
        out = np.zeros_like(a)
        n = a.shape[axis]
        for i, w in enumerate(k):
            out += w * (p[i:i + n] if axis == 0 else p[:, i:i + n])
        a = out
    return a


def canvas(w, h, ss):
    """Ảnh PIL mode 'F' để vẽ (gốc trên trái như PIL)."""
    return Image.new("F", (w * ss, h * ss), 0.0)


def to_array(img, ss):
    """Ảnh PIL (gốc trên trái) → mảng v hướng lên, đã gộp mẫu."""
    return down(np.asarray(img, np.float32)[::-1], ss)


# ---------------------------------------------------------------- tia, chớp

def spark():
    w, h, ss = 128, 32, 4
    u, v = grid(w, h, ss)
    d = np.abs(v - 0.5) * 2
    sigma = 0.16 + 0.3 * u
    along = u ** 1.6 * (1 - smoothstep(0.86, 1.0, u))
    a = along * (0.55 * np.exp(-(d / sigma) ** 2) + 0.75 * np.exp(-(d / (sigma * 0.35)) ** 2))
    save("FxSpark", down(np.clip(a, 0, 1), ss))


def slash():
    w, h = 256, 64
    u, v = grid(w, h)
    streak = fbm(h, w, 10, 3, 31, 3)  # sọc chạy dọc vệt
    inner = smoothstep(0.0, 0.7, v) ** 1.1
    outer = 1 - smoothstep(0.84, 1.0, v)
    profile = inner * outer
    core = np.exp(-((v - 0.8) / 0.06) ** 2)
    mod = 0.5 + 0.8 * streak
    mod = np.where(v > 0.65, mod + (1 - mod) * smoothstep(0.65, 0.78, v), mod)
    a = np.clip(profile * mod * 0.95 + core * 0.45, 0, 1)
    save("FxSlash", a)


def ring():
    n, ss = 256, 2
    u, v = grid(n, n, ss)
    x, y = u * 2 - 1, v * 2 - 1
    r = np.hypot(x, y)
    th = np.arctan2(y, x)
    noise = fbm(64, 256, 2, 12, 5, 3, wrap_x=True)
    nz = sample(noise, (th + math.pi) / (2 * math.pi) * 256, np.clip(r, 0, 1) * 63, wrap_x=True)
    rim = np.exp(-((r - 0.88) / 0.03) ** 2)
    inner = np.where(r < 0.88, 0.5 * np.exp(-((0.88 - r) / 0.2) ** 2), 0)
    halo = 0.35 * np.exp(-((r - 0.88) / 0.07) ** 2)
    a = (rim + inner + halo) * (0.7 + 0.45 * nz)
    a *= 1 - smoothstep(0.96, 1.0, r)
    save("FxRing", down(np.clip(a, 0, 1), ss))


# ---------------------------------------------------------------- đất đá

def crack():
    size, ss = 512, 2
    img = canvas(size, size, ss)
    draw = ImageDraw.Draw(img)
    rng = np.random.default_rng(11)
    c = size * ss / 2

    def branch(x, y, ang, length, width, depth):
        step = 8 * ss
        base = ang
        while length > 0 and width > 0.6 * ss:
            ang = 0.7 * ang + 0.3 * base + rng.normal(0, 0.38)  # lắc qua lại quanh hướng chính
            nx, ny = x + math.cos(ang) * step, y + math.sin(ang) * step
            draw.line([(x, y), (nx, ny)], fill=1.0, width=max(1, int(width)))
            x, y = nx, ny
            length -= step
            width *= 0.965
            if depth < 2 and rng.random() < 0.16:
                branch(x, y, ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.0), length * 0.5, width * 0.7, depth + 1)

    for k in range(10):
        ang = k * 2 * math.pi / 10 + rng.normal(0, 0.18)
        branch(c, c, ang, rng.uniform(0.7, 0.95) * c, rng.uniform(6.5, 9) * ss, 0)
    core = np.asarray(img, np.float32)
    glow = blur(core, 6 * ss)
    a = np.clip(core * 0.95 + glow * 2.0, 0, 1)
    yy, xx = np.mgrid[0:size * ss, 0:size * ss]
    r = np.hypot(xx - c, yy - c) / c
    a = np.clip(a + 0.9 * np.exp(-(r / 0.12) ** 2), 0, 1) * (1 - smoothstep(0.85, 1.0, r))
    a = down(a, ss)
    small = Image.fromarray(a.astype(np.float32), "F").resize((512, 160), Image.BICUBIC)
    save("FxCrack", np.asarray(small)[::-1])


def facet_shard(draw, pts, center, light, rng, scale):
    """Vẽ khối đa giác chia mặt (tam giác quạt từ center), mặt hướng về đèn sáng hơn."""
    for i in range(len(pts)):
        p, q = pts[i], pts[(i + 1) % len(pts)]
        mx, my = (p[0] + q[0]) / 2 - center[0], (p[1] + q[1]) / 2 - center[1]
        n = math.hypot(mx, my) + 1e-6
        tone = 0.58 + 0.4 * ((mx / n) * light[0] + (my / n) * light[1]) + rng.uniform(-0.06, 0.06)
        draw.polygon([center, p, q], fill=float(np.clip(tone, 0.12, 1.0)))
    draw.line(pts + [pts[0]], fill=0.1, width=max(1, int(1.6 * scale)))


def rocks():
    n, ss = 64, 4
    light = (-0.6, -0.8)  # ảnh PIL: y hướng xuống → đèn trên trái
    for k in range(3):
        rng = np.random.default_rng(100 + k)
        tone = canvas(n, n, ss)
        mask = canvas(n, n, ss)
        c = n * ss / 2
        count = rng.integers(6, 9)
        angs = np.sort(rng.uniform(0, 2 * math.pi, count))
        stretch = rng.uniform(0.7, 1.0)
        pts = [(c + math.cos(a) * c * rng.uniform(0.62, 0.92), c + math.sin(a) * c * rng.uniform(0.62, 0.92) * stretch) for a in angs]
        center = (c - 0.18 * c, c - 0.2 * c)
        facet_shard(ImageDraw.Draw(tone), pts, center, light, rng, ss)
        ImageDraw.Draw(mask).polygon(pts, fill=1.0)
        save(f"FxRock{k}", to_array(mask, ss), to_array(tone, ss) / np.maximum(to_array(mask, ss), 1e-4))


def spikes():
    w, h, ss = 256, 256, 3
    tone = canvas(w, h, ss)
    mask = canvas(w, h, ss)
    td, md = ImageDraw.Draw(tone), ImageDraw.Draw(mask)
    rng = np.random.default_rng(23)
    base = h * ss * 0.94
    # (tâm x, nửa bề ngang, cao, nghiêng) — vẽ cọc thấp trước, cao sau
    spires = [(0.2, 0.09, 0.42, -0.08), (0.78, 0.1, 0.48, 0.1), (0.36, 0.11, 0.72, -0.05), (0.62, 0.11, 0.8, 0.06),
              (0.5, 0.13, 0.95, 0.0), (0.1, 0.06, 0.25, -0.12), (0.9, 0.06, 0.28, 0.12)]
    spires.sort(key=lambda s: s[2])
    for cx, hw, height, lean in spires:
        x0 = cx * w * ss
        half = hw * w * ss
        top = (x0 + lean * w * ss, base - height * (base - 4 * ss))
        mid_l = (x0 - half * 0.55 + rng.uniform(-3, 3) * ss, base - height * 0.45 * (base - 4 * ss))
        mid_r = (x0 + half * 0.5 + rng.uniform(-3, 3) * ss, base - height * 0.5 * (base - 4 * ss))
        ridge = (x0 + lean * w * ss * 0.4 + half * 0.08, base)
        left = [(x0 - half, base), mid_l, top, ridge]
        right = [ridge, top, mid_r, (x0 + half, base)]
        td.polygon(left, fill=float(rng.uniform(0.72, 0.85)))
        td.polygon(right, fill=float(rng.uniform(0.32, 0.42)))
        outline = [(x0 - half, base), mid_l, top, mid_r, (x0 + half, base)]
        td.line(outline, fill=0.1, width=int(2 * ss))
        td.line([top, ridge], fill=0.95, width=int(1 * ss))
        md.polygon(outline, fill=1.0)
    # đá vụn ở chân
    for i in range(9):
        x = rng.uniform(0.08, 0.92) * w * ss
        r = rng.uniform(6, 14) * ss
        pts = [(x + math.cos(a) * r, base - r * 0.6 + math.sin(a) * r * 0.6) for a in np.sort(rng.uniform(0, 2 * math.pi, 6))]
        facet_shard(td, pts, (x - r * 0.2, base - r * 0.8), (-0.6, -0.8), rng, ss)
        md.polygon(pts, fill=1.0)
    m = to_array(mask, ss)
    save("FxSpikes", m, to_array(tone, ss) / np.maximum(m, 1e-4))


def flame():
    w, h, ss = 128, 256, 2
    u, v = grid(w, h, ss)
    x, y = u * 2 - 1, v
    n = fbm(h * ss, w * ss, 6, 3, 41, 4)
    n2 = fbm(h * ss, w * ss, 3, 2, 43, 3)
    xw = x + (n2 - 0.5) * 0.7 * y
    width = 0.85 * np.sin(np.pi * np.clip((y + 0.12) / 1.12, 0, 1)) ** 0.8 * (1 - y) ** 0.5
    body = 1 - smoothstep(width * 0.35, width + 1e-3, np.abs(xw))
    core = 1 - smoothstep(0.0, width * 0.45 + 1e-3, np.abs(xw))
    fade = (1 - y ** 1.4) * smoothstep(0.0, 0.08, y)
    a = (body * (0.45 + 0.55 * n) + 0.5 * core * (1 - y)) * fade
    save("FxFlame", down(np.clip(a, 0, 1), ss))


# ---------------------------------------------------------------- mây, nước, độc

def bubble():
    n, ss = 64, 4
    u, v = grid(n, n, ss)
    x, y = u * 2 - 1, v * 2 - 1
    r = np.hypot(x, y)
    fill = (0.1 + 0.55 * (r / 0.88) ** 4) * (r < 0.88)
    rim = np.exp(-((r - 0.86) / 0.05) ** 2)
    hi = np.exp(-(((x + 0.33) / 0.2) ** 2 + ((y - 0.38) / 0.13) ** 2))
    hi2 = 0.6 * np.exp(-(((x - 0.42) / 0.08) ** 2 + ((y + 0.42) / 0.08) ** 2))
    a = np.clip(fill + rim + hi + hi2, 0, 1) * (1 - smoothstep(0.93, 1.0, r))
    save("FxBubble", down(a, ss))


def drop():
    w, h, ss = 64, 32, 4
    u, v = grid(w, h, ss)
    x, y = u * 2, (v - 0.5) * 1.0  # x 0..2, y -0.5..0.5 (một đơn vị = chiều cao ảnh)
    head = np.hypot(x - 1.55, y) - 0.36
    t = np.clip((x - 0.1) / 1.45, 0, 1)
    tail = np.abs(y) - 0.36 * t ** 1.2
    tail = np.where((x > 0.1) & (x < 1.55), tail, 1)
    d = np.minimum(head, tail)
    a = 1 - smoothstep(-0.03, 0.02, d)
    hi = np.exp(-(((x - 1.65) / 0.12) ** 2 + ((y - 0.15) / 0.08) ** 2))
    tone = np.clip(0.75 + 0.25 * (y + 0.5) + hi, 0, 1)
    save("FxDrop", down(a, ss), down(tone, ss))


def splat():
    n, ss = 128, 3
    u, v = grid(n, n, ss)
    x, y = u * 2 - 1, v * 2 - 1
    r = np.hypot(x, y)
    th = np.arctan2(y, x)
    rng = np.random.default_rng(3)
    edge_noise = fbm(32, 256, 1, 10, 13, 3, wrap_x=True)
    rad = 0.34 * (0.8 + 0.5 * sample(edge_noise, (th + math.pi) / (2 * math.pi) * 256, np.full_like(th, 16), wrap_x=True))
    blob = 1 - smoothstep(rad - 0.02, rad + 0.01, r)
    a = blob.copy()
    for k in range(8):  # gai bắn toé
        ang = rng.uniform(0, 2 * math.pi)
        length = rng.uniform(0.55, 0.85)
        dth = np.angle(np.exp(1j * (th - ang)))
        t = np.clip(r / length, 0, 1)
        across = np.abs(np.sin(dth)) * r
        spike = (across < 0.05 * (1 - t) ** 1.2 + 0.004) & (np.cos(dth) > 0) & (r < length)
        a = np.maximum(a, spike.astype(np.float64))
    for k in range(12):  # giọt văng
        ang = rng.uniform(0, 2 * math.pi)
        dist = rng.uniform(0.5, 0.88)
        rr = rng.uniform(0.025, 0.07)
        d = np.hypot(x - math.cos(ang) * dist, y - math.sin(ang) * dist)
        a = np.maximum(a, 1 - smoothstep(rr - 0.01, rr + 0.005, d))
    rim = smoothstep(rad - 0.12, rad - 0.02, r) * blob
    a = a * (0.78 + 0.22 * rim)
    tone = 0.8 + 0.2 * rim
    save("FxSplat", down(np.clip(a, 0, 1), ss), down(tone, ss))


# ---------------------------------------------------------------- tơ nhện

def web():
    n, ss = 512, 3
    size = n * ss
    img = canvas(n, n, ss)
    draw = ImageDraw.Draw(img)
    rng = np.random.default_rng(21)
    c = size / 2
    R = size / 2 * 0.95
    spokes = 13
    angs = np.sort((np.arange(spokes) + rng.uniform(-0.3, 0.3, spokes)) * 2 * math.pi / spokes)
    ends = [R * rng.uniform(0.86, 1.0) for _ in range(spokes)]
    bend = rng.uniform(-0.05, 0.05, spokes)

    def spoke_point(i, t):
        a = angs[i] + bend[i] * math.sin(t * math.pi)
        return c + math.cos(a) * ends[i] * t, c + math.sin(a) * ends[i] * t

    # nan (dày)
    for i in range(spokes):
        pts = [spoke_point(i, t) for t in np.linspace(0.03, 1, 12)]
        draw.line(pts, fill=1.0, width=int(2.4 * ss), joint="curve")
    # khung ngoài nối đầu nan, võng vào trong
    for i in range(spokes):
        j = (i + 1) % spokes
        p, q = spoke_point(i, 0.97), spoke_point(j, 0.97)
        mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
        sag = 0.1
        ctrl = (mx + (c - mx) * sag, my + (c - my) * sag)
        pts = [((1 - t) ** 2 * p[0] + 2 * (1 - t) * t * ctrl[0] + t * t * q[0],
                (1 - t) ** 2 * p[1] + 2 * (1 - t) * t * ctrl[1] + t * t * q[1]) for t in np.linspace(0, 1, 12)]
        draw.line(pts, fill=0.95, width=int(2.0 * ss), joint="curve")
    # sợi xoắn: từ trong ra ngoài, giữa hai nan võng vào tâm
    t = 0.14
    i = 0
    dews = []
    while t < 0.93:
        j = (i + 1) % spokes
        t2 = t + 0.052 / spokes * (1 + 0.6 * rng.random())
        p, q = spoke_point(i, t), spoke_point(j, t2)
        mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
        sag = 0.06 + 0.05 * rng.random()
        ctrl = (mx + (c - mx) * sag, my + (c - my) * sag)
        pts = [((1 - s) ** 2 * p[0] + 2 * (1 - s) * s * ctrl[0] + s * s * q[0],
                (1 - s) ** 2 * p[1] + 2 * (1 - s) * s * ctrl[1] + s * s * q[1]) for s in np.linspace(0, 1, 8)]
        if rng.random() > 0.04:  # vài đoạn đứt cho tự nhiên
            draw.line(pts, fill=0.85, width=int(1.4 * ss), joint="curve")
            if rng.random() < 0.18:
                dews.append(pts[rng.integers(1, 7)])
        t, i = t2, j
    # lưới rối ở tâm
    for k in range(18):
        a1, a2 = rng.uniform(0, 2 * math.pi, 2)
        r1, r2 = rng.uniform(0.0, 0.12, 2) * R
        draw.line([(c + math.cos(a1) * r1, c + math.sin(a1) * r1), (c + math.cos(a2) * r2, c + math.sin(a2) * r2)], fill=0.9, width=int(1.2 * ss))
    for x, y in dews:
        rr = rng.uniform(2.0, 3.4) * ss
        draw.ellipse([x - rr, y - rr, x + rr, y + rr], fill=1.0)
    lines = np.asarray(img, np.float32)
    glow = blur(lines, 3 * ss)
    a = np.clip(lines + glow * 0.9, 0, 1)
    save("FxWeb", down(a, ss)[::-1])


def cocoon():
    w, h, ss = 192, 300, 2
    u, v = grid(w, h, ss)
    x, y = (u - 0.5) * 2, (v - 0.5) * 2
    noise = fbm(h * ss, w * ss, 10, 6, 61, 4)
    egg = x * x / (0.78 ** 2 * (1 - 0.16 * y)) + y * y / 0.9 ** 2
    body = 1 - smoothstep(0.88, 1.0, egg + (noise - 0.5) * 0.1)
    nz = np.sqrt(np.clip(1 - egg, 0, 1))
    light = np.clip(0.5 + 0.35 * (-0.55 * x + 0.5 * y) + 0.4 * nz, 0, 1)
    base = 0.42 + 0.4 * light
    # sợi tơ quấn: đường cong chéo ôm quanh quả trứng (chỉ thấy nửa trước)
    img = canvas(w, h, ss)
    draw = ImageDraw.Draw(img)
    rng = np.random.default_rng(8)
    W, H = w * ss, h * ss
    for k in range(95):
        y0 = rng.uniform(-1.05, 1.05)
        slope = rng.choice([-1, 1]) * rng.uniform(0.25, 0.75)
        bow = rng.uniform(0.08, 0.2)
        pts = []
        for t in np.linspace(-1, 1, 24):
            yy = y0 + slope * t - bow * (1 - t * t)
            half = 0.78 * math.sqrt(max(0.0, 1 - (yy / 0.92) ** 2)) * (1 - 0.08 * yy)
            xx = t * half
            pts.append(((xx / 2 + 0.5) * W, (0.5 - yy / 2) * H))
        draw.line(pts, fill=float(rng.uniform(0.55, 1.0)), width=int(rng.uniform(1.0, 2.2) * ss), joint="curve")
    # sợi lòng thòng ở hai đầu
    for k in range(14):
        top = k < 7
        x0 = (0.5 + rng.uniform(-0.16, 0.16)) * W
        y0 = (0.07 if top else 0.93) * H
        dy = rng.uniform(0.03, 0.07) * H * (-1 if top else 1)
        draw.line([(x0, y0 - dy * 0.4), (x0 + rng.uniform(-10, 10) * ss, y0 + dy)], fill=0.7, width=int(1.1 * ss))
    strands = np.asarray(img, np.float32)[::-1]
    on_body = strands * body
    tone = np.clip(base * (1 - 0.6 * on_body) + on_body * (0.55 + 0.5 * light), 0, 1)
    tone = np.clip(tone - 0.22 * (1 - nz) ** 3 * body, 0, 1)
    a = np.maximum(body * 0.97, strands * 0.8)
    tone = np.where(body > 0.02, tone, 0.85)
    save("FxCocoon", down(a, ss), down(tone, ss))


# ---------------------------------------------------------------- nhện con

def spiderling():
    """Nhện con (chiêu cuối nhện) nhìn ngang, mặt hướng phải, 2 dáng bước (bò thì đổi qua lại): thân tròn bóng tím (sáng
    hơn nhện mẹ đen tím), 8 chân gối cao, viền sáng hồng trên lưng, vạch hồng phát sáng trên bụng, mắt hồng, viền tối mỏng
    quanh cả con. Hình gọn, chân rõ để nhỏ cỡ nào cũng nhận ra là con nhện. Ảnh màu (không tô màu lúc chạy). In ra chỗ cụm
    mắt (CombatFx.spiderlingEyes)."""
    w, h, ss = 236, 144, 3
    u, v = grid(w, h, ss)
    x, y = u * w - 4, v * h                  # toạ độ pixel ảnh xuất (lệch 4 cho chân sau), y hướng lên
    px = 1.0 / ss

    def ellipse(cx, cy, rx, ry, rot=0.0):
        c, s_ = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        lx, ly = (x - cx) * c + (y - cy) * s_, -(x - cx) * s_ + (y - cy) * c
        q = np.hypot(lx / rx, ly / ry)
        return (q - 1) * min(rx, ry), lx / rx, ly / ry, q

    def capsule(a, b, ra, rb):
        ax, ay = a
        bx, by = b
        dx, dy = bx - ax, by - ay
        t = np.clip(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy), 0, 1)
        qx, qy = x - (ax + dx * t), y - (ay + dy * t)
        dist = np.hypot(qx, qy)
        return dist - (ra + (rb - ra) * t), qy / np.maximum(dist, 1e-4)

    def cover(d):
        return 1 - smoothstep(-px, px, d)

    def leg(hip, knee, foot, scale):
        ankle = (knee[0] + (foot[0] - knee[0]) * 0.55 + (foot[0] - hip[0]) * 0.08, knee[1] + (foot[1] - knee[1]) * 0.55 + 5)
        parts = [capsule(hip, knee, 6.4 * scale, 5.2 * scale), capsule(knee, ankle, 4.8 * scale, 3.6 * scale),
                 capsule(ankle, foot, 3.2 * scale, 1.3 * scale)]
        d = np.minimum(np.minimum(parts[0][0], parts[1][0]), parts[2][0])
        ny = np.choose(np.argmin(np.stack([p[0] for p in parts]), 0), [p[1] for p in parts])
        joints = sum(np.exp(-(np.hypot(x - jx, y - jy) / (3.2 * scale)) ** 2) for jx, jy in (knee, ankle))
        return d, ny, joints

    # Nhện con mới nở: tím sáng hơn nhện mẹ (đen tím) cho nổi trên nền hang và trên người mẹ
    dark = np.array([0.2, 0.08, 0.3])
    violet = np.array([0.82, 0.45, 1.0])
    pink = np.array([1.0, 0.42, 0.9])
    light = np.array([-0.45, 0.65, 0.62])
    light /= np.linalg.norm(light)

    hips = [(154, 50), (146, 47), (138, 47), (130, 50)]
    knees = [(188, 100), (166, 112), (112, 110), (76, 100)]
    feet = [214, 180, 98, 38]
    eye_spots = [(166, 70, 5.2), (157, 74, 4.6), (172, 63, 3.4), (149, 77, 3.2)]

    for frame, name in enumerate(["FxSpiderling", "FxSpiderlingStep"]):
        rgb = np.zeros((h * ss, w * ss, 3))
        alpha = np.zeros((h * ss, w * ss))
        union = np.full((h * ss, w * ss), 1e3)

        def paint(a, color):
            nonlocal rgb, alpha
            rgb = rgb * (1 - a[..., None]) + color * a[..., None]
            alpha = alpha + a * (1 - alpha)

        def legs(far, which):
            nonlocal union
            for i in which:
                swing = (1 if (i % 2 == 0) != far else -1) * (1 if frame == 0 else -1)   # bước chéo: 1, 3 gần + 2, 4 xa cùng nhịp
                lift = 5 if swing > 0 else 0
                sx = 7 if far else 0
                hip = (hips[i][0] - sx, hips[i][1] + (3 if far else 0))
                knee = (knees[i][0] - sx + swing * 7, knees[i][1] - (7 if far else 0) + lift)
                foot = (feet[i] - sx * 1.6 + swing * 14, 7 + lift + (3 if far else 0))
                d, ny, joints = leg(hip, knee, foot, 0.85 if far else 1.0)
                a = cover(d)
                col = dark * (0.7 if far else 1.0) \
                    + ((violet * 0.6 + pink * 0.4) * (0.45 if far else 1.0))[None, None] * (np.clip(ny + 0.3, 0, 1) ** 2)[..., None] \
                    * (1 - smoothstep(0.5, 2.8, -d))[..., None] \
                    + (pink * 0.25)[None, None] * np.clip(joints, 0, 1)[..., None] * (0 if far else 1)
                paint(a, col)
                union = np.minimum(union, d)

        legs(True, range(4))
        legs(False, (2, 3))  # chân sau phía gần: thò ra dưới bụng (bụng che gốc chân cho hình gọn)

        # Bụng (sau) + đầu ngực (trước): khối tròn bóng, ánh sáng trên-trái-trước
        for cx, cy, rx, ry, rot in [(84, 64, 52, 40, -12), (144, 58, 33, 26, 4)]:
            d, lx, ly, q = ellipse(cx, cy, rx, ry, rot)
            nz = np.sqrt(np.clip(1 - q * q, 0, 1))
            n = np.stack([lx, ly, nz], -1)
            n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-4)
            lam = np.clip((n * light).sum(-1), 0, 1)
            spec = lam ** 40
            rim = np.clip(ly + 0.2, 0, 1) ** 1.5 * (1 - smoothstep(0, 3.0, -d))      # viền hồng trên lưng
            col = dark[None, None] + violet[None, None] * (lam ** 4)[..., None] * 0.75 \
                + np.array([0.95, 0.85, 1.0])[None, None] * spec[..., None] * 0.8 + pink[None, None] * rim[..., None]
            paint(cover(d), col)
            union = np.minimum(union, d)

        # Vạch hồng phát sáng dọc lưng bụng (to dần về phía đầu)
        for k, (cx, cy, r) in enumerate([(60, 92, 4.2), (76, 98, 5.2), (94, 100, 6.0)]):
            d, _, _, q = ellipse(cx, cy, r * 1.25, r * 0.75, -14)
            glow = np.exp(-np.clip(d, 0, None) ** 2 / 10) * (union < 0)
            paint(np.clip(glow * 0.6, 0, 1), pink)
            paint(cover(d), np.array([1.0, 0.75, 0.97]))

        legs(False, (0, 1))

        # Nanh, chân xúc giác
        for a_, b_, ra, rb in [((166, 46), (176, 30), 4.0, 1.6), ((171, 50), (186, 40), 3.0, 2.0), ((186, 40), (190, 28), 2.0, 1.2)]:
            d, ny = capsule(a_, b_, ra, rb)
            paint(cover(d), dark + violet * 0.5 * np.clip(ny, 0, 1)[..., None] ** 2)
            union = np.minimum(union, d)

        # Mắt hồng sáng: quầng hồng + tròng hồng + lõi trắng
        for cx, cy, r in eye_spots:
            dist = np.hypot(x - cx, y - cy)
            paint(np.clip(np.exp(-(dist / (r * 1.9)) ** 2) * 0.55, 0, 1), pink)
            paint(cover(dist - r), pink)
            paint(cover(dist - r * 0.45), np.array([1.0, 0.92, 1.0]))

        # Viền tối mỏng quanh cả con cho tách khỏi nền
        outline = cover(union - 2.2) * (1 - alpha)
        rgb = rgb + np.array([0.03, 0.01, 0.06]) * outline[..., None]
        alpha = alpha + outline
        premul = np.dstack([rgb, alpha])
        small = down(premul, ss)
        a = small[..., 3]
        col = np.where(a[..., None] > 1e-4, small[..., :3] / np.maximum(a, 1e-4)[..., None], 0)
        img = np.dstack([np.clip(col, 0, 1), np.clip(a, 0, 1)])[::-1]
        OUT.mkdir(parents=True, exist_ok=True)
        Image.fromarray((img * 255 + 0.5).astype(np.uint8), "RGBA").save(OUT / f"{name}.png", optimize=True)
        print("ghi", name, w, "x", h)

    ex = sum(e[0] for e in eye_spots[:2]) / 2
    ey = sum(e[1] for e in eye_spots[:2]) / 2
    print(f"  mắt (tính từ tâm, theo bề ngang ảnh): ({(ex + 4) / w - 0.5:.3f}, {(ey - h / 2) / w:.3f})")


if __name__ == "__main__":
    jobs = [spark, slash, ring, crack, rocks, spikes, flame, bubble, drop, splat, web, cocoon, spiderling]
    wanted = sys.argv[1:]
    for job in jobs:
        if not wanted or job.__name__ in wanted:
            job()
