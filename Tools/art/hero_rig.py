"""
Tách 3 nhân vật người (Xích Ảnh, Tử Tinh, Lam Hồn) từ tấm bộ phận Docs/nhan_vat_moi/<red|pink|blue>/<tên>.png để gắn
xương (2D Animation) trong Unity.

    python3 Tools/art/hero_rig.py [red] [pink] [blue] [--sheets thư/mục/nhan_vat_moi] [--preview thư/mục/ảnh_xem]

Tỉ lệ từng mảnh (scale) chỉnh theo các tấm dáng chiêu trong <tên>/{basic_attack,skill,special_skill} (vd Xích Ảnh đầu to,
chân ngắn, kiếm dài gần bằng người như tấm dáng, không như con ráp sẵn).

Không ghi tên thì làm cả 3. Cần Pillow, numpy, scipy. Chạy lại bao nhiêu lần cũng được (ghi đè
Assets/_Project/Art/Heroes/<Red|Pink|Blue>), rồi trong Unity dựng lại nhân vật (HeroRigBuilder.Build, menu / Setup) để
nạp ảnh + xương mới. Đổi tấm ảnh khác: sửa bảng HEROES bên dưới (khung / đa giác cắt, điểm khớp, tỉ lệ, thứ tự vẽ).

Mỗi tấm có một con ráp sẵn (mẫu tỉ lệ) và các mảnh rời. Script cắt từng mảnh rời (đầu, thân, tay trên, tay dưới, đùi,
cẳng + giày, vũ khí), đưa về tỉ lệ con ráp sẵn rồi ráp thành dáng đứng (tay buông, chân thẳng) quay sang phải:
    torso        thân (cổ áo, khăn, áo, đai, vạt áo) có xương Hips → Chest + vài xương vải (vạt áo / khăn / áo choàng)
                 để lay theo quán tính; chỗ mảnh thân vẽ sẵn tay / đùi thì cắt bỏ (tay, chân là mảnh riêng)
    head         đầu (mặt + tóc), xương Head + vài xương tóc toả ra quanh mặt (mặt giữ cứng)
    arm_upper_n / arm_lower_n   tay gần (cầm vũ khí, vẽ trước thân), arm_upper_f / arm_lower_f tay xa (sau thân, tối hơn)
    leg_upper_n / leg_lower_n   chân gần,  leg_upper_f / leg_lower_f chân xa
                 tay, chân là mảnh cứng, gốc ảnh ở khớp đầu (vai / khuỷu / háng / gối)
    weapon       vũ khí (gốc ảnh ở chỗ cầm)
    hair_back    (Tử Tinh) tóc dài sau lưng lấy từ con ráp sẵn (bỏ trượng, tay, thân; tô bù chỗ bị che), chuỗi 4 xương
    flame        (Lam Hồn) ngọn lửa hồn trên tay trái lấy từ con ráp sẵn (tách theo màu), chuỗi 3 xương
    glow         quầng sáng tròn (vẽ bằng code) cho vũ khí rực / lửa
Ra (Assets/_Project/Art/Heroes/<Red|Pink|Blue>):
    *.png        ảnh từng lớp
    rig.json     lớp (vị trí unit tranh: gốc ở mặt sàn giữa hai chân, x sang phải, y lên trên; đỉnh tóc cách sàn ≈ 2 unit),
                 xương / lưới / trọng số (pixel ảnh lớp, gốc dưới-trái như ISpriteBoneDataProvider), các điểm khớp
Ảnh ráp thử: <preview>/<tên>_assemble.png: trái = con ráp sẵn trên tấm ảnh, giữa = các lớp ráp lại (dáng đứng),
phải = như giữa kèm khớp, xương; <preview>/<tên>_parts.png: từng mảnh đã cắt kèm điểm khớp
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
OUT_ROOT = ROOT / "Assets/_Project/Art/Heroes"
OUT_PPU = 256.0           # pixel ảnh xuất mỗi unit tranh (đỉnh tóc cao ≈ 2 unit → ảnh cả con ≈ 512 pixel)
GRID = 8                  # bước lưới tam giác của lớp có xương (pixel ảnh xuất)

# Thứ tự vẽ (tương đối; Unity cộng gốc dải thứ tự của người chơi, xem HeroRigBuilder): sau → trước. Tay / chân xa sau
# thân, tay gần trước thân, vũ khí giữa đầu và tay gần (nắm tay đè lên chuôi). Các số để trống dành cho vũ khí đổi lớp
# khi diễn: 3 = sau thân (xoay người, vũ khí ở phía xa), 9 = sau đầu (giơ kiếm ra sau đầu), 13 = quầng sáng vũ khí
ORDER = {"hair_back": 0, "arm_upper_f": 1, "arm_lower_f": 2, "leg_upper_f": 4, "leg_lower_f": 5, "leg_upper_n": 6,
         "leg_lower_n": 7, "torso": 8, "head": 10, "flame": 11, "weapon": 12, "arm_upper_n": 14, "arm_lower_n": 15,
         "glow": 16}


def box(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


# Cán trượng của Tử Tinh trên con ráp sẵn (để cắt khỏi lớp tóc dài sau lưng)
STAFF_STROKE = [(140, 170), (150, 300), (170, 400), (195, 500), (215, 600), (232, 700), (245, 780), (256, 845)]


# ---------------------------------------------------------------- bảng nhân vật
#
# Toạ độ là pixel tấm ảnh gốc (1086 x 1448). Mỗi mảnh:
#   src      cách lấy điểm ảnh: comp = khung (lấy các mảng màu nằm gần trọn trong khung), poly = đa giác (kèm seed = một
#            điểm trên mảng màu cần lấy, để không dính mảnh bên cạnh)
#   cuts     đa giác cắt bỏ; strokes = [(đường gấp khúc, bán kính)] cắt bỏ (vd cán trượng); fills = đa giác tô bù
#   flip     lật ngang (vd giày mũi chĩa sang trái → sang phải); scale = tỉ lệ so với con ráp sẵn
#   joints   điểm khớp: tay / chân a = khớp đầu (vai, khuỷu, háng, gối), b = khớp cuối (khuỷu, chỗ cầm, gối, đế giày)
# Ráp: thân đặt trước, đầu theo cổ, tay theo vai, chân theo háng, cẳng theo gối, vũ khí theo chỗ cầm của tay gần.
# ref: con ráp sẵn (khung, đỉnh tóc, mặt sàn) để so tỉ lệ; 2 unit tranh = khoảng đỉnh tóc → sàn của con ráp sẵn.

HEROES = {
    "red": dict(
        folder="Red",
        ref=dict(box=(0, 0, 540, 1040), top=5, floor=1030),
        # Tỉ lệ theo các tấm dáng chiêu (đầu to ≈ 45% chiều cao, chân ngắn) hơn là con ráp sẵn (đầu ≈ 33%)
        head=dict(src=dict(comp=(552, 10, 938, 346)), scale=1.25,
                  joints=dict(neck=(742, 328), top=(751, 10), center=(748, 200)),
                  # tóc: (tên xương, ngọn); mặt (đa giác) giữ cứng; r0 → r1: từ tâm ra tới đâu thì tóc lay hẳn
                  hair=[("Hair Back", (570, 215)), ("Hair Top", (760, 20)), ("Hair Front", (925, 230)),
                        ("Hair Nape", (615, 315))],
                  hair_r=(95, 165),
                  face=[(630, 210), (870, 200), (875, 300), (780, 352), (700, 345), (640, 305)]),
        torso=dict(src=dict(comp=(531, 372, 841, 850)), scale=0.92,
                   joints=dict(neck=(686, 392), chest=(686, 520), pelvis=(686, 640), shoulder_n=(585, 452),
                               shoulder_f=(788, 452), hip_n=(648, 650), hip_f=(726, 650)),
                   spine_blend=(560, 600),
                   # vải: (tên xương, cha, gốc, ngọn, vùng)
                   cloth=[("Cloth Back", "chest", (585, 470), (555, 840), box(528, 430, 618, 852)),
                          ("Cloth Front", "chest", (790, 470), (825, 840), box(758, 430, 845, 852)),
                          ("Cloth Skirt", "hips", (686, 630), (686, 850), box(605, 628, 772, 852))]),
        arm_upper_n=dict(src=dict(comp=(813, 364, 937, 522)), scale=0.95, joints=dict(a=(882, 395), b=(873, 497))),
        arm_lower_n=dict(src=dict(comp=(841, 546, 943, 775)), scale=0.95, joints=dict(a=(893, 565), b=(887, 738))),
        arm_upper_f=dict(src=dict(comp=(965, 364, 1086, 521)), scale=0.95, joints=dict(a=(1012, 395), b=(1020, 497))),
        arm_lower_f=dict(src=dict(comp=(963, 547, 1067, 775)), scale=0.95, joints=dict(a=(1013, 565), b=(1019, 738))),
        leg_upper_n=dict(src=dict(comp=(486, 855, 660, 1116)), scale=0.8, joints=dict(a=(573, 885), b=(568, 1092))),
        leg_lower_n=dict(src=dict(comp=(427, 1121, 619, 1397)), flip=True, scale=0.8,
                         joints=dict(a=(530, 1140), b=(538, 1385))),
        leg_upper_f=dict(src=dict(comp=(698, 855, 871, 1116)), scale=0.8, joints=dict(a=(785, 885), b=(790, 1092))),
        leg_lower_f=dict(src=dict(comp=(735, 1121, 916, 1392)), scale=0.8, joints=dict(a=(822, 1140), b=(818, 1385))),
        # Kiếm to gần bằng người (như các tấm dáng chiêu: kiếm dài ≈ 0.9 chiều cao nhân vật). grip = chỗ tay gần nắm (sát
        # chắn kiếm), off = chỗ tay xa nắm khi cầm 2 tay (sát chuôi), cast = viên đá đỏ trên chắn kiếm
        weapon=dict(src=dict(comp=(868, 786, 1072, 1448)), scale=1.3,
                    joints=dict(grip=(968, 895), off=(968, 850), axis=(985, 1440), tip=(986, 1444), cast=(968, 955),
                                glow_a=(970, 990), glow_b=(985, 1400), pommel=(968, 795))),
    ),
    "pink": dict(
        folder="Pink",
        ref=dict(box=(0, 0, 600, 915), top=95, floor=905),
        head=dict(src=dict(poly=box(572, 6, 888, 372), seed=(720, 150)), scale=1.0,
                  joints=dict(neck=(748, 300), top=(722, 22), center=(735, 165)),
                  hair=[("Hair Back", (595, 230)), ("Hair Top", (722, 25)), ("Hair Front", (875, 230)),
                        ("Lock Back", (625, 350)), ("Lock Front", (850, 345))],
                  hair_r=(85, 150),
                  face=[(650, 165), (845, 150), (850, 275), (770, 305), (695, 295), (650, 250)]),
        torso=dict(src=dict(poly=[(585, 392), (918, 392), (918, 690), (912, 905), (585, 905)], seed=(780, 600)),
                   scale=0.85,
                   # tay gần (vai trần giữ lại, từ vòng tay trở xuống là mảnh tay riêng), bàn tay con ráp sẵn chạm vào
                   # mép trái, 2 đùi dưới gấu váy (giữ dải khăn tím vắt trước đùi)
                   cuts=[[(668, 492), (712, 503), (706, 540), (701, 600), (699, 700), (702, 730), (692, 798),
                          (636, 798), (584, 738), (584, 700), (600, 640), (640, 560), (658, 520)],
                         box(560, 538, 604, 592),
                         dict(poly=[(694, 676), (730, 670), (762, 684), (792, 700), (803, 712), (832, 700),
                                    (856, 706), (858, 836), (692, 836)], keep="purple")],
                   joints=dict(neck=(707, 402), chest=(775, 520), pelvis=(780, 625), shoulder_n=(712, 468),
                               shoulder_f=(858, 470), hip_n=(742, 668), hip_f=(820, 672)),
                   spine_blend=(560, 600),
                   cloth=[("Cloth Back", "chest", (680, 440), (615, 700), box(585, 420, 700, 720)),
                          ("Cloth Front", "chest", (880, 450), (905, 890), box(862, 440, 918, 905)),
                          ("Cloth Skirt", "hips", (790, 630), (800, 880), [(700, 620), (870, 620), (870, 905),
                                                                           (640, 905), (640, 700)])]),
        arm_upper_n=dict(src=dict(comp=(43, 927, 206, 1132)), flip=True, scale=0.68,
                         joints=dict(a=(200, 952), b=(118, 1092))),
        arm_lower_n=dict(src=dict(comp=(562, 917, 750, 1109)), flip=True, scale=0.7,
                         joints=dict(a=(695, 932), b=(612, 1075))),
        arm_upper_f=dict(src=dict(comp=(358, 927, 516, 1133)), scale=0.68, joints=dict(a=(402, 952), b=(482, 1092))),
        arm_lower_f=dict(src=dict(comp=(847, 917, 1038, 1109)), scale=0.7, joints=dict(a=(890, 932), b=(985, 1075))),
        leg_upper_n=dict(src=dict(comp=(43, 1144, 241, 1426)), scale=0.72, joints=dict(a=(140, 1170), b=(150, 1405))),
        leg_lower_n=dict(src=dict(comp=(632, 1128, 744, 1416)), flip=True, scale=0.62,
                         joints=dict(a=(688, 1150), b=(690, 1408))),
        leg_upper_f=dict(src=dict(comp=(316, 1144, 500, 1421)), scale=0.72, joints=dict(a=(408, 1170), b=(398, 1405))),
        leg_lower_f=dict(src=dict(comp=(857, 1128, 970, 1416)), scale=0.62, joints=dict(a=(913, 1150), b=(912, 1408))),
        weapon=dict(src=dict(poly=[(900, 20), (1086, 20), (1086, 910), (940, 910), (918, 700), (905, 560), (900, 300)],
                             seed=(1000, 600)), scale=0.95,
                    joints=dict(grip=(1003, 470), off=(996, 640), axis=(1000, 150), tip=(1000, 190), cast=(1000, 190),
                                glow_a=(1000, 120), glow_b=(1000, 260), pommel=(993, 860))),
        extras=dict(
            # Tóc dài sau lưng: lấy từ con ráp sẵn (khối tóc bên trái thân), bỏ trượng, tay cầm trượng, mặt; tô bù chỗ trống
            hair_back=dict(src=dict(poly=[(330, 330), (330, 700), (290, 760), (230, 805), (160, 852), (100, 852),
                                          (40, 792), (8, 700), (8, 560), (14, 420), (40, 330), (90, 280), (150, 240),
                                          (215, 212), (262, 228), (292, 290)], seed=(100, 500)),
                           # mặt + tai, tay cầm trượng + ống tay, đầu trượng, thân (váy, đùi, giày)
                           cuts=[[(262, 268), (290, 240), (345, 240), (345, 445), (285, 338), (262, 302)],
                                 [(165, 380), (225, 375), (332, 420), (332, 662), (290, 662), (230, 612), (190, 562),
                                  (165, 430)],
                                 [(50, 18), (240, 18), (240, 280), (195, 342), (140, 332), (60, 250)],
                                 [(300, 440), (345, 440), (345, 870), (262, 870), (272, 720), (288, 600), (298, 520)]],
                           strokes=[(STAFF_STROKE, 13)],
                           clones=[((STAFF_STROKE, 13), (34, 6)),
                                   ([(50, 18), (240, 18), (240, 280), (195, 342), (140, 332), (60, 250)], (75, 12)),
                                   ([(165, 380), (225, 375), (332, 420), (332, 662), (290, 662), (230, 612), (190, 562),
                                     (165, 430)], (112, 0))],
                           scale=1.0, bone="Hair Long",
                           chain=[(300, 340), (215, 450), (140, 570), (95, 690), (110, 815)],
                           joints=dict(root=(300, 340)),
                           place=("root", "head", (685, 262))),
        ),
    ),
    "blue": dict(
        folder="Blue",
        ref=dict(box=(0, 0, 640, 900), top=185, floor=890),
        head=dict(src=dict(comp=(711, 15, 1035, 277)), scale=0.76,
                  joints=dict(neck=(852, 262), top=(880, 20), center=(860, 140)),
                  hair=[("Hair Back", (725, 160)), ("Hair Top", (870, 25)), ("Hair Front", (1020, 170)),
                        ("Hair Nape", (765, 250))],
                  hair_r=(80, 140),
                  face=[(775, 145), (955, 135), (955, 235), (870, 272), (790, 250)]),
        torso=dict(src=dict(comp=(653, 294, 1085, 713)), scale=0.75,
                   joints=dict(neck=(870, 312), chest=(870, 410), pelvis=(872, 478), shoulder_n=(785, 398),
                               shoulder_f=(985, 398), hip_n=(845, 530), hip_f=(900, 530)),
                   spine_blend=(430, 470),
                   cloth=[("Cloth Back", "chest", (790, 460), (690, 700), [(650, 430), (822, 430), (830, 716),
                                                                           (650, 716)]),
                          ("Cloth Front", "chest", (960, 460), (1060, 700), [(928, 430), (1086, 430), (1086, 716),
                                                                             (920, 716)]),
                          ("Cloth Skirt", "hips", (875, 490), (880, 705), [(815, 480), (940, 480), (950, 716),
                                                                           (810, 716)])]),
        arm_upper_n=dict(src=dict(poly=box(665, 718, 800, 848), seed=(735, 780)), scale=0.75,
                         joints=dict(a=(735, 745), b=(722, 836))),
        arm_lower_n=dict(src=dict(poly=box(665, 846, 800, 1022), seed=(730, 900)), scale=0.75,
                         joints=dict(a=(722, 860), b=(742, 958))),
        arm_upper_f=dict(src=dict(poly=box(918, 718, 1060, 848), seed=(990, 780)), scale=0.75,
                         joints=dict(a=(990, 745), b=(1002, 836))),
        arm_lower_f=dict(src=dict(poly=box(918, 846, 1060, 1022), seed=(1000, 900)), scale=0.75,
                         joints=dict(a=(1002, 860), b=(980, 958))),
        leg_upper_n=dict(src=dict(poly=box(620, 1015, 790, 1152), seed=(700, 1080)), flip=True, scale=0.95,
                         joints=dict(a=(705, 1035), b=(705, 1140))),
        leg_lower_n=dict(src=dict(poly=box(620, 1148, 790, 1402), seed=(700, 1250)), flip=True, scale=0.95,
                         joints=dict(a=(705, 1162), b=(698, 1386))),
        leg_upper_f=dict(src=dict(poly=box(920, 1015, 1086, 1152), seed=(1005, 1080)), scale=0.95,
                         joints=dict(a=(1005, 1035), b=(1005, 1140))),
        leg_lower_f=dict(src=dict(poly=box(920, 1148, 1086, 1402), seed=(1005, 1250)), scale=0.95,
                         joints=dict(a=(1003, 1162), b=(1012, 1386))),
        # Lưỡi hái: mảnh rời vẽ cán ngắn / lưỡi to hơn con ráp sẵn; 1.15 = lưỡi to hơn chút, cán ngắn hơn chút so với con ráp sẵn
        weapon=dict(src=dict(comp=(17, 899, 618, 1388)), scale=1.15,
                    joints=dict(grip=(425, 1252), off=(500, 1310), axis=(240, 1110), tip=(563, 1000), cast=(160, 1080),
                                glow_a=(330, 1010), glow_b=(110, 1260), pommel=(600, 1385))),
        extras=dict(
            # Lửa hồn trên tay trái: lấy từ con ráp sẵn theo màu (bỏ móng vuốt tối đè lên lửa)
            flame=dict(src=dict(poly=box(505, 298, 628, 442), seed=(560, 420)), colorkey="cyan", largest=False,
                       scale=1.0, bone="Flame",
                       chain=[(566, 430), (566, 388), (570, 350), (572, 312)],
                       joints=dict(root=(566, 430)), points=dict(flame_center=(566, 382)),
                       place=("root", "arm_lower_f", "b")),
        ),
    ),
}

ARM_LEG = ["arm_upper_n", "arm_lower_n", "arm_upper_f", "arm_lower_f",
           "leg_upper_n", "leg_lower_n", "leg_upper_f", "leg_lower_f"]


def parse_args():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[1])
    ap.add_argument("heroes", nargs="*", default=list(HEROES))
    ap.add_argument("--sheets", type=Path, default=ROOT / "Docs/nhan_vat_moi")
    ap.add_argument("--out", type=Path, default=OUT_ROOT)
    ap.add_argument("--preview", type=Path, default=Path(tempfile.gettempdir()) / "hero_preview")
    return ap.parse_args()


# ---------------------------------------------------------------- mặt nạ, tô bù

def poly_mask(points, shape, feather=0.0, ss=4):
    """Mặt nạ đa giác khử răng cưa (0..1), feather = độ nhoè mép (pixel)."""
    h, w = shape
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


def stroke_mask(points, radius, shape, feather=0.7):
    """Mặt nạ dải dày 2·radius dọc đường gấp khúc points (cắt cán trượng, cán lưỡi hái…)."""
    h, w = shape
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    d.line([tuple(p) for p in points], fill=255, width=int(round(2 * radius)), joint="curve")
    for x, y in points:
        d.ellipse([x - radius, y - radius, x + radius, y + radius], fill=255)
    m = np.asarray(img, np.float32) / 255.0
    return ndi.gaussian_filter(m, feather) if feather > 0 else m


def smoothstep(a, b, x):
    k = np.clip((x - a) / (b - a), 0.0, 1.0)
    return k * k * (3 - 2 * k)


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


def synthesize(rgb, known, region, seed, tile=20):
    """Tô bù vùng region: màu trơn lan từ xung quanh + vân ghép từ các ô lấy ngẫu nhiên trong chỗ đặc của chính lớp
    (cửa sổ Hann chồng nhau nên không lộ mép ô)."""
    h, w = known.shape
    solid = (known > 0.97).astype(np.float32)
    smooth = push_pull(rgb, solid)
    need = region > 0.02
    if not need.any():
        return smooth
    den = np.maximum(ndi.gaussian_filter(solid, 3.0), 1e-6)
    mean = np.stack([ndi.gaussian_filter(rgb[..., c] * solid, 3.0) / den for c in range(3)], -1)
    detail = (rgb - mean) * solid[..., None]
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
    sy1, sx1 = min(y1, h), min(x1, w)
    tex = acc / np.maximum(wacc, 1e-6)[..., None]
    texture[sy0:sy1, sx0:sx1] = tex[sy0 - y0:sy1 - y0, sx0 - x0:sx1 - x0]
    return np.clip(smooth + texture, 0, 255)


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


def chain_maps(alpha, joints, blend=0.08):
    """Chuỗi xương joints[0] → … → joints[-1] (toạ độ ảnh lớp): tham số dọc chuỗi t (0 gốc, 1 ngọn) đo bằng đường đi
    trong hình, chia trọng số cho từng xương, chuyển mượt quanh mỗi khớp."""
    a = alpha > 60
    lab, n = ndi.label(a)
    if n > 1:
        sizes = np.bincount(lab.ravel())
        sizes[0] = 0
        a = lab == sizes.argmax()
    root, tip = nearest_in(a, joints[0]), nearest_in(a, joints[-1])
    d_root, d_tip = bfs(a, root), bfs(a, tip)
    with np.errstate(invalid="ignore"):
        t = d_root / np.maximum(d_root + d_tip, 1e-6)
    _, (ny, nx) = ndi.distance_transform_edt(~a, return_indices=True)
    t = t[ny, nx]
    t = np.where(np.isfinite(t), t, 0.0)
    ts = [0.0] + [float(t[nearest_in(a, p)]) for p in joints[1:-1]] + [1.0]
    count = len(joints) - 1
    maps = []
    for i in range(count):
        lo = smoothstep(ts[i] - blend, ts[i] + blend, t) if i > 0 else np.ones_like(t)
        hi = 1.0 - smoothstep(ts[i + 1] - blend, ts[i + 1] + blend, t) if i < count - 1 else np.ones_like(t)
        maps.append(lo * hi)
    total = np.maximum(sum(maps), 1e-6)
    return [m / total for m in maps]


# ---------------------------------------------------------------- tấm ảnh, mảnh

class Sheet:
    def __init__(self, path):
        self.rgba = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)
        self.h, self.w = self.rgba.shape[:2]
        self.labels, _ = ndi.label(self.rgba[..., 3] > 40)
        # Viền mờ (alpha thấp) thuộc về mảng gần nhất, để cắt mảnh không dính viền mảnh bên cạnh
        _, (iy, ix) = ndi.distance_transform_edt(self.labels == 0, return_indices=True)
        self.owner = self.labels[iy, ix]

    def components_in(self, bx):
        """Các mảng nằm gần trọn trong khung bx (mảng bên cạnh chỉ chạm mép khung thì bỏ)."""
        x0, y0, x1, y1 = bx
        ids = []
        for i, sl in enumerate(ndi.find_objects(self.labels)):
            if sl is None or (self.labels[sl] == i + 1).sum() < 150:
                continue
            ox = max(0, min(x1, sl[1].stop) - max(x0, sl[1].start))
            oy = max(0, min(y1, sl[0].stop) - max(y0, sl[0].start))
            inside = ox * oy / ((sl[1].stop - sl[1].start) * (sl[0].stop - sl[0].start))
            if inside > 0.9:
                ids.append(i + 1)
        return ids


class Part:
    """Một mảnh đã cắt: ảnh RGBA (pixel tấm ảnh, đã lật nếu cần), điểm khớp toạ độ ảnh mảnh, tỉ lệ so với con ráp sẵn.
    offset = vị trí góc trên-trái của mảnh trong hệ ráp (pixel con ráp sẵn, y xuống)."""

    def __init__(self, name, rgba, origin, flip, scale, joints):
        self.name = name
        self.rgba = rgba
        self.origin = origin          # góc trên-trái trên tấm ảnh
        self.flip = flip
        self.scale = scale
        self.h, self.w = rgba.shape[:2]
        self.joints = {k: self.local(p) for k, p in joints.items()}
        self.offset = (0.0, 0.0)

    def local(self, p):
        x, y = p[0] - self.origin[0], p[1] - self.origin[1]
        if self.flip:
            x = self.w - x
        return (x, y)

    def rig(self, key_or_point):
        """Điểm (tên khớp hoặc toạ độ ảnh mảnh) → hệ ráp (pixel con ráp sẵn, y xuống)."""
        p = self.joints[key_or_point] if isinstance(key_or_point, str) else key_or_point
        return (self.offset[0] + p[0] * self.scale, self.offset[1] + p[1] * self.scale)

    def sheet_to_rig(self, p):
        return self.rig(self.local(p))

    def place(self, joint, at):
        p = self.joints[joint]
        self.offset = (at[0] - p[0] * self.scale, at[1] - p[1] * self.scale)


def cut_part(sheet, name, spec, extra_cut=None):
    """Cắt mảnh theo spec (xem bảng HEROES). Trả về Part."""
    src = spec["src"]
    shape = (sheet.h, sheet.w)
    if "comp" in src:
        keep = np.isin(sheet.owner, sheet.components_in(src["comp"])).astype(np.float32)
    else:
        keep = poly_mask(src["poly"], shape, src.get("feather", 0.6))
        if "seed" in src:
            sx, sy = src["seed"]
            keep *= (sheet.owner == sheet.labels[sy, sx]).astype(np.float32)
    rgb = sheet.rgba[..., :3].copy()
    cut = np.zeros(shape, np.float32)
    for c in spec.get("cuts", []):
        if isinstance(c, dict):
            # Cắt nhưng giữ điểm ảnh theo màu (vd dải khăn tím vắt trước đùi)
            cut = np.maximum(cut, poly_mask(c["poly"], shape, 0.7) * (1.0 - colorkey(rgb, c["keep"])))
        else:
            cut = np.maximum(cut, poly_mask(c, shape, 0.7))
    for pts, radius in spec.get("strokes", []):
        cut = np.maximum(cut, stroke_mask(pts, radius, shape))
    if extra_cut is not None:
        cut = np.maximum(cut, extra_cut)
    full = sheet.rgba[..., 3] / 255.0 * keep
    keep = keep * (1.0 - cut)
    known = sheet.rgba[..., 3] / 255.0 * keep
    if "colorkey" in spec:
        known *= colorkey(rgb, spec["colorkey"])
    alpha = known
    fill = np.zeros(shape, np.float32)
    for f in spec.get("fills", []):
        fill = np.maximum(fill, poly_mask(f, shape, 0.6))
    if spec.get("fill_cuts"):
        # Tô bù chỗ vừa cắt, nhưng chỉ trong dáng khối gốc (đóng kín các khe giữa sợi tóc, không phình ra ngoài)
        body = ndi.binary_closing(full > 0.5, structure=np.ones((3, 3)), iterations=12)
        body = ndi.binary_fill_holes(body)
        fill = np.maximum(fill, cut * ndi.gaussian_filter(body.astype(np.float32), 1.0))
    # Tô bù kiểu "đóng dấu": chép điểm ảnh lệch (dx, dy) vào chỗ vừa cắt (tóc chảy dọc nên chép ngang trông liền sợi)
    if spec.get("clones"):
        body = ndi.binary_closing(full > 0.5, structure=np.ones((3, 3)), iterations=12)
        body = ndi.gaussian_filter(ndi.binary_fill_holes(body).astype(np.float32), 1.0)
        work = np.dstack([rgb, known])
        for area, (dx, dy) in spec["clones"]:
            if isinstance(area, tuple):
                region = stroke_mask(area[0], area[1] + 2, shape, 1.5)
            else:
                region = poly_mask(area, shape, 1.5)
            region = region * body * np.clip(cut * 1.5, 0, 1)
            shifted = np.roll(work, (dy, dx), axis=(0, 1))
            w = region[..., None] * (1.0 - work[..., 3:4])
            work = work * (1 - w) + shifted * w
            work[..., 3] = np.maximum(work[..., 3], 0)
        rgb, known = work[..., :3], work[..., 3]
        alpha = known
    if fill.any():
        region = fill * (1.0 - known)
        synth = synthesize(rgb, known, region, seed=sum(map(ord, name)))
        alpha = np.clip(known + region, 0, 1)
        mix = np.where(alpha > 1e-4, known / np.maximum(alpha, 1e-4), 1.0)[..., None]
        rgb = rgb * mix + synth * (1 - mix)
    if spec.get("largest", True):
        lab, n = ndi.label(alpha > 0.25)
        if n > 1:
            sizes = np.bincount(lab.ravel())
            sizes[0] = 0
            alpha = alpha * ndi.binary_dilation(lab == sizes.argmax(), iterations=3)
    alpha[alpha < 2 / 255] = 0
    ys, xs = np.nonzero(alpha > 0)
    x0, y0 = max(int(xs.min()) - 3, 0), max(int(ys.min()) - 3, 0)
    x1, y1 = min(int(xs.max()) + 4, sheet.w), min(int(ys.max()) + 4, sheet.h)
    rgba = np.dstack([rgb, alpha * 255.0])[y0:y1, x0:x1]
    flip = spec.get("flip", False)
    if flip:
        rgba = rgba[:, ::-1]
    return Part(name, np.ascontiguousarray(rgba), (x0, y0), flip, spec.get("scale", 1.0), spec.get("joints", {}))


def colorkey(rgb, kind):
    """Mức "đúng màu" 0..1: cyan = lửa xanh (bỏ móng vuốt tối đè lên lửa), purple = vải / khăn tím."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    if kind == "cyan":
        return np.clip((np.minimum(g, b) - r - 25.0) / 70.0, 0, 1) * np.clip((b - 90.0) / 60.0, 0, 1)
    if kind == "purple":
        return np.clip((np.minimum(r, b) - g - 30.0) / 50.0, 0, 1)
    return np.ones_like(r)


