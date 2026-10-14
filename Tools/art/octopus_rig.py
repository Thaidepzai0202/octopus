"""
Tách bạch tuộc chính (octopus.png) thành bộ phận để gắn xương (2D Animation) trong Unity.

    python octopus_rig.py <octopus.png> <thư mục ra>

Ra:
    body.png            đầu + giáp; chỗ mắt được vẽ bù thành mí mắt khép (thấy khi chớp mắt)
    eye.png             tròng mắt (đã xoá con ngươi, vẽ bù phần tròng phía sau)
    pupil.png           con ngươi dọc
                        (đầu cũng có 3 xương để mềm, ngả theo quán tính; mắt gắn vào xương giữa)
    tentacle_l/m/r.png  3 xúc tu, mỗi xúc tu kèm một đoạn gốc giấu dưới thân để khi xoay không hở
    rig.json            vị trí từng bộ phận, xương (chuỗi khớp dọc trục xúc tu), lưới tam giác và trọng số

Toạ độ trong rig.json (mảng phẳng để JsonUtility của Unity đọc được: vertices = x0,y0,x1,y1…;
weights = 2 cặp (xương, trọng số) mỗi đỉnh; edges = a0,b0,a1,b1…):
    - "rig" (unit): gốc ở điểm chạm đất (đáy xúc tu giữa), x sang phải, y lên trên
    - xương / lưới (pixel của ảnh bộ phận, gốc dưới-trái) theo đúng quy ước của ISpriteBoneDataProvider
"""
import json
import math
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi

SRC, OUT = sys.argv[1], sys.argv[2]

HEIGHT_UNITS = 1.5      # cả con cao bấy nhiêu unit (bằng khung idle cũ)
PPU = 256               # pixel ảnh xuất mỗi unit tranh (Unity phóng to cả con thêm "size" lần, xem OctopusRigAnimator)
BONES = 14              # số xương mỗi xúc tu
GRID = 4                # bước lưới tam giác của xúc tu (pixel ảnh xuất)
BODY_GRID = 6           # bước lưới của đầu
CUT_Y = 662             # dưới dòng này là xúc tu, trên là thân (pixel ảnh gốc)
FADE = 16               # thân mờ dần trong dải này để chỗ nối không thành đường cắt cứng
EXT_TOP = 585           # gốc xúc tu kéo dài lên tới đây, giấu dưới thân

# Trục xúc tu (pixel ảnh gốc), từ gốc dưới thân tới chóp cuộn
AXES = {
    "l": [(520, 630), (470, 680), (430, 722), (382, 776), (330, 828), (282, 868), (258, 930), (232, 1000), (185, 1040),
          (125, 1040), (78, 1005), (56, 950), (62, 902), (90, 874), (118, 878), (128, 902)],
    "m": [(648, 620), (642, 700), (635, 780), (630, 870), (602, 940), (585, 1000), (590, 1060), (618, 1110), (668, 1145),
          (730, 1150), (790, 1108), (822, 1050), (812, 1000), (775, 980), (735, 990), (716, 1012)],
    "r": [(812, 650), (860, 700), (905, 765), (930, 835), (945, 905), (965, 990), (1005, 1030), (1060, 1060),
          (1110, 1063), (1160, 1045), (1200, 1000), (1210, 950), (1190, 905), (1150, 888), (1118, 905), (1110, 935)],
}

# Các lưỡi gai thuộc về thân dù nằm dưới CUT_Y (đứng yên theo đầu, không theo xúc tu)
BODY_BLADES = [
    [(525, 560), (600, 560), (590, 610), (560, 650), (530, 690), (512, 705), (500, 698), (505, 650), (515, 610)],
    [(700, 600), (775, 600), (748, 650), (738, 676), (730, 676), (718, 645)],
    [(755, 645), (800, 650), (835, 690), (858, 735), (860, 770), (845, 770), (815, 735), (775, 705), (752, 675)],
]
EYE_BOX = (615, 390, 845, 612)   # x0, y0, x1, y1 vùng tìm mắt

# Trục đầu (áo bạch tuộc): từ chỗ nối với xúc tu lên tới đỉnh. Khớp ở chỗ nối, ngang tâm mắt, giữa đầu và đỉnh đầu:
# 3 xương để đầu mềm, ngả theo quán tính như áo bạch tuộc thật. Mắt gắn vào xương thứ 2 (khớp ngang tâm mắt)
MANTLE_AXIS = ((645, CUT_Y), (625, 30))
MANTLE_JOINTS_Y = [CUT_Y, None, 274, 30]     # None = ngang tâm mắt (tính sau khi tìm mắt)

