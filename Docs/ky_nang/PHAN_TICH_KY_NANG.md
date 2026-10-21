# Hệ thống kỹ năng: 3 nút, vuốt đổi kiểu, thanh nộ 3 mốc

Ảnh xem trước (chụp trong game, mỗi ô là một chiêu, thứ tự như bảng bên dưới):
- `xem_chieu_bach_tuoc.png`: 9 chiêu của bạch tuộc
- `xem_chieu_nhen.png`: 9 chiêu của nhện
- `xem_man_hinh.png`: cả màn hình (thanh nộ góc trên trái, cụm nút góc dưới phải, đang giữ nút 2 vuốt lên nút 3)
- `truoc_sau_bach_tuoc.png`, `truoc_sau_nhen.png`: hiệu ứng trước (trái) và sau (phải) khi làm lại theo ảnh mẫu ChatGPT
- `mau_chatgpt/`: ảnh mẫu từng chiêu do ChatGPT vẽ (6 khung động tác mỗi chiêu), dùng làm chuẩn cho hoạt ảnh và hiệu ứng

---

## 1. Cách bấm

```
                    ┌───┐
                    │ 3 │  chiêu cuối (cần nộ)
                    └───┘
          ┌───┐     ┌───┐
          │ 1 │     │ 2 │  ← góc dưới bên phải
          └───┘     └───┘
      đánh thường    chiêu
```

| Nút | Chạm | Vuốt sang nút này | Vuốt sang nút kia |
|---|---|---|---|
| **1 — Đánh thường** | combo 3 đòn | 1 → 2 | 1 → 3 |
| **2 — Chiêu** | chiêu chính | 2 → 1 | 2 → 3 (vuốt lên) |
| **3 — Chiêu cuối** | chiêu cuối chính | 3 → 1 | 3 → 2 (vuốt xuống) |

- **Đặt ngón lên nút, kéo về phía nút khác rồi nhả tay.** Khi đang kéo: nút đích sáng viền vàng và phóng to, tên chiêu hiện
  phía trên cụm nút. Nhả tay mới ra chiêu, nên kéo nhầm thì kéo ngón về giữa nút rồi nhả để ra chiêu chính.
- Kéo chưa tới khoảng 55 px, hoặc kéo lệch hẳn sang hướng khác, thì tính là **chạm**.
- Cách bấm này lấy từ **Brawl Stars** và **Liên Quân / Wild Rift** (kéo nút để chọn hướng / kiểu chiêu). Khác ở chỗ: ở đây
  hướng kéo không dùng để ngắm mà để chọn **một trong 3 kiểu chiêu**. Nhờ vậy 3 nút có 9 chiêu mà màn hình điện thoại vẫn gọn.
- **Đánh thường bấm liên tục ra combo 3 đòn**, đòn thứ 3 là đòn nặng (kiểu Genshin / Honkai). Bấm trong lúc đòn trước chưa xong
  thì đòn sau được xếp hàng, nên bấm dồn 3 cái vẫn ra đủ 3 đòn. Nghỉ quá 0.7 giây thì combo quay về đòn 1.
- **Hồi chiêu tính riêng từng chiêu.** Laze đang hồi vẫn phun mực được; nhện vừa vồ mồi vẫn đánh thường được. Nút hiện
  hồi chiêu của kiểu **chạm**. Khi đang giữ hoặc kéo, nút hiện hồi chiêu của kiểu đang chọn, và tên chiêu kèm số giây, ví dụ
  `Laze mắt  (3s)`. Chiêu đang hồi mà bấm thì nút lắc.
- Mỗi lúc chỉ ra một chiêu. Ra chiêu vẫn bò, nhảy, đu được (trừ chiêu tự lao người đi như Vồ mồi, Phi tơ, Phun mực).
- Chạm vào cụm nút không làm nhân vật nhảy hay bắn xúc tu.

