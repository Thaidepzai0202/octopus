"""Hiệu ứng chiêu của RED cắt thẳng từ các tấm chiêu của người dùng (Docs/nhan_vat_moi/red/<loại>/<tên chiêu>.png).

Mỗi chiêu dùng hình cắt từ CHÍNH tấm của nó, tách thành nhiều lớp để code diễn riêng từng lớp (RedFx.cs):
  - vệt chém theo PHA (cùng một mốc = ngực / chân RED trong khung đó → đổi pha không bị giật): Đánh thường khung 3 / 4 / 5
    (chém ngang, chém ngược tay, bổ xuống + nổ đất), Đánh thường hất tung khung 2 → 3 → 4 → 5 (quệt đất → quét lên → vượt
    đầu → thu vệt), Đánh thường áp sát khung 4 (chém vòng ngang) + dải tốc độ khung 3, Kĩ năng đặc biệt khung 2 → 3 (cung
    lớn → cung thứ hai) → khung 4 (hai nhánh chữ X riêng + mảnh vỡ / đá);
  - lõi trắng rực tách riêng (<tên>_core, phát cộng sáng rồi tắt trước) và thân đỏ đã bớt trắng (ảnh <tên>) cho các vệt chém;
  - nửa SAU / nửa TRƯỚC người (<tên>_back / <tên>_front) cho các vòng quanh thân: vòng chém xoay, vòng nổ tinh thể, vòng phép,
    vòng xích trên đầu, vòng phép dưới lồng;
  - sóng bay tách ĐẦU (<tên>) và ĐUÔI (<tên>_tail: code kéo dài đuôi riêng theo quãng bay; mép cuối đuôi mờ theo đường cong,
    không còn cạnh cắt thẳng);
  - lồng xích tách lớp: vòng xích trên đầu, vòng quấn thân (lát nhiều tầng), gai hai bên, vòng phép dưới chân, chớp cú đấm.
Cách cắt:
  1. Màu nền xanh đen ở từng chỗ: lấy các điểm giống nền rồi làm mờ có trọng số (nền hơi loang, có nhiễu).
  2. Alpha kiểu "màu ra trong suốt": mỗi điểm = trộn giữa màu hiệu ứng và nền, alpha nhỏ nhất đủ để giải ra màu hiệu ứng
     (sáng hơn nền: đẩy về trắng; tối hơn nền: đẩy về đen) → quầng sáng mềm giữ đúng độ mờ, màu thật của lõi (bỏ nhân trước).
     Đá / thân tối mà không ngả xanh như nền thì đặc lại (đá vỡ không bị trong). Lọc "chỉ phần phát sáng" (glow) bỏ viền tối
     lem nền navy (nhìn thấy trên nền sáng).
  3. Bỏ phần không phải hiệu ứng (người RED, kiếm, tóc, hình nộm, khung bên cạnh) bằng đa giác; chỗ bị che thì DỰNG LẠI bằng
     chính nét của hiệu ứng: nội suy theo cung elip của vệt (toạ độ cực quanh tâm cung: mỗi vòng bán kính nối hai đầu nét còn
     nguyên, chỉ còn một phía thì kéo dài nét ra, thon và mờ dần = dựng lại đuôi bị cắt), hoặc lấy nét đối xứng qua tâm (chữ X),
     hoặc nửa đối xứng của vòng phép. Tâm vụ nổ khuất sau người: lót quầng theo màu trung bình từng vòng của chính vụ nổ.
  4. Mép mờ chỉ ở chỗ hình thật sự hết; cắt sát, ghi pivot (0..1 tính từ góc dưới trái) vào pivots.json cho
     Editor/HeroFxImporter_Red.cs đặt khi import.
Tỉ lệ: PPU 150 (nhân vật trên tấm cao ~290 px ≈ 1.94 unit trong game) nên hiệu ứng to đúng như tranh so với người.

Cùng lúc vẽ lại 3 biểu tượng nút chiêu của RED (tròn 256 px, kiểu Tools/art/skill_icons.py / hero_ui.py) từ khung đinh của
tấm chiêu thường mỗi nút: 1 = Đánh thường khung 3 (chém ngang), 2 = Kĩ năng khung 5 (nổ vòng tinh thể), 3 = Kĩ năng đặc biệt
khung 4 (chém chữ X). Ghi đè đúng file cũ (giữ .meta / GUID). Lưu ý: hero_ui.py cũng ghi Skill_Red_*.png (bản tạm) — chạy
hero_ui.py thì chạy lại script này sau.

Chạy:  python3 Tools/art/hero_fx_red.py [thư mục Docs] [thư mục xem trước]
Ra:    Assets/_Project/Resources/HeroFx/Red/<tên>.png + pivots.json (RedFx nạp bằng Resources.Load<Sprite>)
       Assets/_Project/Art/UI/Skill_Red_1..3.png
       Ảnh cũ không còn dùng (x_slash, cage, crystal_ring, magic_circle bản liền) bị xoá cùng .meta.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[2]
DOCS = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "Docs"
PREVIEW = Path(sys.argv[2]) if len(sys.argv) > 2 else None
OUT = ROOT / "Assets/_Project/Resources/HeroFx/Red"
ICONS = ROOT / "Assets/_Project/Art/UI"
ICON = 256
PPU = 150.0

SHEETS = {
    "danh_thuong": "basic_attack/Đánh thường.png",
    "hat_tung": "basic_attack/Đánh thường hất tung.png",
    "ap_sat": "basic_attack/Đánh thường áp sát.png",
    "ki_nang": "skill/Kĩ năng.png",
    "tam_xa": "skill/Kĩ năng tầm xa.png",
    "thu": "skill/Kĩ năng thủ.png",
    "dac_biet": "special_skill/Kĩ năng đặc biệt.png",
    "dac_biet_tam_xa": "special_skill/Kĩ năng đặc biệt tầm xa.png",
    "ap_che": "special_skill/Kĩ năng đặc biệt áp chế.png",
}

# Ảnh cũ đã thay bằng các lớp tách riêng: xoá khi chạy lại
OBSOLETE = ["x_slash", "cage", "crystal_ring", "magic_circle"]

# ---------------------------------------------------------------------------------------------------------------------
# Mỗi mảnh: tấm, vùng cắt (trái, trên, phải, dưới; px trên tấm 2172 x 724), pivot (px trên tấm), feather = mép mờ (trái,
# trên, phải, dưới) chỉ ở cạnh mà hình thật sự hết. Các bước tuỳ chọn (theo thứ tự chạy):
#   glow=(lo, hi)        chỉ giữ điểm có độ sáng (kênh lớn nhất) ≥ lo..hi: bỏ viền tối / bóng của người lẫn vào
#   bright_only=[đa giác] trong vùng này chỉ giữ phần sáng rực (lớn hơn bright_lo..bright_hi)
#   circle / patch       vòng phép bị chân / kiếm che: vá bằng góc khác của vòng
#   hole=[đa giác] + fill=dict(center, axes, …)  dựng lại nét theo cung elip (xem polar_fill)
#   mirror=dict(center, hole)                    dựng lại bằng điểm đối xứng qua tâm
#   arm=dict(p0, p1, width) / not_arms=[…]       chỉ giữ / bỏ dải thẳng (nhánh chữ X)
#   ring=dict(center, axes, r, span)             chỉ giữ vành elip (bỏ mảnh lạc ngoài vệt)
#   radial_fade=dict(center, axes, r)            mờ dần theo khoảng cách elip từ center (đuôi tự tắt theo đường cong)
#   drop=[…] / keep=[…]                          bỏ / chỉ giữ đa giác
#   gate                                         bỏ nhiễu alpha thấp
# Đầu ra thêm:
#   split=dict(y, feather)    → <tên>_back (phần trên đường y: vẽ sau người) + <tên>_front (phần dưới: vẽ trước người)
#   core=True                 → <tên>_core (lõi trắng rực) + <tên> (thân đã ngả đỏ chỗ lõi)
#   tail=dict(r=(r0, r1), axes) → <tên> (đầu sóng: trong bán kính r0..r1 quanh pivot) + <tên>_tail (phần còn lại)
# ---------------------------------------------------------------------------------------------------------------------

XC = (1275, 362)  # tâm chữ X (Kĩ năng đặc biệt khung 4)
XHOLE = dict(center=XC, feather=10,
             hole=[[(1100, 335), (1190, 322), (1236, 352), (1240, 430), (1230, 500), (1150, 510), (1100, 470)]])
XA = dict(p0=(1110, 70), p1=(1450, 650), width=150, soft=22)   # nhánh "\"
XB = dict(p0=(1452, 50), p1=(1095, 640), width=150, soft=22)   # nhánh "/"

ELEMENTS = [
    # ===== #0 Đánh thường: 3 bộ vệt riêng (pivot = ngực RED trong khung; chân RED cho nhát bổ) =====
    # khung 3: chém ngang tới trước (lưỡi liềm ">" mũi ở phải); người + kiếm che nửa trái → dựng lại theo cung
    dict(name="combo_h", sheet="danh_thuong", box=(760, 262, 1140, 500), pivot=(835, 398), feather=(4, 6, 10, 6),
         hole=[[(760, 262), (938, 262), (958, 300), (956, 372), (905, 384), (898, 432), (938, 445), (985, 468), (990, 500),
                (760, 500)], [(895, 381), (1068, 386), (1068, 404), (995, 423), (895, 430)]],
         fill=dict(center=(930, 385), axes=(192, 80), extend=28, max_gap=70, span=(262, 118)),
         drop=[[(1102, 262), (1140, 262), (1140, 352), (1112, 350), (1098, 300)]], drop_feather=14, glow=(0.2, 0.4),
         gate=0.03, core=True),
    # khung 4: chém ngược tay — vệt dài từ sau lưng trên cao quét xuống trước thấp (cung tròn tâm trên cao bên phải)
    dict(name="combo_back", sheet="danh_thuong", box=(1080, 228, 1490, 500), pivot=(1290, 398), feather=(6, 4, 10, 6),
         hole=[[(1172, 228), (1420, 228), (1420, 440), (1398, 452), (1398, 500), (1172, 500), (1172, 360)]],
         fill=dict(center=(1512, -25), axes=(498, 498), extend=12, max_gap=45, span=(88, 152), rband=(0.9, 1.07)),
         drop=[[(1080, 352), (1176, 345), (1176, 500), (1080, 500)],
               [(1410, 228), (1490, 228), (1490, 500), (1466, 500), (1470, 446), (1420, 440), (1408, 400)]],
         drop_feather=16, glow=(0.22, 0.42), gate=0.04, core=True),
    # khung 5: giơ kiếm bổ xuống — cung từ sau đầu vòng qua trên xuống chạm đất trước mặt (pivot = chân RED)
    dict(name="combo_slam", sheet="danh_thuong", box=(1415, 108, 1815, 552), pivot=(1590, 545), feather=(10, 4, 14, 40),
         hole=[[(1415, 300), (1470, 250), (1560, 205), (1585, 172), (1605, 160), (1650, 196), (1692, 232), (1702, 292),
                (1698, 362), (1692, 420), (1704, 470), (1712, 552), (1415, 552)],
               [(1668, 384), (1708, 384), (1786, 520), (1760, 552), (1680, 462)]],
         fill=dict(center=(1560, 520), axes=(190, 372), extend=18, max_gap=90, span=(170, 20), rband=(0.82, 1.2)),
         glow=(0.22, 0.42), gate=0.04, core=True),
    # khung 5: nổ đất chỗ mũi kiếm chạm (gốc ở mặt đất)
    dict(name="combo_slam_hit", sheet="danh_thuong", box=(1640, 430, 1856, 566), pivot=(1745, 548), feather=(30, 66, 14, 4),
         drop=[[(1640, 430), (1712, 430), (1768, 516), (1752, 532), (1712, 520), (1712, 566), (1640, 566)],
               [(1800, 420), (1856, 420), (1856, 530), (1812, 530)],
               [(1700, 430), (1762, 430), (1766, 488), (1742, 494), (1706, 470)]],
         drop_feather=10, glow=(0.2, 0.4), gate=0.03),

    # ===== #1 Đánh thường hất tung: chuỗi 4 pha, mốc chung = ngực RED trong từng khung =====
    dict(name="rise_0", sheet="hat_tung", box=(400, 580, 712, 670), pivot=(548, 488), feather=(30, 8, 40, 6),
         drop=[[(395, 520), (508, 520), (508, 608), (478, 628), (436, 640), (395, 640)],
               [(556, 540), (646, 540), (646, 636), (556, 636)]],
         drop_feather=12, glow=(0.24, 0.44), gate=0.04),
    dict(name="rise_1", sheet="hat_tung", box=(706, 160, 1098, 668), pivot=(895, 445), feather=(10, 8, 10, 10),
         hole=[[(706, 280), (860, 236), (962, 284), (972, 380), (952, 424), (932, 474), (908, 522), (880, 562), (842, 604),
                (800, 654), (706, 668)], [(940, 226), (1026, 226), (1030, 300), (998, 392), (936, 392)]],
         fill=dict(center=(715, 264), axes=(374, 374), extend=16, max_gap=60, span=(330, 95), rband=(0.72, 1.08)),
         glow=(0.22, 0.42), gate=0.04, core=True),
    # khung 4 (tên cũ slash_v): lưỡi liềm dọc vượt quá đầu
    dict(name="slash_v", sheet="hat_tung", box=(1150, 0, 1515, 664), pivot=(1245, 372), feather=(10, 2, 12, 10),
         hole=[[(1120, 190), (1240, 176), (1322, 226), (1366, 282), (1374, 362), (1356, 432), (1332, 474), (1282, 524),
                (1252, 600), (1150, 610)], [(1176, 6), (1226, 4), (1360, 206), (1350, 270), (1296, 262), (1178, 72)]],
         fill=dict(center=(1168, 349), axes=(301, 301), extend=14, max_gap=60, span=(262, 100), rband=(0.62, 1.1)),
         glow=(0.22, 0.42), gate=0.04, core=True),
    dict(name="rise_3", sheet="hat_tung", box=(1528, 78, 1842, 560), pivot=(1655, 425), feather=(8, 6, 8, 30),
         hole=[[(1528, 200), (1600, 188), (1702, 228), (1744, 298), (1754, 380), (1760, 440), (1742, 484), (1724, 562),
                (1528, 562)], [(1528, 70), (1572, 70), (1716, 196), (1700, 250), (1650, 240), (1528, 130)]],
         fill=dict(center=(1447, 463), axes=(370, 370), extend=14, max_gap=60, span=(268, 60), rband=(0.82, 1.12)),
         glow=(0.22, 0.42), gate=0.04),

    # ===== #2 Đánh thường áp sát =====
    # khung 4: chém vòng NGANG tới trước (lưỡi liềm ")" trước mặt; kiếm đè ngang giữa → dựng lại theo cung), pivot = ngực
    dict(name="dash_h", sheet="ap_sat", box=(1250, 236, 1530, 500), pivot=(1290, 385), feather=(6, 6, 6, 10),
         hole=[[(1268, 370), (1462, 384), (1462, 422), (1268, 428)]],
         fill=dict(center=(1390, 370), axes=(132, 102), extend=10, max_gap=60, span=(240, 120)),
         ring=dict(center=(1390, 370), axes=(132, 102), r=(0.6, 1.25), span=(244, 118)),
         drop=[[(1505, 236), (1530, 236), (1530, 352), (1505, 330)], [(1515, 372), (1530, 372), (1530, 500), (1490, 500)],
               [(1320, 236), (1388, 236), (1388, 302), (1320, 302)], [(1290, 452), (1362, 452), (1362, 500), (1290, 500)]],
         drop_feather=10, glow=(0.22, 0.42), gate=0.04, core=True),
    # khung 3: dải tốc độ đỏ sau lưng (pivot = mép phải, nối vào lưng)
    dict(name="dash_lines", sheet="ap_sat", box=(684, 262, 784, 505), pivot=(784, 400), feather=(30, 6, 24, 6),
         drop=[[(684, 436), (790, 436), (790, 505), (684, 505)], [(684, 262), (692, 262), (692, 395), (684, 395)],
               [(722, 332), (784, 332), (784, 384), (722, 384)]],
         drop_feather=8, glow=(0.24, 0.45), gate=0.05),
    # khung 5: bụi tung lúc trượt dừng (gốc ở mặt đất, tung về phía sau = trái)
    dict(name="dust", sheet="ap_sat", box=(1468, 428, 1652, 514), pivot=(1600, 508), feather=(8, 8, 14, 4), gray_only=True),

    # ===== #3 Kĩ năng: một vòng → nhiều vòng → nổ; vòng nào cũng tách nửa sau / nửa trước người =====
    dict(name="spin_bolt", sheet="ki_nang", box=(48, 168, 152, 330), pivot=(140, 300), feather=(8, 8, 14, 10),
         drop=[[(118, 290), (152, 270), (152, 330), (118, 330)]], drop_feather=10, glow=(0.3, 0.55), gate=0.05),
    dict(name="spin_ring1", sheet="ki_nang", box=(600, 250, 1010, 545), pivot=(818, 405), feather=(6, 10, 12, 8),
         hole=[[(752, 262), (900, 248), (968, 300), (972, 372), (1010, 380), (1010, 452), (962, 470), (958, 545), (760, 545),
                (744, 470), (748, 360)]],
         fill=dict(center=(812, 402), axes=(196, 78), extend=30, max_gap=150, rband=(0.55, 1.35)),
         ring=dict(center=(812, 402), axes=(196, 78), r=(0.45, 1.5)),
         drop=[[(600, 490), (672, 490), (672, 545), (600, 545)]],
         drop_feather=10, glow=(0.22, 0.42), gate=0.04, split=dict(y=402, feather=14)),
    dict(name="spin_rings", sheet="ki_nang", box=(990, 190, 1385, 590), pivot=(1190, 420), feather=(8, 8, 10, 8),
         hole=[[(1100, 232), (1290, 228), (1312, 300), (1300, 372), (1320, 420), (1306, 470), (1300, 580), (1100, 580),
                (1096, 470), (1036, 436), (1030, 330), (1092, 300)]],
         fill=dict(center=(1185, 410), axes=(196, 150), extend=30, max_gap=150, rband=(0.25, 1.3)),
         glow=(0.22, 0.42), gate=0.04, split=dict(y=410, feather=16)),
    # khung 5: nổ vòng tinh thể (pivot = chân RED); giữa khuất sau người → lót quầng theo chính màu vụ nổ
    dict(name="crystal_ring", sheet="ki_nang", box=(1386, 120, 1866, 592), pivot=(1617, 545), feather=(24, 10, 24, 10),
         hole=[[(1540, 296), (1690, 296), (1714, 380), (1708, 470), (1702, 550), (1528, 550), (1518, 470), (1512, 380)]],
         fill=dict(center=(1612, 430), axes=(150, 140), extend=20, max_gap=110, core=0.4, remap=0.7),
         glow=(0.16, 0.36), gate=0.03, split=dict(y=505, feather=20)),

    # ===== #4 Kĩ năng tầm xa =====
    # khung 3: cung bung ngay mũi kiếm lúc đâm (pivot = mũi kiếm)
    dict(name="wave_launch", sheet="tam_xa", box=(856, 280, 1112, 526), pivot=(1000, 428), feather=(8, 8, 8, 8),
         hole=[[(860, 390), (1012, 398), (1034, 438), (1012, 458), (860, 474)], [(820, 280), (892, 280), (892, 424), (820, 424)]],
         fill=dict(center=(900, 405), axes=(192, 112), extend=26, max_gap=80, span=(232, 128)),
         glow=(0.22, 0.42), gate=0.04, core=True),
    # khung 4: sóng lưỡi liềm đang bay — đầu sóng (pivot) + đuôi riêng
    dict(name="wave", sheet="tam_xa", box=(1098, 250, 1572, 560), pivot=(1556, 415), feather=(4, 10, 6, 10),
         radial_fade=dict(center=(1556, 415), axes=(1.0, 0.75), r=(330, 455)),
         tail=dict(r=(150, 230), axes=(1.0, 1.6))),
    # khung 5: nổ tinh thể (gốc ở mặt đất)
    dict(name="wave_burst", sheet="tam_xa", box=(1570, 130, 1872, 568), pivot=(1745, 545), feather=(24, 10, 20, 8)),

    # ===== #5 Kĩ năng thủ =====
    # khung 4: vòng phép đỏ dưới chân (chân + kiếm che: vá bằng góc khác của vòng); tách nửa sau / trước
    dict(name="magic_circle", sheet="thu", box=(975, 470, 1400, 610), pivot=(1185.5, 535.5), feather=(0, 0, 0, 0),
         circle=(1185.5, 535.5, 196, 53),
         patch=[[(1095, 450), (1142, 450), (1145, 500), (1138, 532), (1120, 544), (1082, 544), (1078, 520), (1090, 495)],
                [(1170, 450), (1207, 450), (1218, 475), (1232, 515), (1225, 529), (1193, 529), (1175, 522), (1172, 500)],
                [(1226, 450), (1264, 450), (1261, 530), (1250, 551), (1236, 551), (1227, 530)]],
         split=dict(y=535.5, feather=8)),
    # khung 2: nứt đất đỏ toả ra từ chỗ cắm kiếm (kiếm + giày bỏ, khe nứt dựng lại theo góc quanh mũi kiếm)
    dict(name="guard_cracks", sheet="thu", box=(400, 505, 690, 592), pivot=(555, 545), feather=(30, 14, 30, 8),
         hole=[[(528, 505), (588, 505), (580, 548), (536, 548)]],
         fill=dict(center=(555, 545), axes=(1, 1), extend=20, max_gap=90),
         drop=[[(460, 505), (530, 505), (530, 532), (470, 536)], [(660, 505), (690, 505), (690, 560), (660, 560)]],
         drop_feather=10, glow=(0.2, 0.4), gate=0.04),
    # khung 6: cột sáng mảnh, pha lê hình thoi bay lên
    dict(name="pillar", sheet="thu", box=(2060, 262, 2090, 425), pivot=(2075, 425), feather=(6, 30, 6, 6)),
    dict(name="crystal", sheet="thu", box=(2082, 266, 2120, 336), pivot=(2101, 301), feather=(4, 4, 4, 4)),
    dict(name="crystal_small", sheet="thu", box=(1830, 296, 1862, 350), pivot=(1846, 323), feather=(4, 4, 4, 4)),

    # ===== #6 Kĩ năng đặc biệt: cung lớn (khung 2) → cung thứ hai (khung 3) → hai nhánh X + mảnh vỡ (khung 4) =====
    dict(name="slash_big", sheet="dac_biet", box=(352, 150, 745, 604), pivot=(550, 445), feather=(30, 8, 22, 10),
         hole=[[(352, 270), (440, 260), (560, 300), (612, 338), (630, 420), (612, 500), (586, 540), (520, 604), (352, 604)]],
         fill=dict(center=(500, 380), axes=(210, 210), extend=20, max_gap=80, span=(250, 110), rband=(0.8, 1.2)),
         glow=(0.22, 0.42), gate=0.04, core=True),
    dict(name="db_arc2", sheet="dac_biet", box=(840, 150, 1080, 600), pivot=(935, 450), feather=(20, 8, 6, 10),
         hole=[[(840, 280), (930, 280), (1000, 330), (1010, 420), (990, 480), (940, 540), (840, 560)]],
         fill=dict(center=(780, 400), axes=(240, 240), extend=16, max_gap=60, span=(280, 80), rband=(0.8, 1.25)),
         drop=[[(1040, 150), (1080, 150), (1080, 215), (1040, 215)]], glow=(0.22, 0.42), gate=0.04, core=True),
    dict(name="x_arm_a", sheet="dac_biet", box=(1062, 30, 1505, 690), pivot=XC, feather=(20, 10, 30, 10), mirror=XHOLE,
         arm=XA, glow=(0.2, 0.4), gate=0.03),
    dict(name="x_arm_b", sheet="dac_biet", box=(1062, 30, 1505, 690), pivot=XC, feather=(20, 10, 30, 10), mirror=XHOLE,
         arm=XB, glow=(0.2, 0.4), gate=0.03),
    dict(name="x_debris", sheet="dac_biet", box=(1062, 30, 1505, 690), pivot=XC, feather=(30, 10, 60, 10), mirror=XHOLE,
         not_arms=[dict(XA, width=120), dict(XB, width=120)], gate=0.05),

    # ===== #7 Kĩ năng đặc biệt tầm xa =====
    # khung 3: lưỡi liềm khổng lồ bung ở mũi kiếm (cắt rộng sang trái, đuôi cung mờ theo đường cong; pivot = mũi kiếm)
    dict(name="wave_big_launch", sheet="dac_biet_tam_xa", box=(690, 140, 1056, 604), pivot=(904, 442), feather=(10, 8, 10, 8),
         hole=[[(600, 360), (712, 360), (712, 560), (600, 560)], [(690, 424), (908, 426), (908, 460), (690, 464)]],
         fill=dict(center=(720, 400), axes=(330, 205), extend=24, max_gap=80, span=(236, 124)),
         radial_fade=dict(center=(1060, 400), axes=(1.0, 1.25), r=(220, 365)),
         glow=(0.2, 0.4), gate=0.03, core=True),
    # khung 4: sóng khổng lồ — đầu sóng + đuôi lửa đỏ đen riêng (mép cuối đuôi mờ theo elip, không còn cạnh thẳng)
    dict(name="wave_big", sheet="dac_biet_tam_xa", box=(1054, 185, 1553, 600), pivot=(1546, 415), feather=(4, 12, 4, 12),
         radial_fade=dict(center=(1546, 415), axes=(1.0, 0.85), r=(360, 488)),
         tail=dict(r=(170, 260), axes=(1.0, 1.5))),
    # khung 5: nổ lớn, cột đá đỏ trồi lên (gốc ở mặt đất)
    dict(name="burst_big", sheet="dac_biet_tam_xa", box=(1556, 10, 1985, 600), pivot=(1752, 560), feather=(36, 8, 30, 10)),

    # ===== #8 Kĩ năng đặc biệt áp chế khung 5: lồng xích tách lớp (không còn hình nộm) =====
    dict(name="cage_top", sheet="ap_che", box=(1586, 100, 1806, 300), pivot=(1690, 225), feather=(6, 6, 6, 16),
         drop=[[(1586, 262), (1640, 262), (1640, 300), (1586, 300)],
               [(1626, 256), (1650, 240), (1700, 236), (1732, 252), (1744, 300), (1622, 300)]],
         drop_feather=8, gate=0.04, split=dict(y=228, feather=10)),
    dict(name="cage_wrap", sheet="ap_che", box=(1618, 336, 1744, 394), pivot=(1681, 365), feather=(10, 6, 10, 6),
         keep=[[(1632, 346), (1650, 340), (1700, 338), (1732, 346), (1732, 388), (1700, 392), (1650, 392), (1632, 384)]],
         keep_feather=12, bright_only=[[(1618, 336), (1744, 336), (1744, 394), (1618, 394)]], bright_lo=0.55, bright_hi=0.8,
         gate=0.05),
    dict(name="cage_spikes", sheet="ap_che", box=(1586, 236, 1800, 520), pivot=(1690, 520), feather=(6, 6, 6, 6),
         drop=[[(1626, 236), (1738, 236), (1738, 520), (1626, 520)], [(1586, 360), (1620, 360), (1620, 430), (1586, 430)],
               [(1586, 236), (1626, 236), (1626, 272), (1586, 272)], [(1738, 236), (1800, 236), (1800, 300), (1738, 300)]],
         drop_feather=6, glow=(0.25, 0.5), gate=0.05),
    dict(name="cage_base", sheet="ap_che", box=(1586, 470, 1800, 566), pivot=(1690, 543), feather=(6, 30, 6, 4),
         drop=[[(1770, 470), (1800, 470), (1800, 520), (1770, 520)],
               [(1646, 470), (1724, 470), (1716, 516), (1700, 528), (1670, 528), (1652, 516)]],
         drop_feather=8, gate=0.04, split=dict(y=541, feather=6)),
    # khung 4: chớp cú đấm (chỉ tia trắng rực quanh nắm tay)
    dict(name="punch_flash", sheet="ap_che", box=(1236, 300, 1346, 420), pivot=(1288, 360), feather=(14, 14, 14, 14),
         white_only=(0.55, 0.85), radial_fade=dict(center=(1288, 360), axes=(1.0, 1.0), r=(22, 54)), gate=0.04),
]

# Biểu tượng 3 nút: tấm, vùng vuông (trái, trên, cạnh) quanh khung đinh, nền (tâm, mép), viền, có tia sáng toả không
ICON_FRAMES = [
    dict(name="Skill_Red_1", sheet="danh_thuong", box=(672, 160, 440), inner=(0.42, 0.06, 0.08), outer=(0.08, 0.02, 0.05),
         rim=(0.75, 0.16, 0.15), rays=False),
    dict(name="Skill_Red_2", sheet="ki_nang", box=(1380, 115, 480), inner=(0.45, 0.07, 0.09), outer=(0.1, 0.02, 0.05),
         rim=(0.85, 0.85, 0.9), rays=False),
    dict(name="Skill_Red_3", sheet="dac_biet", box=(1080, 140, 420), inner=(0.55, 0.12, 0.2), outer=(0.15, 0.03, 0.08),
         rim=(0.95, 0.4, 0.35), rays=True),
]

# Xích (Kĩ năng đặc biệt áp chế khung 2): đoạn xích gần nằm ngang giữa tay RED và hình nộm → xoay nằm ngang, cắt đúng
# số mắt xích (chu kỳ tìm bằng tự tương quan) để lát nối tiếp (SpriteRenderer kiểu Tiled)
CHAIN = dict(sheet="ap_che", a=(634, 389.5), b=(740, 372.5), half=11)


# ---------------------------------------------------------------- tiện ích

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def load(name):
    img = Image.open(DOCS / "nhan_vat_moi" / "red" / SHEETS[name]).convert("RGB")
    return np.asarray(img).astype(np.float32) / 255.0


def background(rgb):
    """Màu nền từng chỗ: điểm tối ngả xanh (giống nền) làm mờ có trọng số; trong vùng hiệu ứng lớn lấy từ xung quanh."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    like = (rgb.max(2) < 0.26) & (b > r + 0.03) & (b > g + 0.02)
    w = like.astype(np.float32)
    sig = 22
    wb = ndi.gaussian_filter(w, sig)
    out = np.empty_like(rgb)
    fallback = np.median(rgb[like], axis=0)
    for c in range(3):
        cb = ndi.gaussian_filter(rgb[..., c] * w, sig)
        out[..., c] = np.where(wb > 1e-3, cb / np.maximum(wb, 1e-3), fallback[c])
    # vùng hiệu ứng rất lớn (trọng số gần 0): pha dần về màu nền chung
    k = smoothstep(0.0, 0.05, wb)[..., None]
    return out * k + fallback * (1 - k)


