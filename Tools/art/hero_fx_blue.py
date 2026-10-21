"""Hiệu ứng chiêu của BLUE (lưỡi hái lửa xanh), cắt thẳng từ 9 tấm chiêu Docs/nhan_vat_moi/blue/{basic_attack,skill,special_skill}.

Mỗi chiêu lấy hình từ CHÍNH tấm của nó (không mượn hình chiêu khác), cắt thành nhiều lớp nhỏ để code diễn riêng từng lớp:
cung chém / vòng xoáy (thân màu), lõi trắng (lớp _Core, vẽ cộng sáng), đuôi mảnh, tàn lửa, đá vụn, bụi, nứt đất, và nửa
TRƯỚC / nửa SAU quanh người (lớp _Front vẽ trước nhân vật, _Back vẽ sau lưng).

Cách cắt một mảng (PIECES, toạ độ px trên tấm 2172 x 724):
  - tách nền xanh than: "bright" = chỉ giữ phần sáng hơn nền (lửa, quầng: color-to-alpha, màu chia lại cho alpha) — áo đen,
    tóc, giáp tối của nhân vật tự biến mất; "full" = giữ cả phần tối hơn nền (đá đen, khói, xích sắt);
  - keep  = các đa giác vùng giữ (ngoài vùng giữ: bỏ); cut = đa giác thân người / lưỡi hái cần bỏ, kèm ngưỡng sáng: dải lửa
    vắt NGANG QUA trước thân (sáng hơn ngưỡng) vẫn giữ; metal = vùng xoá điểm thép / da / xám (ít màu, ngả ấm);
  - mép bị cắt (vùng bỏ, mép khung) KHÔNG cắt thẳng: "vuốt nhọn" — gần chỗ cắt phần mờ biến mất trước, nét lửa thu mảnh dần
    thành đuôi nhọn (ăn mòn theo alpha), rồi mới mờ hẳn;
  - patch = vá phần bị che bằng chính hoạ tiết của hiệu ứng: xoay quanh tâm vòng (vòng elip: xoay trong không gian elip), dời
    (xích / cột dọc), soi gương; split = tách TRƯỚC / SAU theo bóng người trên tấm (bóng người: đa giác body; dải sáng vắt qua
    thân = trước, phần còn lại = sau, chỗ người che ở lớp sau được vá);
  - core = thêm lớp lõi trắng nóng (_Core) để vẽ cộng sáng riêng.
Ảnh ra cắt sát; màu của điểm trong suốt loang từ điểm đặc gần nhất (không viền đen khi lọc ảnh trên nền sáng).
Mỗi mảng có pivot (điểm neo) + các điểm mốc (ngực, mũi lưỡi hái, chân…) ghi vào manifest.json: Editor/HeroFxImporter_Blue.cs
đặt pivot sprite theo đó, BlueFx đọc điểm mốc (unit, so với pivot) để đặt hiệu ứng theo đường lưỡi hái thật.
134 px = 1 unit (BLUE cao ~268 px trên tấm, 2 unit trong game).

Cũng vẽ lại 3 biểu tượng nút chiêu của BLUE (tròn 256 px) từ khung chính của tấm chiêu "thường" mỗi nút.

Chạy:  python3 Tools/art/hero_fx_blue.py [thư mục Docs] [tên mảng…]   (mặc định: Docs cạnh Assets, mọi mảng + biểu tượng)
       python3 Tools/art/hero_fx_blue.py --preview out.png [Docs]   (bảng mọi mảng trên nền tối + nền sáng, để soát mép)
Ra:    Assets/_Project/Resources/HeroFx/Blue/*.png + manifest.json (nạp lúc chạy: Resources.Load, BlueFx.cs)
       Assets/_Project/Art/UI/Skill_Blue_1..3.png (giữ nguyên file / GUID cũ)
"""
import json
import sys
import unicodedata
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "Assets/_Project/Art/UI"
ICON = 256
PPU = 134.0
ARGS = [a for a in sys.argv[1:] if not a.startswith("--")]
PREVIEW = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
if PREVIEW in ARGS:
    ARGS.remove(PREVIEW)
DOCS = Path(ARGS[0]) if ARGS and Path(ARGS[0]).is_dir() else ROOT / "Docs"
if ARGS and Path(ARGS[0]).is_dir():
    ARGS = ARGS[1:]
SHEETS = DOCS / "nhan_vat_moi" / "blue"
OUT = ROOT / "Assets/_Project/Resources/HeroFx/Blue"

BASIC = "basic_attack/Đánh thường"
RISE = "basic_attack/Đánh thường hất tung"
DASH = "basic_attack/Đánh thường áp sát"
WHIRL = "skill/Dùng chiêu"
RANGED = "skill/Dùng chiêu tầm xa"
GUARD = "skill/Dùng chiêu thủ"
STORM = "special_skill/kĩ năng đặc biệt"
TIDE = "special_skill/Kĩ năng đặc biệt tầm xa"
PRISON = "special_skill/Kĩ năng đặc biệt áp chế"

LUM = np.array([0.2126, 0.7152, 0.0722], np.float32)

# ---------------------------------------------------------------------------------------------------------------------
# Thư viện cắt
# ---------------------------------------------------------------------------------------------------------------------

_cache = {}


def sheet(name):
    if name not in _cache:
        path = SHEETS / f"{name}.png"
        if not path.exists():  # tên file macOS có thể lưu dạng NFD
            for p in path.parent.iterdir():
                if unicodedata.normalize("NFC", p.name) == unicodedata.normalize("NFC", path.name):
                    path = p
                    break
        rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.float32) / 255
        bg = np.median(rgb[:12].reshape(-1, 3), axis=0)  # nền phẳng, chỉ nhiễu ±1
        _cache[name] = (rgb, bg)
    return _cache[name]


def unblend(rgb, bg, dark=True, eps=6 / 255, dark_gain=1.7, bright=True):
    """RGB trên nền bg → RGBA (màu đã chia alpha). Sáng hơn nền: color-to-alpha; dark: thêm phần tối hơn nền (đá, khói)."""
    up = np.clip(rgb - bg - eps, 0, None) / np.maximum(1 - bg, 1e-3)
    a = up.max(axis=-1) if bright else np.zeros(rgb.shape[:2], np.float32)
    if dark:
        lum = rgb @ LUM
        lum_bg = float(bg @ LUM)
        a = np.maximum(a, np.clip((lum_bg - lum - eps * 0.5) / max(lum_bg, 1e-3) * dark_gain, 0, 1))
    a = np.clip(a, 0, 1)
    safe = np.maximum(a, 1e-4)[..., None]
    fg = np.clip((rgb - (1 - a[..., None]) * bg) / safe, 0, 1)
    fg = np.where(a[..., None] > 1e-4, fg, 0)
    return np.dstack([fg, a]).astype(np.float32)


def polys(shape, items, x0, y0):
    """Mặt nạ (bool) các đa giác (toạ độ tấm) trong khung bắt đầu ở (x0, y0)."""
    img = Image.new("L", (shape[1], shape[0]), 0)
    d = ImageDraw.Draw(img)
    for p in items:
        d.polygon([(x - x0, y - y0) for x, y in p], fill=255)
    return np.asarray(img) > 127


def ellipse(cx, cy, rx, ry, n=56, a0=0.0, a1=360.0):
    """Đa giác elip (hoặc cung a0..a1 độ, nối về tâm)."""
    pts = [(cx + rx * np.cos(np.deg2rad(a)), cy + ry * np.sin(np.deg2rad(a))) for a in np.linspace(a0, a1, n)]
    if a1 - a0 < 360:
        pts.append((cx, cy))
    return pts


def hsv_parts(rgb):
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-3)
    return mx, mn, sat