# ---------------------------------------------------------------- trọng số

def torso_weights(part, spec):
    """Hips / Chest theo trục lưng, cộng các xương vải (gốc cứng theo cha, càng xa gốc càng theo xương vải)."""
    h, w = part.h, part.w
    gy, gx = np.mgrid[0:h, 0:w].astype(np.float32) + 0.5
    lo, hi = (part.local((0, spec["spine_blend"][0]))[1], part.local((0, spec["spine_blend"][1]))[1])
    w_chest = 1.0 - smoothstep(lo, hi, gy)
    cloth_maps = []
    for name, parent, root, tip, region in spec.get("cloth", []):
        reg = np.zeros((h, w), np.float32)
        local_poly = [part.local(p) for p in region]
        reg = poly_mask(local_poly, (h, w), 3.0)
        r, t = part.local(root), part.local(tip)
        d = np.subtract(t, r)
        length = max(math.hypot(*d), 1.0)
        along = ((gx - r[0]) * d[0] + (gy - r[1]) * d[1]) / (length * length)
        cloth_maps.append(reg * smoothstep(0.0, 0.55, along))
    total = sum(cloth_maps) if cloth_maps else np.zeros((h, w), np.float32)
    over = np.maximum(total, 1.0)
    cloth_maps = [m / over for m in cloth_maps]
    body = 1.0 - np.minimum(total, 1.0)
    return [body * (1.0 - w_chest), body * w_chest] + cloth_maps


