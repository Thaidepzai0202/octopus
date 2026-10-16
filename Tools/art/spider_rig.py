"""
Tách con nhện từ tấm bộ phận Docs/spider/Spider_pink.png để gắn xương (2D Animation) trong Unity.

    python3 Tools/art/spider_rig.py

Tấm ảnh có sẵn từng bộ phận rời (dưới con nhện ráp sẵn): bụng, đầu ngực, 8 mắt, 4 răng nanh và 3 kiểu chân (mỗi kiểu
một chiếc trái, một chiếc phải). Script cắt từng bộ phận, đưa về cùng tỉ lệ, ráp thành con nhện nhìn chính diện hơi
nghiêng sang phải như tranh ráp sẵn, và gắn 2 xương cho mỗi chân: đùi (gốc → gối) và cẳng (gối → móng).

Ra (Assets/_Project/Art/Spider/Rig cho game, Docs/spider/rig bản xem):
    abdomen.png, head.png, eyes.png, fang_l/r.png, fang_inner_l/r.png
    leg_a_l/r.png  chân trước có gai      leg_b_l/r.png  chân thứ hai
    leg_c_l/r.png  chân thứ ba            leg_d_l/r.png  chân sau, dài nhất
    rig.json       vị trí bộ phận (unit, gốc ở mặt đất dưới thân, x sang phải, y lên trên), xương / lưới / trọng số
                   của chân (pixel ảnh bộ phận, gốc dưới-trái như ISpriteBoneDataProvider), chỗ gắn và chỗ đặt 8 chân
Ảnh ráp thử (chân đặt xuống đất bằng IK, giống Unity sẽ làm): Docs/spider/xem_rig/lap_rap.png
"""
import json
import math
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

from rig_mesh import crop_box, premultiplied_resize, skin_data

ROOT = Path(__file__).resolve().parents[2]
SHEET = ROOT / "Docs/spider/Spider_pink.png"
OUTS = [ROOT / "Assets/_Project/Art/Spider/Rig", ROOT / "Docs/spider/rig"]
PREVIEW = ROOT / "Docs/spider/xem_rig/lap_rap.png"

SRC_PPU = 300           # pixel tấm bộ phận mỗi unit nhện (đầu ngực rộng ~1 unit trước khi thu)
PPU = 256               # pixel ảnh xuất mỗi unit
GRID = 4                # bước lưới tam giác của chân (pixel ảnh xuất)
KNEE_BLEND = 0.035      # dải chuyển trọng số đùi → cẳng quanh gối (phần chiều dài chân)

# Bộ phận: khung gần đúng trên tấm ảnh (để nhận ra mảng màu), tỉ lệ thu, tâm đặt (unit), thứ tự vẽ.
# Mắt / nanh trong tấm ảnh vẽ to hơn tranh ráp sẵn nhiều nên thu nhỏ hơn.
PARTS = {
    "abdomen":      dict(box=(106, 593, 424, 914), scale=0.95, at=(-0.30, 1.05), order=3),
    "head":         dict(box=(488, 616, 785, 908), scale=0.85, at=(0.12, 0.66), order=6),
    "eyes":         dict(box=(821, 646, 1028, 796), scale=0.40, at=None, order=7),
    "fang_l":       dict(box=(804, 779, 891, 920), scale=0.50, at=None, order=8),
    "fang_r":       dict(box=(959, 779, 1045, 919), scale=0.50, at=None, order=8),
    "fang_inner_l": dict(box=(882, 810, 911, 888), scale=0.50, at=None, order=8),
    "fang_inner_r": dict(box=(938, 811, 968, 888), scale=0.50, at=None, order=8),
}
# Mắt, nanh đặt theo đầu: hốc mắt trên tấm ảnh đầu ở (650, 782), nanh ngay dưới hốc mắt
EYE_SOCKET = (650, 782)
FANG_OFFSETS = {"fang_l": (-0.115, -0.2), "fang_r": (0.115, -0.2), "fang_inner_l": (-0.035, -0.17),
                "fang_inner_r": (0.035, -0.17)}

