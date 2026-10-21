**Bản ×2 đã rõ hiệu ứng hơn, nhưng độ giống vẫn bị giới hạn bởi dáng người, hình vệt chém và lớp sáng trắng che mất chi tiết.** Chỉ tăng kích thước chưa giải quyết được ba điểm này.

Đã kiểm tra, không sửa file: [HeroRigAnimator.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Heroes/HeroRigAnimator.cs:581) nội suy các dáng rig; [BlueFx.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Combat/Heroes/BlueFx.cs:180) phát một sprite rồi biến đổi; [CombatFx.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Combat/CombatFx.cs:232) bổ sung vệt chém. [Script cắt](/Users/apple/StudioProjects/octopus/Tools/art/hero_fx_blue.py:52) chưa lấy FX riêng từ sheet của **#0, #2, #3, #6**; các chiêu này dùng lại hình từ chiêu khác.

Quy ước: **F1–F6** đếm các pha gốc từ trái sang phải; riêng #7, F3–F4 là luồng lửa vẽ nối liền, #8 F6 gồm BLUE và lồng bên cạnh. “Vùng sạch” chỉ phần được nêu, không có nghĩa cả FX sạch. Điểm dự kiến giả định có chỉnh đồng bộ dáng và thời điểm phát. Sheet gốc không quy định thời lượng, nên đánh giá nhịp dựa trên chuỗi ảnh và mã.

**#0 — Nút 1 thường: Đánh thường — 5,5/10**

- **Giống/khác:** Có giơ hái rồi quét ngang, đúng xanh cyan. Gốc là cung chém dài ôm đường đi lưỡi hái; game giống một khối sóng phóng trước người. Dáng chân/thân ít đổi, ×2 che phần lớn người; lõi trắng dày hơn gốc. Mã còn chia thành ba lần bấm combo, không phát liền toàn bộ sheet.
- **Chọn B → 7,5/10:** Lấy cung ngoài bên phải **F3**, cung phải và dải thấp **F4**, mũi vệt sát đất **F1/F6**. **Không F1–F6 nào sạch nguyên hiệu ứng**; F2/F5 đặc biệt dính lưỡi hái. Thêm lớp cung chính, đuôi mảnh, tàn lửa; bám theo lưỡi hái thay vì dùng chung `Wave`.
- **Khó:** Phải loại kim loại mà giữ lõi lửa trắng. Nhiều lớp sáng chồng nhau dễ tiếp tục làm người chìm.

**#1 — Nút 1 ↑: Hất tung — 6/10**

- **Giống/khác:** Đúng chuỗi quét thấp → hất lên → bật người. Game có vệt đứng tương tự, nhưng khá giống tấm cong dựng sẵn; tư thế co gối, nghiêng thân chưa rõ. ×2 làm vệt vượt cao khỏi người; vòng trắng sát đất gây nhiễu. Trong mã, `Rise` và `Pillar` xuất hiện cùng pha, trong khi gốc phân biệt cung hất và cột bật.
- **Chọn C → 8/10:** Cắt vùng cung phải **F3→F4** thành chuỗi ngắn; lấy chân cột **F5**, tách tia giữa và lửa hai bên. Mép quét thấp **F2** cũng dùng được. **Không sạch nguyên FX ở F1–F6**: F3/F4 dính hái, F5 bị người che phần trên; F1/F6 ít thông tin mới.
- **Khó:** Hai cung có hình và kích thước khác nhau, cần căn chung gốc chân. Không kéo giãn một mảnh `Rise` để thay cả chuyển động.

**#2 — Nút 1 ↓: Áp sát — 5,5/10**

- **Giống/khác:** Có lướt, vệt kéo sau và chém cuối. Gốc nhấn mạnh thân lao thấp, áo/lửa kéo dài, rồi trượt khựng; game nổi bật chủ yếu ở sóng cuối. ×2 khiến đòn kết quá lớn so với dáng lướt; vệt đường đi nhạt, trắng hơn lửa xanh gốc.
- **Chọn B → 7,5/10:** Lấy tia đạp đất phía sau **F2**, các dải tốc độ ngoài thân **F3**, cung ngoài bên phải **F4**, bụi/đá sau gót **F5**. **F1–F6 đều không sạch nguyên FX**; F1/F6 chủ yếu là tư thế chuẩn bị/hồi. Thêm đuôi lửa ngang, tia đạp, cung kết và bụi dừng riêng.
- **Khó:** Đuôi lửa hòa vào áo, không thể tách sạch bằng ngưỡng màu. Bụi xám dễ bị thuật toán xóa nhầm như kim loại.

**#3 — Nút 2 thường: Xoay rồi quét — 4,5/10**

