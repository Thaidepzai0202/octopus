# Con nhện

## Ảnh gốc
`Docs/spider/Spider_pink.png`: tấm bộ phận rời (bụng, đầu ngực, 8 mắt, 4 răng nanh, 3 kiểu chân trái / phải) kèm con nhện
ráp sẵn ở trên để làm mẫu. Muốn đổi ảnh: thay file này (giữ cách xếp bộ phận như cũ), rồi:
1. `python3 Tools/art/spider_rig.py` → cắt bộ phận, gắn xương, xuất vào `Assets/_Project/Art/Spider/Rig/`
   và ảnh ráp thử `Docs/spider/xem_rig/lap_rap.png`.
2. Unity: menu **Octopus → Dựng lại nhện (giữ map)** → Cmd + S.

Cách ráp (vị trí, tỉ lệ từng bộ phận; chỗ gắn và chỗ đặt 8 chân) nằm ở đầu `Tools/art/spider_rig.py`
(các bảng `PARTS`, `LEGS`, `LEG_SCALE`). Sửa số ở đó rồi chạy lại 2 bước trên.

## Trong Unity
- Nhện là **nhân vật chơi thứ hai**: chọn ở màn **chọn nhân vật** (scene `CharacterSelect`, mở đầu tiên khi vào game).
  Điều khiển giống bạch tuộc: vuốt ngang để bò, chạm nhanh để nhảy, chạm vào đá để **nhả tơ** đu, giữ tay để rút tơ
  lên tới đá rồi **bám ngược** dưới đá (vuốt để bò ngược), chạm nhanh khi đang bám để thả xuống. Đánh nhau bằng cụm
  3 nút góc dưới phải (cào độc, bắn tơ, kén tơ…), xem `Docs/ky_nang/PHAN_TICH_KY_NANG.md`.
- Bấm Play trong Unity (ở scene nào cũng vậy) là hiện màn chọn nhân vật trước. Muốn vào thẳng màn chơi bằng nhện để thử
  nhanh: chọn `Octopus` → component **Player Character** → bỏ tick **Choose On Play**, **Start With** = Spider.
- Hình nhện là object **Octopus › Spider Visual** (bình thường đang tắt). **Start With** = Spider thì thấy nhện ngay trong
  Scene để chỉnh.
- **Spider Visual** (component **Spider Rig Animator**): chọn **Preview** (Idle, Walk, Jump, Fall, Swing, Cling, Cling Walk)
  rồi bấm **▶ Chạy thử** để xem không cần Play. Các ô chỉnh: mục **Bò** (nhịp bước, độ nhấc chân, nhún, nghiêng),
  **Đứng yên** (thở, dồn trọng tâm, gõ chân), **Nhảy, rơi, đu** (co chân, sợi tơ), **Bám ngược dưới đá**,
  **Bụng, nanh, mắt**, **Size** (to / nhỏ).
- Mỗi chân có 2 xương (đùi, cẳng): chọn ảnh chân trong `Assets/_Project/Art/Spider/Rig/` → Sprite Editor → Skinning Editor
  để xem / sửa xương và trọng số.

Ảnh xem trước (`Docs/spider/xem_rig/`): `lap_rap.png` (ráp thử), `chon_nhan_vat.png` (màn chọn nhân vật),
`nguoi_choi.png` (hàng 1 đứng + bò, hàng 2 nhảy / rơi / đu bằng tơ, hàng 3 bám ngược + bò ngược),
`man_choi_nhen.png` (màn chơi với nhện, ảnh chụp trước khi có hệ thống chiêu).