# Kiểu chân: khung trên tấm ảnh của chiếc trái / phải
LEG_ART = {"a": ((22, 894, 261, 1077), (276, 898, 511, 1078)),
           "b": ((514, 903, 706, 1070), (720, 899, 927, 1068)),
           "c": ((514, 903, 706, 1070), (720, 899, 927, 1068)),
           "d": ((993, 839, 1229, 1062), (1238, 839, 1445, 1058))}
LEG_SCALE = {"a": 1.05, "b": 1.2, "c": 1.4, "d": 1.45}

# 8 chân: (kiểu, bên: 1 trái / -1 phải, thứ tự 0 trước … 3 sau, chỗ gắn trên thân, chỗ đặt móng, thứ tự vẽ).
# Nhìn chính diện hơi nghiêng: bên phải lệch sang phải một chút; chân sau đặt hơi cao (xa hơn trên mặt đất)
LEGS = [
    ("a", 1, 0, (-0.08, 0.50), (-0.42, 0.00), 9),
    ("b", 1, 1, (-0.18, 0.58), (-0.95, 0.02), 4),
    ("c", 1, 2, (-0.22, 0.68), (-1.35, 0.04), 2),
    ("d", 1, 3, (-0.22, 0.80), (-1.72, 0.07), 1),
    ("a", -1, 0, (0.32, 0.50), (0.70, 0.00), 9),
    ("b", -1, 1, (0.42, 0.58), (1.22, 0.02), 4),
    ("c", -1, 2, (0.46, 0.68), (1.62, 0.04), 2),
    ("d", -1, 3, (0.46, 0.80), (1.98, 0.07), 1),
]

# ---------------------------------------------------------------- nạp ảnh, tách mảng màu

sheet = np.asarray(Image.open(SHEET).convert("RGBA")).astype(np.float32)
labels, _ = ndi.label(sheet[..., 3] > 40)
# Viền mờ (alpha thấp) thuộc về mảng gần nhất, để cắt bộ phận không dính viền bộ phận bên cạnh
_, (iy, ix) = ndi.distance_transform_edt(labels == 0, return_indices=True)
owner = labels[iy, ix]


def component_ids(box):
    """Các mảng nằm gần trọn trong khung box (mảng bên cạnh chỉ chạm mép khung thì bỏ)."""
    x0, y0, x1, y1 = box
    ids = []
    for i, sl in enumerate(ndi.find_objects(labels)):
        if (labels[sl] == i + 1).sum() < 200:
            continue
        ox = max(0, min(x1, sl[1].stop) - max(x0, sl[1].start))
        oy = max(0, min(y1, sl[0].stop) - max(y0, sl[0].start))
        inside = ox * oy / ((sl[1].stop - sl[1].start) * (sl[0].stop - sl[0].start))
        if inside > 0.9:
            ids.append(i + 1)
    return ids


def cut(box):
    """Ảnh RGBA của các mảng trong box, cắt sát (toạ độ tấm ảnh của góc trên-trái kèm theo)."""
    keep = np.isin(owner, component_ids(box))
    rgba = sheet.copy()
    rgba[..., 3] *= keep
    x0, y0, x1, y1 = crop_box(rgba[..., 3], margin=4)
    return rgba[y0:y1, x0:x1], (x0, y0)


# ---------------------------------------------------------------- chân: gối, gốc, móng, trọng số

def bfs(mask, start):
    """Khoảng cách đi trong hình (pixel) từ start tới mọi điểm của mask."""
    h, w = mask.shape
    dist = np.full(mask.shape, np.inf)
    dist[start] = 0.0
    queue = deque([start])
    steps = [(1, 0, 1.0), (-1, 0, 1.0), (0, 1, 1.0), (0, -1, 1.0),
             (1, 1, 1.414), (1, -1, 1.414), (-1, 1, 1.414), (-1, -1, 1.414)]
    while queue:
        y, x = queue.popleft()
        for dy, dx, c in steps:
            yy, xx = y + dy, x + dx
            if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and dist[yy, xx] > dist[y, x] + c:
                dist[yy, xx] = dist[y, x] + c
                queue.append((yy, xx))
    return dist