# Vây quanh đầu: xương toả tròn từ tâm đầu ra viền, trong khoảng góc FIN_SECTOR (độ, 0 = sang phải, 90 = lên trên),
# chừa phía dưới là chỗ nối xúc tu. Phần viền từ FIN_INNER bán kính trở ra đi theo xương vây
HEAD_CENTER = (630, 340)
FIN_COUNT = 12
FIN_SECTOR = (-40, 220)
FIN_FADE = 15
FIN_INNER = 0.58


def polygon_mask(shape, poly):
    from PIL import ImageDraw
    img = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(img).polygon(poly, fill=1)
    return np.asarray(img).astype(bool)


def catmull(points, per_segment=40):
    p = np.array(points, float)
    p = np.vstack([2 * p[0] - p[1], p, 2 * p[-1] - p[-2]])
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for t in np.linspace(0, 1, per_segment, endpoint=False):
            out.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(p[-2])
    return np.array(out)


def resample(curve, count):
    """count điểm cách đều nhau theo chiều dài cung."""
    seg = np.linalg.norm(np.diff(curve, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    targets = np.linspace(0, s[-1], count)
    return np.stack([np.interp(targets, s, curve[:, 0]), np.interp(targets, s, curve[:, 1])], 1), s[-1]


def geodesic_param(mask, curve):
    """Mỗi pixel trong mask nhận tham số chiều dài (pixel) của điểm trục gần nhất, đo theo đường đi bên trong mask
    (không nhảy qua khe hở giữa các vòng cuộn). Ngoài mask: lấy theo pixel trong mask gần nhất."""
    h, w = mask.shape
    seg = np.linalg.norm(np.diff(curve, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    param = np.full(mask.shape, -1.0)
    dist = np.full(mask.shape, np.inf)
    queue = deque()
    for (x, y), t in zip(curve, s):
        xi, yi = int(round(x)), int(round(y))
        if 0 <= xi < w and 0 <= yi < h and dist[yi, xi] > 0:
            dist[yi, xi] = 0
            param[yi, xi] = t
            queue.append((yi, xi))
    # BFS 8 hướng (khoảng cách xấp xỉ), đủ để phân vùng theo trục
    steps = [(-1, 0, 1.0), (1, 0, 1.0), (0, -1, 1.0), (0, 1, 1.0),
             (-1, -1, 1.414), (-1, 1, 1.414), (1, -1, 1.414), (1, 1, 1.414)]
    while queue:
        y, x = queue.popleft()
        d0 = dist[y, x]
        for dy, dx, c in steps:
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and dist[ny, nx] > d0 + c + 1e-6:
                dist[ny, nx] = d0 + c
                param[ny, nx] = param[y, x]
                queue.append((ny, nx))
    known = param >= 0
    _, (iy, ix) = ndi.distance_transform_edt(~known, return_indices=True)
    return param[iy, ix]


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


# ---------------------------------------------------------------- nạp ảnh, hệ toạ độ

src = np.asarray(Image.open(SRC).convert("RGBA")).astype(np.float32)
rgb, alpha = src[..., :3], src[..., 3]
H, W = alpha.shape
ys, xs = np.nonzero(alpha > 8)
SRC_PPU = (ys.max() + 1 - ys.min()) / HEIGHT_UNITS        # pixel ảnh gốc mỗi unit
GROUND_Y = ys.max() + 1
CENTER_X = (xs.min() + xs.max() + 1) / 2
SCALE = PPU / SRC_PPU
yy, xx = np.mgrid[0:H, 0:W]


def to_rig(x, y):
    return [round(float((x - CENTER_X) / SRC_PPU), 5), round(float((GROUND_Y - y) / SRC_PPU), 5)]


print(f"ảnh gốc {W}x{H}, {SRC_PPU:.1f} px/unit → xuất {PPU} px/unit (x{SCALE:.3f})")

# ---------------------------------------------------------------- tách thân / xúc tu

blades = np.zeros((H, W), bool)
for poly in BODY_BLADES:
    blades |= polygon_mask((H, W), poly)

visible = alpha > 2
body_weight = np.clip((CUT_Y - yy) / FADE, 0, 1)
body_weight[blades] = 1.0

# Hạt giống: 3 khối đặc rời nhau bên dưới y = 720; rồi loang trong mask lên tới EXT_TOP
solid = alpha > 128
solid[:720] = False
labels, n = ndi.label(solid)
sizes = ndi.sum(solid, labels, range(1, n + 1))
big = [int(i) + 1 for i in np.argsort(-sizes)[:3]]
centers = [xx[labels == i].mean() for i in big]
names = ["l", "m", "r"]
owner = np.full((H, W), -1, int)
for k, i in enumerate(sorted(big, key=lambda i: centers[big.index(i)])):
    owner[labels == i] = k

region = visible & (yy >= EXT_TOP)
queue = deque(zip(*np.nonzero(owner >= 0)))
while queue:
    y, x = queue.popleft()
    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        ny, nx = y + dy, x + dx
        if 0 <= ny < H and 0 <= nx < W and region[ny, nx] and owner[ny, nx] < 0:
            owner[ny, nx] = owner[y, x]
            queue.append((ny, nx))
# Quầng sáng rời rạc phía dưới: theo khối gần nhất
lost = visible & (yy >= CUT_Y - FADE) & (owner < 0)
_, (iy, ix) = ndi.distance_transform_edt(owner < 0, return_indices=True)
owner[lost] = owner[iy, ix][lost]

# Phần gốc giấu dưới thân chỉ lấy chỗ thân đặc (không lòi ra ngoài viền thân khi đứng yên)
body_core = ndi.binary_erosion(alpha > 250, iterations=4)
hidden = (yy < CUT_Y) & ~(body_core | (yy >= CUT_Y - FADE))
owner[hidden & ~blades] = -1
owner[(yy < CUT_Y - FADE) & ~body_core] = -1

# ---------------------------------------------------------------- mắt

ex0, ey0, ex1, ey1 = EYE_BOX
box = (slice(ey0, ey1), slice(ex0, ex1))
mx, mn = rgb.max(2), rgb.min(2)
sat = mx - mn
eye_color = (sat > 70) & (alpha > 200)
eye_color[:ey0] = eye_color[ey1:] = False
eye_color[:, :ex0] = eye_color[:, ex1:] = False
lab, n = ndi.label(eye_color)
sizes = ndi.sum(eye_color, lab, range(1, n + 1))
# Con ngươi: khe dọc xanh than. Đầu khe chạm viền tối của tròng nên không tách theo khối được:
# dò từng hàng dải tối quanh trục khe, giới hạn trong dáng thấu kính (rộng nhất ở giữa, nhọn hai đầu)
SLIT_TOP, SLIT_BOTTOM, SLIT_HALF = (750, 406), (742, 568), 17
lens = np.zeros((H, W), bool)
for y in range(SLIT_TOP[1], SLIT_BOTTOM[1] + 1):
    u = (y - SLIT_TOP[1]) / (SLIT_BOTTOM[1] - SLIT_TOP[1])
    cx = SLIT_TOP[0] + (SLIT_BOTTOM[0] - SLIT_TOP[0]) * u
    half = SLIT_HALF * math.sin(math.pi * u) ** 0.8 + 1.5
    lens[y, int(cx - half):int(cx + half) + 1] = True

eye = lab == int(np.argmax(sizes)) + 1
eye = ndi.binary_fill_holes(ndi.binary_closing(eye | lens, iterations=4))
eye = ndi.binary_opening(eye, iterations=3)
eye = ndi.binary_fill_holes(eye | lens)
eye_soft = np.clip(ndi.gaussian_filter(eye.astype(np.float32), 1.2) * 1.6 - 0.3, 0, 1)
eye_soft[ndi.binary_erosion(eye, iterations=2)] = 1
eys, exs = np.nonzero(eye)
EYE_CENTER = ((exs.min() + exs.max() + 1) / 2, (eys.min() + eys.max() + 1) / 2)

pupil = (rgb[..., 0] < 24) & (rgb[..., 1] < 75) & lens & eye
pupil = ndi.binary_fill_holes(ndi.binary_closing(pupil, iterations=2)) & eye
pupil_fill = ndi.binary_dilation(pupil, iterations=4) & eye     # cả viền sáng quanh khe đi theo con ngươi
pys, pxs = np.nonzero(pupil)
PUPIL_CENTER = ((pxs.min() + pxs.max() + 1) / 2, (pys.min() + pys.max() + 1) / 2)

# Tròng không con ngươi: mỗi hàng nội suy màu giữa mép trái và mép phải của con ngươi
iris = rgb.copy()
for y in np.unique(np.nonzero(pupil_fill)[0]):
    cols = np.nonzero(pupil_fill[y])[0]
    left, right = cols.min() - 1, cols.max() + 1
    if not (eye[y, left] and eye[y, right]):
        left = left if eye[y, left] else right
        right = right if eye[y, right] else left
    for x in cols:
        u = (x - left) / max(right - left, 1)
        iris[y, x] = rgb[y, left] * (1 - u) + rgb[y, right] * u
iris = np.where(pupil_fill[..., None], ndi.gaussian_filter(iris, (1.5, 1.5, 0)), iris)
iris = np.where(pupil_fill[..., None], iris, rgb)

pupil_alpha = np.clip(ndi.gaussian_filter(pupil_fill.astype(np.float32), 1.0) * 1.5 - 0.2, 0, 1)

# Mí mắt (lộ ra khi chớp): loang màu giáp quanh mắt vào trong, tối dần về đường khép ở giữa
lid = rgb.copy()
inside = ndi.binary_dilation(eye, iterations=2)
sub = (slice(ey0 - 10, ey1 + 10), slice(ex0 - 10, ex1 + 10))
patch, known = rgb[sub].copy(), ~ndi.binary_dilation(eye, iterations=9)[sub]
patch[~known] = rgb[sub][known].mean(0)
for _ in range(500):
    patch = np.where(known[..., None], rgb[sub], ndi.uniform_filter(patch, size=(3, 3, 1)))
lid[sub] = patch
ring = ndi.binary_dilation(eye, iterations=26) & ~ndi.binary_dilation(eye, iterations=12) & (alpha > 250)
armor = np.median(rgb[ring], axis=0)                       # màu giáp xám quanh mắt
seam_y = EYE_CENTER[1] + 2
half_h = max((eys.max() - eys.min()) / 2, 1)
dy = (yy - seam_y) / half_h
# mí trên sáng (giáp hứng sáng từ trên), mí dưới tối hơn, sát đường khép tối nhất
shade = np.where(dy < 0, 1.05 - 0.45 * np.clip(1 + dy, 0, 1) ** 3, 0.75 - 0.3 * np.clip(1 - dy, 0, 1) ** 3)
base = lid * 0.4 + armor * 0.6
lid = np.where(inside[..., None], base * shade[..., None], lid)
seam = np.exp(-((yy - seam_y) / 2.0) ** 2) * inside
glint = np.exp(-((yy - seam_y + 4.5) / 1.6) ** 2) * inside
lid = lid * (1 - 0.85 * seam[..., None]) + np.array([170, 160, 230]) * 0.45 * glint[..., None]
lid = np.clip(lid, 0, 255)

# ---------------------------------------------------------------- xuất ảnh bộ phận

parts = []


def export(name, rgba, pivot_src=None, extra=None):
    x0, y0, x1, y1 = crop_box(rgba[..., 3])
    out = premultiplied_resize(rgba[y0:y1, x0:x1], SCALE)
    Image.fromarray(out, "RGBA").save(f"{OUT}/{name}.png")
    h, w = out.shape[:2]
    sx, sy = (x1 - x0) / w, (y1 - y0) / h          # pixel gốc mỗi pixel xuất (sau làm tròn)
    if pivot_src is None:
        pivot_src = ((x0 + x1) / 2, (y0 + y1) / 2)
    pivot = [round(float((pivot_src[0] - x0) / (x1 - x0)), 5), round(float(1 - (pivot_src[1] - y0) / (y1 - y0)), 5)]
    entry = {"name": name, "file": f"{name}.png", "size": [int(w), int(h)], "pivot": pivot, "position": to_rig(*pivot_src)}
    if extra:
        entry.update(extra(out, (x0, y0, sx, sy)))
    parts.append(entry)
    print(f"  {name}.png {w}x{h} pivot {pivot} tại {entry['position']}")
    return out


def tent(t, joint_t, first=0):
    """Trọng số "lều" quanh các khớp có tham số joint_t: [(xương, trọng số)…]. Xương j nối khớp j → j+1."""
    count = len(joint_t) - 1
    j = 0
    while j < count - 1 and t >= joint_t[j + 1]:
        j += 1
    if t <= joint_t[0]:
        return [(first, 1.0)]
    if j >= count - 1:
        return [(first + count - 1, 1.0)]
    u = float((t - joint_t[j]) / (joint_t[j + 1] - joint_t[j]))
    return [(first + j, 1 - u), (first + j + 1, u)]


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


# Đầu: tham số = hình chiếu lên trục đầu (đo từ chỗ nối lên đỉnh)
(mx0, my0), (mx1, my1) = MANTLE_AXIS
axis_dir = np.array([mx1 - mx0, my1 - my0], float)
axis_len = np.linalg.norm(axis_dir)
axis_dir /= axis_len
mantle_y = [EYE_CENTER[1] if y is None else y for y in MANTLE_JOINTS_Y]
mantle_t = [(y - my0) / axis_dir[1] for y in mantle_y]
mantle_joints = [(mx0 + axis_dir[0] * t, my0 + axis_dir[1] * t) for t in mantle_t]
MANTLE_BONES = len(mantle_joints) - 1

# Vây quanh đầu: FIN_COUNT xương toả tròn từ tâm đầu, mỗi xương từ FIN_INNER bán kính viền ra tới viền.
# Chỉ phần viền ngoài (các lưỡi gai) đi theo chúng; mặt, mắt, chỗ nối xúc tu đứng yên.
fin_mask = body_alpha_mask = (alpha * body_weight) > 128
eye_distance = ndi.distance_transform_edt(~eye)


def fin_angle(i):
    a0, a1 = FIN_SECTOR
    return a0 + (a1 - a0) * i / (FIN_COUNT - 1)


def edge_radius(angle):
    dx, dy = math.cos(math.radians(angle)), -math.sin(math.radians(angle))   # góc theo trục y hướng lên
    last = 0
    for r in range(0, 900):
        x, y = int(HEAD_CENTER[0] + dx * r), int(HEAD_CENTER[1] + dy * r)
        if not (0 <= x < W and 0 <= y < H):
            break
        if fin_mask[y, x]:
            last = r
    return last


fin_edges = [edge_radius(fin_angle(i)) for i in range(FIN_COUNT)]


def fin_point(angle, radius):
    return (HEAD_CENTER[0] + math.cos(math.radians(angle)) * radius,
            HEAD_CENTER[1] - math.sin(math.radians(angle)) * radius)


def mantle_param_at(x, y):
    return (x - mx0) * axis_dir[0] + (y - my0) * axis_dir[1]


def mantle_bone_at(x, y):
    t = mantle_param_at(x, y)
    j = 0
    while j < MANTLE_BONES - 1 and t >= mantle_t[j + 1]:
        j += 1
    return j


def body_weights(x, y):
    pairs = tent(mantle_param_at(x, y), mantle_t)
    # Góc / bán kính so với tâm đầu → phần nào thuộc vây
    dx, dy = x - HEAD_CENTER[0], HEAD_CENTER[1] - y
    angle = math.degrees(math.atan2(dy, dx))
    a0, a1 = FIN_SECTOR
    while angle < a0 - 180:
        angle += 360
    while angle > a0 + 180:
        angle -= 360
    if angle < a0 - 90:
        angle += 360
    step = (a1 - a0) / (FIN_COUNT - 1)
    f = (angle - a0) / step
    fade = 1.0
    if f < 0:
        fade = max(0.0, 1 + f * step / FIN_FADE)
        f = 0.0
    elif f > FIN_COUNT - 1:
        fade = max(0.0, 1 - (f - (FIN_COUNT - 1)) * step / FIN_FADE)
        f = float(FIN_COUNT - 1)
    k = min(int(f), FIN_COUNT - 2)
    u = f - k
    edge = fin_edges[k] * (1 - u) + fin_edges[k + 1] * u
    inner = edge * FIN_INNER
    radius = math.hypot(dx, dy)
    ramp = min(max((radius - inner) / max(edge - inner, 1), 0.0), 1.0)
    ramp = ramp * ramp * (3 - 2 * ramp)
    xi, yi = min(int(x), W - 1), min(int(y), H - 1)
    away_from_eye = min(max((eye_distance[yi, xi] - 8) / 40, 0.0), 1.0)
    above_neck = min(max((CUT_Y - 40 - y) / 40, 0.0), 1.0)
    weight = ramp * fade * away_from_eye * above_neck
    if weight <= 0:
        return pairs
    fins = [(MANTLE_BONES + k, weight * (1 - u)), (MANTLE_BONES + k + 1, weight * u)]
    return [(b, wt * (1 - weight)) for b, wt in pairs] + fins


def body_rig(out, origin):
    specs = [(f"MANTLE_{b:02d}", mantle_joints[b], mantle_joints[b + 1], b - 1) for b in range(MANTLE_BONES)]
    for i in range(FIN_COUNT):
        angle = fin_angle(i)
        base = fin_point(angle, fin_edges[i] * FIN_INNER)
        tip = fin_point(angle, fin_edges[i])
        specs.append((f"FIN_{i:02d}", base, tip, mantle_bone_at(*base)))
    data, nv, nt = skin_data(out, origin, specs, body_weights, BODY_GRID)
    print(f"    đầu: {MANTLE_BONES} xương đầu + {FIN_COUNT} xương vây, {nv} đỉnh, {nt} tam giác")
    data.update({"order": 10, "eyeBone": 1, "chainBones": MANTLE_BONES,
                 "joints": [c for p in mantle_joints for c in to_rig(*p)]})
    return data


body_rgb = np.where(inside[..., None], lid, rgb)
body_alpha = alpha * body_weight
export("body", np.dstack([body_rgb, body_alpha]), extra=body_rig)
export("eye", np.dstack([iris, alpha * eye_soft]), pivot_src=EYE_CENTER, extra=lambda out, o: {"order": 11})
export("pupil", np.dstack([rgb, 255 * pupil_alpha * (alpha / 255)]), pivot_src=PUPIL_CENTER,
       extra=lambda out, o: {"order": 12})


def tentacle_rig(key, mask_src):
    """Xương dọc trục xúc tu (cách đều) + lưới + trọng số theo vị trí dọc trục đo bên trong hình xúc tu."""
    curve = catmull(AXES[key])
    joints, length = resample(curve, BONES + 1)
    step = length / BONES
    param = geodesic_param(mask_src, curve)

    def make(out, origin):
        joint_t = [j * step for j in range(BONES + 1)]
        specs = [(f"{key.upper()}_{b:02d}", tuple(joints[b]), tuple(joints[b + 1]), b - 1) for b in range(BONES)]

        def param_at(x, y):
            return param[min(int(y), H - 1), min(int(x), W - 1)]

        def ok(corners):
            ts = [param_at(x, y) for x, y in corners]
            return max(ts) - min(ts) <= 2.2 * step

        data, nv, nt = skin_data(out, origin, specs, lambda x, y: tent(param_at(x, y), joint_t), GRID, ok)
        data["joints"] = [c for p in joints for c in to_rig(*p)]
        # Điểm chạm đất: đáy phần đặc của xúc tu
        solid_ys, solid_xs = np.nonzero(mask_src & (alpha > 200))
        k = np.argmax(solid_ys)
        contact = (solid_xs[k], solid_ys[k])
        contact_t = param[contact[1], contact[0]]
        print(f"    {key}: dài {length / SRC_PPU:.2f} unit, {nv} đỉnh, {nt} tam giác, chạm đất ở khớp {contact_t / step:.1f}")
        data.update({"order": 8 if key == "m" else 7,
                     "contact": to_rig(*contact),
                     "contactJoint": round(float(contact_t / step), 3)})
        return data

    return make


for k, key in enumerate(names):
    mine = owner == k
    rgba = np.dstack([rgb, alpha * mine])
    # Chỗ lưỡi gai của thân đè lên: thay bằng màu xúc tu xung quanh để khi xúc tu cử động không lộ gai thứ hai
    covered = mine & blades
    if covered.any():
        _, (iy, ix) = ndi.distance_transform_edt(covered | ~mine, return_indices=True)
        rgba[covered, :3] = rgb[iy, ix][covered]
        rgba[covered, :3] = ndi.gaussian_filter(rgba[..., :3], (2, 2, 0))[covered]
    export(f"tentacle_{key}", rgba, extra=tentacle_rig(key, mine | (ndi.binary_dilation(mine, iterations=3) & visible)))

rig = {
    "pixelsPerUnit": PPU,
    "heightUnits": HEIGHT_UNITS,
    "eyeCenter": to_rig(*EYE_CENTER),
    "pupilCenter": to_rig(*PUPIL_CENTER),
    "parts": parts,
}
with open(f"{OUT}/rig.json", "w") as f:
    json.dump(rig, f, separators=(",", ":"))
print("rig.json:", len(parts), "bộ phận")
