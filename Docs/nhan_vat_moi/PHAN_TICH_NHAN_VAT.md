# 3 nhân vật mới: RED, PINK, BLUE

Ảnh nhân vật: `red/red.png`, `pink/pink.png`, `blue/blue.png`. Ảnh chiêu: `<nhân vật>/basic_attack`, `skill`, `special_skill`
(9 tấm mỗi con, mỗi tấm 6–7 khung). Tên hiển thị trong game: **RED**, **PINK**, **BLUE**. Tên chiêu lấy đúng tên file.

## 1. Cách điều khiển (chung cho cả 3 con; bạch tuộc và nhện giữ cách cũ)

- **Bên trái: cần điều hướng.** Trái / phải để di chuyển. Lên / xuống **không di chuyển**, mà kết hợp với nút chiêu để ra
  kiểu khác: bấm nút không kéo = kiểu thường, kéo **lên** + bấm = kiểu ↑, kéo **xuống** + bấm = kiểu ↓.
- **Bên phải:** 3 nút chiêu (1 đánh thường, 2 dùng chiêu, 3 chiêu đặc biệt cần nộ) và **nút nhảy** (nhảy đúp được).
- Tảng đá là bục để nhảy lên đứng.
- Bàn phím trong Editor: A / D di chuyển, W / S lên / xuống, Space nhảy, J / K / L chiêu (nút 1 / 2 / 3; giữ W / S rồi bấm = kiểu ↑ / ↓).

## 2. Xếp ảnh của bạn vào thường / ↑ / ↓

Tên file của bạn cho biết kiểu chiêu. Mình xếp theo một quy tắc chung để dễ nhớ:
- **↑** cho chiêu đi lên hoặc bắn xa: *hất tung*, *tầm xa*.
- **↓** cho chiêu thủ hoặc trói: *thủ*, *áp chế*, rồi tới *áp sát*.
- **Thường** cho chiêu không có hậu tố. Nếu nút không có chiêu nào như vậy, kiểu còn lại để ở thường.

Muốn đổi chỗ chiêu nào thì báo mình.

| Nút | RED: thường / ↑ / ↓ | PINK: thường / ↑ / ↓ | BLUE: thường / ↑ / ↓ |
|---|---|---|---|
| **1** đánh thường | Đánh thường / hất tung / áp sát | Đánh thường / hất tung / **thủ** | Đánh thường / hất tung / áp sát |
| **2** dùng chiêu | Kĩ năng / tầm xa / thủ | **Dùng chiêu áp sát** / tầm xa / thủ | Dùng chiêu / tầm xa / thủ |
| **3** chiêu đặc biệt | Kĩ năng đặc biệt / tầm xa / áp chế | Kĩ năng đặc biệt / tầm xa / áp chế | Kĩ năng đặc biệt / tầm xa / áp chế |

## 3. Từng chiêu theo ảnh

### RED: kiếm đỏ
| Chiêu | Trong ảnh |
|---|---|
| Đánh thường | chém ngang tới trước, chém ngược tay, giơ kiếm bổ xuống nổ đất |
| Đánh thường hất tung | khom người, nhảy lên chém hất từ dưới lên (vệt trăng dọc), đáp xuống |
| Đánh thường áp sát | lướt thấp tới trước (vệt tốc độ đỏ), chém lưỡi liềm, trượt dừng tung bụi |
| Kĩ năng | kiếm tụ lực, xoay người thành các vòng chém đỏ quanh thân, nổ vòng tinh thể |
| Kĩ năng tầm xa | giơ kiếm, đâm tới phóng sóng lưỡi liềm đỏ bay xa, nổ tinh thể ở đích |
| Kĩ năng thủ | cắm kiếm xuống đất, vòng phép đỏ dưới chân, cột sáng và tinh thể bay lên |
| Kĩ năng đặc biệt | bùng hào quang lửa, lao chém liên tiếp, chém chữ X khổng lồ nổ đá, sóng xung kích |
| Kĩ năng đặc biệt tầm xa | bùng hào quang, giơ kiếm, phóng sóng lưỡi liềm khổng lồ bay rất xa, nổ cột đá đỏ |
| Kĩ năng đặc biệt áp chế | phóng xích đỏ trói kẻ địch, kéo lại đấm, nhấc lên giam trong lồng xích có vòng phép và gai |

