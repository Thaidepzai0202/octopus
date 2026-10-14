# Hướng dẫn xem và chỉnh khung xương bạch tuộc (cho người mới dùng Unity)

## Bước 0 — Mở đúng chỗ

1. Mở **Unity Hub** → bấm vào project **octopus**. Chờ Unity mở xong.
2. Nếu Unity hỏi *"scene đã thay đổi bên ngoài, Reload?"* → bấm **Reload**.
3. Ở khung **Project** (phía dưới màn hình) vào `Assets → _Project → Scenes`, **bấm đúp** vào `Playground`.

Màn hình Unity gồm 5 khu vực chính:

```
┌──────────────── nút ▶ Play / ⏸ / ⏭ (giữa, trên cùng) ────────────────┐
│ Hierarchy  │        Scene  |  Game  (2 thẻ)        │  Inspector       │
│ (danh sách │   Scene = bàn làm việc, chỉnh đồ      │  (thông số của   │
│ mọi vật    │   Game  = màn hình như trên điện thoại │  vật đang chọn)  │
│ trong màn) │                                       │                  │
├────────────┴───────────────────────────────────────┴──────────────────┤
│ Project (các file: ảnh, script, scene…)  |  Console (thông báo, lỗi)   │
└────────────────────────────────────────────────────────────────────────┘
```

Thao tác trong khung **Scene**:

| Muốn | Làm |
|---|---|
| Phóng to / thu nhỏ | Cuộn chuột, hoặc vuốt 2 ngón lên / xuống trên trackpad |
| Kéo khung nhìn | Bấm phím **Q** (bàn tay) rồi kéo chuột |
| Nhảy tới vật đang chọn | Rê chuột vào Scene rồi bấm **F** |
| Hoàn tác | **Cmd + Z** |
| Lưu | **Cmd + S** |

## Bước 1 — Tìm bạch tuộc

1. Ở khung **Hierarchy** bấm vào **mũi tên ▸** cạnh dòng `Octopus` để mở ra.
2. Bấm chọn dòng **`Rig Visual`**.
3. Rê chuột vào khung **Scene**, bấm **F**: Scene phóng tới con bạch tuộc.

Bên phải, **Inspector** hiện thông số của `Octopus Rig Animator`. Trên cùng có khung giải thích, nút xanh **▶ Chạy thử trong Scene**, và 4 nút **Đầu / Xúc tu trái / Xúc tu giữa / Xúc tu phải**.

## Bước 2 — Nhìn thấy khung xương

Khung xương được vẽ thẳng lên con bạch tuộc trong Scene:

| Thấy gì | Nghĩa là |
|---|---|
| Đường **vàng → đỏ** có vòng tròn nhỏ | Xương xúc tu, vàng ở gốc, đỏ ở chóp. Mỗi vòng tròn là một khớp (14 xương mỗi xúc tu) |
| Đường **xanh dương** dọc đầu | 3 xương của đầu |
| Chấm **xanh lá** dưới chóp xúc tu | Chóp đang **bám đất** (đứng yên) |
| Chấm **cam** | Chóp đang **nhấc lên** đưa tới |
| Dấu **+ trắng** | Chỗ code muốn đặt chóp xúc tu (đích IK) |
| Vòng **vàng** | Chỗ xúc tu đang nắm đá (khi đu) |

**Không thấy xương?**
- Trên thanh nhỏ phía trên khung Scene, nút **Gizmos** (hình quả cầu) phải đang sáng.
- Trong Inspector, ô **Show Bones** phải có dấu tick.

## Bước 3 — Xem bạch tuộc chuyển động mà không cần Play

1. Trong Inspector tìm ô **Preview**, bấm vào chọn **Crawl** (bò).
2. Bấm nút xanh **▶ Chạy thử trong Scene**. Bạch tuộc bò tại chỗ ngay trong Scene, thấy rõ chấm xanh lá / cam đổi theo nhịp.
3. Bấm **■ Dừng chạy thử** để dừng.

Các dáng khác trong ô Preview:

| Dáng | Nghĩa |
|---|---|
| Idle | Đứng yên, thở |
| Crawl | Bò |
| Jump | Đang bay lên |
| Fall | Rơi nhanh |
| Swing | Đu bằng xúc tu |
| Cling | Bám dưới đá |

Khi không chạy thử, kéo thanh **Preview Time** để xem từng khoảnh khắc. Kéo hết sang phải thì mắt nhắm (để xem mí mắt).

> Sau khi chạy thử, tên scene có thể có dấu `*` (có thay đổi). Bình thường thôi, lưu hay không đều được.

## Bước 4 — Chỉnh thông số

- **Rê chuột lên tên một ô** để hiện giải thích tiếng Việt.
- **Sửa số:** bấm vào ô số, gõ số mới, Enter. Hoặc giữ chuột trên **tên** ô rồi kéo sang trái / phải để tăng giảm từ từ.
- Mục có **mũi tên ▸** (như **Gait**) thì bấm mũi tên để mở ra.
- Nên bật **▶ Chạy thử** trong lúc chỉnh để thấy kết quả ngay.