**Thử trong Editor bằng bàn phím:** `J` / `K` / `L` = chạm nút 1 / 2 / 3. **Giữ một phím rồi bấm phím kia** = vuốt, ví dụ giữ `K`
bấm `J` = Phun mực, giữ `L` bấm `K` = Đại pháo laze. `R` = thêm 100 nộ để thử chiêu cuối (chỉ có trong Editor và bản build Development).
Di chuyển vẫn như cũ: A/D, W, Space. Phím E cũ (laze) đã bỏ; laze giờ là nút 2.

---

## 2. Thanh nộ (góc trên bên trái)

- **Đầy dần khi di chuyển:** bò, nhảy, đu, bò ngược đều tính, 2.5 nộ cho mỗi ô (unit) đi được. Hồi sinh hoặc bị dịch chuyển tức thời thì không tính.
- **Đầy dần khi đánh trúng:** 0.35 nộ cho mỗi điểm sát thương gây ra (một combo 3 đòn của bạch tuộc khoảng 27 nộ). Chiêu cuối không cộng nộ.
- **3 mốc, mỗi mốc 100 nộ** (tối đa 300). Thanh có 3 vạch mốc, qua vạch nào thì vạch đó sáng. Thanh đổi màu cam → đỏ → hồng tím,
  nảy lên khi vừa qua mốc. Chữ đổi theo mốc: `NỘ` → `NỘ  I` → `NỘ  II` → `NỘ  III`.
- **Chưa qua mốc 1 thì nút 3 bị khoá:** nút tối, có chữ `NỘ`, vùng tối rút dần theo nộ. Qua mốc 1 thì viền nút 3 đỏ rực và nhấp nháy.
  3 chấm phía trên nút 3 cho biết đang ở mốc mấy.
- **Dùng chiêu cuối là xả hết nộ.** Mốc càng cao thì đòn càng đau:

| Mốc lúc dùng | Sát thương nhân | Thêm |
|---|---|---|
| I (100–199) | × 1.0 | — |
| II (200–299) | × 1.7 | vùng to hơn, thêm xúc tu / nhện con |
| III (300) | × 2.6 | vùng to nhất, nhiều xúc tu / nhện con nhất |

Thanh nộ có 3 mức đúng như **Street Fighter 6** (Super Art cấp 1 / 2 / 3) và **Dragon Ball FighterZ**. Khác ở chỗ ở đây không
chọn cấp mà luôn xả hết. Người chơi phải tự chọn: dùng sớm ở mốc I cho an toàn, hay để dành tới mốc III cho một đòn thật đau.

---

## 3. Bạch tuộc: pháp sư khống chế, thân nặng, tầm xa

**Phân tích:** 3 xúc tu dài và con mắt lớn là hai điểm nổi bật nhất của con vật này. Vì vậy bộ chiêu xoay quanh **xúc tu**
(quất, móc, trồi lên từ đất) và **con mắt** (laze). Bạch tuộc thật phun mực để trốn, nên có thêm chiêu phun mực rồi bật lùi.
Vai trò: đứng tầm trung–xa, **khống chế** (choáng, kéo về, hút vào, làm chậm), sát thương lớn nhưng chiêu cần vận sức.
Điểm yếu: chậm, laze phải đứng bắn lâu; muốn thoát thân thì dùng Phun mực.
Tham khảo: **Illaoi** (LMHT, xúc tu đập từ đất lên), **Tidehunter** (Dota 2, chiêu Ravage làm xúc tu trồi lên hất tung cả vùng),
**Pudge / Blitzcrank** (móc kéo), **Vel'Koz / Xerath** (LMHT, tia laze dài), **Splatoon** và bạch tuộc thật (phun mực).