### PINK: trượng pha lê tím
| Chiêu | Trong ảnh |
|---|---|
| Đánh thường | vung trượng chém ngang 2 vệt lưỡi liềm tím, đâm trượng nổ tia sáng |
| Đánh thường hất tung | khom thấp, vung trượng hất lên thành vệt trăng dọc, pha lê trồi lên khỏi đất và bay vút lên |
| Đánh thường thủ | dựng trượng, vòng sáng xoắn quanh người, cắm trượng làm gai pha lê trồi lên quanh mình |
| Dùng chiêu áp sát | lướt tới (bóng mờ, vệt tốc độ), quét trượng lưỡi liềm, trượt dừng tung bụi |
| Dùng chiêu tầm xa | vòng phép trước trượng, phóng luồng pha lê tím hình rồng bay xa, nổ đá vụn ở đích |
| Dùng chiêu thủ | cắm trượng, nhắm mắt, vòng phép dưới chân, vòng sáng và pha lê bay quanh người |
| Kĩ năng đặc biệt | vòng phép xoay quanh người, bay lên, nổ mái vòm pha lê lớn quanh mình, đá vụn văng |
| Kĩ năng đặc biệt tầm xa | tụ cầu sáng đầu trượng, vòng phép, phóng tia pha lê lớn, nổ hình sao ở đích |
| Kĩ năng đặc biệt áp chế | phóng xích từ trượng trói kẻ địch, giam trong lồng xích có cột giáo pha lê và vòng phép |

### BLUE: lưỡi hái lửa xanh
| Chiêu | Trong ảnh |
|---|---|
| Đánh thường | giơ lưỡi hái lên, quét ngang lưỡi liềm lửa xanh, quét tiếp, thu về |
| Đánh thường hất tung | quét thấp, hất lưỡi hái lên thành vệt dọc, bay lên trong cột lửa xanh, đáp xuống |
| Đánh thường áp sát | lướt tới (vệt lửa xanh), quét lưỡi liềm lớn, trượt dừng tung bụi |
| Dùng chiêu | xoay người thành các vòng lửa xanh quanh thân, rồi quét mạnh tới trước |
| Dùng chiêu tầm xa | giơ lưỡi hái tụ cầu sáng và vòng phép, quét phóng sóng lưỡi liềm xanh bay xa, nổ tinh thể |
| Dùng chiêu thủ | giơ lưỡi hái, lửa xanh và đầu lâu bốc lên quanh người, vòng phép dưới chân, tinh thể bay quanh |
| Kĩ năng đặc biệt | lửa bùng, xoay chém thành vòng xoáy, bay lên rồi giáng xuống nổ cột tinh thể lớn |
| Kĩ năng đặc biệt tầm xa | tụ cầu sáng, quét phóng luồng lửa xanh khổng lồ chạy dọc mặt đất, nổ tinh thể ở cuối |
| Kĩ năng đặc biệt áp chế | ném lưỡi hái và xích trói kẻ địch, giam trong lồng xích có cột giáo và lửa xanh |

## 4. Tác dụng trong game (suy ra từ ảnh)

- **Hất tung:** hất kẻ địch bay lên, nhân vật bật lên theo.
- **Áp sát:** lướt tới, chém trên đường đi.
- **Tầm xa:** đạn bay xa, nổ ở chỗ trúng.
- **Thủ:** trong lúc vòng phép còn thì chặn sát thương; kẻ địch đứng sát bị đánh (gai, cột sáng, lửa).
- **Áp chế:** trói kẻ địch gần nhất phía trước, giam lâu và gây sát thương liên tục. Boss bị giam ngắn hơn.
- **Chiêu đặc biệt:** tốn nộ, mốc nộ càng cao càng đau.