Mục **Gait** là nhịp bò của từng xúc tu, gồm 3 dòng (khi bạch tuộc quay mặt sang phải):

| Dòng | Xúc tu | Vai trò |
|---|---|---|
| Element 0 | trái | phía sau, **đẩy** thân |
| Element 1 | giữa | **lăn** dưới thân |
| Element 2 | phải | phía trước, **vươn tới rồi kéo** thân |

Mỗi dòng có các ô sau:

| Ô | Nghĩa |
|---|---|
| Phase | Lệch nhịp so với hai xúc tu kia |
| Grip | Bao nhiêu phần nhịp bám đất (còn lại là nhấc lên) |
| Plant Ahead | Đặt chóp xa phía trước bao nhiêu |
| Lift | Nhấc chóp cao bao nhiêu |
| Grip Curl | Chóp cuộn bao nhiêu khi bám (1 = như tranh, nhỏ hơn = duỗi, lớn hơn = cuộn chặt) |
| Swing Curl | Chóp cuộn bao nhiêu khi đưa tới |

**Ví dụ chỉnh thử** (các ô Gait, Surge, Mantle Trail/Surge chỉ có tác dụng khi **bỏ tick Use Crawl Clip**; đang dùng clip thì chỉnh dáng bò trong cửa sổ Animation, xem phần **Chỉnh dáng bò** cuối file):

| Muốn | Ô cần sửa | Từ → thành |
|---|---|---|
| Bạch tuộc to hơn | **Size** | 1.5 → 1.8 |
| Bò sải dài, chậm rãi hơn | **Cycle Distance** | 1 → 1.4 |
| Xúc tu trước vươn xa hơn | **Gait › Element 2 › Plant Ahead** | 0.3 → 0.45 |
| Nhấc xúc tu cao hơn khi bò | **Gait › Element 0/1/2 › Lift** | tăng lên |
| Chóp xúc tu mềm, vẫy nhiều | **Tip Stiffness** 12 → 8 và **Damping** 0.65 → 0.45 | |
| Đầu lắc lư, ngả sau nhiều hơn | **Mantle Stiffness** 10 → 6 và **Mantle Trail** 8 → 14 | |
| Thân chồm mạnh hơn mỗi nhịp kéo | **Surge** | 0.05 → 0.08 |
| Chớp mắt thưa hơn | **Blink Interval** | X = 2, Y = 5 → 4 và 8 |

Chỉnh xong bấm **Cmd + S** để lưu. Lỡ tay thì **Cmd + Z**.

## Bước 5 — Thử trong game (Play)

1. Bấm nút **▶** ở giữa phía trên. Unity chuyển sang khung **Game**.
2. Điều khiển bằng chuột trong khung Game:

| Hành động | Chuột |
|---|---|
| Nhảy | Bấm nhanh |
| Bò | Bấm giữ rồi kéo ngang |
| Bắn xúc tu đu | Bấm vào tảng đá trong vòng tròn mờ |
| Rút lên đá | Đang đu thì giữ chuột |
| Buông | Đang đu thì bấm nhanh |

3. Bấm **▶** lần nữa để dừng.

> ⚠️ **Quan trọng:** mọi thứ chỉnh **trong lúc đang Play sẽ mất khi dừng Play** (lúc đó màn hình Unity hơi ngả màu để nhắc).
> Muốn giữ con số vừa chỉnh khi đang Play:
> 1. Bấm dấu **⋮** ở góc phải dòng tiêu đề `Octopus Rig Animator` → **Copy Component**.
> 2. Dừng Play.
> 3. Bấm **⋮** lần nữa → **Paste Component Values**.
>
> Cách dễ hơn: chỉnh lúc **không Play**, dùng nút ▶ Chạy thử ở Bước 3.

Muốn xem cả xương khi đang Play: trên thanh khung Game bấm nút **Gizmos**, hoặc kéo thẻ **Scene** ra đặt cạnh thẻ Game.

## Bước 6 — Tự tay xoay xương xem ảnh uốn thế nào

1. Chọn `Rig Visual`, **bỏ tick** ô **Drive Bones** (code thôi điều khiển xương).
2. Trong Hierarchy mở lần lượt `Rig Visual › Pose › Torso`. Bên trong:
   - `MANTLE_00` là xương đầu.
   - `L_00`, `M_00`, `R_00` là gốc 3 xúc tu. Mỗi xương nằm bên trong xương trước nó: `R_00 › R_01 › R_02 …`
3. Chọn một xương, ví dụ `R_05`. Rê chuột vào Scene, bấm **E** (công cụ xoay), kéo vòng tròn quanh xương. Phần xúc tu từ xương đó trở ra uốn theo.
   Cách khác: trong Inspector, ô **Rotation → Z**, gõ số độ.
4. Xong thì **tick lại Drive Bones**. Code sẽ điều khiển lại, dáng tay vừa xoay tự mất (không sao cả).

## Bước 7 — Sửa vị trí xương và trọng số (Skinning Editor)

**Trọng số** là mỗi phần ảnh đi theo xương nào, nhiều hay ít. Nó quyết định ảnh uốn mượt hay bị gãy.