def bad_mask(rgb, kind):
    """Điểm không phải hiệu ứng lọt vào vùng cắt:
       metal = thép bạc / giáp xám / da (ít màu hoặc ngả ấm; lõi lửa trắng rực thì giữ);
       dark  = metal + đen trung tính (lưỡi hái, áo, tóc đen — bóng lửa xanh đậm thì giữ vì rất nhiều màu);
       dummy = hình nộm xám (xám ánh lam nhạt) + metal; xích xanh rực / lõi trắng thì giữ."""
    mx, mn, sat = hsv_parts(rgb)
    lum = rgb @ LUM
    r, b = rgb[..., 0], rgb[..., 2]
    warm = (r >= b - 0.03) & (mx > 0.12) & (mn < 0.8)
    grey = (sat < 0.2) & (lum < 0.8) & (mx > 0.1)
    m = warm | grey
    if kind == "dark":
        m |= (mx < 0.34) & (sat < 0.6)
    elif kind == "dummy":
        m |= ((sat < 0.45) & (lum < 0.84) & (b - r < 0.32) & (mx > 0.1)) | ((sat < 0.24) & (b - r < 0.2) & (mx > 0.3)) | (lum < 0.3)
    return m


def blue_paint(rgb, lum_min):
    """Nét lửa xanh sáng (vắt qua trước thân): đủ sáng, ngả lam."""
    lum = rgb @ LUM
    return (lum > lum_min) & (rgb[..., 2] > rgb[..., 0] + 0.12)


def sample(img, ys, xs):
    """Lấy mẫu song tuyến ảnh nhiều kênh tại toạ độ thực (ngoài ảnh: kẹp mép)."""
    h, w = img.shape[:2]
    ys = np.clip(ys, 0, h - 1)
    xs = np.clip(xs, 0, w - 1)
    out = np.empty(np.shape(ys) + (img.shape[2],), np.float32)
    for c in range(img.shape[2]):
        out[..., c] = ndi.map_coordinates(img[..., c], [ys, xs], order=1, mode="nearest")
    return out


def sources(spec, ys, xs, x0, y0):
    """Các điểm nguồn ứng viên (lần lượt) cho điểm cần vá:
       rot=(cx, cy, k, [độ…]) xoay quanh tâm vòng (k = ry/rx: vòng elip xoay trong không gian elip);
       shift=[(dx, dy)…] dời; mirror=cx soi gương ngang."""
    cands = []
    if "rot" in spec:
        cx, cy, k, angles = spec["rot"]
        cx -= x0
        cy -= y0
        for deg in angles:
            r = np.deg2rad(deg)
            dx, dy = xs - cx, (ys - cy) / k
            cands.append((cy + (dx * np.sin(r) + dy * np.cos(r)) * k, cx + dx * np.cos(r) - dy * np.sin(r)))
    for dx, dy in spec.get("shift", []):
        cands.append((ys + dy, xs + dx))
    if "mirror" in spec:
        cands.append((ys.astype(np.float32), 2 * (spec["mirror"] - x0) - xs))
    return cands


def heal(img, bad, spec, x0, y0, feather=3.0):
    """Vá các điểm bad của img (nhiều kênh, sửa tại chỗ) bằng hoạ tiết chính hiệu ứng ở chỗ khác (sources). Vùng vá theo
    đúng hình vật bị xoá (không phải khung chữ nhật), mép hoà dần feather px. Điểm không tìm được nguồn: loang màu xung
    quanh vào."""
    if not bad.any():
        return
    grow = ndi.binary_dilation(bad, iterations=int(np.ceil(feather * 1.5)))
    ys, xs = np.nonzero(grow)
    h, w = bad.shape
    done = np.zeros(len(ys), bool)
    sy_all = np.zeros(len(ys), np.float32)
    sx_all = np.zeros(len(ys), np.float32)
    for sy, sx in sources(spec, ys, xs, x0, y0):
        iy = np.clip(np.round(sy).astype(int), 0, h - 1)
        ix = np.clip(np.round(sx).astype(int), 0, w - 1)
        ok = (sy >= 0) & (sy < h) & (sx >= 0) & (sx < w) & ~grow[iy, ix] & ~done
        sy_all[ok], sx_all[ok] = sy[ok], sx[ok]
        done |= ok
    vals = sample(img, sy_all, sx_all)
    wgt = np.clip(ndi.gaussian_filter(bad.astype(np.float32), feather) * 2.2, 0, 1)
    wgt = np.maximum(wgt, bad)[ys, xs]
    sel = done
    img[ys[sel], xs[sel]] = img[ys[sel], xs[sel]] * (1 - wgt[sel, None]) + vals[sel] * wgt[sel, None]
    left = np.zeros_like(bad)
    left[ys[~done], xs[~done]] = True
    left &= bad
    if left.any():
        diffuse(img, left)


def diffuse(img, hole, steps=300):
    """Loang màu từ các điểm xung quanh vào vùng hole (chỉ dùng cho lỗ nhỏ còn sót)."""
    filled = np.where(hole[..., None], 0, img)
    weight = (~hole).astype(np.float32)
    for _ in range(steps):
        num = ndi.uniform_filter(filled * weight[..., None], size=(3, 3, 1))
        den = ndi.uniform_filter(weight, size=3)
        g = hole & (den > 1e-4) & (weight < 1)
        if not g.any():
            break
        filled[g] = num[g] / den[g][:, None]
        weight[g] = 1
    img[hole] = filled[hole]


def hcross(pre, hole):
    """Lấp lỗ (do người che) trong lớp RGBA nhân trước alpha: mỗi hàng, dải lửa hai bên lỗ chạy tiếp vào trong — soi gương
    dải ngay bên trái và ngay bên phải lỗ qua mép lỗ (liền mạch ở mép), hoà chéo dần từ trái sang phải (vòng lốc nằm
    ngang nối liền, không lộ đường ghép)."""
    h, w = hole.shape
    out = pre.copy()
    for y in range(h):
        row = hole[y]
        if not row.any():
            continue
        idx = np.nonzero(row)[0]
        splits = np.nonzero(np.diff(idx) > 1)[0]
        for run in np.split(idx, splits + 1):
            L, R = run[0], run[-1]
            n = R - L + 1
            xs = np.arange(L, R + 1)
            t = (xs - L + 0.5) / n
            t = t * t * (3 - 2 * t)
            lx, rx = 2 * L - 1 - xs, 2 * R + 1 - xs
            lv = pre[y, np.clip(lx, 0, w - 1)] * (lx >= 0)[:, None]
            rv = pre[y, np.clip(rx, 0, w - 1)] * (rx < w)[:, None]
            if L == 0:
                lv = rv
            if R == w - 1:
                rv = lv
            out[y, xs] = lv * (1 - t)[:, None] + rv * t[:, None]
    # dọc theo cột cũng làm mượt nhẹ trong lỗ (các hàng lấp độc lập nhau)
    sm = ndi.gaussian_filter(out, (1.5, 0.5, 0))
    return np.where(hole[..., None], sm, out)


def taper(a, removed, length, seed=0):
    """Gần vùng bị bỏ: phần mờ biến mất trước, nét lửa thu mảnh dần thành đuôi nhọn rồi mới mờ hẳn; đường mờ uốn lượn theo
    nhiễu (không thành mép thẳng / góc vuông như vết cắt)."""
    if not removed.any() or length <= 0:
        return np.where(removed, 0, a)
    d = ndi.distance_transform_edt(~removed)
    rng = np.random.default_rng(seed)
    noise = ndi.gaussian_filter(rng.standard_normal(a.shape).astype(np.float32), max(2.0, length / 4))
    noise /= max(float(np.abs(noise).max()), 1e-6)
    t = np.clip((d - length * 0.15 + noise * length * 0.55) / length, 0, 1)
    thr = (1 - t) * 0.8
    smooth = t * t * (3 - 2 * t)
    out = np.clip((a - thr) / np.maximum(1 - thr, 1e-3), 0, 1) * smooth
    return np.where(removed, 0, np.minimum(out, a)).astype(np.float32)


