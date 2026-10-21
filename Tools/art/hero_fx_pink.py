"""Cắt hiệu ứng chiêu của PINK từ chính các tấm dáng chiêu Docs/nhan_vat_moi/pink/{basic_attack,skill,special_skill}.

Mỗi chiêu dùng mảnh cắt từ CHÍNH tấm của nó, tách thành nhiều lớp diễn riêng (lõi trắng rực, thân màu, vệt đuôi, mảnh vỡ /
đá, nửa trước / nửa sau quanh người) — PinkFx.cs ghép lại theo nhịp chiêu:
  - alpha = độ khác so với màu nền ("color to alpha"), rồi gỡ nền ra khỏi màu (unpremultiply) nên quầng sáng mờ dần tự
    nhiên, không viền xanh đen; kiểu "glow": phần tối (tóc tối, thân hình nộm, cán trượng) bỏ hẳn, chỉ giữ ánh sáng; "fill":
    đá vụn / mặt tối pha lê lọt giữa vùng sáng lấp lại cho đặc; "bright": chỉ giữ nét trắng hồng (mắt xích, cột giáo) —
    bỏ hẳn hình nộm xám tím; "soft": khói bụi mờ.
  - "keep" (đa giác giữ lại) + "erase" (đa giác xoá: tóc, chân, đầu trượng) bỏ phần nhân vật; chỗ hiệu ứng bị nhân vật che
    thì DỰNG LẠI bằng chính nét của hiệu ứng, không để lỗ / vệt nhoè:
      "bridge"  nội suy theo một chiều (dọc / ngang) giữa hai mép còn lại — cán trượng vắt ngang vệt trăng, chân che dải quét;
      "radial"  kéo dài các tia sáng toả từ một tâm vào trong — lõi vụ nổ / mái vòm chỗ PINK đứng;
      "rot"     lấy điểm đối xứng tâm (vòng phép xoay 180° vẫn như cũ);
      "core"    vẽ lại lõi sáng tròn (tâm vòng phép từng có đầu trượng);
    mép ảnh mờ dần theo hình elip ("oval") hoặc theo cạnh ("fade") — không còn cạnh thẳng / khung vuông.
  - "variants": từ cùng một vùng đã làm sạch tách thêm lớp — "core" (phần trắng rực, cộng sáng), "rows" (nửa trên / nửa dưới
    vòng elip = nửa sau / nửa trước người), "keep" (một phần) — gốc ảnh tính chung nên các lớp chồng khít nhau.
Gốc ảnh (pivot) đặt ở chỗ có nghĩa: mũi trượng (vệt trăng, chớp), chân PINK trong khung (hiệu ứng trên mặt đất), đầu rồng /
mũi tia (vật bay), tâm (vụ nổ, vòng phép). Mỗi tấm có số px / unit riêng (PINK cao 2 unit) nên ảnh to đúng như trong tranh.

Chạy:  $S/imgenv/bin/python Tools/art/hero_fx_pink.py [--sheets Docs/nhan_vat_moi/pink] [--preview thư/mục] [--only tên…]
Ra:    Assets/_Project/Resources/HeroFx/Pink/<tên>.png + fx_manifest.json (px / unit, pivot; Editor/HeroFxImporter_Pink.cs
       đọc để đặt kiểu Sprite). Trong game: Resources.Load<Sprite>("HeroFx/Pink/<tên>") (Combat/Heroes/PinkFx.cs).
       Assets/_Project/Art/UI/Skill_Pink_1..3.png: biểu tượng 3 nút chiêu (ghi đè file cũ, giữ GUID; Tools/art/hero_ui.py cũng
       ghi 3 file này — chạy hero_ui.py thì chạy lại script này sau).
Cần Pillow, numpy, scipy.
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Project/Resources/HeroFx/Pink"
SCALE = 0.7  # thu nhỏ ảnh ra so với tranh gốc (đủ nét cho màn hình điện thoại)

# Các tấm tranh: tên tắt → (đường dẫn trong thư mục pink, px tranh / unit game: PINK cao 2 unit)
SHEETS = {
    "basic": ("basic_attack/đánh thường.png", 165.0),
    "launch": ("basic_attack/Đánh thường hất tung.png", 158.0),
    "guard": ("basic_attack/Đánh thường thủ.png", 180.0),
    "dash": ("skill/dùng chiêu áp sát.png", 160.0),
    "ranged": ("skill/dùng chiêu tầm xa.png", 155.0),
    "ward": ("skill/dùng chiêu thủ.png", 200.0),
    "nova": ("special_skill/kĩ năng đặt biêt.png", 146.0),
    "lance": ("special_skill/kĩ năng đặc biệt tầm xa.png", 160.0),
    "seal": ("special_skill/kĩ năng đặc biệt áp chế.png", 153.0),
}

def ELL_BAND(cx, cy, rx, ry, band, a0, a1, n=40):
    """Đa giác dải elip (tâm cx, cy; bán trục rx, ry; dày band px) từ góc a0 tới a1 (độ, 0 = phải, 180 → 360 = nửa trên)."""
    outer = [(cx + (rx + band / 2) * math.cos(math.radians(a)), cy + (ry + band / 2) * math.sin(math.radians(a)))
             for a in np.linspace(a0, a1, n)]
    inner = [(cx + (rx - band / 2) * math.cos(math.radians(a)), cy + (ry - band / 2) * math.sin(math.radians(a)))
             for a in np.linspace(a1, a0, n)]
    return outer + inner


def POLY_BAND(points, width):
    """Đa giác dải dày width px dọc theo đường gấp khúc points (vòng chữ cổ cong)."""
    pts = np.array(points, np.float32)
    normals = []
    for i in range(len(pts)):
        a, b = pts[max(0, i - 1)], pts[min(len(pts) - 1, i + 1)]
        d = (b - a) / max(1e-6, np.hypot(*(b - a)))
        normals.append(np.array([-d[1], d[0]]))
    left = [tuple(p + n * width / 2) for p, n in zip(pts, normals)]
    right = [tuple(p - n * width / 2) for p, n in zip(pts, normals)]
    return left + right[::-1]


# Từng mảnh: tấm, khung cắt (trái, trên, phải, dưới; px tranh), kiểu tách nền, đa giác giữ / xoá / dựng lại, mép mờ, gốc.
# K1…K7 = khung trên tấm tranh đếm từ trái sang.
FX = [
    # ===== #0 Đánh thường (basic): K3 vệt trăng nhỏ → K4 vệt trăng lớn (gốc chung ở mũi trượng), K5 chớp sao + mảnh pha lê
    dict(name="crescent_small", sheet="basic", box=(930, 230, 1112, 536), mode="glow",
         keep=[(938, 232), (1104, 232), (1108, 330), (1108, 420), (1104, 470), (1100, 534), (938, 534)],
         erase=[[(928, 336), (1010, 336), (1034, 350), (1042, 388), (1034, 424), (1010, 434), (928, 438)]], feather=5.0,
         pivot=(1036, 390)),
    dict(name="crescent_big", sheet="basic", box=(1200, 186, 1448, 578), mode="glow",
         keep=[(1205, 186), (1300, 186), (1440, 236), (1446, 330), (1440, 420), (1442, 470), (1432, 578), (1282, 578),
               (1286, 520), (1302, 470), (1306, 430), (1296, 340), (1288, 262), (1205, 222)],
         erase=[[(1284, 334), (1350, 336), (1376, 350), (1384, 386), (1376, 420), (1350, 432), (1284, 434)]], feather=5.0,
         taper=[([(1282, 470), (1340, 470), (1340, 580), (1282, 580)], (1288, 540), (1318, 530))],
         pivot=(1376, 386),
         variants=[dict(name="crescent_core", kind="core", range=(0.6, 0.88))]),
    dict(name="sparkle", sheet="basic", box=(1690, 330, 1835, 480), mode="glow",
         rot=[([(1688, 360), (1748, 360), (1752, 392), (1748, 452), (1688, 452)], (1763, 405))],
         oval=((1763, 405), (68, 70), 0.45), pivot=(1763, 405)),
    dict(name="sparkle_shards", sheet="basic", box=(1676, 215, 1856, 548), mode="glow",
         erase=[[(1670, 355), (1760, 352), (1770, 380), (1760, 455), (1670, 460)]],
         disc=((1763, 405), 40, 22), oval=((1763, 395), (120, 190), 0.25), pivot=(1763, 405)),
    dict(name="shard", sheet="basic", box=(1686, 212, 1756, 288), mode="glow", isolate=True, pivot="center"),
    # ===== #1 Đánh thường hất tung (launch): K3 cung nhỏ → K4 cung lớn dựng đứng + pha lê toé dưới đất, K5 pha lê bay vút
    #       Gốc: dưới mũi trượng, trên mặt đất (x mũi trượng, y chân) — cung mọc từ đất ra trước mặt, theo đường trượng
    dict(name="rise_small", sheet="launch", box=(985, 222, 1078, 585), mode="glow",
         keep=[(1004, 226), (1040, 230), (1072, 330), (1076, 450), (1056, 540), (1000, 585), (972, 575), (1004, 500),
               (1018, 420), (1012, 340), (998, 296)],
         taper=[([(960, 500), (1080, 500), (1080, 590), (960, 590)], (985, 575), (1030, 545))],
         pivot=(1000, 637)),
    dict(name="rise_slash", sheet="launch", box=(1200, 30, 1512, 625), mode="glow",
         keep=[(1210, 44), (1300, 32), (1420, 70), (1508, 200), (1508, 625), (1330, 625), (1338, 560), (1352, 470),
               (1350, 410), (1342, 335), (1345, 250), (1336, 140), (1300, 120), (1262, 128), (1215, 150)],
         erase=[[(1262, 126), (1300, 118), (1338, 140), (1344, 250), (1300, 260), (1270, 230)]],
         fade=dict(bottom=22), pivot=(1310, 637),
         variants=[dict(name="rise_core", kind="core", range=(0.6, 0.88))]),
    dict(name="rise_splash", sheet="launch", box=(1175, 545, 1475, 692), mode="glow",
         radial=[([(1212, 552), (1268, 552), (1272, 645), (1212, 645)], (1318, 648), 0.3)],
         oval=((1320, 640), (150, 100), 0.3), pivot=(1318, 640)),
    dict(name="rise_crystals", sheet="launch", box=(1596, 50, 1836, 650), mode="glow",
         keep=[(1620, 52), (1834, 52), (1834, 650), (1670, 650), (1668, 340), (1648, 300), (1622, 290), (1600, 240)],
         fade=dict(bottom=20), pivot=(1525, 637)),
    # ===== #2 Đánh thường thủ (guard): K2 vòng chữ xoắn quanh eo (nửa trước; nửa sau = lật lại, sau lưng), K3 tia đất bên
    #       trượng, K4 gai pha lê: cụm trái (sau lưng), cụm phải (trước), cụm chân gai thấp (trước chân), gai lớn rời, mảnh vỡ
    dict(name="rise_shard", sheet="launch", box=(1592, 138, 1672, 220), mode="glow", isolate=True, pivot="center"),
    dict(name="guard_ring", sheet="guard", box=(450, 380, 672, 492), mode="glow", nowarm=True,
         keep=[(458, 446), (500, 440), (560, 436), (608, 428), (628, 412), (612, 396), (640, 390), (664, 404), (668, 436),
               (648, 460), (600, 474), (540, 482), (480, 486), (456, 474)],
         pivot=(560, 440)),
    dict(name="guard_ray", sheet="guard", box=(895, 340, 1066, 630), mode="glow", nowarm=True,
         keep=[(960, 340), (1066, 340), (1066, 630), (898, 630), (900, 575), (942, 545), (955, 470)], keep_feather=4.0,
         oval=((985, 580), (92, 245), 0.3), fade=dict(bottom=6), pivot=(962, 605)),
    dict(name="spikes_back", sheet="guard", box=(1062, 390, 1228, 626), mode="fill", nowarm=True,
         keep=[(1143, 394), (1153, 400), (1180, 442), (1203, 518), (1214, 560), (1224, 612), (1215, 626), (1068, 626),
               (1066, 560), (1074, 492), (1084, 486), (1108, 500), (1130, 516), (1136, 450)],
         fade=dict(bottom=8), pivot=(1240, 612)),
    dict(name="spike_tall", sheet="guard", box=(1128, 390, 1228, 626), mode="fill", nowarm=True,
         keep=[(1143, 394), (1153, 400), (1180, 442), (1203, 518), (1214, 560), (1224, 614), (1140, 618), (1137, 520),
               (1136, 450)],
         fade=dict(bottom=14), pivot=(1180, 614)),
    dict(name="spikes_front", sheet="guard", box=(1328, 396, 1472, 628), mode="fill", nowarm=True,
         keep=[(1336, 468), (1362, 436), (1404, 400), (1472, 396), (1472, 628), (1330, 628), (1334, 560)],
         fade=dict(bottom=8, right=6), pivot=(1240, 612)),
    dict(name="spikes_low", sheet="guard", box=(1170, 530, 1352, 632), mode="glow", nowarm=True,
         oval=((1261, 605), (90, 72), 0.4), fade=dict(bottom=6), pivot=(1240, 612)),
    dict(name="guard_crystal", sheet="guard", box=(1370, 352, 1428, 418), mode="glow", isolate=0.5, pivot="center"),
    # ===== #3 Dùng chiêu áp sát (dash): bóng mờ cả dáng lao (K3, cố ý), vệt tốc độ dài / ngắn (K2), cung quét phải + dải
    #       quét sát đất (K4; chân che → nối lại), bụi trái / phải (K5; mép mờ elip, không khung vuông)
    dict(name="afterimage", sheet="dash", box=(728, 264, 1096, 510), mode="ghost", gain=0.85,
         keep=[(770, 300), (800, 284), (860, 278), (920, 272), (965, 270), (1005, 282), (1032, 300), (1044, 330), (1044, 372),
               (1052, 396), (1086, 418), (1092, 446), (1060, 458), (1020, 436), (985, 432), (955, 452), (925, 470), (880, 476),
               (840, 488), (800, 504), (772, 500), (776, 470), (790, 452), (750, 436), (734, 404), (744, 362), (756, 326)],
         keep_feather=3.0, pivot=(866, 506)),
    dict(name="streak_long", sheet="dash", box=(290, 428, 482, 500), mode="glow", nowarm=True,
         fade=dict(left=50, right=30, top=6, bottom=6), pivot=(482, 466)),
    dict(name="streak_short", sheet="dash", box=(620, 444, 740, 500), mode="glow", nowarm=True,
         fade=dict(left=40, right=24, top=4, bottom=4), pivot=(740, 472)),
    dict(name="sweep", sheet="dash", box=(1360, 270, 1576, 480), mode="glow", nowarm=True,
         keep=[(1368, 274), (1460, 280), (1540, 310), (1572, 360), (1574, 420), (1540, 470), (1486, 470), (1470, 450),
               (1494, 420), (1500, 380), (1480, 345), (1440, 326), (1396, 320), (1368, 318)],
         taper=[([(1360, 270), (1420, 270), (1420, 330), (1360, 330)], (1370, 300), (1418, 304))],
         pivot=(1460, 500)),
    dict(name="sweep_band", sheet="dash", box=(1128, 430, 1540, 506), mode="glow", nowarm=True,
         keep=[(1136, 480), (1250, 470), (1350, 460), (1430, 450), (1490, 438), (1534, 440), (1528, 462), (1480, 476),
               (1400, 490), (1300, 498), (1200, 502), (1136, 502)],
         bridge=[([(1354, 452), (1428, 452), (1428, 504), (1354, 504)], "h")],
         taper=[([(1128, 430), (1260, 430), (1260, 506), (1128, 506)], (1140, 490), (1250, 485))],
         pivot=(1460, 500)),
    dict(name="dust", sheet="dash", box=(1496, 396, 1602, 516), mode="soft", gain=1.5, nowarm=True,
         oval=((1552, 470), (56, 72), 0.4), fade=dict(bottom=8), pivot=(1552, 512)),
    dict(name="dust_b", sheet="dash", box=(1796, 452, 1896, 516), mode="soft", gain=1.6, nowarm=True,
         oval=((1846, 494), (52, 40), 0.45), fade=dict(bottom=8), pivot=(1846, 512)),
    # ===== #4 Dùng chiêu tầm xa (ranged): K2 vòng phép trước trượng → K3 vòng nghiêng + mũi pha lê → K4 rồng: vòng ở trượng,
    #       thân (luồng), lõi trắng, đầu rồng, nổ (quầng) + đá vụn (lớp riêng)
    dict(name="circle_front", sheet="ranged", box=(425, 172, 665, 548), mode="glow", mirror=545,
         oval=((545, 362), (118, 186), 0.22), pivot=(545, 362)),
    dict(name="circle_side", sheet="ranged", box=(790, 210, 1005, 525), mode="glow", nowarm=True,
         keep=[(790, 210), (905, 210), (908, 330), (898, 345), (898, 415), (910, 430), (905, 525), (790, 525)], keep_feather=4.0,
         oval=((858, 372), (74, 150), 0.3),
         erase=[[(815, 352), (872, 350), (878, 422), (815, 428)]], feather=3.0,
         bridge=[([(824, 346), (852, 346), (852, 430), (824, 430)], "v")],
         pivot=(862, 382)),
    dict(name="bolt_tip", sheet="ranged", box=(880, 215, 1040, 520), mode="glow", nowarm=True,
         keep=[(895, 215), (1040, 215), (1040, 452), (962, 468), (942, 520), (895, 520)], keep_feather=4.0,
         oval=((955, 375), (84, 150), 0.35), fade=dict(left=10), pivot=(862, 382)),
    dict(name="dragon", sheet="ranged", box=(1290, 140, 1706, 522), mode="glow", holes=True, dark=(14, 75),
         erase=[[(1560, 440), (1706, 420), (1706, 522), (1560, 522)]], feather=10.0,
         fade=dict(left=120, right=24, top=24, bottom=24), pivot=(1700, 375),
         variants=[dict(name="dragon_core", kind="core", range=(0.62, 0.9))]),
    dict(name="dragon_head", sheet="ranged", box=(1628, 268, 1800, 476), mode="glow",
         keep=[(1640, 296), (1690, 313), (1715, 326), (1740, 338), (1755, 348), (1772, 356), (1787, 364), (1788, 386),
               (1776, 396), (1764, 393), (1746, 389), (1738, 396), (1747, 410), (1763, 424), (1770, 441), (1752, 449),
               (1730, 442), (1712, 432), (1690, 428), (1660, 432), (1640, 424)], keep_feather=1.5,
         fade=dict(left=30), pivot=(1786, 385)),
    dict(name="dragon_ring", sheet="ranged", box=(1212, 222, 1314, 498), mode="glow", nowarm=True,
         oval=((1262, 370), (50, 136), 0.3),
         erase=[[(1230, 345), (1288, 345), (1288, 398), (1230, 398)]], feather=2.0,
         bridge=[([(1236, 342), (1290, 342), (1290, 400), (1236, 400)], "v")],
         fade=dict(left=6, right=6), pivot=(1262, 370)),
    dict(name="dragon_blast", sheet="ranged", box=(1740, 85, 1936, 560), mode="glow", holes=True,
         keep=[(1740, 85), (1936, 85), (1936, 368), (1890, 382), (1880, 560), (1740, 560)],
         fade=dict(left=60, bottom=10), pivot=(1825, 400)),
    dict(name="dragon_rocks", sheet="ranged", box=(1560, 170, 1892, 560), mode="rocks", oval=((1730, 365), (160, 190), 0.15),
         keep=[(1560, 170), (1892, 170), (1892, 380), (1880, 560), (1560, 560)],
         erase=[[(1650, 300), (1700, 278), (1740, 298), (1770, 330), (1792, 370), (1792, 412), (1776, 452), (1740, 468),
                 (1690, 452), (1648, 422)]], pivot=(1825, 400)),
    # ===== #5 Dùng chiêu thủ (ward): vòng phép dưới chân K6 — nửa trước (trước chân) / nửa sau (sau lưng); đoạn vòng xoắn
    #       quanh người K5 (bên phải, sạch; quay quanh người, đổi trước / sau); 2 mẫu pha lê bay quanh
    dict(name="ward_circle_full", sheet="ward", box=(1796, 540, 2170, 680), mode="glow", variants_only=True, oval=((1980, 606), (190, 90), 0.12),
         erase=[[(1952, 632), (2006, 632), (2006, 680), (1952, 680)]], feather=5.0,  # mũi nhọn dưới tâm vòng: giãn ngang thành khối
         patch=[([(1938, 536), (2032, 536), (2032, 605), (1938, 605)], "v", 606),
                ([(2052, 536), (2090, 536), (2090, 605), (2052, 605)], "h", 1978)],
         fade=dict(top=22), pivot=(1978, 606),
         variants=[dict(name="ward_circle", kind="rows", y=604, above=False, soft=5),
                   dict(name="ward_circle_back", kind="rows", y=604, above=True, soft=5)]),
    dict(name="ward_swirl", sheet="ward", box=(1664, 304, 1782, 436), mode="glow", nowarm=True,
         keep=[(1668, 304), (1782, 304), (1782, 436), (1668, 436)], fade=dict(left=10, right=8, top=6, bottom=6),
         pivot=(1590, 380)),
    dict(name="diamond", sheet="ward", box=(1826, 170, 1894, 294), mode="glow", isolate=0.55, pivot="center"),
    dict(name="diamond_b", sheet="ward", box=(1720, 482, 1778, 580), mode="glow", isolate=0.55, pivot="center"),
    # ===== #6 Kĩ năng đặc biệt (nova): K2 đoạn vòng chữ cổ (bay quanh người, trước / sau), K3 vệt cung lớn quanh người khi
    #       bật lên, K4 → K5 hai pha mái vòm (chỗ PINK đứng dựng lại bằng tia toả từ chỗ dộng trượng — lõi sắc, không nhoè),
    #       tách: vòng chữ cổ của vòm, chân vòm / gai trước chân (trước người), đá vụn (lớp riêng, văng ra)
    dict(name="nova_runes", sheet="nova", box=(330, 205, 530, 356), mode="glow", nowarm=True,
         keep=[(330, 205), (530, 205), (530, 262), (472, 270), (442, 300), (424, 356), (330, 356)],
         oval=((430, 278), (100, 76), 0.35), pivot="center"),
    dict(name="nova_arc", sheet="nova", box=(782, 92, 992, 526), mode="glow", nowarm=True,
         keep=[(800, 92), (992, 92), (992, 526), (934, 526), (918, 400), (906, 300), (880, 222), (842, 182), (800, 152)],
         fade=dict(bottom=40, right=6), pivot=(830, 350)),
    dict(name="nova_dome_a", sheet="nova", box=(1008, 118, 1442, 668), mode="glow", holes=True, nowarm=True,
         radial=[([(1150, 356), (1200, 326), (1260, 318), (1302, 326), (1330, 296), (1350, 318), (1340, 420), (1330, 520),
                   (1304, 584), (1250, 600), (1204, 598), (1176, 562), (1116, 548), (1084, 500), (1090, 418)], (1215, 604), 0.5)],
         fade=dict(left=40, right=24, top=10, bottom=10), pivot=(1215, 604)),
    dict(name="nova_dome_full", sheet="nova", box=(1446, 92, 1904, 664), mode="glow", holes=True, nowarm=True, variants_only=True,
         radial=[([(1556, 330), (1600, 292), (1650, 286), (1690, 296), (1712, 286), (1726, 320), (1720, 400), (1716, 470),
                   (1708, 560), (1692, 602), (1640, 614), (1598, 602), (1582, 540), (1556, 482), (1536, 440), (1536, 378)],
                  (1640, 606), 0.5)],
         fade=dict(left=24, right=24, top=8, bottom=8), pivot=(1640, 606),
         variants=[dict(name="nova_dome", kind="keep"),
                   dict(name="nova_front", kind="rows", y=548, above=False, soft=26,
                        keep=[(1470, 470), (1820, 470), (1900, 664), (1446, 664)]),
                   dict(name="nova_rune_arc", kind="keep", keep=POLY_BAND([(1458, 400), (1466, 350), (1484, 300), (1505, 255), (1530, 222), (1580, 206), (1640, 200), (1700, 204), (1760, 216),
                                  (1810, 245), (1848, 290), (1870, 340), (1880, 400), (1878, 440)], 30),
                        gain=1.0)]),
    dict(name="nova_rocks", sheet="nova", box=(1446, 92, 1904, 664), mode="rocks", min_rock=160, fade=dict(left=14, right=14),
         erase=[[(1556, 330), (1600, 292), (1650, 286), (1690, 296), (1712, 286), (1726, 320), (1720, 400), (1716, 470),
                 (1708, 560), (1692, 602), (1640, 614), (1598, 602), (1582, 540), (1556, 482), (1536, 440), (1536, 378)]],
         feather=4.0, pivot=(1640, 606)),
    # ===== #7 Kĩ năng đặc biệt tầm xa (lance): K2 cầu lửa ngoài đầu trượng, K3 vòng phép (tâm vẽ lại — không còn đầu trượng
    #       thứ hai), K4 tia: thân, lõi trắng, mũi pha lê, 2 vệt đuôi; K5 nổ hình sao + tia sao (lớp riêng)
    dict(name="nova_shard", sheet="nova", box=(1440, 140, 1512, 252), mode="glow", isolate=True, pivot="center"),
    dict(name="orb", sheet="lance", box=(362, 118, 540, 310), mode="glow", purple=True, nowarm=True,
         keep=[(362, 118), (540, 118), (540, 245), (490, 262), (470, 310), (362, 310)],
         oval=((447, 216), (86, 94), 0.3), pivot=(445, 220)),
    dict(name="circle_lance", sheet="lance", box=(790, 152, 996, 392), mode="glow", mirror=893, nowarm=True,
         core=((893, 275), 50, (0.86, 0.6, 1.0), 0.9), oval=((893, 272), (104, 118), 0.12), pivot=(893, 275)),
    dict(name="lance", sheet="lance", box=(1290, 236, 1600, 432), mode="glow", nowarm=True,
         keep=[(1290, 236), (1490, 236), (1598, 326), (1598, 362), (1490, 432), (1290, 432)], keep_feather=8.0,
         fade=dict(left=90, top=22, bottom=22),
         pivot=(1575, 345),
         variants=[dict(name="lance_core", kind="core", range=(0.62, 0.9)),
                   dict(name="lance_tip", kind="keep", feather=10.0, keep=[(1440, 300), (1530, 296), (1600, 340), (1530, 392), (1440, 392)]),
                   dict(name="lance_tail", kind="keep", feather=9.0,
                        keep=[[(1290, 236), (1530, 236), (1515, 318), (1290, 322)], [(1290, 384), (1515, 386), (1530, 432), (1290, 432)]])]),
    dict(name="star_blast", sheet="lance", box=(1525, 120, 1895, 600), mode="glow", oval=((1730, 350), (180, 232), 0.25),
         pivot=(1735, 355),
         variants=[dict(name="star_rays", kind="core", range=(0.35, 0.72), keep=[[(1727, 145), (1745, 145), (1745, 580), (1727, 580)],
                                                                             [(1565, 344), (1888, 344), (1888, 366), (1565, 366)]])]),
    # ===== #8 Kĩ năng đặc biệt áp chế (seal): một đoạn xích lặp được (K3, cắt đúng số chu kì mắt xích → vẽ kiểu Tiled), xích
    #       quấn K3 (bỏ hẳn hình nộm), lồng K5 (bỏ hình nộm + xích quấn, dựng lại cột giáo giữa sau lưng nó): cột trước +
    #       nửa trước vòng đáy / cột giữa + quầng + nửa sau vòng đáy (sau mục tiêu)
    dict(name="lance_shard", sheet="lance", box=(1770, 155, 1842, 236), mode="glow", isolate=True, pivot="center"),
    dict(name="chain", procedural="chain", ppu=153.0, pivot="center"),
    dict(name="chain_wrap", sheet="seal", box=(940, 310, 1090, 612), mode="bright", gkey=(128, 192), bright2=0.04,
         oval=((1012, 462), (74, 152), 0.25),
         pivot=(1005, 598)),
    dict(name="cage_full", sheet="seal", box=(1588, 12, 1842, 652), mode="glow", variants_only=True, fade=dict(left=14, right=14, bottom=12),
         erase=[[(1683, 196), (1757, 196), (1762, 290), (1758, 436), (1683, 436), (1680, 290)],
                [(1684, 436), (1704, 436), (1704, 492), (1688, 488)], [(1728, 436), (1750, 436), (1746, 488), (1728, 492)]],
         feather=2.0,
         bridge=[([(1704, 150), (1728, 150), (1728, 446), (1704, 446)], "v")],
         pivot=(1714, 600),
         variants=[dict(name="cage", kind="keep",
                        keep=[[(1608, 20), (1645, 20), (1645, 570), (1608, 570)], [(1650, 80), (1686, 80), (1686, 560), (1650, 560)],
                              [(1756, 80), (1794, 80), (1794, 560), (1756, 560)], [(1796, 20), (1834, 20), (1834, 570), (1796, 570)],
                              [(1588, 600), (1842, 600), (1842, 652), (1588, 652)]]),
                   dict(name="cage_back", kind="keep",
                        erase=[[(1608, 20), (1645, 20), (1645, 570), (1608, 570)], [(1650, 80), (1686, 80), (1686, 560), (1650, 560)],
                               [(1756, 80), (1794, 80), (1794, 560), (1756, 560)], [(1796, 20), (1834, 20), (1834, 570), (1796, 570)],
                               [(1588, 602), (1842, 602), (1842, 652), (1588, 652)]])]),
    dict(name="seal_shard", sheet="seal", box=(1032, 500, 1094, 578), mode="glow", isolate=0.62, pivot="center"),
]

# ----------------------------------------------------------------------------------------------------------------------
# Tách nền + dựng lại
# ----------------------------------------------------------------------------------------------------------------------


def background(rgb):
    """Màu nền: trung vị viền tấm tranh (nền phẳng một màu xanh đen)."""
    border = np.concatenate([rgb[:16].reshape(-1, 3), rgb[-16:].reshape(-1, 3), rgb[:, :16].reshape(-1, 3), rgb[:, -16:].reshape(-1, 3)])
    return np.median(border, axis=0)


def smoothstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def unmix(rgb, bg, mode, spec):
    """rgb (h, w, 3) 0..255 → (màu đã gỡ nền 0..1, alpha 0..1)."""
    up = np.where(rgb > bg, (rgb - bg) / np.maximum(255 - bg, 1), 0)
    down = np.where(rgb < bg, (bg - rgb) / np.maximum(bg, 1), 0)
    alpha = np.maximum(up, down).max(-1)
    if mode in ("glow", "fill"):
        # chỉ giữ ánh sáng: phần tối (tóc tối, cán trượng, thân hình nộm) bỏ đi
        lo, hi = spec.get("dark", (8, 55))
        alpha *= smoothstep(rgb.max(-1), bg.max() + lo, bg.max() + hi)
    if mode == "fill":
        # đá tối / mặt tối pha lê nằm lọt giữa vùng sáng (lỗ kín): lấp cho đặc lại, giữ màu tối thật của đá
        alpha0 = np.maximum(up, down).max(-1)
        solid = alpha > 0.3
        holes = ndi.binary_fill_holes(solid) & ~solid
        holes = ndi.gaussian_filter(holes.astype(np.float32), 1.0)
        alpha = np.maximum(alpha, holes * np.maximum(alpha0, 0.85))
    if mode == "bright":
        # nét trắng hồng (mắt xích, cột giáo, vòng sáng): kênh xanh lá cao; tím tối / xám của hình nộm bỏ hẳn
        g0, g1 = spec.get("gkey", (95, 165))
        k2 = spec.get("bright2", 0.15)
        alpha *= smoothstep(rgb[..., 1], g0, g1) * (1 - k2) + k2 * smoothstep(rgb.max(-1), 150, 230)
    if mode == "soft":
        alpha = np.clip(alpha * spec.get("gain", 1.0), 0, 1)
    if mode == "ghost":
        # bóng mờ cả dáng nhân vật (cố ý): hình đặc như icon, màu đổi sang tím đơn sắc sáng dần theo độ sáng gốc
        dist = np.sqrt(((rgb - bg) ** 2).sum(-1))
        lo, hi = spec.get("solid", (10, 30))
        alpha = smoothstep(dist, lo, hi) * spec.get("gain", 0.9)
        color = bg + (rgb - bg) / np.maximum(alpha, 1e-3)[..., None]
        lum = (np.clip(color / 255, 0, 1) @ np.array([0.3, 0.59, 0.11]))[..., None]
        lo, hi = np.array([0.34, 0.14, 0.62]), np.array([0.92, 0.8, 1.0])
        return np.clip(lo + (hi - lo) * np.clip(lum * 1.4, 0, 1) ** 0.8, 0, 1), np.clip(alpha, 0, 1)
    if mode == "rocks":
        # đá vụn: thân đá tối (đỏ / tím xám hơn nền xanh đen, không sáng) — lớp đá riêng, mặt tối đặc
        dark = smoothstep(rgb[..., 0] - bg[0], 10, 28) * (1 - smoothstep(rgb.max(-1), 105, 150))
        dark = ndi.binary_opening(dark > 0.5, iterations=2)
        labels, n = ndi.label(dark)
        if n:
            sizes = np.bincount(labels.ravel())
            sizes[0] = 0
            dark = np.isin(labels, np.nonzero(sizes >= spec.get("min_rock", 60))[0])
        rim = ndi.binary_dilation(dark, iterations=2)
        alpha = np.maximum(ndi.gaussian_filter(dark.astype(np.float32), 0.8), rim * np.clip(alpha, 0, 1))
        alpha = np.clip(alpha, 0, 1)
        color = np.clip(rgb / 255, 0, 1)
        return color, alpha
    alpha = np.clip(alpha, 0, 1)
    if spec.get("nowarm"):
        # da, gỗ trượng (đỏ hơn xanh dương): không phải ánh sáng tím của hiệu ứng → bỏ
        alpha *= 1 - smoothstep(rgb[..., 0] - rgb[..., 2], 4, 26)
    color = bg + (rgb - bg) / np.maximum(alpha, 1e-3)[..., None]
    color, alpha = np.clip(color / 255, 0, 1), alpha
    if spec.get("holes"):
        # lỗ kín trong quầng sáng (chỗ đá tối — đá là lớp riêng): lấp bằng chính ánh sáng xung quanh
        solid = alpha > 0.3
        hole = ndi.binary_fill_holes(solid) & ~solid
        hole = ndi.binary_dilation(hole, iterations=1)
        color, alpha = fill_region(color, alpha, ndi.gaussian_filter(hole.astype(np.float32), 0.7))
    return color, alpha


def poly_mask(points, shape, offset, feather=2.0, ss=4):
    """Mặt nạ đa giác (1 bên trong), toạ độ tranh → toạ độ vùng cắt; mép mềm. points: một đa giác hoặc danh sách đa giác."""
    h, w = shape
    img = Image.new("L", (w * ss, h * ss), 0)
    polys = points if isinstance(points[0][0], (tuple, list)) else [points]
    for poly in polys:
        ImageDraw.Draw(img).polygon([((x - offset[0]) * ss, (y - offset[1]) * ss) for x, y in poly], fill=255)
    m = np.asarray(img.resize((w, h), Image.BOX)).astype(np.float32) / 255
    return np.clip(ndi.gaussian_filter(m, feather), 0, 1) if feather > 0 else m


def premul(color, alpha):
    return np.dstack([color * alpha[..., None], alpha])


def unpremul(pre):
    a = np.clip(pre[..., 3], 0, 1)
    return np.clip(pre[..., :3] / np.maximum(a, 1e-4)[..., None], 0, 1), a


def bridge(color, alpha, hole, axis):
    """Dựng lại vùng hole bằng nội suy thẳng (theo cột nếu axis="v", theo hàng nếu "h") giữa hai mép còn lại ngay ngoài vùng
    (trung bình 3 px mỗi bên, màu nhân alpha): nét vệt sáng bị cán trượng / chân cắt ngang nối liền lại."""
    pre = premul(color, alpha)
    if axis == "h":
        pre = pre.transpose(1, 0, 2)
        hole = hole.T
    out = pre.copy()
    n, w = hole.shape
    for x in range(w):
        col = hole[:, x]
        if not col.any():
            continue
        y = 0
        while y < n:
            if not col[y]:
                y += 1
                continue
            y0 = y
            while y < n and col[y]:
                y += 1
            a = pre[max(0, y0 - 3):y0, x].mean(0) if y0 > 0 else None
            b = pre[y:min(n, y + 3), x].mean(0) if y < n else None
            if a is None and b is None:
                out[y0:y, x] = 0
                continue
            a = b * 0 if a is None else a
            b = a * 0 if b is None else b
            t = ((np.arange(y0, y) - y0 + 1) / (y - y0 + 1))[:, None]
            out[y0:y, x] = a * (1 - t) + b * t
    if axis == "h":
        out = out.transpose(1, 0, 2)
    return unpremul(out)


def radial(color, alpha, hole, center, ramp=0.6, reach=600):
    """Dựng lại vùng hole bằng tia toả từ center: mỗi điểm lấy màu ở điểm gần nhất cùng tia phía ngoài vùng, sáng dần về
    tâm (ramp) — các tia sáng của vụ nổ / mái vòm chụm về đúng chỗ dộng trượng, nét sắc, không nhoè."""
    pre = premul(color, alpha)
    out = pre.copy()
    ys, xs = np.nonzero(hole)
    if len(ys) == 0:
        return color, alpha
    cx, cy = center
    dx, dy = xs - cx, ys - cy
    dist = np.hypot(dx, dy) + 1e-6
    ux, uy = dx / dist, dy / dist
    h, w = alpha.shape
    found = np.zeros(len(ys), bool)
    val = np.zeros((len(ys), 4), np.float32)
    srcd = np.zeros(len(ys), np.float32)
    for step in range(1, reach):
        px = np.round(xs + ux * step).astype(int)
        py = np.round(ys + uy * step).astype(int)
        inside = (px >= 0) & (px < w) & (py >= 0) & (py < h)
        pxc, pyc = np.clip(px, 0, w - 1), np.clip(py, 0, h - 1)
        ok = ~found & inside & ~hole[pyc, pxc]
        if ok.any():
            # trung bình 2 điểm liền nhau phía ngoài cho đỡ hạt
            px2 = np.clip(np.round(xs + ux * (step + 2)).astype(int), 0, w - 1)
            py2 = np.clip(np.round(ys + uy * (step + 2)).astype(int), 0, h - 1)
            val[ok] = (pre[pyc[ok], pxc[ok]] + pre[py2[ok], px2[ok]]) * 0.5
            srcd[ok] = dist[ok] + step
            found |= ok
        if found.all():
            break
    gain = 1 + ramp * np.clip(1 - dist / np.maximum(srcd, 1), 0, 1)
    v = val * gain[:, None]
    v[:, 3] = np.clip(v[:, 3], 0, 1)
    v[:, :3] = np.minimum(v[:, :3], v[:, 3:4])
    out[ys, xs] = v
    return unpremul(out)


def rot_fill(color, alpha, hole, center):
    """Dựng lại vùng hole bằng điểm đối xứng tâm (vòng phép xoay 180°)."""
    pre = premul(color, alpha)
    out = pre.copy()
    ys, xs = np.nonzero(hole)
    h, w = alpha.shape
    sx = np.clip(np.round(2 * center[0] - xs).astype(int), 0, w - 1)
    sy = np.clip(np.round(2 * center[1] - ys).astype(int), 0, h - 1)
    out[ys, xs] = pre[sy, sx]
    return unpremul(out)


def glow_core(color, alpha, center, radius, rgb, strength=1.0):
    """Vẽ lại lõi sáng tròn mềm ở center (thay chỗ đầu trượng giữa vòng phép): màu rgb, đậm dần vào tâm."""
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.hypot(xx - center[0], yy - center[1]) / radius
    k = np.clip(1 - r, 0, 1) ** 1.6 * strength
    pre = premul(color, alpha)
    core = np.dstack([np.broadcast_to(np.array(rgb, np.float32), (h, w, 3)), np.ones((h, w), np.float32)])
    pre = pre * (1 - k[..., None]) + core * k[..., None]
    return unpremul(pre)


def fill_region(color, alpha, mask):
    """Lấp vùng mask bằng ánh sáng xung quanh: khuếch tán (normalized convolution) — chỉ dùng cho lỗ nhỏ (đá rời khỏi quầng)."""
    known = (mask < 0.5).astype(np.float32)
    pre = premul(color, alpha)
    out = pre.copy()
    filled = np.zeros_like(alpha, dtype=bool)
    hole = mask >= 0.5
    for sigma in (2, 4, 8, 16):
        num = np.dstack([ndi.gaussian_filter(pre[..., c] * known, sigma) for c in range(4)])
        den = ndi.gaussian_filter(known, sigma)[..., None]
        ok = (den[..., 0] > 0.02) & hole & ~filled
        out[ok] = (num / np.maximum(den, 1e-6))[ok]
        filled |= ok
    m = mask[..., None]
    return unpremul(pre * (1 - m) + out * m)


def fade_edges(alpha, fade):
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    k = np.ones_like(alpha)
    if fade.get("left"):
        k *= np.clip(xx / fade["left"], 0, 1)
    if fade.get("right"):
        k *= np.clip((w - 1 - xx) / fade["right"], 0, 1)
    if fade.get("top"):
        k *= np.clip(yy / fade["top"], 0, 1)
    if fade.get("bottom"):
        k *= np.clip((h - 1 - yy) / fade["bottom"], 0, 1)
    return alpha * k * k * (3 - 2 * k)


def oval(alpha, center, radii, soft):
    """Mờ dần ra ngoài elip tâm center bán trục radii (toạ độ vùng cắt): bỏ mọi cạnh thẳng / góc vuông của khung cắt."""
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.hypot((xx - center[0]) / radii[0], (yy - center[1]) / radii[1])
    return alpha * smoothstep(1 - r, 0, soft)


def keep_largest(alpha, grow=3, count=1):
    labels, n = ndi.label(alpha > 0.12)
    if n == 0:
        return alpha
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    keep = np.isin(labels, np.argsort(sizes)[::-1][:count])
    keep = ndi.binary_dilation(keep, iterations=grow)
    return alpha * ndi.gaussian_filter(keep.astype(np.float32), 1.0)


def isolate(alpha, grow=4, thr=0.15):
    """Giữ đúng một mảnh (viên pha lê) ở giữa vùng cắt: thành phần liền gần tâm nhất, nới ra grow px rồi mờ mép — vùng cắt
    rộng hơn mảnh nên mảnh không bị cắt cụt, mảnh / quầng bên cạnh bị bỏ."""
    labels, n = ndi.label(alpha > thr)
    if n == 0:
        return alpha
    h, w = alpha.shape
    cy, cx = h / 2, w / 2
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    best, pick = 1e9, int(sizes.argmax())
    for i, sl in enumerate(ndi.find_objects(labels), 1):
        ys, xs = np.nonzero(labels[sl] == i)
        if len(ys) < max(12, sizes.max() * 0.25):
            continue
        d = np.hypot(ys.mean() + sl[0].start - cy, xs.mean() + sl[1].start - cx)
        if d < best:
            best, pick = d, i
    keep = ndi.binary_dilation(labels == pick, iterations=grow)
    return alpha * np.clip(ndi.gaussian_filter(keep.astype(np.float32), grow * 0.5), 0, 1)


def mirror_right(color, alpha, cx):
    """Nửa trái (bị trượng / tay che) thay bằng nửa phải lật gương quanh cột cx (toạ độ vùng cắt)."""
    color, alpha = color.copy(), alpha.copy()
    w = alpha.shape[1]
    for x in range(0, cx):
        src = 2 * cx - x
        if src < w:
            color[:, x] = color[:, src]
            alpha[:, x] = alpha[:, src]
        else:
            alpha[:, x] = 0
    return color, alpha


def patch(color, alpha, mask, how, at):
    """Vá vùng mask bằng ảnh lật gương: how = "v" lật quanh hàng at, "h" lật quanh cột at."""
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w]
    if how == "v":
        sy, sx = np.clip(2 * at - yy, 0, h - 1), xx
    else:
        sy, sx = yy, np.clip(2 * at - xx, 0, w - 1)
    m = mask[..., None]
    return color * (1 - m) + color[sy, sx] * m, alpha * (1 - mask) + alpha[sy, sx] * mask


def tileable(color, alpha):
    """Đoạn xích nằm ngang → cắt đúng một số nguyên chu kì mắt xích (tự dò chu kì bằng tự tương quan) để vẽ lặp (Tiled)
    liền mạch; hai đầu hoà vào nhau vài px."""
    prof = alpha.sum(0)
    prof = prof - prof.mean()
    best, period = -1e9, 16
    for p in range(10, min(60, len(prof) // 2)):
        c = (prof[:-p] * prof[p:]).mean()
        if c > best:
            best, period = c, p
    start = int(np.argmin(alpha.sum(0)[:period]))
    k = max(1, (alpha.shape[1] - start) // period)
    end = start + k * period
    color, alpha = color[:, start:end].copy(), alpha[:, start:end].copy()
    # hoà 3 px hai đầu (mép trái = tiếp nối mép phải)
    for i in range(3):
        t = (i + 1) / 4
        a, b = premul(color[:, i:i + 1], alpha[:, i:i + 1]), premul(color[:, -1 - i:alpha.shape[1] - i], alpha[:, -1 - i:alpha.shape[1] - i])
        m = a * (1 - t) + b * t
        c2, a2 = unpremul(m)
        color[:, i:i + 1], alpha[:, i:i + 1] = c2, a2
    print(f"   xích: chu kì {period} px, {k} chu kì")
    return color, alpha


def crop_tight(color, alpha, margin=3):
    ys, xs = np.nonzero(alpha > 0.012)
    if len(ys) == 0:
        raise ValueError("mảnh trống")
    y0, y1 = max(0, ys.min() - margin), min(alpha.shape[0], ys.max() + margin + 1)
    x0, x1 = max(0, xs.min() - margin), min(alpha.shape[1], xs.max() + margin + 1)
    return color[y0:y1, x0:x1], alpha[y0:y1, x0:x1], x0, y0


def resize(color, alpha, scale):
    """Thu nhỏ qua màu nhân alpha (không viền tối)."""
    h, w = alpha.shape
    size = (max(1, round(w * scale)), max(1, round(h * scale)))
    pre = premul(color, alpha)
    chans = [np.asarray(Image.fromarray(pre[..., c].astype(np.float32), "F").resize(size, Image.LANCZOS)) for c in range(4)]
    return unpremul(np.clip(np.dstack(chans), 0, 1))


# ----------------------------------------------------------------------------------------------------------------------

def region(spec, sheets):
    rgb, bg, ppu = sheets[spec["sheet"]]
    if "band" in spec:
        # dải nghiêng (đoạn xích): xoay cả tấm cho dải nằm ngang rồi cắt
        (ax, ay), (bx, by), half = spec["band"]
        angle = math.degrees(math.atan2(by - ay, bx - ax))
        cx, cy = (ax + bx) / 2, (ay + by) / 2
        length = math.hypot(bx - ax, by - ay)
        img = Image.fromarray(rgb.astype(np.uint8), "RGB").rotate(angle, Image.BICUBIC, center=(cx, cy), fillcolor=tuple(int(v) for v in bg))
        box = (int(cx - length / 2), int(cy - half), int(cx + length / 2), int(cy + half))
        return np.asarray(img.crop(box)).astype(np.float32), box, bg, ppu
    box = spec["box"]
    return rgb[box[1]:box[3], box[0]:box[2]], box, bg, ppu


def clean(spec, sheets):
    """Vùng cắt đã tách nền, bỏ nhân vật, dựng lại phần bị che → (màu, alpha, box)."""
    rgb, box, bg, ppu = region(spec, sheets)
    color, alpha = unmix(rgb, bg, spec["mode"], spec)
    off = (box[0], box[1])
    if spec.get("purple"):
        purple = smoothstep(rgb[..., 2] - rgb[..., 1], 15, 60)
        white = smoothstep(rgb.min(-1), 190, 235)
        alpha = alpha * np.maximum(purple, white)
    if spec.get("keep"):
        alpha = alpha * poly_mask(spec["keep"], alpha.shape, off, feather=spec.get("keep_feather", 2.5))
    if spec.get("erase"):
        alpha = alpha * (1 - poly_mask(spec["erase"], alpha.shape, off, feather=spec.get("feather", 2.0)))
    for poly, axis in spec.get("bridge", []):
        soft = poly_mask(poly, alpha.shape, off, feather=1.5)
        c2, a2 = bridge(color, alpha, poly_mask(poly, alpha.shape, off, feather=0) >= 0.5, axis)
        color = color * (1 - soft[..., None]) + c2 * soft[..., None]
        alpha = alpha * (1 - soft) + a2 * soft
    for poly, center, ramp in spec.get("radial", []):
        soft = poly_mask(poly, alpha.shape, off, feather=3.0)
        c2, a2 = radial(color, alpha, poly_mask(poly, alpha.shape, off, feather=0) >= 0.5, (center[0] - off[0], center[1] - off[1]), ramp)
        color = color * (1 - soft[..., None]) + c2 * soft[..., None]
        alpha = alpha * (1 - soft) + a2 * soft
    for poly, center in spec.get("rot", []):
        soft = poly_mask(poly, alpha.shape, off, feather=2.0)
        c2, a2 = rot_fill(color, alpha, poly_mask(poly, alpha.shape, off, feather=0) >= 0.5, (center[0] - off[0], center[1] - off[1]))
        color = color * (1 - soft[..., None]) + c2 * soft[..., None]
        alpha = alpha * (1 - soft) + a2 * soft
    for poly, how, at in spec.get("patch", []):
        color, alpha = patch(color, alpha, poly_mask(poly, alpha.shape, off), how, at - (box[1] if how == "v" else box[0]))
    if "mirror" in spec:
        color, alpha = mirror_right(color, alpha, spec["mirror"] - box[0])
    if "fill" in spec:
        color, alpha = fill_region(color, alpha, poly_mask(spec["fill"], alpha.shape, off, feather=4.0))
    if "core" in spec:
        (cx, cy), r, rgbc, k = spec["core"]
        color, alpha = glow_core(color, alpha, (cx - off[0], cy - off[1]), r, rgbc, k)
    for poly, a, b in spec.get("taper", []):
        # đuôi vệt bị cắt cụt: mờ dần theo hướng a → b trong vùng poly (nhọn dần như nét vẽ thật)
        h, w = alpha.shape
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        ax_, ay_, bx_, by_ = a[0] - off[0], a[1] - off[1], b[0] - off[0], b[1] - off[1]
        d2 = (bx_ - ax_) ** 2 + (by_ - ay_) ** 2
        t = ((xx - ax_) * (bx_ - ax_) + (yy - ay_) * (by_ - ay_)) / d2
        m = poly_mask(poly, alpha.shape, off, feather=3.0)
        alpha = alpha * (1 - m + m * smoothstep(t, 0, 1))
    if "disc" in spec:
        # khoét tâm tròn mềm (lớp mảnh vỡ để trống chỗ lõi chớp — lõi là lớp riêng)
        (cx, cy), r, soft = spec["disc"]
        h, w = alpha.shape
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        alpha = alpha * smoothstep(np.hypot(xx - cx + off[0], yy - cy + off[1]), r, r + soft)
    if spec.get("isolate"):
        alpha = isolate(alpha, thr=spec["isolate"] if not isinstance(spec["isolate"], bool) else 0.15)
        h, w = alpha.shape
        alpha = oval(alpha, (w / 2, h / 2), (w / 2, h / 2), 0.35)  # mảnh dính quầng bên cạnh: mờ dần trước mép vùng cắt
    if spec.get("largest"):
        alpha = keep_largest(alpha, count=spec["largest"] if isinstance(spec["largest"], int) and spec["largest"] > 1 else 1)
    if "fade" in spec:
        alpha = fade_edges(alpha, spec["fade"])
    if "oval" in spec:
        (cx, cy), radii, soft = spec["oval"]
        alpha = oval(alpha, (cx - off[0], cy - off[1]), radii, soft)
    if spec.get("tile"):
        color, alpha = tileable(color, alpha)
    if "gain" in spec and spec["mode"] not in ("soft", "ghost"):
        alpha = np.clip(alpha * spec["gain"], 0, 1)
    return color, alpha, box, ppu


def variant(color, alpha, box, var):
    """Lớp tách thêm từ cùng vùng đã làm sạch (gốc chung → chồng khít)."""
    color, alpha = color.copy(), alpha.copy()
    off = (box[0], box[1])
    kind = var["kind"]
    if kind == "core":
        # phần trắng rực (cộng sáng lên thân): kênh nhỏ nhất cao
        lo, hi = var.get("range", (0.62, 0.9))
        alpha = alpha * smoothstep(color.min(-1) * np.sqrt(alpha), lo, hi)
        alpha = ndi.gaussian_filter(alpha, var.get("blur", 0.8))
    elif kind == "rows":
        # nửa trên (above=True) / nửa dưới hàng y (px tranh), mép chuyển mềm
        h, w = alpha.shape
        yy = np.arange(h, dtype=np.float32)[:, None] + off[1]
        k = smoothstep(yy, var["y"] - var.get("soft", 6), var["y"] + var.get("soft", 6))
        alpha = alpha * ((1 - k) if var.get("above") else k)
    if var.get("keep"):
        alpha = alpha * poly_mask(var["keep"], alpha.shape, off, feather=var.get("feather", 2.5))
    if var.get("erase"):
        alpha = alpha * (1 - poly_mask(var["erase"], alpha.shape, off, feather=2.0))
    if "gain" in var:
        alpha = np.clip(alpha * var["gain"], 0, 1)
    return color, alpha


def finish(name, color, alpha, box, ppu, pivot, tile=False):
    if tile:
        x0, y0 = 0, 0  # đoạn lặp: giữ nguyên bề ngang đúng chu kì
    else:
        color, alpha, x0, y0 = crop_tight(color, alpha)
    h, w = alpha.shape
    if pivot == "center":
        px, py = w / 2, h / 2
    else:
        px, py = pivot[0] - box[0] - x0, pivot[1] - box[1] - y0
    color, alpha = resize(color, alpha, SCALE)
    image = Image.fromarray((np.dstack([color, alpha]) * 255 + 0.5).astype(np.uint8), "RGBA")
    info = dict(name=name, pixelsPerUnit=round(ppu * SCALE, 3), pivot=[round(px / w, 4), round(1 - py / h, 4)], size=list(image.size))
    return image, info


def chain_tile(ppu):
    """Một đoạn xích lặp liền mạch (2 mắt: mắt tròn nhìn mặt + mắt dẹt nhìn cạnh), vẽ lại cùng kiểu mắt xích trắng hồng viền
    tím của tấm áp chế (cắt từ tranh không lặp được vì xích trong tranh cong và chồng lên người)."""
    ss = 4
    R = 2  # vẽ gấp đôi độ phân giải tranh cho nét mảnh còn rõ (ppu cũng gấp đôi → cỡ trong game không đổi)
    P, H = 30 * R, 16 * R  # chu kì 2 mắt, cao
    W, Hh = P * ss, H * ss
    yy, xx = np.mgrid[0:Hh, 0:W].astype(np.float32) / ss
    cy = H / 2

    def ring(cx, rx, ry, t):
        r = np.hypot((xx - cx) / rx, (yy - cy) / ry)
        return np.clip(1 - np.abs(r - 1) * min(rx, ry) / t, 0, 1)

    # mắt tròn (mặt) ở giữa ô, mắt dẹt (cạnh) nối hai đầu ô (nửa trái + nửa phải ghép lại khi lặp)
    face = ring(P * 0.5, 9.5 * R, 5.6 * R, 1.4 * R)
    edge = np.zeros_like(face)
    for cx in (0.0, float(P)):
        d = np.clip(1 - np.maximum(np.abs(xx - cx) / (7.5 * R), 0) ** 6, 0, 1)
        band = np.clip(1 - np.abs(yy - cy) / (1.5 * R), 0, 1)
        edge = np.maximum(edge, d * band)
    line = np.maximum(face, edge)
    line_img = Image.fromarray((line * 255).astype(np.uint8)).resize((P, H), Image.LANCZOS)
    line = np.asarray(line_img).astype(np.float32) / 255
    glow = ndi.gaussian_filter(line, 1.6 * R, mode="wrap")
    alpha = np.clip(line + glow * 0.9, 0, 1)
    core = np.array([1.0, 0.93, 1.0])
    rim = np.array([0.82, 0.42, 1.0])
    k = np.clip(line / np.maximum(alpha, 1e-3), 0, 1)[..., None]
    color = rim + (core - rim) * k
    return color, alpha


def cut(spec, sheets):
    """→ [(tên, ảnh, info)] cho mảnh chính + các lớp tách thêm."""
    if spec.get("procedural") == "chain":
        color, alpha = chain_tile(spec["ppu"])
        image, info = finish(spec["name"], color, alpha, (0, 0, 0, 0), spec["ppu"] * 2, "center", tile=True)
        return [(spec["name"], image, info)]
    color, alpha, box, ppu = clean(spec, sheets)
    out = []
    if not spec.get("variants_only"):
        image, info = finish(spec["name"], color, alpha, box, ppu, spec["pivot"], spec.get("tile", False))
        out.append((spec["name"], image, info))
    for var in spec.get("variants", []):
        c, a = variant(color, alpha, box, var)
        image, info = finish(var["name"], c, a, box, ppu, var.get("pivot", spec["pivot"]))
        out.append((var["name"], image, info))
    return out


# ----------------------------------------------------------------------------------------------------------------------
# Biểu tượng 3 nút chiêu (tròn 256 px, cùng kiểu Tools/art/skill_icons.py / hero_ui.py): một khung chính của tấm "thường"
# mỗi nút — 1 = đánh thường khung 4 (đâm trượng, vệt trăng to), 2 = dùng chiêu áp sát khung 4 (lướt quét lưỡi liềm),
# 3 = kĩ năng đặc biệt khung 5 (mái vòm pha lê nổ, nền đỏ tím rực như mọi chiêu cuối). Ghi đè đúng file cũ (giữ GUID).
# ----------------------------------------------------------------------------------------------------------------------

ICON = 256
ICONS = [
    dict(file="Skill_Pink_1.png", sheet="basic", box=(1085, 170, 1480, 570), fit=244, offset=(4, 2),
         inner=(0.25, 0.13, 0.32), outer=(0.06, 0.03, 0.1), rim=(0.58, 0.32, 0.75)),
    dict(file="Skill_Pink_2.png", sheet="dash", box=(1092, 255, 1545, 522), fit=246, offset=(0, 6),
         inner=(0.43, 0.23, 0.55), outer=(0.12, 0.03, 0.2), rim=(0.85, 0.85, 0.9)),
    dict(file="Skill_Pink_3.png", sheet="nova", box=(1462, 95, 1892, 655), fit=236, offset=(0, 4),
         inner=(0.55, 0.12, 0.2), outer=(0.15, 0.03, 0.08), rim=(0.95, 0.4, 0.35), rays=True, solid=(22, 70)),
]


def icon_art(spec, sheets):
    """Khung tranh tách khỏi nền xanh đen; người (áo, tóc tối) giữ đặc, quầng sáng giữ mềm."""
    rgb, bg, _ = sheets[spec["sheet"]]
    x0, y0, x1, y1 = spec["box"]
    region = rgb[y0:y1, x0:x1]
    color, alpha = unmix(region, bg, "plain", {})
    dist = np.sqrt(((region - bg) ** 2).sum(-1))
    lo, hi = spec.get("solid", (8, 40))  # khác nền bấy nhiêu thì coi là đặc (áo, tóc tối của nhân vật)
    alpha = np.maximum(alpha, smoothstep(dist, lo, hi))
    color = np.clip((bg + (region - bg) / np.maximum(alpha, 1e-3)[..., None]) / 255, 0, 1)
    color, alpha, _, _ = crop_tight(color, alpha)
    h, w = alpha.shape
    color, alpha = resize(color, alpha, spec["fit"] / max(w, h))
    return Image.fromarray((np.dstack([color, alpha]) * 255 + 0.5).astype(np.uint8), "RGBA")


def make_icon(spec, sheets, path):
    yy, xx = np.mgrid[0:ICON, 0:ICON].astype(np.float32) + 0.5
    r = np.hypot(xx - ICON / 2, yy - ICON / 2)
    k = np.clip(r / (ICON / 2), 0, 1)[..., None]
    base = np.array(spec["inner"]) * (1 - k) + np.array(spec["outer"]) * k
    canvas = Image.fromarray((np.dstack([base, np.ones(k.shape)]) * 255).astype(np.uint8), "RGBA")
    if spec.get("rays"):
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(layer)
        c = ICON / 2
        for i in range(12):
            a, b = math.radians(i * 30 + 8), math.radians(i * 30 + 17)
            draw.polygon([(c, c), (c + math.cos(a) * c * 1.1, c + math.sin(a) * c * 1.1), (c + math.cos(b) * c * 1.1, c + math.sin(b) * c * 1.1)],
                         fill=(255, 140, 100, 60))
        canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(3)))
    art = icon_art(spec, sheets)
    pos = ((ICON - art.width) // 2 + spec["offset"][0], (ICON - art.height) // 2 + spec["offset"][1])
    # quầng sáng tím sau hình
    halo = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    glow = Image.new("RGBA", art.size, (200, 110, 255, 0))
    glow.putalpha(art.getchannel("A").point(lambda v: int(v * 0.8)))
    halo.alpha_composite(glow, pos)
    canvas.alpha_composite(halo.filter(ImageFilter.GaussianBlur(9)))
    canvas.alpha_composite(art, pos)
    arr = np.asarray(canvas).astype(np.float32) / 255
    rim = np.exp(-((r - (ICON / 2 - 10)) / 7) ** 2)[..., None] * np.array(spec["rim"])  # viền sáng mép trong
    arr[..., :3] = 1 - (1 - np.clip(arr[..., :3], 0, 1)) * np.exp(-rim)
    arr[..., 3] = np.clip(ICON / 2 - 2 - r + 0.5, 0, 1)
    Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(path)
    print("->", path.relative_to(ROOT))


def preview(items, path):
    """Ảnh xem: mỗi mảnh trên nền tối (tranh), nền sáng (lộ viền xanh đen / khung vuông) và nền cam; dấu + ở gốc."""
    font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 13)
    cells = []
    for name, img, info in items:
        w, h = img.size
        cell = Image.new("RGB", (w * 3 + 24, h + 22), (24, 24, 28))
        for i, bgc in enumerate([(9, 17, 52), (236, 234, 240), (176, 96, 44)]):
            base = Image.new("RGBA", (w, h), bgc + (255,))
            base.alpha_composite(img)
            d = ImageDraw.Draw(base)
            px, py = info["pivot"][0] * w, (1 - info["pivot"][1]) * h
            d.line([(px - 7, py), (px + 7, py)], fill=(0, 255, 0), width=2)
            d.line([(px, py - 7), (px, py + 7)], fill=(0, 255, 0), width=2)
            cell.paste(base.convert("RGB"), (i * (w + 8), 22))
        ImageDraw.Draw(cell).text((3, 3), f"{name} {w}x{h} ppu {info['pixelsPerUnit']}", fill=(255, 255, 255), font=font)
        cells.append(cell)
    # xếp theo hàng, mỗi hàng rộng tối đa 2400 px
    rows, row, width = [], [], 0
    for c in cells:
        if row and width + c.width > 2400:
            rows.append(row)
            row, width = [], 0
        row.append(c)
        width += c.width + 8
    if row:
        rows.append(row)
    W = max(sum(c.width + 8 for c in r) for r in rows)
    H = sum(max(c.height for c in r) + 8 for r in rows)
    sheet = Image.new("RGB", (W, H), (14, 14, 16))
    y = 0
    for r in rows:
        x = 0
        for c in r:
            sheet.paste(c, (x, y))
            x += c.width + 8
        y += max(c.height for c in r) + 8
    sheet.save(path)
    print("xem:", path, sheet.size)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--sheets", default=str(ROOT / "Docs/nhan_vat_moi/pink"))
    parser.add_argument("--preview", default=None)
    parser.add_argument("--only", nargs="*", default=None)
    parser.add_argument("--no-write", action="store_true", help="chỉ xem, không ghi ảnh vào Resources")
    args = parser.parse_args()

    sheets = {}
    for key, (rel, ppu) in SHEETS.items():
        rgb = np.asarray(Image.open(Path(args.sheets) / rel).convert("RGB")).astype(np.float32)
        sheets[key] = (rgb, background(rgb), ppu)

    OUT.mkdir(parents=True, exist_ok=True)
    manifest_path = OUT / "fx_manifest.json"
    manifest = {}
    if manifest_path.exists():
        manifest = {s["name"]: s for s in json.loads(manifest_path.read_text()).get("sprites", [])}
    items = []
    names = []
    for spec in FX:
        produced = [spec["name"]] if not spec.get("variants_only") else []
        produced += [v["name"] for v in spec.get("variants", [])]
        names += produced
        if args.only and not any(n in args.only for n in produced + [spec["name"]]):
            continue
        for name, image, info in cut(spec, sheets):
            if not args.no_write:
                image.save(OUT / f"{name}.png")
            manifest[name] = info
            items.append((name, image, info))
            print("->", name, image.size)
    if not args.no_write:
        manifest_path.write_text(json.dumps({"sprites": [manifest[n] for n in names if n in manifest]}, indent=1, ensure_ascii=False) + "\n")
        if not args.only:
            for spec in ICONS:
                make_icon(spec, sheets, ROOT / "Assets/_Project/Art/UI" / spec["file"])
    if args.preview and items:
        Path(args.preview).mkdir(parents=True, exist_ok=True)
        preview(items, Path(args.preview) / "hero_fx_pink.png")


if __name__ == "__main__":
    main()
