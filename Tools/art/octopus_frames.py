"""
Tách các tấm hoạt ảnh bạch tuộc (nền trắng) thành từng khung PNG nền trong suốt cho game.

    python octopus_frames.py <thư mục ảnh gốc> <thư mục ra>

Ảnh gốc (Docs/octopus): octopus.png (nhân vật, đã trong suốt), octopus_walk.png, octoppus_jump.png,
octopus_climb.png, octopus_free_fall.png (mỗi tấm 6 khung xếp ngang, nền trắng).

Ra: idle.png, walk_1..6.png, jump_1..6.png, fall_1..6.png, swing_1..2.png, release.png và frames.json
(danh sách khung: pivot, đặt theo chân hay theo tâm, điểm mọc xúc tu ở khung đu; số pixel mỗi unit).
"""
import json
import sys

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

SRC, OUT = sys.argv[1], sys.argv[2]

# Cả 4 tấm vẽ cùng cỡ nhân vật: khung đứng đầu tiên của tấm "climb" cao 290 px ↔ 1.55 unit trong game
SOURCE_PX_PER_UNIT = 290 / 1.55
TEXTURE_PX_PER_UNIT = 160      # độ nét lưu ra: đủ nét trên điện thoại mà nhẹ
IDLE_HEIGHT = 1.5              # octopus.png (ảnh lớn) thu về cao bấy nhiêu unit


def remove_white(rgb):
    """Nền trắng → trong suốt.
    - Vùng sáng nối ra mép ảnh, hoặc vùng sáng bị bao kín nhưng lớn và không màu (khoảng trống giữa các xúc tu) là nền.
    - Trong nền: phần có màu (quầng tím, vạch tốc độ) giữ bán trong suốt bằng color-to-alpha; phần xám / trắng bỏ hẳn.
    - Dải mép 2 px quanh nền: color-to-alpha để mép mềm, không viền trắng.
    - Vùng sáng nhỏ bị bao kín (đốm sáng trong mắt, tâm giác hút) giữ nguyên."""
    rgb = rgb.astype(np.float32)
    mn, mx = rgb.min(2), rgb.max(2)
    sat = mx - mn
    light = mn > 185
    labels, n = ndi.label(light)
    border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    area = ndi.sum(light, labels, range(1, n + 1))
    mean_sat = ndi.mean(sat, labels, range(1, n + 1))
    holes = set()
    for i, box in enumerate(ndi.find_objects(labels)):
        if i + 1 in border or box is None or mean_sat[i] > 40:
            continue
        # Xét viền 3 px quanh vùng sáng: quanh là xúc tu tối → khoảng trống nền; quanh là mắt / giác hút có màu → giữ
        y0, y1 = max(box[0].start - 4, 0), box[0].stop + 4
        x0, x1 = max(box[1].start - 4, 0), box[1].stop + 4
        blob = labels[y0:y1, x0:x1] == i + 1
        ring = ndi.binary_dilation(blob, iterations=3) & ~blob
        if area[i] > 150 or sat[y0:y1, x0:x1][ring].mean() < 60:
            holes.add(i + 1)
    background = np.isin(labels, list(border | holes))
    fringe = ndi.binary_dilation(background, iterations=2) & ~background & (mn > 120)

    alpha_c2a = (255.0 - mn) / 255.0
    keep_color = np.clip((sat - 18.0) / 40.0, 0, 1)
    alpha = np.ones_like(mn)
    alpha[background] = (alpha_c2a * keep_color)[background]
    alpha[fringe] = alpha_c2a[fringe]
    a = np.maximum(alpha, 1e-3)[..., None]
    soft = background | fringe
    color = np.where(soft[..., None], np.clip((rgb - (1 - a) * 255.0) / a, 0, 255), rgb)
    return np.dstack([color, alpha * 255]).astype(np.uint8)


def frames_of(rgba, count):
    """Tìm các khung: các khối đặc lớn nhất, xếp theo hoành độ; mỗi khung lấy dải cột giữa hai khung kề bên."""
    solid = rgba[..., 3] > 200
    labels, n = ndi.label(solid)
    sizes = ndi.sum(solid, labels, range(1, n + 1))
    all_boxes = ndi.find_objects(labels)
    big = sorted((int(i) + 1 for i in np.argsort(-sizes)[:count]), key=lambda i: all_boxes[i - 1][1].start)
    boxes = [all_boxes[i - 1] for i in big]
    centers = [(b[1].start + b[1].stop) / 2 for b in boxes]
    edges = [0] + [int((centers[i] + centers[i + 1]) / 2) for i in range(len(centers) - 1)] + [rgba.shape[1]]
    return [(labels == big[i], edges[i], edges[i + 1], boxes[i]) for i in range(len(big))]


def cut(rgba, body, x0, x1, box, drop_below=None, top=None):
    """Cắt một khung: chỉ lấy dải cột của khung; bỏ phần thấp hơn đáy thân + drop_below (bóng đổ) và cao hơn top."""
    y0 = top if top is not None else 0
    y1 = box[0].stop + drop_below if drop_below is not None else rgba.shape[0]
    region = rgba[y0:y1, x0:x1].copy()
    ys, xs = np.nonzero(region[..., 3] > 8)
    l, t, r, b = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    region = region[t:b, l:r]
    body = body[y0:y1, x0:x1][t:b, l:r]
    return region, body, (x0 + l, y0 + t)