| # | Nút · kiểu | Tên | Làm gì | Số liệu (mặc định) | Hoạt ảnh |
|---|---|---|---|---|---|
| 0 | 1 · chạm | **Quất xúc tu** | Combo 3 đòn: quất xuống, quất hất lên, nện xuống đất | 20 / 20 / 38, đòn 3 hất văng | Xúc tu trước vung tới quất, xúc tu giữa quất hất lên; đòn 3 giơ cao 2 xúc tu rồi nện, đất vỡ, vòng sóng |
| 1 | 1 → 2 | **Quất xoáy** | Xoay tròn, 3 xúc tu quất 2 vòng quanh người | 24 × 2, hồi 0.6s | Cả thân xoay tít, 3 xúc tu duỗi toả ra, vệt chém tròn |
| 2 | 1 → 3 | **Móc kéo** | Phóng xúc tu móc mục tiêu gần nhất phía trước (tầm 6), giật về sát người, choáng 0.6s | 30, hồi 2s | Xúc tu vươn thẳng ra, thân nghiêng tới, chớp sáng chỗ móc |
| 3 | 2 · chạm | **Laze mắt** | Vận sức 1s, bắn tia xuyên thấu 2s | 110 / giây, hồi 5s | Mắt mở to, sét tím, tia bung, mắt lim dim bốc khói |
| 4 | 2 → 1 | **Phun mực** | Phun đám mực trước mặt rồi bật lùi ra sau (vẫn nhìn về phía trước). Ai đứng trong mực bị chậm, mất máu dần 2.5s | 9 mỗi 0.3s, hồi 4s | Thân bẹp xuống rồi phụt mực, mây mực tím đen, bọt tím nổi lên; mục tiêu bị nhuộm tối |
| 5 | 2 → 3 | **Quét laze** | Tia ngắn quét từ dưới (−30°) lên trên (65°), trúng cả mục tiêu trên cao | 150 / giây, vận sức 0.35s, bắn 0.7s, hồi 4s | Mắt ngước dần lên theo tia |
| 6 | 3 · chạm | **Kraken trỗi dậy** | Rống lên, xúc tu khổng lồ lần lượt trồi khỏi mặt đất phía trước, hất tung, choáng 1.2s | 45 mỗi xúc tu; 3 / 4 / 6 xúc tu theo mốc | Thân ngả ra sau, 3 xúc tu giơ cao toả ra, chớp đỏ; xúc tu khổng lồ trồi lên, đá vụn bắn |
| 7 | 3 → 1 | **Xoáy nước** | Xoay tít tạo xoáy hút mục tiêu vào giữa, đánh liên tục 1.8s rồi nổ tung hất văng | 10 mỗi 0.18s + 55 nổ | Xoay liên tục, các vòng sóng xoáy và bọt cuốn vào tâm, cuối cùng nổ vòng sáng |
| 8 | 3 → 2 | **Đại pháo laze** | Vận sức 0.8s, bắn tia khổng lồ (to gấp 2.6 lần) 1.6s | 200 / giây | Thân ngả ra sau, mắt rực, tia rất to |

## 4. Nhện: sát thủ cơ động, độc, tơ

**Phân tích:** nhện nhanh, nhiều chân, có nanh độc và nhả tơ. Vai trò: **lao vào** (Vồ mồi, Phi tơ), **dồn sát thương bằng độc**
(đánh thường nào cũng tẩm độc, mất máu dần), **trói** bằng tơ. Mỗi đòn đánh thường nhẹ hơn bạch tuộc (16 so với 20) nhưng ra
nhanh hơn, cộng thêm độc thì tổng sát thương ngang hoặc hơn. Laze dùng cụm 8 mắt nên yếu và ngắn hơn laze của bạch tuộc.
Tham khảo: **Elise** (LMHT, chiêu Kén tơ làm choáng, thả nhện con), **Broodmother** (Dota 2, đẻ bầy nhện con, giăng tơ),
**Spider-Man** (bắn tơ, phi tơ kéo mình lao tới đá, lộn nhào), **Rengar** (LMHT, vồ mồi), nhện nhảy ngoài đời (bật xa vồ mồi).

