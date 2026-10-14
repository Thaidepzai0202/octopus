# Octopus Swing

Game 2D: bạch tuộc 3 xúc tu bắn xúc tu và đu kiểu người nhện trong hang động tím. Unity **6000.3.26f1** (6.3 LTS), URP 2D.
Ảnh xem trước các tư thế và map: `Docs/preview/`.

## Chạy thử

1. Unity Hub → **Add → Add project from disk** → chọn thư mục này.
2. Mở `Assets/_Project/Scenes/Playground.unity` rồi bấm **Play**.

## Điều khiển

Đá có viền xanh ngọc sáng rực là đá nằm trong **bán kính xúc tu** (vòng tròn mờ quanh bạch tuộc), chạm vào là bám được.

| Trạng thái | Chạm vào đá trong tầm | Chạm nhanh (tap) | Giữ tay | Vuốt ngang |
|---|---|---|---|---|
| Trên sàn | Bắn xúc tu + giật lên | Nhảy | — | Bò trái / phải |
| Đang đu | Đổi sang đá khác | Buông, văng đi | Rút lên tới đá (nhả tay là dừng) | Bơm đu |
| Bám dưới đá | Bắn sang đá khác | Thả xuống | — | Bò dọc theo đá |

Nhấc tay khi đang đu **không** buông xúc tu; muốn buông thì chạm nhanh một cái.
**Cộng hưởng:** giữ tay để rút xúc tu đúng lúc đi qua điểm thấp nhất thì cú đu cao dần (bảo toàn mô-men động lượng, như nhún xích đu).

Trong Editor dùng chuột như ngón tay, hoặc bàn phím: A/D = vuốt, W = giữ, Space = tap.

## Cấu trúc

```
Assets/_Project/
├── Scripts/Runtime/        Octopus.Runtime.asmdef
│   ├── Input/              PlayerInputReader: chuột / cảm ứng → tap, giữ, vuốt
│   ├── Player/             OctopusController (máy trạng thái), SwingController (xúc tu đu),
│   │                       SurfaceMover (bò, nhảy, bám đá), PlayerRespawn
│   ├── Presentation/       OctopusSpriteAnimator (hoạt ảnh khung hình), OctopusAnimator (hoạt ảnh vẽ bằng code),
│   │                       ReachIndicator (bán kính), SpeedZoom (camera),
│   │                       ParallaxLayer (nền nhiều lớp), AmbientMotes (bụi sáng)
│   ├── Level/              Rock (đá bám được, viền sáng), KillZone
│   └── Core/               FrameRateSetup (60–120fps)
├── Scripts/Editor/         ProjectSetup (dựng scene), CaveBuilder (vẽ hang động), OctopusBuilder (dựng bạch tuộc),
│                           ArtFactory (vẽ PNG bằng code), DrawnArt (ảnh vẽ tay), CaveLayout (bố cục chung),
│                           BuildScripts (build), IosPostBuild (Info.plist)
├── Tests/PlayMode/         Test vật lý, điều khiển và hoạt ảnh
├── Scenes/                 Playground.unity
└── Art/                    Generated/ (art sinh bằng code), Drawn/ (ảnh vẽ tay), Octopus/ (khung hình bạch tuộc), Materials/
```

Luồng một chiều: **Input → OctopusController → SwingController / SurfaceMover → OctopusAnimator**.
Muốn thêm âm thanh, hạt mực, rung… thì nghe event `OctopusController.StateChanged` / `Jumped` hoặc `SwingController.Attached` / `Released`, đừng sửa logic di chuyển.

## Hoạt ảnh