1. Chọn `Rig Visual`, bấm nút **Xúc tu giữa** (hoặc Đầu / trái / phải) ở đầu Inspector. Cửa sổ **Sprite Editor** mở ra.
   Nếu không tự mở: trong khung Project bấm vào ảnh `Assets/_Project/Art/Octopus/Rig/tentacle_m.png`, rồi vào menu **Window → 2D → Sprite Editor**.
2. Ở **góc trên bên trái** cửa sổ có ô chữ **Sprite Editor**. Bấm vào, chọn **Skinning Editor**.
3. Bạn sẽ thấy ảnh xúc tu với 14 xương. Thanh công cụ dọc bên trái có các nút sau (rê chuột lên nút để xem tên):

| Công cụ | Dùng để | An toàn? |
|---|---|---|
| **Preview Pose** | Kéo xoay thử xương xem ảnh uốn thế nào. **Không** làm thay đổi gì; bấm **Reset Pose** để trả về | ✅ Thử thoải mái |
| **Edit Bone** | Kéo khớp xương sang chỗ khác (dời trục xương) | Cẩn thận |
| **Weight Brush** | Tô trọng số: chọn một xương trong danh sách (nút **Visibility** mở danh sách xương), rồi tô lên vùng ảnh muốn đi theo xương đó | Cẩn thận |
| **Auto Weights → Generate** | Unity tự tính lại trọng số | ⚠️ Xoá trọng số mình đã tính sẵn |
| Create Bone / Split Bone | Thêm xương | ⚠️ Sau đó phải làm mục 6 dưới đây |

4. Ưng ý thì bấm **Apply** ở **góc trên bên phải** cửa sổ. Chưa ưng thì bấm **Revert** để bỏ hết.
5. Quay lại Scene, bấm **▶ Chạy thử** để xem kết quả. Code tự đọc dáng xương mới, không cần làm gì thêm.
6. Nếu **thêm hoặc bớt** xương: vào menu trên cùng **Octopus → Dựng lại bạch tuộc (giữ map)**, rồi **Cmd + S**.

**Lỡ làm hỏng?** Nhắn mình. Hoặc chạy lại 2 lệnh tách ảnh trong README (mục "Bạch tuộc gắn xương"), rồi chọn **Octopus → Dựng lại bạch tuộc (giữ map)**: mọi thứ về như ban đầu.

## Bước 8 — Hai menu "Dựng lại" khác nhau

| Menu | Làm gì | Giữ thông số bạn đã chỉnh? |
|---|---|---|
| **Octopus → Dựng lại bạch tuộc (giữ map)** | Chỉ dựng lại con bạch tuộc | ✅ Giữ |
| **Octopus → Dựng lại màn chơi (ghi đè scene)** | Dựng lại cả map lẫn bạch tuộc | ❌ Mọi thông số về mặc định |

## Gặp trục trặc

| Hiện tượng | Cách xử lý |
|---|---|
| Không thấy nút ▶ Chạy thử | Phải đang **không Play**, và đang chọn đúng dòng `Rig Visual` |
| Bấm chạy thử mà không cử động | Kiểm tra ô **Drive Bones** có tick không |
| Không thấy xương | Nút **Gizmos** trên khung Scene phải sáng; ô **Show Bones** phải tick |
| Khung Console hiện chữ đỏ | Chụp màn hình gửi mình |
| Muốn đưa bản đã chỉnh lên iPhone | Nhắn mình, hoặc menu **Octopus → Build iOS** rồi làm như lần trước |

---

# Chỉnh dáng bò

Có **2 cách**, chọn bằng ô **Use Crawl Clip** trên `Rig Visual`:

| | A. Bằng tay — clip `Crawl.anim` (đang bật) | B. Bằng thông số — code tự tính |
|---|---|---|
| Chỉnh ở đâu | Cửa sổ **Animation**: đặt keyframe cho tay cầm | Inspector, mục **Bò bằng code** (Gait…) |
| Hợp với | Muốn tự tay canh từng tư thế cho đẹp | Muốn đổi nhanh cả kiểu bò bằng vài con số |

Clip ban đầu được tạo **giống hệt** dáng bò bằng code, nên bạn sửa tiếp từ đó chứ không phải làm từ đầu.

## A. Chỉnh bằng tay trong cửa sổ Animation

### Ý tưởng: tay cầm (controller)
Bạn **không** xoay 14 xương của từng xúc tu. Mỗi xúc tu có **một tay cầm ở chóp**: chóp xúc tu luôn chạy tới chỗ tay cầm, IK tự uốn cả xúc tu theo.

Trong `Rig Visual › Pose › Crawl` có 4 tay cầm:

| Tay cầm | Trong Scene | Nó điều khiển |
|---|---|---|
| **Foot L** | chấm **xanh lá** bên trái | Chóp xúc tu trái (phía sau) |
| **Foot M** | chấm xanh lá ở giữa | Chóp xúc tu giữa |
| **Foot R** | chấm xanh lá bên phải | Chóp xúc tu phải (phía trước) |
| **Body** | chấm **xanh dương** | Thân chồm tới / nhấp nhô; **Mantle** = đầu ngả sau bao nhiêu độ |