def head_weights(part, spec):
    """Head cứng (mặt + giữa đầu), tóc ngoài rìa theo các xương tóc toả ra từ tâm đầu."""
    h, w = part.h, part.w
    gy, gx = np.mgrid[0:h, 0:w].astype(np.float32) + 0.5
    c = part.joints["center"]
    rx, ry = gx - c[0], gy - c[1]
    radius = np.hypot(rx, ry)
    ang = np.arctan2(ry, rx)
    r0, r1 = spec["hair_r"]
    reach = smoothstep(r0, r1, radius)
    face = poly_mask([part.local(p) for p in spec["face"]], (h, w), 4.0)
    reach *= 1.0 - face
    maps = []
    for _, tip in spec["hair"]:
        t = part.local(tip)
        ta = math.atan2(t[1] - c[1], t[0] - c[0])
        diff = np.angle(np.exp(1j * (ang - ta)))
        maps.append(np.exp(-(diff / 0.55) ** 2))
    total = np.maximum(sum(maps), 1e-6)
    return [1.0 - reach] + [reach * m / total for m in maps]


def sampler(maps, part):
    """weights_at(x, y) cho skin_data: (x, y) là toạ độ hệ ảnh mảnh đã nhân tỉ lệ (pixel ráp, gốc góc trên-trái mảnh)."""
    h, w = part.h, part.w

    def weights_at(x, y):
        ix = int(np.clip(x / part.scale, 0, w - 1))
        iy = int(np.clip(y / part.scale, 0, h - 1))
        return [(i, float(m[iy, ix])) for i, m in enumerate(maps)]

    return weights_at