def leg_keys(rgba, side):
    """Chân vẽ hình chữ V ngược: gối ở đỉnh; hai đầu dưới là gốc (đùi, ngắn) và móng (cẳng, dài).
    Chân trái móng ở bên trái, chân phải móng ở bên phải. Trả về (gốc, gối, móng) toạ độ ảnh (x, y) và tham số
    dọc chân t (0 ở gốc, 1 ở móng) cho mọi pixel, t_gối."""
    mask = rgba[..., 3] > 40
    lab, _ = ndi.label(mask)
    sizes = np.bincount(lab.ravel())
    sizes[0] = 0
    mask = lab == sizes.argmax()
    ys, xs = np.nonzero(mask)
    top = ys <= ys.min() + 6
    knee = (int(round(ys[top].mean())), int(round(xs[top].mean())))
    from_knee = bfs(mask, knee)
    reach = np.where(np.isfinite(from_knee), from_knee, -1.0)
    columns = np.arange(mask.shape[1])[None, :]
    left = np.unravel_index(np.argmax(np.where(columns < knee[1], reach, -1.0)), mask.shape)
    right = np.unravel_index(np.argmax(np.where(columns >= knee[1], reach, -1.0)), mask.shape)
    tip, root = (left, right) if side > 0 else (right, left)

    d_root, d_tip = bfs(mask, root), bfs(mask, tip)
    with np.errstate(invalid="ignore"):          # ngoài hình: inf / inf, được thay ngay bên dưới
        t = d_root / np.maximum(d_root + d_tip, 1e-6)
    # Ngoài hình (viền mờ, lưới phủ quá mép) lấy theo điểm gần nhất trong hình
    _, (ny, nx) = ndi.distance_transform_edt(~mask, return_indices=True)
    t = t[ny, nx]
    xy = lambda p: (float(p[1]), float(p[0]))
    return xy(root), xy(knee), xy(tip), t, float(t[knee])


def smoothstep(a, b, x):
    k = np.clip((x - a) / (b - a), 0.0, 1.0)
    return k * k * (3 - 2 * k)


# ---------------------------------------------------------------- xuất

def to_units(px, scale):
    return px / (SRC_PPU / scale)


def export(name, rgba, offset, scale, position, order, extra=None):
    """Thu ảnh theo tỉ lệ, lưu PNG; position = tâm ảnh (unit). Trả về mục rig.json và ảnh đã thu."""
    k = scale * PPU / SRC_PPU
    out = premultiplied_resize(rgba, k)
    for folder in OUTS:
        folder.mkdir(parents=True, exist_ok=True)
        Image.fromarray(out, "RGBA").save(folder / f"{name}.png")
    entry = {"name": name, "file": f"{name}.png", "size": [out.shape[1], out.shape[0]], "pivot": [0.5, 0.5],
             "position": [round(position[0], 5), round(position[1], 5)], "order": order}
    if extra:
        entry.update(extra)
    return entry, out


parts, images, legs = [], {}, []

# Thân, đầu, mắt, nanh
cuts = {name: cut(spec["box"]) for name, spec in PARTS.items()}
head_rgba, head_off = cuts["head"]
head_spec = PARTS["head"]
head_center_px = (head_off[0] + head_rgba.shape[1] / 2, head_off[1] + head_rgba.shape[0] / 2)
socket = (head_spec["at"][0] + to_units(EYE_SOCKET[0] - head_center_px[0], head_spec["scale"]),
          head_spec["at"][1] - to_units(EYE_SOCKET[1] - head_center_px[1], head_spec["scale"]))
for name, spec in PARTS.items():
    rgba, offset = cuts[name]
    at = spec["at"]
    if name == "eyes":
        at = socket
    elif name.startswith("fang"):
        at = (socket[0] + FANG_OFFSETS[name][0], socket[1] + FANG_OFFSETS[name][1])
    entry, out = export(name, rgba, offset, spec["scale"], at, spec["order"])
    parts.append(entry)
    images[name] = (out, entry)

