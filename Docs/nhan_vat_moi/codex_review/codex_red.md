**RED hiện giống về ý tưởng chiêu hơn là hình dáng chuyển động.** Bản ×2 dễ thấy hiệu ứng hơn, nhưng một số vệt bị nâng cao, rời kiếm và lấn át nhân vật.

Đã đọc mã, không sửa file: dáng dùng các mốc nội suy trong [HeroRigAnimator.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Heroes/HeroRigAnimator.cs:368); `RedFx` phát sprite đơn bằng dịch chuyển, phóng to và mờ dần, **chưa có flipbook**. `CombatFx` đã có lớp thân, lõi sáng và quầng; phần thiếu chủ yếu là hình vẽ đặc trưng và cách phối hợp chúng.

Quy ước dưới đây: đánh số **khung 1–6 từ trái sang phải**; các ảnh đính kèm không có khung 7. “Cắt sạch” nghĩa là vùng hiệu ứng không bị người/kiếm/hình nộm che. Điểm dự kiến tính cả việc chỉnh cỡ, điểm bám và nhịp; chỉ cắt nhỏ thêm sẽ không đủ. Nhịp gốc chỉ suy được từ thứ tự tư thế vì ảnh tham khảo không ghi thời lượng.

**0 · Nút 1 thường — Đánh thường: 5/10 → 7/10**

- **Giống/khác:** Có đủ ba nhát và bổ đất cuối. Tuy nhiên, vệt ngang rộng ở khung 3 gốc biến thành những cung nghiêng/dọc khá rời rạc. Ở ×2, một số mảng sáng bay trên đầu khi kiếm còn thấp, rõ ở khoảng **0,12 và 1,02 giây**. Màu trong game thiên hồng cam, thiếu viền đỏ sẫm và những nét lửa mảnh. Dáng giơ–chém–bổ có, nhưng lực thân và hướng kiếm chưa khớp hình vệt.
- **Chọn B:** Thêm ba bộ riêng: **vệt ngang khung 3, vệt chém ngược khung 4, cung bổ và chạm đất khung 5**; tách thân đỏ, lõi trắng, đuôi và tia lửa. Không có khung nào cắt sạch **toàn bộ** vệt: chỉ các đoạn ngoài người ở **3–5** dùng được. **1, 2, 6** không có hiệu ứng chính đứng riêng.
- **Khó:** Kiếm trùng màu với lõi vệt; xóa kiếm dễ làm thủng cung. Đuôi mảnh dễ mất khi khử nền navy. Cần thay bớt cung dựng hiện tại để tránh chồng sáng và tăng chi phí mobile.

**1 · Nút 1 ↑ — Đánh thường hất tung: 6/10 → 8/10**

- **Giống/khác:** Đúng lưỡi liềm dựng và động tác bật lên. Nhưng game hiện một hình trăng lớn rồi trôi/mờ; gốc diễn tiến từ **quệt đất → quét lên → vượt đầu → thu vệt**. ×2 làm cung lớn trong khi độ bật người giữ nguyên, nên kiếm không dẫn được cả cung. Cung cắt đỏ trắng khá đúng; cung procedural đi kèm nhạt và tạo cảm giác hai vệt khác nhau.
- **Chọn C:** Dùng các phần nhìn thấy ở **2→3→4→5** làm chuỗi biến đổi; tách chân vệt sát đất, thân cung và đuôi trên cao. **Không khung nào trong 2–5 sạch toàn cung**: người/kiếm đều che; phần cung ngoài bên phải cắt được. **1, 6** không có vệt độc lập đáng lấy.
- **Khó:** Phải phục hồi đoạn sau kiếm để đổi khung không bị khuyết/giật hình. Giữ chung mốc gốc cung; không căn giữa từng ảnh theo vùng cắt. Flipbook chỉ vài khung, nhưng không cần thêm quầng lớn trên tất cả khung.

**2 · Nút 1 ↓ — Đánh thường áp sát: 5/10 → 7/10**