def drop_small(a, min_area):
    labels, n = ndi.label(a > 0.06)
    if not n:
        return a
    sizes = ndi.sum(np.ones_like(labels), labels, index=np.arange(1, n + 1))
    small = np.isin(labels, 1 + np.nonzero(sizes < min_area)[0])
    grow = ndi.binary_dilation(small, iterations=2) & ~ndi.binary_dilation(~small & (labels > 0), iterations=1)
    a = a.copy()
    a[grow | small] = 0
    return a


def bleed(rgba):
    """Điểm trong suốt lấy màu điểm đặc gần nhất (lọc ảnh không kéo viền đen / xanh than vào mép)."""
    solid = rgba[..., 3] > 0.02
    if not solid.any():
        return rgba
    idx = ndi.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    rgb = rgba[..., :3][idx[0], idx[1]]
    out = rgba.copy()
    out[..., :3] = np.where(solid[..., None], rgba[..., :3], rgb)
    return out


def core_of(rgba, lo=0.7, hi=0.93):
    """Lớp lõi trắng nóng: điểm cả 3 kênh đều cao (trắng xanh), màu trắng ánh lam."""
    mn = rgba[..., :3].min(-1)
    w = np.clip((mn - lo) / (hi - lo), 0, 1)
    w = w * w * (3 - 2 * w)
    a = ndi.gaussian_filter(rgba[..., 3] * w, 0.8)
    out = np.zeros_like(rgba)
    out[..., 0], out[..., 1], out[..., 2] = 0.82, 0.95, 1.0
    out[..., 3] = a
    return out


def soft(mask, sigma, gain=1.5):
    return np.clip(ndi.gaussian_filter(mask.astype(np.float32), sigma) * gain, 0, 1)


def cut(name, spec):
    """Cắt một mảng → dict(tên → RGBA float) (một mảng, hoặc _Front / _Back, và _Core nếu có)."""
    rgb_all, bg = sheet(spec["sheet"])
    x0, y0, x1, y1 = spec["box"]
    rgb = rgb_all[y0:y1, x0:x1].copy()
    h, w = rgb.shape[:2]
    shape = (h, w)

    # 1. Vật lọt vào (lưỡi hái, giày, hình nộm): vá bằng hoạ tiết hiệu ứng ở chỗ khác, theo đúng hình vật (trước tách nền)
    for p in spec.get("heal", []):
        region = polys(shape, [p["poly"]], x0, y0)
        bad = region & ndi.binary_dilation(bad_mask(rgb, p.get("kind", "dark")), iterations=p.get("grow", 2))
        heal(rgb, bad, p, x0, y0, p.get("feather", 3.0))

    mode = spec.get("mode", "bright")
    rgba = unblend(rgb, bg, dark=mode in ("full", "dark"), bright=mode != "dark", dark_gain=spec.get("dark_gain", 1.7))
    a = rgba[..., 3]
    if spec.get("fill"):
        # lỗ tối nằm lọt giữa vùng sáng (đá đen giữa vụ nổ): đặc lại, giữ màu tranh
        solid = ndi.binary_fill_holes(ndi.binary_closing(a > 0.35, iterations=2))
        holes = solid & (a < 0.9)
        rgba[holes, :3] = rgb[holes]
        a[holes] = np.maximum(a[holes], 0.95)

    # 2. Vùng bỏ (cắt): ngoài keep, trong cut (trừ nét lửa sáng vắt qua nếu có ngưỡng). Mép bỏ được vuốt nhọn (taper)
    removed = np.zeros(shape, bool)
    if spec.get("keep"):
        removed |= ~polys(shape, spec["keep"], x0, y0)
    for item in spec.get("cut", []):
        poly, lum_min = item if isinstance(item, tuple) else (item, None)
        m = polys(shape, [poly], x0, y0)
        if lum_min is not None:
            m &= ~ndi.binary_opening(blue_paint(rgb, lum_min), iterations=1)
        removed |= m
    # 3. Điểm thép / xám / hình nộm còn sót (không vá): xoá từng điểm, mép mềm
    junk = np.zeros(shape, bool)
    for item in spec.get("metal", []):
        poly, kind = item if isinstance(item, tuple) else (item, "metal")
        junk |= polys(shape, [poly], x0, y0) & ndi.binary_dilation(bad_mask(rgb, kind), iterations=spec.get("metal_grow", 1))
    if junk.any():
        a = a * (1 - soft(junk, 0.8, 1.6))
    # mép khung (trừ phía "open": phía đó nối tiếp mảng khác) cũng vuốt nhọn
    border = np.zeros(shape, bool)
    border[0, :] = border[-1, :] = True
    open_sides = spec.get("open", "")
    if "l" not in open_sides:
        border[:, 0] = True
    if "r" not in open_sides:
        border[:, -1] = True
    a = taper(a, removed | border, spec.get("taper", 18), seed=len(name) * 7 + x0)
    seam = spec.get("seam", 0)
    if seam:
        # thân nối được: đầu trái hiện dần (smoothstep) bấy nhiêu px, đầu phải đặc. Đoạn sau đặt chồng lên đuôi đoạn trước đúng
        # chừng đó và vẽ đè lên: hoà chéo liền mạch, không lộ mối nối
        ramp = np.clip((np.arange(w, dtype=np.float32) + 0.5) / seam, 0, 1)
        a = a * (ramp * ramp * (3 - 2 * ramp))[None, :]
    if spec.get("min_area"):
        a = drop_small(a, spec["min_area"])
    rgba[..., 3] = a * spec.get("alpha", 1.0)

    outs = {}
    split = spec.get("split")
    if split:
        # TRƯỚC = dải sáng vắt qua thân (trong bóng người) + front; SAU = phần còn lại, chỗ người che lấp bằng dải hai bên
        sil = polys(shape, split.get("silhouette", []), x0, y0)
        wf = soft(sil, 2.0)
        if split.get("front"):
            wf = np.maximum(wf, soft(polys(shape, split["front"], x0, y0), split.get("blur", 6.0), 1.2))
        front = rgba.copy()
        front[..., 3] *= wf
        back = rgba.copy()
        back[..., 3] *= 1 - wf
        if split.get("fill") and sil.any():
            hole = ndi.binary_dilation(sil, iterations=spec.get("taper", 18) + 3)
            if split.get("fill_region"):
                hole &= polys(shape, split["fill_region"], x0, y0)
            pre = np.dstack([back[..., :3] * back[..., 3:], back[..., 3:]])
            pre = hcross(pre, hole)
            al = pre[..., 3]
            col = np.where(al[..., None] > 1e-4, pre[..., :3] / np.maximum(al[..., None], 1e-4), 0)
            back = np.dstack([np.clip(col, 0, 1), al * np.where(hole, split.get("fill_alpha", 1.0), 1.0)]).astype(np.float32)
        outs[name + "_Front"] = front
        outs[name + "_Back"] = back
    else:
        outs[name] = rgba
    if spec.get("core"):
        for k in list(outs):
            outs[k + "_Core"] = core_of(outs[k], *spec.get("core_range", (0.7, 0.93)))
    return outs