Mỗi tay cầm chân có thêm 2 số:
- **Curl:** chóp cuộn bao nhiêu (1 = như tranh, nhỏ hơn = duỗi thẳng, lớn hơn = cuộn chặt).
- **Contact:** 1 = **đang bám đất**, 0 = **đang nhấc lên**. Lúc Contact = 1, game giữ chóp đứng yên hẳn trên đất (khoá chân) để chân không trượt.

Hệ toạ độ của tay cầm:
- **Position Y = 0** là mặt đất.
- **Position X dương** là phía trước (khi bạch tuộc quay mặt sang phải).

Mặt đất chuyển động sẵn trong clip: một vòng bò dài **1 giây** (0:00 → 1:00), lặp mãi. Trong game, clip tự chạy nhanh hay chậm theo tốc độ bò. Thân đi được **Cycle Distance** thì clip chạy hết 1 vòng.

### Bước 1 — Mở cửa sổ Animation
1. Chọn `Octopus › Rig Visual` trong Hierarchy.
2. Trong Inspector bấm **Mở cửa sổ Animation để chỉnh dáng bò** (hoặc menu **Window → Animation → Animation**, phím tắt **Cmd + 6**).
3. Cửa sổ mới hiện ra. Kéo **tab** của nó thả xuống khu vực dưới cùng (cạnh Project) để vừa thấy Scene vừa thấy dòng thời gian.

Cửa sổ Animation gồm:
```
┌────────────────────────────┬──────────────────────────────────────────┐
│ ● Record  ▶ Play  [  20  ] │ 0:00   0:10   0:20   0:30 …  1:00 ← thước │
│ Crawl ▾                    │    ◆        ◆            ◆        ◆      │
├────────────────────────────┤        │ ← vạch thời gian (kéo được)    │
│ ▸ Body : Position          │  ◆           ◆           ◆           ◆   │
│   Body : Octopus Crawl Body.Mantle      ◆ = keyframe (tư thế mốc)    │
│ ▸ Foot L : Position        │                                          │
│   Foot L : …Curl, …Contact │                                          │
│ ▸ Foot M … ▸ Foot R …      │                                          │
├────────────────────────────┴──────────────────────────────────────────┤
│  Dopesheet | Curves   ← 2 cách xem, nút ở dưới cùng                    │
└────────────────────────────────────────────────────────────────────────┘
```

### Bước 2 — Xem clip
- **Bấm hoặc kéo trên thước thời gian:** bạch tuộc trong Scene đổi dáng theo đúng thời điểm đó.
- **Bấm ▶ trong cửa sổ Animation** để xem bò lặp liên tục (bò tại chỗ). Bấm lần nữa để dừng.
- Khi xem xong, bấm nút **Preview** (hoặc đóng cửa sổ Animation) để bạch tuộc trở về như cũ.

### Bước 3 — Sửa vị trí chóp xúc tu (việc làm nhiều nhất)
1. Kéo vạch thời gian tới frame muốn sửa, vd **20**. Ô số cạnh nút ▶ cho biết frame hiện tại; gõ số vào đó cũng được.
2. Bấm nút **● Record**. Dòng thời gian và các ô số chuyển **màu đỏ**: mọi thay đổi bây giờ đều thành keyframe.
3. Trong Scene **bấm vào chấm xanh lá** của chóp cần sửa. Hoặc chọn `Rig Visual › Pose › Crawl › Foot R` trong Hierarchy.
4. Bấm **W** (công cụ di chuyển) rồi **kéo mũi tên** đỏ (ngang) hoặc xanh (dọc). Xúc tu uốn theo ngay, và trong cửa sổ Animation hiện thêm ◆ ở frame 20.
5. Sửa xong bấm **● Record** lần nữa để tắt.

### Bước 4 — Sửa Curl, Contact, Mantle
Cũng bật **● Record** như trên, rồi chọn cách tiện:
- Chọn tay cầm, ví dụ **Foot R**. Trong Inspector, mục **Octopus Crawl Foot**, gõ số mới vào **Curl** hoặc **Contact**.
- Hoặc gõ thẳng vào ô số bên phải tên thuộc tính trong danh sách bên trái cửa sổ Animation.
- **Mantle** nằm trên tay cầm **Body**.

### Bước 5 — Dời, xoá, thêm keyframe
| Muốn | Làm |
|---|---|
| Đổi thời điểm của một tư thế | Kéo hình thoi **◆** sang trái / phải |
| Xoá keyframe | Bấm chọn ◆ → phím **Delete** |
| Thêm keyframe tại vạch thời gian | Chuột phải lên dòng thuộc tính → **Add Key** |
| Chọn nhiều ◆ | Kéo khung chữ nhật quanh chúng |