- **Giống/khác:** Có lướt thấp, vệt tốc độ và bụi dừng. Khác lớn nhất là **khung 4 gốc chém vòng ngang tới trước**, còn game dùng thêm lưỡi liềm dọc của chiêu hất tung — xác nhận tại [RedSkills.cs](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Combat/Heroes/RedSkills.cs:361). ×2 khiến cung dựng cao trên một nhân vật đang nằm rất thấp. Thiếu bước chống tay/trượt dừng ở khung 5; các vệt đỏ giống dây sáng hơn là luồng áo–lửa dày.
- **Chọn B:** Thêm vệt ngang riêng từ **khung 4**, các dải tốc độ từ **2–3**, bụi sau gót từ **5**. Những vùng này có đoạn sạch; **không có toàn hiệu ứng sạch ở 2–5** vì thân/áo/kiếm xen vào. **1, 6** không có hiệu ứng độc lập.
- **Khó:** Áo choàng và vệt tốc độ cùng màu, dễ cắt lẫn. Bụi xám dễ mang nền navy. Phát quá nhiều dải trong từng frame sẽ vừa rối vừa tốn mobile.

**3 · Nút 2 thường — Kĩ năng xoay: 5/10 → 8/10**

- **Giống/khác:** Có tụ lực và nhiều vòng quanh thân. Game trông như các vòng elip hồng cam khá đều; gốc có vòng đỏ trắng sắc, méo theo nhát kiếm, xen mảng tối và đá. Dáng xoay ít đọc được dưới các vòng lớn ×2. **Ảnh dừng ở 0,90 giây nên chưa đủ kết luận thiếu nổ tinh thể cuối**: mã có phát `crystal_ring` sau hai nhịp xoay.
- **Chọn C:** Chuỗi **2→3→4** cho vòng chém phát triển, rồi **5** cho nổ. Tách nửa vòng trước/sau người, lõi, lửa và đá. **Không khung 1–5 nào sạch toàn bộ hiệu ứng**; lấy sét ngoài người ở **1**, cung ngoài ở **2–4**, tinh thể/đá quanh rìa ở **5**. **6** không có mảng chính đáng lấy.
- **Khó:** Che khuất rất nhiều; `crystal_ring` hiện có vùng vá người ở giữa. Cần giữ RED đọc được qua lớp trước/sau. Đây là chiêu dễ tăng overdraw nhất khi thêm nhiều vòng trong suốt.

**4 · Nút 2 ↑ — Kĩ năng tầm xa: 7/10 → 8/10**

- **Giống/khác:** Sóng bay và nổ tinh thể nhận diện đúng, màu đỏ trắng gần gốc hơn các cung procedural. Dáng giơ kiếm–đâm tới cũng đúng ý. Thiếu chuyển tiếp **cung bung ngay đầu kiếm ở khung 3** sang sóng khung 4; ở ×2, đầu sóng cao hơn đường kiếm, dễ thấy như vật bay riêng. Hình sóng gần như giữ nguyên suốt đường bay.
- **Chọn B:** **Khung 4: sóng sạch; khung 5: vụ nổ sạch**, tránh lấn hình bên cạnh. **Khung 3** lấy được phần cung phía trước, nhưng gốc nối kiếm không sạch. **1, 2, 6** không có mảng hiệu ứng chính đứng riêng. Thêm cung phóng; tách đầu trắng, thân đỏ, đuôi và đá nổ.
- **Khó:** Đuôi sóng có thể lộ cạnh cắt thẳng; quầng dễ ngả xanh khi khử nền. Không nên ghép 3→4→5 thành flipbook cùng một vị trí: chúng là các giai đoạn có điểm bám khác nhau.

**5 · Nút 2 ↓ — Kĩ năng thủ: 7/10 → 8/10**