| # | Nút · kiểu | Tên | Làm gì | Số liệu (mặc định) | Hoạt ảnh |
|---|---|---|---|---|---|
| 0 | 1 · chạm | **Cào độc** | Combo 3 đòn: cào chân trước, cào chân kia, cắn. Đòn nào cũng tẩm độc (6 / giây trong 2.5s, đòn cắn 9 / giây) | 16 / 16 / 30 | Chân trước nhấc lên đâm tới rồi rút về, đổi chân; đòn 3 thân lao tới, nanh mở cắn |
| 1 | 1 → 2 | **Vồ mồi** | Bật nhảy vồ tới mục tiêu phía trước (tầm 5.5), đáp xuống cắn, choáng 0.4s | 42, hồi 1.5s | Chồm lên lấy đà, bay vòng cung, đáp xuống lao cắn, vòng sóng ở đất |
| 2 | 1 → 3 | **Lộn nhào** | Bật lên lộn một vòng, 8 chân quét quanh người, hất tung mục tiêu lên cao | 20 × 2, hồi 0.8s | Cả thân lộn tròn trên không, vệt chém tròn hồng |
| 3 | 2 · chạm | **Bắn tơ** | Phóng cục tơ thẳng phía trước (tầm 9), trúng thì trói đứng im 2.5s | 28, hồi 3s | Chồm lên nhả cục tơ; trúng thì bung mạng tơ, mục tiêu bị phủ tơ xoay |
| 4 | 2 → 1 | **Phi tơ** | Bắn sợi tơ dính vào mục tiêu (tầm 6.5), kéo mình lao tới đá văng | 36, hồi 3s | Chân vươn tới, sợi tơ căng, thân lao vụt theo tơ, chớp sáng khi đá |
| 5 | 2 → 3 | **Laze tám mắt** | Tám con mắt cùng bắn một tia | 120 / giây, vận sức 0.5s, bắn 1.2s, hồi 5s | Cụm mắt sáng rực, tia mảnh hơn của bạch tuộc |
| 6 | 3 · chạm | **Kén tơ** | Chồm lên phun lưới tơ tròn quanh mình, cuốn mọi mục tiêu thành kén (choáng 1.6s), 1 giây sau siết nổ tung | 30 + 75; vùng to dần theo mốc | Thân ngả ra sau, chân trước giơ cao, nanh mở; lưới tơ tròn bung ra, mục tiêu bị bọc kén trắng |
| 7 | 3 → 1 | **Bầy nhện con** | Đẻ ra bầy nhện con bò lên phía trước (tầm 8), con nào gặp mục tiêu thì cắn tẩm độc | 18 mỗi con + độc 3s; 4 / 6 / 9 con theo mốc | Thân bẹp xuống, chớp xanh độc, nhện con bò ra lũ lượt |
| 8 | 3 → 2 | **Lưới khổng lồ** | Phóng tấm lưới tơ khổng lồ chụp cả vùng phía trước, trói 3s, rồi giật tơ siết kéo mục tiêu về | 50 + 45; lưới to dần theo mốc | Chồm lên tung lưới, lưới bay ra bung rộng, sợi tơ nối về; cuối cùng lao người giật tơ |

Cột **#** là số thứ tự chiêu trong Inspector (xem mục 6).

---

## 5. Bia tập

Game chưa có quái, nên có **3 bia tập** (tượng đá có lõi pha lê) để thử chiêu: một bia ngay cạnh chỗ xuất phát (bên phải),
một bia bên trái, một bia xa hơn về phía cuối màn. Bia có 600 máu và thanh máu trên đầu. Khi trúng đòn, bia loé sáng, rung,
bị đẩy văng hoặc kéo lại rồi trôi về chỗ cũ, và hiện số sát thương bay lên (đòn liên tục như laze hay độc thì cộng dồn vào một số).
Trạng thái của bia hiện lên rõ: phủ tơ khi bị trói, bọc kén, sao xoay khi choáng, xanh lá khi trúng độc (số xanh lá),
tím đen khi dính mực. Hết máu thì bia vỡ, 3 giây sau dựng lại. 4 giây không bị đánh thì bia hồi đầy máu.
Bia không cản đường: đi, đu, bò xuyên qua được.

Sau này muốn làm quái: gắn component **Health** và một Collider2D lên object, đặt ở layer **Enemy**. Chiêu nào cũng sẽ đánh trúng.