### Bước 6 — Chỉnh cho mượt (Curves)
1. Bấm **Curves** ở dưới cùng cửa sổ Animation. Mỗi thuộc tính là một đường cong theo thời gian.
2. Bấm tên thuộc tính bên trái để chỉ xem đường đó. Rê chuột vào vùng đường cong, bấm **F** để thấy trọn đường.
3. Kéo điểm ◆ để đổi giá trị. Bấm vào ◆, hai **tay nắm tiếp tuyến** hiện ra: kéo chúng để chuyển động nhanh / chậm dần.
4. Chuột phải lên ◆ để đổi kiểu:
   - **Clamped Auto:** tự mượt.
   - **Broken:** hai bên gãy góc.
   - **Constant:** giữ nguyên tới ◆ sau. Clip làm sẵn dùng kiểu này cho Contact.

### Bước 7 — Lưu và thử
- Bấm **Cmd + S**. Game dùng clip mới ngay, không cần bấm gì thêm.
- Thử nhanh không cần Play: Inspector của Rig Visual → **Preview = Crawl** → **▶ Chạy thử trong Scene**. Cách này có khoá chân như trong game.
- Thử thật: bấm **▶ Play**, giữ chuột kéo ngang trong khung Game để bò.

### Quy tắc để đẹp và không lỗi
1. **Frame 0:00 và 1:00 phải giống nhau.** Đó là chỗ vòng sau nối vào vòng trước; khác nhau thì xúc tu giật mỗi vòng. Sửa ◆ ở 0:00 thì sửa cả ◆ ở 1:00.
2. **Lúc Contact = 1 (đang bám), giữ Position Y sát 0** (chóp nằm trên đất). Khoá chân sẽ giữ chóp đứng yên theo chiều ngang.
3. **Đổi Contact từ 1 sang 0 đúng lúc chóp bắt đầu nhấc lên**, và từ 0 về 1 đúng lúc chóp chạm đất lại.
4. **Không xoay xương** (L_00, R_05…) khi đang Record. Code sẽ ghi đè, và Inspector báo vàng "thuộc tính game không dùng". Lỡ làm thì chọn dòng đó trong cửa sổ Animation → Delete.
5. **Không với chóp quá xa thân.** Xúc tu chỉ duỗi dài tối đa **Arm Stretch Y** lần. Thấy **dấu + trắng nằm xa chóp xúc tu** là đang với quá tầm.
6. **Lỡ tay:** **Cmd + Z**. Hỏng nặng thì bấm **Tạo lại clip bò từ thông số "Bò bằng code"…** trong Inspector của Rig Visual. Clip quay về dáng bò bằng code (mất phần đã sửa tay).

### Thử ngay một chỉnh sửa (5 phút)
Mục tiêu: **xúc tu trước vươn xa hơn và nhấc cao hơn**.
1. Mở cửa sổ Animation, bấm **Curves**, bấm dòng **Foot R : Position › Position.x**.
2. Bấm **● Record**.
3. Chọn ◆ cao nhất của đường (lúc chóp vươn xa nhất), kéo lên thêm một chút: chóp đặt xa hơn.
4. Bấm dòng **Position.y**, kéo ◆ cao nhất lên: chóp nhấc cao hơn.
5. Tắt Record, bấm ▶ trong cửa sổ Animation xem thử, ưng thì **Cmd + S**.

## B. Chỉnh bằng thông số (bò bằng code)
Bỏ tick **Use Crawl Clip**, rồi sửa mục **Bò bằng code** trong Inspector. Muốn mang dáng đó sang clip để chỉnh tay tiếp: tick lại Use Crawl Clip → bấm **Tạo lại clip bò từ thông số "Bò bằng code"…**.

Một nhịp bò của một xúc tu:
```
 0 ─────────── Grip ────────────┬──────── phần còn lại ────────── 1
   chóp BÁM ĐẤT, đứng yên        │  chóp NHẤC LÊN (cao Lift), đưa
   (thân trôi qua nên chóp       │  về chỗ bám mới ở phía trước
    lùi dần về sau)              │  (Plant Ahead)
```

| Ô | Tác dụng | Mặc định |
|---|---|---|
| **Gait › Element 0 / 1 / 2** | Xúc tu trái (đẩy) / giữa (lăn) / phải (vươn kéo) | |
| ↳ Phase | Lệch nhịp (0–1) | 0.35 / 0.6 / 0 |
| ↳ Grip | Phần nhịp bám đất | 0.6 / 0.45 / 0.55 |
| ↳ Plant Ahead | Đặt chóp xa phía trước bao nhiêu khi vừa bám | 0.05 / 0.22 / 0.3 |
| ↳ Lift | Nhấc chóp cao bao nhiêu | 0.09 / 0.04 / 0.07 |
| ↳ Grip Curl / Swing Curl | Chóp cuộn khi bám / khi đưa tới | |
| **Surge**, **Crawl Bob** | Thân chồm tới, nhấp nhô | 0.05, 0.035 |
| **Mantle Trail**, **Mantle Surge** | Đầu ngả sau, lắc theo nhịp (độ) | 8, 5 |
| **Plant Side Tentacles** | Hai xúc tu bên hạ chóp chạm đất | 0.7 |

**Ô dùng cho cả hai cách** (mục **Bò** và **Lò xo**):

