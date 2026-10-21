"""
Tách boss sư tử từ tấm bộ phận Docs/boss_su_tu/su_tu/lion_component.png để gắn xương (2D Animation) trong Unity.

    python3 Tools/art/lion_rig.py [--sheet đường/dẫn/lion_component.png] [--preview ảnh_xem.png]

Cần Pillow, numpy, scipy. Chạy lại bao nhiêu lần cũng được (ghi đè Assets/_Project/Art/Lion/Rig), rồi trong Unity chọn
menu Octopus → Dựng lại sư tử (hoặc chạy lại Setup) để nạp ảnh + xương mới.

Con sư tử ráp sẵn to nhất trên tấm ảnh (hàng 1, con nhìn nghiêng thứ 3, quay sang phải) là mẫu: cắt nó thành từng lớp theo
đa giác (toạ độ pixel tấm ảnh, ghi ở các bảng bên dưới; thay tấm ảnh khác thì sửa các đa giác này):
    torso      thân (hông + ngực) liền một mảnh, 2 xương Hips → Chest để cong lưng / chồm lên
    mane       bờm lửa (cả chùm lông dưới cằm), 1 xương gốc + 6 xương toả ra các chùm lửa để lay như lửa
    head       đầu (mặt, sừng, hàm) không xương, xoay quanh cổ
    head_roar  đầu há miệng gầm (lấy từ đầu số 3 hàng 2, xoay / thu cho trùng mắt và mũi với đầu thường)
    tail       đuôi 3 đoạn + chùm lửa ở ngọn (4 xương)
    leg_nf / leg_ff / leg_nh / leg_fh   4 chân (trước gần / trước xa / sau gần / sau xa), mỗi chân 3 xương:
               đùi (gốc → khớp), cẳng (khớp → cổ chân), bàn (cổ chân → đầu móng)
    flame_*    vài lọn lửa rời (hàng 2) cắm thêm quanh bờm, mỗi lọn 3 xương uốn như ngọn lửa
    glow       quầng sáng tròn (vẽ bằng code) cho mắt và hào quang nổi giận
Chỗ bị lớp trên che (lưng dưới bờm, ngực sau chân trước gần, cổ sau đầu, gốc đuôi, đầu đùi giấu trong thân) được tô bù
bằng màu xung quanh + vân lấy từ chỗ khác của chính lớp đó, để khi bộ phận cử động không lộ lỗ.

Ra (Assets/_Project/Art/Lion/Rig):
    *.png       ảnh từng lớp, phóng UP lần so với tấm gốc
    rig.json    vị trí lớp (unit tranh: gốc ở mặt sàn giữa hai bàn chân, x sang phải, y lên trên), xương / lưới / trọng số
                (pixel ảnh lớp, gốc dưới-trái như ISpriteBoneDataProvider) và các điểm mốc (cổ, miệng, móng…)
Ảnh ráp thử: --preview (mặc định <thư mục tạm>/lion_preview/assemble.png): trái = ráp lại các lớp như tranh gốc,
phải = đầu gầm thay đầu thường + các khớp (chân, đuôi), khớp cổ, miệng, mắt, khung chiều cao
"""
import argparse
import json
import math
import tempfile
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

from rig_mesh import premultiplied_resize, skin_data

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SHEET = ROOT / "Docs/boss_su_tu/su_tu/lion_component.png"
OUT = ROOT / "Assets/_Project/Art/Lion/Rig"

SRC_PPU = 100.0           # pixel tấm ảnh mỗi unit tranh (con ráp sẵn cao ≈ 3.9 unit tranh)
UP = 1.5                  # ảnh xuất phóng to bấy nhiêu lần (Lanczos + làm nét nhẹ) cho đỡ nhoè khi boss to trên màn hình
PPU = SRC_PPU * UP        # pixel ảnh xuất mỗi unit tranh
ORIGIN = (940.0, 397.0)   # gốc tranh trên tấm ảnh: mặt sàn (đáy móng chân gần), giữa chân trước và chân sau
VIEW_AT = (950, 250)      # một điểm nằm trên thân con ráp sẵn (để nhận ra mảng màu của nó)
# Chiều cao con sư tử đứng = từ sàn tới điểm cao nhất của các lớp đã ráp (tính tự động, gồm cả lọn lửa cắm thêm)

# ---------------------------------------------------------------- đa giác cắt lớp (toạ độ tấm ảnh)

# Đầu: mặt, sừng, gai má, hàm. Phía trái (sau gáy) cắt sát sừng / gai má, phía phải rộng ra ngoài hình
HEAD = [(1097, 90), (1110, 86), (1124, 108), (1127, 93), (1140, 92), (1146, 86), (1158, 90), (1166, 108), (1180, 112),
        (1192, 135), (1199, 165), (1206, 190), (1208, 222), (1196, 246), (1180, 262), (1160, 264), (1146, 258),
        (1137, 244), (1128, 228), (1112, 217), (1092, 208), (1104, 197), (1110, 190), (1088, 182), (1100, 172),
        (1118, 163), (1124, 154), (1116, 138), (1104, 117)]
HEAD_PIVOT = (1128, 190)  # khớp cổ: đầu xoay quanh đây
MOUTH = (1176, 228)       # giữa miệng (gầm, phun lửa)
EYE = (1163, 186)
NOSE = (1199, 208)

# Bờm: cả chùm lửa trên lưng + quanh đầu + chùm lông dưới cằm. Mép dưới chạy theo chỗ lửa gặp lưng đen, vòng quanh
# gai vai, xuống theo mép chùm lông cằm
MANE = [(838, 0), (1215, 0), (1215, 262), (1150, 266), (1135, 266), (1128, 276), (1116, 290), (1103, 300),
        (1090, 292), (1074, 293), (1058, 292), (1049, 283), (1060, 266), (1070, 245), (1074, 224), (1068, 204),
        (1063, 186), (1062, 166), (1056, 178), (1052, 195), (1047, 212), (1043, 218), (1037, 207), (1032, 186),
        (1031, 165), (1036, 147), (1027, 150), (1018, 160), (1010, 171), (1000, 177), (985, 184), (967, 188),
        (950, 184), (935, 176), (917, 169), (900, 164), (885, 161), (870, 159), (855, 160), (838, 156)]
