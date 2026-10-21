**Bản ×2 nhận ra được cả 9 chiêu, nhưng phóng to cũng làm lộ lỗi cắt và lệch tỉ lệ.** Cắt thêm có ích nhất khi lấy đúng các pha hiệu ứng và tách lớp trước/sau; chia nhỏ một sprite hiện tại sẽ không bổ sung được chuyển động còn thiếu.

Đã kiểm tra [HeroRigAnimator.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Heroes/HeroRigAnimator.cs:477), [PinkSkills.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Combat/Heroes/PinkSkills.cs:142), [PinkFx.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Combat/Heroes/PinkFx.cs:177), [CombatFx.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Combat/CombatFx.cs:232) và [script cắt](/Users/apple/StudioProjects/octopus/Tools/art/hero_fx_pink.py:61). Đúng như mô tả: nhân vật dùng rig; mỗi mảnh FX là một sprite co giãn/di chuyển/fade, chưa có flipbook. **Không sửa file.**

Quy ước: **K1…K6** đếm trái→phải trên ảnh gốc; #4 có rồng và vụ nổ nối liền trong **K4**, dáng hồi là **K5**. “Cắt sạch” dưới đây chỉ phần FX nhìn thấy, không bao gồm phần bị nhân vật che. Điểm dự kiến tính khi sửa FX, cỡ và nhịp, vẫn giữ rig hiện tại. Sheet không ghi thời lượng nên chỉ đánh giá thứ tự và sự chuyển pha.

**#0 — Slot 1 thường: Đánh thường — 6/10**

- **Giống/khác:** Có đúng trăng nhỏ, trăng lớn và chớp tím. Dáng đâm hai tay tương đối đúng nhưng độ xoay thân/tóc kém gốc. Ở ×2, vệt trăng hơi tách xa đầu trượng, cung code chồng thêm làm dày và trắng hơn. Game chia chuỗi nhỏ→lớn→chớp thành ba đòn combo, khác cảm giác chuyển động liên tục của sheet.
- **Chọn C → 8/10:** Dùng **K3→K4** làm hai pha phát triển của vệt; **K5** là pha chớp/tan riêng. Cắt sạch được **mép ngoài cung K3–4, sao và các mảnh xa đầu trượng K5**. Không sạch toàn bộ K3–4 vì trượng/tóc/chân chồng vào; K5 cần loại đầu trượng. **K1,2,6** không có FX độc lập phù hợp. Thêm lõi cung, đuôi mảnh và cụm shard riêng; giữ pivot nhất quán giữa hai pha.
- **Khó:** Hai pha cung khác hình và kích thước, dễ giật khi đổi ảnh; phần xóa trượng có thể khoét mất lõi sáng.

**#1 — Slot 1 ↑: Đánh thường hất tung — 7/10**

- **Giống/khác:** Nhận diện tốt nhất ở vệt hất dựng đứng, đúng tím hồng và pha lê bay lên. Dáng chưa vươn/ngửa rõ như gốc. ×2 khiến vệt rất cao so với người; hiện giống một hình kéo dài rồi giữ nguyên, thiếu pha cung nhỏ dẫn lên cung lớn. Pha tan còn chồng với vệt chính.
- **Chọn C → 8/10:** Lấy **K3→K4** cho cung phát triển, **K5** cho pha lê bay lên. Sạch: **cung ngoài bên phải K3–4, cụm tinh thể bên phải K5**. Không sạch nguyên vệt K3–4 vì trượng và nhân vật che lòng cung; **K2** có hào quang dính đầu trượng; **K1,6** không có pha FX mới. Thêm tia chạm đất, cung dẫn nhỏ và tinh thể rời.
- **Khó:** Ghép hai cung phải khớp chân cung; loại tóc còn sót ở K5. Quầng cao ×2 dễ tốn diện tích phủ trên màn hình.

**#2 — Slot 1 ↓: Đánh thường thủ — 5/10**

