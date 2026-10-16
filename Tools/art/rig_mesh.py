"""Hàm dùng chung cho các script tách nhân vật thành bộ phận gắn xương (octopus_rig.py, spider_rig.py)."""
import math

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi


def premultiplied_resize(rgba, scale):
    """Thu nhỏ trên màu đã nhân alpha (không viền đen / trắng) rồi làm nét nhẹ (unsharp mask) cho nét vẽ rõ hơn."""
    img = Image.fromarray(rgba.astype(np.uint8), "RGBA").convert("RGBa")
    w, h = max(1, round(img.width * scale)), max(1, round(img.height * scale))
    small = np.asarray(img.resize((w, h), Image.LANCZOS)).astype(np.float32)
    sharp = np.asarray(Image.fromarray(small[..., :3].astype(np.uint8), "RGB")
                       .filter(ImageFilter.UnsharpMask(radius=1.0, percent=70, threshold=1))).astype(np.float32)
    small[..., :3] = np.minimum(sharp, small[..., 3:4])   # màu đã nhân alpha không được vượt alpha
    return np.asarray(Image.fromarray(small.astype(np.uint8), "RGBa").convert("RGBA"))


def crop_box(alpha, margin=6):
    ys, xs = np.nonzero(alpha > 2)
    return (max(xs.min() - margin, 0), max(ys.min() - margin, 0),
            min(xs.max() + 1 + margin, alpha.shape[1]), min(ys.max() + 1 + margin, alpha.shape[0]))


def skin_data(out, origin, bone_specs, weights_at, grid, cell_ok=None):
    """Xương + lưới tam giác phủ ảnh + trọng số (tối đa 4 xương mỗi đỉnh).
    bone_specs: [(tên, điểm đầu, điểm cuối, xương cha)] toạ độ pixel ảnh gốc; cha -1 = gốc.
    weights_at(x, y): [(xương, trọng số)…] cho điểm ảnh gốc (x, y).
    cell_ok(4 góc ảnh gốc): False = bỏ ô lưới (vd ô vắt qua khe giữa hai vòng cuộn)."""
    x0, y0, sx, sy = origin
    h, w = out.shape[:2]

    def src_of(px, py_top):  # pixel ảnh xuất (gốc trên-trái) → pixel ảnh gốc
        return x0 + min(px, w) * sx, y0 + min(py_top, h) * sy

    def local(x, y):         # pixel ảnh gốc → pixel ảnh xuất, gốc dưới-trái (quy ước của Unity)
        return (x - x0) / sx, h - (y - y0) / sy

    cover = ndi.binary_dilation(out[..., 3] > 1, iterations=1)
    gx, gy = int(math.ceil(w / grid)), int(math.ceil(h / grid))
    vid = -np.ones((gy + 1, gx + 1), int)
    verts, flat_weights, tris = [], [], []

    def vertex(i, j):
        if vid[j, i] < 0:
            px, py = min(i * grid, w), min(j * grid, h)
            vid[j, i] = len(verts)
            verts.append((float(px), float(h - py)))
            pairs = [(b, wt) for b, wt in weights_at(*src_of(px, py)) if wt > 1e-4]
            pairs = sorted(pairs, key=lambda p: -p[1])[:4]
            total = sum(wt for _, wt in pairs) or 1.0
            pairs = [(b, round(float(wt / total), 4)) for b, wt in pairs] + [(0, 0.0)] * (4 - len(pairs))
            flat_weights.extend(c for pair in pairs for c in pair)
        return vid[j, i]

    for j in range(gy):
        for i in range(gx):
            if not cover[j * grid:(j + 1) * grid, i * grid:(i + 1) * grid].any():
                continue
            corners = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if cell_ok is not None and not cell_ok([src_of(ci * grid, cj * grid) for ci, cj in corners]):
                continue
            a, b, c, d = (vertex(ci, cj) for ci, cj in corners)
            tris += [int(a), int(b), int(c), int(a), int(c), int(d)]

    # Cạnh viền (cạnh chỉ thuộc một tam giác), cho Skinning Editor
    edge_count = {}
    for k in range(0, len(tris), 3):
        tri = tris[k:k + 3]
        for e in ((tri[0], tri[1]), (tri[1], tri[2]), (tri[2], tri[0])):
            key2 = (min(e), max(e))
            edge_count[key2] = edge_count.get(key2, 0) + 1
    edges = [i for e, c in edge_count.items() if c == 1 for i in (int(e[0]), int(e[1]))]

    # Xương: vị trí / góc so với xương cha (pixel ảnh xuất), như ISpriteBoneDataProvider cần
    bones, absolute = [], []
    for name, start, stop, parent in bone_specs:
        (ax, ay), (bx, by) = local(*start), local(*stop)
        angle = math.degrees(math.atan2(by - ay, bx - ax))
        length = math.hypot(bx - ax, by - ay)
        if parent < 0:
            position, rotation = [round(ax, 3), round(ay, 3)], angle
        else:
            (px, py), pa = absolute[parent]
            ca, sa = math.cos(math.radians(-pa)), math.sin(math.radians(-pa))
            dx, dy = ax - px, ay - py
            position = [round(dx * ca - dy * sa, 3), round(dx * sa + dy * ca, 3)]
            rotation = angle - pa
        absolute.append(((ax, ay), angle))
        bones.append({"name": name, "parent": parent, "position": position,
                      "rotation": round(rotation, 4), "length": round(length, 3)})

    return {"bones": bones,
            "vertices": [round(c, 2) for v in verts for c in v],
            "indices": tris,
            "edges": edges,
            "weights": flat_weights,
            "influences": 4}, len(verts), len(tris) // 3