# Chân: mỗi kiểu xuất một chiếc trái, một chiếc phải, kèm 2 xương
leg_parts = {}
for kind, boxes in LEG_ART.items():
    for side, box in ((1, boxes[0]), (-1, boxes[1])):
        name = f"leg_{kind}_{'l' if side > 0 else 'r'}"
        rgba, offset = cut(box)
        root, knee, tip, t_map, t_knee = leg_keys(rgba, side)
        scale = LEG_SCALE[kind]
        k = scale * PPU / SRC_PPU
        out = premultiplied_resize(rgba, k)
        origin = (offset[0], offset[1], rgba.shape[1] / out.shape[1], rgba.shape[0] / out.shape[0])
        g = lambda p: (offset[0] + p[0], offset[1] + p[1])
        specs = [("Femur", g(root), g(knee), -1), ("Tibia", g(knee), g(tip), 0)]

        def weights_at(x, y, t_map=t_map, t_knee=t_knee, offset=offset):
            ix = int(np.clip(round(x - offset[0]), 0, t_map.shape[1] - 1))
            iy = int(np.clip(round(y - offset[1]), 0, t_map.shape[0] - 1))
            w = float(smoothstep(t_knee - KNEE_BLEND, t_knee + KNEE_BLEND, t_map[iy, ix]))
            return [(0, 1.0 - w), (1, w)]

        data, nv, nt = skin_data(out, origin, specs, weights_at, GRID)
        center = (offset[0] + rgba.shape[1] / 2, offset[1] + rgba.shape[0] / 2)
        rel = lambda p: (to_units(offset[0] + p[0] - center[0], scale), -to_units(offset[1] + p[1] - center[1], scale))
        leg_parts[name] = dict(out=out, data=data, root=rel(root), knee=rel(knee), tip=rel(tip), t_map=t_map,
                               t_knee=t_knee, scale=scale)
        print(f"  {name}: {out.shape[1]}x{out.shape[0]}, {nv} đỉnh, {nt} tam giác, "
              f"đùi {math.dist(rel(root), rel(knee)):.2f} cẳng {math.dist(rel(knee), rel(tip)):.2f} unit")

for i, (kind, side, slot, attach, rest, order) in enumerate(LEGS):
    name = f"leg_{kind}_{'l' if side > 0 else 'r'}"
    info = leg_parts[name]
    # Ảnh chân không xoay; đặt sao cho gốc đùi trùng chỗ gắn trên thân
    position = (attach[0] - info["root"][0], attach[1] - info["root"][1])
    part_name = f"leg{i}"
    entry = {"name": part_name, "file": f"{name}.png", "size": [info["out"].shape[1], info["out"].shape[0]],
             "pivot": [0.5, 0.5], "position": [round(position[0], 5), round(position[1], 5)], "order": order}
    entry.update(info["data"])
    parts.append(entry)
    legs.append({"part": part_name, "side": side, "slot": slot, "root": list(attach), "rest": list(rest)})
    for folder in OUTS:
        Image.fromarray(info["out"], "RGBA").save(folder / f"{name}.png")

manifest = {"pixelsPerUnit": PPU, "parts": parts, "legs": legs}
for folder in OUTS:
    (folder / "rig.json").write_text(json.dumps(manifest, separators=(",", ":")))
print(f"rig.json: {len(parts)} bộ phận, {len(legs)} chân")


# ---------------------------------------------------------------- ảnh ráp thử