MANE_ROOT = (1080, 168)
# Lửa toả ra từ gốc bờm: (tên, đầu chùm lửa). Gai vai trong bờm giữ cứng theo gốc
MANE_FLAMES = [("Mane Top", (1000, 12)), ("Mane Crest", (935, 32)), ("Mane Back", (875, 78)),
               ("Mane Low", (860, 140)), ("Mane Front", (1105, 45)), ("Mane Ruff", (1100, 292))]
# Khi lao tới lửa vuốt ngược ra sau (1), lửa sau lưng thì hơi vểnh lên (âm); cùng thứ tự MANE_FLAMES
MANE_SWEEP = [1.0, 0.7, -0.4, -0.5, 1.0, 0.3]
MANE_RIGID = [[(1028, 140), (1068, 104), (1074, 120), (1062, 165), (1050, 222), (1030, 210), (1026, 165)]]
# Bù phần bờm sau đầu (lộ ra khi đầu ngẩng / cúi)
MANE_FILL = [[(1098, 98), (1118, 88), (1140, 86), (1160, 92), (1167, 110), (1162, 130), (1160, 160), (1152, 195), (1146, 230),
              (1140, 255), (1125, 262), (1105, 250), (1095, 215), (1092, 170), (1092, 130)]]

# Đuôi: từ gốc trên mông vòng xuống chùm lửa
TAIL = [(636, 236), (700, 236), (722, 168), (768, 138), (825, 142), (852, 146), (854, 168), (836, 176), (816, 186),
        (808, 200), (792, 206), (779, 216), (768, 234), (762, 258), (753, 280), (747, 296), (737, 312), (720, 330),
        (712, 368),
        (636, 368)]
TAIL_BONES = [("Tail 1", (842, 160)), ("Tail 2", (788, 184)), ("Tail 3", (755, 232)), ("Tuft", (728, 292)),
              (None, (652, 322))]
# Gốc đuôi giấu trong mông (lớp đuôi vẽ sau thân)
TAIL_FILL = [[(812, 172), (830, 160), (858, 156), (868, 170), (850, 186), (822, 192)]]

# Chân: (tên lớp, đa giác, bù đầu đùi giấu sau thân, xương: gốc → khớp → cổ chân → đầu móng, chân trước?, chân gần?)
# Tâm bước khi đi (x tấm ảnh): dáng tranh gốc chân dạng rộng, khi đi dồn về dưới vai / hông
WALK_X = {"leg_nf": 1036, "leg_ff": 1084, "leg_nh": 806, "leg_fh": 858}
LEGS = [
    ("leg_nh",
     [(732, 282), (748, 270), (770, 266), (790, 262), (806, 256), (810, 270), (816, 284), (826, 298), (838, 310),
      (852, 316), (846, 323), (830, 334), (820, 352), (818, 402), (730, 402)],
     [[(805, 250), (812, 228), (850, 224), (864, 262), (854, 316), (838, 315), (825, 305), (813, 289), (806, 268)]],
     [(842, 238), (770, 293), (778, 360), (809, 391)], False, True),
    ("leg_fh",
     [(822, 330), (836, 312), (852, 316), (866, 314), (880, 304), (887, 288), (889, 260), (908, 258), (922, 262),
      (918, 300), (910, 330), (912, 350), (930, 368), (932, 390), (836, 390), (830, 360)],
     [[(846, 300), (858, 240), (906, 238), (926, 256), (891, 262), (890, 288), (882, 308), (863, 319), (845, 321)]],
     [(878, 245), (852, 333), (870, 362), (918, 382)], False, False),
    ("leg_nf",
     [(946, 212), (958, 200), (975, 197), (992, 206), (1006, 216), (1024, 226), (1046, 240), (1053, 262),
      (1058, 290), (1064, 312), (1073, 334), (1088, 346), (1110, 352), (1130, 364), (1134, 404), (1026, 404),
      (1006, 372), (990, 352), (968, 330), (946, 306), (936, 280), (938, 240)],
     [],
     [(1012, 228), (1000, 280), (1060, 348), (1118, 393)], True, True),
    ("leg_ff",
     [(1048, 286), (1060, 268), (1090, 270), (1100, 292), (1116, 312), (1135, 338), (1162, 352), (1182, 372),
      (1182, 392), (1098, 392), (1088, 364), (1074, 336), (1060, 312)],
     [[(1052, 296), (1060, 236), (1102, 236), (1100, 292), (1080, 296)]],
     [(1076, 246), (1068, 300), (1120, 352), (1170, 384)], True, False),
]
NF_TOP_FEATHER = 2.5      # đầu chân trước gần nhoè vào ngực (lớp chân vẽ trước thân)
NF_TOP = [(930, 150), (1080, 150), (1080, 236), (1046, 236), (1012, 222), (992, 206), (975, 198), (958, 201),
          (930, 206)]

# Thân: dáng thân đầy đủ (kể cả chỗ bị che); chỗ không còn điểm ảnh gốc thì tô bù
TORSO_SHAPE = [(800, 214), (803, 190), (815, 172), (836, 161), (862, 156), (900, 152), (950, 151), (1000, 147),
               (1030, 136), (1060, 122), (1090, 116), (1114, 128), (1130, 150), (1138, 180), (1140, 215),
               (1132, 246), (1114, 270), (1094, 286), (1070, 292), (1050, 293), (1020, 288), (990, 278),
               (960, 266), (936, 258), (910, 258), (889, 260), (887, 282), (878, 300), (860, 311), (842, 313),
               (829, 303), (818, 286), (811, 266), (806, 244)]
TORSO_KEEP = [(760, 120), (1150, 100), (1150, 300), (760, 300)]
HIPS = (850, 222)         # khớp hông (xương Hips bắt đầu)
CHEST = (1012, 205)       # khớp ngực (xương Chest bắt đầu)
NECK = (1100, 178)        # cuối xương Chest (cổ)
SPINE_BLEND = (905, 985)  # dải chuyển trọng số hông → ngực theo trục lưng