---

## 6. Chỉnh thông số trong Unity

Chọn người chơi `Octopus` trong Hierarchy (cùng một object cho cả bạch tuộc và nhện).

| Muốn chỉnh | Ở đâu | Ô |
|---|---|---|
| Sát thương, hồi chiêu, tầm, thời gian từng chiêu | `Octopus` › **Combat Controller** › **Octopus Skills** / **Spider Skills** › mở chiêu theo **#** ở bảng trên (mỗi dòng hiện tên chiêu) | **Cooldown**, **Damage**…, laze thì mở **Shot** (**Charge**, **Fire**, **Width**, **Sweep From / To**) |
| Cỡ / tầm mọi chiêu của một nhân vật (đang ×2) | `Octopus` › **Combat Controller** › **Cỡ / tầm chiêu** | **Size**, **Range** của từng nhân vật (1 = như ban đầu) |
| Đổi tên / mô tả chiêu (hiện khi vuốt) | cùng chỗ | **Name**, **Description** |
| Nộ tăng nhanh / chậm | `Octopus` › **Rage Meter** | **Per Unit Moved** (2.5), **Per Damage** (0.35) |
| Chiêu cuối mạnh bao nhiêu ở mỗi mốc | `Octopus` › Rage Meter | **Tier Multiplier** (1, 1.7, 2.6) |
| Phải vuốt xa bao nhiêu mới tính là vuốt | `HUD › Safe Area › Skill Pad` (component **Skill Pad**) | **Swipe Threshold** (55) |
| Màu viền nút | Skill Pad | **Ready Color**, **Target Color**, **Rage Color** |
| Vị trí / cỡ từng nút | `HUD › Safe Area › Skill Pad › Button 1 / 2 / 3` | Rect Transform: **Pos X / Pos Y**, **Width / Height** |
| Vị trí cả cụm nút | `HUD › Safe Area › Skill Pad` | Rect Transform (neo góc dưới phải) |
| Vị trí / cỡ thanh nộ, màu theo mốc | `HUD › Safe Area › Rage Bar` | Rect Transform; component **Rage Bar** › **Tier Colors** |
| Máu bia, thời gian dựng lại, hồi máu | `Training Dummy` | **Health** › **Max Health** (600); **Training Dummy** › **Respawn Time** (3), **Regen Delay** (4) |
| Mục tiêu đánh được (layer) | `Octopus` › Combat Controller | **Target Mask** (Enemy) |
| Khựng hình khi trúng đòn (bật / tắt, dài bao lâu) | `Game Feel` (component **Hit Stop**) | **Stop On Hit**, **Light Stop** (0.035 s), **Heavy Stop** (0.08 s), **Ultimate Stop** (0.12 s) |
| Rung màn hình khi đòn nặng / chiêu cuối | `Follow Camera` (component **Camera Shake**) | **Shake On Hit**, **Intensity** (1), **Max Offset** (0.28) |

Chỉnh trong lúc đang Play để thử ngay thì được, nhưng **dừng Play là mất**. Thấy số ưng ý thì ghi lại, dừng Play rồi nhập lại và Cmd+S.

**Lưu ý:** menu **Octopus › Dựng lại màn chơi (ghi đè scene)** dựng lại cả scene, nên mọi số đã chỉnh trong Inspector sẽ về
mặc định. Muốn đổi mặc định vĩnh viễn thì sửa trong code (mục 7).

Biểu tượng nút: `Assets/_Project/Art/UI/Skill_Octopus_1..3.png`, `Skill_Spider_1..3.png`, tạo lại bằng `python3 Tools/art/skill_icons.py`
(ghép từ chính tranh nhân vật). Muốn vẽ tay thì ghi đè các file PNG này, giữ khổ tròn 256 × 256.

---

## 7. Code ở đâu