def two_bone(root, target, l1, l2):
    """Gối (unit) khi đùi dài l1, cẳng dài l2 nối root → target; chọn gối phía trên."""
    rx, ry = root
    dx, dy = target[0] - rx, target[1] - ry
    d = min(max(math.hypot(dx, dy), abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    base = math.atan2(dy, dx)
    bend = math.acos(max(-1.0, min(1.0, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
    knees = [(rx + l1 * math.cos(base + s * bend), ry + l1 * math.sin(base + s * bend)) for s in (1, -1)]
    return max(knees, key=lambda p: p[1])


def preview():
    scale_px = 220                                   # pixel ảnh xem mỗi unit
    width, height = 4.6, 2.0
    canvas = Image.new("RGBA", (int(width * scale_px), int(height * scale_px)), (40, 30, 64, 255))
    origin = (2.2 * scale_px, 1.75 * scale_px)       # mặt đất dưới thân

    def to_px(p):
        return origin[0] + p[0] * scale_px, origin[1] - p[1] * scale_px

    def draw(img, matrix):
        """Vẽ ảnh qua phép biến đổi affine matrix (pixel ảnh, gốc trên-trái → pixel ảnh xem)."""
        a, b, c, d, e, f = matrix
        det = a * e - b * d
        inv = (e / det, -b / det, (b * f - c * e) / det, -d / det, a / det, (c * d - a * f) / det)
        layer = Image.fromarray(img, "RGBA").transform(canvas.size, Image.AFFINE, inv, Image.BICUBIC)
        canvas.alpha_composite(layer)

    def rigid(img, src_a, src_b, dst_a, dst_b):
        """Ma trận đưa đoạn src_a→src_b (pixel ảnh) tới dst_a→dst_b (pixel ảnh xem), giữ tỉ lệ ảnh xuất."""
        k = scale_px / PPU
        ang = math.atan2(dst_b[1] - dst_a[1], dst_b[0] - dst_a[0]) - math.atan2(src_b[1] - src_a[1], src_b[0] - src_a[0])
        ca, sa = math.cos(ang) * k, math.sin(ang) * k
        return (ca, -sa, dst_a[0] - (ca * src_a[0] - sa * src_a[1]), sa, ca, dst_a[1] - (sa * src_a[0] + ca * src_a[1]))

    items = [(e["order"], "part", e) for e in parts if not e["name"].startswith("leg")]
    items += [(LEGS[i][5], "leg", i) for i in range(len(LEGS))]
    for _, kind, item in sorted(items, key=lambda it: it[0]):
        if kind == "part":
            out, entry = images[item["name"]]
            k = scale_px / PPU
            cx, cy = to_px(entry["position"])
            draw(out, (k, 0, cx - out.shape[1] * k / 2, 0, k, cy - out.shape[0] * k / 2))
            continue

        leg_kind, side, slot, attach, rest, order = LEGS[item]
        info = leg_parts[f"leg_{leg_kind}_{'l' if side > 0 else 'r'}"]
        out, t_map = info["out"], info["t_map"]
        l1, l2 = math.dist(info["root"], info["knee"]), math.dist(info["knee"], info["tip"])
        knee = two_bone(attach, rest, l1, l2)
        # Trọng số cẳng trên ảnh xuất (lấy mẫu t trên ảnh gốc)
        h, w = out.shape[:2]
        ty = np.clip((np.arange(h) * t_map.shape[0] / h).astype(int), 0, t_map.shape[0] - 1)
        tx = np.clip((np.arange(w) * t_map.shape[1] / w).astype(int), 0, t_map.shape[1] - 1)
        wt = smoothstep(info["t_knee"] - KNEE_BLEND, info["t_knee"] + KNEE_BLEND, t_map[ty][:, tx])
        # Toạ độ điểm khoá trên ảnh xuất (pixel, gốc trên-trái)
        img_px = lambda p: (w / 2 + p[0] * PPU, h / 2 - p[1] * PPU)
        for part_w, a, b, da, db in ((1 - wt, info["root"], info["knee"], attach, knee),
                                     (wt, info["knee"], info["tip"], knee, rest)):
            layer = out.copy()
            layer[..., 3] = (layer[..., 3] * part_w).astype(np.uint8)
            draw(layer, rigid(layer, img_px(a), img_px(b), to_px(da), to_px(db)))

    ImageDraw = __import__("PIL.ImageDraw", fromlist=["ImageDraw"])
    d = ImageDraw.Draw(canvas)
    d.line([(0, origin[1]), (canvas.width, origin[1])], fill=(140, 120, 200, 255), width=2)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(PREVIEW)
    print("->", PREVIEW.relative_to(ROOT))


preview()