## 5. Cỡ và tầm chiêu (×2)

Mọi chiêu của cả 5 nhân vật hiện to ×2 và xa ×2:
- **Cỡ ×2:** vùng đánh trúng và hiệu ứng.
- **Tầm ×2:** khoảng đánh tới, quãng đạn hoặc sóng bay (bay nhanh gấp đôi để thời gian bay không đổi), quãng lướt, tầm tìm mục tiêu
  của chiêu trói, độ dài tia laze.
- **Không đổi:** sát thương, hồi chiêu, thời gian chiêu, độ cao hất tung, thời gian choáng. Lồng giam vẫn vừa với kẻ bị trói.

Chỉnh trong Unity: chọn người chơi `Octopus` › **Combat Controller** › mục **Cỡ / tầm chiêu**. Mỗi nhân vật có một cặp
**Size** (cỡ) và **Range** (tầm), mặc định 2 / 2; đặt 1 / 1 là về như cũ. Ảnh trước (trên hoặc trái) / sau (dưới hoặc phải)
từng chiêu: `Docs/nhan_vat_moi/xem_chieu_x2/`.

## 6. Hiệu ứng chiêu: cắt từ tấm của bạn, tách lớp

Hiệu ứng mỗi chiêu là các mảnh cắt từ chính tấm ảnh chiêu của bạn (`Assets/_Project/Resources/HeroFx/Red|Pink|Blue/`),
không vẽ thêm. Code đặt, xoay, phóng to và làm mờ từng mảnh theo nhịp chiêu.

- **Cắt từ nhiều khung:** chiêu nào tấm có nhiều pha (vệt nhỏ → vệt lớn, một vòng → nhiều vòng, vòng cung → chữ X) thì lấy
  từng pha làm một mảnh và chiếu lần lượt.
- **Tách lớp:** lõi trắng chớp rồi tắt, thân màu, đuôi, tia và đá vụn là các mảnh riêng. Vòng phép, vòng xoay và lồng giam
  chia nửa sau (vẽ sau lưng nhân vật hoặc kẻ địch) và nửa trước.
- **Bám vũ khí:** vệt chém đặt ở ngực hoặc tay cầm kiếm, sóng và đạn xuất phát từ đầu vũ khí, hiệu ứng đất đặt sát mặt đất.
  Ở cỡ ×2 hiệu ứng không còn bay lệch khỏi vũ khí.
- **Lồng giam (chiêu áp chế):** đã bỏ hình nộm trong ảnh; kẻ địch thật đứng giữa lớp lồng trước và sau.
- **Cỡ hiệu ứng ở ×2:** vùng đánh trúng vẫn ×2, còn hình hiệu ứng lớn vừa phải để không che nhân vật. Chỉnh từng chiêu trong
  Unity: chọn `Octopus` › **Combat Controller** › **Red / Pink / Blue Skills** › chiêu (0–8), ô **Fx Grow** (RED) hoặc
  **Fx Scale** (PINK, BLUE). Số càng lớn, hình càng
  gần cỡ vùng đánh.

Codex chấm lại độ giống tấm gốc (điểm trên 10, trung bình 9 chiêu):

| Nhân vật | Trước | Sau |
|---|---:|---:|
| RED | 6,0 | 7,7 |
| PINK | 5,9 | 7,8 |
| BLUE | 6,1 | 7,7 |

Ảnh 3 tầng cho từng chiêu (tấm gốc / trước khi sửa / sau khi sửa): `codex_review/sau_khi_sua/<nhân vật>_<0..8>.jpg`.
Mọi mảnh hiệu ứng: `codex_review/sau_khi_sua/<nhân vật>_fx_pieces_after.jpg`. Nhận xét chi tiết của Codex:
`codex_review/codex_<nhân vật>_lan2.md` (lần 1: `codex_<nhân vật>.md`).
