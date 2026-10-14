"""
Từ các dải đã tách (split_layers.py) tạo đúng bộ ảnh game dùng trong Assets/_Project/Art/Drawn.

    python cut_pieces.py <thư mục split> <thư mục ra>

  sky.png      dải màu dọc lấy từ toàn cảnh thác (phông, cùng tông với tranh)
  deep.png     toàn cảnh thác + sương, mờ dần mép trên / dưới
  far.png      toàn cảnh hồ, mờ dần nửa trên để lộ lớp deep phía sau
  mid.png      dải lâu đài, bỏ đảo tinh thể bên trái (đã dùng làm đá bám)
  near.png     dải đảo + cầu dây, bỏ gờ đèn lồng (đã dùng làm sàn)
  ceiling.png  dải trần, bỏ hai lùm cây ở góc để lặp soi gương cho liền
  ledge.png    gờ đèn lồng ghép với bản lật gương: hai đầu đều là mép đá tự nhiên
  rock.png     đảo tinh thể, chỉ giữ khối liền lớn nhất (bỏ đảo mờ phía sau)
"""
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageOps

SPLIT, OUT = sys.argv[1], sys.argv[2]
band2 = Image.open(f"{SPLIT}/2_dao_cau_day.png")
band3 = Image.open(f"{SPLIT}/3_lau_dai.png")


def trim(img, pad=4):
    left, top, right, bottom = img.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    return img.crop((max(left - pad, 0), max(top - pad, 0), min(right + pad, img.width), min(bottom + pad, img.height)))


def fade(img, top, bottom):
    """Làm mờ dần mép trên / dưới (tỉ lệ chiều cao) để hoà vào lớp phía sau."""
    a = np.asarray(img).astype(np.float32).copy()
    h = a.shape[0]
    ramp = np.ones(h, np.float32)
    t, b = int(h * top), int(h * bottom)
    if t:
        ramp[:t] = np.linspace(0, 1, t) ** 1.6
    if b:
        ramp[h - b:] = np.linspace(1, 0, b) ** 1.6
    a[..., 3] *= ramp[:, None]
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")


def keep_largest(img, threshold=40, grow=3):
    """Chỉ giữ khối liền lớn nhất (cộng viền mờ sát nó), bỏ các mảnh vụn rời."""
    a = np.asarray(img).copy()
    mask = a[..., 3] > threshold
    h, w = mask.shape
    label = np.zeros((h, w), np.int32)
    sizes = {}
    for y in range(h):
        for x in range(w):
            if mask[y, x] and not label[y, x]:
                n = len(sizes) + 1
                label[y, x] = n
                queue, size = deque([(y, x)]), 0
                while queue:
                    cy, cx = queue.popleft()
                    size += 1
                    for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not label[ny, nx]:
                            label[ny, nx] = n
                            queue.append((ny, nx))
                sizes[n] = size
    keep = label == max(sizes, key=sizes.get)
    for _ in range(grow):
        keep = keep | np.roll(keep, 1, 0) | np.roll(keep, -1, 0) | np.roll(keep, 1, 1) | np.roll(keep, -1, 1)
    a[..., 3] = np.where(keep, a[..., 3], 0)
    result = Image.fromarray(a, "RGBA")
    return result.crop(result.getchannel("A").getbbox())


def save(img, name):
    img.save(f"{OUT}/{name}.png")
    print(f"  {name}.png  {img.width}x{img.height}")


# Sàn: gờ đèn lồng (x 0..905) ghép với bản lật gương → hai đầu đều là mép đá tự nhiên.
# Mặt đá để đứng nằm ở ~46% chiều cao tính từ trên → ô Surface của ledge trong Bảng chỉnh ảnh vẽ tay.
cliff = band2.crop((0, 0, 905, band2.height))
ledge = Image.new("RGBA", (cliff.width * 2, cliff.height))
ledge.paste(ImageOps.mirror(cliff), (0, 0))
ledge.paste(cliff, (cliff.width, 0))
save(trim(ledge), "ledge")

# Đá bám: đảo tinh thể bên trái dải 3; bỏ đảo xanh nhạt phía sau chồng ở mép phải rồi giữ khối lớn nhất
island = band3.crop((10, 40, 610, band3.height))
px = np.asarray(island).astype(np.float32).copy()
bluish = (px[..., 2] > px[..., 0] * 1.35) & (px[..., :3].mean(2) > 62)
px[..., 3] = np.where(bluish & (np.arange(island.width)[None, :] > 470), 0, px[..., 3])
save(keep_largest(trim(Image.fromarray(px.clip(0, 255).astype(np.uint8), "RGBA"))), "rock")

save(trim(band2.crop((930, 0, band2.width, band2.height))), "near")
save(trim(band3.crop((620, 0, band3.width, band3.height))), "mid")

ceiling = Image.open(f"{SPLIT}/1_tran_hang.png")
save(ceiling.crop((380, 0, 2700, ceiling.height)), "ceiling")

waterfall = Image.open(f"{SPLIT}/4_toan_canh_thac.png")
save(fade(waterfall, 0.06, 0.12), "deep")
save(fade(Image.open(f"{SPLIT}/5_toan_canh_ho.png"), 0.5, 0.12), "far")

# Phông: màu trung bình từng hàng của toàn cảnh thác → dải màu trên / giữa / dưới
rows = np.asarray(waterfall.convert("RGB")).astype(np.float32).mean(1)
top = rows[: len(rows) // 6].mean(0) * 0.8
middle = rows[len(rows) // 3: 2 * len(rows) // 3].mean(0)
bottom = rows[-len(rows) // 6:].mean(0) * 0.7
v = np.linspace(0, 1, 256)[:, None]
column = np.where(v < 0.5, top + (middle - top) * (v / 0.5), middle + (bottom - middle) * ((v - 0.5) / 0.5))
sky = Image.fromarray(np.repeat(column[:, None, :], 64, axis=1).clip(0, 255).astype(np.uint8), "RGB")
save(sky.resize((512, 256), Image.BICUBIC), "sky")