- **Giống/khác:** Đúng thứ tự vòng xoắn→cắm trượng→gai. Dáng khom/cắm có nhưng thiếu sức nén của gốc. Vòng code thành các cung ngang dày; gai hiện như hai mảng hai bên, thiếu cụm thấp nối giữa. Màu sáng khá đúng, nhưng ×2 làm hiệu ứng rộng và rời người.
- **Chọn B → 7/10:** Sạch được **tia đất bên phải K3; các ngọn gai ngoài cùng K4–5**. Không sạch toàn cụm **K2** vì vòng xuyên qua thân, **K3** vì đầu trượng/chân, **K4–5** vì tóc/chân che và xen giữa gai. **K1,6** không có FX độc lập. Thêm 3–4 mẫu gai lớn/nhỏ, cụm chân gai thấp, mảnh vỡ; tách vòng xoắn thành nửa trước/nửa sau.
- **Khó:** Gai đang bị nhân vật che không thể lấy lại bằng cắt. `spikes_back` hiện còn cả mảng tóc; nhân bản mảnh này sẽ nhân luôn lỗi.

**#3 — Slot 2 thường: Dùng chiêu áp sát — 5/10**

- **Giống/khác:** Có lướt, bóng mờ, quét và bụi đúng ý tưởng. Dáng lao chưa thấp/duỗi như gốc; động tác trượng chưa dẫn được cung quét rõ. ×2 làm cung cuối quá nổi so với người, trong khi bóng mờ nhỏ và yếu. Vệt code rộng, sáng nhạt hơn nét tím sắc của gốc; bụi và quét còn tạo cảm giác dán ảnh.
- **Chọn B → 7/10:** **K2–3:** lấy vệt tốc độ ngoài thân; bóng nhân vật phải giữ thành lớp ghost có chủ ý. **K4:** lấy mép cung phải và dải sát đất; **K5:** lấy các đám bụi ngoài hai bên. Không sạch toàn FX K2–5 vì thân/trượng/chân xen vào; **K1,6** không có FX mới. Thêm ghost toàn dáng lao, vệt dài/ngắn, cung trước và bụi trước/sau.
- **Khó:** Chỉ có một pha quét rõ nên chưa đủ nguồn cho flipbook quét đầy đủ. `sweep` còn da/chân và `dust` còn tóc; mép cắt đặc biệt dễ lộ khi ×2.

**#4 — Slot 2 ↑: Dùng chiêu tầm xa — 6/10**

- **Giống/khác:** Có vòng tụ phép, luồng tím và nổ đá đúng chủ đề. Dáng chĩa trượng gần đúng nhưng thiếu pha chuyển từ tụ sang phóng. Ở ảnh game, đầu rồng khó đọc; luồng sáng/nổ lấn át hình rồng. ×2 làm vòng và vụ nổ khá lớn. Mã cho rồng nở trong **0,25 giây**, nên trúng mục tiêu gần có thể nổ trước khi thân hiện đủ.
- **Chọn C → 8/10:** Chuỗi FX **K2→K3→K4**, đồng thời tách vòng, lõi tia, đầu rồng, lửa đuôi và đá. Sạch: **nửa phải vòng K2; phần mũi pha lê phía trước K3; thân/đầu rồng và phần lớn vụ nổ K4**. Không sạch nguyên vòng K2–3 vì trượng/tóc; mép phải dưới vụ nổ K4 sát tóc dáng hồi cần mask. **K1,5** chủ yếu là dáng trượng.
- **Khó:** Nguồn chỉ có một hình rồng hoàn chỉnh, không đủ làm hoạt ảnh rồng nhiều khung thật. Tách đá phải giữ mặt tối đặc; luồng dài ×2 dễ tăng overdraw.

**#5 — Slot 2 ↓: Dùng chiêu thủ — 6/10**