# Đầu gầm: đầu số 3 hàng 2 (há miệng), cắt bỏ phần bờm phía sau, ráp cho mắt + mũi trùng đầu thường
ROAR_SOURCE = [(414, 440), (440, 430), (462, 426), (482, 430), (497, 440), (522, 455), (537, 470), (548, 486),
               (552, 500), (548, 516), (538, 532), (534, 556), (522, 576), (504, 582), (480, 577), (464, 562),
               (454, 542), (444, 526), (428, 521), (426, 504), (438, 495), (448, 480), (438, 470), (414, 452)]
ROAR_EYE = (493, 489)
ROAR_NOSE = (538, 498)
ROAR_SCALE = 1.04         # đầu gầm vẽ mặt ngắn hơn, nới thêm chút cho sừng / hàm cân với đầu thường
ROAR_TURN = 6.0           # ngẩng thêm (độ) so với khớp mắt-mũi, cho dáng gầm

# Lọn lửa rời (hàng 2) cắm thêm quanh bờm: (tên, khung trên tấm ảnh, gốc, ngọn, gốc đặt ở (tấm ảnh con ráp sẵn),
# ngọn đặt ở, lật ngang?, thứ tự: sau thân / trên bờm)
FLAMES = [
    ("flame_crest", (877, 432, 1014, 606), (888, 440), (985, 590), (985, 62), (925, 4), False, "back"),
    ("flame_back", (961, 418, 1109, 618), (972, 432), (1085, 600), (930, 112), (848, 72), False, "back"),
    ("flame_top", (1416, 446, 1527, 622), (1428, 456), (1505, 610), (1042, 62), (1050, 0), True, "back"),
    ("flame_low", (1252, 418, 1384, 615), (1262, 428), (1292, 608), (905, 150), (836, 128), True, "back"),
    ("flame_neck", (1109, 516, 1179, 599), (1116, 524), (1170, 590), (1092, 120), (1046, 82), False, "front"),
]

# Thứ tự vẽ (tương đối; Unity cộng thêm gốc dải thứ tự của boss)
# (12 để trống: chân trước xa giơ lên vả thì vẽ ở đây, trên bờm sau đầu)
ORDER = {"glow_aura": 0, "leg_fh": 2, "leg_ff": 3, "tail": 4, "leg_nh": 5, "flame_back": 6, "torso": 8, "mane": 10,
         "flame_front": 11, "head": 13, "head_roar": 14, "glow_eye": 15, "leg_nf": 17}
# Điểm bao thân / bờm / đầu (không tính đuôi) cho VisualBounds: mông, bụng, ngực, đỉnh bờm (None = điểm cao nhất của
# hình ráp, tính tự động), bờm sau, sừng, mũi, cằm
BOUNDS = [(790, 200), (930, 262), (1070, 295), None, (845, 110), (1100, 88), (1206, 210), (1170, 264)]

GRID = {"torso": 12, "mane": 10, "tail": 7, "leg": 7, "flame": 8}


def parse_args():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[1])
    ap.add_argument("--sheet", type=Path, default=DEFAULT_SHEET)
    ap.add_argument("--preview", type=Path, default=Path(tempfile.gettempdir()) / "lion_preview" / "assemble.png")
    ap.add_argument("--debug", type=Path, default=None, help="thư mục ghi ảnh soi từng lớp (tuỳ chọn)")
    return ap.parse_args()


ARGS = parse_args()

# ---------------------------------------------------------------- nạp ảnh

sheet = np.asarray(Image.open(ARGS.sheet).convert("RGBA")).astype(np.float32)
H, W = sheet.shape[:2]
labels, _ = ndi.label(sheet[..., 3] > 40)
# Viền mờ (alpha thấp) thuộc về mảng gần nhất, để không dính viền bộ phận bên cạnh
_, (iy, ix) = ndi.distance_transform_edt(labels == 0, return_indices=True)
owner = labels[iy, ix]
view = sheet.copy()
view[..., 3] *= owner == labels[VIEW_AT[1], VIEW_AT[0]]
VIEW_A = view[..., 3] / 255.0
VIEW_RGB = view[..., :3]
SHEET_A = sheet[..., 3] / 255.0


def to_rig(p):
    return ((p[0] - ORIGIN[0]) / SRC_PPU, (ORIGIN[1] - p[1]) / SRC_PPU)


def r5(v):
    return [round(float(c), 5) for c in v]


# ---------------------------------------------------------------- mặt nạ đa giác, tô bù

def poly_mask(points, feather=0.0, shape=None, ss=4):
    """Mặt nạ đa giác khử răng cưa (0..1), feather = độ nhoè mép (pixel)."""
    h, w = shape or (H, W)
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    pad = int(3 * feather) + 2
    x0, y0 = max(int(min(xs)) - pad, 0), max(int(min(ys)) - pad, 0)
    x1, y1 = min(int(max(xs)) + pad + 1, w), min(int(max(ys)) + pad + 1, h)
    out = np.zeros((h, w), np.float32)
    if x1 <= x0 or y1 <= y0:
        return out
    img = Image.new("L", ((x1 - x0) * ss, (y1 - y0) * ss), 0)
    ImageDraw.Draw(img).polygon([((x - x0) * ss, (y - y0) * ss) for x, y in points], fill=255)
    small = np.asarray(img.resize((x1 - x0, y1 - y0), Image.BOX), np.float32) / 255.0
    if feather > 0:
        small = ndi.gaussian_filter(small, feather)
    out[y0:y1, x0:x1] = small
    return out


def union(polys, feather=0.0):
    m = np.zeros((H, W), np.float32)
    for p in polys:
        m = np.maximum(m, poly_mask(p, feather))
    return m


def push_pull(rgb, weight):
    """Màu trơn lan từ chỗ đã biết (weight) ra chỗ chưa biết: trung bình có trọng số nhiều tầng, tầng mịn đè tầng thô."""
    result = None
    for sigma in (48, 24, 12, 6, 3, 1.5):
        den = ndi.gaussian_filter(weight, sigma)
        num = np.stack([ndi.gaussian_filter(rgb[..., c] * weight, sigma) for c in range(3)], -1)
        est = num / np.maximum(den, 1e-6)[..., None]
        if result is None:
            result = est
        else:
            k = np.clip(den / 0.25, 0.0, 1.0)[..., None]
            result = result * (1 - k) + est * k
    return result