- **Giống/khác:** Vòng phép, cột sáng và pha lê đúng bộ hình gốc; quá trình cắm kiếm rồi mở vòng đọc được. Nhưng ×2 làm vòng rộng, cột cao trong khi RED nhỏ ở giữa. Gốc có luồng lửa quấn quanh thân và mật độ tăng dần; game chủ yếu là vòng ổn định với cột/hạt xuất hiện rời nhau.
- **Chọn B:** Giữ vòng hiện có; thêm vết nứt **1–2**, các đoạn lửa xoắn **4–5**, phân lớp vòng sau/trước. **Không vòng hoàn chỉnh nào ở 3–6 sạch**, vì chân và kiếm che. Cột và pha lê đứng riêng ở **4–6** cắt sạch được; **1–2** chỉ lấy nứt đất hai bên, **3** lấy cung vòng ngoài.
- **Khó:** Vá vòng bằng đối xứng có thể sai ký tự phép; xoay toàn vòng elip sẽ sai phối cảnh. Nhiều cột cộng sáng cùng lúc làm mất nét vòng và tăng overdraw. Đây là chiêu cần bổ sung ít nhất.

**6 · Nút 3 thường — Kĩ năng đặc biệt: 6/10 → 8/10**

- **Giống/khác:** Có aura, lao chém, chữ X và đá; X là dấu hiệu giống rõ nhất. Tuy nhiên hai nhát mở đầu còn rời nhau, aura giống cột lửa thẳng hơn khối lửa ôm người. Gốc chuyển từ cung lớn sang hai đường chéo rồi bùng đá–xoáy; game nổi bật chủ yếu ở ảnh X lớn được giữ lại. Dáng lao chưa tạo cảm giác xuyên qua đòn.
- **Chọn C:** Chuỗi hình từ **2→3→4**, sau đó lớp xoáy/đá **5**. **Không khung 1–5 nào sạch toàn hiệu ứng**: aura ôm người, các cung chồng kiếm, X và xoáy chồng RED. Cắt được dải lửa ngoài ở **1**, cung ngoài **2–3**, nhánh X phía trên/phải và đá ở **4**, cung/đá ngoài ở **5**. **6** không có mảng chính.
- **Khó:** `x_slash` có **lỗ/vết vá tại vị trí RED cũ**, thấy trong bảng mảnh cắt. Tách hai nhánh X và đá giúp tránh phóng to cả vết vá. Các lớp lớn rất dễ che nhân vật, số sát thương và mục tiêu.

**7 · Nút 3 ↑ — Kĩ năng đặc biệt tầm xa: 7/10 → 8/10**

- **Giống/khác:** Sóng có lõi trắng, lửa đỏ đen và vụ nổ cao đúng ngôn ngữ gốc. Nhưng trong hàng ×2, khoảng **0,80 giây** đã thấy sóng và nổ dồn sát nhau; đường bay khó đọc. Nguyên nhân có thể kiểm chứng trong mã: cỡ sóng/vùng trúng tăng, tốc độ cũng tăng theo tầm, nên mục tiêu gần kích nổ sớm. Đây không tự động là lỗi thời gian.
- **Chọn B:** **Khung 4 và 5 sạch** cho sóng và nổ. **Khung 3** có vùng cung phóng phía phải sạch, nhưng phần nối kiếm không sạch toàn bộ. **1, 2, 6** không có hiệu ứng chính hoàn chỉnh đứng riêng. Tách cung phóng, lõi tia, đuôi đỏ đen, đá và cột nổ.
- **Khó:** `wave_big_launch` có **mép trái gần thẳng đứng**; `wave_big` cũng dễ lộ biên đuôi như một tấm ảnh. ×2 phóng đại nhược điểm này. Không kéo dài sóng bằng cách giãn toàn sprite; cần điều khiển đuôi riêng.

**8 · Nút 3 ↓ — Kĩ năng đặc biệt áp chế: 6/10 → 8/10**

