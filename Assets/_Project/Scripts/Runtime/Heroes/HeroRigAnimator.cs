using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Hoạt ảnh gắn xương của 3 nhân vật người (Art/Heroes/&lt;Red|Pink|Blue&gt;, do Tools/art/hero_rig.py cắt từ
    /// Docs/nhan_vat_moi). Bộ điều khiển và các chiêu chỉ nói chuyện qua IHeroVisual:
    ///  - SetMove: đứng (thở, vải / tóc lay), chạy (chân đặt trên sàn bằng IK 2 xương, có nhịp bay; nghiêng người theo tốc
    ///    độ), nhảy / nhảy đúp (co chân), rơi (tay hơi giơ), lượn, đáp (nhún), lướt, trúng đòn. Vải, tóc, lửa bay theo gió
    ///    ngược chiều chạy / rơi.
    ///  - Act: mỗi dáng là chuỗi mốc (lấy đà → ra đòn → quá đà → về). Góc tay + hướng vũ khí nội suy theo góc nên mũi vũ khí
    ///    đi theo cung như vung thật. Đòn mới đi ra từ đúng dáng đang hiện; hết đòn thì hoà về dáng di chuyển.
    ///    Xích Ảnh (kiếm) theo các tấm dáng trong Docs/nhan_vat_moi/blue/{basic_attack,skill,special_skill}: thủ thế thấp,
    ///    kiếm cầm 2 tay chĩa xuống trước; Whip = giơ kiếm qua đầu rồi chém ngang (nhắm cao: chém hất từ dưới lên); WhipAlt =
    ///    quét ngược tay từ sau ra trước thấp; Slam = bổ 2 tay từ trên đầu xuống đất; Spin = xoay người một vòng, kiếm ngang;
    ///    Reach = đâm thẳng; Rear = giơ chéo kiếm lên cao; Lunge = lao thấp kéo kiếm sau lưng rồi chém tới; Squash = cắm kiếm
    ///    xuống đất, 2 tay tì chuôi (thế thủ). Tử Tinh (trượng) theo Docs/nhan_vat_moi/pink: Whip = kéo trượng ra sau rồi
    ///    đâm 2 tay tới trước (nhắm cao: hất trượng lên), WhipAlt = quét vòng dưới, Slam = giơ trượng rồi dộng xuống đất,
    ///    Spin = xoay người, Reach = chĩa trượng 2 tay phóng phép, Rear = giơ trượng quá đầu, Lunge = lao thấp chĩa trượng,
    ///    Squash = cắm trượng xuống đất. Lam Hồn (lưỡi hái) theo Docs/nhan_vat_moi/blue: Whip = giơ cao rồi quét ngang (nhắm
    ///    cao: hất lên), WhipAlt = quét vòng dưới, Slam = bổ xuống đất, Spin = xoay người, Reach = vung xuống chĩa tới (phóng
    ///    sóng), Rear = giơ lưỡi hái lên cao, Lunge = lao thấp rồi bổ chéo, Squash = dựng lưỡi hái, tay lửa đưa ra trước.
    ///  - Facing: quay đầu trong turnTime, đổi chiều ngay giữa lượt (chỉ co nhẹ, không bóp thành sợi chỉ).
    ///  - Flash / SetGhost / SetWeaponPower: chớp trắng → đỏ, trong mờ ngả xanh (nhấp nháy nhẹ), vũ khí to ra + rực sáng.
    /// Góc trong code: độ, trong hệ Body (0 = phía trước mặt, 90 = lên trên, -90 = xuống), đã tính lật khi quay trái.
    /// Không Play vẫn xem được: chọn Preview rồi bấm ▶ Chạy thử trong Inspector.
    /// </summary>
    [ExecuteAlways]
    public class HeroRigAnimator : MonoBehaviour, IHeroVisual
    {
        /// <summary>Kiểu vũ khí: Kiếm (Xích Ảnh), Trượng (Tử Tinh), Lưỡi hái (Lam Hồn).</summary>
        public enum Style { Sword, Staff, Scythe }

        [Serializable]
        public class Limb
        {
            [Tooltip("Khớp đầu (vai / háng): trục +x dọc đoạn trên")]
            public Transform upper;
            [Tooltip("Khớp giữa (khuỷu / gối): trục +x dọc đoạn dưới")]
            public Transform lower;
            [Tooltip("Đầu chi: chỗ cầm của bàn tay (tay gần: trục +x là trục vũ khí) / đế giày")]
            public Transform end;
            [Tooltip("Ảnh đoạn trên / dưới (tay: kéo dài dọc xương khi vươn xa)")]
            public Transform upperArt, lowerArt;
            [Tooltip("Đoạn trên dài (unit tranh)")]
            public float upperLength;
            [Tooltip("Đoạn dưới dài (unit tranh)")]
            public float lowerLength;
            [Tooltip("Chân: đế giày khi đứng thẳng như tranh (Body, sàn y = 0)")]
            public Vector2 rest;
        }

        [Serializable]
        public class Sway
        {
            public Transform bone;
            [Tooltip("Góc gốc (độ, so với cha)")]
            public float rest;
            [Tooltip("Gió / quán tính làm lệch bao nhiêu độ (gió mạnh = 1)")]
            public float wind = 28f;
            [Tooltip("Lay nhẹ khi đứng (độ)")]
            public float sway = 3f;
            [Tooltip("Lệch pha (xương xa gốc lay trễ hơn)")]
            public float phase;
        }

        [Serializable]
        public class Rig
        {
            public Transform pose;
            public Transform body;
            public Transform hips;
            public Transform chest;
            public Transform neck;
            [Tooltip("Góc nghỉ (độ, local z) của Hips / Chest / Neck")]
            public float hipsAngle, chestAngle, neckAngle;
            public Vector2 hipsRest;
            public Limb armNear = new Limb(), armFar = new Limb(), legNear = new Limb(), legFar = new Limb();
            [Tooltip("Vũ khí (con của bàn tay gần, gốc ở chỗ cầm, trục +x = trục vũ khí): to ra khi rực")]
            public Transform weapon;
            public Transform weaponTip;
            [Tooltip("Chỗ tay xa nắm khi cầm vũ khí 2 tay")]
            public Transform offGrip;
            public Transform castPoint;
            public SpriteRenderer weaponArt;
            [Tooltip("Quầng sáng dọc lưỡi vũ khí (khi rực)")]
            public SpriteRenderer weaponGlow;
            [Tooltip("Quầng sáng nhỏ ở điểm phóng phép (pha lê trượng / lửa hồn)")]
            public SpriteRenderer castGlow;
            [Tooltip("Thứ tự vẽ vũ khí: thường (trước đầu), sau đầu (giơ ra sau đầu), sau thân (xoay người, ở phía xa)")]
            public int weaponOrder, weaponOrderBehindHead, weaponOrderBehindBody;
            [Tooltip("Vạt áo, khăn, áo choàng (con của Hips / Chest)")]
            public Sway[] cloth = Array.Empty<Sway>();
            [Tooltip("Tóc quanh đầu")]
            public Sway[] hair = Array.Empty<Sway>();
            [Tooltip("Tóc dài sau lưng (Tử Tinh): chuỗi từ gáy xuống")]
            public Sway[] longHair = Array.Empty<Sway>();
            [Tooltip("Lửa hồn trên tay xa (Lam Hồn): chuỗi từ lòng bàn tay lên, luôn hướng lên trời")]
            public Sway[] flame = Array.Empty<Sway>();
            [Tooltip("Mọi lớp ảnh (để chớp / hoá ma)")]
            public SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
            [Tooltip("Các lớp rực lên theo SetWeaponPower (vũ khí, lửa hồn): dùng vật liệu riêng")]
            public SpriteRenderer[] weaponRenderers = Array.Empty<SpriteRenderer>();
            [Tooltip("Các điểm bao quanh người (không tính vũ khí) để tính VisualBounds")]
            public Transform[] boundPoints = Array.Empty<Transform>();
        }

        [Header("Xem trước khi không Play (bấm ▶ Chạy thử trong Inspector)")]
        [SerializeField] HeroMove preview = HeroMove.Idle;
        [Tooltip("Tốc độ khi xem chạy / lướt (unit world / giây)")]
        [SerializeField] float previewSpeed = 6f;
        [SerializeField] bool previewLeft;

        [Header("Nhân vật")]
        [SerializeField] Style style = Style.Sword;
        [Tooltip("Tỉ lệ unit tranh → world (HeroRigBuilder đặt theo chiều cao)")]
        [SerializeField] float size = 1f;
        [Tooltip("Chiều cao nhân vật (unit tranh, đỉnh tóc → sàn)")]
        [SerializeField] float rigHeight = 2f;
        [Tooltip("Cầm vũ khí 2 tay ở các dáng có cầm 2 tay (tắt = luôn cầm một tay)")]
        [SerializeField] bool twoHanded = true;
        [Tooltip("Chỗ đặt 2 đế giày khi đứng (x trong Body: chân gần, chân xa)")]
        [SerializeField] Vector2 stance = new Vector2(-0.44f, 0.46f);
        [Tooltip("Thời gian quay đầu khi đổi hướng (giây)")]
        [SerializeField, Range(0.08f, 0.5f)] float turnTime = 0.2f;
        [Tooltip("Quay đầu: đổi chiều ngay giữa lượt, quanh lúc đổi thân chỉ co lại bấy nhiêu phần")]
        [SerializeField, Range(0f, 0.5f)] float turnSquash = 0.22f;

        [Header("Chạy")]
        [Tooltip("Tốc độ (unit tranh / giây) mà từ đó chạy hết cỡ")]
        [SerializeField] float runFull = 2.2f;
        [Tooltip("Thân đi được ít nhất bấy nhiêu (unit tranh) mỗi chu kỳ 2 bước")]
        [SerializeField, Min(0.3f)] float runCycle = 1.2f;
        [Tooltip("Nhịp chạy tối đa (chu kỳ / giây); chạy nhanh hơn thì sải dài ra")]
        [SerializeField] float runCadence = 3.2f;
        [Tooltip("Phần chu kỳ bàn chân chạm đất (phần còn lại có lúc cả 2 chân rời đất)")]
        [SerializeField, Range(0.2f, 0.7f)] float runStance = 0.38f;
        [Tooltip("Sải chân dài nhất (unit tranh); dài hơn thì chân trượt chút ít")]
        [SerializeField] float runMaxStride = 0.62f;
        [Tooltip("Nhấc chân (unit tranh)")]
        [SerializeField, Range(0f, 0.5f)] float runLift = 0.2f;
        [Tooltip("Gót đá lên sau lưng (unit tranh)")]
        [SerializeField, Range(0f, 0.5f)] float runKick = 0.16f;
        [Tooltip("Nghiêng người khi chạy hết cỡ (độ)")]
        [SerializeField, Range(0f, 30f)] float runLean = 14f;
        [Tooltip("Thân nhún mỗi bước (unit tranh)")]
        [SerializeField, Range(0f, 0.2f)] float runBob = 0.045f;
        [Tooltip("Tay vung theo bước (độ)")]
        [SerializeField, Range(0f, 80f)] float runArmSwing = 50f;

        [Header("Đứng, nhảy, đáp")]
        [SerializeField, Range(0f, 0.05f)] float breathBob = 0.012f;
        [SerializeField] float breathSpeed = 2.4f;
        [Tooltip("Đáp đất: nhún sâu nhất (unit tranh) khi rơi mạnh")]
        [SerializeField, Range(0f, 0.3f)] float landDip = 0.13f;
        [SerializeField, Range(0.08f, 0.5f)] float landTime = 0.24f;
        [Tooltip("Đổi dáng di chuyển (chạy ↔ nhảy ↔ rơi…) hoà trong bấy nhiêu giây")]
        [SerializeField, Range(0.03f, 0.3f)] float moveBlend = 0.11f;

        [Header("Vải, tóc")]
        [Tooltip("Gió ngược chiều chạy / rơi: unit tranh / giây → độ mạnh gió")]
        [SerializeField, Range(0f, 0.5f)] float windScale = 0.16f;
        [Tooltip("Gió theo kịp vận tốc nhanh cỡ nào (lò xo; thấp = trễ, lắc lâu)")]
        [SerializeField, Range(5f, 200f)] float windSpring = 70f;
        [SerializeField, Range(1f, 30f)] float windDamping = 9f;

        [Header("Ra đòn")]
        [Tooltip("Hết đòn: hoà về dáng di chuyển trong bấy nhiêu giây")]
        [SerializeField, Range(0.05f, 0.5f)] float settleTime = 0.2f;
        [Tooltip("Tay / vũ khí xoay theo hướng nhắm tối đa (độ)")]
        [SerializeField, Range(0f, 85f)] float maxAim = 65f;
        [Tooltip("Whip nhắm cao hơn góc này (độ) thì chém hất từ dưới lên")]
        [SerializeField, Range(10f, 80f)] float riseAim = 28f;

        [Header("Chớp, hoá ma, vũ khí rực")]
        [SerializeField, Range(0.05f, 0.6f)] float flashTime = 0.2f;
        [SerializeField] Color flashWhite = Color.white;
        [SerializeField] Color flashRed = new Color(1f, 0.2f, 0.15f);
        [SerializeField] Color ghostColor = new Color(0.45f, 0.75f, 1f);
        [Tooltip("Màu rực của vũ khí")]
        [SerializeField] Color glowColor = new Color(1f, 0.15f, 0.1f);
        [Tooltip("Vũ khí rực hẳn thì to ra bấy nhiêu lần")]
        [SerializeField, Range(1f, 2f)] float powerScale = 1.4f;

        [Header("Tham chiếu (đã nối sẵn, không cần sửa)")]
        [SerializeField] Rig rig = new Rig();

        // ---------- dữ liệu dáng ----------

        struct ArmPose
        {
            public float Upper;   // góc tay trên (Body)
            public float Bend;    // khuỷu gập (cẳng tay so với tay trên, + = ngược chiều kim đồng hồ)
            public float Blade;   // hướng vũ khí (Body): từ chỗ cầm tới mũi trục vũ khí

            public ArmPose(float upper, float bend, float blade)
            {
                Upper = upper;
                Bend = bend;
                Blade = blade;
            }

            public static ArmPose Mix(in ArmPose a, in ArmPose b, float t) => new ArmPose(
                Mathf.LerpUnclamped(a.Upper, b.Upper, t), Mathf.LerpUnclamped(a.Bend, b.Bend, t),
                Mathf.LerpUnclamped(a.Blade, b.Blade, t));

            /// <summary>Cùng dáng, đổi góc sang vòng gần reference nhất (để nội suy không quay vòng dài).</summary>
            public ArmPose Near(in ArmPose reference) => new ArmPose(Closest(Upper, reference.Upper), Bend,
                Closest(Blade, reference.Blade));
        }

        struct Look
        {
            public Vector2 Offset;     // dời hông (unit tranh)
            public float Lean;         // cả người chúi tới (+) / ngả ra sau (−) quanh hông (độ)
            public float Bend;         // ngực gập thêm tới trước (độ)
            public float Head;         // đầu cúi (+) / ngẩng (−) (độ)
            public float Squash;       // + bẹp xuống, − vươn cao
            public float Stretch;      // + kéo dài ngang (lướt)
            public float Flutter;      // vải / tóc bay mạnh thêm (0..1)
            public ArmPose Near, Far;
            public float Reach;        // tay vươn dài ra (1 = như tranh)
            public float Two;          // cầm vũ khí 2 tay (0..1)
            public Vector2 GroundN, GroundF;   // đế giày đặt trên sàn (Body)
            public Vector2 RelN, RelF;         // đế giày so với háng (phần chiều dài chân)
            public float Plant;                // 1 = chân đặt trên sàn, 0 = chân theo háng

            public static Look Mix(in Look a, in Look b, float t) => new Look
            {
                Offset = Vector2.LerpUnclamped(a.Offset, b.Offset, t),
                Lean = Mathf.LerpUnclamped(a.Lean, b.Lean, t),
                Bend = Mathf.LerpUnclamped(a.Bend, b.Bend, t),
                Head = Mathf.LerpUnclamped(a.Head, b.Head, t),
                Squash = Mathf.LerpUnclamped(a.Squash, b.Squash, t),
                Stretch = Mathf.LerpUnclamped(a.Stretch, b.Stretch, t),
                Flutter = Mathf.Lerp(a.Flutter, b.Flutter, t),
                Near = ArmPose.Mix(a.Near, b.Near.Near(a.Near), t),
                Far = ArmPose.Mix(a.Far, b.Far.Near(a.Far), t),
                Reach = Mathf.Lerp(a.Reach, b.Reach, t),
                Two = Mathf.Lerp(a.Two, b.Two, t),
                GroundN = Vector2.LerpUnclamped(a.GroundN, b.GroundN, t),
                GroundF = Vector2.LerpUnclamped(a.GroundF, b.GroundF, t),
                RelN = Vector2.LerpUnclamped(a.RelN, b.RelN, t),
                RelF = Vector2.LerpUnclamped(a.RelF, b.RelF, t),
                Plant = Mathf.Lerp(a.Plant, b.Plant, t),
            };
        }

        // Phần đòn: thân thay dáng đứng riêng của nhân vật rồi cộng vào dáng di chuyển, tay + vũ khí thay hẳn
        struct ActLook
        {
            public Vector2 Offset;
            public float Lean, Bend, Head, Squash;
            public float FootN, FootF;  // dời đế giày gần / xa (unit tranh, khi đứng trên sàn)
            public ArmPose Near, Far;
            public float Reach, Two;

            public static ActLook Mix(in ActLook a, in ActLook b, float t) => new ActLook
            {
                Offset = Vector2.LerpUnclamped(a.Offset, b.Offset, t),
                Lean = Mathf.LerpUnclamped(a.Lean, b.Lean, t),
                Bend = Mathf.LerpUnclamped(a.Bend, b.Bend, t),
                Head = Mathf.LerpUnclamped(a.Head, b.Head, t),
                Squash = Mathf.LerpUnclamped(a.Squash, b.Squash, t),
                FootN = Mathf.LerpUnclamped(a.FootN, b.FootN, t),
                FootF = Mathf.LerpUnclamped(a.FootF, b.FootF, t),
                Near = ArmPose.Mix(a.Near, b.Near, t),
                Far = ArmPose.Mix(a.Far, b.Far, t),
                Reach = Mathf.LerpUnclamped(a.Reach, b.Reach, t),
                Two = Mathf.Lerp(a.Two, b.Two, t),
            };
        }

        // InOut: êm hai đầu. Out: nhanh rồi chậm dần. Snap: vụt cực nhanh, phanh gấp (ra đòn). In: chậm rồi nhanh dần
        enum Ease : byte { Linear, InOut, Out, Snap, In }

        readonly struct ActKey
        {
            public readonly float K;
            public readonly Ease Ease;
            public readonly ActLook Look;
            public readonly float Aim;      // tay gần + vũ khí xoay theo hướng nhắm bấy nhiêu phần
            public readonly float AimFar;   // tay xa xoay theo hướng nhắm (Lam Hồn phóng lửa)
            public readonly float Turn;     // mốc đầu: tay gần + vũ khí đi tới theo chiều này (+1 ngược kim đồng hồ = vòng
                                            // lên phía trước, -1 thuận, 0 = vòng gần nhất)

            public ActKey(float k, Ease ease, in ActLook look, float aim, float aimFar, float turn)
            {
                K = k;
                Ease = ease;
                Look = look;
                Aim = aim;
                AimFar = aimFar;
                Turn = turn;
            }
        }

        // Dáng di chuyển theo nhân vật: tay gần (cầm vũ khí), tay xa; dáng đứng (dời hông, chúi, gập ngực, cúi đầu)
        struct MoveSet
        {
            public ArmPose IdleN, IdleF, RunN, RunF, JumpN, JumpF, FallN, FallF, GlideN, GlideF, DashN, DashF, HurtN, HurtF;
            public float IdleY, IdleLean, IdleBend, IdleHead, IdleTwo;
            public float DashY, DashLean, DashHead;
        }

        static ArmPose A(float upper, float bend, float blade = 0f) => new ArmPose(upper, bend, blade);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static readonly MoveSet[] Sets =
        {
            // Kiếm (Xích Ảnh): thủ thế thấp chân dạng rộng, 2 tay cầm kiếm ở hông chĩa xuống trước (tấm Đánh thường khung
            // 1 / 6); chạy / lướt kéo kiếm thấp sau lưng, tay kia nắm đấm vươn tới (tấm Đánh thường áp sát khung 2–3)
            new MoveSet
            {
                IdleN = A(-95f, 75f, -26f), IdleF = A(-80f, 60f), IdleY = -0.17f, IdleLean = 14f, IdleBend = 4f,
                IdleHead = 6f, IdleTwo = 1f,
                RunN = A(-118f, 15f, -146f), RunF = A(-50f, 90f),
                JumpN = A(-135f, 35f, -150f), JumpF = A(30f, 60f),
                FallN = A(-25f, 30f, -30f), FallF = A(110f, 25f),
                GlideN = A(-25f, 30f, -30f), GlideF = A(130f, 25f),
                DashN = A(-100f, 10f, -138f), DashF = A(-20f, 100f), DashY = -0.3f, DashLean = 40f, DashHead = 4f,
                HurtN = A(35f, 50f, 125f), HurtF = A(80f, 40f),
            },
            // Trượng (Tử Tinh): đứng chân dạng, trượng dựng đứng trước người (tấm đánh thường khung 1 / 6); chạy chĩa trượng
            // chếch tới
            new MoveSet
            {
                IdleN = A(-55f, 75f, 92f), IdleF = A(-95f, 30f), IdleY = -0.06f, IdleLean = 4f, IdleBend = 2f, IdleHead = 2f,
                RunN = A(-90f, 110f, 70f), RunF = A(-95f, 50f),
                JumpN = A(-60f, 115f, 100f), JumpF = A(40f, 35f),
                FallN = A(-30f, 95f, 108f), FallF = A(115f, 20f),
                GlideN = A(-20f, 60f, 70f), GlideF = A(140f, 25f),
                DashN = A(-150f, 25f, -176f), DashF = A(-150f, 20f), DashY = -0.06f, DashLean = 30f, DashHead = -16f,
                HurtN = A(30f, 60f, 130f), HurtF = A(80f, 40f),
            },
            // Lưỡi hái (Lam Hồn): đứng dựng lưỡi hái một tay, tay kia nâng lửa hồn (tấm dùng chiêu khung 1); chạy kéo lưỡi hái
            new MoveSet
            {
                IdleN = A(-112f, 25f, 96f), IdleF = A(-25f, 45f), IdleY = -0.03f, IdleLean = 2f,
                RunN = A(-125f, 35f, -160f), RunF = A(-40f, 50f),
                JumpN = A(-110f, 40f, 135f), JumpF = A(20f, 50f),
                FallN = A(-50f, 40f, 110f), FallF = A(100f, 30f),
                GlideN = A(-50f, 40f, 110f), GlideF = A(100f, 30f),
                DashN = A(-170f, 10f, -178f), DashF = A(-165f, 10f), DashY = -0.06f, DashLean = 30f, DashHead = -16f,
                HurtN = A(30f, 50f, 125f), HurtF = A(80f, 40f),
            },
        };

        // ---------- bảng ra đòn ----------
        // Mỗi mốc: k (phần thời gian đòn), kiểu nội suy, tay gần (trên, khuỷu, vũ khí), tay xa, rồi dáng thân: dời hông x / y,
        // chúi, gập ngực, cúi đầu, bẹp, dời 2 đế giày, tay vươn dài, cầm 2 tay, mức nhắm. Góc trong cùng bảng nội suy thẳng
        // (ghi góc liên tục, vd 175 → 342 = quét qua phía dưới); mốc đầu đi ra từ dáng đang hiện theo vòng gần nhất.
        // Mốc cuối = hết quá đà; sau đó hoà về dáng di chuyển trong settleTime.

        static ActKey K(float k, Ease ease, ArmPose near, ArmPose far, float x = 0f, float y = 0f, float lean = 0f,
            float bend = 0f, float head = 0f, float squash = 0f, float footN = 0f, float footF = 0f, float reach = 1f,
            float two = 0f, float aim = 0f, float aimFar = 0f, float turn = 0f) =>
            new ActKey(k, ease, new ActLook
            {
                Offset = new Vector2(x, y), Lean = lean, Bend = bend, Head = head, Squash = squash, FootN = footN,
                FootF = footF, Near = near, Far = far, Reach = reach, Two = two,
            }, aim, aimFar, turn);

        // ===== Xích Ảnh (kiếm), theo tấm dáng =====

        // Đánh thường khung 2 → 3: giơ kiếm 2 tay lên quá đầu (lưỡi ngả ra sau) → chém ngang tới trước, 2 tay duỗi thẳng
        static readonly ActKey[] SwordWhip =
        {
            K(0.3f, Ease.InOut, A(108f, 5f, 162f), A(100f, 10f), y: -0.1f, lean: -6f, bend: -4f, head: -2f, reach: 1.45f, two: 1f,
                turn: 1f),
            K(0.44f, Ease.Snap, A(-5f, 0f, 0f), A(-15f, 15f), y: -0.17f, lean: 16f, bend: 6f, head: 2f, footN: -0.06f,
                reach: 1.15f, two: 1f, aim: 1f),
            K(0.68f, Ease.Out, A(-22f, 0f, -18f), A(-30f, 15f), y: -0.18f, lean: 18f, bend: 6f, head: 3f, footN: -0.06f,
                reach: 1.1f, two: 0.9f, aim: 0.5f),
            K(1f, Ease.InOut, A(-60f, 40f, -25f), A(-70f, 50f), y: -0.17f, lean: 15f, bend: 5f, head: 5f, footN: -0.03f,
                two: 1f, aim: 0.2f),
        };

        // Đánh thường hất tung khung 2 → 3 → 4: khuỵu thấp, mũi kiếm quệt đất phía sau → chém hất từ dưới lên trước, người
        // bật lên ngửa ra → kiếm dựng đứng trên đầu
        static readonly ActKey[] SwordWhipRise =
        {
            K(0.28f, Ease.InOut, A(-110f, 50f, -135f), A(-100f, 40f), y: -0.26f, lean: 28f, bend: 10f, footN: 0.04f,
                footF: -0.04f, two: 1f),
            K(0.44f, Ease.Snap, A(32f, 0f, 60f), A(25f, 15f), y: 0.1f, lean: -14f, bend: -6f, head: -6f, reach: 1.3f,
                two: 0.5f, aim: 0.6f),
            K(0.7f, Ease.Out, A(62f, 0f, 95f), A(55f, 10f), y: 0.12f, lean: -10f, bend: -4f, head: -6f, reach: 1.35f,
                two: 1f, aim: 0.3f),
            K(1f, Ease.InOut, A(40f, 30f, 80f), A(30f, 30f), y: 0.02f, lean: -2f, reach: 1.15f, two: 1f),
        };

        // Đánh thường khung 4: kéo kiếm ngang ra sau lưng → quét ngược qua phía dưới ra trước, dừng chĩa xuống trước
        static readonly ActKey[] SwordWhipAlt =
        {
            K(0.3f, Ease.InOut, A(-150f, 40f, 175f), A(-30f, 60f), y: -0.14f, lean: 4f, bend: -4f, head: 2f, footN: -0.02f,
                reach: 1.1f),
            K(0.44f, Ease.Snap, A(-30f, 0f, 342f), A(-45f, 20f), y: -0.18f, lean: 20f, bend: 6f, head: 6f, footN: -0.04f,
                footF: -0.02f, reach: 1.15f, two: 0.7f, aim: 1f),
            K(0.68f, Ease.Out, A(-45f, 0f, 325f), A(-55f, 20f), y: -0.19f, lean: 22f, bend: 6f, head: 6f, footN: -0.04f,
                reach: 1.1f, two: 0.8f, aim: 0.5f),
            K(1f, Ease.InOut, A(-80f, 55f, 333f), A(-80f, 55f), y: -0.17f, lean: 16f, bend: 5f, head: 5f, two: 1f, aim: 0.2f),
        };

        // Đánh thường khung 2 → 5: giơ kiếm 2 tay quá đầu → bổ thẳng xuống đất trước mặt (tay vẫn trên cao, mũi chạm sàn)
        static readonly ActKey[] SwordSlam =
        {
            K(0.32f, Ease.InOut, A(108f, 5f, 165f), A(100f, 10f), y: -0.08f, lean: -8f, bend: -6f, head: -4f, reach: 1.45f,
                two: 1f, turn: 1f),
            K(0.46f, Ease.Snap, A(60f, 0f, -60f), A(58f, 5f), y: -0.18f, lean: 22f, bend: 10f, head: 6f, footN: -0.02f,
                footF: -0.04f, reach: 1.4f, two: 1f),
            K(0.7f, Ease.Linear, A(56f, 0f, -64f), A(54f, 5f), y: -0.2f, lean: 24f, bend: 10f, head: 7f, footN: -0.02f,
                footF: -0.04f, reach: 1.38f, two: 1f),
            K(1f, Ease.InOut, A(10f, 30f, -50f), A(0f, 40f), y: -0.15f, lean: 14f, bend: 5f, head: 5f, reach: 1.1f, two: 1f),
        };

        // Kĩ năng khung 2 → 3–4: hạ thấp, kiếm chĩa xuống trước → xoay người một vòng (SpinFrom → SpinTo), kiếm duỗi ngang
        static readonly ActKey[] SwordSpin =
        {
            K(0.2f, Ease.InOut, A(-55f, 20f, -12f), A(-140f, 40f), y: -0.2f, lean: 26f, bend: 6f, head: -10f, footN: -0.06f,
                footF: -0.04f),
            K(0.3f, Ease.Out, A(-15f, 0f, -5f), A(-150f, 30f), y: -0.14f, lean: 6f, footN: -0.04f, reach: 1.1f),
            K(0.78f, Ease.Linear, A(-15f, 0f, -5f), A(-150f, 30f), y: -0.14f, lean: 6f, footN: -0.04f, reach: 1.1f),
            K(1f, Ease.InOut, A(-60f, 40f, -20f), A(-90f, 50f), y: -0.16f, lean: 12f, bend: 4f, head: 4f, two: 1f),
        };

        // Kĩ năng đặc biệt áp chế khung 1: rút kiếm về sát ngực → đâm thẳng tới điểm nhắm, tay kia kéo ra sau
        static readonly ActKey[] SwordReach =
        {
            K(0.28f, Ease.InOut, A(-150f, 140f, 5f), A(-40f, 60f), y: -0.12f, lean: -6f, bend: -4f, footN: 0.02f, aim: 0.6f),
            K(0.42f, Ease.Snap, A(2f, 0f, 8f), A(-115f, 60f), y: -0.15f, lean: 8f, bend: 2f, head: 2f, footN: -0.06f,
                footF: -0.02f, reach: 1.15f, aim: 1f),
            K(0.72f, Ease.Linear, A(2f, 0f, 8f), A(-115f, 60f), y: -0.15f, lean: 8f, bend: 2f, head: 2f, footN: -0.06f,
                footF: -0.02f, reach: 1.15f, aim: 1f),
            K(1f, Ease.InOut, A(-40f, 40f, -15f), A(-90f, 50f), y: -0.15f, lean: 12f, bend: 4f, head: 4f, two: 0.5f, aim: 0.4f),
        };

        // Kĩ năng tầm xa khung 2: giơ chéo kiếm lên cao phía trước bằng 2 tay (vận sức) → giữ, run nhẹ
        static readonly ActKey[] SwordRear =
        {
            K(0.35f, Ease.Out, A(42f, 0f, 56f), A(35f, 10f), y: -0.1f, lean: -6f, bend: -4f, head: -4f, reach: 1.25f, two: 1f),
            K(1f, Ease.Linear, A(44f, 0f, 58f), A(37f, 10f), y: -0.1f, lean: -7f, bend: -5f, head: -5f, reach: 1.27f, two: 1f),
        };

        // Đánh thường áp sát khung 2–3 → 4: lao người thật thấp, kiếm kéo sau lưng → chém ngang tới trước, chân choãi rộng
        static readonly ActKey[] SwordLunge =
        {
            K(0.22f, Ease.InOut, A(-105f, 10f, -142f), A(-20f, 100f), y: -0.34f, lean: 40f, bend: 8f, head: 10f,
                footN: -0.28f, footF: -0.1f),
            K(0.38f, Ease.Snap, A(-2f, 0f, 2f), A(-160f, 20f), y: -0.3f, lean: 26f, bend: 6f, footN: -0.28f, footF: 0.04f,
                reach: 1.2f, aim: 1f),
            K(0.7f, Ease.Out, A(-12f, 0f, -8f), A(-160f, 20f), y: -0.3f, lean: 24f, bend: 6f, footN: -0.28f, footF: 0.04f,
                reach: 1.15f, aim: 0.6f),
            K(1f, Ease.InOut, A(-60f, 40f, -25f), A(-90f, 50f), y: -0.2f, lean: 16f, bend: 4f, head: 4f, footN: -0.1f,
                two: 0.5f),
        };

        // Kĩ năng thủ: nhấc kiếm → cắm thẳng xuống đất trước mặt (lưỡi lút xuống sàn), 2 tay tì chuôi ngang ngực → giữ → rút
        static readonly ActKey[] SwordSquash =
        {
            K(0.16f, Ease.Out, A(30f, 40f, -80f), A(20f, 40f), y: -0.06f, lean: 2f, footN: 0.04f, footF: -0.2f, reach: 1.1f,
                two: 1f),
            K(0.3f, Ease.Snap, A(-4f, 0f, -90f), A(-6f, 5f), y: -0.1f, lean: 6f, bend: 2f, head: 4f, footN: 0.08f,
                footF: -0.28f, reach: 1.25f, two: 1f),
            K(0.85f, Ease.Linear, A(-4f, 0f, -90f), A(-6f, 5f), y: -0.1f, lean: 6f, bend: 2f, head: 4f, footN: 0.08f,
                footF: -0.28f, reach: 1.25f, two: 1f),
            K(1f, Ease.InOut, A(-40f, 40f, -60f), A(-50f, 40f), y: -0.12f, lean: 10f, bend: 3f, head: 5f, footN: 0.04f,
                footF: -0.14f, two: 1f),
        };

        // Xoay người (Spin): quay một vòng quanh trục đứng trong khoảng k này
        const float SpinFrom = 0.25f, SpinTo = 0.8f;

        // ===== Tử Tinh (trượng), theo tấm dáng Docs/nhan_vat_moi/pink =====

        // đánh thường khung 2 → 3: kéo trượng 2 tay ra sau (pha lê chếch lên sau đầu) → đâm trượng thẳng tới trước
        static readonly ActKey[] StaffWhip =
        {
            K(0.3f, Ease.InOut, A(-90f, 150f, 150f), A(-40f, 80f), y: -0.12f, lean: -6f, bend: -4f, footN: -0.04f,
                footF: 0.04f, two: 1f, turn: 1f),
            K(0.44f, Ease.Snap, A(-5f, 0f, 0f), A(-20f, 20f), y: -0.12f, lean: 12f, bend: 4f, head: 2f, footN: -0.12f,
                footF: 0.1f, reach: 1.15f, two: 1f, aim: 1f),
            K(0.7f, Ease.Linear, A(-8f, 0f, -3f), A(-22f, 20f), y: -0.12f, lean: 12f, bend: 4f, head: 2f, footN: -0.12f,
                footF: 0.1f, reach: 1.12f, two: 1f, aim: 0.8f),
            K(1f, Ease.InOut, A(-50f, 60f, 40f), A(-60f, 50f), y: -0.08f, lean: 6f, footN: -0.06f, footF: 0.05f, two: 0.5f,
                aim: 0.2f),
        };

        // Đánh thường hất tung khung 1 → 3 → 4: khom thấp, pha lê quệt sát đất phía trước → vung trượng hất lên một tay →
        // trượng dựng thẳng trên cao, người kiễng lên
        static readonly ActKey[] StaffWhipRise =
        {
            K(0.28f, Ease.InOut, A(-55f, 20f, -25f), A(-80f, 60f), y: -0.2f, lean: 16f, bend: 6f, head: 2f, footN: -0.12f,
                footF: 0.12f, two: 1f),
            K(0.44f, Ease.Snap, A(55f, 10f, 68f), A(-110f, 30f), y: -0.06f, lean: -8f, bend: -4f, head: -4f, footF: -0.08f,
                reach: 1.15f, aim: 0.5f),
            K(0.7f, Ease.Out, A(78f, 0f, 88f), A(-120f, 30f), y: 0.02f, lean: -10f, bend: -6f, head: -8f, footN: 0.05f,
                footF: -0.12f, reach: 1.2f, aim: 0.2f),
            K(1f, Ease.InOut, A(40f, 40f, 92f), A(-100f, 30f), y: -0.04f, lean: -2f, reach: 1.05f),
        };

        // Quét ngược (theo dùng chiêu áp sát khung 5 → 4): trượng giơ chếch sau lưng → quét vòng qua phía dưới ra trước, thấp
        static readonly ActKey[] StaffWhipAlt =
        {
            K(0.3f, Ease.InOut, A(140f, -10f, 140f), A(-50f, 50f), y: -0.14f, lean: 4f, head: -2f, footN: -0.08f,
                footF: 0.06f, reach: 1.1f),
            K(0.44f, Ease.Snap, A(-25f, 0f, 345f), A(-45f, 30f), y: -0.22f, lean: 22f, bend: 6f, head: -4f, footN: -0.16f,
                footF: 0.1f, reach: 1.1f, two: 1f, aim: 1f),
            K(0.7f, Ease.Out, A(-30f, 0f, 352f), A(-48f, 30f), y: -0.22f, lean: 22f, bend: 6f, head: -4f, footN: -0.16f,
                footF: 0.1f, reach: 1.1f, two: 1f, aim: 0.5f),
            K(1f, Ease.InOut, A(-50f, 60f, 380f), A(-60f, 50f), y: -0.12f, lean: 10f, footN: -0.08f, footF: 0.06f, two: 0.6f),
        };

        // Kĩ năng đặc biệt khung 3 → 4: giơ trượng thẳng lên cao, kiễng người → dộng trượng xuống đất, khuỵu thấp → giữ
        static readonly ActKey[] StaffSlam =
        {
            K(0.3f, Ease.InOut, A(105f, 0f, 108f), A(-100f, 30f), y: 0.02f, lean: -6f, bend: -4f, head: -8f, squash: -0.04f,
                reach: 1.2f),
            K(0.44f, Ease.Snap, A(-60f, 50f, 92f), A(-50f, 60f), y: -0.24f, lean: 12f, bend: 4f, head: 6f, squash: 0.08f,
                footN: -0.12f, footF: 0.08f, two: 1f),
            K(0.7f, Ease.Linear, A(-60f, 50f, 92f), A(-50f, 60f), y: -0.24f, lean: 12f, bend: 4f, head: 6f, squash: 0.05f,
                footN: -0.12f, footF: 0.08f, two: 1f),
            K(1f, Ease.InOut, A(-55f, 70f, 92f), A(-80f, 40f), y: -0.1f, lean: 6f, head: 2f, footN: -0.06f, footF: 0.04f,
                two: 0.3f),
        };

        // Kĩ năng đặc biệt khung 1 → 2: khom người, trượng 2 tay → xoay một vòng, trượng chéo trước người, tay kia dang ra
        static readonly ActKey[] StaffSpin =
        {
            K(0.2f, Ease.InOut, A(-80f, 90f, 120f), A(-60f, 60f), y: -0.12f, lean: 8f, footN: -0.08f, footF: 0.06f, two: 1f),
            K(0.3f, Ease.Out, A(-20f, 60f, 60f), A(-150f, 30f), y: -0.08f, lean: 4f, footN: -0.06f, footF: 0.04f),
            K(0.78f, Ease.Linear, A(-20f, 60f, 60f), A(-150f, 30f), y: -0.08f, lean: 4f, footN: -0.06f, footF: 0.04f),
            K(1f, Ease.InOut, A(-50f, 70f, 85f), A(-95f, 30f), y: -0.06f, lean: 4f),
        };

        // dùng chiêu tầm xa: rút trượng về sát ngực → chĩa thẳng 2 tay tới điểm nhắm (phóng phép) → giữ
        static readonly ActKey[] StaffReach =
        {
            K(0.28f, Ease.InOut, A(-120f, 140f, 20f), A(-60f, 80f), y: -0.1f, lean: -4f, footN: -0.04f, two: 1f, aim: 0.6f),
            K(0.42f, Ease.Snap, A(-5f, 0f, 0f), A(-20f, 20f), y: -0.12f, lean: 12f, bend: 4f, head: 2f, footN: -0.12f,
                footF: 0.1f, reach: 1.15f, two: 1f, aim: 1f),
            K(0.75f, Ease.Linear, A(-5f, 0f, 0f), A(-20f, 20f), y: -0.12f, lean: 12f, bend: 4f, head: 2f, footN: -0.12f,
                footF: 0.1f, reach: 1.15f, two: 1f, aim: 1f),
            K(1f, Ease.InOut, A(-30f, 40f, 10f), A(-40f, 40f), y: -0.1f, lean: 8f, footN: -0.08f, footF: 0.06f, two: 0.8f,
                aim: 0.6f),
        };

        // Kĩ năng đặc biệt tầm xa khung 2: giơ trượng lên cao quá đầu (vận phép) → giữ, run nhẹ
        static readonly ActKey[] StaffRear =
        {
            K(0.35f, Ease.Out, A(105f, 0f, 108f), A(-100f, 30f), y: -0.04f, lean: -6f, bend: -4f, head: -8f, reach: 1.2f),
            K(1f, Ease.Linear, A(107f, 0f, 110f), A(-102f, 30f), y: -0.04f, lean: -7f, bend: -5f, head: -9f, reach: 1.22f),
        };

        // dùng chiêu áp sát: khom người, trượng 2 tay ôm sau lưng → lao thấp, chĩa trượng tới → quét → trượt dừng, trượng
        // vung lên sau lưng
        static readonly ActKey[] StaffLunge =
        {
            K(0.18f, Ease.InOut, A(-150f, 120f, 140f), A(-60f, 80f), y: -0.2f, lean: 10f, bend: 4f, head: 2f, footN: -0.12f,
                footF: 0.06f, two: 1f),
            K(0.38f, Ease.Snap, A(-15f, 0f, -5f), A(-30f, 20f), y: -0.3f, lean: 30f, bend: 6f, head: -10f, footN: -0.3f,
                footF: 0.06f, reach: 1.1f, two: 1f, aim: 1f),
            K(0.62f, Ease.Out, A(-25f, 0f, -15f), A(-45f, 30f), y: -0.3f, lean: 26f, bend: 6f, head: -8f, footN: -0.3f,
                footF: 0.12f, reach: 1.1f, two: 1f, aim: 0.6f),
            K(1f, Ease.InOut, A(140f, -10f, 140f), A(-50f, 50f), y: -0.22f, lean: 18f, head: -4f, footN: -0.25f,
                footF: 0.12f, reach: 1.1f),
        };

        // Đánh thường thủ khung 3: nhấc trượng → cắm thẳng xuống đất (đầu nhọn lút sàn, pha lê trên cao), 2 tay tì trượng,
        // khom người → giữ
        static readonly ActKey[] StaffSquash =
        {
            K(0.16f, Ease.Out, A(-40f, 60f, 92f), A(-40f, 60f), y: -0.06f, lean: 2f, footN: -0.04f, two: 1f),
            K(0.3f, Ease.Snap, A(-60f, 50f, 92f), A(-50f, 60f), y: -0.2f, lean: 10f, bend: 4f, head: 6f, footN: -0.12f,
                footF: 0.06f, two: 1f),
            K(0.85f, Ease.Linear, A(-60f, 50f, 92f), A(-50f, 60f), y: -0.2f, lean: 10f, bend: 4f, head: 6f, footN: -0.12f,
                footF: 0.06f, two: 1f),
            K(1f, Ease.InOut, A(-55f, 70f, 92f), A(-80f, 40f), y: -0.08f, lean: 4f, footN: -0.05f, footF: 0.03f, two: 0.3f),
        };

        // ===== Lam Hồn (lưỡi hái), theo tấm dáng Docs/nhan_vat_moi/blue =====

        // Đánh thường khung 2 → 3 → 6: giơ lưỡi hái 2 tay lên cao ra sau → quét ngang tới trước → quá đà xuống thấp →
        // thủ thế thấp (lưỡi hái chĩa xuống trước)
        static readonly ActKey[] ScytheWhip =
        {
            K(0.3f, Ease.InOut, A(105f, 10f, 125f), A(100f, 10f), y: -0.1f, lean: -6f, bend: -4f, footN: -0.15f,
                footF: 0.15f, reach: 1.2f, two: 1f, turn: 1f),
            K(0.44f, Ease.Snap, A(-10f, 0f, 2f), A(-20f, 15f), y: -0.16f, lean: 14f, bend: 6f, head: 2f, footN: -0.2f,
                footF: 0.17f, reach: 1.15f, two: 1f, aim: 1f),
            K(0.7f, Ease.Out, A(-30f, 0f, -35f), A(-40f, 20f), y: -0.18f, lean: 16f, bend: 6f, head: 4f, footN: -0.2f,
                footF: 0.17f, reach: 1.1f, two: 1f, aim: 0.4f),
            K(1f, Ease.InOut, A(-95f, 60f, -30f), A(-80f, 60f), y: -0.18f, lean: 14f, bend: 4f, head: 4f, footN: -0.17f,
                footF: 0.17f, two: 1f),
        };

        // Đánh thường hất tung khung 2 → 3 → 4: khom thấp, lưỡi hái quét sát đất phía trước → hất vút lên trời, người bật
        // lên → lưỡi hái giơ cao trên đầu
        static readonly ActKey[] ScytheWhipRise =
        {
            K(0.28f, Ease.InOut, A(-100f, 50f, -20f), A(-90f, 60f), y: -0.2f, lean: 18f, bend: 6f, head: 4f, footN: -0.2f,
                footF: 0.17f, two: 1f),
            K(0.44f, Ease.Snap, A(60f, 10f, 110f), A(55f, 15f), lean: -6f, bend: -4f, head: -8f, footN: -0.05f,
                reach: 1.2f, two: 1f, aim: 0.5f),
            K(0.7f, Ease.Out, A(80f, 5f, 120f), A(75f, 10f), y: 0.06f, lean: -8f, bend: -4f, head: -8f, reach: 1.25f,
                two: 1f, aim: 0.2f),
            K(1f, Ease.InOut, A(50f, 20f, 110f), A(30f, 30f), lean: -2f, reach: 1.1f, two: 0.5f),
        };

        // Quét ngược: lưỡi hái kéo thấp ra sau lưng → quét vòng qua dưới thấp ra trước (dùng chiêu khung 2 → 5)
        static readonly ActKey[] ScytheWhipAlt =
        {
            K(0.3f, Ease.InOut, A(-140f, 30f, -165f), A(-110f, 40f), y: -0.2f, lean: 20f, bend: 6f, footN: -0.17f,
                footF: 0.15f, two: 1f),
            K(0.44f, Ease.Snap, A(-20f, 10f, -8f), A(-30f, 25f), y: -0.22f, lean: 22f, bend: 6f, head: -6f, footN: -0.3f,
                footF: 0.19f, reach: 1.15f, two: 1f, aim: 1f),
            K(0.7f, Ease.Out, A(-5f, 10f, 20f), A(-20f, 25f), y: -0.2f, lean: 18f, bend: 6f, head: -4f, footN: -0.3f,
                footF: 0.19f, reach: 1.1f, two: 1f, aim: 0.5f),
            K(1f, Ease.InOut, A(-60f, 40f, 40f), A(-60f, 40f), y: -0.14f, lean: 10f, footN: -0.15f, footF: 0.1f, two: 0.6f),
        };

        // Chém hành quyết / kĩ năng đặc biệt khung 5: giơ lưỡi hái lên cao → bổ thẳng xuống đất trước mặt, khuỵu thấp → giữ
        static readonly ActKey[] ScytheSlam =
        {
            K(0.32f, Ease.InOut, A(105f, 10f, 125f), A(100f, 10f), y: -0.06f, lean: -8f, bend: -6f, head: -6f, reach: 1.2f,
                two: 1f, turn: 1f),
            K(0.46f, Ease.Snap, A(-40f, 80f, -90f), A(-40f, 70f), y: -0.24f, lean: 16f, bend: 8f, head: 6f, footN: -0.15f,
                footF: 0.11f, two: 1f),
            K(0.7f, Ease.Linear, A(-40f, 80f, -90f), A(-40f, 70f), y: -0.24f, lean: 16f, bend: 8f, head: 6f, footN: -0.15f,
                footF: 0.11f, two: 1f),
            K(1f, Ease.InOut, A(-60f, 60f, -40f), A(-60f, 60f), y: -0.14f, lean: 10f, footN: -0.08f, footF: 0.06f, two: 0.6f),
        };

        // Dùng chiêu khung 2 → 3–4: lưỡi hái kéo thấp sau lưng → xoay người một vòng, lưỡi hái giơ chéo → quét thấp tới trước
        static readonly ActKey[] ScytheSpin =
        {
            K(0.2f, Ease.InOut, A(-140f, 30f, -165f), A(-110f, 40f), y: -0.2f, lean: 20f, bend: 6f, footN: -0.17f,
                footF: 0.15f, two: 1f),
            K(0.3f, Ease.Out, A(20f, 30f, 62f), A(-140f, 30f), y: -0.12f, lean: 6f, footN: -0.11f, footF: 0.09f, reach: 1.1f),
            K(0.78f, Ease.Linear, A(20f, 30f, 62f), A(-140f, 30f), y: -0.12f, lean: 6f, footN: -0.11f, footF: 0.09f,
                reach: 1.1f),
            K(1f, Ease.InOut, A(-20f, 10f, -8f), A(-30f, 25f), y: -0.2f, lean: 18f, bend: 6f, footN: -0.25f, footF: 0.17f,
                reach: 1.1f, two: 1f),
        };

        // Dùng chiêu tầm xa khung 2 → 4: giơ lưỡi hái lên cao → vung xuống chĩa thẳng tới trước bằng 2 tay (phóng sóng) → giữ
        static readonly ActKey[] ScytheReach =
        {
            K(0.25f, Ease.Out, A(88f, 0f, 94f), A(-100f, 30f), y: -0.04f, lean: -4f, head: -6f, reach: 1.2f),
            K(0.42f, Ease.Snap, A(-2f, 0f, 2f), A(-12f, 10f), y: -0.12f, lean: 10f, bend: 2f, footN: -0.17f, footF: 0.15f,
                reach: 1.2f, two: 1f, aim: 1f),
            K(0.75f, Ease.Linear, A(-2f, 0f, 2f), A(-12f, 10f), y: -0.12f, lean: 10f, bend: 2f, footN: -0.17f, footF: 0.15f,
                reach: 1.2f, two: 1f, aim: 1f),
            K(1f, Ease.InOut, A(-40f, 40f, 40f), A(-40f, 40f), y: -0.08f, lean: 6f, footN: -0.08f, footF: 0.07f, two: 0.5f,
                aim: 0.5f),
        };

        // Dùng chiêu tầm xa khung 2: giơ lưỡi hái thẳng lên cao một tay (vận sức) → giữ, run nhẹ
        static readonly ActKey[] ScytheRear =
        {
            K(0.35f, Ease.Out, A(88f, 0f, 94f), A(-100f, 30f), y: -0.04f, lean: -4f, head: -6f, reach: 1.2f),
            K(1f, Ease.Linear, A(90f, 0f, 96f), A(-102f, 30f), y: -0.04f, lean: -5f, head: -7f, reach: 1.22f),
        };

        // Đánh thường áp sát khung 3 → 4 → 5: lao thật thấp, lưỡi hái kéo sau lưng → vung lên qua đầu → bổ chéo xuống tới
        // trước → trượt dừng
        static readonly ActKey[] ScytheLunge =
        {
            K(0.18f, Ease.InOut, A(-150f, 15f, 172f), A(-20f, 100f), y: -0.3f, lean: 36f, bend: 6f, head: -14f,
                footN: -0.35f, footF: 0.11f),
            K(0.28f, Ease.InOut, A(-40f, 60f, 140f), A(-50f, 60f), y: -0.28f, lean: 28f, bend: 6f, head: -6f, footN: -0.37f,
                footF: 0.17f, two: 1f),
            K(0.38f, Ease.Snap, A(-20f, 10f, -25f), A(-30f, 25f), y: -0.28f, lean: 24f, bend: 6f, footN: -0.37f,
                footF: 0.17f, reach: 1.15f, two: 1f, aim: 1f),
            K(0.7f, Ease.Out, A(-30f, 10f, -40f), A(-40f, 25f), y: -0.28f, lean: 24f, bend: 6f, footN: -0.37f, footF: 0.17f,
                reach: 1.1f, two: 1f, aim: 0.5f),
            K(1f, Ease.InOut, A(-60f, 40f, -30f), A(-60f, 40f), y: -0.2f, lean: 16f, footN: -0.2f, footF: 0.12f, two: 0.6f),
        };

        // Dùng chiêu thủ: lưỡi hái dựng đứng một tay, tay kia (lửa hồn) đưa ra trước, đứng vững → giữ
        static readonly ActKey[] ScytheSquash =
        {
            K(0.25f, Ease.Out, A(-100f, 95f, 96f), A(-15f, 5f), y: -0.06f, lean: 2f, footN: -0.05f, footF: 0.05f, reach: 1.1f),
            K(0.85f, Ease.Linear, A(-100f, 95f, 96f), A(-15f, 5f), y: -0.06f, lean: 2f, footN: -0.05f, footF: 0.05f,
                reach: 1.1f),
            K(1f, Ease.InOut, A(-108f, 50f, 96f), A(-20f, 30f), y: -0.04f, lean: 2f),
        };

        /// <summary>
        /// Lúc đòn trúng (phần thời gian Act, 0..1), giống nhau cho cả 3 nhân vật: mũi vũ khí qua hướng nhắm (Whip, WhipAlt,
        /// Reach, Lunge), chạm đất (Slam), xoay bắt đầu quét (Spin: trúng liên tục tới 0.8), giơ tới đỉnh (Rear), cắm / khom
        /// hết cỡ (Squash). Dùng để canh lúc gây sát thương / bắn hiệu ứng.
        /// </summary>
        public static float StrikeAt(CombatPose pose)
        {
            switch (pose)
            {
                case CombatPose.Whip:
                case CombatPose.WhipAlt:
                    return 0.38f;
                case CombatPose.Slam:
                    return 0.42f;
                case CombatPose.Spin:
                    return SpinFrom;
                case CombatPose.Reach:
                    return 0.38f;
                case CombatPose.Rear:
                    return 0.35f;
                case CombatPose.Lunge:
                    return 0.32f;
                case CombatPose.Squash:
                    return 0.27f;
                default:
                    return 0f;
            }
        }

        ActKey[] ActKeys(CombatPose pose)
        {
            bool rise = _aim > riseAim;
            switch (style)
            {
                case Style.Sword:
                    switch (pose)
                    {
                        case CombatPose.Whip: return rise ? SwordWhipRise : SwordWhip;
                        case CombatPose.WhipAlt: return SwordWhipAlt;
                        case CombatPose.Slam: return SwordSlam;
                        case CombatPose.Spin: return SwordSpin;
                        case CombatPose.Reach: return SwordReach;
                        case CombatPose.Rear: return SwordRear;
                        case CombatPose.Lunge: return SwordLunge;
                        case CombatPose.Squash: return SwordSquash;
                        default: return null;
                    }
                case Style.Staff:
                    switch (pose)
                    {
                        case CombatPose.Whip: return rise ? StaffWhipRise : StaffWhip;
                        case CombatPose.WhipAlt: return StaffWhipAlt;
                        case CombatPose.Slam: return StaffSlam;
                        case CombatPose.Spin: return StaffSpin;
                        case CombatPose.Reach: return StaffReach;
                        case CombatPose.Rear: return StaffRear;
                        case CombatPose.Lunge: return StaffLunge;
                        case CombatPose.Squash: return StaffSquash;
                        default: return null;
                    }
                default:
                    switch (pose)
                    {
                        case CombatPose.Whip: return rise ? ScytheWhipRise : ScytheWhip;
                        case CombatPose.WhipAlt: return ScytheWhipAlt;
                        case CombatPose.Slam: return ScytheSlam;
                        case CombatPose.Spin: return ScytheSpin;
                        case CombatPose.Reach: return ScytheReach;
                        case CombatPose.Rear: return ScytheRear;
                        case CombatPose.Lunge: return ScytheLunge;
                        case CombatPose.Squash: return ScytheSquash;
                        default: return null;
                    }
            }
        }

        // ---------- trạng thái ----------

        float _previewDelta;
        bool _previewLeft;
        float _time;

        HeroMove _move = HeroMove.Idle, _lastMove = HeroMove.Idle;
        Vector2 _velocity;            // vận tốc thân bộ điều khiển báo (world)
        bool _grounded = true;
        float _facing = 1f, _flip = 1f;
        float _run;                   // trọng số chạy 0..1
        float _phase;                 // pha chu kỳ chạy
        float _lastAirVy;
        Look _locoFrom, _loco;
        float _locoBlend = 1f, _locoBlendTime = 0.1f;
        float _stateTime;             // đã ở dáng di chuyển hiện tại bao lâu
        float _landAge = 10f, _landStrength;

        CombatPose _act;
        ActKey[] _keys;
        float _actTime, _actDuration;
        float _aim, _aimFar;
        ActLook _actFrom, _actShown, _releaseFrom;
        bool _releasing;
        float _releaseTime;
        float _actWeight;
        float _spinTurn;              // 0..1 vòng xoay người (Spin)
        float _floorClip;             // 0..1 đứng trên sàn: phần vũ khí thò xuống dưới mặt sàn không vẽ

        Look _final;
        float _stride;
        Vector2 _wind, _windVelocity;
        float _flash, _flashAge = 10f;
        float _ghost, _ghostShown;
        float _power, _powerShown;
        Material _bodyMaterial, _weaponMaterial, _glowMaterial;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int GhostId = Shader.PropertyToID("_Ghost");
        static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        static readonly int ClipId = Shader.PropertyToID("_Clip");
        static readonly int ClipYId = Shader.PropertyToID("_ClipY");

        // ---------- IHeroVisual ----------

        public float Facing
        {
            get => _facing;
            set => _facing = value < 0f ? -1f : 1f;
        }

        public void SetMove(HeroMove move, Vector2 velocity)
        {
            _move = move;
            _velocity = velocity;
        }

        /// <summary>
        /// Diễn dáng pose trong duration giây, point = điểm nhắm (world). Gọi một lần mỗi đòn; đòn mới nối liền từ dáng
        /// đang hiện. Act(None) = thôi đòn, hoà về dáng di chuyển. Lúc trúng: xem StrikeAt.
        /// </summary>
        public void Act(CombatPose pose, float duration, Vector2 point)
        {
            if (!HasRig)
                return;
            if (pose == CombatPose.None)
            {
                StartRelease();
                return;
            }

            AimAt(point);
            _keys = ActKeys(pose);
            // Đi ra từ đúng dáng đang hiện: phần thân của đòn trước (đang hoà về thì đã nhạt bớt); tay như đang thấy
            ActLook from = ActLook.Mix(default, _actShown, _actWeight);
            from.Near = _final.Near;
            from.Far = _final.Far;
            from.Reach = _final.Reach;
            from.Two = _final.Two;
            if (_keys != null && _keys.Length > 0)
            {
                ActKey first = _keys[0];
                from.Near = from.Near.Near(first.Look.Near);
                from.Far = from.Far.Near(first.Look.Far);
                if (first.Turn != 0f)
                {
                    from.Near.Upper = WindFrom(from.Near.Upper, first.Look.Near.Upper, first.Turn);
                    from.Near.Blade = WindFrom(from.Near.Blade, first.Look.Near.Blade, first.Turn);
                }
            }

            _actFrom = from;
            _act = pose;
            _actDuration = Mathf.Max(0.05f, duration);
            _actTime = 0f;
            _releasing = false;
        }

        public void Flash(float strength)
        {
            _flash = Mathf.Clamp01(Mathf.Max(strength, _flashAge < flashTime ? CurrentFlash() : 0f));
            _flashAge = 0f;
        }

        public void SetGhost(float amount) => _ghost = Mathf.Clamp01(amount);

        public void SetWeaponPower(float amount) => _power = Mathf.Clamp01(amount);

        public Vector2 WeaponTip => rig.weaponTip != null ? (Vector2)rig.weaponTip.position : (Vector2)transform.position;

        public Vector2 CastPoint => rig.castPoint != null ? (Vector2)rig.castPoint.position : (Vector2)transform.position;

        public Bounds VisualBounds
        {
            get
            {
                Transform[] points = rig.boundPoints;
                if (points == null || points.Length == 0)
                    return new Bounds(transform.position, Vector3.zero);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] == null)
                        continue;
                    Vector2 p = points[i].position;
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }

                var bounds = new Bounds();
                bounds.SetMinMax(new Vector3(min.x, min.y, 0f), new Vector3(max.x, max.y, 0f));
                return bounds;
            }
        }

        /// <summary>Kiểu vũ khí của nhân vật này.</summary>
        public Style WeaponStyle => style;

        /// <summary>Dáng di chuyển bộ điều khiển báo gần nhất.</summary>
        public HeroMove CurrentMove => _move;

        /// <summary>Đòn đang diễn (None = đang di chuyển / đứng).</summary>
        public CombatPose CurrentPose => _act;

        /// <summary>Tiến độ đòn đang diễn (0..1).</summary>
        public float ActProgress => _act == CombatPose.None ? 1f : Mathf.Clamp01(_actTime / _actDuration);

        /// <summary>Editor gọi mỗi frame khi bấm ▶ Chạy thử (không Play): cho thời gian trôi để xem chuyển động.</summary>
        public void TickPreview(float deltaTime) => _previewDelta = Mathf.Clamp(deltaTime, 0f, 0.05f);

        /// <summary>HeroRigBuilder gọi khi dựng: nối xương và đặt thông số mặc định theo nhân vật.</summary>
        public void Setup(Style weaponStyle, Rig references, float scale, float height)
        {
            style = weaponStyle;
            rig = references;
            size = scale;
            rigHeight = height;
            twoHanded = true;
            switch (weaponStyle)
            {
                case Style.Sword:   // Xích Ảnh: nhanh, chúi nhiều; thủ thế chân dạng rộng như tấm dáng
                    stance = new Vector2(-0.44f, 0.46f);
                    runCadence = 3.4f;
                    runStance = 0.36f;
                    runLift = 0.2f;
                    runKick = 0.2f;
                    runLean = 17f;
                    runBob = 0.04f;
                    runArmSwing = 40f;
                    glowColor = new Color(1f, 0.12f, 0.08f);
                    break;
                case Style.Staff:   // Tử Tinh: nhẹ, nhún cao, ít chúi; đứng chân dạng vừa
                    stance = new Vector2(-0.3f, 0.3f);
                    runCadence = 2.7f;
                    runStance = 0.34f;
                    runLift = 0.16f;
                    runKick = 0.12f;
                    runLean = 7f;
                    runBob = 0.07f;
                    runArmSwing = 30f;
                    glowColor = new Color(0.75f, 0.3f, 1f);
                    break;
                default:            // Lam Hồn: sải dài, nhịp chậm; đứng thẳng
                    stance = new Vector2(-0.25f, 0.25f);
                    runCadence = 2.4f;
                    runStance = 0.4f;
                    runLift = 0.22f;
                    runKick = 0.14f;
                    runLean = 11f;
                    runBob = 0.06f;
                    runArmSwing = 25f;
                    glowColor = new Color(0.25f, 0.8f, 1f);
                    break;
            }
        }

        bool HasRig
        {
            get
            {
                if (rig == null || rig.pose == null || rig.body == null || rig.hips == null || rig.chest == null || rig.neck == null)
                    return false;
                return LimbOk(rig.armNear) && LimbOk(rig.armFar) && LimbOk(rig.legNear) && LimbOk(rig.legFar);
            }
        }

        static bool LimbOk(Limb limb) => limb != null && limb.upper != null && limb.lower != null && limb.end != null;

        void Awake()
        {
            if (!Application.isPlaying || rig == null || rig.renderers == null || rig.renderers.Length == 0)
                return;
            // Mỗi nhân vật 2 vật liệu riêng (chớp / hoá ma / rực không lây sang con khác): thân và vũ khí. Tạo một lần
            Material shared = null;
            foreach (SpriteRenderer r in rig.renderers)
            {
                if (r != null)
                {
                    shared = r.sharedMaterial;
                    break;
                }
            }

            if (shared == null)
                return;
            _bodyMaterial = new Material(shared) { name = "Hero (instance)" };
            _weaponMaterial = new Material(shared) { name = "Hero Weapon (instance)" };
            foreach (SpriteRenderer r in rig.renderers)
            {
                if (r == null || r.sharedMaterial != shared)
                    continue;
                bool weapon = rig.weaponRenderers != null && Array.IndexOf(rig.weaponRenderers, r) >= 0;
                r.sharedMaterial = weapon ? _weaponMaterial : _bodyMaterial;
            }

            // Quầng sáng (cộng sáng) cũng một bản riêng để cắt phần dưới mặt sàn như vũ khí
            if (rig.weaponGlow != null && rig.weaponGlow.sharedMaterial != null)
            {
                Material glow = rig.weaponGlow.sharedMaterial;
                _glowMaterial = new Material(glow) { name = "Hero Glow (instance)" };
                rig.weaponGlow.sharedMaterial = _glowMaterial;
                if (rig.castGlow != null && rig.castGlow.sharedMaterial == glow)
                    rig.castGlow.sharedMaterial = _glowMaterial;
            }
        }

        void OnDestroy()
        {
            if (_bodyMaterial != null)
                Destroy(_bodyMaterial);
            if (_weaponMaterial != null)
                Destroy(_weaponMaterial);
            if (_glowMaterial != null)
                Destroy(_glowMaterial);
        }

        void OnEnable()
        {
            _locoBlend = 1f;
            _wind = Vector2.zero;
            _windVelocity = Vector2.zero;
        }

        void LateUpdate()
        {
            if (!HasRig)
                return;

            bool playing = Application.isPlaying;
            if (playing && Time.deltaTime <= 0f)
                return; // khựng hình (hit-stop) / tạm dừng: giữ nguyên dáng đang hiện
            float dt = playing ? Mathf.Min(Time.deltaTime, 0.05f) : _previewDelta;
            _previewDelta = 0f;
            bool live = dt > 0f;
            if (!playing)
                ApplyPreview();

            _time += dt;
            UpdateMotion(dt, live);
            _loco = LocoLook(dt, live);
            UpdateAct(dt, live);
            Compose();
            PlacePose();
            PlaceBody();
            Matrix4x4 toBody = rig.body.worldToLocalMatrix;
            PlaceArms(toBody);
            PlaceLegs(toBody);
            PlaceSecondary(toBody);
            UpdateLook(dt, live);
        }

        void ApplyPreview()
        {
            if (previewLeft != _previewLeft)
                _facing = previewLeft ? -1f : 1f; // đổi ô Preview Left thì quay theo; nút Quay đầu vẫn dùng được
            _previewLeft = previewLeft;
            Vector2 v = Vector2.zero;
            switch (preview)
            {
                case HeroMove.Run:
                    v = new Vector2(previewSpeed * _facing, 0f);
                    break;
                case HeroMove.Jump:
                case HeroMove.DoubleJump:
                    v = new Vector2(0f, 4f);
                    break;
                case HeroMove.Fall:
                    v = new Vector2(0f, -6f);
                    break;
                case HeroMove.Glide:
                    v = new Vector2(2f * _facing, -1.2f);
                    break;
                case HeroMove.Dash:
                    v = new Vector2(previewSpeed * 2f * _facing, 0f);
                    break;
                case HeroMove.Hurt:
                    v = new Vector2(-3f * _facing, 2f);
                    break;
            }

            _move = preview;
            _velocity = v;
            // Xem đáp: diễn lại mỗi giây
            if (preview == HeroMove.Land && _stateTime > 1.1f)
                _lastMove = HeroMove.Fall;
        }

        // ---------- di chuyển ----------

        static bool IsGround(HeroMove move) => move == HeroMove.Idle || move == HeroMove.Run || move == HeroMove.Land;

        void UpdateMotion(float dt, bool live)
        {
            float scale = Mathf.Max(size, 0.01f);
            if (_move != _lastMove)
                OnMoveChanged(_lastMove, _move);
            _lastMove = _move;
            _stateTime += dt;
            _landAge += dt;

            bool wasGrounded = _grounded;
            _grounded = IsGround(_move) || ((_move == HeroMove.Dash || _move == HeroMove.Hurt) && wasGrounded &&
                                            Mathf.Abs(_velocity.y) < 0.3f);
            if (!_grounded)
                _lastAirVy = _velocity.y;

            float speed = Mathf.Abs(_velocity.x) / scale;
            float run = _move == HeroMove.Run ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, runFull, speed)) : 0f;
            _run = live ? Mathf.MoveTowards(_run, run, dt * 6f) : run;
            // Nhịp chạy theo quãng đường đã đi: chân đang chạm đất đứng yên trên sàn
            float cycle = Mathf.Max(runCycle, speed / Mathf.Max(runCadence, 0.1f));
            if (live && _move == HeroMove.Run)
                _phase = Mathf.Repeat(_phase + Mathf.Max(speed, 0.6f) * dt / cycle, 1f);
            _stride = Mathf.Min(runStance * cycle, runMaxStride);
            _flip = live ? Mathf.MoveTowards(_flip, _facing, dt * 2f / Mathf.Max(turnTime, 0.01f)) : _facing;

            // Gió ngược chiều chạy / rơi (hệ Body), theo sau vận tốc như lò xo: dừng gấp thì vải văng tới rồi lắc lại
            Vector2 v = _velocity / scale;
            v.x *= _facing;
            Vector2 target = Vector2.ClampMagnitude(-v * windScale, 1.5f);
            if (live)
            {
                _windVelocity += ((target - _wind) * windSpring - _windVelocity * windDamping) * dt;
                _wind += _windVelocity * dt;
            }
            else
            {
                _wind = target;
            }
        }

        void OnMoveChanged(HeroMove from, HeroMove to)
        {
            _stateTime = 0f;
            bool fromGround = IsGround(from), toGround = IsGround(to);
            // Đáp: nhún theo vận tốc rơi (từ trên không xuống sàn, kể cả khi chạy tiếp ngay)
            if (toGround && !fromGround)
            {
                _landAge = 0f;
                _landStrength = Mathf.Clamp(-_lastAirVy / 12f, 0.35f, 1f);
            }

            if (fromGround && toGround)
                return; // đứng ↔ chạy ↔ đáp liền mạch
            _locoFrom = _loco;
            _locoBlend = 0f;
            _locoBlendTime = toGround ? moveBlend * 0.7f : moveBlend;
            if (to == HeroMove.DoubleJump || to == HeroMove.Hurt)
                _locoBlendTime = moveBlend * 0.6f;
        }

        Look LocoLook(float dt, bool live)
        {
            MoveSet set = Sets[(int)style];
            Look look;
            switch (_move)
            {
                case HeroMove.Jump:
                    look = AirLook(set.JumpN, set.JumpF, V(-0.08f, -0.62f), V(0.18f, -0.72f), 4f, 2f, 0f);
                    // Bật lên: vươn người
                    look.Squash -= 0.08f * Mathf.Clamp01(1f - _stateTime / 0.18f);
                    break;
                case HeroMove.DoubleJump:
                    // Nhảy lần 2: bật vươn người rồi co chân gọn hơn lần 1
                    look = AirLook(set.JumpN, set.JumpF, V(-0.02f, -0.5f), V(0.22f, -0.56f), 8f, 4f, 2f);
                    look.Squash -= 0.1f * Mathf.Clamp01(1f - _stateTime / 0.14f);
                    break;
                case HeroMove.Fall:
                    look = AirLook(set.FallN, set.FallF, V(-0.12f, -0.9f), V(0.12f, -0.85f), 6f, 2f, 4f);
                    look.Flutter = 0.3f;
                    break;
                case HeroMove.Glide:
                    // Lơ lửng: chân buông, tay hơi dang, nhấp nhô chậm, vải / tóc bay
                    look = AirLook(set.GlideN, set.GlideF, V(-0.16f, -0.86f), V(0.08f, -0.9f), 4f, 2f, 2f);
                    look.Flutter = 1f;
                    look.Offset.y += 0.025f * Mathf.Sin(_time * 3.2f);
                    look.Near.Upper += 3f * Mathf.Sin(_time * 3.2f + 0.8f);
                    look.Near.Blade += 3f * Mathf.Sin(_time * 3.2f + 0.8f);
                    break;
                case HeroMove.Dash:
                    look = AirLook(set.DashN, set.DashF, V(-0.6f, -0.66f), V(0.34f, -0.76f), set.DashLean, 6f, set.DashHead);
                    look.Offset.y += set.DashY;
                    look.Stretch = 0.06f;
                    look.Flutter = 0.5f;
                    break;
                case HeroMove.Hurt:
                {
                    look = AirLook(set.HurtN, set.HurtF, V(0.2f, -0.85f), V(0.38f, -0.78f), -20f, -10f, -18f);
                    look.Offset.x -= 0.05f;
                    // Giật lùi: thoáng đầu ngửa mạnh hơn rồi chùng lại
                    float jolt = Mathf.Clamp01(1f - _stateTime / 0.2f);
                    look.Lean -= 12f * jolt;
                    look.Head -= 10f * jolt;
                    if (_grounded)
                    {
                        look.Plant = 1f;
                        look.Offset.y -= 0.06f;
                    }

                    break;
                }
                default:
                    look = GroundLook(set);
                    break;
            }

            if (_locoBlend < 1f)
            {
                if (live)
                    _locoBlend = Mathf.Min(1f, _locoBlend + dt / Mathf.Max(_locoBlendTime, 0.01f));
                look = Look.Mix(_locoFrom, look, Eased(Ease.InOut, _locoBlend));
            }

            // Đáp đất: nhún xuống thật nhanh rồi nảy về
            if (_landAge < landTime)
            {
                float t = _landAge / landTime;
                float dip = (t < 0.22f ? Eased(Ease.Out, t / 0.22f) : 1f - Eased(Ease.InOut, (t - 0.22f) / 0.78f)) * _landStrength;
                look.Offset.y -= landDip * dip;
                look.Squash += 0.16f * dip;
                look.Lean += 8f * dip;
                look.Head += 6f * dip;
                look.Near.Upper -= 12f * dip;
                look.Far.Upper -= 15f * dip;
            }

            return look;
        }

        // Dáng trên không: chân theo háng (rel = phần chiều dài chân), chỗ đặt chân trên sàn (thế đứng) để nội suy với dáng
        // đứng và cho trúng đòn khi đang đứng
        Look AirLook(ArmPose near, ArmPose far, Vector2 relN, Vector2 relF, float lean, float bend, float head)
        {
            float vy = _velocity.y / Mathf.Max(size, 0.01f);
            var look = new Look
            {
                Lean = lean,
                Bend = bend,
                Head = head,
                Near = near,
                Far = far,
                Reach = 1f,
                RelN = relN,
                RelF = relF,
                GroundN = new Vector2(stance.x, 0f),
                GroundF = new Vector2(stance.y, 0f),
                Plant = 0f,
            };
            // Tay nhấp nhô nhẹ cho khỏi cứng đờ
            look.Far.Upper += 4f * Mathf.Sin(_time * 5.1f);
            look.Near.Upper += 2f * Mathf.Sin(_time * 4.3f + 1f);
            look.Lean += Mathf.Clamp(-vy * 0.8f, -6f, 6f);
            return look;
        }

        Look GroundLook(in MoveSet set)
        {
            float tau = 2f * Mathf.PI;
            float run = _run;
            float idle = 1f - run;
            float breath = Mathf.Sin(_time * breathSpeed);
            var look = new Look
            {
                Plant = 1f,
                Reach = 1f,
                Two = set.IdleTwo * idle,
                Lean = set.IdleLean * idle + runLean * run,
                Bend = (set.IdleBend + 1.2f * Mathf.Sin(_time * breathSpeed + 0.6f)) * idle + 4f * run,
                Head = set.IdleHead * idle - runLean * 0.4f * run,
            };
            look.Offset.y = set.IdleY * idle + breathBob * breath * idle;

            // Chân: đứng theo thế đứng của nhân vật; chạy thì tâm bước dồn về dưới háng, theo chu kỳ (chân đang chạm đất
            // trượt lùi đúng tốc độ thân)
            Vector2 standN = new Vector2(stance.x, 0f), standF = new Vector2(stance.y, 0f);
            Vector2 centerN = rig.legNear.rest + new Vector2(-0.05f, 0f), centerF = rig.legFar.rest + new Vector2(0.06f, 0f);
            Vector2 gaitN = Gait(_phase), gaitF = Gait(_phase + 0.5f);
            look.GroundN = Vector2.Lerp(standN, centerN + gaitN, run);
            look.GroundF = Vector2.Lerp(standF, centerF + gaitF, run);
            look.RelN = new Vector2(0f, -0.95f);
            look.RelF = new Vector2(0f, -0.95f);

            // Thân nhún: thấp nhất giữa lúc chân chạm đất, cao nhất lúc cả 2 chân rời đất
            float bob = -Mathf.Cos(2f * tau * (_phase - runStance * 0.5f));
            look.Offset.y += (-0.04f - runBob * bob) * run;
            look.Offset.x += 0.03f * run;
            look.Lean += 2f * Mathf.Sin(2f * tau * _phase) * run;

            // Tay: đứng thì đung đưa nhẹ; chạy thì tay xa vung ngược chân xa, tay cầm vũ khí vung ít
            float swing = Mathf.Sin(tau * _phase);
            look.Near = ArmPose.Mix(set.IdleN, set.RunN.Near(set.IdleN), run);
            look.Far = ArmPose.Mix(set.IdleF, set.RunF.Near(set.IdleF), run);
            look.Far.Upper += runArmSwing * swing * run + 2.5f * Mathf.Sin(_time * breathSpeed * 0.5f) * idle;
            look.Far.Bend += 15f * Mathf.Max(0f, swing) * run;
            look.Near.Upper -= runArmSwing * 0.25f * swing * run;
            look.Near.Blade -= runArmSwing * 0.2f * swing * run;
            // Vũ khí khi đứng: nhấp nhô theo nhịp thở
            look.Near.Upper += 2f * breath * idle;
            look.Near.Blade += 2f * Mathf.Sin(_time * breathSpeed - 0.5f) * idle;
            look.Head += 1.5f * Mathf.Sin(2f * tau * _phase + 1f) * run;
            look.Flutter = 0.15f * idle;
            return look;
        }

        // Một chân trong chu kỳ chạy (so với tâm bước): đang chạm đất thì trượt lùi; nhấc lên thì đá gót ra sau rồi vươn tới
        Vector2 Gait(float phase)
        {
            float u = Mathf.Repeat(phase, 1f);
            float stride = _stride;
            if (u < runStance)
                return new Vector2(stride * (0.5f - u / runStance), 0f);

            float k = (u - runStance) / (1f - runStance);
            float kick = runKick * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.8f)) * (1f - k);
            float x = stride * (Mathf.SmoothStep(0f, 1f, k) - 0.5f) + 0.06f * Mathf.Sin(Mathf.PI * k) - kick * 0.6f;
            float y = runLift * Mathf.Sin(Mathf.PI * k) + kick;
            return new Vector2(x, y);
        }

        // ---------- ra đòn ----------

        void AimAt(Vector2 point)
        {
            Vector2 p = rig.body.InverseTransformPoint(point);
            Vector2 near = rig.body.InverseTransformPoint(rig.armNear.upper.position);
            Vector2 far = rig.body.InverseTransformPoint(rig.armFar.upper.position);
            _aim = AimAngle(p - near);
            _aimFar = AimAngle(p - far);
        }

        float AimAngle(Vector2 d)
        {
            if (d.sqrMagnitude < 1e-4f)
                return 0f;
            // Điểm nhắm sau lưng: vẫn đánh về phía trước (quay mặt là việc của bộ điều khiển), chỉ lấy độ cao
            d.x = Mathf.Max(d.x, 0.35f * Mathf.Abs(d.y) + 0.05f);
            return Mathf.Clamp(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, -maxAim, maxAim);
        }

        void StartRelease()
        {
            if (_act == CombatPose.None && !_releasing)
                return;
            _releaseFrom = _actShown;
            _act = CombatPose.None;
            _releasing = true;
            _releaseTime = 0f;
        }

        void UpdateAct(float dt, bool live)
        {
            if (live)
                _flashAge += dt;
            _spinTurn = 0f;
            if (_act != CombatPose.None)
            {
                if (live)
                    _actTime += dt;
                float k = Mathf.Clamp01(_actTime / _actDuration);
                _actShown = SampleAct(_keys, k);
                AddActMotion(ref _actShown, k);
                _actWeight = 1f;
                if (_act == CombatPose.Spin)
                    _spinTurn = Eased(Ease.InOut, Mathf.Clamp01((k - SpinFrom) / (SpinTo - SpinFrom)));
                if (_actTime >= _actDuration)
                    StartRelease();
            }
            else if (_releasing)
            {
                if (live)
                    _releaseTime += dt;
                float w = 1f - Eased(Ease.InOut, Mathf.Clamp01(_releaseTime / settleTime));
                // Tay về dáng di chuyển theo vòng gần nhất
                _releaseFrom.Near = _releaseFrom.Near.Near(_loco.Near);
                _releaseFrom.Far = _releaseFrom.Far.Near(_loco.Far);
                _actShown = _releaseFrom;
                _actWeight = w;
                if (_releaseTime >= settleTime)
                {
                    _releasing = false;
                    _actWeight = 0f;
                }
            }
            else
            {
                _actWeight = 0f;
            }

            // Đứng trên sàn: vũ khí quệt / cắm xuống không xuyên qua mặt sàn (phần dưới sàn không vẽ)
            float clip = _grounded ? 1f : 0f;
            _floorClip = live ? Mathf.MoveTowards(_floorClip, clip, dt * 8f) : clip;
        }

        ActLook SampleAct(ActKey[] keys, float k)
        {
            if (keys == null || keys.Length == 0)
                return _actFrom;
            int i = 0;
            while (i < keys.Length - 1 && k > keys[i].K)
                i++;
            ActKey b = keys[i];
            float k0 = i == 0 ? 0f : keys[i - 1].K;
            ActLook a = i == 0 ? _actFrom : WithAim(keys[i - 1]);
            float x = Eased(b.Ease, Mathf.Clamp01((k - k0) / Mathf.Max(b.K - k0, 1e-4f)));
            return ActLook.Mix(a, WithAim(b), x);
        }

        ActLook WithAim(in ActKey key)
        {
            ActLook look = key.Look;
            float aim = _aim * key.Aim, aimFar = _aimFar * key.AimFar;
            look.Near.Upper += aim;
            look.Near.Blade += aim;
            look.Far.Upper += aimFar;
            look.Lean -= 0.2f * (aim + aimFar);
            return look;
        }

        // Chuyển động thêm bằng code theo đòn (run khi vận sức)
        void AddActMotion(ref ActLook look, float k)
        {
            if (_act == CombatPose.Rear)
            {
                float on = Mathf.Clamp01((k - 0.35f) / 0.1f);
                look.Near.Upper += 1.5f * Mathf.Sin(_time * 41f) * on;
                look.Near.Blade += 2f * Mathf.Sin(_time * 37f + 1f) * on;
                look.Offset.y += 0.006f * Mathf.Sin(_time * 33f) * on;
            }
        }

        // Ghép dáng di chuyển với đòn: tay + vũ khí + dáng đứng riêng của nhân vật thay hẳn theo trọng số đòn, phần còn lại
        // (nhún chạy, nghiêng khi bay…) cộng thêm
        void Compose()
        {
            Look look = _loco;
            float w = _actWeight;
            if (w > 0f)
            {
                MoveSet set = Sets[(int)style];
                ActLook act = _actShown;
                // Trên sàn, đòn thay dáng đứng (thủ thế) chứ không cộng chồng lên
                float ground = look.Plant * (1f - _run);
                look.Offset += (act.Offset - new Vector2(0f, set.IdleY * ground)) * w;
                look.Lean += (act.Lean - set.IdleLean * ground) * w;
                look.Bend += (act.Bend - set.IdleBend * ground) * w;
                look.Head += (act.Head - set.IdleHead * ground) * w;
                look.Squash += act.Squash * w;
                look.Near = ArmPose.Mix(look.Near, act.Near, w);
                look.Far = ArmPose.Mix(look.Far, act.Far, w);
                look.Reach = Mathf.Lerp(look.Reach, act.Reach, w);
                look.Two = Mathf.Lerp(look.Two, act.Two, w);
                // Dạng chân: chỉ khi đứng trên sàn và không đang chạy (chạy thì chân theo nhịp chạy)
                look.GroundN.x += act.FootN * w * ground;
                look.GroundF.x += act.FootF * w * ground;
            }

            _final = look;
        }

        // ---------- đặt xương ----------

        void PlacePose()
        {
            // Quay đầu: không bóp qua 0 mà đổi chiều ngay giữa lượt; quanh lúc đổi thân co nhẹ rồi bung ra
            float turning = 1f - Mathf.Abs(_flip);
            float sx = (_flip >= 0f ? 1f : -1f) * (1f - turnSquash * turning);
            float sy = 1f - 0.04f * turning;

            // Xoay người (Spin): quay một vòng quanh trục đứng, cùng cách quay đầu (lật ở giữa, không bóp dẹt)
            if (_spinTurn > 0f && _spinTurn < 1f)
            {
                float c = Mathf.Cos(2f * Mathf.PI * _spinTurn);
                sx *= (c >= 0f ? 1f : -1f) * (1f - 0.25f * (1f - Mathf.Abs(c)));
            }

            float squash = Mathf.Clamp(_final.Squash, -0.3f, 0.4f);
            sx *= 1f + 0.6f * squash + _final.Stretch;
            sy *= 1f - squash - 0.3f * _final.Stretch;
            rig.pose.localScale = new Vector3(sx * size, sy * size, 1f);
            rig.pose.localPosition = Vector3.zero;
            rig.pose.localRotation = Quaternion.identity;
        }

        void PlaceBody()
        {
            Look look = _final;
            rig.hips.localPosition = rig.hipsRest + look.Offset;
            rig.hips.localRotation = Quaternion.Euler(0f, 0f, rig.hipsAngle - look.Lean);
            rig.chest.localRotation = Quaternion.Euler(0f, 0f, rig.chestAngle - look.Bend);
            // Đầu giữ gần thẳng khi người chúi (nhìn về phía trước)
            float head = look.Head - 0.55f * (look.Lean + look.Bend);
            rig.neck.localRotation = Quaternion.Euler(0f, 0f, rig.neckAngle - head);
        }

        void PlaceArms(in Matrix4x4 toBody)
        {
            float follow = 0.5f * (_final.Lean + _final.Bend);
            float reach = Mathf.Clamp(_final.Reach, 0.6f, 1.6f);
            PlaceArm(rig.armNear, _final.Near, toBody, follow, reach, true);
            float two = twoHanded && rig.offGrip != null ? Mathf.Clamp01(_final.Two) : 0f;
            float farReach = Mathf.Lerp(1f, reach, two);
            PlaceArm(rig.armFar, _final.Far, toBody, follow, farReach, false);

            // Vũ khí: to ra khi rực; xoay người thì vũ khí ngắn lại khi chĩa về phía người xem / ra sau lưng
            float power = 1f + (powerScale - 1f) * Eased(Ease.InOut, _powerShown);
            float along = 1f;
            if (_spinTurn > 0f && _spinTurn < 1f)
                along = Mathf.Max(Mathf.Abs(Mathf.Cos(2f * Mathf.PI * _spinTurn)), 0.18f);
            if (rig.weapon != null)
                rig.weapon.localScale = new Vector3(power * along, power, 1f);
            if (two > 0.001f)
                GripWithFarHand(toBody, two, farReach);
            SortWeapon(toBody, follow);
        }

        void PlaceArm(Limb arm, in ArmPose pose, in Matrix4x4 toBody, float follow, float reach, bool weapon)
        {
            float parent = BodyAngle(toBody, arm.upper.parent);
            float upper = pose.Upper - follow;
            arm.upper.localRotation = Quaternion.Euler(0f, 0f, upper - parent);
            arm.lower.localRotation = Quaternion.Euler(0f, 0f, pose.Bend);
            StretchArm(arm, reach);
            if (weapon)
                arm.end.localRotation = Quaternion.Euler(0f, 0f, pose.Blade - follow - upper - pose.Bend);
        }

        // Tay vươn dài: ảnh 2 đoạn kéo dài dọc xương, khớp sau dời theo
        static void StretchArm(Limb arm, float reach)
        {
            arm.lower.localPosition = new Vector3(arm.upperLength * reach, 0f, 0f);
            arm.end.localPosition = new Vector3(arm.lowerLength * reach, 0f, 0f);
            if (arm.upperArt != null)
                arm.upperArt.localScale = new Vector3(reach, 1f, 1f);
            if (arm.lowerArt != null)
                arm.lowerArt.localScale = new Vector3(reach, 1f, 1f);
        }

        // Cầm 2 tay: tay xa vươn tới chỗ nắm thứ 2 trên chuôi (IK 2 xương, khuỷu chĩa xuống), hoà với dáng tay xa theo two
        void GripWithFarHand(in Matrix4x4 toBody, float two, float reach)
        {
            Limb arm = rig.armFar;
            Vector2 shoulder = toBody.MultiplyPoint3x4(arm.upper.position);
            Vector2 target = toBody.MultiplyPoint3x4(rig.offGrip.position);
            float l1 = Mathf.Max(arm.upperLength * reach, 1e-4f), l2 = Mathf.Max(arm.lowerLength * reach, 1e-4f);
            Vector2 toGoal = target - shoulder;
            float d = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f);
            float baseAngle = Mathf.Atan2(toGoal.y, toGoal.x) * Mathf.Rad2Deg;
            float bend = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f)) * Mathf.Rad2Deg;
            float upper = baseAngle - bend;
            Vector2 elbow = shoulder + new Vector2(Mathf.Cos(upper * Mathf.Deg2Rad), Mathf.Sin(upper * Mathf.Deg2Rad)) * l1;
            float lower = Mathf.Atan2(target.y - elbow.y, target.x - elbow.x) * Mathf.Rad2Deg;

            float parent = BodyAngle(toBody, arm.upper.parent);
            float fkUpper = parent + arm.upper.localEulerAngles.z;
            float fkBend = arm.lower.localEulerAngles.z;
            float u = Mathf.LerpAngle(fkUpper, upper, two);
            float b = Mathf.LerpAngle(fkBend, Mathf.DeltaAngle(upper, lower), two);
            arm.upper.localRotation = Quaternion.Euler(0f, 0f, u - parent);
            arm.lower.localRotation = Quaternion.Euler(0f, 0f, b);
        }

        // Thứ tự vẽ vũ khí: xoay người tới nửa vòng phía sau → sau thân; vũ khí giơ ra sau đầu (hoặc dựng đứng, cầm ở sau
        // gáy) → sau đầu; còn lại trước đầu
        void SortWeapon(in Matrix4x4 toBody, float follow)
        {
            if (rig.weaponArt == null)
                return;
            int order = rig.weaponOrder;
            float blade = Mathf.DeltaAngle(0f, _final.Near.Blade - follow);
            bool high = rig.weaponTip != null && rig.weaponTip.position.y > rig.neck.position.y;
            bool behind = toBody.MultiplyPoint3x4(rig.armNear.end.position).x < toBody.MultiplyPoint3x4(rig.neck.position).x - 0.05f;
            if (_spinTurn > 0.5f && _spinTurn < 1f)
                order = rig.weaponOrderBehindBody;
            else if (high && (blade > 100f || blade < -170f || (blade > 70f && behind)))
                order = rig.weaponOrderBehindHead;
            rig.weaponArt.sortingOrder = order;
            if (rig.weaponGlow != null)
                rig.weaponGlow.sortingOrder = order + 1;
        }

        void PlaceLegs(in Matrix4x4 toBody)
        {
            PlaceLeg(rig.legNear, toBody, _final.GroundN, _final.RelN);
            PlaceLeg(rig.legFar, toBody, _final.GroundF, _final.RelF);
        }

        void PlaceLeg(Limb leg, in Matrix4x4 toBody, Vector2 ground, Vector2 rel)
        {
            Matrix4x4 parent = toBody * leg.upper.parent.localToWorldMatrix;
            Vector2 root = parent.MultiplyPoint3x4(leg.upper.localPosition);
            float parentAngle = Mathf.Atan2(parent.m10, parent.m00) * Mathf.Rad2Deg;
            float l1 = Mathf.Max(leg.upperLength, 1e-4f), l2 = Mathf.Max(leg.lowerLength, 1e-4f);
            Vector2 target = Vector2.Lerp(root + rel * (l1 + l2), ground, _final.Plant);
            if (_grounded)
                target.y = Mathf.Max(target.y, 0f);

            // IK 2 xương: gối luôn nhô về phía trước
            Vector2 toGoal = target - root;
            float d = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f);
            float baseAngle = Mathf.Atan2(toGoal.y, toGoal.x);
            float bend = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f));
            float upper = baseAngle + bend;
            Vector2 knee = root + new Vector2(Mathf.Cos(upper), Mathf.Sin(upper)) * l1;
            float lower = Mathf.Atan2(target.y - knee.y, target.x - knee.x);
            float upperDeg = upper * Mathf.Rad2Deg;
            leg.upper.localRotation = Quaternion.Euler(0f, 0f, upperDeg - parentAngle);
            leg.lower.localRotation = Quaternion.Euler(0f, 0f, lower * Mathf.Rad2Deg - upperDeg);
        }

        // Vải, tóc, lửa: lệch theo gió (ngược chiều chạy / rơi) + lay nhẹ; xương gốc trước, xương con sau
        void PlaceSecondary(in Matrix4x4 toBody)
        {
            float flutter = _final.Flutter;
            PlaceSway(rig.cloth, toBody, 1f, flutter);
            PlaceSway(rig.hair, toBody, 0.8f, flutter);
            PlaceSway(rig.longHair, toBody, 1f, flutter);

            Sway[] flame = rig.flame;
            if (flame == null || flame.Length == 0 || flame[0].bone == null)
                return;
            // Lửa hồn: gốc luôn hướng lên trời, ngọn lửa uốn như lửa cháy, nghiêng theo gió
            for (int i = 0; i < flame.Length; i++)
            {
                Sway s = flame[i];
                if (s.bone == null)
                    continue;
                float wave = 9f * Mathf.Sin(_time * 7.3f - i * 1.1f) + 5f * Mathf.Sin(_time * 12.7f - i * 1.9f);
                float lean = Mathf.Clamp(_wind.x * 30f, -40f, 40f) * (i == 0 ? 0.6f : 0.3f);
                if (i == 0)
                {
                    float parent = BodyAngle(toBody, s.bone.parent);
                    s.bone.localRotation = Quaternion.Euler(0f, 0f, 90f - parent + wave * 0.4f - lean);
                    float pulse = 1f + 0.08f * Mathf.Sin(_time * 9.1f) + 0.25f * Eased(Ease.InOut, _powerShown);
                    s.bone.localScale = new Vector3(pulse, 1f + 0.05f * Mathf.Sin(_time * 6.7f), 1f);
                }
                else
                {
                    s.bone.localRotation = Quaternion.Euler(0f, 0f, s.rest + wave - lean);
                }
            }
        }

        void PlaceSway(Sway[] bones, in Matrix4x4 toBody, float gain, float flutter)
        {
            if (bones == null)
                return;
            Vector2 wind = _wind;
            for (int i = 0; i < bones.Length; i++)
            {
                Sway s = bones[i];
                if (s.bone == null)
                    continue;
                float parent = BodyAngle(toBody, s.bone.parent);
                float a = (parent + s.rest) * Mathf.Deg2Rad;
                // Gió đẩy ngọn xương về phía gió thổi: độ lệch tỉ lệ với tích có hướng (hướng xương × gió)
                float push = Mathf.Cos(a) * wind.y - Mathf.Sin(a) * wind.x;
                float angle = Mathf.Clamp(s.wind * gain * push, -65f, 65f);
                float amp = s.sway * (1f + 2.5f * flutter);
                float speed = 1f + 1.6f * flutter;
                angle += amp * (0.65f * Mathf.Sin(_time * 1.9f * speed + s.phase) + 0.35f * Mathf.Sin(_time * 3.3f * speed + s.phase * 1.7f));
                s.bone.localRotation = Quaternion.Euler(0f, 0f, s.rest + angle);
            }
        }

        static float BodyAngle(in Matrix4x4 toBody, Transform t)
        {
            Matrix4x4 m = toBody * t.localToWorldMatrix;
            return Mathf.Atan2(m.m10, m.m00) * Mathf.Rad2Deg;
        }

        // ---------- chớp, hoá ma, rực ----------

        float CurrentFlash()
        {
            float k = Mathf.Clamp01(_flashAge / flashTime);
            return _flash * (1f - k) * (1f - k);
        }

        void UpdateLook(float dt, bool live)
        {
            _ghostShown = live ? Mathf.MoveTowards(_ghostShown, _ghost, dt * 5f) : _ghost;
            _powerShown = live ? Mathf.MoveTowards(_powerShown, _power, dt * 3f) : _power;
            float power = Eased(Ease.InOut, _powerShown);
            float ghostFade = 1f - 0.6f * _ghostShown;

            if (rig.weaponGlow != null)
            {
                float a = power * (0.55f + 0.2f * Mathf.Sin(_time * 7f)) * ghostFade;
                rig.weaponGlow.enabled = a > 0.01f;
                rig.weaponGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, a);
            }

            if (rig.castGlow != null)
            {
                float a = (0.3f + 0.12f * Mathf.Sin(_time * 4.1f) + 0.5f * power) * ghostFade;
                rig.castGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, Mathf.Clamp01(a));
                float s = 1f + 0.08f * Mathf.Sin(_time * 5.3f) + 0.5f * power;
                rig.castGlow.transform.localScale = new Vector3(s, s, 1f) * 0.45f;
            }

            if (_bodyMaterial == null || _weaponMaterial == null)
                return;
            float flash = CurrentFlash();
            Color color = Color.Lerp(flashWhite, flashRed, Mathf.Clamp01(_flashAge / (flashTime * 0.35f)));
            color.a = flash * 0.8f; // không phủ kín: vẫn thấy nét vẽ dưới lớp chớp
            // Hoá ma: nhấp nháy nhẹ
            float ghost = _ghostShown * (0.88f + 0.12f * Mathf.Sin(_time * 23f));
            for (int i = 0; i < 2; i++)
            {
                Material m = i == 0 ? _bodyMaterial : _weaponMaterial;
                m.SetColor(FlashId, color);
                m.SetFloat(GhostId, ghost);
                m.SetColor(GhostColorId, ghostColor);
            }

            _weaponMaterial.SetFloat(GlowId, power);
            _weaponMaterial.SetColor(GlowColorId, glowColor);
            // Đứng trên sàn: phần vũ khí dưới mặt sàn (gốc hình = mặt sàn) không vẽ
            float clip = _floorClip;
            _weaponMaterial.SetFloat(ClipId, clip);
            _weaponMaterial.SetFloat(ClipYId, transform.position.y);
            if (_glowMaterial != null)
            {
                _glowMaterial.SetFloat(ClipId, clip);
                _glowMaterial.SetFloat(ClipYId, transform.position.y);
            }
        }

        // ---------- tiện ích ----------

        /// <summary>Góc tương đương với angle (±360) gần reference nhất.</summary>
        static float Closest(float angle, float reference) => reference + Mathf.DeltaAngle(reference, angle);

        /// <summary>Góc tương đương với from (±360) sao cho đi từ đó tới to theo chiều dir (+1 tăng, -1 giảm), dưới 1 vòng.</summary>
        static float WindFrom(float from, float to, float dir) => to - Mathf.Sign(dir) * Mathf.Repeat(Mathf.Sign(dir) * (to - from), 360f);

        static float Eased(Ease ease, float x)
        {
            switch (ease)
            {
                case Ease.InOut:
                    return x * x * (3f - 2f * x);
                case Ease.Out:
                    return 1f - (1f - x) * (1f - x);
                case Ease.Snap:
                {
                    float u = 1f - x;
                    return 1f - u * u * u * u;
                }
                case Ease.In:
                    return x * x;
                default:
                    return x;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!HasRig)
                return;
            Bounds b = VisualBounds;
            Gizmos.color = new Color(1f, 0.5f, 0.2f);
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(WeaponTip, 0.08f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(CastPoint, 0.08f);
        }
    }
}