- **Giống/khác:** Có vòng dưới chân, pha lê và vòng sáng quanh người. Dáng niệm còn ít biểu cảm; chưa thấy rõ chuyển nhắm mắt→mở mắt như gốc. Vòng ngang ×2 quá rộng so với thân, thiếu cảm giác xoắn ôm người. Màu hơi trắng/hồng nhạt, mật độ chữ phép và tinh thể theo chiều cao chưa bằng gốc. Nhịp cắm→bung vòng đúng.
- **Chọn B → 8/10:** Sạch: **nửa trước và hai bên vòng chân K3–6; tinh thể rời ngoài người K4–6**. Không sạch nguyên vòng chân vì chân/trượng; vòng xoắn **K4–6** bị tóc/thân che; chớp **K2** dính đầu nhọn trượng. **K1** không có FX độc lập. Thêm các đoạn vòng xoắn trước/sau, chữ phép, tia đứng và 2–3 mẫu tinh thể.
- **Khó:** Vá vòng bằng đối xứng dễ lặp hoa văn. Nhiều lớp bán trong suốt phủ cùng vùng sẽ tăng overdraw; nên dùng ít mảnh lớn có hình rõ.

**#6 — Slot 3 thường: Kĩ năng đặc biệt — 6/10**

- **Giống/khác:** Đúng chuỗi tụ lực→bật lên→dộng xuống→vòm nổ. Thiếu vòng chữ cổ và đường cuộn dẫn động tác ở pha đầu. Vòm ×2 đủ hoành tráng nhưng che nhiều cảnh; lõi thành mảng tím nhòe, mất chi tiết sắc của gốc. Toàn vòm nằm sau nhân vật nên thiếu gai/đá phủ trước chân.
- **Chọn C → 7/10:** Dùng **K4→K5** cho hai pha vòm; lấy thêm rune **K2**, cung ngoài **K3**, tàn lửa **K1,6**. Sạch được **các đoạn rune ngoài thân, mép cung, ngọn pha lê và đá ngoài vòm**. Không khung nào cho toàn bộ vòm/vòng quanh người sạch: K1–3,6 dính tóc/thân; K4–5 bị PINK che giữa. Thêm rune trước/sau, gai trước chân, đá riêng và chớp đất.
- **Khó:** `nova_dome` hiện lấp chỗ nhân vật bằng ánh sáng khuếch tán, tạo rõ một “bóng tím” giữa vòm. Cắt vụn chỉ cải thiện được phần ngoài; muốn lõi sắc cần vá/vẽ lại. Đây cũng là chiêu có nguy cơ overdraw cao nhất.

**#7 — Slot 3 ↑: Kĩ năng đặc biệt tầm xa — 6/10**

- **Giống/khác:** Có đủ cầu tụ→vòng→tia→nổ sao. Dáng giơ/chĩa trượng tương đối đúng nhưng thiếu lực đẩy thân. Trong game, tia dễ đọc thành khối sáng ngắn; nổ và vòng ×2 lấn át mũi tia. Màu đúng họ tím nhưng nhiều lớp sáng chồng làm mất các cạnh pha lê. Nhịp phát chiêu rõ, hình bay chưa biến đổi nhiều.
- **Chọn B → 8/10:** Sạch nhất: **tia phía trước đầu trượng K4 và vụ nổ độc lập K5**. **K2** chỉ lấy lửa ngoài đầu trượng; **K3** lấy vòng ngoài nhưng không sạch toàn vòng vì vũ khí nằm giữa. **K1,6** không có FX mới. Thêm mũi pha lê, lõi trắng, hai vệt đuôi và các tia sao riêng; cầu/vòng vẫn bám đầu trượng.
- **Khó:** `circle_lance` hiện giữ cả chi tiết đầu trượng rồi đối xứng, có thể tạo cảm giác thêm một đầu vũ khí. Chỉ có một hình tia và một vụ nổ nên tách lớp hữu ích hơn cố làm flipbook.

**#8 — Slot 3 ↓: Kĩ năng đặc biệt áp chế — 6/10**

