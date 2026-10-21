# 3 nhân vật mới: Red, Pink, Blue

Mỗi nhân vật thả ảnh vào đúng thư mục của nó: `red/`, `pink/`, `blue/`. Ảnh PNG nền **trong suốt**,
càng to càng tốt (cạnh dài ≥ 1500 px). Tên file không bắt buộc.

## Mỗi thư mục nên có

1. **Tấm bộ phận rời (quan trọng nhất)**, giống tấm `Docs/spider/Spider_pink.png` hoặc `Docs/boss_su_tu/su_tu/lion_component.png`:
   - Trên cùng là cả con ráp sẵn, nhìn **ngang, quay mặt sang phải**, để làm mẫu.
   - Bên dưới là từng bộ phận tách rời, không chồng lên nhau: thân, đầu, mắt / miệng (nếu muốn biểu cảm),
     tay / chân / cánh / đuôi…, mỗi khớp tách một đoạn. Chừa khoảng trống giữa các bộ phận.
   - Có tấm này thì hoạt ảnh gắn xương được, mượt như bạch tuộc và nhện.
2. **Tấm dáng** (tuỳ chọn): vài dáng đứng / di chuyển / đánh của cùng con đó, để làm mẫu hoạt ảnh.
3. **Ghi chú** (tuỳ chọn): file `ghi_chu.txt` viết con này là con gì, tính cách, muốn nó di chuyển và đánh kiểu gì.
   Không viết thì mình tự phân tích từ hình.

## Sau khi có ảnh, mình sẽ làm

- **Cách điều khiển mới** cho 3 con: cần điều hướng bên trái (trái/phải chạy, lên/xuống đổi kiểu chiêu), 3 nút chiêu + nút
  nhảy (nhảy đúp) bên phải. Mỗi con có nét di chuyển riêng (xem `PHAN_TICH_NHAN_VAT.md`).
- **Bộ 9 chiêu riêng** (3 nút × thường / ↑ / ↓), không con nào trùng con nào, và cũng không trùng bạch tuộc, nhện.
- Thêm vào màn chọn nhân vật, chơi được ở cả màn hang động và màn đánh boss.

## Ảnh hoạt ảnh theo chiêu

Mỗi nhân vật có 3 thư mục con, ứng với 3 nút chiêu:

| Thư mục | Nút | Chứa |
|---|---|---|
| `basic_attack/` | 1: đánh thường | ảnh / tấm dáng cho đánh thường |
| `skill/` | 2: dùng chiêu | ảnh / tấm dáng cho chiêu |
| `special_skill/` | 3: chiêu đặc biệt (cần nộ) | ảnh / tấm dáng cho chiêu cuối |

Mỗi nút có 3 kiểu (thường / ↑ / ↓, xem `PHAN_TICH_NHAN_VAT.md`). Nên đặt tên file theo kiểu để mình biết ảnh nào cho chiêu nào:
`neutral_*.png`, `up_*.png`, `down_*.png` (ví dụ `red/skill/up_shadow_dash.png`). Tấm nhiều khung thì xếp các khung thành một hàng
ngang theo thứ tự thời gian, nhân vật quay mặt sang phải.