def local_mean(rgb, weight, sigma):
    den = np.maximum(ndi.gaussian_filter(weight, sigma), 1e-6)
    return np.stack([ndi.gaussian_filter(rgb[..., c] * weight, sigma) / den for c in range(3)], -1)


def synthesize(rgb, known, region, seed, tile=24, keep_red=False):
    """Tô bù vùng region: màu trơn lan từ xung quanh (xa chỗ đã biết thì ngả dần về màu chung của lớp) + vân ghép từ
    các ô lấy ngẫu nhiên trong chỗ đặc của chính lớp (cửa sổ Hann chồng nhau nên không lộ mép ô)."""
    solid = (known > 0.97).astype(np.float32)
    redness = np.clip((rgb[..., 0] - np.maximum(rgb[..., 1], rgb[..., 2])) / 120.0, 0, 1)
    base_w = solid if keep_red else solid * (1.0 - 0.9 * redness)
    smooth = push_pull(rgb, base_w)
    pick = base_w > 0.5
    median = np.median(rgb[pick], axis=0) if pick.any() else np.array([30, 24, 28], np.float32)
    far = smoothstep(3.0, 18.0, ndi.distance_transform_edt(solid < 0.5))
    smooth = smooth * (1 - 0.7 * far[..., None]) + median * 0.7 * far[..., None]
    need = region > 0.02
    if not need.any():
        return smooth
    detail = (rgb - local_mean(rgb, solid, 3.0)) * solid[..., None]
    half = tile // 2
    donors = ndi.binary_erosion(solid > 0.5, iterations=half)
    dys, dxs = np.nonzero(donors)
    if len(dys) == 0:
        return smooth
    rng = np.random.default_rng(seed)
    ys, xs = np.nonzero(need)
    y0, y1, x0, x1 = ys.min() - half, ys.max() + half + 1, xs.min() - half, xs.max() + half + 1
    acc = np.zeros((y1 - y0 + tile, x1 - x0 + tile, 3), np.float32)
    wacc = np.zeros(acc.shape[:2], np.float32)
    win = np.outer(np.hanning(tile), np.hanning(tile)).astype(np.float32) + 1e-4
    for ty in range(0, y1 - y0, half):
        for tx in range(0, x1 - x0, half):
            k = rng.integers(len(dys))
            cy, cx = dys[k], dxs[k]
            acc[ty:ty + tile, tx:tx + tile] += detail[cy - half:cy + half, cx - half:cx + half] * win[..., None]
            wacc[ty:ty + tile, tx:tx + tile] += win
    texture = np.zeros_like(rgb)
    sy0, sx0 = max(y0, 0), max(x0, 0)
    sy1, sx1 = min(y1, H), min(x1, W)
    tex = acc / np.maximum(wacc, 1e-6)[..., None]
    texture[sy0:sy1, sx0:sx1] = tex[sy0 - y0:sy1 - y0, sx0 - x0:sx1 - x0]
    return np.clip(smooth + texture * 1.1, 0, 255)


def build_layer(name, keep, cuts=(), fills=(), keep_feather=0.6, cut_feather=0.7, fill_feather=0.6, src=None,
                outline=True, cut_masks=(), keep_red=False, fill_copy=False, largest=True):
    """Lớp RGBA (cỡ tấm ảnh): điểm ảnh gốc trong keep (một hay nhiều đa giác) trừ cuts, cộng phần tô bù trong fills.
    fill_copy: trong fills giữ luôn điểm ảnh gốc (vd đầu đùi giấu trong mông chép đúng mông), chỉ tô chỗ trống.
    largest: bỏ các mẩu rời (chỉ giữ mảng liền lớn nhất)."""
    src_rgb = VIEW_RGB if src is None else src[..., :3]
    src_a = VIEW_A if src is None else src[..., 3] / 255.0
    keeps = keep if isinstance(keep[0], list) else [keep]
    if fill_copy:
        keeps = keeps + list(fills)
    m = union(keeps, keep_feather)
    for c in cuts:
        m *= 1.0 - poly_mask(c, cut_feather)
    for c in cut_masks:
        m *= 1.0 - c
    known = src_a * m
    rgb = src_rgb.copy()
    if fills:
        fill = union(fills, fill_feather)
        region = fill * (1.0 - known)
        synth = synthesize(src_rgb, known, region, seed=sum(map(ord, name)), keep_red=keep_red)
        alpha = known + region
        mix = np.where(alpha > 1e-4, known / np.maximum(alpha, 1e-4), 1.0)[..., None]
        rgb = src_rgb * mix + synth * (1 - mix)
        if outline:
            # Mép hình mới (chỗ tô bù giáp nền trong suốt) kẻ viền tối như nét vẽ gốc
            solid = alpha > 0.5
            edge = solid & ~ndi.binary_erosion(solid, iterations=2)
            edge = ndi.gaussian_filter(edge.astype(np.float32), 0.7) * (region > 0.3)
            rgb = rgb * (1 - 0.75 * edge[..., None]) + np.array([40, 8, 14], np.float32) * 0.75 * edge[..., None]
    else:
        alpha = known
    alpha = np.clip(alpha, 0, 1)
    if largest:
        lab, n = ndi.label(alpha > 0.25)
        if n > 1:
            sizes = np.bincount(lab.ravel())
            sizes[0] = 0
            main = ndi.binary_dilation(lab == sizes.argmax(), iterations=2)
            alpha = alpha * main
    out = np.dstack([rgb, alpha * 255.0])
    out[..., 3][out[..., 3] < 2] = 0
    if ARGS.debug:
        ARGS.debug.mkdir(parents=True, exist_ok=True)
        bg = Image.new("RGBA", (W, H), (120, 160, 200, 255))
        bg.alpha_composite(Image.fromarray(out.astype(np.uint8), "RGBA"))
        bb = crop_bounds(out[..., 3], 6)
        bg.crop(bb).save(ARGS.debug / f"layer_{name}.png")
    return out