def color_to_alpha(rgb, bg):
    """Alpha nhỏ nhất để rgb = a * màu + (1 - a) * nền với màu trong [0, 1]; trả về (màu, alpha)."""
    up = np.where(rgb > bg, (rgb - bg) / np.maximum(1 - bg, 1e-4), 0)
    down = np.where(rgb < bg, (bg - rgb) / np.maximum(bg, 1e-4), 0)
    a = np.clip(np.maximum(up, down).max(2), 0, 1)
    col = np.where(a[..., None] > 1e-4, (rgb - bg) / np.maximum(a[..., None], 1e-4) + bg, rgb)
    return np.clip(col, 0, 1), a


def solid_dark(rgb, bg, col, a):
    """Đá / mảnh vỡ tối (không ngả xanh như nền): đặc lại để không bị trong suốt."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    dist = np.linalg.norm(rgb - bg, axis=2)
    rocky = (rgb.max(2) < 0.42) & (b < r + 0.04) & (b < g + 0.12)
    s = np.where(rocky, smoothstep(0.07, 0.16, dist), 0)
    a2 = np.maximum(a, s)
    col2 = np.where(a2[..., None] > 1e-4, (rgb - (1 - a2[..., None]) * bg) / np.maximum(a2[..., None], 1e-4), col)
    return np.clip(col2, 0, 1), a2


def poly_mask(shape, polys, feather, offset=(0, 0)):
    """1 trong đa giác / elip (toạ độ trên tấm, trừ offset = góc vùng cắt), mép mờ feather px."""
    img = Image.new("L", (shape[1], shape[0]), 0)
    d = ImageDraw.Draw(img)
    ox, oy = offset
    for p in polys:
        if len(p) == 4 and not isinstance(p[0], (tuple, list)):
            cx, cy, rx, ry = p
            d.ellipse((cx - ox - rx, cy - oy - ry, cx - ox + rx, cy - oy + ry), fill=255)
        else:
            d.polygon([(x - ox, y - oy) for x, y in p], fill=255)
    m = np.asarray(img).astype(np.float32) / 255
    return ndi.gaussian_filter(m, feather / 2.5) if feather > 0 else m


def edge_fade(h, w, f):
    left, top, right, bottom = f
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    k = np.ones((h, w), np.float32)
    for dist, n in ((xx, left), (w - 1 - xx, right), (yy, top), (h - 1 - yy, bottom)):
        if n > 0:
            k *= smoothstep(0, n, dist)
    return k


def unpremultiply(pre):
    al = np.clip(pre[..., 3], 0, 1)
    co = np.where(al[..., None] > 1e-4, pre[..., :3] / np.maximum(al[..., None], 1e-4), 0)
    return np.clip(co, 0, 1), al


def polar_fill(col, a, hole, center, axes, extend=40.0, max_gap=200.0, span=None, rband=None, core=0.0, remap=0.0,
               nth=1440):
    """Dựng lại chỗ trống (hole 0..1) bằng chính nét của hiệu ứng, chạy dọc cung elip (tâm center, bán trục axes): trong toạ
    độ cực của elip, mỗi vòng bán kính nội suy theo góc giữa hai điểm còn nguyên gần nhất (cách nhau ≤ max_gap độ); chỉ còn
    một phía thì kéo dài nét ra thêm tối đa `extend` độ, thon và mờ dần (dựng lại đuôi bị cắt). span = (từ, tới) độ: chỉ vá
    trong khoảng góc này; rband = (r0, r1): chỉ vá trong vành này. core > 0: tâm vụ nổ khuất sau người — lót quầng theo màu
    trung bình từng vòng; remap > 0: thêm vân tia của vành ngoài ép vào trong (tia hội tụ về tâm, không thành đĩa trơn)."""
    h, w = a.shape
    cx, cy = center
    ax, ay = axes
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = (xx - cx) / ax, (yy - cy) / ay
    r = np.hypot(u, v)
    th = np.degrees(np.arctan2(v, u)) % 360
    rmax = float(r[hole > 0.02].max()) + 0.05 if (hole > 0.02).any() else 1.0
    nr = int(rmax * max(ax, ay)) + 2
    rs = np.linspace(0, rmax, nr)
    ths = np.arange(nth) * 360.0 / nth
    R, T = np.meshgrid(rs, np.radians(ths), indexing="ij")
    px, py = cx + R * np.cos(T) * ax, cy + R * np.sin(T) * ay
    coords = np.array([py, px])
    pre = np.dstack([col * a[..., None], a])
    P = np.dstack([ndi.map_coordinates(pre[..., c], coords, order=1, mode="constant") for c in range(4)])
    H = ndi.map_coordinates(hole, coords, order=1, mode="constant", cval=1.0)
    inside = (px >= 0) & (px <= w - 1) & (py >= 0) & (py <= h - 1)
    valid = (H < 0.35) & inside
    step = 360.0 / nth
    out = P.copy()
    idx = np.arange(nth)
    i3 = np.arange(3 * nth)
    for i in range(nr):
        ok = valid[i]
        if ok.all():
            continue
        if not ok.any():
            out[i] = 0
            continue
        ok3 = np.concatenate([ok, ok, ok])
        left = np.maximum.accumulate(np.where(ok3, i3, -10 ** 9))[nth:2 * nth]
        right = np.minimum.accumulate(np.where(ok3, i3, 10 ** 9)[::-1])[::-1][nth:2 * nth]
        j = idx + nth
        dl, dr = (j - left) * step, (right - j) * step
        L, Rr = P[i, left % nth], P[i, right % nth]
        t = (dl / np.maximum(dl + dr, 1e-6))[:, None]
        interp = L * (1 - t) + Rr * t
        ext = np.where((dl <= dr)[:, None], L, Rr) * (1 - smoothstep(0, extend, np.minimum(dl, dr)))[:, None]
        fill = np.where((dl + dr <= max_gap)[:, None], interp, ext)
        out[i, ~ok] = fill[~ok]
    if core > 0:
        M = np.zeros((nr, 4), np.float32)
        have = np.zeros(nr, bool)
        for i in range(nr):
            if valid[i].mean() > 0.2:
                M[i] = P[i, valid[i]].mean(0)
                have[i] = True
        if have.any():
            first = int(np.argmax(have))
            for i in range(first):
                M[i] = M[first] * (0.6 + 0.4 * rs[i] / max(rs[first], 1e-6))
            for i in range(first, nr):
                if not have[i]:
                    M[i] = M[i - 1]
            M = ndi.gaussian_filter1d(M, 6, axis=0) * core
            under = np.repeat(M[:, None, :], nth, 1)
            if remap > 0:
                # vân tia hội tụ (kiểu nhoè toả tâm): mỗi góc lấy màu trung bình của dải vành ngoài còn nguyên ngay ngoài lỗ ở
                # góc đó, kéo vào tâm và nhạt dần — tâm có vân theo đúng các tia / gai của vụ nổ, không thành đĩa trơn
                firsts = np.argmax(valid, axis=0)
                band = max(4, nr // 6)
                A = np.zeros((nth, 4), np.float32)
                for q in range(nth):
                    f0 = firsts[q]
                    if valid[f0, q]:
                        seg = P[f0:f0 + band, q]
                        A[q] = seg.mean(0)
                A = ndi.gaussian_filter1d(A, 3, axis=0, mode="wrap")
                fr = np.clip(rs[:, None] / np.maximum(rs[firsts][None, :], 1e-6), 0, 1)
                k = (remap * (0.25 + 0.75 * fr ** 1.5))[..., None]
                rays = A[None, :, :] * k
                under = rays + under * (1 - rays[..., 3:4])
            lay = out + under * (1 - out[..., 3:4])
            out = np.where((~valid)[..., None], lay, out)
    if span is not None:
        lo, hi = span
        inspan = ((ths - lo) % 360) <= ((hi - lo) % 360)
        out[:, ~inspan] *= valid[:, ~inspan, None]
    cc = np.array([r / rmax * (nr - 1), th / step])
    F = np.dstack([ndi.map_coordinates(out[..., c], cc, order=1, mode="grid-wrap") for c in range(4)])
    F[r > rmax] = 0
    if rband is not None:
        lo, hi = rband
        F *= (smoothstep(lo - 0.03, lo + 0.03, r) * (1 - smoothstep(hi - 0.03, hi + 0.03, r)))[..., None]
    k = hole[..., None]
    return unpremultiply(pre * (1 - k) + F * k)


def mirror_fill(col, a, hole, center):
    """Dựng lại chỗ trống bằng điểm đối xứng qua tâm (chữ X gần đối xứng tâm): lấy nét của nửa bên kia."""
    h, w = a.shape
    cx, cy = center
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    coords = np.array([2 * cy - yy, 2 * cx - xx])
    pre = np.dstack([col * a[..., None], a])
    F = np.dstack([ndi.map_coordinates(pre[..., c], coords, order=1, mode="constant") for c in range(4)])
    k = hole[..., None]
    return unpremultiply(pre * (1 - k) + F * k)


def strip(shape, p0, p1, width, soft, offset):
    """Dải thẳng p0 → p1 rộng width (mờ soft), hai đầu thon."""
    h, w = shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    xx += offset[0]
    yy += offset[1]
    (x0, y0), (x1, y1) = p0, p1
    L = np.hypot(x1 - x0, y1 - y0)
    ux, uy = (x1 - x0) / L, (y1 - y0) / L
    t = (xx - x0) * ux + (yy - y0) * uy
    d = np.abs(-(xx - x0) * uy + (yy - y0) * ux)
    half = width / 2 * (0.35 + 0.65 * np.clip(np.minimum(t, L - t) / (0.25 * L), 0, 1))
    return (1 - smoothstep(half - soft, half + soft, d)) * (t > -soft) * (t < L + soft)


def ellipse_r(shape, center, axes, offset):
    h, w = shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    u = (xx + offset[0] - center[0]) / axes[0]
    v = (yy + offset[1] - center[1]) / axes[1]
    return np.hypot(u, v), np.degrees(np.arctan2(v, u)) % 360


def tight(col, a, pivot, margin=2):
    """Cắt sát phần có hình, nhưng luôn chứa pivot (pivot nằm ngoài hình thì nới khung trong suốt ra tới pivot)."""
    ys, xs = np.nonzero(a > 2 / 255)
    px, py = pivot
    y0, y1 = min(ys.min() - margin, int(np.floor(py)) - 1), max(ys.max() + margin + 1, int(np.ceil(py)) + 2)
    x0, x1 = min(xs.min() - margin, int(np.floor(px)) - 1), max(xs.max() + margin + 1, int(np.ceil(px)) + 2)
    h, w = a.shape
    pad = ((max(0, -y0), max(0, y1 - h)), (max(0, -x0), max(0, x1 - w)))
    if any(v for pr in pad for v in pr):
        a = np.pad(a, pad)
        col = np.pad(col, pad + ((0, 0),))
        y0, y1, x0, x1 = y0 + pad[0][0], y1 + pad[0][0], x0 + pad[1][0], x1 + pad[1][0]
        px, py = px + pad[1][0], py + pad[0][0]
    y0, x0 = max(0, y0), max(0, x0)
    return col[y0:y1, x0:x1], a[y0:y1, x0:x1], (px - x0, py - y0)


def save(name, col, a, pivot, pivots, scale=1.0):
    """Ghi PNG cắt sát + pivot. scale < 1: lưu nhỏ hơn (lõi trắng mềm không cần đủ độ phân giải), PPU giảm theo nên cỡ trong
    game giữ nguyên."""
    col, a, pivot = tight(col, a, pivot)
    rgba = np.dstack([col, a])
    img = Image.fromarray((np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")
    h, w = a.shape
    if scale != 1.0:
        pre = np.dstack([col * a[..., None], a])  # thu nhỏ ở dạng nhân trước: viền không lem màu
        small = Image.fromarray((np.clip(pre, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").resize(
            (max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
        arr = np.asarray(small).astype(np.float32) / 255
        c2, a2 = unpremultiply(arr)
        img = Image.fromarray((np.dstack([c2, a2]) * 255 + 0.5).astype(np.uint8), "RGBA")
    img.save(OUT / f"{name}.png")
    # pivot Unity: 0..1 từ góc DƯỚI trái (tỉ lệ không đổi khi thu nhỏ)
    pivots[name] = dict(x=round(float(pivot[0]) / w, 4), y=round(1 - float(pivot[1]) / h, 4), ppu=round(PPU * scale, 3))
    print(f"-> {name}.png {img.width}x{img.height} pivot ({pivots[name]['x']}, {pivots[name]['y']}) ppu {pivots[name]['ppu']}")
    return img, (float(pivot[0]), float(pivot[1]))


# ---------------------------------------------------------------- cắt

def cut(e, sheets, bgs):
    """Một mảnh: trả (màu, alpha, pivot trong vùng cắt) — chưa tách lớp."""
    rgb, bg = sheets[e["sheet"]], bgs[e["sheet"]]
    x0, y0, x1, y1 = e["box"]
    off = (x0, y0)
    c, b = rgb[y0:y1, x0:x1], bg[y0:y1, x0:x1]
    col, a = color_to_alpha(c, b)
    col, a = solid_dark(c, b, col, a)
    a = np.clip((a - 0.035) / 0.965, 0, 1)  # bỏ nhiễu nền
    sat = col.max(2) - col.min(2)
    if e.get("gray_only"):
        a = a * (1 - smoothstep(0.25, 0.45, sat))  # bụi xám: bỏ vệt đỏ của áo choàng / kiếm lọt vào
    else:
        a = a * (1 - smoothstep(0.02, 0.1, col[..., 2] - col[..., 0]))  # hiệu ứng đỏ / trắng / đá: phần ngả xanh là nền loang
    h, w = a.shape

    if "glow" in e:
        g0, g1 = e["glow"]
        a = a * smoothstep(g0, g1, c.max(2))
    if "white_only" in e:
        w0, w1 = e["white_only"]
        a = a * smoothstep(w0, w1, c.min(2) + 0.35 * (c.max(2) - c.min(2)))
    if "bright_only" in e:
        # vùng có người / hình nộm: chỉ giữ phần sáng rực (mắt xích, tia trắng), bỏ tóc, áo, viền người
        m = poly_mask((h, w), e["bright_only"], 6, off)
        lum, s2 = c.max(2), c.max(2) - c.min(2)
        keep = smoothstep(e.get("bright_lo", 0.72), e.get("bright_hi", 0.95), lum) * smoothstep(0.0, 0.05, c.min(2) + s2)
        a = a * (1 - m) + a * keep * m

    if "circle" in e:
        cx, cy, rx, ry = e["circle"]
        cx, cy = cx - x0, cy - y0
        if "patch" in e:
            # chỗ bị chân / kiếm che: lấy điểm cùng bán kính trên vòng phép ở góc khác (quay quanh tâm trong không gian vòng
            # tròn: 180° trước, nếu chỗ đó cũng bị che thì 120°, 240°, 90°, 270°)
            m = poly_mask((h, w), e["patch"], 5, off)
            yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
            u, v = (xx - cx) / rx, (yy - cy) / ry
            fill_c, fill_a = col.copy(), a.copy()
            todo = m > 0.02
            for deg in (180, 120, 240, 90, 270, 150, 210):
                t = np.radians(deg)
                su, sv = u * np.cos(t) - v * np.sin(t), u * np.sin(t) + v * np.cos(t)
                sx = np.clip(np.round(cx + su * rx).astype(int), 0, w - 1)
                sy = np.clip(np.round(cy + sv * ry).astype(int), 0, h - 1)
                ok = todo & (m[sy, sx] < 0.05)
                fill_c[ok] = col[sy[ok], sx[ok]]
                fill_a[ok] = a[sy[ok], sx[ok]]
                todo &= ~ok
            col = col * (1 - m[..., None]) + fill_c * m[..., None]
            a = a * (1 - m) + fill_a * m
        # chỉ giữ vòng phép (elip nới rộng + mũi gai trước / sau)
        keep = poly_mask((h, w), [(cx, cy, rx + 14, ry + 13), [(cx - 18, cy), (cx + 18, cy), (cx + 6, cy + 70), (cx - 6, cy + 70)],
                                  [(cx - 18, cy), (cx + 18, cy), (cx + 6, cy - 70), (cx - 6, cy - 70)]], 8)
        a = a * keep

    if "hole" in e:
        hole = poly_mask((h, w), e["hole"], e.get("hole_feather", 6), off)
        f = e["fill"]
        col, a = polar_fill(col, a, hole, (f["center"][0] - x0, f["center"][1] - y0), f["axes"], f.get("extend", 40.0),
                            f.get("max_gap", 200.0), f.get("span"), f.get("rband"), f.get("core", 0.0), f.get("remap", 0.0))
    if "mirror" in e:
        mi = e["mirror"]
        col, a = mirror_fill(col, a, poly_mask((h, w), mi["hole"], mi.get("feather", 8), off),
                             (mi["center"][0] - x0, mi["center"][1] - y0))
    if "arm" in e:
        am = e["arm"]
        a = a * strip((h, w), am["p0"], am["p1"], am["width"], am.get("soft", 14), off)
    if "not_arms" in e:
        m = np.zeros((h, w), np.float32)
        for am in e["not_arms"]:
            m = np.maximum(m, strip((h, w), am["p0"], am["p1"], am["width"], am.get("soft", 14), off))
        a = a * (1 - m)
    if "ring" in e:
        rg = e["ring"]
        rr, th = ellipse_r((h, w), rg["center"], rg["axes"], off)
        lo, hi = rg["r"]
        m = smoothstep(lo - 0.08, lo + 0.04, rr) * (1 - smoothstep(hi - 0.04, hi + 0.08, rr))
        if "span" in rg:
            s0, s1 = rg["span"]
            d0, width = (th - s0) % 360, (s1 - s0) % 360
            m = m * smoothstep(0, 12, d0) * smoothstep(0, 12, width - d0)
        a = a * m
    if "radial_fade" in e:
        rf = e["radial_fade"]
        rr, _ = ellipse_r((h, w), rf["center"], rf["axes"], off)
        a = a * (1 - smoothstep(rf["r"][0], rf["r"][1], rr))
    if "drop" in e:
        a = a * (1 - poly_mask((h, w), e["drop"], e.get("drop_feather", 8), off))
    if "keep" in e:
        a = a * poly_mask((h, w), e["keep"], e.get("keep_feather", 8), off)

    a = a * edge_fade(h, w, e.get("feather", (8, 8, 8, 8)))
    g = e.get("gate", 0.0)
    if g > 0:
        a = np.clip((a - g) / (1 - g), 0, 1)
    px, py = e["pivot"]
    return col, a, (px - x0, py - y0)


def split_core(col, a):
    """Lõi trắng rực (kênh nhỏ nhất cao) tách riêng; thân = ảnh gốc, chỗ lõi ngả về đỏ (lõi tắt trước thì thân còn đỏ)."""
    white = col.min(2)
    k = smoothstep(0.55, 0.92, white) * a
    core_col = np.ones_like(col) * np.array([1.0, 0.95, 0.93])
    body = col * (1 - 0.55 * k[..., None]) + np.array([1.0, 0.18, 0.16]) * (0.55 * k[..., None])
    return (core_col, k), (np.clip(body, 0, 1), a)


def outputs(e, col, a, pivot, pivots, images):
    name = e["name"]
    if "split" in e:
        sp = e["split"]
        y = sp["y"] - e["box"][1]
        yy = np.arange(a.shape[0], dtype=np.float32)[:, None]
        front = np.repeat(smoothstep(y - sp["feather"], y + sp["feather"], yy), a.shape[1], 1)
        for suffix, m in (("_back", 1 - front), ("_front", front)):
            img, _ = save(name + suffix, col, a * m, pivot, pivots)
            images.append((name + suffix, img))
        return
    if "tail" in e:
        tl = e["tail"]
        rr, _ = ellipse_r(a.shape, (pivot[0], pivot[1]), tl["axes"], (0, 0))
        head = 1 - smoothstep(tl["r"][0], tl["r"][1], rr)
        img, _ = save(name, col, a * head, pivot, pivots)
        images.append((name, img))
        # đuôi: phần còn lại, pivot vẫn ở đầu sóng (kéo dài ngang quanh đầu sóng = đuôi dài ra phía sau)
        img, _ = save(name + "_tail", col, a * (1 - head), pivot, pivots)
        images.append((name + "_tail", img))
        return
    if e.get("core"):
        (cc, ca), (bc, ba) = split_core(col, a)
        img, _ = save(name, bc, ba, pivot, pivots)
        images.append((name, img))
        # lõi giữ cùng khung với thân (pivot trùng) — cắt sát riêng vẫn đúng chỗ vì pivot tính lại theo vùng cắt; lõi trắng
        # mềm lưu nửa độ phân giải (PPU 75) cho nhẹ
        img, _ = save(name + "_core", cc, ca, pivot, pivots, scale=0.5)
        images.append((name + "_core", img))
        return
    img, _ = save(name, col, a, pivot, pivots)
    images.append((name, img))


def cut_chain(sheets, bgs):
    """Đoạn xích nằm ngang, dài đúng số nguyên chu kỳ mắt xích, hai đầu nối liền nhau khi lát."""
    e = CHAIN
    rgb, bg = sheets[e["sheet"]], bgs[e["sheet"]]
    (ax, ay), (bx, by) = e["a"], e["b"]
    ang = np.degrees(np.arctan2(by - ay, bx - ax))
    length = int(np.hypot(bx - ax, by - ay))
    pad = 40
    x0, y0 = int(min(ax, bx)) - pad, int(min(ay, by)) - pad
    x1, y1 = int(max(ax, bx)) + pad, int(max(ay, by)) + pad
    col, a = color_to_alpha(rgb[y0:y1, x0:x1], bg[y0:y1, x0:x1])
    a = np.clip((a - 0.05) / 0.95, 0, 1)
    rgba = np.dstack([col * a[..., None], a])  # nhân trước để xoay không lem viền
    img = Image.fromarray((rgba * 255).astype(np.uint8), "RGBA")
    cxp, cyp = (ax + bx) / 2 - x0, (ay + by) / 2 - y0
    rot = img.rotate(ang, Image.BICUBIC, center=(cxp, cyp))  # PIL: góc dương = ngược chiều kim → kéo đoạn xích về nằm ngang
    arr = np.asarray(rot).astype(np.float32) / 255
    hh = e["half"]
    xa, xb = int(cxp - length / 2), int(cxp + length / 2)
    wide = arr[int(cyp) - 2 * hh:int(cyp) + 2 * hh, xa:xb]
    rows = ndi.gaussian_filter1d(wide[..., 3].mean(1), 2)
    mid = int(cyp) - 2 * hh + int(np.argmax(rows))  # tâm sợi xích (hàng sáng nhất)
    band = arr[mid - hh:mid + hh, xa:xb]
    # chu kỳ mắt xích: tự tương quan của độ sáng theo chiều dọc xích
    prof = band[..., 3].mean(0)
    prof = prof - prof.mean()
    ac = np.correlate(prof, prof, "full")[len(prof) - 1:]
    lo = 18
    period = lo + int(np.argmax(ac[lo:40]))
    reps = max(1, int(band.shape[1] * 0.75) // period)
    start = (band.shape[1] - reps * period) // 2
    tile = band[:, start:start + reps * period].copy()
    # hai đầu khớp nhau: vài px đầu pha dần với vài px ngay sau chu kỳ cuối (nối liền khi lát)
    k = 4
    after = band[:, start + reps * period:start + reps * period + k]
    if after.shape[1] == k:
        t = ((np.arange(k) + 0.5) / k)[None, :, None]
        tile[:, :k] = after * (1 - t) + tile[:, :k] * t
    al = tile[..., 3]
    col = np.where(al[..., None] > 1e-3, tile[..., :3] / np.maximum(al[..., None], 1e-3), 0)
    print(f"   chain period {period}px x{reps}, angle {ang:.1f}")
    return np.clip(col, 0, 1), al, (0, tile.shape[0] / 2)


def save_chain(col, a, pivot, pivots):
    """Xích lát: giữ nguyên khung (không cắt sát — lát nối tiếp cần đúng chu kỳ)."""
    img = Image.fromarray((np.clip(np.dstack([col, a]), 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")
    img.save(OUT / "chain.png")
    h, w = a.shape
    pivots["chain"] = dict(x=round(float(pivot[0]) / w, 4), y=round(1 - float(pivot[1]) / h, 4), ppu=PPU)
    print(f"-> chain.png {w}x{h}")
    return img


def icon(e, sheets, bgs):
    """Biểu tượng tròn 256 px: khung đinh của tấm (bỏ nền xanh đen) trên nền đỏ toả tròn, quầng sáng, viền sáng."""
    rgb, bg = sheets[e["sheet"]], bgs[e["sheet"]]
    x0, y0, n = e["box"]
    c, b = rgb[y0:y0 + n, x0:x0 + n], bg[y0:y0 + n, x0:x0 + n]
    col, a = color_to_alpha(c, b)
    col, a = solid_dark(c, b, col, a)
    a = np.clip((a - 0.035) / 0.965, 0, 1)
    art = Image.fromarray((np.dstack([col * a[..., None], a]) * 255 + 0.5).astype(np.uint8), "RGBA")  # nhân trước để thu nhỏ
    art = art.resize((ICON, ICON), Image.LANCZOS)
    pre = np.asarray(art).astype(np.float32) / 255

    yy, xx = np.mgrid[0:ICON, 0:ICON].astype(np.float32) + 0.5
    r = np.hypot(xx - ICON / 2, yy - ICON / 2) / (ICON / 2)
    base = np.array(e["inner"]) * (1 - np.clip(r, 0, 1)[..., None]) + np.array(e["outer"]) * np.clip(r, 0, 1)[..., None]
    if e["rays"]:
        ang = np.arctan2(yy - ICON / 2, xx - ICON / 2)
        ray = np.clip(np.cos(ang * 12) * 2 - 1.2, 0, 1) * np.clip(r, 0, 1) * 0.25
        base = base + ray[..., None] * np.array([1.0, 0.55, 0.4])
    # quầng đỏ theo hình (sau hình)
    halo = ndi.gaussian_filter(pre[..., 3], 8) * 0.8
    base = base * (1 - halo[..., None]) + np.array([1.0, 0.18, 0.15]) * halo[..., None]
    out = base * (1 - pre[..., 3:]) + pre[..., :3]
    # viền sáng mép trong + cắt tròn
    rim = np.exp(-((r * ICON / 2 - (ICON / 2 - 10)) / 6) ** 2)[..., None] * np.array(e["rim"])
    out = 1 - (1 - np.clip(out, 0, 1)) * np.exp(-rim * 1.6)
    alpha = np.clip(ICON / 2 - 1 - r * ICON / 2 + 0.5, 0, 1)
    img = Image.fromarray((np.dstack([np.clip(out, 0, 1), alpha]) * 255 + 0.5).astype(np.uint8), "RGBA")
    path = ICONS / f"{e['name']}.png"
    img.save(path)
    print("->", path.relative_to(ROOT))
    return img


def preview(images, path):
    """Xem trước: mỗi mảnh trên nền hang tối VÀ nền sáng (lộ viền navy / mép cắt), xếp thành lưới."""
    pad, label = 8, 14
    tiles = []
    for name, img in images:
        w, h = img.size
        sc = min(1.0, 260 / max(w, h))
        im = img.resize((max(1, int(w * sc)), max(1, int(h * sc))), Image.LANCZOS)
        w, h = im.size
        cell = Image.new("RGB", (w * 2 + pad * 3, h + pad * 2 + label), (18, 18, 22))
        for i, bgc in enumerate(((36, 22, 52), (232, 232, 236))):
            t = Image.new("RGB", (w, h), bgc)
            t.paste(im, (0, 0), im)
            cell.paste(t, (pad + i * (w + pad), pad + label))
        ImageDraw.Draw(cell).text((pad, 2), name, fill=(255, 255, 0))
        tiles.append(cell)
    cols = 4
    colw = max(t.width for t in tiles)
    rows = [tiles[i:i + cols] for i in range(0, len(tiles), cols)]
    H = sum(max(t.height for t in row) for row in rows)
    sheet = Image.new("RGB", (colw * cols, H), (10, 10, 12))
    y = 0
    for row in rows:
        for i, t in enumerate(row):
            sheet.paste(t, (i * colw, y))
        y += max(t.height for t in row)
    sheet.save(path, quality=90)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    sheets = {k: load(k) for k in SHEETS}
    bgs = {k: background(v) for k, v in sheets.items()}
    pivots, images = {}, []
    for e in ELEMENTS:
        col, a, pivot = cut(e, sheets, bgs)
        outputs(e, col, a, pivot, pivots, images)
    col, a, pivot = cut_chain(sheets, bgs)
    images.append(("chain", save_chain(col, a, pivot, pivots)))
    for name in OBSOLETE:
        for p in (OUT / f"{name}.png", OUT / f"{name}.png.meta"):
            if p.exists():
                p.unlink()
                print("x ", p.relative_to(ROOT))
    (OUT / "pivots.json").write_text(json.dumps({"items": [dict(name=k, **v) for k, v in pivots.items()]}, indent=1))
    print("->", (OUT / "pivots.json").relative_to(ROOT))
    icons = [(e["name"], icon(e, sheets, bgs)) for e in ICON_FRAMES]
    if PREVIEW:
        PREVIEW.mkdir(parents=True, exist_ok=True)
        preview(images, PREVIEW / "hero_fx_red_preview.jpg")
        strip_ = Image.new("RGBA", (ICON * 3 + 40, ICON + 20), (30, 30, 40, 255))
        for i, (_, im) in enumerate(icons):
            strip_.alpha_composite(im, (10 + i * (ICON + 10), 10))
        strip_.save(PREVIEW / "hero_fx_red_icons.png")


if __name__ == "__main__":
    main()