# ---------------------------------------------------------------- dựng một nhân vật

def build(hero, cfg, args):
    print(f"== {hero}")
    sheet = Sheet(args.sheets / hero / f"{hero}.png")
    ref = cfg["ref"]
    src_ppu = (ref["floor"] - ref["top"]) / 2.0     # pixel con ráp sẵn mỗi unit tranh
    k_out = OUT_PPU / src_ppu                        # pixel ráp → pixel ảnh xuất

    parts = {}
    for name in ["torso", "head"] + ARM_LEG + ["weapon"]:
        parts[name] = cut_part(sheet, name, cfg[name])
    for name, spec in cfg.get("extras", {}).items():
        parts[name] = cut_part(sheet, name, spec)

    # Ráp dáng đứng
    torso, head = parts["torso"], parts["head"]
    torso.place("neck", torso.joints["neck"])
    head.place("neck", torso.rig("neck"))
    for side in "nf":
        up, lo = parts[f"arm_upper_{side}"], parts[f"arm_lower_{side}"]
        up.place("a", torso.rig(f"shoulder_{side}"))
        lo.place("a", up.rig("b"))
        th, sh = parts[f"leg_upper_{side}"], parts[f"leg_lower_{side}"]
        th.place("a", torso.rig(f"hip_{side}"))
        sh.place("a", th.rig("b"))
    parts["weapon"].place("grip", parts["arm_lower_n"].rig("b"))
    for name, spec in cfg.get("extras", {}).items():
        anchor, other, at = spec["place"]
        target = parts[other].rig(at) if isinstance(at, str) else parts[other].sheet_to_rig(at)
        parts[name].place(anchor, target)

    # Gốc tranh: mặt sàn = đế giày thấp nhất, x = giữa hai háng
    floor = max(parts["leg_lower_n"].rig("b")[1], parts["leg_lower_f"].rig("b")[1])
    ox = (torso.rig("hip_n")[0] + torso.rig("hip_f")[0]) / 2

    def units(p):
        return [round((p[0] - ox) / src_ppu, 5), round((floor - p[1]) / src_ppu, 5)]

    out_dir = args.out / cfg["folder"]
    out_dir.mkdir(parents=True, exist_ok=True)
    entries, images = [], {}

    def export(part, pivot_key, bones=None, maps=None, order_key=None):
        out = premultiplied_resize(part.rgba, part.scale * k_out)
        oh, ow = out.shape[:2]
        Image.fromarray(out, "RGBA").save(out_dir / f"{part.name}.png")
        p = part.joints[pivot_key] if isinstance(pivot_key, str) else pivot_key
        entry = {"name": part.name, "file": f"{part.name}.png", "size": [ow, oh],
                 "pivot": [round(p[0] / part.w, 5), round(1 - p[1] / part.h, 5)],
                 "position": units(part.rig(p)), "order": ORDER[order_key or part.name]}
        if bones:
            # Xương / lưới trong hệ ảnh mảnh đã nhân tỉ lệ; skin_data đổi sang pixel ảnh xuất gốc dưới-trái
            origin = (0.0, 0.0, part.w * part.scale / ow, part.h * part.scale / oh)
            specs = [(n, (a[0] * part.scale, a[1] * part.scale), (b[0] * part.scale, b[1] * part.scale), par)
                     for n, a, b, par in bones]
            data, nv, nt = skin_data(out, origin, specs, sampler(maps, part), GRID)
            entry.update(data)
            print(f"  {part.name}: {ow}x{oh}, {len(bones)} xương, {nv} đỉnh, {nt} tam giác")
        else:
            print(f"  {part.name}: {ow}x{oh}")
        entries.append(entry)
        images[part.name] = (out, entry, part)
        return entry

    # Thân: Hips (hông → ngực), Chest (ngực → cổ), xương vải
    tj = torso.joints
    bones = [("Hips", tj["pelvis"], tj["chest"], -1), ("Chest", tj["chest"], tj["neck"], 0)]
    for name, parent, root, tip, _ in cfg["torso"].get("cloth", []):
        bones.append((name, torso.local(root), torso.local(tip), 0 if parent == "hips" else 1))
    export(torso, "pelvis", bones, torso_weights(torso, cfg["torso"]))

    # Đầu: Head (cổ → đỉnh) + tóc
    hj = head.joints
    bones = [("Head", hj["neck"], hj["top"], -1)]
    for name, tip in cfg["head"]["hair"]:
        bones.append((name, hj["center"], head.local(tip), 0))
    export(head, "neck", bones, head_weights(head, cfg["head"]))

    for name in ARM_LEG:
        export(parts[name], "a")
    export(parts["weapon"], "grip")

    for name, spec in cfg.get("extras", {}).items():
        part = parts[name]
        chain = [part.local(p) for p in spec["chain"]]
        maps = chain_maps(part.rgba[..., 3], [(p[0], p[1]) for p in chain], spec.get("blend", 0.08))
        prefix = spec["bone"]
        bones = [(f"{prefix} {i + 1}", chain[i], chain[i + 1], i - 1) for i in range(len(chain) - 1)]
        export(part, spec["place"][0], bones, maps)

    # Quầng sáng (vũ khí rực, lửa): vẽ bằng code
    g = np.linspace(-1, 1, 128)
    gxx, gyy = np.meshgrid(g, g)
    falloff = np.clip(1 - np.sqrt(gxx ** 2 + gyy ** 2), 0, 1) ** 2.0
    glow = np.dstack([np.full_like(falloff, 255)] * 3 + [falloff * 255]).astype(np.uint8)
    Image.fromarray(glow, "RGBA").save(out_dir / "glow.png")
    entries.append({"name": "glow", "file": "glow.png", "size": [128, 128], "pivot": [0.5, 0.5], "position": [0, 0],
                    "order": ORDER["glow"]})

    # Điểm khớp (unit tranh, dáng đứng)
    points = {"neck": torso.rig("neck"), "chest": torso.rig("chest"), "pelvis": torso.rig("pelvis"),
              "head_top": head.rig("top"), "head_center": head.rig("center")}
    for side in "nf":
        points[f"shoulder_{side}"] = parts[f"arm_upper_{side}"].rig("a")
        points[f"elbow_{side}"] = parts[f"arm_upper_{side}"].rig("b")
        points[f"wrist_{side}"] = parts[f"arm_lower_{side}"].rig("a")   # = khuỷu ở mảnh tay dưới
        points[f"hand_{side}"] = parts[f"arm_lower_{side}"].rig("b")
        points[f"hip_{side}"] = parts[f"leg_upper_{side}"].rig("a")
        points[f"knee_{side}"] = parts[f"leg_upper_{side}"].rig("b")
        points[f"shin_{side}"] = parts[f"leg_lower_{side}"].rig("a")
        points[f"foot_{side}"] = parts[f"leg_lower_{side}"].rig("b")
    # Điểm bao (VisualBounds): mép trái / phải của đầu và thân, gấu áo
    for name, key in (("head", "center"), ("torso", "chest")):
        part = parts[name]
        solid = part.rgba[..., 3] > 128
        cols = np.nonzero(solid.any(axis=0))[0]
        rows = np.nonzero(solid.any(axis=1))[0]
        cy = part.joints[key][1]
        points[f"{name}_l"] = part.rig((cols[0], cy))
        points[f"{name}_r"] = part.rig((cols[-1] + 1, cy))
        if name == "torso":
            points["torso_b"] = part.rig((part.joints["pelvis"][0], rows[-1] + 1))
    weapon = parts["weapon"]
    for key in weapon.joints:
        points[f"weapon_{key}"] = weapon.rig(key)
    for name, spec in cfg.get("extras", {}).items():
        for key, p in spec.get("points", {}).items():
            points[key] = parts[name].sheet_to_rig(p)

    # Chiều cao: đỉnh tóc (điểm cao nhất của đầu, alpha > 50%) → sàn
    out, entry, part = images["head"]
    rows = np.nonzero((part.rgba[..., 3] > 128).any(axis=1))[0]
    top_y = part.rig((0, rows[0]))[1]
    height = (floor - top_y) / src_ppu
    manifest = {"pixelsPerUnit": OUT_PPU, "height": round(height, 4), "parts": entries,
                "points": [{"name": k, "at": units(v)} for k, v in points.items()]}
    (out_dir / "rig.json").write_text(json.dumps(manifest, separators=(",", ":")))
    print(f"  rig.json: {len(entries)} lớp, cao {height:.3f} unit tranh, {OUT_PPU:g} px/unit → {out_dir}")

    preview(hero, cfg, sheet, parts, images, points, units, src_ppu, args)