def finish_piece(rgba, spec, x0, y0, pad=2):
    """Cắt sát (chừa pad), loang màu mép; trả về ảnh + (xA, yA) góc trên trái trên tấm."""
    ys, xs = np.nonzero(rgba[..., 3] > 0.012)
    if len(ys) == 0:
        raise SystemExit(f"mảng rỗng {spec}")
    yA, yB = max(0, ys.min() - pad), min(rgba.shape[0], ys.max() + pad + 1)
    xA, xB = max(0, xs.min() - pad), min(rgba.shape[1], xs.max() + pad + 1)
    out = bleed(rgba[yA:yB, xA:xB])
    flip = spec.get("flip", False)
    if flip:
        out = out[:, ::-1]
    return out, x0 + xA, y0 + yA


def to_image(rgba):
    return Image.fromarray((np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")


def manifest_entry(name, img, spec, xA, yA):
    """pivot (0..1, gốc dưới trái như Unity) + điểm mốc (unit so với pivot, y lên trên). flip: lật ngang mọi toạ độ."""
    w, h = img.size
    flip = spec.get("flip", False)

    def local(p):
        x, y = p[0] - xA, p[1] - yA
        if flip:
            x = w - x
        return x, y

    px, py = local(spec["pivot"]) if "pivot" in spec else (w / 2, h / 2)
    entry = dict(name=name, w=w, h=h, pivot=[round(px / w, 5), round(1 - py / h, 5)], points=[])
    for key, p in spec.get("points", {}).items():
        x, y = local(p)
        entry["points"].append(dict(name=key, x=round((x - px) / PPU, 4), y=round(-(y - py) / PPU, 4)))
    return entry


# ---------------------------------------------------------------------------------------------------------------------
# Các mảng (toạ độ px trên tấm). pivot = điểm neo; points = điểm mốc ghi vào manifest (chest = ngực nhân vật trên khung đó,
# tip = đầu lưỡi hái, feet = chân). Cung chém đặt ngực trùng ngực BLUE trong game → cung ôm đúng đường lưỡi hái.
# ---------------------------------------------------------------------------------------------------------------------

PIECES = {}


def piece(name, **spec):
    PIECES[name] = spec


# ===== #0 Đánh thường (tấm "Đánh thường") =====
# Khung 3: cung lửa dài ôm đường quét ngang (đuôi mảnh bên trái trên đầu → vòng phải → cuộn dưới đầu lưỡi hái). Đầu lưỡi hái
# nằm đè lên vòng phải: vá bằng đoạn cung bên cạnh (xoay quanh tâm cung) theo đúng hình lưỡi hái
piece("ReapArcA", sheet=BASIC, box=(640, 212, 1078, 512),
      cut=[([(645, 318), (700, 312), (760, 298), (790, 292), (850, 292), (905, 300), (915, 330), (925, 400), (956, 412),
             (956, 470), (930, 500), (930, 560), (645, 560)], 0.74),
           [(905, 405), (960, 400), (960, 470), (905, 470)]],
      heal=[dict(poly=[(950, 380), (1078, 380), (1078, 500), (950, 500)], rot=(862, 432, 1.0, [-20, -28, 20, -36]))],
      pivot=(848, 445), points=dict(chest=(848, 445), tip=(1040, 438)), core=True, min_area=30, taper=24)
# Khung 4: lưỡi liềm lớn (đỉnh trái → vòng phải sáng nhất → dải thấp vắt trước chân); đầu lưỡi hái vá như trên
piece("ReapArcB", sheet=BASIC, box=(1048, 222, 1548, 612),
      keep=[[(1048, 222), (1500, 222), (1500, 330), (1548, 380), (1548, 612), (1048, 612)]],
      cut=[([(1100, 330), (1160, 300), (1230, 280), (1310, 290), (1338, 330), (1338, 398), (1352, 408), (1352, 470),
             (1270, 482), (1262, 560), (1200, 572), (1110, 582), (1066, 576), (1058, 470), (1068, 360)], 0.7)],
      heal=[dict(poly=[(1300, 370), (1478, 370), (1478, 500), (1300, 500)], rot=(1268, 430, 1.0, [-22, 22, -32, 32, -44]))],
      pivot=(1268, 438), points=dict(chest=(1268, 438), tip=(1425, 440)), core=True, min_area=30, taper=22)
# Khung 1 / 6: mũi vệt quét sát đất dưới lưỡi hái (nhát thu về)
piece("ReapTip", sheet=BASIC, box=(118, 438, 382, 592),
      keep=[[(128, 562), (140, 540), (220, 534), (290, 518), (330, 498), (352, 468), (374, 446), (380, 500), (362, 542),
             (322, 570), (250, 584), (160, 578)]],
      metal=[[(118, 438), (382, 438), (382, 592), (118, 592)]],
      pivot=(178, 432), points=dict(chest=(178, 432), tip=(345, 478)), min_area=20)
# Tàn lửa bay quanh nhát chém (khung 3–4)
piece("Ember1", sheet=BASIC, box=(1056, 240, 1106, 272), pivot=(1081, 256), min_area=12, taper=6)
piece("Ember2", sheet=BASIC, box=(1118, 262, 1176, 304), pivot=(1147, 283), min_area=12, taper=6)
piece("Ember3", sheet=BASIC, box=(926, 236, 958, 268), pivot=(942, 252), min_area=12, taper=6)

# ===== #1 Đánh thường hất tung — chuỗi: quét thấp (K2) → cung hất (K3 → K4, chung điểm neo chân) → cột lửa (K5) =====
piece("RiseLow", sheet=RISE, box=(408, 518, 802, 666),
      cut=[([(430, 518), (560, 518), (662, 528), (672, 600), (640, 612), (560, 612), (480, 616), (440, 606)], 0.7)],
      metal=[([(648, 526), (782, 526), (782, 616), (648, 616)], "dark")],
      pivot=(560, 614), points=dict(chest=(575, 478), feet=(560, 614)), min_area=25)
piece("RiseArc1", sheet=RISE, box=(880, 212, 1128, 646),
      keep=[[(905, 228), (1000, 228), (1072, 266), (1114, 348), (1126, 450), (1114, 562), (1084, 636), (1010, 644),
             (996, 604), (1036, 560), (1048, 500), (1038, 420), (1006, 352), (958, 300), (905, 270)]],
      metal=[[(880, 212), (1128, 212), (1128, 646), (880, 646)]],
      pivot=(920, 634), points=dict(feet=(920, 634), chest=(925, 470)), min_area=25)
piece("Rise", sheet=RISE, box=(1176, 84, 1522, 672),
      keep=[[(1288, 96), (1382, 92), (1452, 128), (1494, 198), (1514, 300), (1508, 450), (1494, 562), (1474, 642),
             (1380, 670), (1250, 670), (1178, 662), (1186, 628), (1300, 616), (1360, 588), (1400, 520), (1418, 420),
             (1408, 300), (1378, 222), (1330, 182), (1288, 162)]],
      cut=[([(1150, 280), (1332, 250), (1372, 330), (1372, 452), (1332, 522), (1302, 636), (1220, 650), (1150, 560)], 0.7)],
      metal=[[(1176, 84), (1522, 84), (1522, 672), (1176, 672)]],
      pivot=(1262, 662), points=dict(feet=(1262, 662), chest=(1270, 420)), core=True, min_area=25)
# Khung 5: tia sáng giữa cột (đoạn dưới chân người, sạch); trong game kéo cao theo chiều dọc (tia thẳng đều)
piece("PillarBeam", sheet=RISE, box=(1690, 522, 1746, 652), keep=[[(1698, 522), (1738, 522), (1742, 652), (1694, 652)]],
      pivot=(1718, 646), min_area=20, taper=10)
# Khung 5: lửa hai bên + chân cột (bỏ người, lưỡi hái + cung trên đầu)
piece("Pillar", sheet=RISE, box=(1528, 206, 1896, 670),
      cut=[[(1556, 300), (1640, 248), (1720, 202), (1802, 214), (1812, 330), (1792, 442), (1700, 446), (1640, 444),
            (1560, 432)],
           [(1528, 206), (1645, 206), (1645, 328), (1528, 328)]],
      metal=[([(1600, 430), (1800, 430), (1800, 540), (1600, 540)], "dark")],
      pivot=(1717, 652), points=dict(feet=(1717, 652)), min_area=25, taper=24)

# ===== #2 Đánh thường áp sát =====
# Khung 2: tia lửa toé lên sau gót khi đạp đất (bỏ cán lưỡi hái)
piece("DashKick", sheet=DASH, box=(296, 280, 436, 548),
      cut=[[(384, 470), (520, 290), (540, 300), (400, 490)], [(352, 438), (408, 438), (408, 492), (352, 492)],
           [(420, 300), (440, 300), (440, 548), (420, 548)]],
      metal=[([(296, 280), (436, 280), (436, 548), (296, 548)], "dark")],
      pivot=(400, 528), points=dict(feet=(400, 528)), min_area=12, taper=14)
# Khung 3: dải lửa tốc độ kéo sau lưng (phần ngoài thân, bỏ cán lưỡi hái)
piece("DashStreak", sheet=DASH, box=(694, 286, 880, 530),
      cut=[([(800, 340), (880, 330), (880, 530), (790, 530), (770, 470)], 0.72),
           [(694, 300), (790, 318), (800, 340), (694, 340)]],
      metal=[([(694, 286), (880, 286), (880, 530), (694, 530)], "dark")],
      pivot=(870, 470), points=dict(chest=(905, 420)), min_area=15, taper=26)
# Khung 4: lưỡi liềm lớn cuối đường lướt (vòng phải + đỉnh + dải thấp trước chân)
piece("DashArc", sheet=DASH, box=(1240, 150, 1556, 552),
      cut=[([(1240, 290), (1300, 280), (1380, 300), (1420, 330), (1430, 440), (1470, 470), (1470, 505), (1400, 500),
             (1330, 520), (1240, 520)], 0.72),
           [(1240, 150), (1300, 150), (1300, 290), (1240, 290)]],
      heal=[dict(poly=[(1400, 440), (1490, 440), (1490, 515), (1400, 515)], rot=(1330, 400, 1.0, [-20, 20, -30]))],
      pivot=(1330, 420), points=dict(chest=(1330, 420), tip=(1460, 490)), core=True, min_area=25, taper=22)
# Khung 5: bụi xám + đá đen tung sau gót lúc trượt dừng (giữ cả phần tối)
piece("DashDust", sheet=DASH, box=(1512, 388, 1680, 548), mode="full", fill=True,
      cut=[[(1590, 388), (1680, 388), (1680, 470), (1640, 470), (1600, 430)]],
      keep=[[(1520, 440), (1560, 410), (1600, 420), (1650, 450), (1676, 500), (1676, 548), (1512, 548)]],
      pivot=(1640, 540), points=dict(feet=(1650, 540)), min_area=30, taper=16)

# ===== #3 Dùng chiêu — chuỗi: lửa chân (K2) → vòng lốc K3 → lốc nhiều tầng K4 (cùng điểm neo chân) → cung kết (K5) =====
WHIRL3_BODY = [(724, 300), (760, 300), (790, 280), (870, 290), (890, 340), (880, 400), (860, 470), (900, 520), (900, 600),
               (800, 600), (760, 520), (730, 470), (724, 380)]
WHIRL4_BODY = [(1150, 300), (1230, 290), (1280, 320), (1300, 400), (1310, 470), (1290, 560), (1300, 600), (1150, 600),
               (1150, 520), (1170, 450), (1150, 400)]
piece("WhirlFlameL", sheet=WHIRL, box=(282, 290, 410, 594),
      cut=[[(380, 340), (410, 340), (410, 594), (360, 594), (350, 480)]],
      metal=[([(282, 440), (410, 440), (410, 594), (282, 594)], "dark")],
      pivot=(400, 588), points=dict(feet=(470, 588)), min_area=20, taper=20)
piece("WhirlFlameR", sheet=WHIRL, box=(536, 300, 642, 594),
      cut=[[(536, 300), (560, 300), (560, 594), (536, 594)]],
      pivot=(545, 588), points=dict(feet=(470, 588)), min_area=20, taper=20)
# K3: vòng lốc quanh người. TRƯỚC = dải vắt qua thân + phần dưới các vòng; SAU = phần còn lại, chỗ người che: dải hai bên nối vào
piece("WhirlRing3", sheet=WHIRL, box=(616, 176, 1006, 600),
      cut=[(WHIRL3_BODY, 0.66)],
      heal=[dict(poly=[(700, 176), (1006, 176), (1006, 300), (840, 300), (760, 400), (700, 380)], kind="dark",
                 rot=(810, 400, 0.45, [30, -30, 50, -50]))],
      metal=[([(616, 176), (1006, 176), (1006, 600), (616, 600)], "dark")],
      split=dict(silhouette=[WHIRL3_BODY], front=[[(616, 450), (1006, 420), (1006, 600), (616, 600)]], fill=True),
      pivot=(810, 590), points=dict(feet=(810, 590), chest=(810, 440)), min_area=25, taper=18)
# K4: lốc nhiều tầng (to hơn K3; cùng điểm neo chân)
piece("WhirlRing4", sheet=WHIRL, box=(996, 156, 1428, 606),
      cut=[(WHIRL4_BODY, 0.66)],
      heal=[dict(poly=[(1100, 156), (1300, 156), (1300, 300), (1200, 330), (1140, 330)], kind="dark",
                 rot=(1225, 400, 0.4, [30, -30, 50, -50]))],
      metal=[([(996, 156), (1428, 156), (1428, 606), (996, 606)], "dark")],
      split=dict(silhouette=[WHIRL4_BODY], front=[[(996, 460), (1428, 440), (1428, 606), (996, 606)]], fill=True),
      pivot=(1225, 596), points=dict(feet=(1225, 596), chest=(1225, 440)), min_area=25, taper=18)
# K5: cung quét mạnh tới trước (đầu lưỡi hái vá bằng cung bên cạnh)
piece("WhirlFinish", sheet=WHIRL, box=(1600, 240, 1896, 606),
      cut=[([(1600, 330), (1660, 380), (1700, 430), (1690, 520), (1600, 560)], 0.7)],
      heal=[dict(poly=[(1690, 400), (1860, 400), (1860, 490), (1690, 490)], shift=[(0, 60), (0, -60), (0, 90), (0, -90)])],
      metal=[([(1600, 240), (1700, 240), (1700, 330), (1600, 330)], "dark")],
      pivot=(1600, 440), points=dict(chest=(1600, 440), tip=(1810, 440)), core=True, min_area=25, taper=22)

# ===== #4 Dùng chiêu tầm xa — chuỗi: vòng phép (K2) → cung quét (K3) → sóng bay (K4) → nổ (K5) =====
# K4: sóng lưỡi liềm lửa xanh (đủ ngọn trên / mũi phải); lưỡi hái vá bằng vệt lửa trên / dưới nó
piece("Wave", sheet=RANGED, box=(1232, 256, 1574, 570),
      cut=[[(1500, 256), (1574, 256), (1574, 292), (1520, 296)]],
      heal=[dict(poly=[(1232, 330), (1400, 330), (1400, 452), (1232, 452)], shift=[(0, 70), (0, -70), (0, 100), (0, -100)])],
      pivot=(1400, 400), points=dict(front=(1548, 400)), core=True, min_area=25, taper=28)
# K3: cung quét ngoài (chuyển từ quét sang sóng bay)
piece("WaveSweep", sheet=RANGED, box=(860, 226, 1050, 524),
      cut=[[(860, 330), (900, 330), (905, 450), (860, 450)]],
      heal=[dict(poly=[(880, 300), (1010, 300), (1010, 470), (880, 470)], rot=(760, 410, 1.0, [-16, 16, -26, 26]))],
      metal=[([(860, 226), (1050, 226), (1050, 524), (860, 524)], "dark")],
      pivot=(740, 420), points=dict(chest=(740, 420), tip=(940, 400)), min_area=25, taper=22)
# K5: vụ nổ tinh thể (đá đen đặc lại; bỏ mép sóng K4 bên trái, người K6 bên phải)
piece("Burst", sheet=RANGED, box=(1520, 104, 1944, 604), mode="full", fill=True,
      cut=[[(1520, 280), (1590, 280), (1600, 520), (1520, 540)], [(1900, 340), (1944, 330), (1944, 604), (1880, 604)]],
      pivot=(1770, 400), core=True, min_area=40, taper=26)
# K2: cầu sáng + vòng phép (lưỡi hái che nửa trái: vá bằng nửa phải soi gương, theo đúng hình lưỡi hái)
piece("Circle", sheet=RANGED, box=(388, 152, 594, 356),
      heal=[dict(poly=[(388, 152), (486, 152), (486, 356), (388, 356)], mirror=489, kind="dark")],
      keep=[ellipse(489, 253, 99, 99)],
      pivot=(489, 253), min_area=10, taper=10)

# ===== #5 Dùng chiêu thủ =====
# K3: vòng phép dưới chân (giày + mũi cán lưỡi hái che: vá bằng chính vòng, xoay trong không gian elip); nửa trước / sau
piece("Seal", sheet=GUARD, box=(652, 548, 1094, 654),
      heal=[dict(poly=[(740, 548), (1010, 548), (1010, 612), (740, 612)], kind="metal", grow=3, feather=2.0,
                 rot=(872, 598, 0.23, [26, -26, 40, -40, 56, -56]))],
      keep=[ellipse(872, 598, 218, 50)],
      split=dict(front=[[(652, 600), (1094, 600), (1094, 654), (652, 654)]], blur=2.0),
      pivot=(872, 598), min_area=12, taper=14)
GUARD4_BODY = [(1250, 330), (1330, 320), (1360, 380), (1370, 470), (1380, 560), (1400, 620), (1190, 620), (1200, 480),
               (1196, 420), (1150, 330), (1110, 230), (1140, 200), (1196, 300), (1230, 380)]
# K4: dải lửa xoắn quanh người (bỏ người; lưỡi hái vá bằng dải lửa bên cạnh), tách trước / sau
piece("GuardSwirl", sheet=GUARD, box=(1064, 120, 1420, 622),
      cut=[(GUARD4_BODY, 0.66)],
      heal=[dict(poly=[(1080, 140), (1250, 140), (1260, 330), (1230, 420), (1180, 440), (1100, 300)], kind="dark",
                 rot=(1290, 420, 0.5, [30, -30, 45, -45, 60]))],
      metal=[([(1064, 120), (1420, 120), (1420, 622), (1064, 622)], "dark")],
      split=dict(silhouette=[GUARD4_BODY], front=[[(1064, 480), (1420, 450), (1420, 622), (1064, 622)]], fill=True),
      pivot=(1295, 612), points=dict(feet=(1295, 612), chest=(1295, 450)), min_area=25, taper=20)
# K3: lưỡi lửa đứng hai bên người (lửa hồn bốc lên)
piece("GuardFlameL", sheet=GUARD, box=(684, 290, 790, 606),
      cut=[[(760, 340), (790, 340), (790, 606), (750, 606), (740, 480)]], metal=[[(684, 290), (790, 290), (790, 606), (684, 606)]],
      pivot=(760, 600), points=dict(feet=(870, 600)), min_area=20, taper=22)
piece("GuardFlameR", sheet=GUARD, box=(960, 290, 1078, 606),
      cut=[[(960, 330), (990, 330), (990, 606), (960, 606)]], metal=[[(960, 290), (1078, 290), (1078, 606), (960, 606)]],
      pivot=(980, 600), points=dict(feet=(870, 600)), min_area=20, taper=22)
# Đầu lâu lửa: K6, K3 trái / phải, K5 trên / phải (cắt theo elip quanh đầu lâu + đuôi lửa, không còn khuyết vuông)
piece("Skull", sheet=GUARD, box=(2080, 372, 2162, 474), keep=[ellipse(2122, 426, 36, 44)], pivot=(2122, 430), min_area=20, taper=12)
piece("SkullA", sheet=GUARD, box=(694, 356, 774, 452), keep=[ellipse(733, 404, 36, 44)], pivot=(733, 410), min_area=20, taper=12)
piece("SkullB", sheet=GUARD, box=(966, 384, 1040, 466), keep=[ellipse(1003, 424, 34, 38)], pivot=(1003, 428), min_area=20, taper=12)
piece("SkullC", sheet=GUARD, box=(1608, 104, 1700, 204), keep=[ellipse(1652, 152, 42, 48)], pivot=(1652, 158), min_area=20, taper=12)
piece("SkullD", sheet=GUARD, box=(1690, 330, 1790, 430), keep=[ellipse(1740, 380, 46, 46)], pivot=(1740, 384), min_area=20, taper=12)
# K5: tinh thể bay quanh người
piece("CrystalA", sheet=GUARD, box=(1498, 88, 1548, 164), min_area=30, taper=4)
piece("CrystalB", sheet=GUARD, box=(1408, 178, 1460, 274), min_area=30, taper=4)

# ===== #6 Kĩ năng đặc biệt — chuỗi: lửa chân (K2) → vành xoáy K3 → xoáy nhiều dải K4 → giáng: tia, cột / tinh thể, nứt đất (K5) =====
piece("StormFlameL", sheet=STORM, box=(300, 330, 400, 626),
      cut=[[(372, 330), (400, 330), (400, 626), (372, 626)]],
      metal=[([(300, 330), (400, 330), (400, 626), (300, 626)], "dark")],
      pivot=(390, 618), points=dict(feet=(470, 618)), min_area=20, taper=20)
piece("StormFlameR", sheet=STORM, box=(540, 300, 612, 626),
      cut=[[(540, 300), (556, 300), (556, 626), (540, 626)]],
      metal=[([(540, 300), (612, 300), (612, 626), (540, 626)], "dark")],
      pivot=(550, 618), points=dict(feet=(470, 618)), min_area=20, taper=20)
STORM3_BODY = [(720, 180), (900, 170), (900, 300), (880, 420), (830, 500), (760, 520), (730, 420), (700, 300)]
piece("StormRim", sheet=STORM, box=(640, 120, 1018, 650),
      cut=[(STORM3_BODY, 0.7), [(640, 120), (760, 120), (760, 470), (640, 470)]],
      metal=[([(640, 120), (1018, 120), (1018, 650), (640, 650)], "dark")],
      pivot=(800, 360), points=dict(chest=(800, 360)), min_area=25, taper=22)
STORM4_BODY = [(1120, 84), (1250, 84), (1300, 150), (1330, 220), (1340, 300), (1312, 410), (1230, 432), (1172, 392), (1146, 290),
               (1150, 200), (1112, 150)]
# K4: các dải xoáy, giữ đá đen bay trong xoáy; lưỡi hái trên đầu vá bằng dải xoáy bên cạnh; tách trước / sau
piece("StormSwirl", sheet=STORM, box=(1000, 70, 1514, 672), mode="full", fill=True,
      cut=[(STORM4_BODY, 0.68)],
      split=dict(silhouette=[STORM4_BODY], front=[[(1000, 380), (1514, 350), (1514, 672), (1000, 672)]], fill=True),
      pivot=(1250, 330), points=dict(chest=(1250, 330)), min_area=25, taper=20)
# K5: cột / tinh thể hai bên (giữ đá đen), tia giữa, nứt đất + bậc đá ở chân
piece("StormPillarL", sheet=STORM, box=(1500, 120, 1660, 600), mode="full", fill=True,
      cut=[[(1630, 300), (1660, 300), (1660, 600), (1600, 600)]],
      pivot=(1700, 590), points=dict(feet=(1700, 590)), min_area=30, taper=24)
piece("StormPillarR", sheet=STORM, box=(1780, 120, 1930, 600), mode="full", fill=True,
      cut=[[(1780, 300), (1800, 300), (1800, 600), (1780, 600)], [(1890, 440), (1930, 440), (1930, 600), (1880, 600)]],
      pivot=(1700, 590), points=dict(feet=(1700, 590)), min_area=30, taper=24)
piece("StormBeam", sheet=STORM, box=(1650, 60, 1770, 340),
      pivot=(1708, 330), points=dict(top=(1708, 70)), core=True, min_area=20, taper=30)
piece("StormCrack", sheet=STORM, box=(1520, 530, 1900, 650), mode="full", fill=True,
      cut=[[(1660, 530), (1760, 530), (1760, 560), (1660, 560)]],
      pivot=(1708, 600), min_area=30, taper=20)

# ===== #7 Kĩ năng đặc biệt tầm xa =====
# K2: cầu lửa xoáy (lưỡi hái / tóc che bên trái: vá bằng phần đối diện xoay 180°, theo đúng hình vật che)
piece("Vortex", sheet=TIDE, box=(384, 204, 592, 412),
      heal=[dict(poly=[(384, 204), (476, 204), (476, 412), (384, 412)], kind="dark", grow=3,
                 rot=(487, 307, 1.0, [180, 160, 200, 140, 220]))],
      keep=[ellipse(487, 307, 100, 100)],
      pivot=(487, 307), min_area=12, taper=16)
# K3–4: luồng lửa. Thân nối được (3 biến thể, đầu trái hiện dần 64 px, đầu phải đặc: đoạn sau đè lên đuôi đoạn trước thành
# dải liền), đầu nhọn (đầu trái hiện dần, đè lên đoạn thân cuối), chỗ bắt đầu (trước lưỡi hái, mép phải đặc), khói đen trên,
# đá vụn dưới
TIDE_ROW = (236, 580)
for i, x in enumerate((960, 1090, 1220)):
    piece("GroundWave" + ("", "B", "C")[i], sheet=TIDE, box=(x, TIDE_ROW[0], x + 190, TIDE_ROW[1]), mode="full",
          fill=True, seam=64, open="lr", pivot=(x, 572), points=dict(end=(x + 190, 572)), min_area=30, taper=20)
piece("TideHead", sheet=TIDE, box=(1200, 236, 1476, 580), mode="full", fill=True, seam=50, open="l",
      keep=[[(1200, 236), (1370, 250), (1420, 320), (1452, 390), (1470, 432), (1452, 478), (1410, 530), (1360, 578), (1200, 580)]],
      cut=[[(1440, 236), (1476, 236), (1476, 380), (1440, 360)]],
      pivot=(1200, 572), points=dict(tip=(1468, 432), body=(1376, 572)), min_area=30, taper=26)
piece("TideStart", sheet=TIDE, box=(786, 270, 980, 580), mode="full", fill=True, open="r",
      cut=[[(786, 270), (830, 270), (870, 330), (880, 410), (830, 430), (786, 480)], [(790, 480), (820, 470), (820, 580), (786, 580)]],
      metal=[([(786, 270), (900, 270), (900, 440), (786, 440)], "dark")],
      pivot=(980, 572), points=dict(root=(830, 420)), min_area=30, taper=22)
piece("TideSmokeA", sheet=TIDE, box=(900, 150, 1110, 262), mode="full", dark_gain=2.0, pivot=(1005, 258), min_area=30, taper=18)
piece("TideSmokeB", sheet=TIDE, box=(1110, 140, 1360, 250), mode="full", dark_gain=2.0, pivot=(1235, 246), min_area=30, taper=18)
piece("TideRocks", sheet=TIDE, box=(1060, 470, 1440, 584), mode="dark", dark_gain=2.2, pivot=(1250, 572), min_area=30, taper=14)
# K5: tinh thể đen trồi lên nổ tung ở cuối luồng (bỏ người K6 bên phải)
piece("Eruption", sheet=TIDE, box=(1448, 100, 1930, 604), mode="full", fill=True,
      cut=[[(1880, 360), (1930, 340), (1930, 604), (1880, 604)], [(1448, 380), (1470, 380), (1470, 604), (1448, 604)]],
      pivot=(1690, 590), core=True, min_area=60, taper=30)

# ===== #8 Kĩ năng đặc biệt áp chế =====
# K2: một đoạn xích thẳng (nối nhiều đoạn thành sợi xích)
piece("Chain", sheet=PRISON, box=(600, 396, 704, 428), min_area=10, taper=4)
DUMMY3 = [(936, 336), (1004, 336), (1012, 400), (1044, 450), (1034, 520), (1004, 562), (896, 562), (916, 450), (928, 400)]
# K3: xích quấn quanh mục tiêu, bỏ hình nộm xám (mục tiêu thật hiện qua kẽ xích): TRƯỚC = xích vắt qua thân, SAU = phần còn lại
piece("ChainWrap", sheet=PRISON, box=(806, 316, 1078, 560),
      metal=[(DUMMY3, "dummy")], metal_grow=1,
      split=dict(silhouette=[DUMMY3]),
      pivot=(970, 556), points=dict(top=(970, 345)), min_area=15, taper=14)
CAGE_DUMMY = [(1212, 330), (1294, 330), (1302, 420), (1308, 524), (1200, 524), (1206, 420)]
# K4: lồng xích. Hình nộm vá bằng xích treo + ánh sáng phía trên nó (dời lên theo bội số mắt xích: không còn lỗ hình người).
# TRƯỚC = 2 cột giáo ngoài + chóp + đế tinh thể; SAU = xích treo, ánh sáng giữa
piece("Cage", sheet=PRISON, box=(1076, 96, 1422, 604),
      heal=[dict(poly=CAGE_DUMMY, kind="dummy", grow=2, feather=2.5, shift=[(0, -26 * k) for k in range(2, 10)])],
      split=dict(front=[[(1096, 170), (1166, 170), (1166, 520), (1096, 520)], [(1336, 170), (1406, 170), (1406, 520), (1336, 520)],
                        [(1196, 100), (1300, 100), (1300, 250), (1196, 250)], [(1076, 490), (1422, 490), (1422, 604), (1076, 604)]],
                 blur=4.0),
      pivot=(1250, 580), points=dict(top=(1250, 110)), min_area=40, taper=20)


# ---------------------------------------------------------------------------------------------------------------------
# Biểu tượng 3 nút chiêu
# ---------------------------------------------------------------------------------------------------------------------

BLUE = (0.3, 0.78, 1.0)
EDGE = (0.02, 0.2, 0.45)

# Vùng vuông trên tấm, nền tròn (trong → ngoài), viền, tia sáng (chiêu cuối), dời ảnh (px biểu tượng)
ICONS = [
    dict(sheet=BASIC, box=(1088, 228, 1508, 648), inner=(0.12, 0.22, 0.42), outer=(0.03, 0.03, 0.1), rim=(0.22, 0.58, 0.75),
         offset=(-4, 0)),
    dict(sheet="skill/Dùng chiêu", box=(985, 172, 1425, 612), inner=(0.16, 0.42, 0.55), outer=(0.01, 0.1, 0.22),
         rim=(0.85, 0.85, 0.9), offset=(0, 4)),
    dict(sheet="special_skill/kĩ năng đặc biệt", box=(1452, 168, 1862, 578), inner=(0.55, 0.12, 0.2), outer=(0.15, 0.03, 0.08),
         rim=(0.95, 0.4, 0.35), rays=(1.0, 0.55, 0.4), offset=(0, 6)),
]


def radial(inner, outer):
    yy, xx = np.mgrid[0:ICON, 0:ICON].astype(np.float32) + 0.5
    r = np.clip(np.hypot(xx - ICON / 2, yy - ICON / 2) / (ICON / 2), 0, 1)[..., None]
    rgb = np.array(inner) * (1 - r) + np.array(outer) * r
    return Image.fromarray((np.dstack([rgb, np.ones(r.shape)]) * 255).astype(np.uint8), "RGBA")


def rays(base, color, count=12):
    from PIL import ImageDraw, ImageFilter
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    c = base.width / 2
    for i in range(count):
        a = np.deg2rad(i * 360 / count + 8)
        b = np.deg2rad(i * 360 / count + 17)
        pts = [(c, c), (c + np.cos(a) * c * 1.1, c + np.sin(a) * c * 1.1), (c + np.cos(b) * c * 1.1, c + np.sin(b) * c * 1.1)]
        draw.polygon(pts, fill=tuple(int(255 * v) for v in color) + (60,))
    out = base.copy()
    out.alpha_composite(layer.filter(ImageFilter.GaussianBlur(3)))
    return out


def shrink(rgba, size):
    """Thu nhỏ (khử răng cưa qua alpha nhân trước) cho cạnh dài = size."""
    h, w = rgba.shape[:2]
    scale = size / max(w, h)
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    pre = np.dstack([rgba[..., :3] * rgba[..., 3:], rgba[..., 3:]])
    small = np.asarray(Image.fromarray((np.clip(pre, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").resize((nw, nh), Image.LANCZOS))
    small = small.astype(np.float32) / 255
    a = small[..., 3:]
    rgb = np.where(a > 1e-4, small[..., :3] / np.maximum(a, 1e-4), 0)
    return Image.fromarray((np.dstack([np.clip(rgb, 0, 1), a]) * 255 + 0.5).astype(np.uint8), "RGBA")


def finish(img, rim):
    """Cắt tròn, viền sáng mép trong (như skill_icons.py)."""
    arr = np.asarray(img).astype(np.float32) / 255
    yy, xx = np.mgrid[0:ICON, 0:ICON].astype(np.float32) + 0.5
    r = np.hypot(xx - ICON / 2, yy - ICON / 2)
    glow = np.exp(-((r - (ICON / 2 - 10)) / 7) ** 2)[..., None] * np.array(rim)
    arr[..., :3] = 1 - (1 - np.clip(arr[..., :3], 0, 1)) * np.exp(-glow)
    arr[..., 3] = np.clip(ICON / 2 - 2 - r + 0.5, 0, 1)
    return Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")


def icons():
    for i, spec in enumerate(ICONS):
        rgb_all, bg = sheet(spec["sheet"])
        x0, y0, x1, y1 = spec["box"]
        rgb = rgb_all[y0:y1, x0:x1]
        art = unblend(rgb, bg, eps=5 / 255)
        # Người và tinh thể đen phải đặc (không để nền biểu tượng ánh qua áo tối): lấp kín bên trong vùng đã sáng
        a = art[..., 3]
        solid = ndi.binary_fill_holes(ndi.binary_closing(a > 0.3, iterations=3))
        solid = ndi.binary_erosion(solid, iterations=2)
        soft = np.clip(ndi.gaussian_filter(solid.astype(np.float32), 1.5), 0, 1)
        art[..., :3] = art[..., :3] * (1 - soft[..., None]) + rgb * soft[..., None]
        art[..., 3] = np.maximum(a, soft)
        # mép vùng cắt mờ dần (khung bên cạnh không lọt vào)
        h, w = art.shape[:2]
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        d = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)) / (0.08 * w)
        art[..., 3] *= np.clip(d, 0, 1)
        base = radial(spec["inner"], spec["outer"])
        if spec.get("rays"):
            base = rays(base, spec["rays"])
        layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
        small = shrink(art, ICON + 14)  # tràn mép một chút: cắt tròn sau cùng
        ox, oy = spec.get("offset", (0, 0))
        layer.alpha_composite(small, ((ICON - small.width) // 2 + ox, (ICON - small.height) // 2 + oy))
        base.alpha_composite(layer)
        path = UI / f"Skill_Blue_{i + 1}.png"
        finish(base, spec["rim"]).save(path)
        print("->", path.relative_to(ROOT))




# ---------------------------------------------------------------------------------------------------------------------
# Chạy
# ---------------------------------------------------------------------------------------------------------------------

MANIFEST = OUT / "manifest.json"


def build(only):
    OUT.mkdir(parents=True, exist_ok=True)
    old = {}
    if MANIFEST.exists():
        for e in json.loads(MANIFEST.read_text())["pieces"]:
            old[e["name"]] = e
    entries = dict(old) if only else {}
    for name, spec in PIECES.items():
        if only and name not in only:
            continue
        x0, y0 = spec["box"][:2]
        for out_name, rgba in cut(name, spec).items():
            arr, xA, yA = finish_piece(rgba, spec, x0, y0)
            img = to_image(arr)
            img.save(OUT / f"{out_name}.png")
            entries[out_name] = manifest_entry(out_name, img, spec, xA, yA)
            print("->", out_name, img.size)
    MANIFEST.write_text(json.dumps(dict(ppu=PPU, pieces=sorted(entries.values(), key=lambda e: e["name"])), indent=1,
                                   ensure_ascii=False))


def preview(path, prefix=()):
    """Bảng mọi mảng: mỗi ô nền tối (trái) + nền sáng (phải), chấm đỏ = pivot."""
    from PIL import ImageFont
    entries = json.loads(MANIFEST.read_text())["pieces"]
    if prefix:
        entries = [e for e in entries if any(e["name"].startswith(p) for p in prefix)]
    cell = 260
    cols = 4
    rows = (len(entries) + cols - 1) // cols
    sheet_img = Image.new("RGB", (cols * cell * 2, rows * (cell + 16)), (40, 40, 40))
    d = ImageDraw.Draw(sheet_img)
    for i, e in enumerate(entries):
        img = Image.open(OUT / f"{e['name']}.png").convert("RGBA")
        k = min(1.0, (cell - 8) / max(img.size))
        small = img.resize((max(1, int(img.width * k)), max(1, int(img.height * k))), Image.LANCZOS)
        cx, cy = (i % cols) * cell * 2, (i // cols) * (cell + 16)
        for j, bgc in enumerate([(14, 16, 30), (236, 232, 220)]):
            tile = Image.new("RGBA", (cell, cell), bgc + (255,))
            ox, oy = (cell - small.width) // 2, (cell - small.height) // 2
            tile.alpha_composite(small, (ox, oy))
            px = ox + e["pivot"][0] * small.width
            py = oy + (1 - e["pivot"][1]) * small.height
            ImageDraw.Draw(tile).ellipse([px - 3, py - 3, px + 3, py + 3], outline=(255, 40, 40))
            sheet_img.paste(tile.convert("RGB"), (cx + j * cell, cy))
        d.text((cx + 4, cy + cell + 2), f"{e['name']} {e['w']}x{e['h']}", fill=(255, 255, 160))
    sheet_img.save(path)


def main():
    only = set(a for a in ARGS if a != "icons")
    if PREVIEW:
        preview(PREVIEW, tuple(only))
        return
    build(only)
    if not only or "icons" in ARGS:
        icons()


if __name__ == "__main__":
    main()