- **Giống/khác:** Xích nối tới mục tiêu, chuyển sang lồng cao đúng trình tự; màu và hình giáo khá sát. Tuy nhiên lồng/xích quấn là một hình giữ nguyên, thiếu chuyển động siết và các lớp bao quanh mục tiêu. **PNG hiện còn nguyên hình nộm tím**, gồm đầu, mắt và thân, dễ đè thành “người thứ hai” trên nạn nhân thật. Lồng được chỉnh theo mục tiêu, không nhân ×2.
- **Chọn B → 8/10:** Sạch: **đoạn xích xa người K2–3; cột giáo ngoài và vòng chân K4–5; phần lồng bên phải K6**. Không sạch toàn `chain_wrap` **K3** hoặc toàn lồng **K4–6** vì chứa hình nộm; K6 còn bị PINK che bên trái. **K1** không có FX hữu ích. Thêm từng cột giáo, đoạn xích trước/sau, vòng đáy và hạt tan; bỏ hình nộm khỏi mọi mảnh.
- **Khó:** Xích chồng lên nộm cần mask kỹ để giữ mắt xích. Hiện mỗi sợi dùng 40 SpriteRenderer, hai sợi là 80; tách thêm cần kiểm soát số renderer và batching.

**Các lỗi hình ảnh cần xử lý**

- **Dính nhân vật:** thấy trực tiếp trong PNG của `crescent_big`, `spikes_back`, `rise_crystals`, `sweep`, `dust`; `chain_wrap` và `cage` còn hình nộm rất rõ.
- **Mép cụt:** `spikes_back` có biên dọc thẳng; `sweep` bị khoét giữa; một số đuôi/quầng kết thúc sát biên ảnh.
- **Nền/khối chữ nhật:** `dust` có vùng cắt vuông dễ lộ; mã cũng ghi nhận và giữ bụi ở cỡ ×1. Chưa đủ bằng chứng để gọi mọi mảnh là “hình chữ nhật đục”.
- **Neo và cỡ:** cung #0 dễ xa đầu trượng; #1 quá cao; #2–3 rộng/rời người; #4 và #6 lấn cảnh ở ×2. Pivot ngoài ảnh trong manifest là chủ ý neo theo chân, **không tự nó chứng minh pivot sai**; vấn đề là nhân ×2 cả khoảng lệch chứa trong sprite.
- **Thứ tự lớp:** vòng `Slash` của #2/#5 nằm trước nhân vật, thiếu nửa sau bị thân che; vòm #6 lại toàn bộ ở phía sau.

| Chiêu | Điểm hiện tại | Đề xuất | Điểm dự kiến | Ưu tiên |
|---|---:|---|---:|---|
| #0 Đánh thường | 6 | C — nhiều pha + tách lớp | 8 | Cao |
| #1 Hất tung | 7 | C — cung nhỏ/lớn + tinh thể | 8 | Trung |
| #2 Đánh thường thủ | 5 | B — gai và vòng trước/sau | 7 | Trung |
| #3 Áp sát | 5 | B — ghost, quét, bụi | 7 | Cao |
| #4 Rồng tầm xa | 6 | C — tụ/phóng + thân/đầu/đá | 8 | Cao |
| #5 Chiêu thủ | 6 | B — vòng xoắn và tinh thể | 8 | Trung |
| #6 Vòm đặc biệt | 6 | C — hai pha vòm + rune/đá | 7 | Cao |
| #7 Tia đặc biệt | 6 | B — lõi, mũi, đuôi, sao | 8 | Trung |
| #8 Áp chế | 6 | B — bỏ nộm, tách xích/lồng | 8 | Cao |

**5 ưu tiên cho PINK**

1. **Làm sạch PNG trước:** bỏ nộm #8, tóc/chân trong các mảnh và mép vuông của bụi/quét.
2. **Canh lại ×2 theo từng chiêu:** giữ điểm phát ở vũ khí/chân; chỉnh riêng kích thước và khoảng cách để FX còn gắn với động tác.
3. **Sửa #6:** tách rune, vòm, gai trước chân và đá; xử lý mảng tím vá giữa vòm.
4. **Sửa #4:** cho hình rồng hiện rõ trước khi trúng/nổ, giữ luồng nối hợp lý với đầu trượng; áp dụng tương tự cho tia #7.
5. **Chỉnh dáng và chiều sâu:** ưu tiên dáng lao/quét #3, cắm trượng #2/#5; tách vòng trước/sau và nối nhịp nhỏ→lớn của #0.