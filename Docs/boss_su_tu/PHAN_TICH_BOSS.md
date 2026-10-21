# Boss Sư Tử Hắc Viêm: phân tích và thiết kế

Ảnh gốc: `su_tu/lion_component.png` (bộ phận rời để gắn xương) và `su_tu/lion_walk_and_hit.png` (6 khung đi, 6 khung cào,
dùng làm mẫu dáng). Sư tử đen, bờm lửa đỏ, gai và sừng xương, móng đỏ rực.

## 1. Ý tưởng trận đấu

Bạch tuộc và nhện mạnh nhất khi **đu trên cao** và nhỏ, nhanh. Boss thì to (cao gấp **5 lần** nhân vật), mạnh trên mặt đất
nhưng chậm. Trận đánh xoay quanh việc **đu lên các tảng đá lơ lửng để né, rồi lao xuống đánh khi boss sơ hở**.
Boss có đòn để với lên cao (vồ, cầu lửa, gầm), nên không có chỗ nào an toàn mãi.

Tham khảo:
- **Monster Hunter:** quái to, có báo trước rõ ràng, có lúc choáng để trừng phạt.
- **Hollow Knight, Cuphead:** đòn đánh có nhịp, đọc được; boss có 3 pha, càng về sau càng nhanh.
- **Sư tử Nemea** (thần thoại Hy Lạp): da cứng, nên không thể bị đẩy văng hay kéo đi.

## 2. Thông số

| | Giá trị | Ghi chú |
|---|---|---|
| Máu boss | 6000 | Combo đánh thường ~80, chiêu ~60–200, chiêu cuối 300–900 theo mốc nộ. Trận kéo dài khoảng 2–3 phút |
| Kích thước | Cao ≈ 5 × nhân vật | Camera trong màn boss lùi xa hơn để thấy cả boss lẫn đá |
| Máu người chơi | 300 | Boss đánh 15–45 mỗi đòn. Sau khi bị đánh trúng được bất tử 0,8 s, nhấp nháy, bị đẩy lùi |
| Bị khống chế | Không bị đẩy, không bị kéo | Choáng chỉ còn 30% thời gian, trói và kén còn tối đa 0,6 s, độc vẫn ăn đủ |
| Choáng gục | Nhận ≥ 600 sát thương trong 6 s | Boss gục 2 s (nằm nghiêng, nhận thêm 25% sát thương). Đây là cửa sổ trừng phạt |
| Nộ người chơi | Thêm nộ khi bị đánh (0,5 nộ / 1 máu mất) | Bị dồn ép thì nhanh có chiêu cuối để lật thế |

## 3. Ba pha

| Pha | Máu | Thay đổi |
|---|---|---|
| 1 | 100–60% | Vả vuốt, Vồ, Gầm. Nghỉ 1,2 s giữa hai đòn |
| 2 | 60–30% | Gầm chuyển pha, thêm **Cầu lửa từ bờm** và **Húc lao**. Nghỉ 0,9 s |
| 3 | < 30% | Nổi giận: hào quang đỏ, mọi thứ nhanh hơn 25%, nối 2 đòn liền, thêm **Mưa lửa**. Nghỉ 0,6 s |

## 4. Bộ đòn

Mọi đòn đều có **báo trước** (dáng lấy đà + hiệu ứng) để người chơi kịp né.