> Đang dùng bản **gắn xương** từ ảnh con chính, xem mục [Bạch tuộc gắn xương](#bạch-tuộc-gắn-xương-đang-dùng).
> Mục này mô tả bản vẽ bằng code (`Octopus → Visual`, đang tắt).

Bạch tuộc vẽ bằng code (`OctopusAnimator`), không dùng clip vẽ sẵn, vì xúc tu phải vươn tới đúng tảng đá và đúng mặt đất mỗi lần.
Mỗi frame: chọn **tư thế** theo trạng thái + vận tốc → chuyển mượt → vẽ thân và 3 xúc tu bằng `LineRenderer`.

| Tư thế | Dáng |
|---|---|
| Nghỉ | Thân cuộn hình lưỡi liềm, 3 xúc tu tựa xuống đất, đầu mút cuộn; thân thở phồng / xẹp, mắt liếc quanh |
| Bò | Dáng đi 3 chân: mỗi xúc tu bám đất đứng yên rồi nhấc lên bước tới theo vòng cung, lệch nhau 1/3 chu kỳ; thân nhấp nhô theo nhịp bước |
| Nhảy | Bật lên: ngóc đầu, xúc tu đạp ra sau, thân dãn dài → đỉnh: xúc tu xoè như chiếc ô → rơi: xúc tu chĩa xuống đón đất → chạm đất: thân bẹp xuống rồi nảy lại như lò xo |
| Đu | Đầu hướng về đá, xúc tu trước nắm đá, hai xúc tu kia quất theo quán tính phía sau |
| Bay | Theo bản phác: thân duỗi theo hướng bay, xúc tu chụm phía trước, vệt tốc độ sau đuôi |
| Bám đá | Hai xúc tu bám đá, xúc tu giữa đung đưa, thân lủng lẳng |

Mắt có con ngươi riêng nhìn theo hướng di chuyển và chớp ngẫu nhiên vài giây một lần. Xúc tu có hàng giác hút và viền tối.

### Chỉnh chi tiết hoạt ảnh

Chọn `Octopus → Visual` trong Hierarchy → `OctopusAnimator` trên Inspector. **Không cần bấm Play**: chọn tư thế ở ô **Preview Pose**
(Nghỉ / Bò / Nhảy - bật lên / Nhảy - đỉnh / Nhảy - rơi / Đu / Bay / Bám đá), Scene vẽ lại ngay khi kéo thanh trượt. Chỉnh xong **Cmd+S**.
Với tư thế Bò, kéo **Preview Crawl Phase** để xem từng nhịp bước.

| Nhóm | Chỉnh được |
|---|---|
| Ngoại hình | Màu thân / mực / quầng sáng, độ béo thân (đường cong), độ to xúc tu, khoảng cách giác hút, cỡ mắt, cỡ đầu mút |
| Dáng thân | Độ dài, độ cong khi nghỉ / khi đu, độ dãn khi bay, nhịp thở |
| Co giãn | Độ cứng / giảm chấn lò xo, độ dãn khi bật nhảy, độ bẹp khi tiếp đất |
| Xúc tu | Độ dài, độ uốn lượn, độ cuộn đầu mút, góc xoè khi đu |
| Bò | Độ dài bước, tỉ lệ bám đất / nhấc chân, độ cao nhấc chân, chỗ đặt 3 xúc tu khi đứng yên |
| Nhảy | Góc ngóc đầu, góc đạp chân khi bật, độ xoè ở đỉnh và khi rơi |
| Mắt | Khoảng thời gian chớp, độ di chuyển con ngươi |

Chỉnh lúc **đang Play** sẽ mất khi thoát. Muốn giữ: chuột phải tên component → **Copy Component**, dừng Play, chuột phải → **Paste Component Values**.

## Hang động (map)

Toàn bộ hình hang động được **vẽ bằng code** trong `CaveBuilder` (theo ảnh mẫu hang tím), lưu thành PNG ở `Art/Generated`:

- **Nền nhiều lớp trôi theo chiều sâu** (`ParallaxLayer`): phông sáng → cột / nhũ đá xa → lớp giữa → tảng đá gần có tinh thể → tiền cảnh tối.
- **Trần hang** với nhũ đá rủ xuống; **cột đá tối** nối trần với từng tảng đá bám được.
- **Tảng đá bám được**: viền dưới phát sáng xanh ngọc, sáng rực và nhấp nháy khi nằm trong tầm xúc tu.
- **Gờ đá (sàn)**: viền trên phát sáng, vài cụm tinh thể; vực bên dưới chìm trong **sương tím**.
- **Bụi sáng** lơ lửng (`AmbientMotes`) và **cụm tinh thể lớn** ở đích.

Muốn đổi:
- **Màu**: các hằng số màu ở đầu `CaveBuilder.cs` (`FarTint`, `RockTint`, `RimCyan`…).
- **Vị trí sàn / đá**: mảng `Ledges` và `Rocks` trong `CaveBuilder.cs`.
- Sửa xong chạy menu **Octopus → Dựng lại màn chơi (ghi đè scene)** để vẽ lại art và dựng lại `Playground.unity`.

`Art/Generated` bị vẽ lại mỗi lần dựng lại màn chơi, đừng sửa tay các PNG trong đó. Muốn dùng hình tự vẽ thì xem phần dưới.

## Vẽ ảnh tay

Art vẽ bằng code chỉ là tạm. Thay dần bằng ảnh vẽ tay: **vẽ lớp nào thay lớp đó**, lớp chưa vẽ vẫn giữ art cũ.

### Cần vẽ những file nào

Thả PNG vào `Assets/_Project/Art/Drawn/` với **đúng tên file**:

| File | Khổ khuyên dùng | Vẽ gì | Lưu ý |
|---|---|---|---|
| `sky.png` | 1920 × 1080 | Phông xa nhất: màu nền, khoảng sáng cửa hang | Không cần trong suốt; dính theo camera |
| `deep.png` | 3840 × 1080 | Toàn cảnh xa nhất (sương, thác, núi đá mờ) | Trôi rất chậm; có thể kín nền |
| `far.png` | 3840 × 1080 | Cột / nhũ đá xa, nhạt màu | Nền trong suốt, **lặp ngang** |
| `mid.png` | 3840 × 1080 | Cột / nhũ đá tầm giữa | Nền trong suốt, lặp ngang |
| `near.png` | 3840 × 1080 | Tảng đá, tinh thể gần | Nền trong suốt, lặp ngang |
| `front.png` | 3840 × 1080 | Đá rất tối sát mép dưới (tiền cảnh) | Chỉ vẽ phần dưới, còn lại trong suốt, lặp ngang |
| `ceiling.png` | 3840 × 1080 | Trần hang + nhũ đá rủ xuống | Mép trên ảnh là trần, lặp ngang |
| `ledge.png` | 2048 × 2048 | Một gờ đá (sàn) kèm thân đá phía dưới | **Mép trên ảnh là mặt sàn** |
| `rock.png` | 1024 × 512 | Một tảng đá treo để bám | **Mép dưới ảnh là chỗ xúc tu bám** |
| `column.png` | 512 × 2048 | Cột đá nối tảng đá lên trần | Bị kéo giãn theo chiều cao |

Khổ ảnh khác cũng được: Unity tự co giãn theo chiều cao (lớp nền) hoặc bề rộng (sàn, đá). Khổ khuyên dùng là đủ nét trên điện thoại mà vẫn nhẹ.

### Khung mẫu để vẽ

`Docs/art-templates/` có sẵn một ảnh mẫu cho mỗi file trên, đúng khổ, nền trong suốt, kẻ vạch:

| Vạch | Ý nghĩa |
|---|---|
| Xanh lá liền | Khung camera nhìn thấy lúc bình thường: chi tiết đẹp nhất đặt trong này |
| Xanh lá đứt | Phần có thể lộ ra khi camera lên cao / zoom xa: vẫn phải vẽ kín tới đây |
| Đỏ | Mặt sàn (lớp nền, trần) · mặt sàn (ledge) · chỗ bám (rock) |
| Dải vàng mờ | Độ cao các tảng đá treo: đừng vẽ chi tiết rối ở đây để người chơi dễ thấy đá |
| Khung xám | Collider thật của sàn / đá |

Mở ảnh mẫu thành **lớp dưới cùng** trong phần mềm vẽ, vẽ ở các lớp phía trên, **ẩn lớp mẫu trước khi xuất PNG**.
Mất ảnh mẫu thì tạo lại bằng menu **Octopus → Tạo khung mẫu để vẽ**.

### Vẽ lặp ngang (seamless)

Các lớp nền và trần được đặt nối tiếp nhau theo chiều ngang, nên **mép trái phải nối liền mép phải**:
- **Krita** (miễn phí): bật *Wrap Around Mode* (phím **W**), vẽ tràn qua mép sẽ hiện ở mép bên kia.
- **Photoshop**: *Filter → Other → Offset*, dịch ngang nửa khổ ảnh để thấy và sửa đường nối ở giữa.

### Đưa vào game

1. Lưu PNG vào `Assets/_Project/Art/Drawn/`.
2. Unity → menu **Octopus → Áp dụng ảnh vẽ tay**. Unity tự đặt ảnh vào đúng lớp parallax, đúng kích thước, rồi lưu scene.
3. Bấm Play để xem.

### Sửa tiếp từ art hiện tại

Menu **Octopus → Xuất ảnh màu hiện tại** xuất art đang có (đã tô màu như trong game) vào `Docs/colored-art/`:
- `sky`, `far`, `mid`, `near`, `front`, `ceiling`, `ledge`, `rock`, `column.png`: đúng định dạng ảnh vẽ tay.
  Mở ra vẽ đè, chỉnh màu… rồi lưu cùng tên vào `Assets/_Project/Art/Drawn` là game dùng ngay.
- `pieces/`: từng mảnh riêng lẻ đã tô màu (nhũ đá, cột, tảng đá, thân bạch tuộc…) để xem và làm mẫu.

Ảnh trong `Art/Generated` là ảnh **xám**: màu được nhuộm trong Unity (ô Color của SpriteRenderer), nên muốn xem màu thật thì dùng bản xuất này.

### Thay ảnh

**Lưu đè file cùng tên** (ví dụ sửa `rock.png` trong Krita rồi Save). Bấm sang cửa sổ Unity là scene Playground tự cập nhật,
không cần bấm menu. Nhớ **Cmd+S** để lưu scene.

Muốn bỏ ảnh vẽ tay của một lớp để quay về art vẽ bằng code: xoá file đó trong `Art/Drawn` rồi chạy **Octopus → Dựng lại màn chơi**.

### Chỉnh ảnh ngay trong Unity

Menu **Octopus → Bảng chỉnh ảnh vẽ tay** mở bảng chỉnh trên Inspector. Mỗi ảnh có:

| Ô | Tác dụng |
|---|---|
| **Tint** | Nhân màu cho ảnh. Kênh **A (Alpha)** là độ đậm: kéo xuống để lớp nền mờ đi, xa hơn |
| **Offset Y / Offset X** | Dịch lên xuống / dịch chỗ bắt đầu lặp (lớp nền) · **Offset** dịch hình so với collider (sàn, đá, cột) |
| **Scale** | Phóng to / thu nhỏ so với mặc định |
| **Flip X** | Lật ngang |
| **Mirror Tiles** | Lớp nền / trần: các ô lặp lật xen kẽ như soi gương, chỗ nối luôn liền mạch dù ảnh không vẽ để lặp |
| **Surface** | Sàn: mặt đứng nằm cách mép trên ảnh bao nhiêu (tỉ lệ chiều cao) · Đá: chỗ bám cách mép dưới bao nhiêu |
| **Show Columns** | Hiện / ẩn cột đá nối tảng đá lên trần (ẩn khi đá là đảo lơ lửng) |
| **Show Ledge Rim** | Hiện / ẩn vạch sáng xanh dọc mặt sàn |

Sửa số là scene đổi ngay (đang mở Playground, không Play), rồi **Cmd+S**. Giá trị lưu trong `Art/Drawn/DrawnArtSettings.asset`,
áp dụng lại bao nhiêu lần cũng không mất. Tắt ô **Auto Apply** nếu muốn chỉ cập nhật khi bấm menu.

Chỉ dịch **hình**, không dịch collider: dịch tảng đá quá xa thì chỗ xúc tu bám (viền sáng) sẽ lệch khỏi hình.

Viền xanh phát sáng của tảng đá vẫn được giữ (nó báo đá nào trong tầm xúc tu), chạy thẳng theo mép dưới ảnh `rock.png`.
**Octopus → Dựng lại màn chơi** cũng tự dùng ảnh vẽ tay nếu có.

### Mẹo vẽ cho đẹp và nhẹ

- Lớp càng xa càng **nhạt và ngả xanh**, càng ít chi tiết; lớp càng gần càng **tối và đậm**, như ảnh mẫu hang tím.
- Lớp nền / tiền cảnh **để trong suốt càng nhiều càng tốt**: điện thoại phải vẽ chồng các lớp lên nhau, vùng trống giúp game nhẹ.
- Tảng đá bám được nên **sáng hơn hẳn nền** để người chơi nhìn ra ngay.

## Map hiện tại: hang tím từ bảng phân lớp

Map đang dùng ảnh tách từ `Docs/layers/Hang Động Tím Huyền Bí Phân Lớp.png` (bảng 5 dải do AI tạo):

| Dải trong bảng | Thành file | Đặt ở đâu trong game |
|---|---|---|
| 4 · toàn cảnh thác, sương | `deep.png` (+ màu cho `sky.png`) | Lớp xa nhất, phủ gần kín màn hình |
| 5 · toàn cảnh hồ, cột đá | `far.png` | Nửa dưới, nửa trên mờ dần để lộ thác phía sau |
| 3 · lâu đài, đảo có thác | `mid.png` | Thấp, tối bớt để không lẫn với đá thật |
| 2 · đảo nối cầu dây | `near.png` | Dưới vực, tối hơn nữa |
| 1 · trần nhũ đá | `ceiling.png` | Khung tối phía trên |
| Gờ đèn lồng (dải 2) | `ledge.png` | 3 sàn; ghép gương nên có cổng 2 đèn lồng ở giữa |
| Đảo tinh thể (dải 3) | `rock.png` | 10 tảng đá bám (đảo lơ lửng, đã ẩn cột đá) |

Vị trí, độ cao, độ tối của từng lớp nằm trong **Octopus → Bảng chỉnh ảnh vẽ tay**.
Ảnh trung gian: `Docs/layers/split/` (5 dải đã tách nền), `Docs/layers/game/` (bộ ảnh game dùng).

**Làm lại với bảng phân lớp khác** (cần Python có Pillow + NumPy):

```bash
python Tools/art/split_layers.py "Docs/layers/<bảng mới>.png" Docs/layers/split   # tách nền ô caro giả, phóng to ×2
python Tools/art/cut_pieces.py Docs/layers/split Docs/layers/game                 # cắt sàn, đá, các lớp
cp Docs/layers/game/*.png Assets/_Project/Art/Drawn/                              # Unity tự cập nhật
```

Toạ độ cắt trong hai script ứng với bảng hiện tại; bảng khác bố cục thì phải đo lại.

## Bạch tuộc gắn xương (đang dùng)

Chỉ dùng **một ảnh con chính** (`Docs/octopus/octopus.png`). Ảnh được tách thành 6 bộ phận; **đầu có 3 xương**
(mềm như áo bạch tuộc), **mỗi xúc tu 14 xương** (package **2D Animation**: SpriteSkin uốn ảnh theo xương). Code tính góc
từng xương mỗi frame, nên mọi dáng đều sinh ra từ cùng một bức tranh; không cần vẽ thêm khung nào.

```
Octopus
└─ Rig Visual            ← OctopusRigAnimator (mọi thông số chỉnh ở đây)
   ├─ Glow, Speed Trail
   └─ Pose               ← xoay / lật / co giãn / phóng to cả con (Size)
      ├─ Torso           ← chồm tới, nhấp nhô khi bò; hạ xuống khi tiếp đất
      │  ├─ MANTLE_00 → MANTLE_01 → MANTLE_02     ← 3 xương đầu (+ FIN_00…11: vây quanh đầu, con của xương đầu)
      │  │              └─ Eye (tròng, có SpriteMask) → Pupil (con ngươi)
      │  └─ L_00…L_13, M_00…M_13, R_00…R_13       ← 3 chuỗi xương xúc tu
      ├─ Body            ← ảnh đầu + SpriteSkin
      └─ Tentacle L / M / R   ← ảnh xúc tu + SpriteSkin
```

| Trạng thái | Xương làm gì |
|---|---|
| Đứng yên | Đầu hạ thấp, hai xúc tu bên xoè ra nằm trên đất uốn sóng chậm, đầu phồng xẹp như thở, mắt chớp, con ngươi liếc quanh |
| **Bò** | Kiểu **trườn**: đầu hạ thấp, ba xúc tu nằm dài sát đất uốn sóng như rắn từ gốc ra ngọn, chóp chỉ trượt nhẹ tới lui, thân lướt êm. (Kiểu bước cũ: `Crawl Style` = Step) |
| Nhảy | Bật lên thì xúc tu duỗi ra kéo theo sau như mực phụt nước; tới đỉnh thì cuộn lại như tranh |
| Rơi | Xúc tu xoè ngược lên như chiếc ô, đầu ngả ngược hướng bay |
| Tiếp đất | Thân bẹp và hạ xuống, xúc tu vẫn bám đất nên tự co lại, rồi đứng dậy |
| Đu | **Xúc tu giữa vươn thật tới đá**: đoạn gốc kéo dài, đoạn chóp giữ nét cuộn của tranh và quấn vào đá |
| Bám đá | Đỉnh đầu áp vào đá, hai xúc tu bên nắm mặt đá; bò dọc đá thì thay nhau với tới như đu xà |
| Mắt | Con ngươi luôn dựng đứng dù thân nghiêng (mắt bạch tuộc thật giữ con ngươi theo phương ngang trời) |

### Dáng bò chỉnh tay (clip `Crawl.anim`)

Dáng bò lấy từ clip `Assets/_Project/Art/Octopus/Rig/Crawl.anim` (1 giây = 1 vòng bò, tự chạy nhanh / chậm theo tốc độ bò).
Clip không xoay xương trực tiếp mà điều khiển **4 tay cầm** trong `Rig Visual › Pose › Crawl`: `Foot L / M / R` (chỗ chóp
xúc tu chạm tới; `Curl` = độ cuộn chóp; `Contact` = 1 đang bám đất / 0 đang nhấc) và `Body` (thân chồm / nhấp nhô; `Mantle`
= đầu ngả sau). IK tự uốn 14 xương theo tay cầm; lúc `Contact` = 1 chóp được khoá đứng yên trên đất.
Chỉnh: chọn Rig Visual → **Mở cửa sổ Animation để chỉnh dáng bò** → bật ● Record → kéo tay cầm trong Scene.
Game dùng bản sao đường cong `CrawlCurves.asset`, tự cập nhật mỗi khi sửa clip. Hướng dẫn từng bước cho người mới:
[Docs/HUONG_DAN_CHINH_XUONG.md](Docs/HUONG_DAN_CHINH_XUONG.md). Bỏ tick `Use Crawl Clip` để quay về bò bằng code (mục Gait).

### Xem và chỉnh khung xương

Chọn `Octopus → Rig Visual` trong Hierarchy, mở cửa sổ **Scene**:

1. **Xem xương:** bật `Show Bones` (mặc định bật). Scene vẽ xương xúc tu (vàng ở gốc → đỏ ở chóp), xương đầu (xanh),
   chỗ chân **đang bám đất** (chấm xanh lá; chấm cam = đang nhấc lên), đích IK của chân (dấu +), điểm nắm đá (vòng vàng).
   Khi bấm Play cũng thấy (Scene view, hoặc bật nút Gizmos trên Game view).
2. **Xem chuyển động không cần Play:** ở ô `Preview` chọn Idle / Crawl / Jump / Fall / Swing / Cling rồi bấm nút xanh
   **▶ Chạy thử trong Scene** ở đầu Inspector. Bạch tuộc chuyển động tại chỗ; sửa thông số là thấy ngay. Bấm ■ để dừng.
   Không chạy thử thì kéo `Preview Time` để xem từng thời điểm (kéo hết sang phải = mắt nhắm).
3. **Tự xoay xương bằng tay:** tắt `Drive Bones` → mở `Pose → Torso` trong Hierarchy, chọn xương (vd `R_05`) và xoay
   bằng công cụ Rotate (phím E) trong Scene, ảnh uốn theo. Bật lại để code điều khiển tiếp.
4. **Sửa vị trí xương / lưới / trọng số:** 4 nút **Đầu / Xúc tu trái / giữa / phải** ở đầu Inspector mở Sprite Editor
   cho ảnh đó → góc trên trái chọn **Skinning Editor**. Ở đó kéo khớp xương, vẽ lại trọng số (màu = xương ảnh hưởng)
   rồi bấm **Apply**: code tự đọc dáng xương mới. Nếu **thêm / bớt** xương thì chạy menu
   **Octopus → Dựng lại bạch tuộc (giữ map)** (giữ nguyên map và các thông số đã chỉnh).

**Thông số trên Inspector của `OctopusRigAnimator`:**
- `Size`: to / nhỏ cả con (1 = cao 1,5 unit như tranh; mặc định 1,5). Chỉ đổi hình, không đổi va chạm.
- Bò: `Cycle Distance` (sải mỗi vòng), `Arm Stretch` (xúc tu co / duỗi tối đa), `Use Crawl Clip`, `Foot Lock`; khi bò bằng code: `Surge` (thân chồm), `Crawl Bob`,
  `Mantle Trail` / `Mantle Surge` (đầu ngả sau, lắc theo nhịp), và **`Gait`**: từng xúc tu một dòng (0 trái = đẩy, 1 giữa = lăn,
  2 phải = vươn kéo): `Phase` (lệch nhịp), `Grip` (phần nhịp bám đất), `Plant Ahead` (đặt chóp xa phía trước bao nhiêu),
  `Lift` (nhấc cao), `Grip Curl` / `Swing Curl` (chóp cuộn chặt hay duỗi khi bám / khi đưa tới).
- Đứng yên: `Breath Amount` (đầu thở), `Idle Curl`, `Tip Wiggle`.
- Hạ thân khi bò: `Crawl Crouch` (đầu thấp xuống, xúc tu tự co lại), `Crawl Flatten` (đầu dẹt xuống một chút).
- Sóng trên xúc tu (chạy từ gốc ra tận ngọn, cả lúc bò, bay, đu): `Wave Amount`, `Wave Tip Boost` (ngọn mạnh gấp mấy lần gốc),
  `Wave Length`, `Wave Speed`, `Wave Idle`. Lúc bò, IK tính cả sóng nên chóp đang bám vẫn đứng yên.
- Vây quanh đầu lượn sóng: ảnh đầu có thêm 12 xương vây `FIN_00…11` toả tròn từ tâm đầu (chỉ viền gai đi theo, mặt và mắt
  đứng yên); sóng chạy vòng quanh đầu: `Fin Sway` (lắc, độ), `Fin Pulse` (vươn ra / thu vào), `Fin Waves` (số con sóng),
  `Fin Speed`, `Fin Idle`. Kéo `Fin Sway` và `Fin Pulse` về 0 để vây đứng yên.
- Nhảy / rơi: `Land Dip`, `Trail Speed` / `Full Trail Speed`, `Max Uncurl`, `Air Lean`, `Air Wave`, `Mantle Drag`.
- Đu / bám: `Reach Bones`, `Shoot Duration`, `Hold Curl`, `Grip Spread`, `Regrip Distance`.
- Lò xo: `Root Stiffness`, `Tip Stiffness` (nhỏ thì chóp vẫy nhiều), `Damping` (nhỏ thì nảy nhiều), `Mantle Stiffness` (nhỏ thì đầu mềm hơn).
- Mắt: `Blink Interval`, `Blink Time`, `Pupil Range`, `Pupil Upright`.

**Độ nét:** ảnh bộ phận xuất 256 px mỗi unit tranh, làm nét nhẹ khi thu nhỏ; Unity dùng mipmap lọc Kaiser (thu nhỏ trên
điện thoại màn nhỏ không bị lấm tấm răng cưa). Camera đứng yên cỡ 6 (bay nhanh lùi ra tới 9, chỉnh ở `Follow Camera → SpeedZoom`).

Ảnh chụp thử các dáng (bò, nhảy, đu, bám, chạy thử trong Scene) ở `Docs/octopus/xem_rig/`.

**Ảnh bộ phận** nằm ở `Docs/octopus/rig/` (bản để xem) và `Assets/_Project/Art/Octopus/Rig/` (bản game dùng):
`body.png` (chỗ mắt được vẽ bù thành mí mắt khép, chỉ thấy khi chớp), `eye.png` (tròng đã xoá con ngươi và vẽ bù),
`pupil.png`, `tentacle_l/m/r.png` (mỗi xúc tu kèm một đoạn gốc giấu dưới thân để khi xoay không hở) và `rig.json`
(vị trí bộ phận, xương, lưới, trọng số).

- Muốn **tô lại bộ phận** (đổi màu, thêm chi tiết): sửa thẳng PNG trong `Assets/_Project/Art/Octopus/Rig/`, **giữ nguyên cỡ ảnh và vị trí nét vẽ**, Unity tự nạp lại; xương và lưới vẫn dùng được.
- Muốn **đổi cả con** (vẽ lại `octopus.png`, cùng tư thế 3 xúc tu): chạy lại script rồi **Octopus → Dựng lại bạch tuộc (giữ map)**
  (chạy lại script sẽ ghi đè xương / trọng số đã sửa tay trong Skinning Editor):

```bash
python Tools/art/octopus_rig.py Docs/octopus/octopus.png Docs/octopus/rig
cp Docs/octopus/rig/* Assets/_Project/Art/Octopus/Rig/
```

  Nếu tư thế khác hẳn, sửa trong script: `AXES` (đường trục từng xúc tu, toạ độ pixel ảnh gốc, từ gốc dưới thân tới chóp cuộn),
  `CUT_Y` (dòng chia thân / xúc tu), `BODY_BLADES` (các lưỡi gai thuộc về thân), `EYE_BOX` và `SLIT_*` (vị trí mắt, con ngươi),
  `MANTLE_AXIS` / `MANTLE_JOINTS_Y` (trục và các khớp xương đầu), `PPU` (độ nét ảnh xuất).
- File `.psd` tự tách lớp (đầu, mắt, con ngươi, 3 xúc tu, có vẽ bù phần bị che) sẽ cho kết quả đẹp hơn ảnh tách tự động.

### Bản khung hình (flipbook, đang tắt)

Bản cũ dùng khung hình vẽ tay trong `Assets/_Project/Art/Octopus/` (component `OctopusSpriteAnimator` trên `Octopus → Sprite Visual`).
Để dùng lại: tắt `Rig Visual`, bật `Sprite Visual`. Bản vẽ bằng code (`Octopus → Visual`, `OctopusAnimator`) cũng còn, đang tắt.

| Trạng thái | Khung | Cách chọn |
|---|---|---|
| Đứng yên | `idle` (octopus.png) | Thở nhẹ |
| Bò | `walk_1..6` | Đổi khung theo quãng đường đi được → chân không trượt |
| Nhảy | `jump_2` bật → `jump_3` lên → `jump_4` đỉnh → `jump_5` rơi → `jump_6` tiếp đất → `jump_1` ngồi thụp | Theo sự kiện bật / tiếp đất và vận tốc dọc |
| Rơi nhanh | `fall_1..6` | Rơi càng nhanh càng chúc đầu |
| Đu | `swing_1` (đu nhanh), `swing_2` (đu chậm) | Xoay cho đỉnh đầu hướng về đá; **xúc tu nắm đá vẽ bằng code** nối từ gốc xúc tu tới đá |
| Bám đá | `swing_2` | Gốc xúc tu áp sát mặt đá |
| Vừa buông | `release` | 0,25 giây sau khi buông |

Co giãn khi bật nhảy / tiếp đất, nghiêng theo hướng bay, quầng sáng tím và vệt tốc độ vẫn do code thêm vào.
Các ngưỡng (bò nhanh cỡ nào thì đổi khung, rơi nhanh cỡ nào thì chúc đầu, thời gian từng khung…) chỉnh trên Inspector của `OctopusSpriteAnimator`.

**Thay hoặc thêm khung hình:** ảnh gốc ở `Docs/octopus/` (4 tấm 6 khung, nền trắng + `octopus.png`). Chạy:

```bash
python Tools/art/octopus_frames.py Docs/octopus Docs/octopus/frames   # tách nền trắng, cắt khung, tính điểm đặt
cp Docs/octopus/frames/* Assets/_Project/Art/Octopus/
```

rồi Unity → **Octopus → Dựng lại màn chơi**. Script tự tách nền (giữ quầng tím phát sáng, giữ đốm sáng trong mắt), cắt từng khung,
bỏ bóng đổ dưới các khung đang bay, cắt bỏ xúc tu + tảng đá vẽ sẵn ở khung đu và ghi lại gốc xúc tu (`frames.json`).
Vẽ lại theo đúng bố cục cũ (6 khung xếp ngang, nền trắng, cùng cỡ nhân vật) là chạy được ngay.

## Tinh chỉnh cảm giác điều khiển

Chọn `Octopus` trong scene:
- `SwingController`: `reach` (bán kính xúc tu), `reelSpeed`, `pumpForce`, `autoPumpForce`, `groundLaunchSpeed`, `releaseBoost`, `maxSpeed`.
- `SurfaceMover`: `crawlSpeed`, `jumpSpeed`, `clingCrawlSpeed`, `dropSpeed`.
- `PlayerInputReader`: `tapMaxDuration` (ngưỡng tap / giữ), `dragThreshold` (ngưỡng vuốt).
- Trọng lực: `Rigidbody2D.gravityScale`.

## Test

Window → General → **Test Runner** → PlayMode → Run All. Hoặc chạy từ terminal (đóng Unity Editor trước):

```bash
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode \
  -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```

## Build lên iPhone

Menu **Octopus → Build iOS (Xcode project)** → mở `Builds/iOS/Unity-iPhone.xcodeproj` → chọn iPhone → ▶ Run.

Trước khi lên TestFlight: đổi bundle id `com.yourcompany.octopus`, Company Name và **Signing Team ID** trong **Project Settings → Player**.