def crop_bounds(alpha, margin):
    ys, xs = np.nonzero(alpha > 2)
    return (max(int(xs.min()) - margin, 0), max(int(ys.min()) - margin, 0),
            min(int(xs.max()) + 1 + margin, W), min(int(ys.max()) + 1 + margin, H))


# ---------------------------------------------------------------- trọng số

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
        d0 = dist[y, x]
        for dy, dx, c in steps:
            yy, xx = y + dy, x + dx
            if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and dist[yy, xx] > d0 + c:
                dist[yy, xx] = d0 + c
                queue.append((yy, xx))
    return dist


def nearest_in(mask, p):
    ys, xs = np.nonzero(mask)
    i = np.argmin((xs - p[0]) ** 2 + (ys - p[1]) ** 2)
    return int(ys[i]), int(xs[i])


def smoothstep(a, b, x):
    k = np.clip((x - a) / (b - a), 0.0, 1.0)
    return k * k * (3 - 2 * k)


def chain_weight_maps(rgba, box, joints, blend=0.045):
    """Chuỗi xương joints[0] → … → joints[-1] (toạ độ tấm ảnh): tham số dọc chuỗi t (0 gốc, 1 ngọn) đo bằng đường đi
    trong hình, rồi chia trọng số cho từng xương, chuyển mượt quanh mỗi khớp. Trả về các bản đồ trọng số (cỡ box)."""
    x0, y0, x1, y1 = box
    a = rgba[y0:y1, x0:x1, 3] > 60
    lab, n = ndi.label(a)
    if n > 1:
        sizes = np.bincount(lab.ravel())
        sizes[0] = 0
        a = lab == sizes.argmax()
    local = [(p[0] - x0, p[1] - y0) for p in joints]
    root = nearest_in(a, local[0])
    tip = nearest_in(a, local[-1])
    d_root, d_tip = bfs(a, root), bfs(a, tip)
    with np.errstate(invalid="ignore"):
        t = d_root / np.maximum(d_root + d_tip, 1e-6)
    _, (ny, nx) = ndi.distance_transform_edt(~a, return_indices=True)
    t = t[ny, nx]
    t = np.where(np.isfinite(t), t, 0.0)
    ts = [0.0] + [float(t[nearest_in(a, p)]) for p in local[1:-1]] + [1.0]
    count = len(joints) - 1
    maps = []
    for i in range(count):
        lo = smoothstep(ts[i] - blend, ts[i] + blend, t) if i > 0 else np.ones_like(t)
        hi = 1.0 - smoothstep(ts[i + 1] - blend, ts[i + 1] + blend, t) if i < count - 1 else np.ones_like(t)
        maps.append(lo * hi)
    total = np.maximum(sum(maps), 1e-6)
    return [m / total for m in maps], ts


def sampler(maps, box):
    x0, y0, x1, y1 = box

    def weights_at(x, y):
        ix = int(np.clip(round(x - x0), 0, x1 - x0 - 1))
        iy = int(np.clip(round(y - y0), 0, y1 - y0 - 1))
        return [(i, float(m[iy, ix])) for i, m in enumerate(maps)]

    return weights_at


# ---------------------------------------------------------------- xuất

parts = []
images = {}


def export(name, rgba, box=None, pivot=None, bones=None, weights_at=None, grid=8, order=0, file=None, extra=None):
    """Cắt lớp theo box (toạ độ tấm ảnh), phóng UP lần, lưu PNG. pivot = điểm tấm ảnh làm gốc sprite (mặc định tâm)."""
    if box is None:
        box = crop_bounds(rgba[..., 3], 4)
    x0, y0, x1, y1 = box
    # Cạnh chẵn để ảnh phóng 1.5 lần ra số nguyên pixel, tâm không lệch
    if (x1 - x0) % 2:
        x1 += 1
    if (y1 - y0) % 2:
        y1 += 1
    crop = np.zeros((y1 - y0, x1 - x0, 4), np.float32)
    sx0, sy0, sx1, sy1 = max(x0, 0), max(y0, 0), min(x1, rgba.shape[1]), min(y1, rgba.shape[0])
    crop[sy0 - y0:sy1 - y0, sx0 - x0:sx1 - x0] = rgba[sy0:sy1, sx0:sx1]
    out = premultiplied_resize(crop, UP)
    file = file or f"{name}.png"
    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out, "RGBA").save(OUT / file)
    w, h = x1 - x0, y1 - y0
    if pivot is None:
        pivot = (x0 + w / 2, y0 + h / 2)
    entry = {"name": name, "file": file, "size": [out.shape[1], out.shape[0]],
             "pivot": [round((pivot[0] - x0) / w, 5), round(1 - (pivot[1] - y0) / h, 5)],
             "position": r5(to_rig(pivot)), "order": order}
    if bones:
        origin = (x0, y0, w / out.shape[1], h / out.shape[0])
        data, nv, nt = skin_data(out, origin, bones, weights_at, grid)
        entry.update(data)
        print(f"  {name}: {out.shape[1]}x{out.shape[0]}, {len(bones)} xương, {nv} đỉnh, {nt} tam giác")
    else:
        print(f"  {name}: {out.shape[1]}x{out.shape[0]}")
    if extra:
        entry.update(extra)
    parts.append(entry)
    images[name] = (out, entry, (x0, y0, x1, y1))
    return entry


print("Cắt lớp từ", ARGS.sheet)
leg_polys = {name: poly for name, poly, *_ in LEGS}
NF_FADE = ndi.gaussian_filter(poly_mask(NF_TOP), NF_TOP_FEATHER)

# Đầu
head = build_layer("head", HEAD, keep_feather=0.7)
export("head", head, pivot=HEAD_PIVOT, order=ORDER["head"])