# ---------------------------------------------------------------- ảnh ráp thử

def preview(hero, cfg, sheet, parts, images, points, units, src_ppu, args):
    args.preview.mkdir(parents=True, exist_ok=True)
    ref = cfg["ref"]
    scale_px = 260.0                           # pixel ảnh xem mỗi unit tranh
    panel_w, panel_h = 2.6, 2.6
    canvas = Image.new("RGBA", (int(3 * panel_w * scale_px), int(panel_h * scale_px)), (60, 72, 96, 255))
    d = ImageDraw.Draw(canvas)
    base_y = 2.45 * scale_px

    # Trái: con ráp sẵn, cùng tỉ lệ (đỉnh tóc → sàn = 2 unit)
    x0, y0, x1, y1 = ref["box"]
    k = scale_px / src_ppu
    crop = Image.fromarray(sheet.rgba[y0:y1, x0:x1].astype(np.uint8), "RGBA")
    crop = crop.resize((max(1, round(crop.width * k)), max(1, round(crop.height * k))), Image.LANCZOS)
    canvas.alpha_composite(crop, (int(panel_w * scale_px / 2 - crop.width / 2), int(base_y - (ref["floor"] - y0) * k)))

    def to_px(p, panel):
        return (panel + 0.5) * panel_w * scale_px + p[0] * scale_px, base_y - p[1] * scale_px

    for panel in (1, 2):
        for name, (out, entry, part) in sorted(images.items(), key=lambda it: it[1][1]["order"]):
            kk = scale_px / OUT_PPU
            img = Image.fromarray(out, "RGBA").resize((max(1, round(out.shape[1] * kk)), max(1, round(out.shape[0] * kk))),
                                                      Image.LANCZOS)
            if name.endswith("_f"):
                arr = np.asarray(img).astype(np.float32)
                arr[..., :3] *= np.array([0.72, 0.7, 0.8])
                img = Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA")
            px, py = to_px(entry["position"], panel)
            canvas.alpha_composite(img, (round(px - entry["pivot"][0] * img.width),
                                         round(py - (1 - entry["pivot"][1]) * img.height)))
        gy = to_px((0, 0), panel)[1]
        d.line([(panel * panel_w * scale_px, gy), ((panel + 1) * panel_w * scale_px, gy)], fill=(150, 170, 120, 255))
    top = to_px((0, 2.0), 0)[1]
    d.line([(0, top), (3 * panel_w * scale_px, top)], fill=(120, 160, 255, 255))

    def dot(p, color, r=3):
        x, y = to_px(units(p), 2)
        d.ellipse([x - r, y - r, x + r, y + r], fill=color)

    for side, color in (("n", (80, 255, 120, 255)), ("f", (255, 200, 60, 255))):
        for chain in (("shoulder", "elbow", "hand"), ("hip", "knee", "foot")):
            pts = [to_px(units(points[f"{c}_{side}"]), 2) for c in chain]
            d.line(pts, fill=color, width=2)
            for c in chain:
                dot(points[f"{c}_{side}"], color)
    for key in ("neck", "chest", "pelvis", "head_center"):
        dot(points[key], (255, 255, 255, 255), 4)
    for key in ("weapon_grip", "weapon_tip", "weapon_cast"):
        dot(points[key], (255, 60, 220, 255), 4)
    canvas.save(args.preview / f"{hero}_assemble.png")

    # Từng mảnh kèm điểm khớp
    tiles = []
    for name, (out, entry, part) in images.items():
        img = Image.fromarray(part.rgba.astype(np.uint8), "RGBA")
        bg = Image.new("RGBA", img.size, (90, 120, 150, 255))
        bg.alpha_composite(img)
        dd = ImageDraw.Draw(bg)
        for key, p in part.joints.items():
            dd.ellipse([p[0] - 4, p[1] - 4, p[0] + 4, p[1] + 4], outline=(255, 255, 0, 255), width=2)
            dd.text((p[0] + 5, p[1] - 5), key, fill=(255, 255, 0, 255))
        dd.text((3, 3), name, fill=(255, 255, 255, 255))
        tiles.append(bg)
    rows, row, x = [], [], 0
    for t in tiles:
        if x + t.width > 1600 and row:
            rows.append(row)
            row, x = [], 0
        row.append(t)
        x += t.width + 6
    rows.append(row)
    sheet_w = max(sum(t.width + 6 for t in r) for r in rows)
    sheet_h = sum(max(t.height for t in r) + 6 for r in rows)
    out_img = Image.new("RGBA", (sheet_w, sheet_h), (30, 30, 30, 255))
    y = 0
    for r in rows:
        x = 0
        for t in r:
            out_img.alpha_composite(t, (x, y))
            x += t.width + 6
        y += max(t.height for t in r) + 6
    out_img.save(args.preview / f"{hero}_parts.png")
    print("  ->", args.preview / f"{hero}_assemble.png")


def main():
    args = parse_args()
    for hero in args.heroes:
        build(hero, HEROES[hero], args)


if __name__ == "__main__":
    main()