```
Assets/_Project/Scripts/Runtime/Combat/
├── CharacterSkills.cs   18 chiêu (mỗi chiêu một class) + DefaultSkills: bộ chiêu mặc định và tên tiếng Việt
├── Skill.cs             lớp gốc Skill, SkillContext (đánh trúng vùng tròn / chữ nhật / đoạn thẳng, tìm mục tiêu), các tư thế CombatPose
├── CombatController.cs  nhận bấm (nút / phím), xếp hàng combo, hồi chiêu, xả nộ khi dùng chiêu cuối
├── RageMeter.cs         nộ: tăng khi di chuyển / đánh trúng, 3 mốc, hệ số sát thương
├── Health.cs            máu + HitEffect (đẩy văng, choáng, trói, độc, kén, mực)
├── TrainingDummy.cs     bia tập: bẹp / nảy khi trúng đòn, nghiêng theo cú đánh, vòng sao vàng khi choáng
├── CombatFx.cs          hiệu ứng: vệt chém hình lưỡi liềm, vết cào 3 vạch, tia lửa, đất nứt + đá văng + gai đá + lửa tím,
│                        mây mực tím có bọt, xoáy nước, độc văng, lưới tơ, kén tơ, xúc tu khổng lồ, nhện con…
├── HitStop.cs           khựng hình ngắn khi trúng đòn (đòn nặng / chiêu cuối khựng lâu hơn; laze, độc không khựng)
└── DamageNumbers.cs     số sát thương nảy lên rồi bay vòng cung
Assets/_Project/Scripts/Runtime/Presentation/CameraShake.cs   rung màn hình khi đòn nặng / chiêu cuối (gắn trên camera Cinemachine)
Assets/_Project/Scripts/Runtime/UI/      SkillPad (cụm 3 nút + vuốt), SkillPadButton, RageBar
Assets/_Project/Scripts/Editor/CombatBuilder.cs   dựng bia, HUD, hiệu ứng, gắn bộ chiêu khi dựng màn chơi
Assets/_Project/Scripts/Editor/GameFeelBuilder.cs   gắn khựng hình, rung màn hình, vòng sao choáng cho bia
Tools/art/fx_textures.py   vẽ lại các ảnh hiệu ứng Fx*.png bằng code (trắng xám, game tự tô màu): python3 Tools/art/fx_textures.py
Tools/art/fx_sheet.py      cắt tấm hiệu ứng vẽ tay của ChatGPT (Docs/vfx/cg_vfx_01_energy.png) thành vệt chém, vết cào, tia lửa,
                           chớp, sóng xung kích, xoáy nước, khói, đất nứt: python3 Tools/art/fx_sheet.py
Assets/_Project/Tests/PlayMode/CombatPlayModeTests.cs   test: vị trí nút, hướng vuốt, nộ, 18 chiêu đều trúng bia, combo, hồi chiêu
```

Hoạt ảnh ra đòn nằm ở `OctopusRigAnimator.Act` (xúc tu) và `SpiderRigAnimator.Act` (chân nhện). Chiêu chỉ gọi
`ctx.Act(CombatPose.Whip / Slam / Spin / Reach / Rear / Lunge / Squash, thời gian, điểm nhắm)`, mỗi con tự diễn theo kiểu riêng.
Mỗi tư thế là một chuỗi khung khoá: lấy đà → ra đòn nhanh → vung quá đà → thu về, chuyển mượt từ dáng đang có sang
dáng đánh và ngược lại. Ảnh mẫu từng chiêu (vẽ bằng ChatGPT) nằm ở `Docs/ky_nang/mau_chatgpt/`.

**Thêm hoặc đổi một chiêu:** viết class mới kế thừa `Skill` trong `CharacterSkills.cs`, làm việc trong hàm `Run`
(xem các chiêu có sẵn làm mẫu). Sau đó đặt chiêu vào đúng vị trí trong `DefaultSkills.Octopus()` / `Spider()`, thứ tự là
nút 1 (chạm, →2, →3), nút 2 (chạm, →1, →3), nút 3 (chạm, →1, →2). Cuối cùng chạy menu **Octopus › Dựng lại màn chơi**.