def neck_row(body):
    """Khung đu: đi từ dưới lên, qua hết phần thân dày (ít nhất 40 hàng rộng) rồi gặp đoạn hẹp (dưới 35% bề rộng
    lớn nhất) liên tục 8 hàng: đó là cổ xúc tu mọc lên tảng đá. Trả về hàng thấp nhất của đoạn hẹp đó."""
    widths = body.sum(1)
    limit = widths.max() * 0.35
    wide = narrow = 0
    for y in range(len(widths) - 1, -1, -1):
        if widths[y] >= limit:
            wide, narrow = wide + 1, 0
        elif wide >= 40 and widths[y] > 0:
            narrow += 1
            if narrow >= 8:
                return y + narrow - 1
    return 0


manifest = {"pixelsPerUnit": TEXTURE_PX_PER_UNIT, "frames": []}


def save(name, region, body, pivot_mode, attach=None, scale=TEXTURE_PX_PER_UNIT / SOURCE_PX_PER_UNIT):
    img = Image.fromarray(region, "RGBA").convert("RGBa")
    w, h = max(1, round(img.width * scale)), max(1, round(img.height * scale))
    img = img.resize((w, h), Image.LANCZOS).convert("RGBA")
    img.save(f"{OUT}/{name}.png")
    ys, xs = np.nonzero(body)
    cx = xs.mean() / body.shape[1]
    if pivot_mode == "ground":       # chân chạm đất: đáy phần thân đặc, giữa theo khối thân
        pivot = [cx, 1 - (ys.max() + 1) / body.shape[0]]
    else:                            # trên không: trọng tâm khối thân
        pivot = [cx, 1 - ys.mean() / body.shape[0]]
    entry = {"name": name, "pivot": [round(float(pivot[0]), 4), round(float(pivot[1]), 4)], "ground": pivot_mode == "ground", "attach": []}
    if attach is not None:           # điểm mọc xúc tu, toạ độ chuẩn hoá trong ảnh (gốc dưới-trái)
        entry["attach"] = [round(float(attach[0]) / body.shape[1], 4), round(1 - float(attach[1]) / body.shape[0], 4)]
    manifest["frames"].append(entry)
    print(f"  {name}.png {w}x{h} pivot {entry['pivot']}" + (f" attach {entry['attach']}" if attach is not None else ""))


# Nhân vật chính (đã trong suốt) làm tư thế đứng yên
idle = np.asarray(Image.open(f"{SRC}/octopus.png").convert("RGBA"))
ys, xs = np.nonzero(idle[..., 3] > 8)
idle = idle[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
save("idle", idle, idle[..., 3] > 200, "ground", scale=TEXTURE_PX_PER_UNIT * IDLE_HEIGHT / idle.shape[0])

sheets = {name: remove_white(np.asarray(Image.open(f"{SRC}/{name}.png").convert("RGB")))
          for name in ["octopus_walk", "octoppus_jump", "octopus_climb", "octopus_free_fall"]}

# Bò: 6 khung, chân chạm đất
rgba = sheets["octopus_walk"]
for k, (body, x0, x1, box) in enumerate(frames_of(rgba, 6)):
    save(f"walk_{k + 1}", *cut(rgba, body, x0, x1, box)[:2], "ground")

# Nhảy: 1 ngồi thụp, 2 bật (tia bụi), 3 lên, 4 đỉnh, 5 rơi, 6 tiếp đất (bụi). Khung 3–5 bỏ bóng đổ dưới đất
rgba = sheets["octoppus_jump"]
for k, (body, x0, x1, box) in enumerate(frames_of(rgba, 6)):
    airborne = k in (2, 3, 4)
    region, b, _ = cut(rgba, body, x0, x1, box, drop_below=30 if airborne else None)
    save(f"jump_{k + 1}", region, b, "center" if airborne else "ground")

# Rơi tự do: 6 khung, càng về sau càng chúc đầu
rgba = sheets["octopus_free_fall"]
for k, (body, x0, x1, box) in enumerate(frames_of(rgba, 6)):
    save(f"fall_{k + 1}", *cut(rgba, body, x0, x1, box, drop_below=30)[:2], "center")

# Bám / đu: khung 4–5 là đu (bỏ xúc tu vẽ sẵn và tảng đá trên đầu, giữ đoạn gốc), khung 6 là vừa buông
rgba = sheets["octopus_climb"]
frames = frames_of(rgba, 6)
for k, name in ((3, "swing_1"), (4, "swing_2")):
    body, x0, x1, box = frames[k]
    region, b, (ox, oy) = cut(rgba, body, x0, x1, box, drop_below=30)
    neck = neck_row(b)
    stub = max(neck - 12, 0)                      # giữ ~12 px gốc xúc tu để xúc tu vẽ bằng code nối vào
    region, b = region[stub:], b[stub:]
    cols = np.nonzero(b[12])[0]                   # hàng ngang chỗ cắt + 12 px: tâm gốc xúc tu
    attach = ((cols.min() + cols.max()) / 2, 0)
    save(name, region, b, "center", attach=attach)
body, x0, x1, box = frames[5]
save("release", *cut(rgba, body, x0, x1, box, drop_below=30, top=max(box[0].start - 10, 0))[:2], "center")  # bỏ tảng đá phía trên

with open(f"{OUT}/frames.json", "w") as f:
    json.dump(manifest, f, indent=2)
print("frames.json:", len(manifest["frames"]), "khung")