- **Giống/khác:** Đúng ý xoay quanh thân rồi chém trước. Tuy nhiên gốc có lốc nhiều tầng, ngọn lửa sắc và khoảng trống thấy người; game thành các vòng trắng dẹt cùng màn lửa phía sau. ×2 làm vòng quá rộng, thân người nhỏ ở giữa. Vòng sinh ngẫu nhiên trong mã nên bố cục khó giữ giống gốc.
- **Chọn C → 8/10:** Lấy các đoạn vòng ngoài **F3→F4**, tách cung trước/cung sau; cung kết lấy bên phải **F5**, lửa chân lấy hai bên **F2**. **Không có FX nguyên khối sạch ở F1–F6**; tâm F2–F5 dính người/hái, F1/F6 không có lốc riêng.
- **Khó:** Phải vá phần vòng bị che và giữ đúng thứ tự trước/sau. Thay hình F3→F4 cần giữ tâm, tránh giật. Nhiều vòng cộng sáng là nguồn tốn GPU đáng kể.

**#4 — Nút 2 ↑: Sóng tầm xa — 7/10**

- **Giống/khác:** Nhận diện tốt nhất nhóm thường: vòng phép → sóng liềm → nổ. Cyan và hình sóng khá sát vì lấy đúng sheet. Dáng giơ hái/chĩa hái còn cứng; ×2 làm cầu tụ lớn so với đầu hái. Sóng bay giữ nguyên hình, vụ nổ chỉ phóng to rồi mờ, thiếu biến đổi nội tại.
- **Chọn C → 8,5/10:** **F5** có vùng nổ độc lập lớn, tránh mép sát nhân vật F6. **F4** lấy phần đầu sóng phía trước mũi hái; **F3** lấy cung ngoài để chuyển từ quét sang bay. **F2** chỉ nửa phải vòng phép sạch. Không lấy nguyên F2/F3/F4 vì dính hái/người; F1/F6 không có FX mới.
- **Khó:** Hai hình sóng F3/F4 cần xóa hái và căn cùng tâm. Chỉ có một trạng thái nổ sạch; muốn nổ nhiều khung thực sự phải vẽ bổ sung.

**#5 — Nút 2 ↓: Thủ — 6/10**

- **Giống/khác:** Đủ vòng phép, đầu lâu và tinh thể; dáng giữ hái đứng khá đúng. Nhưng gốc phát triển từ vòng mỏng → lửa cuộn → bùng cao; game duy trì màn lửa khá đều. ×2 khiến tinh thể/vòng bay xa thân, lửa xanh trắng quá rộng và dày, mất cảm giác bao sát người.
- **Chọn B → 8/10:** Lấy nửa trước vòng chân **F3**; dải xoắn bên ngoài **F2/F4**; tinh thể và đầu lâu rời **F5/F6**, đầu lâu hai bên **F3**. **F1–F6 không sạch nguyên hào quang**; F1 không có FX thủ độc lập. Thêm các lưỡi lửa đứng, xoắn trước/sau, vài biến thể đầu lâu; giữ tinh thể là mảnh riêng.
- **Khó:** Vòng chân bị chân che, cần vá thay vì soi đối xứng máy móc. Quá nhiều đầu lâu, lửa và bản glow cùng tồn tại sẽ tăng vùng vẽ chồng.

**#6 — Nút 3 thường: Bật xoáy rồi giáng — 6,5/10**

- **Giống/khác:** Đúng cấu trúc lấy đà → bay xoáy → giáng nổ, có đá đen và cyan. Khác chính là lốc gốc có đường xoắn rõ; game dùng hai `Wave` quay quanh thân cộng vòng ngẫu nhiên. Dáng co người/giáng hái chưa mạnh. ×2 làm vụ đáp rất lớn, lõi trắng che nhân vật và giảm chi tiết đá.
- **Chọn C → 8/10:** Lấy vành phải **F3**, các dải xoắn ngoài **F4**, cột/tinh thể hai bên **F5**, lửa chân ngoài thân **F2**. **Không F1–F6 nào sạch nguyên FX**: F2–F5 bị người/hái che, F1/F6 chỉ có vệt hồi nhỏ. Dùng chuỗi lớp xoáy F3→F4, thêm đá bay, tia giáng và nứt đất riêng.
- **Khó:** Phần bị che lớn, cần vá/vẽ lại. Không dùng nổ của #7 thay toàn bộ F5: hình cột đá và hướng lực của hai chiêu khác nhau.

**#7 — Nút 3 ↑: Luồng lửa tầm xa — 7/10**

- **Giống/khác:** Giữ tốt nhận diện cầu xoáy, luồng lửa có khói/đá và vụ nổ cuối. Nhưng game lặp cùng một mảng thành hàng; đầu luồng khá vuông, hoa văn lặp rõ. ×2 tạo bức tường lửa lớn, gần phủ hết vùng chiến đấu. Nhân vật chưa thể hiện rõ động tác đẩy luồng bằng hái.
- **Chọn B → 8,5/10:** **F4**, phần giữa luồng, là nguồn sạch tốt nhất; **F5** lấy nổ độc lập, tránh người F6. **F3** chỉ phần luồng phía trước hái sạch; **F2** chỉ nửa phải cầu xoáy. F1/F6 không có luồng mới. Tách đầu luồng nhọn, thân nối được, lõi sáng, khói trên và đá dưới.
- **Khó:** **F3–F4 là hình luồng liên tục, không phải hai ảnh hoàn chỉnh để làm flipbook.** Cần sửa mép nối và tạo biến thể thân; diện tích phủ màn hình khiến đây là chiêu có nguy cơ tốn GPU cao nhất.