- **Giống/khác:** Đúng chuỗi phóng xích–kéo–đánh–giam; xích có mắt rõ. Nhưng ×2 làm ba dây thành một bó sáng dày. Đòn đấm hiện thành chớp tròn lớn, khó đọc tư thế tay. Gốc có vòng xích trên đầu và thân bị treo rất rõ; game lồng chồng sát RED, nét quấn quanh mục tiêu chưa rõ chiều trước/sau. Lồng được giữ vừa mục tiêu trong mã, không nhân đôi toàn bộ.
- **Chọn B:** **Khung 2** cắt sạch các đoạn xích giữa tay và hình nộm. **5–6** lấy sạch được vương miện/vòng trên đầu và gai ngoài; **toàn lồng không sạch** vì chứa hình nộm. **1, 3, 4** chỉ có các đoạn hiệu ứng ngoài thân, không có mảng chính sạch hoàn chỉnh. Thêm xích quấn trước/sau, vòng đỉnh, vòng đáy, gai và chớp đấm riêng.
- **Khó:** Xóa hình nộm bằng ngưỡng màu dễ mất mắt xích tối hoặc để bóng người. Lồng bám mục tiêu cần giữ đáy đúng chân; co giãn quá mức làm mắt xích méo. Dùng vài dải xích lát lặp sẽ nhẹ hơn nhiều sprite mắt xích riêng.

**Các lỗi thị giác đáng xử lý**

- **Rời kiếm ở ×2:** rõ nhất #0, #1, #2. [Hàm `Spot`/`Lift`](/Users/apple/StudioProjects/octopus/Assets/_Project/Scripts/Runtime/Combat/Heroes/RedSkills.cs:140) nâng điểm hiệu ứng theo cỡ chiêu; mũi kiếm thật không được nâng tương ứng. Đây là lỗi **đặt điểm bám**, chưa phải bằng chứng pivot import sai.
- **Sai hình vệt:** #2 dùng vệt dọc của hất tung cho nhát chém ngang.
- **Vết xóa người:** `x_slash` có lỗ tối; `crystal_ring` có vùng giữa mờ, thiếu hình liên tục. Script đang [đục và lấp bằng màu loang](/Users/apple/StudioProjects/octopus/Tools/art/hero_fx_red.py:260), nên không phục hồi được nét gốc bị che.
- **Biên cắt:** dễ thấy ở đuôi sóng lớn/cung phóng #7; cần sửa đường biên, không chỉ tăng feather.
- **Màu và cỡ:** cung procedural thường hồng cam, mềm hơn đỏ trắng sắc của sprite gốc. ×2 quá lớn tương đối ở #0–3; không nên áp một hệ số thị giác cho mọi chiêu.
- **Ô chữ nhật đặc:** chưa thấy bằng chứng rõ trong ảnh capture. Có cạnh cắt thẳng không đồng nghĩa còn nguyên hình chữ nhật nền navy.

| Chiêu | Điểm hiện tại | Đề xuất | Điểm dự kiến | Ưu tiên |
|---|---:|---|---:|---|
| #0 Đánh thường | 5 | B — ba bộ vệt riêng | 7 | Cao |
| #1 Hất tung | 6 | C — chuỗi cung + lớp | 8 | Trung |
| #2 Áp sát | 5 | B — vệt ngang riêng | 7 | Cao |
| #3 Xoay | 5 | C — chuỗi vòng + lớp | 8 | Cao |
| #4 Tầm xa | 7 | B — cung phóng, đầu/đuôi | 8 | Trung |
| #5 Thủ | 7 | B — bổ sung lửa, nứt | 8 | Thấp |
| #6 Đặc biệt | 6 | C — cung→X, đá riêng | 8 | Cao |
| #7 Đặc biệt tầm xa | 7 | B — lõi, đuôi, đá | 8 | Trung |
| #8 Áp chế | 6 | B — xích/lồng trước–sau | 8 | Trung |

**Năm ưu tiên cho RED**

1. **Sửa điểm bám và cỡ ×2:** cho vệt phát từ đường kiếm thật; chỉnh riêng từng chiêu.
2. **Làm vệt đúng hình cho #0 và #2:** ngang, chém ngược, bổ; bỏ việc dùng chung lưỡi liềm dọc.
3. **Sửa chất lượng alpha và vùng bị che:** ưu tiên `x_slash`, `crystal_ring`, biên sóng lớn.
4. **Làm lại tiến trình xoay #3:** một vòng → nhiều vòng → nổ, có lớp trước/sau và khoảng nhìn thấy RED.
5. **Chỉnh dáng và chuyển pha #6/#8:** đọc được lao chém, giao X, đấm và treo mục tiêu; đồng bộ chúng với thời điểm hiệu ứng.