| Ô | Tác dụng |
|---|---|
| **Cycle Distance** | Thân đi bao xa mỗi vòng. Lớn = sải dài, nhịp chậm |
| **Arm Stretch** | Xúc tu co ngắn nhất / duỗi dài nhất |
| **Tip Stiffness**, **Damping** | Phần chóp xúc tu mềm / vẫy nhiều (giảm số) |
| **Mantle Stiffness** | Đầu mềm, lắc lư (giảm số) |
| Octopus › **Surface Mover › Crawl Speed** | Tốc độ bò thật trong game (đổi cả cách chơi) |

Muốn kiểu chuyển động mà cả hai cách đều không làm được (vd kéo lê cả thân, bò bằng 2 xúc tu): mô tả hoặc gửi video mẫu, mình sửa code.

---

# Chỉnh từng frame của vòng bò

## Hiểu dòng thời gian
- Clip dài **60 frame = 1 vòng bò**. **Frame 60 chính là frame 0** của vòng sau.
- Không phải 60 tấm ảnh vẽ sẵn. Chỉ những frame có **◆ (keyframe)** là tư thế bạn đặt; các frame ở giữa Unity tự tính chuyển tiếp. Muốn chỉnh frame nào thì biến frame đó thành keyframe.

## Bản đồ nhịp của clip gốc
1 ô = 2 frame. **█** = chóp đang bám đất (Contact = 1). **~** = chóp nhấc lên, đưa tới.
```
frame  0    10   20   30   40   50   60
Phải R █████████████████~~~~~~~~~~~~~█     bám 0→33 · nhấc lên 33 · cao nhất 46 · đặt xuống 60 (= 0)
Giữa M ~~~~~~~~~~~~██████████████~~~~~     đặt xuống 24 · bám 24→51 · nhấc lên 51 · cao nhất 8
Trái L ████████~~~~~~~~~~~~███████████     nhấc lên 15 · cao nhất 27 · đặt xuống 39 · bám 39→60→15
Thân   0 lùi nhất · 15 đầu ngả sau nhất · 30 chồm tới nhất + thấp nhất · 45 đầu ngả ít nhất
```
Lúc xem trong cửa sổ Animation, bạch tuộc **bò tại chỗ** (như chạy máy chạy bộ). Vì vậy chóp đang bám sẽ **trôi dần ra sau**. Đó là đúng: trong game thân tiến lên còn chóp đứng yên trên đất.

## Đi từng frame
| Muốn | Bấm |
|---|---|
| Tới frame bất kỳ | Gõ số vào **ô frame** (cạnh nút ▶), hoặc bấm lên thước thời gian |
| Lùi / tới **1 frame** | Phím **,** / **.** (dấu phẩy / dấu chấm) |
| Nhảy tới **keyframe** trước / sau | **Option + ,** / **Option + .**, hoặc 2 nút có hình ◆ cạnh nút ▶ |
| Về đầu / tới cuối | **Shift + ,** / **Shift + .** |

Muốn dùng phím tắt, bấm chuột vào cửa sổ Animation hoặc Scene trước.

## Chỉnh một frame (làm đi làm lại)
1. Chọn `Rig Visual`, mở cửa sổ **Animation**, để ở chế độ **Dopesheet** (nút dưới cùng).
2. Tới frame muốn sửa (ví dụ **46**).
3. Bấm **● Record**. Thanh thời gian chuyển đỏ.
4. **Kéo chóp:** trong Scene bấm chấm xanh lá của xúc tu (hoặc chọn `Pose › Crawl › Foot R` trong Hierarchy) → **W** → kéo mũi tên đỏ (ngang) hoặc xanh (dọc).
5. **Đổi độ cuộn / bám:** trong Inspector, mục **Octopus Crawl Foot**, gõ **Curl** hoặc **Contact**.
6. **Đổi thân / đầu:** chọn `Body` → kéo (chồm tới / nhấp nhô), hoặc gõ **Mantle** (đầu ngả sau).
7. Sang frame khác, lặp lại. Xong thì tắt **● Record**, bấm **▶** trong cửa sổ Animation xem cả vòng, ưng thì **Cmd + S**.

**Khoá cả tư thế ở một frame:** chọn cùng lúc cả 4 tay cầm (giữ **Cmd** bấm `Foot L`, `Foot M`, `Foot R`, `Body` trong Hierarchy). Trong cửa sổ Animation bấm nút **Add Keyframe** (◆ có dấu +), hoặc chuột phải lên thước thời gian → **Add Key**. Mọi thuộc tính có ◆ ở frame đó, nên chỉnh frame khác không làm dáng ở đây đổi theo.

## Ví dụ chỉnh theo frame
| Frame | Đang xảy ra | Thử chỉnh |
|---|---|---|
| **46** | Xúc tu phải nhấc cao nhất, đang vươn tới | Foot R: **Position Y** 0.11 → 0.18 (nhấc cao), **Curl** 0.55 → 0.35 (duỗi thẳng như đang dò đường) |
| **0 và 60** | Xúc tu phải đặt xuống | Foot R: **Position X** tăng thêm 0.1 ở **cả frame 0 và 60** (đặt xa hơn) |
| **27** | Xúc tu trái nhấc cao nhất | Foot L: **Curl** 1.4 → 1.7 (cuộn tròn lại khi thu về) |
| **8** | Xúc tu giữa đang đưa tới | Foot M: **Position Y** 0.04 → 0.08 |
| **30** | Thân chồm tới nhất | Body: **Position X** 0.05 → 0.08 |
| **15** | Đầu ngả sau nhất | Body: **Mantle** 13 → 18 |