**#8 — Nút 3 ↓: Áp chế — 7/10**

- **Giống/khác:** Xích và lồng rất dễ nhận ra, đúng màu và đường nét chủ đạo. Game thiếu pha xích quấn quanh thân mục tiêu trước khi lồng đóng; lồng chủ yếu mọc bằng kéo cao một ảnh, không có chuyển động nâng/quấn rõ như F4→F5. Dáng chĩa hái chưa đủ thẳng; vệt quét trắng lớn lúc phóng xích hơi thừa.
- **Chọn B → 8,5/10:** Lấy đoạn xích giữa hái và mục tiêu **F2**; cột ngoài, chóp, đế **F4/F5**; dải xoắn ngoài **F5** và phần lồng bên phải **F6**. **Không lấy nguyên F3–F6:** hình nộm nằm trong xích/lồng; F2 dính hái ở đầu nối, F1 không có FX mới. Thêm xích quấn trước/sau mục tiêu, lồng trước/sau, tia giữa và đế riêng.
- **Khó:** Xóa hình nộm không khôi phục được xích nằm sau nó. Lồng cần vừa mục tiêu; trong [mã hiện tại](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Combat/Heroes/BlueSkills.cs:1321), cỡ lồng tính theo mục tiêu, **không trực tiếp nhân `ctx.Size`**.

**Các lỗi hình cần xử lý**

- **Mép cắt lộ rõ trong bảng mảnh:** `Wave` bị cụt phía trên/phải; `Rise` giống lát dọc hẹp; `Pillar` có biên thẳng bên phải; `Skull` có khuyết vuông phía trên. Phóng ×2 làm chúng dễ thấy hơn.
- **Đường ghép:** `Vortex` lộ đường dọc giữa hai nửa; `Seal` còn chi tiết xám giống phần chân/giáp. `Cage` có khoảng rỗng theo đầu/thân hình nộm; cần phục hồi họa tiết bị mất.
- **Luồng #7:** Đầu vuông và họa tiết lặp là lỗi thấy trực tiếp ở bản game. Mã đang kéo ngang từng mảnh để hiện dần, làm biến dạng hình thay vì mở dần.
- **Căn với vũ khí:** #0/#2/#3 có cảm giác sóng rời lưỡi hái. `Crescent` đặt theo tâm người cộng khoảng cách, không theo đầu hái; khi tăng cỡ/tầm, khoảng hở dễ rõ hơn.
- **Cỡ ×2:** #3 và #5 rộng quá mức so với thân; #6 quá sáng lúc đáp. #1 cần cân lại chiều cao vệt với độ bật người. #7 vốn phải lớn, nên ưu tiên giữ đầu luồng và nhân vật dễ đọc.
- **Chưa đủ bằng chứng kết luận có hình chữ nhật nền đặc hoặc pivot sai.** Có mép cắt thẳng; thuật toán tách nền/vá vùng tối cũng có nguy cơ giữ màu navy. Cần kiểm tra alpha trên nền sáng trước khi xác nhận hai lỗi này.

| Skill | Hiện tại | Đề xuất | Dự kiến | Ưu tiên |
|---|---:|---|---:|---|
| #0 · 1 thường | 5,5 | B · cung riêng, tách đuôi | 7,5 | Cao |
| #1 · 1 ↑ | 6 | C · chuỗi cung + cột riêng | 8 | Cao |
| #2 · 1 ↓ | 5,5 | B · đuôi lướt, chém, bụi | 7,5 | Trung |
| #3 · 2 thường | 4,5 | C · vòng nhiều pha, trước/sau | 8 | Cao |
| #4 · 2 ↑ | 7 | C · chuyển hình sóng + tách nổ | 8,5 | Trung |
| #5 · 2 ↓ | 6 | B · lửa cuộn và hào quang | 8 | Cao |
| #6 · 3 thường | 6,5 | C · xoáy riêng + giáng riêng | 8 | Cao |
| #7 · 3 ↑ | 7 | B · đầu/thân/khói/đá | 8,5 | Cao |
| #8 · 3 ↓ | 7 | B · xích quấn, lồng trước/sau | 8,5 | Trung |

**Năm ưu tiên cho BLUE**

1. **Sửa alpha, mép cụt và đường ghép** của các mảnh hiện có trước khi cắt thêm.
2. **Làm lại lốc #3**, tách vòng trước/sau và giảm vòng trắng; đây là chiêu khác gốc nhiều nhất.
3. **Đồng bộ hái–FX–thời điểm đánh**, nhất là #0/#1/#2; bổ sung dáng lao thấp, co gối và giáng người.
4. **Tạo bộ hình riêng cho #0/#2/#6**, giảm việc dùng cùng `Wave` cho mọi kiểu chém.
5. **Cân lại ×2 và chi phí vẽ chồng:** giảm màn lửa #5, sáng đáp #6; sửa đầu/thân nối của #7. Dùng atlas và kho tái sử dụng hiện có; hạn chế nhân đôi glow trên mọi lớp.