# Đầu gầm: xoay / thu đầu số 3 hàng 2 cho mắt và mũi trùng đầu thường, đặt vào đúng chỗ trên tấm ảnh
roar_src = sheet.copy()
roar_src[..., 3] *= poly_mask(ROAR_SOURCE, 0.7)
d_dst = np.subtract(NOSE, EYE)
d_src = np.subtract(ROAR_NOSE, ROAR_EYE)
scale = math.hypot(*d_dst) / math.hypot(*d_src) * ROAR_SCALE
angle = math.atan2(d_dst[1], d_dst[0]) - math.atan2(d_src[1], d_src[0]) - math.radians(ROAR_TURN)
ca, sa = math.cos(angle) * scale, math.sin(angle) * scale
# điểm tấm ảnh p_dst = EYE + R·s·(p_src - ROAR_EYE)  →  ngược lại cho PIL
inv_s = 1.0 / scale
ia, ib = math.cos(-angle) * inv_s, math.sin(-angle) * inv_s
coeffs = (ia, -ib, ROAR_EYE[0] - (ia * EYE[0] - ib * EYE[1]),
          ib, ia, ROAR_EYE[1] - (ib * EYE[0] + ia * EYE[1]))
premult = roar_src.copy()
premult[..., :3] *= premult[..., 3:4] / 255.0
layers = [Image.fromarray(premult[..., c].astype(np.float32), "F").transform((W, H), Image.AFFINE, coeffs, Image.BICUBIC)
          for c in range(4)]
roar = np.dstack([np.asarray(l) for l in layers]).clip(0, 255)
roar[..., :3] = roar[..., :3] / np.maximum(roar[..., 3:4], 1e-3) * 255.0
roar[..., 3][roar[..., 3] < 2] = 0
export("head_roar", roar, pivot=HEAD_PIVOT, order=ORDER["head_roar"])

# Bờm: trừ đầu, tô bù phần bờm sau đầu
mane = build_layer("mane", MANE, cuts=[HEAD], fills=MANE_FILL, keep_feather=0.9, outline=False, keep_red=True)
mane_box = crop_bounds(mane[..., 3], 4)
mx0, my0, mx1, my1 = mane_box
gy, gx = np.mgrid[my0:my1, mx0:mx1].astype(np.float32)
rx, ry = gx - MANE_ROOT[0], gy - MANE_ROOT[1]
radius = np.hypot(rx, ry)
ang = np.arctan2(ry, rx)
reach = smoothstep(18, 70, radius)
flame_maps = []
for _, tip in MANE_FLAMES:
    ta = math.atan2(tip[1] - MANE_ROOT[1], tip[0] - MANE_ROOT[0])
    diff = np.angle(np.exp(1j * (ang - ta)))
    flame_maps.append(np.exp(-(diff / 0.42) ** 2))
flame_total = np.maximum(sum(flame_maps), 1e-6)
rigid = union(MANE_RIGID, 1.5)[my0:my1, mx0:mx1]
reach *= 1.0 - rigid
mane_maps = [1.0 - reach] + [reach * m / flame_total for m in flame_maps]
mane_bones = [("Mane", MANE_ROOT, (MANE_ROOT[0] - 30, MANE_ROOT[1]), -1)]
mane_bones += [(n, MANE_ROOT, tip, 0) for n, tip in MANE_FLAMES]
export("mane", mane, box=mane_box, bones=mane_bones, weights_at=sampler(mane_maps, mane_box), grid=GRID["mane"],
       order=ORDER["mane"])

# Đuôi
tail = build_layer("tail", TAIL, fills=TAIL_FILL, keep_feather=0.6, outline=False)
tail_box = crop_bounds(tail[..., 3], 4)
tail_pts = [p for _, p in TAIL_BONES]
tail_maps, tail_ts = chain_weight_maps(tail, tail_box, tail_pts)
tail_bones = [(TAIL_BONES[i][0], tail_pts[i], tail_pts[i + 1], i - 1) for i in range(len(TAIL_BONES) - 1)]
export("tail", tail, box=tail_box, bones=tail_bones, weights_at=sampler(tail_maps, tail_box), grid=GRID["tail"],
       order=ORDER["tail"])

# Chân
leg_info = []
for name, poly, fills, joints, front, near in LEGS:
    cuts = {"leg_ff": [leg_polys["leg_nf"], MANE], "leg_nh": [TAIL]}.get(name, [])
    # Chân sau: đầu đùi giấu trong mông chép đúng điểm ảnh mông; chân trước xa: đầu chân tô bù bằng vân chân
    layer = build_layer(name, poly, cuts=cuts, fills=fills, keep_feather=0.6, outline=False,
                        fill_copy=not front)
    if name == "leg_nf":
        # Đầu chân trước gần nhoè dần vào ngực (lớp chân vẽ trước thân, thân bên dưới giữ đúng điểm ảnh gốc ở dải nhoè)
        layer[..., 3] *= 1.0 - NF_FADE
    box = crop_bounds(layer[..., 3], 4)
    maps, ts = chain_weight_maps(layer, box, joints)
    bones = [("Upper", joints[0], joints[1], -1), ("Lower", joints[1], joints[2], 0), ("Paw", joints[2], joints[3], 1)]
    export(name, layer, box=box, bones=bones, weights_at=sampler(maps, box), grid=GRID["leg"], order=ORDER[name])
    leg_info.append({"part": name, "front": front, "near": near,
                     "joints": [c for p in joints for c in r5(to_rig(p))],
                     "walk": r5(to_rig((WALK_X[name], joints[2][1])))})

# Thân: trừ đầu, bờm, đuôi, 4 chân; tô bù cho đủ dáng thân
torso_cuts = [HEAD, MANE, TAIL] + [poly for name, poly, *_ in LEGS if name != "leg_nf"]
nf_cut = poly_mask(leg_polys["leg_nf"], 0.7) * (1.0 - NF_FADE)
torso = build_layer("torso", TORSO_KEEP, cuts=torso_cuts, fills=[TORSO_SHAPE], keep_feather=0.0, cut_masks=[nf_cut])
torso_box = crop_bounds(torso[..., 3], 4)
tx0, ty0, tx1, ty1 = torso_box
spine_dir = np.subtract(CHEST, HIPS) / np.hypot(*np.subtract(CHEST, HIPS))
gy, gx = np.mgrid[ty0:ty1, tx0:tx1].astype(np.float32)
along = (gx - HIPS[0]) * spine_dir[0] + (gy - HIPS[1]) * spine_dir[1] + HIPS[0]
w_chest = smoothstep(SPINE_BLEND[0], SPINE_BLEND[1], along)
torso_bones = [("Hips", HIPS, CHEST, -1), ("Chest", CHEST, NECK, 0)]
export("torso", torso, box=torso_box, bones=torso_bones, weights_at=sampler([1 - w_chest, w_chest], torso_box),
       grid=GRID["torso"], order=ORDER["torso"])