## 4 quy tắc
1. **Frame 0 và frame 60 phải giống hệt nhau**, nếu không mỗi vòng sẽ giật một cái. Sửa ◆ ở 0 thì sửa cả ◆ ở 60.
2. **Đang bám (Contact = 1) thì Position Y giữ sát đất:** khoảng 0.04 với xúc tu trái / phải, 0 với xúc tu giữa.
3. **Contact đổi đúng lúc:** chóp vừa rời đất → **0**, vừa chạm đất lại → **1**.
4. **Đừng với quá xa thân.** Thấy dấu **+ trắng** nằm xa chóp xúc tu là đang với quá tầm.

## Xoá / sửa nhanh
| Muốn | Làm |
|---|---|
| Xoá một keyframe | Bấm chọn ◆ → **Delete** |
| Dời keyframe sang frame khác | Kéo ◆ sang trái / phải |
| Sửa chính xác con số của một keyframe | Tới đúng frame đó, gõ số vào ô bên phải tên thuộc tính (đang Record) |
| Hoàn tác | **Cmd + Z** |
| Làm lại từ đầu | Inspector của Rig Visual → **Tạo lại clip bò từ thông số "Bò bằng code"…** |

---

# ⚡ Cách nhanh: bảng "Chỉnh dáng bò"

Không cần bật Record, không cần chọn tay cầm, không cần bấm W.

## Mở bảng
- Menu trên cùng **Octopus → Chỉnh dáng bò từng frame**, hoặc
- Chọn `Rig Visual` → Inspector → nút vàng **⚡ Bảng chỉnh dáng bò nhanh (từng frame)**.

Kéo tab của bảng đặt cạnh Inspector cho tiện. Nhớ **tắt Preview của cửa sổ Animation** nếu đang mở (bảng sẽ nhắc).

## Trong cửa sổ Scene
Bảng mở là Scene hiện ngay:
- **3 vòng tròn ở chóp 3 xúc tu** (chữ *Trái / Giữa / Phải · bám / nhấc*). **Kéo** vòng tròn → chóp tới đó, keyframe được ghi luôn cho frame đang xem.
- **Ô vuông ở giữa thân** → kéo để thân chồm tới / lùi, nhấp nhô.
- **Vòng tròn lớn quanh thân** → kéo vòng để **xoay đầu ngả** ra sau / về trước.
- **Đường đi của chóp cả vòng:** **xanh lá** = đang bám, **cam** = đang nhấc. Mỗi frame một chấm nhỏ: chấm **thưa** là chóp đi **nhanh**, chấm **dày** là đi **chậm**. Nhìn đường này là biết chuyển động có mượt không.
- Đường ngang mờ là **mặt đất**.

## Trong bảng
```
|◀  ◀  Frame [46] / 60  ▶  ▶|            [▶ Chạy vòng]
Tốc độ chạy vòng ────●──── 1.0×
 0    10   20   30   40   50   60
Phải ███████████████~~~~~~~~~~~~~█   ← bấm / kéo trên dải để nhảy tới frame
Giữa ~~~~~~~~~~~██████████████~~~~      ▪ = keyframe, vạch trắng = frame đang xem
Trái ███████~~~~~~~~~~~~█████████████
Thân ▪──────▪──────▪──────▪──────▪
Xúc tu PHẢI (trước, vươn kéo)        [◆][✕]
 ◆ X (trước / sau)   ────●──── 0.27
 ◇ Y (cao)           ──●────── 0.11
 ◇ Cuộn              ───●───── 0.55
 ◆ Bám đất           ☐
 … Xúc tu GIỮA, TRÁI, THÂN …
[◆ Key cả tư thế]   [✕ Xoá key ở frame này]
[Sao chép tư thế]   [Dán tư thế vào frame này]
☑ Frame 0 và frame cuối luôn giống nhau
☑ Đang bám đất thì chóp dính đất
☑ Hiện đường đi của chóp trong Scene
[Lưu (Cmd+S)]  [Mở cửa sổ Animation]
```
- **◆** = frame này có keyframe. **◇** = chưa có, giá trị đang thấy do Unity tự tính. Kéo thanh trượt là thành ◆.
- **▶ Chạy vòng** để xem lặp. Kéo **Tốc độ** xuống 0.2× để xem chậm từng nhịp.
- **Phím , và .** (dấu phẩy, chấm) lùi / tới 1 frame. Bấm vào bảng hoặc Scene trước.
- **[◆]** cạnh tên một tay cầm: đặt key cho cả tay cầm đó ở frame này. **[✕]**: xoá key của tay cầm đó ở frame này.
- **Cmd + Z** hoàn tác được mọi thao tác, kể cả kéo trong Scene.
- Xong bấm **Lưu** (hoặc Cmd + S).