| Đòn | Khi nào | Báo trước | Diễn ra | Sát thương | Cách né |
|---|---|---|---|---|---|
| **Vả vuốt** | Người chơi ở gần phía trước, thấp | Chồm lên bằng 2 chân sau, mắt loé, 0,45 s | Vả 2 nhát, mỗi nhát một vệt lưỡi liềm đỏ trước mặt (theo mẫu `lion_walk_and_hit`) | 30 × 2 + đẩy văng | Nhảy, đu lên cao, hoặc lùi ra |
| **Vồ** | Người chơi ở xa hoặc trên đá thấp | Thu mình, đuôi quất, vòng đỏ dưới chỗ sắp đáp, 0,6 s | Nhảy vòng cung tới chỗ người chơi, đáp xuống tạo sóng chấn động lan trên sàn | 40 khi đè + 20 sóng sàn | Đu lên đá cao, hoặc nhảy qua sóng |
| **Gầm** | Người chơi ở trên cao lâu > 6 s, hoặc khi đổi pha | Ngẩng đầu, bờm bùng lửa, 0,5 s | Vòng sóng âm lan rộng (bán kính 14) | 15 + đẩy ra xa | Ra khỏi tầm, hoặc chịu đòn khi đang bất tử |
| **Cầu lửa** (pha 2+) | Người chơi ở trên đá | Lắc bờm, các đốm lửa tụ lại, 0,6 s | 5 quả cầu lửa bay vòng cung về phía người chơi. Rơi xuống sàn thì cháy 2 s | 25 mỗi quả, cháy 10/s | Đu sang đá khác |
| **Húc lao** (pha 2+) | Người chơi ở dưới sàn, xa | Cào chân, cúi đầu, bụi bay, 0,7 s | Lao thẳng hết đấu trường. Đâm tường thì choáng 1,2 s | 45 + hất tung | Nhảy hoặc đu lên. Lúc boss đâm tường là cửa sổ đánh |
| **Mưa lửa** (pha 3) | Mỗi 15 s | Gầm lên trời, vòng đỏ đánh dấu 8 chỗ (trên đá và sàn), 1 s | Thiên thạch lửa rơi xuống các chỗ đánh dấu | 30 | Đứng ngoài vòng đỏ |

Di chuyển: đi chậm về phía người chơi khi họ ở xa. Không đi khi đang tung đòn. Luôn quay mặt về phía người chơi trước mỗi đòn.

## 5. Hoạt ảnh boss (gắn xương từ `lion_component.png`)

- **Bộ phận:** thân trước (ngực), thân sau (hông), đầu (thường / há miệng gầm), bờm (mấy mảng xếp lớp, lay như lửa),
  4 chân (đùi + cẳng có bàn), đuôi (3 đoạn + chùm lửa ở ngọn).
- **Dáng:**
  - Đứng thở (bờm lay), đi, chạy (dáng phi), thu mình, bay vồ, đáp, chồm đứng.
  - Vả vuốt (2 nhát), gầm (há miệng, ngửa đầu), lắc bờm, húc (cúi đầu lao).
  - Trúng đòn (chớp sáng, giật nhẹ), choáng gục (nằm nghiêng), chết (gục xuống, lửa tắt dần).
- Quay đầu không lật vụt mà đổi hướng trong 0,25 s, không bị bóp dẹt (giống cách đã sửa cho nhện).

## 6. Bản đồ: Hang Hắc Viêm

- **Sàn đấu rộng** khoảng 56 ô, có vách ở hai đầu nên boss không chạy khỏi màn.
- **12–14 tảng đá lơ lửng** xếp 3 tầng:
  - Tầng thấp (cao ~5 ô trên sàn): boss với tới bằng vuốt và vồ.
  - Tầng giữa (~9 ô): chỉ bị vồ và cầu lửa với tới.
  - Tầng cao (~13 ô): chỉ bị gầm và mưa lửa với tới.
- Đá xếp so le để đu nối từ tảng này sang tảng kia, có lối đu vòng qua đầu boss.
- **Không khí:** dùng lại tranh hang động nhưng ánh đỏ cam. Sàn có khe dung nham phát sáng, tàn lửa bay, ánh sáng đỏ phía sau boss.
  Sau này muốn tranh riêng thì thả vào `dau_truong/` (xem `DAT_ANH_O_DAY.md`).
- **Camera** lùi xa hơn màn thường để luôn thấy boss.

## 7. Giao diện và luồng chơi

- **Màn chọn nhân vật:** thêm nút **Đánh boss** cạnh nút **Bắt đầu** (màn hang động cũ).
- **Thanh máu boss:** giữa phía trên màn hình, có tên "SƯ TỬ HẮC VIÊM", vạch mốc 60% và 30%, thanh "máu vừa mất" tụt chậm phía sau.
- **Thanh máu người chơi:** góc trên trái, ngay dưới thanh nộ.
- **Thua:** màn "THẤT BẠI", nút **Thử lại** và **Đổi nhân vật**.
- **Thắng:** boss gục, lửa tắt, màn "CHIẾN THẮNG" kèm thời gian đánh, nút **Đánh lại** và **Về màn chọn**.

---

## 8. Đã làm: cách chơi và cách chỉnh

**Vào trận:** Play → màn chọn nhân vật → chọn con → nút **ĐÁNH BOSS** (cạnh **BẮT ĐẦU**). Bấm Play ngay trong scene
`BossArena` cũng hiện màn chọn nhân vật trước. Ảnh xem trước: `Docs/boss_su_tu/xem/`.