# Lọn lửa rời: đặt gốc / ngọn theo bảng, 3 xương dọc lọn
def place_flame(spec):
    name, box, root, tip, at_root, at_tip, flip, layer = spec
    src = sheet.copy()
    src[..., 3] *= (owner == labels[nearest_label_point(box)]).astype(np.float32) * poly_mask(
        [(box[0], box[1]), (box[2], box[1]), (box[2], box[3]), (box[0], box[3])])
    d_src = np.subtract(tip, root)
    d_dst = np.subtract(at_tip, at_root)
    s = math.hypot(*d_dst) / math.hypot(*d_src)
    a_src = math.atan2(d_src[1], d_src[0])
    a_dst = math.atan2(d_dst[1], d_dst[0])
    # Lật ngang quanh trục gốc → ngọn của lọn (để chiều cong hợp với bờm), rồi xoay / thu về chỗ đặt
    m_src = np.array([[1, 0, -root[0]], [0, 1, -root[1]], [0, 0, 1]], float)
    rot_to_x = np.array([[math.cos(-a_src), -math.sin(-a_src), 0], [math.sin(-a_src), math.cos(-a_src), 0], [0, 0, 1]])
    mirror = np.diag([1.0, -1.0 if flip else 1.0, 1.0])
    rot_dst = np.array([[math.cos(a_dst) * s, -math.sin(a_dst) * s, at_root[0]],
                        [math.sin(a_dst) * s, math.cos(a_dst) * s, at_root[1]], [0, 0, 1]])
    fwd = rot_dst @ mirror @ rot_to_x @ m_src
    inv = np.linalg.inv(fwd)
    # Vẽ lên khung đủ rộng quanh chỗ đặt (có thể ra ngoài tấm ảnh phía trên: dời khung)
    span = math.hypot(*d_dst) * 1.4 + 20
    cx, cy = (at_root[0] + at_tip[0]) / 2, (at_root[1] + at_tip[1]) / 2
    fx0, fy0 = int(cx - span), int(cy - span)
    size = int(2 * span)
    shift = np.array([[1, 0, fx0], [0, 1, fy0], [0, 0, 1]], float)
    m = inv @ shift
    coeffs = (m[0, 0], m[0, 1], m[0, 2], m[1, 0], m[1, 1], m[1, 2])
    premult = src.copy()
    premult[..., :3] *= premult[..., 3:4] / 255.0
    chans = [np.asarray(Image.fromarray(premult[..., c], "F").transform((size, size), Image.AFFINE, coeffs,
                                                                        Image.BICUBIC)) for c in range(4)]
    img = np.dstack(chans).clip(0, 255)
    img[..., :3] = img[..., :3] / np.maximum(img[..., 3:4], 1e-3) * 255.0
    img[..., 3][img[..., 3] < 2] = 0
    # Đưa về hệ toạ độ tấm ảnh (khung lớn hơn tấm ảnh): mảng riêng + gốc toạ độ
    return img, (fx0, fy0), fwd


def nearest_label_point(box):
    x0, y0, x1, y1 = box
    sub = labels[y0:y1, x0:x1]
    ids, counts = np.unique(sub[sub > 0], return_counts=True)
    best = ids[np.argmax(counts)]
    ys, xs = np.nonzero(sub == best)
    return ys[0] + y0, xs[0] + x0


flame_info = []
for spec in FLAMES:
    name, box, root, tip, at_root, at_tip, flip, layer_kind = spec
    img, (fx0, fy0), fwd = place_flame(spec)
    # Toạ độ "tấm ảnh" của lọn đã đặt = (fx0, fy0) + pixel trong img
    ys, xs = np.nonzero(img[..., 3] > 2)
    bx0, by0, bx1, by1 = xs.min() - 3, ys.min() - 3, xs.max() + 4, ys.max() + 4
    local = img[by0:by1, bx0:bx1]
    ox, oy = fx0 + bx0, fy0 + by0
    # 3 xương dọc lọn: chia đều đoạn gốc → ngọn đã đặt
    pts = [(at_root[0] + (at_tip[0] - at_root[0]) * k, at_root[1] + (at_tip[1] - at_root[1]) * k) for k in (0, 0.33, 0.66, 1.0)]
    lbox = (0, 0, local.shape[1], local.shape[0])
    maps, _ = chain_weight_maps(local, lbox, [(p[0] - ox, p[1] - oy) for p in pts], blend=0.12)

    def flame_weights(x, y, maps=maps, ox=ox, oy=oy, shape=local.shape):
        ix = int(np.clip(round(x - ox), 0, shape[1] - 1))
        iy = int(np.clip(round(y - oy), 0, shape[0] - 1))
        return [(i, float(m[iy, ix])) for i, m in enumerate(maps)]

    # Xuất: box tính trong hệ tấm ảnh nhưng ảnh nằm ngoài tấm → dùng mảng local với gốc (ox, oy)
    w_, h_ = local.shape[1], local.shape[0]
    if w_ % 2:
        local = np.pad(local, ((0, 0), (0, 1), (0, 0)))
    if h_ % 2:
        local = np.pad(local, ((0, 1), (0, 0), (0, 0)))
    w_, h_ = local.shape[1], local.shape[0]
    out = premultiplied_resize(local, UP)
    Image.fromarray(out, "RGBA").save(OUT / f"{name}.png")
    bones = [(f"Flame {i + 1}", pts[i], pts[i + 1], i - 1) for i in range(3)]
    origin = (ox, oy, w_ / out.shape[1], h_ / out.shape[0])
    data, nv, nt = skin_data(out, origin, bones, flame_weights, GRID["flame"])
    center = (ox + w_ / 2, oy + h_ / 2)
    entry = {"name": name, "file": f"{name}.png", "size": [out.shape[1], out.shape[0]], "pivot": [0.5, 0.5],
             "position": r5(to_rig(center)), "order": ORDER["flame_back" if layer_kind == "back" else "flame_front"]}
    entry.update(data)
    parts.append(entry)
    images[name] = (out, entry, (ox, oy, ox + w_, oy + h_))
    flame_info.append({"part": name, "front": layer_kind == "front"})
    print(f"  {name}: {out.shape[1]}x{out.shape[0]}, {nv} đỉnh")