## Quy trình nhanh để chỉnh một đoạn
1. Bấm **▶ Chạy vòng** ở tốc độ 0.3×, tìm đoạn chưa ưng, bấm **■ Dừng**.
2. Dùng **, .** đi tới frame đó.
3. Kéo vòng tròn của xúc tu trong Scene (hoặc kéo thanh trượt), nhìn đường đi xanh / cam đổi theo.
4. Làm thêm 1–2 frame lân cận nếu cần, rồi **▶ Chạy vòng** xem lại.
5. Ưng thì **Lưu**.

Mẹo:
- Muốn **giữ nguyên dáng một frame** trong lúc sửa các frame bên cạnh, bấm **◆ Key cả tư thế** ở frame đó trước.
- Muốn **hai frame giống nhau** (vd tư thế lặp lại): đứng ở frame mẫu → **Sao chép tư thế** → sang frame kia → **Dán**.


---

# Sóng xúc tu, hạ đầu, vây quanh đầu

Chọn `Rig Visual`, xem Inspector. Bấm **▶ Chạy thử trong Scene** (Preview = Crawl) để thấy ngay khi kéo.

| Muốn | Ô | Mặc định |
|---|---|---|
| Đầu thấp hơn / cao hơn khi bò | **Crawl Crouch** (mục Bò) | 0.14 |
| Đầu dẹt xuống khi bò | **Crawl Flatten** | 0.05 |
| Xúc tu uốn sóng mạnh / nhẹ | **Wave Amount** (mục Sóng uốn lượn trên xúc tu) | 6 |
| Ngọn uốn mạnh hơn gốc bao nhiêu lần | **Wave Tip Boost** | 2.6 |
| Sóng dày (nhiều gợn) / thưa | **Wave Length** (nhỏ = dày) | 7 |
| Sóng chạy nhanh / chậm | **Wave Speed** | 7 |
| Đứng yên còn uốn bao nhiêu | **Wave Idle** (0 = đứng yên không uốn) | 0.3 |
| Vây quanh đầu lắc nhiều / ít | **Fin Sway** (mục Vây quanh đầu lượn sóng; 0 = đứng yên) | 18 |
| Vây vươn ra thu vào | **Fin Pulse** | 0.08 |
| Mấy con sóng chạy vòng quanh đầu | **Fin Waves** | 1.5 |
| Sóng vây chạy nhanh / chậm | **Fin Speed** | 5 |

Ảnh `Docs/octopus/xem_rig/xem_xuong_vay.png` cho thấy 12 xương vây (vàng) và vùng đi theo chúng (hồng).


---

# Kiểu bò "Trườn" (đang dùng) và dáng đứng

- **Đi:** xúc tu nằm dài sát đất, uốn sóng như rắn từ gốc ra ngọn; chóp chỉ trượt nhẹ tới lui, không nhấc lên bước. Thân lướt êm, đầu hạ thấp.
- **Đứng:** đầu hạ thấp, hai xúc tu bên xoè ra nằm trên đất, uốn sóng chậm.
- Clip `Crawl.anim` đã tạo lại theo kiểu trườn: mỗi tay cầm một keyframe mỗi 10 frame, **Bám chặt** luôn tắt (chóp được trượt).
  Clip kiểu bước trước đó (có phần bạn đã sửa) được sao lưu ở `Assets/_Project/Art/Octopus/Rig/Backup/Crawl_cu.anim`.

| Muốn | Ô (chọn Rig Visual) | Mặc định |
|---|---|---|
| Đầu thấp hơn lúc **đứng** | **Idle Crouch** (mục Đứng yên) | 0.22 |
| Đầu thấp thêm lúc **đi** | **Crawl Crouch** (mục Bò) | 0.15 |
| Hai xúc tu bên xoè rộng hơn lúc đứng | **Idle Spread** | 0.1 |
| Xúc tu uốn mạnh hơn | **Wave Amount** | 9 |
| Sóng dài, mềm hơn / ngắn, gợn hơn | **Wave Length** (lớn = dài mềm) | 9 |
| Ngọn uốn mạnh hơn gốc | **Wave Tip Boost** | 1.8 |
| Đứng yên vẫn uốn nhiều | **Wave Idle** | 0.45 |
| Quay về kiểu bước (nhấc chân) | **Crawl Style** = Step, rồi bấm **Tạo lại clip bò…** | Slither |

Muốn chỉnh riêng từng xúc tu khi trườn: mục **Slither** (0 trái, 1 giữa, 2 phải):
- **Reach:** chóp nằm xa chỗ nghỉ bao nhiêu (âm = duỗi ra sau).
- **Slide:** trượt tới lui bao nhiêu.
- **Curl:** chóp cuộn bao nhiêu (nhỏ = duỗi dài sát đất).
- **Curl Swing:** độ cuộn lên xuống theo nhịp.

Chỉnh xong bấm **Tạo lại clip bò từ thông số "Bò bằng code"…** để clip lấy dáng mới (hoặc chỉnh thẳng clip bằng bảng ⚡).