**Những gì có trong màn:**
- Scene `Assets/_Project/Scenes/BossArena.unity`: sàn dài có khe dung nham và vách hai đầu, 13 tảng đá lơ lửng chia 3 tầng.
  Tranh hang động cũ được tô lại tông lửa (thác nước thành thác dung nham), có tàn lửa bay.
- **Boss cao 9,5 unit**, khoảng 4,9 lần bạch tuộc. Hình gắn xương cắt từ `su_tu/lion_component.png`. Đi, phi, thở,
  thu mình, vồ, đáp, chồm đứng, vả 2 nhát, gầm (đổi sang đầu há miệng), lắc bờm, cào chân, húc, trúng đòn, gục, chết.
  Bờm và đuôi lay như lửa. Pha 3 có hào quang đỏ và các vết nứt sáng rực.
- **AI:** 6 đòn có báo trước, 3 pha, bị dồn sát thương thì gục 2 giây. Không bị đẩy hay kéo. Đâm vào vách thì tự choáng.
- **Người chơi** có 300 máu. Bị đánh thì nhấp nháy, bất tử 0,8 s, bị đẩy lùi và được cộng nộ.
  Thanh máu nằm ở góc trên trái, dưới thanh nộ.
- Thanh máu boss ở giữa phía trên. Có màn **THẤT BẠI** (Thử lại / Đổi nhân vật) và **CHIẾN THẮNG** (thời gian đánh, Đánh lại / Về màn chọn).
- **Mẹo:** tảng đá chặn được cầu lửa. Thiên thạch rơi xuống bề mặt đầu tiên bên dưới, nên đứng trên sàn dưới một tảng đá
  thì an toàn, còn đang bám dưới chính tảng bị trúng thì không. Chạm vào thân boss không mất máu.

**Chỉnh thông số (Inspector, scene BossArena):**

| Muốn chỉnh | Chọn | Ô |
|---|---|---|
| Sát thương, thời gian báo trước, thời gian nghỉ giữa đòn theo pha, tốc độ nổi giận, tỉ lệ nối đòn, ngưỡng gục | `Lion Boss` (component **Lion Boss**) | các mục theo từng đòn |
| Máu boss | `Lion Boss › Hurtbox` (**Health**) | **Max Health** (6000) |
| Máu, thời gian bất tử của người chơi | `Octopus` (**Health**, **Player Health**) | **Max Health** (300), **Invulnerable Time** (0,8) |
| Camera lùi xa bao nhiêu | camera có component **Boss Arena Camera** | zoom 10 → 12,5 |
| Chiều cao boss, vị trí và số tảng đá | code `Assets/_Project/Scripts/Editor/BossArenaBuilder.cs` | `BossHeight`, `Tiers`, `Rocks` |

Menu Unity:
- **Octopus → Dựng lại màn đánh boss (ghi đè scene BossArena)**
- **Octopus → Dựng lại sư tử (giữ map)**: chỉ dựng lại hình sư tử.

Chọn `Lion Visual` thì có nút xem thử từng dáng ngay trong Inspector.

**Đổi tranh sư tử:** thay `su_tu/lion_component.png` rồi chạy `python3 Tools/art/lion_rig.py` (cần Pillow, numpy, scipy), sau đó
dùng menu **Dựng lại sư tử**. Tấm mới phải xếp bộ phận giống tấm cũ, nếu khác thì toạ độ cắt ở đầu script phải sửa lại.
Tranh hiện cho khoảng 86 px mỗi unit. Muốn boss nét hơn thì cần một tấm bộ phận độ phân giải cao hơn.

**Code:**
- Runtime: `Assets/_Project/Scripts/Runtime/Boss/`
  - `LionBoss.cs`: AI
  - `LionHazards.cs`: vòng báo, cầu lửa, sàn cháy, sóng chấn động, thiên thạch
  - `LionRigAnimator.cs`: hoạt ảnh
  - `ILionVisual.cs`: giao diện giữa AI và hình
  - `BossArenaCamera.cs`, `ArenaGlow.cs`
- Người chơi: `Runtime/Player/PlayerHealth.cs`
- Giao diện: `Runtime/UI/BossBar.cs`, `PlayerHealthBar.cs`, `ResultOverlay.cs`
- Editor: `Editor/BossArenaBuilder.cs`, `Editor/LionRigBuilder.cs`
- Test: `Tests/PlayMode/BossPlayModeTests.cs` (24 test)