# Quầng sáng tròn (mắt, hào quang): vẽ bằng code
g = np.linspace(-1, 1, 128)
gxx, gyy = np.meshgrid(g, g)
rr = np.sqrt(gxx ** 2 + gyy ** 2)
falloff = np.clip(1 - rr, 0, 1) ** 2.2
glow = np.dstack([np.full_like(rr, 255), np.full_like(rr, 255), np.full_like(rr, 255), falloff * 255]).astype(np.uint8)
Image.fromarray(glow, "RGBA").save(OUT / "glow.png")
parts.append({"name": "glow", "file": "glow.png", "size": [128, 128], "pivot": [0.5, 0.5], "position": [0, 0],
              "order": ORDER["glow_eye"]})

# Mốc
# Điểm cao nhất của hình ráp (alpha > 50%), không tính đầu gầm / quầng sáng
top_y, top_x = float("inf"), ORIGIN[0]
for e in parts:
    if e["name"] in ("glow", "head_roar"):
        continue
    out, _, (bx0, by0, bx1, by1) = images[e["name"]]
    rows = np.nonzero((out[..., 3] > 128).any(axis=1))[0]
    if len(rows) == 0:
        continue
    y = by0 + rows[0] / UP
    if y < top_y:
        cols = np.nonzero(out[rows[0], :, 3] > 128)[0]
        top_y, top_x = y, bx0 + cols.mean() / UP
height = (ORIGIN[1] - top_y) / SRC_PPU
BOUNDS = [p if p is not None else (top_x, top_y) for p in BOUNDS]
manifest = {
    "pixelsPerUnit": PPU,
    "height": round(height, 4),
    "parts": parts,
    "legs": leg_info,
    "flames": flame_info,
    "headPivot": r5(to_rig(HEAD_PIVOT)),
    "mouth": r5(to_rig(MOUTH)),
    "eye": r5(to_rig(EYE)),
    "maneRoot": r5(to_rig(MANE_ROOT)),
    "maneSweep": MANE_SWEEP,
    "bounds": [c for p in BOUNDS for c in r5(to_rig(p))],
}
(OUT / "rig.json").write_text(json.dumps(manifest, separators=(",", ":")))
print(f"rig.json: {len(parts)} lớp, cao {height:.2f} unit tranh, {PPU:g} px/unit")


# ---------------------------------------------------------------- ảnh ráp thử

def preview():
    scale_px = 120  # pixel ảnh xem mỗi unit tranh
    panel_w, panel_h = 6.4, 4.6
    canvas = Image.new("RGBA", (int(2 * panel_w * scale_px), int(panel_h * scale_px)), (46, 34, 40, 255))
    d = ImageDraw.Draw(canvas)

    def to_px(p, panel):
        return (3.3 + panel * panel_w) * scale_px + p[0] * scale_px, 4.35 * scale_px - p[1] * scale_px

    def stamp(img, entry, panel, tint=None):
        k = scale_px / PPU
        w, h = img.shape[1] * k, img.shape[0] * k
        px, py = to_px(entry["position"], panel)
        pil = Image.fromarray(img, "RGBA").resize((max(1, round(w)), max(1, round(h))), Image.LANCZOS)
        if tint is not None:
            arr = np.asarray(pil).astype(np.float32)
            arr[..., :3] *= tint
            pil = Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA")
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        layer.paste(pil, (round(px - entry["pivot"][0] * w), round(py - (1 - entry["pivot"][1]) * h)))
        canvas.alpha_composite(layer)

    for panel, skip in ((0, "head_roar"), (1, "head")):
        for e in sorted(parts, key=lambda e: e["order"]):
            if e["name"] in ("glow", skip):
                continue
            far = e["name"] in ("leg_ff", "leg_fh")
            stamp(images[e["name"]][0], e, panel, 0.62 if far else None)
        y0 = to_px((0, 0), panel)[1]
        d.line([(panel * panel_w * scale_px, y0), ((panel + 1) * panel_w * scale_px, y0)], fill=(150, 120, 110, 255))

    # Khớp, mốc (bảng phải)
    def dot(p, color, r=4):
        x, y = to_px(p, 1)
        d.ellipse([x - r, y - r, x + r, y + r], fill=color)

    for leg in leg_info:
        j = leg["joints"]
        pts = [to_px((j[i], j[i + 1]), 1) for i in range(0, len(j), 2)]
        d.line(pts, fill=(80, 255, 120, 255), width=2)
        for i in range(0, len(j), 2):
            dot((j[i], j[i + 1]), (80, 255, 120, 255))
    tail_px = [to_px(to_rig(p), 1) for p in tail_pts]
    d.line(tail_px, fill=(255, 200, 60, 255), width=2)
    for p in tail_pts:
        dot(to_rig(p), (255, 200, 60, 255))
    dot(to_rig(HEAD_PIVOT), (255, 255, 255, 255), 5)
    dot(to_rig(MOUTH), (0, 255, 255, 255), 5)
    dot(to_rig(EYE), (255, 0, 255, 255), 4)
    dot(to_rig(MANE_ROOT), (255, 120, 0, 255), 5)
    top = to_px((0, height), 1)[1]
    d.line([(panel_w * scale_px, top), (2 * panel_w * scale_px, top)], fill=(120, 160, 255, 255))
    canvas.save(ARGS.preview)
    print("->", ARGS.preview)


ARGS.preview.parent.mkdir(parents=True, exist_ok=True)
preview()
