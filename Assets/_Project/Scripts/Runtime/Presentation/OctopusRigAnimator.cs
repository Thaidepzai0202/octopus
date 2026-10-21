using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace Octopus
{
    /// <summary>
    /// Hoạt ảnh bạch tuộc bằng xương. Ảnh con chính (Art/Octopus/Rig) được tách thành đầu, mắt, con ngươi và 3 xúc tu;
    /// đầu có 3 xương (mềm như áo bạch tuộc), mỗi xúc tu 14 xương. SpriteSkin (package 2D Animation) uốn ảnh theo xương.
    /// Mỗi frame code tính góc từng xương rồi cho qua lò xo nên chóp xúc tu và đỉnh đầu luôn trễ, nảy theo gốc.
    ///
    ///  Bò         — như bạch tuộc thật: xúc tu trước duỗi dài vươn tới, bám đất rồi co lại kéo thân; xúc tu giữa lăn
    ///               dưới thân; xúc tu sau duỗi ra đẩy, xong cuộn tròn lại đưa lên trước. Chóp đang bám đứng yên trên đất
    ///               (IK). Thân chồm tới theo từng nhịp kéo, đầu mềm ngả ra sau và lắc theo nhịp.
    ///               Dáng bò lấy từ clip Crawl.anim (chỉnh tay trong cửa sổ Animation: kéo tay cầm chóp xúc tu / thân),
    ///               hoặc tắt Use Crawl Clip để code tự tính từ mục Gait
    ///  Đứng yên   — đầu phồng xẹp như thở, xúc tu cuộn / duỗi chậm, chóp ngoe nguẩy
    ///  Nhảy / rơi — xúc tu duỗi ra kéo theo sau như mực phụt nước khi bay lên; xoè lên như ô khi rơi; đầu ngả ngược hướng bay
    ///  Tiếp đất   — thân hạ xuống, xúc tu vẫn bám đất nên tự co lại (IK), rồi đứng dậy
    ///  Đu         — xúc tu giữa vươn thật tới đá: đoạn gốc kéo dài, đoạn chóp giữ nét cuộn của tranh quấn vào đá
    ///  Bám đá     — đỉnh đầu áp vào đá, hai xúc tu bên nắm mặt đá, bò thì thay nhau với tới như đu xà
    ///  Mắt        — chớp ngẫu nhiên, con ngươi liếc theo hướng đi và luôn dựng đứng như mắt bạch tuộc thật
    ///
    /// Xem và chỉnh: chọn Rig Visual → Inspector có nút ▶ Chạy thử trong Scene; bật "Show Bones" để thấy xương,
    /// chỗ chân bám (chấm xanh) và đích IK (dấu +) ngay trong cửa sổ Scene.
    /// </summary>
    [ExecuteAlways]
    public class OctopusRigAnimator : MonoBehaviour, ILaserEye, ICombatAnimator
    {
        public enum Preview { Idle, Crawl, Jump, Fall, Swing, Cling, ClingCrawl }

        /// <summary>
        /// Trườn: xúc tu sát đất uốn sóng như rắn. Bước: nhấc từng xúc tu lên đặt xuống.
        /// Sâu đo: hai xúc tu bên co lại thành vòm rồi vươn ra nằm thẳng (xúc tu sau làm ngược lại), xúc tu giữa trườn.
        /// </summary>
        public enum CrawlStyle { Slither, Step, Inchworm }

        enum Mode { Ground, Air, Swing, Cling }

        /// <summary>Một chuỗi xương (xúc tu hoặc đầu) và dáng gốc của nó.</summary>
        [Serializable]
        public class Limb
        {
            public string name;
            public SpriteRenderer renderer;
            public Transform[] bones = Array.Empty<Transform>();
            [Tooltip("Góc local (độ) của từng xương ở dáng vẽ gốc (đọc lại từ ảnh mỗi khi sửa trong Skinning Editor)")]
            public float[] bindAngles = Array.Empty<float>();
            [Tooltip("Chiều dài từng xương (unit tranh) ở dáng vẽ gốc")]
            public float[] bindLengths = Array.Empty<float>();
            [Tooltip("Vị trí xương gốc trong Torso")]
            public Vector2 root;
            [Tooltip("Xương chứa điểm chạm đất, và vị trí điểm đó trong hệ toạ độ của xương")]
            public int footBone;
            public Vector2 footOffset;
            [Tooltip("1 = xúc tu bên trái tranh, -1 = bên phải, 0 = ở giữa")]
            public float side = 1f;

            /// <summary>Phía xúc tu uốn ra khi bị ép ngắn và phía quay vòng ra ngoài: trái (1) hoặc phải (-1).</summary>
            public float Bulge => side != 0f ? Mathf.Sign(side) : 1f;

            [NonSerialized] public float[] angles, velocities, targets, spacing, spacingTargets, wave;
            [NonSerialized] public Vector2 home;        // điểm chạm đất ở dáng gốc (toạ độ Torso)
            [NonSerialized] public Vector2 ik;          // (xoay gốc, cuộn) đang dùng cho IK chân, độ
            [NonSerialized] public float stretch = 1f;  // đoạn tới chân dài ra / ngắn lại bao nhiêu lần
            [NonSerialized] public Vector2 footTarget;  // đích IK của chân (toạ độ Torso), để vẽ gizmo
            [NonSerialized] public bool footGripping;   // chân đang bám đất (đứng yên)
            [NonSerialized] public bool reaching;       // đang vươn / nắm một điểm: bám sát đích thay vì lò xo
            [NonSerialized] public float reach = 1f;    // 0..1 tiến độ vươn tới điểm nắm
            [NonSerialized] public Vector2 reachFrom;   // world: chóp đoạn duỗi lúc bắt đầu vươn
            [NonSerialized] public float planted;       // 0..1: đoạn tới chân theo đúng nghiệm IK (đứng trên đất)
            [NonSerialized] public float bindSignature;
            [NonSerialized] public bool locked;         // khoá chân: chóp đang bám được giữ đứng yên trên đất
            [NonSerialized] public float lockGround;    // vị trí chóp đang khoá, đo dọc mặt đất (unit tranh)
            [NonSerialized] public Vector2 ik2;         // IK đoạn gốc khi bò kiểu sâu đo
            [NonSerialized] public int exactUpTo;       // xương tới đây theo đúng đích (không lò xo) khi đứng trên đất
            [NonSerialized] public bool exactPath;      // đang bò kiểu sâu đo / rắn: cả xúc tu theo đúng đường vẽ sẵn
            [NonSerialized] public int contactJoint = -1; // khớp đang chạm đất khi theo đường (-1 = điểm chạm như tranh)
            [NonSerialized] public float snakePhase;    // pha sóng rắn chạy dọc xúc tu (radian)
            [NonSerialized] public float swapBlend;     // vừa đổi hướng: chuyển dáng mượt trong chốc lát
            [NonSerialized] public float[] restTargets, restSpacing;
            [NonSerialized] public float release;       // độ lệch còn lại sau khi mở khoá, giảm dần về 0
            [NonSerialized] public float[] actAngles, actSpacing;     // dáng đòn đang tính (góc tuyệt đối trong khung "quay phải")
            [NonSerialized] public float[] blendAngles, blendSpacing; // chuyển dáng: lệch giữa dáng đang hiện và dáng mới, giảm dần về 0
            [NonSerialized] public float blendTime, blendLength;
            [NonSerialized] public bool blendPending;   // frame sau lấy độ lệch (khi đã có đích mới)
            [NonSerialized] public bool acting;         // đang ra đòn: theo đúng dáng đòn (không qua lò xo)
            [NonSerialized] public int actSerial;       // đòn thứ mấy xúc tu đang theo (đòn mới nối vào thì chuyển dáng)

            public int Count => bones.Length;

            public bool IsValid
            {
                get
                {
                    if (bones.Length < 2 || bindAngles.Length != bones.Length || bindLengths.Length != bones.Length)
                        return false;
                    foreach (Transform bone in bones)
                    {
                        if (bone == null)
                            return false;
                    }

                    return true;
                }
            }
        }

        /// <summary>Nhịp bò của một xúc tu: bám đất (kéo / đẩy thân) rồi nhấc lên đưa tới chỗ bám mới.</summary>
        [Serializable]
        public class ArmGait
        {
            [Tooltip("Lệch pha so với nhịp chung (0..1): các xúc tu bám đất lần lượt chứ không cùng lúc")]
            [Range(0f, 1f)] public float phase;
            [Tooltip("Phần nhịp chóp bám đất (đứng yên trên đất trong khi thân trôi qua); phần còn lại là nhấc lên đưa tới")]
            [Range(0.2f, 0.9f)] public float grip = 0.55f;
            [Tooltip("Lúc vừa bám, chóp đặt cách chỗ nghỉ bấy nhiêu (unit tranh; dương = phía trước)")]
            public float plantAhead = 0.3f;
            [Tooltip("Nhấc chóp cao bấy nhiêu khi đưa tới (unit tranh)")]
            public float lift = 0.06f;
            [Tooltip("Chóp cuộn bao nhiêu khi đang bám (1 = như tranh, < 1 duỗi ra, > 1 cuộn chặt)")]
            public float gripCurl = 1.1f;
            [Tooltip("Chóp cuộn bao nhiêu khi nhấc lên đưa tới")]
            public float swingCurl = 0.6f;
        }

        /// <summary>Trườn của một xúc tu: chóp nằm sát đất, chỉ trượt nhẹ tới lui; dáng uốn do sóng tạo ra.</summary>
        [Serializable]
        public class ArmSlither
        {
            [Tooltip("Lệch nhịp so với hai xúc tu kia (0..1)")]
            [Range(0f, 1f)] public float phase;
            [Tooltip("Chỗ xúc tu chạm đất dời đi bao xa (unit tranh; dương = phía trước)")]
            public float reach;
            [Tooltip("Chỗ chạm đất trượt tới lui bao nhiêu theo nhịp (unit tranh)")]
            public float slide = 0.02f;
            [Tooltip("Chóp cong lên bao nhiêu (0 = thẳng như đuôi rắn, 1 = cong lên khoảng 60 độ)")]
            public float curl = 0.3f;
            [Tooltip("Độ cong chóp lên xuống theo nhịp")]
            public float curlSwing = 0.15f;
        }

        [Header("Xem / chỉnh khung xương")]
        [Tooltip("Dáng xem trước trong Scene khi không Play. Bấm ▶ Chạy thử (phía trên) để xem chuyển động")]
        [SerializeField] Preview preview = Preview.Idle;
        [Tooltip("Kéo để xem từng thời điểm của dáng đang xem trước (nhịp bò, sóng xúc tu…); kéo hết sang phải = mắt nhắm")]
        [SerializeField, Range(0f, 1f)] float previewTime;
        [Tooltip("Vẽ xương (vàng → đỏ từ gốc ra chóp), chỗ chân đang bám (chấm xanh), đích IK (dấu +) trong cửa sổ Scene")]
        [SerializeField] bool showBones = true;
        [Tooltip("Tắt để code thôi điều khiển xương: tự chọn và xoay các xương L_00…, M_00…, MANTLE_00… trong Scene để xem ảnh uốn theo")]
        [SerializeField] bool driveBones = true;

        [Header("Kích thước")]
        [Tooltip("Phóng to cả con (vật lý không đổi). 1 = cao 1,5 unit như tranh gốc")]
        [SerializeField, Range(0.6f, 2.5f)] float size = 1.5f;
        [Tooltip("Lật cả con khi đổi hướng. Tắt (mặc định): đầu, mắt giữ nguyên, chỉ đổi chiều các xúc tu")]
        [SerializeField] bool flipBody;

        [Header("Bò")]
        [Tooltip("Thân đi được bấy nhiêu (unit tranh) trong một vòng bò (một lượt clip Crawl). Lớn = sải dài, nhịp chậm")]
        [SerializeField, Min(0.2f)] float cycleDistance = 1f;
        [Tooltip("Bò nhanh hơn mức này (unit/giây) mới bước")]
        [SerializeField] float crawlThreshold = 0.3f;
        [Tooltip("Đoạn xúc tu tới chỗ bám ngắn lại / dài ra tối đa bao nhiêu lần khi kéo, đẩy")]
        [SerializeField] Vector2 armStretch = new Vector2(0.75f, 1.3f);
        [Tooltip("Vừa tiếp đất, xúc tu chuyển từ dáng bay sang dáng bám đất trong thời gian này")]
        [SerializeField] float plantTime = 0.12f;
        [Tooltip("Hạ thấp đầu thêm khi bò (unit tranh, cộng thêm vào Idle Crouch); xúc tu tự co lại để vẫn sát đất")]
        [SerializeField, Range(0f, 0.5f)] float crawlCrouch = 0.2f;
        [Tooltip("Đầu dẹt xuống một chút khi bò (0 = giữ nguyên)")]
        [SerializeField, Range(0f, 0.3f)] float crawlFlatten = 0.05f;

        [Header("Sóng uốn lượn trên xúc tu (tới tận ngọn)")]
        [Tooltip("Biên độ sóng khi di chuyển (độ mỗi xương ở gốc; ngọn mạnh hơn theo Wave Tip Boost)")]
        [SerializeField, Range(0f, 25f)] float waveAmount = 13f;
        [Tooltip("Ngọn uốn mạnh gấp bao nhiêu lần gốc")]
        [SerializeField, Range(1f, 5f)] float waveTipBoost = 2f;
        [Tooltip("Số xương trên một bước sóng (nhỏ = sóng dày, gợn nhiều)")]
        [SerializeField, Range(3f, 20f)] float waveLength = 8f;
        [Tooltip("Tốc độ sóng chạy từ gốc ra ngọn")]
        [SerializeField, Range(0f, 20f)] float waveSpeed = 6f;
        [Tooltip("Đứng yên thì sóng bằng bao nhiêu phần lúc di chuyển")]
        [SerializeField, Range(0f, 1f)] float waveIdle = 0.3f;

        [Header("Vây quanh đầu lượn sóng")]
        [Tooltip("Vây lắc qua lại bao nhiêu độ khi di chuyển (0 = vây đứng yên)")]
        [SerializeField, Range(0f, 40f)] float finSway = 18f;
        [Tooltip("Vây vươn dài ra / thu ngắn lại theo sóng (tỉ lệ)")]
        [SerializeField, Range(0f, 0.3f)] float finPulse = 0.08f;
        [Tooltip("Số con sóng chạy vòng quanh đầu cùng lúc")]
        [SerializeField, Range(0.5f, 4f)] float finWaves = 1.5f;
        [Tooltip("Tốc độ sóng chạy vòng quanh đầu")]
        [SerializeField, Range(0f, 15f)] float finSpeed = 5f;
        [Tooltip("Đứng yên thì vây lượn bằng bao nhiêu phần lúc di chuyển")]
        [SerializeField, Range(0f, 1f)] float finIdle = 0.35f;

        [Header("Bò bằng clip — chỉnh tay trong cửa sổ Animation")]
        [Tooltip("Bật: dáng bò lấy từ clip Crawl.anim (tay cầm Pose › Crawl › Foot L/M/R, Body). Tắt: code tự tính từ mục Gait bên dưới")]
        [SerializeField] bool useCrawlClip = true;
        [Tooltip("Chóp đang bám (Contact = 1) được giữ đứng yên hẳn trên đất, kể cả khi đường cong vẽ tay hơi lệch")]
        [SerializeField] bool footLock = true;
        [Tooltip("Bản sao đường cong của Crawl.anim mà game dùng khi chạy (tự cập nhật mỗi khi sửa clip)")]
        [SerializeField] OctopusCrawlCurves crawlCurves;
        [Tooltip("Tay cầm chóp xúc tu trái, giữa, phải")]
        [SerializeField] OctopusCrawlFoot[] crawlFeet = Array.Empty<OctopusCrawlFoot>();
        [Tooltip("Tay cầm thân")]
        [SerializeField] OctopusCrawlBody crawlBody;

        [Header("Bò bằng code — dùng khi tắt Use Crawl Clip; nút \"Tạo lại clip bò\" cũng lấy dáng từ đây")]
        [Tooltip("Trườn = cả 3 xúc tu nằm trên đất uốn lượn hình rắn. Sâu đo = hai xúc tu bên co lại thành vòm rồi vươn ra nằm thẳng. Bước = nhấc lên đặt xuống như đi bộ")]
        [SerializeField] CrawlStyle crawlStyle = CrawlStyle.Slither;
        [Tooltip("Kiểu Trườn: khúc uốn gồ lên cao bao nhiêu (unit tranh). 0 = nằm thẳng trên đất")]
        [SerializeField, Range(0f, 0.4f)] float snakeHeight = 0.15f;
        [Tooltip("Trườn: một khúc sóng dài bao nhiêu (unit tranh). Nhỏ = nhiều khúc ngắn, lớn = ít khúc dài mềm")]
        [SerializeField, Range(0.15f, 1.2f)] float snakeLength = 0.4f;
        [Tooltip("Trườn: sóng chạy dọc xúc tu nhanh / chậm (số khúc sóng chạy qua mỗi nhịp bò). Sóng luôn chạy về phía sau như rắn")]
        [SerializeField, Range(0f, 3f)] float snakeFlow = 1f;
        [Tooltip("Trườn: xúc tu chạm đất ở xương thứ mấy tính từ gốc (ít nhất; tự thêm xương nếu thân ở xa mặt đất / mặt đá); từ đó ra tận ngọn nằm trên đất uốn lượn")]
        [SerializeField, Range(2, 10)] int snakeBaseBone = 4;
        [Tooltip("Trườn: chỗ xúc tu chạm đất cách gốc bao xa ra phía ngoài (unit tranh)")]
        [SerializeField, Range(0f, 0.6f)] float snakeBaseOut = 0.16f;
        [Tooltip("Trườn: trục xương cách mặt đất bấy nhiêu ở chỗ chạm đất (khoảng nửa bề dày xúc tu, unit tranh)")]
        [SerializeField, Range(0f, 0.15f)] float snakeThickness = 0.05f;
        [Tooltip("Trườn: xúc tu giữa vươn ra phía trước (bật) hay kéo lê phía sau cùng xúc tu sau (tắt)")]
        [SerializeField] bool snakeMiddleForward;
        [Tooltip("Kiểu Sâu đo — hai xúc tu bên. Xúc tu chạm đất ở xương thứ mấy tính từ gốc; từ đó ra ngọn là đoạn uốn vòm")]
        [SerializeField, Range(2, 10)] int inchBaseBone = 5;
        [Tooltip("Sâu đo: chỗ xúc tu chạm đất cách gốc bao xa ra phía ngoài (unit tranh)")]
        [SerializeField, Range(0f, 0.6f)] float inchBaseOut = 0.22f;
        [Tooltip("Sâu đo: vòm cao nhất khi chóp co về còn bấy nhiêu phần chiều dài đoạn uốn (nhỏ = vòm cao)")]
        [SerializeField, Range(0.2f, 0.95f)] float inchArch = 0.45f;
        [Tooltip("Sâu đo: chóp nhấc cao bao nhiêu khi vươn tới (unit tranh)")]
        [SerializeField, Range(0f, 0.4f)] float inchLift = 0.12f;
        [Tooltip("Sâu đo: khi vươn tới, đỉnh vòm lệch về phía gốc (0.5 = giữa, nhỏ = sát gốc)")]
        [SerializeField, Range(0.1f, 0.9f)] float inchSkew = 0.28f;
        [Tooltip("Sâu đo: chóp quặp xuống đất khi vòm cao (0 = thẳng, 1 = quặp hẳn)")]
        [SerializeField, Range(0f, 1.5f)] float inchHook = 0.9f;
        [Tooltip("Sâu đo: trục xương cách mặt đất bấy nhiêu khi xúc tu nằm sát đất (khoảng nửa bề dày xúc tu, unit tranh)")]
        [SerializeField, Range(0f, 0.15f)] float inchThickness = 0.05f;
        [Tooltip("Sâu đo: lệch nhịp của xúc tu phía sau so với xúc tu phía trước (0..1)")]
        [SerializeField, Range(0f, 1f)] float inchRearPhase;
        [Tooltip("Kiểu Trườn (và xúc tu giữa ở kiểu Sâu đo): 0 xúc tu phía sau · 1 giữa · 2 phía trước (theo hướng đang đi). Trườn: Reach / Slide dời chỗ chạm đất, Curl = chóp cong lên")]
        [SerializeField] ArmSlither[] slither = DefaultSlither();
        [Tooltip("Kiểu Bước: 0 xúc tu phía sau = đẩy · 1 giữa = lăn dưới thân · 2 phía trước = vươn tới kéo (theo hướng đang đi)")]
        [SerializeField] ArmGait[] gait = DefaultGait();
        [Tooltip("Thân chồm tới theo mỗi nhịp kéo (unit tranh)")]
        [SerializeField] float surge = 0.05f;
        [SerializeField] float crawlBob = 0.015f;
        [Tooltip("Đầu ngả ra sau khi bò (độ), như áo bạch tuộc kéo lê phía sau")]
        [SerializeField] float mantleTrail = 10f;
        [Tooltip("Đầu lắc theo nhịp chồm (độ)")]
        [SerializeField] float mantleSurge = 3f;
        [Tooltip("Hai xúc tu bên hạ chóp xuống sát đất (0 = giữ như tranh, 1 = nằm hẳn xuống đất)")]
        [SerializeField, Range(0f, 1f)] float plantSideTentacles = 1f;

        [Header("Đứng yên")]
        [Tooltip("Hạ thấp đầu lúc đứng yên (unit tranh); xúc tu tự co lại cho vừa")]
        [SerializeField, Range(0f, 0.5f)] float idleCrouch = 0.27f;
        [Tooltip("Hai xúc tu bên xoè rộng ra, nằm sát đất (unit tranh)")]
        [SerializeField, Range(0f, 0.4f)] float idleSpread = 0.1f;
        [Tooltip("Đầu phồng xẹp như thở")]
        [SerializeField, Range(0f, 0.1f)] float breathAmount = 0.035f;
        [SerializeField] float breathSpeed = 1.8f;
        [Tooltip("Xúc tu cuộn lại / duỗi ra chậm rãi")]
        [SerializeField, Range(0f, 0.5f)] float idleCurl = 0.12f;
        [Tooltip("Chóp xúc tu ngoe nguẩy (độ mỗi xương)")]
        [SerializeField] float tipWiggle = 3f;

        [Header("Nhảy / rơi")]
        [SerializeField] float takeoffTime = 0.12f;
        [Tooltip("Thời gian thân hạ xuống rồi đứng dậy khi tiếp đất")]
        [SerializeField] float landTime = 0.22f;
        [Tooltip("Tiếp đất mạnh nhất thì thân hạ thấp bấy nhiêu (unit tranh)")]
        [SerializeField] float landDip = 0.14f;
        [Tooltip("Bay nhanh hơn mức này (unit/giây) xúc tu bắt đầu kéo theo sau")]
        [SerializeField] float trailSpeed = 3f;
        [Tooltip("Bay nhanh cỡ này thì xúc tu duỗi hết mức")]
        [SerializeField] float fullTrailSpeed = 16f;
        [Tooltip("Duỗi tối đa (0 = giữ cuộn như tranh, 1 = thẳng đơ)")]
        [SerializeField, Range(0f, 1f)] float maxUncurl = 0.7f;
        [Tooltip("Gốc xúc tu xoay theo hướng bay nhiều đến đâu")]
        [SerializeField, Range(0f, 1f)] float trailFollow = 0.85f;
        [Tooltip("Nghiêng thân theo vận tốc ngang khi ở trên không (độ)")]
        [SerializeField] float airLean = 12f;
        [Tooltip("Đầu ngả ngược hướng bay (độ)")]
        [SerializeField] float mantleDrag = 12f;

        [Header("Vươn tới đá (đu, bám)")]
        [Tooltip("Số xương đoạn gốc được kéo dài; các xương còn lại là chóp cuộn quấn vào đá")]
        [SerializeField, Min(1)] int reachBones = 8;
        [SerializeField] float shootDuration = 0.1f;
        [Tooltip("Đoạn gốc ngắn hơn tỉ lệ này so với chiều dài vẽ thì uốn cong thay vì co lại")]
        [SerializeField, Range(0.3f, 1f)] float minCompression = 0.75f;
        [Tooltip("Sóng uốn dọc xúc tu đang nắm đá (độ)")]
        [SerializeField] float grabWave = 2.5f;
        [Tooltip("Chóp cuộn chặt hơn tranh bao nhiêu khi nắm đá")]
        [SerializeField, Range(0.5f, 2f)] float holdCurl = 1.15f;

        [Header("Bám ngược dưới đá")]
        [Tooltip("Treo ngược: thân kéo sát lên đá bao nhiêu (unit tranh). 0 = xúc tu dài như tranh, âm = treo lủng lẳng xa đá hơn")]
        [SerializeField, Range(-0.3f, 0.5f)] float clingCrouch = 0.3f;
        [Tooltip("Treo ngược: xúc tu ép sát vào đá thêm bấy nhiêu (unit tranh) cho khỏi hở với mép đá")]
        [SerializeField, Range(0f, 0.3f)] float clingReach = 0.03f;
        [Tooltip("Thời gian lật ngược người khi bám vào đá / rời đá (giây)")]
        [SerializeField, Range(0.05f, 1f)] float hangFlipTime = 0.25f;

        [Header("Lò xo")]
        [Tooltip("Độ cứng ở gốc xúc tu (rad/s): càng lớn càng bám dáng")]
        [SerializeField] float rootStiffness = 26f;
        [Tooltip("Độ cứng ở chóp: nhỏ hơn gốc thì chóp trễ và vẫy theo")]
        [SerializeField] float tipStiffness = 12f;
        [Tooltip("1 = không nảy, nhỏ hơn thì nảy qua lại vài lần")]
        [SerializeField, Range(0.2f, 1.5f)] float damping = 0.65f;
        [Tooltip("Độ cứng của đầu: nhỏ = mềm, lắc lư nhiều")]
        [SerializeField] float mantleStiffness = 10f;
        [SerializeField, Range(0.2f, 1.5f)] float mantleDamping = 0.45f;
        [SerializeField] float reachSnap = 40f;
        [SerializeField] float spacingRate = 18f;
        [SerializeField] float rotationSmoothing = 14f;
        [Tooltip("Thời gian quay mặt trái / phải")]
        [SerializeField] float turnTime = 0.1f;

        [Header("Mắt")]
        [SerializeField] Vector2 blinkInterval = new Vector2(2f, 5f);
        [SerializeField] float blinkTime = 0.14f;
        [Tooltip("Con ngươi liếc xa nhất (unit tranh)")]
        [SerializeField] Vector2 pupilRange = new Vector2(0.04f, 0.012f);
        [Tooltip("Con ngươi luôn dựng đứng dù thân nghiêng (mắt bạch tuộc thật giữ con ngươi theo phương ngang trời)")]
        [SerializeField] bool pupilUpright = true;

        [Header("Chiêu laze")]
        [Tooltip("Vận sức thì mắt mở to thêm bao nhiêu (tỉ lệ)")]
        [SerializeField, Range(0f, 0.4f)] float laserEyeWiden = 0.25f;
        [Tooltip("Vận sức thì con ngươi co lại còn bấy nhiêu phần bề ngang (khe mảnh)")]
        [SerializeField, Range(0.2f, 1f)] float laserPupilSlit = 0.45f;
        [Tooltip("Bắn xong mắt lim dim bao lâu (giây), như mỏi mắt")]
        [SerializeField, Range(0f, 2f)] float laserTiredTime = 0.8f;
        [Tooltip("Đầu bị đẩy ngả ra sau khi bắn (độ)")]
        [SerializeField, Range(0f, 20f)] float laserRecoil = 6f;
        [Tooltip("Lúc tia bung ra, thân bẹp lại bao nhiêu (giật lùi)")]
        [SerializeField, Range(0f, 0.5f)] float laserKick = 0.18f;
        [Tooltip("Vận sức: cả người ngả dần ra sau (ngược hướng bắn) tới bấy nhiêu độ lúc sắp bắn. Xúc tu vẫn bám đất")]
        [SerializeField, Range(0f, 15f)] float laserChargeLean = 5f;
        [Tooltip("Vận sức: đầu ngửa thêm ra sau bấy nhiêu độ (qua lò xo đầu nên trễ, nảy sau thân)")]
        [SerializeField, Range(0f, 40f)] float laserChargeNod = 18f;
        [Tooltip("Vận sức: thân nhổm cao lên bấy nhiêu (unit tranh) và lùi ra sau một chút, xúc tu duỗi ra theo")]
        [SerializeField, Range(0f, 0.3f)] float laserChargeRise = 0.12f;
        [Tooltip("Vận sức: cả người run bần bật, mạnh dần tới lúc bắn (unit tranh)")]
        [SerializeField, Range(0f, 0.06f)] float laserChargeTremble = 0.016f;

        [Header("Co giãn (squash & stretch)")]
        [SerializeField] float squashStiffness = 260f;
        [SerializeField] float squashDamping = 13f;
        [SerializeField, Range(0f, 1f)] float jumpStretch = 0.3f;
        [SerializeField, Range(0f, 1f)] float landSquash = 0.35f;
        [SerializeField, Range(0f, 1f)] float grabStretch = 0.15f;
        [SerializeField, Range(0f, 1f)] float releaseStretch = 0.2f;

        [Header("Vệt tốc độ")]
        [Tooltip("Bay / đu nhanh hơn mức này (unit/giây) mới có vệt")]
        [SerializeField] float speedTrailThreshold = 12f;
        [Tooltip("Nhanh hơn ngưỡng bấy nhiêu (unit/giây) thì vệt đậm hết mức")]
        [SerializeField, Min(0.1f)] float speedTrailRamp = 5f;
        [Tooltip("Rơi / bay gần thẳng đứng (lệch khỏi phương dọc ít hơn góc này, độ) thì vệt tắt dần: vệt dựng đứng sau đầu trông như cái cột")]
        [SerializeField, Range(0f, 60f)] float speedTrailVerticalCut = 30f;
        [Tooltip("Vệt hiện ra / mờ đi trong bấy nhiêu giây (không bật tắt đột ngột)")]
        [SerializeField, Range(0.02f, 0.5f)] float speedTrailFade = 0.12f;

        [Header("Kích thước tranh (dựng tự động từ rig.json)")]
        [Tooltip("Tâm vật lý cao hơn mặt đất bấy nhiêu (bán kính collider, unit world)")]
        [SerializeField] float groundOffset = 0.5f;
        [Tooltip("Tâm thân cao bấy nhiêu tính từ chân (unit tranh; điểm xoay khi ở trên không)")]
        [SerializeField] float bodyCenter = 0.75f;

        [Header("Tham chiếu (đã nối sẵn, không cần sửa)")]
        [SerializeField] OctopusController controller;
        [SerializeField] Transform pose;
        [SerializeField] Transform torso;
        [SerializeField] Transform eye;
        [SerializeField] Transform pupil;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] TrailRenderer speedTrail;
        [Tooltip("Ba xương đầu (áo bạch tuộc)")]
        [SerializeField] Limb mantle = new Limb();
        [Tooltip("Xương vây quanh đầu (FIN_00…)")]
        [SerializeField] Transform[] headFins = Array.Empty<Transform>();
        [SerializeField] float[] headFinAngles = Array.Empty<float>();
        [Tooltip("0 trái, 1 giữa (xúc tu nắm đá khi đu), 2 phải")]
        [SerializeField] Limb[] limbs = Array.Empty<Limb>();

        const int GrabLimb = 1;
        const int FrontLimb = 2;

        Rigidbody2D _rigidbody;
        bool _subscribed;
        LaserSkill _laser;
        CombatPose _act;              // dáng ra đòn đang diễn (bộ chiêu gọi Act)
        ActClip _clip;
        float _actTime, _actDuration;
        Vector2 _actPoint;
        float _actDir = 1f;           // hướng "phía trước" trong tranh lúc ra đòn (giữ nguyên tới hết đòn)
        float _actFacing = 1f;        // hướng mặt (world) lúc ra đòn: chiều xoay tròn
        int _actSerial;
        float _actEngage;             // 0..1: đang ra đòn (đầu cứng hơn để kịp lắc theo)
        // Thân trong đòn (toạ độ tranh): dời, ngả (độ, dương = ngược chiều kim đồng hồ), co giãn, đầu ngả, xoay tròn (world)
        Vector2 _actShift;
        float _actLean, _actStretch, _actNod, _actSpin, _actPivot, _actBend;
        Vector2 _fromShift;           // lệch thân lúc đổi đòn, giảm dần về 0
        float _fromLean, _fromStretch, _fromNod, _fromSpin, _fromPivot;
        float _actBlendTime, _actBlendLength = 1f;
        bool _actGround;              // đang ra đòn trên mặt đất / mặt đá: xúc tu không xuyên qua mặt
        Vector2 _actGroundPoint, _actGroundNormal; // mặt đó trong khung đòn (Torso, lật theo hướng đòn)
        // Dáng vận sức / bắn laze (toạ độ tranh), cộng thêm vào dáng thường và dáng đòn
        float _laserPose;             // 0..1: đang gồng (đã làm mượt)
        Vector2 _laserShift;
        float _laserLean, _laserStretch, _laserNod;
        float _laserFwd = 1f, _laserAmount = 1f; // hướng bắn trong tranh (1 phải, -1 trái), biên độ theo thời gian vận sức
        // Vệt tốc độ: độ đậm hiện tại (hiện / tắt dần), màu gốc do builder đặt
        float _trailStrength, _trailApplied = -1f, _trailWidth;
        Vector2 _trailFrom;
        Gradient _trailGradient;
        GradientColorKey[] _trailColors;
        GradientAlphaKey[] _trailAlphas, _trailScaled;
        OctopusState _lastState;
        float _previewDelta;
        float _travel;
        float _wavePhase;
        float _walkAdvance; // nhịp bò vừa đi thêm trong frame này (để sóng rắn chạy theo quãng đường)
        float _editorPhase = -1f;
        bool _clipTool;
        float _time;
        float _squash;
        float _squashVelocity;
        float _walkPhase;
        float _walkWeight;
        float _artDir = 1f;   // hướng đang đi trong tranh: 1 sang phải, -1 sang trái
        float _groundBlend = 1f;
        float _clingBlend;
        Vector2 _clingOffset;
        float _angle;
        float _flip = 1f;
        float _hang = 1f;     // 1 = đứng thẳng, -1 = treo ngược dưới đá (lật dọc tranh)
        float _hangRoll;      // đang lật để bám / rời đá: cả con xoay thêm bấy nhiêu độ (lộn nửa vòng thay vì bẹp dí)
        float _hangDir = -1f; // chiều lộn (dấu góc xoay world), chốt lúc bắt đầu lật
        float _takeoffTimer;
        float _landTimer;
        float _landImpact;
        float _fallSpeed;
        float _blinkTimer = 2f;
        float _blinkClock = -1f;
        Vector2 _pupilBind;
        Vector2 _pupilOffset;
        Vector2 _glanceTarget;
        float _glanceTimer;
        bool _pupilBindStored;
        bool _warnedBones;
        Mode _mode;

        // Ảnh chụp trạng thái mỗi frame: từ controller khi Play, từ ô "Xem trước" khi không Play
        struct Snapshot
        {
            public Mode Mode;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Facing;
            public Vector2 Anchor;
            public Vector2 ClingPoint;
            public Vector2 ClingNormal;
            public Collider2D ClingSurface;
        }

        static ArmSlither[] DefaultSlither() => new[]
        {
            new ArmSlither { phase = 0.5f, reach = 0f, slide = 0.02f, curl = 0.3f, curlSwing = 0.15f },
            new ArmSlither { phase = 0.25f, reach = 0f, slide = 0.02f, curl = 0.3f, curlSwing = 0.15f },
            new ArmSlither { phase = 0f, reach = 0f, slide = 0.02f, curl = 0.3f, curlSwing = 0.15f },
        };

        static ArmGait[] DefaultGait() => new[]
        {
            // Trái (phía sau): bám gần chỗ nghỉ rồi duỗi dài ra đẩy thân, xong cuộn tròn lại đưa lên trước
            new ArmGait { phase = 0.35f, grip = 0.6f, plantAhead = 0.05f, lift = 0.09f, gripCurl = 0.85f, swingCurl = 1.4f },
            // Giữa: lăn dưới thân, nhấc thấp
            new ArmGait { phase = 0.6f, grip = 0.45f, plantAhead = 0.22f, lift = 0.04f, gripCurl = 1.05f, swingCurl = 1.25f },
            // Phải (phía trước): duỗi thẳng vươn xa ra trước, bám rồi cuộn lại kéo thân tới
            new ArmGait { phase = 0f, grip = 0.55f, plantAhead = 0.3f, lift = 0.07f, gripCurl = 1.15f, swingCurl = 0.55f },
        };

        /// <summary>Độ co giãn hiện tại: dương = đang dãn dài, âm = đang bẹp.</summary>
        public float Squash => _squash;

        /// <summary>Hệ số phóng to cả con.</summary>
        public float Size => size;

        /// <summary>Xúc tu giữa đang vươn tới / nắm đá.</summary>
        public bool IsGrabbing => limbs.Length > GrabLimb && limbs[GrabLimb].reaching;

        /// <summary>Điểm cuối đoạn gốc (đầu chóp cuộn) của xúc tu nắm đá, toạ độ world.</summary>
        public Vector2 GrabPoint => JointWorld(limbs[GrabLimb], Mathf.Min(reachBones, limbs[GrabLimb].Count - 1));

        /// <summary>Điểm chạm đất của xúc tu i (0 trái, 1 giữa, 2 phải), toạ độ world.</summary>
        public Vector2 FootPosition(int i) => limbs[i].contactJoint >= 0
            ? JointWorld(limbs[i], limbs[i].contactJoint)
            : (Vector2)torso.TransformPoint(Foot(limbs[i], limbs[i].angles, limbs[i].spacing));

        /// <summary>Xúc tu i đang bám đất (chóp đứng yên trong khi thân trôi qua).</summary>
        public bool FootGripping(int i) => limbs[i].footGripping;

        /// <summary>Tâm con mắt (world): chỗ tia laze bắn ra.</summary>
        public Vector2 EyePosition => eye != null ? (Vector2)eye.position : (Vector2)transform.position;

        /// <summary>Khớp j (0 = gốc, Count = chóp) của xúc tu i, toạ độ world.</summary>
        public Vector2 JointPosition(int i, int j) => JointWorld(limbs[i], j);

        /// <summary>Hướng đang đi trong tranh: 1 sang phải, -1 sang trái.</summary>
        public float ArtDirection => _artDir;

        /// <summary>Chóp xúc tu i, toạ độ world.</summary>
        public Vector2 TipPosition(int i) => JointWorld(limbs[i], limbs[i].Count);

        /// <summary>Đỉnh đầu (cuối xương đầu trên cùng), toạ độ world.</summary>
        public Vector2 HeadTipPosition => JointWorld(mantle, mantle.Count);

        /// <summary>Đang nhắm mắt (chớp).</summary>
        public bool EyeClosed => eye != null && eye.localScale.y < 0.5f;

        /// <summary>Editor gán: cửa sổ Animation đang xem trước / ghi clip (AnimationMode). Khi đó tay cầm do cửa sổ Animation đặt.</summary>
        public static Func<bool> EditorAnimationModeActive;

        /// <summary>
        /// Dáng bò đang lấy từ clip Crawl.anim: chỉ khi clip đã được sửa tay (bảng ⚡ / cửa sổ Animation), hoặc đang mở
        /// công cụ chỉnh clip. Clip chưa sửa tay thì dùng thẳng các ô "Bò bằng code" để chỉnh là thấy ngay.
        /// </summary>
        public bool UsesCrawlClip => HasCrawlClip && (crawlCurves.handEdited || _clipTool);

        /// <summary>Có clip bò + đủ tay cầm (để các công cụ chỉnh clip làm việc).</summary>
        public bool HasCrawlClip
        {
            get
            {
                if (!useCrawlClip || crawlCurves == null || crawlBody == null || crawlFeet == null || crawlFeet.Length != 3)
                    return false;
                foreach (OctopusCrawlFoot foot in crawlFeet)
                {
                    if (foot == null)
                        return false;
                }

                return true;
            }
        }

        public OctopusCrawlCurves CrawlCurves => crawlCurves;

        /// <summary>Tay cầm chóp xúc tu i (0 trái, 1 giữa, 2 phải) của clip bò.</summary>
        public OctopusCrawlFoot CrawlFoot(int i) => crawlFeet != null && i >= 0 && i < crawlFeet.Length ? crawlFeet[i] : null;

        /// <summary>Tay cầm thân của clip bò.</summary>
        public OctopusCrawlBody CrawlBody => crawlBody;

        /// <summary>Transform Pose (toạ độ trong tranh, gốc ở chân).</summary>
        public Transform PoseTransform => pose;

        /// <summary>Tâm thân cao bấy nhiêu tính từ chân (unit tranh).</summary>
        public float BodyCenter => bodyCenter;

        /// <summary>Độ cao "mặt đất" của tay cầm chóp xúc tu i khi đang bám (unit tranh, trong Pose).</summary>
        public float CrawlGroundY(int i)
        {
            if (!HasReferences)
                return 0f;
            EnsureRuntime();
            if (crawlStyle == CrawlStyle.Inchworm && limbs[i].side != 0f)
                return inchThickness; // tay cầm = chóp xúc tu, trục xương cao bằng nửa bề dày
            if (crawlStyle == CrawlStyle.Slither)
                return snakeThickness; // tay cầm = chỗ xúc tu chạm đất
            float y = limbs[i].home.y;
            return limbs[i].side != 0f ? Mathf.Lerp(y, 0f, plantSideTentacles) : y;
        }

        /// <summary>Bảng "Chỉnh dáng bò" (Editor) đặt thời điểm đang xem của vòng bò: 0..1; số âm = thôi.</summary>
        public void SetEditorCrawlPhase(float phase) => _editorPhase = phase;

        /// <summary>Editor gọi mỗi frame khi bấm ▶ Chạy thử trong Scene (không Play): cho thời gian trôi để xem chuyển động.</summary>
        public void TickPreview(float deltaTime) => _previewDelta = Mathf.Clamp(deltaTime, 0f, 0.05f);

        bool HasReferences
        {
            get
            {
                if (controller == null || pose == null || torso == null || eye == null || pupil == null ||
                    limbs.Length != 3 || mantle == null || !mantle.IsValid)
                    return false;
                foreach (Limb limb in limbs)
                {
                    if (limb == null || !limb.IsValid)
                        return false;
                }

                return true;
            }
        }

        void OnEnable()
        {
            _warnedBones = false;
            if (HasReferences)
                RefreshBind();
        }

        void Start()
        {
            if (!Application.isPlaying || controller == null)
                return;

            _rigidbody = controller.GetComponent<Rigidbody2D>();
            // Animator chỉ để cửa sổ Animation mở được clip bò; khi chơi, code tự đọc clip theo quãng đường đã bò
            if (TryGetComponent(out Animator clipHost))
                clipHost.enabled = false;
            controller.StateChanged += OnStateChanged;
            controller.Jumped += OnJumped;
            controller.Swing.Attached += OnAttached;
            controller.Swing.Released += OnReleased;
            _lastState = controller.State;
            _flip = controller.Facing;
            _laser = controller.GetComponent<LaserSkill>();
            if (_laser != null)
                _laser.PhaseChanged += OnLaserPhase;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (!_subscribed || controller == null)
                return;

            controller.StateChanged -= OnStateChanged;
            controller.Jumped -= OnJumped;
            if (_laser != null)
                _laser.PhaseChanged -= OnLaserPhase;
            if (controller.Swing != null)
            {
                controller.Swing.Attached -= OnAttached;
                controller.Swing.Released -= OnReleased;
            }
        }

        void LateUpdate()
        {
            if (!HasReferences)
                return;

            bool playing = Application.isPlaying && _rigidbody != null;
            if (!Application.isPlaying)
                RefreshBind(); // sửa xương trong Skinning Editor là thấy ngay
            EnsureRuntime();

            float dt = playing ? Mathf.Min(Time.deltaTime, 0.05f) : _previewDelta;
            _previewDelta = 0f;
            bool live = dt > 0f; // thời gian có trôi (Play hoặc đang ▶ Chạy thử), không thì là ảnh tĩnh của ô xem trước
            // Cửa sổ Animation đang xem / ghi clip: tay cầm do nó đặt, rig chỉ việc bò theo
            bool animationWindow = !playing && HasCrawlClip && EditorAnimationModeActive != null && EditorAnimationModeActive();
            bool crawlPanel = !playing && !live && HasCrawlClip && _editorPhase >= 0f;
            _clipTool = animationWindow || crawlPanel; // đang mở công cụ chỉnh clip: xem đúng clip
            Snapshot snap = playing ? Capture() : PreviewSnapshot(animationWindow || crawlPanel);
            _mode = snap.Mode;
            _time = live ? _time + dt : previewTime * 6f;

            UpdateTimers(dt);
            UpdateSquash(dt);
            UpdateBlend(snap, dt, live);
            UpdateAct(dt, live);
            UpdateLaserPose(dt, live);
            if (OnSurface(snap.Mode) && UsesCrawlClip && !animationWindow && crawlCurves != null)
                crawlCurves.Sample(transform, _walkPhase * Mathf.Max(crawlCurves.length, 0.01f));

            Vector2 torsoOffset = TorsoOffset(snap);
            torso.localPosition = torsoOffset;
            PlacePose(snap, dt, live);

            Vector2 artVelocity = ToArt(snap.Velocity);
            float motion = Motion(snap);
            // Sóng chậm khi đứng yên, nhanh hơn khi di chuyển (pha cộng dồn nên đổi tốc độ không giật)
            _wavePhase = live ? _wavePhase + dt * waveSpeed * Mathf.Lerp(0.6f, 1f, motion) : _time * waveSpeed;
            if (driveBones)
            {
                PrepareActGround(snap);
                for (int i = 0; i < limbs.Length; i++)
                {
                    Limb limb = limbs[i];
                    limb.footGripping = false;
                    UpdateWave(limb, i, motion);
                    switch (snap.Mode)
                    {
                        // Trên sàn, hoặc treo ngược dưới đá (tranh lật dọc nên mặt đá là "mặt đất" của tranh): cùng một kiểu bò
                        case Mode.Ground:
                        case Mode.Cling:
                            limb.reaching = false;
                            limb.planted = live ? Mathf.MoveTowards(limb.planted, 1f, dt / Mathf.Max(plantTime, 0.01f)) : 1f;
                            GroundTargets(limb, i, torsoOffset, dt, live);
                            break;

                        case Mode.Swing when i == GrabLimb:
                            ReachTargets(limb, snap.Anchor, dt, shootDuration, holdCurl);
                            break;

                        default:
                            limb.reaching = false;
                            AirTargets(limb, i, artVelocity);
                            break;
                    }

                    if (!OnSurface(snap.Mode))
                        limb.planted = 0f;

                    ActLimb(limb, i, snap.Mode, dt, live);
                    Integrate(limb, dt, !live, rootStiffness, tipStiffness, damping);
                    Apply(limb);
                }

                MantleTargets(snap, artVelocity);
                // Ra đòn: đầu cứng hơn để kịp ngả theo nhịp đòn (vẫn trễ, nảy sau thân)
                float headStiffness = mantleStiffness * (1f + 1.2f * _actEngage);
                Integrate(mantle, dt, !live, headStiffness, headStiffness * 0.8f, mantleDamping);
                Apply(mantle);
                float breath = Mathf.Sin(_time * breathSpeed) * breathAmount * (OnSurface(snap.Mode) ? 1f : 0.4f);
                float flat = OnSurface(snap.Mode) ? crawlFlatten * _walkWeight : 0f;
                mantle.bones[Mathf.Min(1, mantle.Count - 1)].localScale =
                    new Vector3(1f + breath * 0.6f + flat * 0.5f, 1f + breath - flat, 1f);
                ApplyFins(motion);
            }

            UpdateEye(snap, dt, live);

            if (glow != null)
                glow.transform.position = snap.Position + (Vector2)pose.TransformVector(Vector3.up).normalized * (bodyCenter * size - groundOffset);
            UpdateSpeedTrail(snap, dt, playing);

            if (snap.Mode == Mode.Air)
                _fallSpeed = Mathf.Max(_fallSpeed, -snap.Velocity.y);
        }

        // Vệt tốc độ khi bay / đu nhanh: nhả ra từ tâm thân (khuất sau đầu nên đầu vệt không lộ mép cắt), hiện / tắt dần
        // theo tốc độ; rơi / bay gần thẳng đứng thì tắt hẳn (vệt dựng đứng sau đầu trông như cái cột)
        void UpdateSpeedTrail(Snapshot snap, float dt, bool playing)
        {
            if (speedTrail == null)
                return;

            Vector2 from = torso.TransformPoint(new Vector3(0f, bodyCenter, 0f));
            if (!playing)
            {
                // Ngoài Play: không đụng tới màu / độ rộng (khỏi ghi nhầm vào scene), chỉ đặt chỗ
                speedTrail.transform.position = from;
                speedTrail.emitting = false;
                return;
            }

            float target = 0f;
            float speed = snap.Velocity.magnitude;
            if (snap.Mode is Mode.Swing or Mode.Air && speed > 0.01f)
            {
                float fast = Mathf.InverseLerp(speedTrailThreshold, speedTrailThreshold + speedTrailRamp, speed);
                float tilt = Mathf.Acos(Mathf.Clamp01(Mathf.Abs(snap.Velocity.y) / speed)) * Mathf.Rad2Deg; // 0 = thẳng đứng
                target = fast * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(speedTrailVerticalCut, speedTrailVerticalCut + 15f, tilt));
            }

            _trailStrength = Mathf.MoveTowards(_trailStrength, target, dt / speedTrailFade);
            bool emit = _trailStrength > 0.01f;
            speedTrail.transform.position = from;
            // Vừa bật lại, hoặc vừa dịch chuyển tức thời (hồi sinh, đặt lại chỗ): xoá vệt cũ cho khỏi nối thành vạch dài
            if (emit && (!speedTrail.emitting || (from - _trailFrom).sqrMagnitude > 4f))
                speedTrail.Clear();
            speedTrail.emitting = emit;
            _trailFrom = from;

            // Cả vệt (kể cả đoạn đã nhả) mờ đi và mảnh lại theo độ đậm, giữ màu gốc do builder đặt
            if (_trailGradient == null)
            {
                Gradient source = speedTrail.colorGradient;
                _trailColors = source.colorKeys;
                _trailAlphas = source.alphaKeys;
                _trailScaled = new GradientAlphaKey[_trailAlphas.Length];
                _trailGradient = new Gradient();
                _trailWidth = speedTrail.widthMultiplier;
            }

            if (Mathf.Abs(_trailApplied - _trailStrength) < 0.002f)
                return;
            _trailApplied = _trailStrength;
            for (int i = 0; i < _trailAlphas.Length; i++)
                _trailScaled[i] = new GradientAlphaKey(_trailAlphas[i].alpha * _trailStrength, _trailAlphas[i].time);
            _trailGradient.SetKeys(_trailColors, _trailScaled);
            speedTrail.colorGradient = _trailGradient;
            speedTrail.widthMultiplier = _trailWidth * Mathf.Lerp(0.6f, 1f, _trailStrength);
        }

        // ---------- Trạng thái ----------

        Snapshot Capture()
        {
            var snap = new Snapshot
            {
                Position = controller.transform.position,
                Velocity = _rigidbody.linearVelocity,
                Facing = controller.Facing,
            };

            switch (controller.State)
            {
                case OctopusState.Grounded:
                    snap.Mode = _takeoffTimer > 0f ? Mode.Air : Mode.Ground;
                    break;
                case OctopusState.Swinging:
                    snap.Mode = Mode.Swing;
                    snap.Anchor = controller.Swing.AnchorPoint;
                    break;
                case OctopusState.Clinging:
                    snap.Mode = Mode.Cling;
                    snap.ClingPoint = controller.Mover.ClingPoint;
                    snap.ClingNormal = controller.Mover.ClingNormal;
                    snap.ClingSurface = controller.Mover.ClingSurface;
                    snap.Velocity = controller.Mover.ClingVelocity;
                    break;
                default:
                    snap.Mode = Mode.Air;
                    break;
            }

            return snap;
        }

        Snapshot PreviewSnapshot(bool animationWindow)
        {
            var snap = new Snapshot { Position = controller.transform.position, Facing = 1f };
            switch (animationWindow ? Preview.Crawl : preview)
            {
                case Preview.Crawl:
                    snap.Mode = Mode.Ground;
                    snap.Velocity = new Vector2(3f, 0f);
                    break;
                case Preview.Jump:
                    snap.Mode = Mode.Air;
                    snap.Velocity = new Vector2(2f, 11f);
                    break;
                case Preview.Fall:
                    snap.Mode = Mode.Air;
                    snap.Velocity = new Vector2(2f, -14f);
                    break;
                case Preview.Swing:
                    snap.Mode = Mode.Swing;
                    snap.Anchor = snap.Position + new Vector2(1.6f, 3.6f);
                    snap.Velocity = new Vector2(6f, -2f);
                    break;
                case Preview.Cling:
                case Preview.ClingCrawl:
                    // Treo ngược dưới một mặt đá phẳng ngay trên đầu; ClingCrawl = bò sang phải dọc mặt đá
                    snap.Mode = Mode.Cling;
                    snap.ClingNormal = Vector2.down;
                    snap.ClingPoint = snap.Position + Vector2.up * groundOffset;
                    snap.Velocity = preview == Preview.ClingCrawl ? new Vector2(3f, 0f) : Vector2.zero;
                    break;
                default:
                    snap.Mode = Mode.Ground;
                    break;
            }

            return snap;
        }

        void UpdateBlend(Snapshot snap, float dt, bool live)
        {
            float ground = OnSurface(snap.Mode) ? 1f : 0f;
            float cling = snap.Mode == Mode.Cling ? 1f : 0f;
            _groundBlend = live ? Mathf.MoveTowards(_groundBlend, ground, dt * 10f) : ground;
            _clingBlend = live ? Mathf.MoveTowards(_clingBlend, cling, dt * 8f) : cling;
            if (snap.Mode == Mode.Cling)
                _clingOffset = snap.ClingPoint - snap.Position - snap.ClingNormal * (clingReach * size);

            // Hướng đang đi trong tranh: không lật thân thì tranh trùng world (sang trái là -1); lật thân thì luôn "tới trước".
            // Bám đá: trục x của tranh nằm dọc mặt đá; vuốt sang phải là đi theo hướng "sang phải" của mặt đá
            float facing = Mathf.Sign(snap.Facing);
            float artDir = snap.Mode == Mode.Cling
                ? facing * Mathf.Sign(Vector2.Dot(ClingArtAxis(snap.ClingNormal), SurfaceMover.SurfaceTangent(snap.ClingNormal)) + 1e-4f)
                : flipBody ? 1f : facing;
            if (artDir != _artDir)
            {
                foreach (Limb limb in limbs)
                    limb.swapBlend = 0.2f; // đổi vai trò xúc tu trước / sau: chuyển dáng mượt
            }

            _artDir = artDir;

            // Tốc độ bò (unit tranh): dương khi đi theo hướng mặt; nhịp bò theo quãng đường nên chân không trượt
            float artVelocity = snap.Mode switch
            {
                Mode.Ground => flipBody ? snap.Velocity.x * facing : snap.Velocity.x,
                Mode.Cling => Vector2.Dot(snap.Velocity, ClingArtAxis(snap.ClingNormal)),
                _ => 0f,
            };
            float artSpeed = artVelocity * _artDir / size;
            float walk = Mathf.Abs(artVelocity) > crawlThreshold ? 1f : 0f;
            _walkWeight = live ? Mathf.MoveTowards(_walkWeight, walk, dt * 6f) : walk;

            _walkAdvance = live ? artSpeed * dt / cycleDistance : 0f;
            _walkPhase = live ? Mathf.Repeat(_walkPhase + _walkAdvance, 1f) : _editorPhase >= 0f ? _editorPhase : previewTime;
            if (live)
                _travel += artVelocity / size * dt;
        }

        ArmGait Gait(int index) => gait != null && gait.Length > 0 ? gait[Mathf.Min(index, gait.Length - 1)] : DefaultGait()[index];

        // ---------- Thân: vị trí, xoay, co giãn ----------

        Vector2 TorsoOffset(Snapshot snap)
        {
            // Ra đòn: thân lùi lấy đà, chồm tới, nhổm lên / hạ xuống (xúc tu đang bám đất tự co duỗi theo); vận sức laze: nhổm, lùi, run
            Vector2 offset = _actShift + _laserShift;
            if (!OnSurface(snap.Mode))
                return offset;

            if (UsesCrawlClip)
            {
                // Tay cầm thân trong clip (làm cho hướng sang phải): chồm tới / lùi lại, nhấp nhô
                Vector2 body = (Vector2)crawlBody.transform.localPosition * _walkWeight;
                offset += new Vector2(body.x * _artDir, body.y);
            }
            else
            {
                BodyCrawl(_walkPhase, out Vector2 body, out _);
                offset += new Vector2(body.x * _artDir, body.y) * _walkWeight;
            }

            // Đầu hạ thấp lúc đứng, thấp thêm lúc bò; treo ngược thì thân kéo sát lên đá theo Cling Crouch
            offset.y -= snap.Mode == Mode.Cling ? clingCrouch : idleCrouch + crawlCrouch * _walkWeight;
            if (_landTimer > 0f)
                offset.y -= landDip * _landImpact * Mathf.Sin(Mathf.PI * (1f - _landTimer / landTime));
            return offset;
        }

        void PlacePose(Snapshot snap, float dt, bool live)
        {
            float target;
            switch (snap.Mode)
            {
                case Mode.Swing:
                {
                    Vector2 toAnchor = snap.Anchor - snap.Position;
                    target = Mathf.Atan2(toAnchor.y, toAnchor.x) * Mathf.Rad2Deg - 90f;
                    break;
                }
                case Mode.Cling:
                    target = Mathf.Atan2(-snap.ClingNormal.y, -snap.ClingNormal.x) * Mathf.Rad2Deg - 90f;
                    break;
                case Mode.Ground:
                    target = 0f;
                    break;
                default:
                    target = -Mathf.Clamp(snap.Velocity.x / 20f, -1f, 1f) * airLean;
                    break;
            }

            _angle = live ? Mathf.LerpAngle(_angle, target, 1f - Mathf.Exp(-rotationSmoothing * dt)) : target;

            // Không lật thân (mặc định): đầu, mắt giữ nguyên, chỉ xúc tu đổi chiều. Lật thân: chỉ quay khi đứng / bám
            if (!flipBody)
                _flip = 1f;
            else if (!live)
                _flip = snap.Facing;
            else if (snap.Mode is Mode.Ground or Mode.Cling)
                _flip = Mathf.MoveTowards(_flip, snap.Facing, dt * 2f / Mathf.Max(turnTime, 0.01f));

            // Bám đá: lật ngược người (tranh lật dọc) để xúc tu bám lên đá, đầu chúc ra xa đá. Không bẹp dí rồi lật (trông như
            // một lát mỏng) mà lộn nửa vòng tới trước: nửa đầu xoay tới 90° (thân nằm ngang), giữa chừng đổi sang dáng lật dọc
            // — lúc thân nằm ngang thì đổi này chỉ là đổi trái / phải của tranh, thân hẹp lại ~0.7 nên không thấy giật — rồi
            // xoay nốt về treo ngược. Rời đá thì lộn ngược lại
            float hangTarget = snap.Mode == Mode.Cling ? -1f : 1f;
            if (Mathf.Abs(_hang) >= 1f)
                _hangDir = snap.Facing < 0f ? 1f : -1f; // quay phải thì lộn theo chiều kim đồng hồ (đầu đi tới trước)
            _hang = live ? Mathf.MoveTowards(_hang, hangTarget, dt * 2f / Mathf.Max(hangFlipTime, 0.01f)) : hangTarget;
            float roll = 90f * (1f - _hang); // 0 → 180 khi lật từ đứng thẳng sang treo ngược
            _hangRoll = _hangDir * (_hang >= 0f ? roll : roll - 180f);
            float turn = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(_hang)); // 1 = giữa chừng (thân nằm ngang)

            // Co giãn giữ thể tích: dài ra thì hẹp lại; lật trái / phải, lật ngược bằng scale âm. Ra đòn cộng thêm co giãn của đòn
            float stretch = 1f + Mathf.Max(-0.5f, _squash + _actStretch + _laserStretch);
            float flip = Mathf.Sign(_flip) * Mathf.Max(Mathf.Abs(_flip), 0.35f);
            float hang = Mathf.Sign(_hang) * (1f + 0.08f * turn);
            var scale = new Vector2(flip * (1f - 0.3f * turn) / Mathf.Sqrt(stretch), stretch * hang) * size;

            // Điểm neo q (unit tranh) đặt vào vị trí at (unit world, tính từ tâm vật lý):
            // chân khi đứng (bẹp xuống mặt đất) hoặc khi treo ngược (chân = chỗ bám trên đá), tâm thân khi bay / đu.
            // Xoay tròn ra đòn: điểm neo chuyển dần lên tâm thân để xoay quanh tâm
            var feet = new Vector2(0f, -groundOffset);
            Vector2 q = Vector2.Lerp(Vector2.Lerp(new Vector2(0f, bodyCenter), Vector2.zero, _groundBlend), new Vector2(0f, bodyCenter), _actPivot);
            Vector2 at = feet + q * size;
            at = Vector2.Lerp(at, _clingOffset, _clingBlend);

            // Thân ngả khi ra đòn tính trong tranh; tranh lật (scale âm) thì chiều xoay world đổi dấu
            float lean = (_actLean + _laserLean) * Mathf.Sign(_flip) * Mathf.Sign(_hang);
            Quaternion rotation = Quaternion.Euler(0f, 0f, _angle + _hangRoll + _actSpin + lean);
            pose.localPosition = at - (Vector2)(rotation * Vector2.Scale(scale, q));
            pose.localRotation = rotation;
            pose.localScale = new Vector3(scale.x, scale.y, 1f);
        }

        static bool OnSurface(Mode mode) => mode is Mode.Ground or Mode.Cling;

        // ---------- Ra đòn (bộ chiêu) ----------
        //
        // Mỗi đòn là một clip dựng bằng mốc khoá như hoạt hoạ 2D: lấy đà (xúc tu cuộn ra sau, thân ngả ngược) → ra đòn nhanh →
        // vượt quá đích → vung theo quán tính → về dáng thường. Xúc tu tả bằng góc gốc, độ cuộn, độ quặp chóp, độ dài, sóng gợn;
        // mỗi xương lấy dáng của thời điểm trễ hơn gốc một chút (Lag) nên khi gốc vụt đi thì chóp còn ở lại rồi mới quất theo —
        // sóng chạy từ gốc ra chóp như roi, xúc tu cong hình chữ S. Thân: ngả, chồm, nhổm, co giãn; đầu ngả theo qua lò xo
        // nên trễ và nảy sau thân. Mọi lần đổi dáng (vào đòn, nối đòn khi đang đánh, hết đòn) đều mượt: lấy độ lệch giữa dáng
        // đang hiện và dáng mới rồi giảm dần về 0, dáng mới vẫn chạy đúng nhịp mà không giật. Clip làm cho hướng sang phải;
        // quay trái thì lật gương phần xúc tu (đầu, mắt không lật — xúc tu đổi vai trước / sau).

        /// <summary>Kiểu nối từ mốc khoá trước tới mốc này.</summary>
        static class E
        {
            public const float S = 0f; // mượt hai đầu
            public const float O = 1f; // bật nhanh rồi chậm dần (ra đòn)
            public const float I = 2f; // chậm rồi nhanh dần (dồn lực nện xuống)
            public const float L = 3f; // đều
        }

        /// <summary>
        /// Dáng một xúc tu trong đòn, khung quay sang phải (0° = phía trước, 90° = lên trên). Mỗi đường là các bộ ba
        /// (giây, giá trị, kiểu nối).
        /// </summary>
        sealed class ArmClip
        {
            public float[] angle;     // góc xương gốc (độ)
            public float[] curl;      // cuộn đều mỗi xương (độ; dương = ngược chiều kim đồng hồ)
            public float[] tip;       // quặp thêm ở nửa ngoài tới chóp (độ mỗi xương)
            public float[] scale;     // độ dài (1 = như tranh)
            public float[] wave;      // sóng gợn chạy dọc xúc tu (độ)
            public float[] aim;       // 0..1: xoay cả xúc tu quanh gốc cho chóp trúng điểm nhắm
            public bool aimStretch;   // nhắm thì kéo dài / co lại cho chóp tới đúng điểm (móc kéo)
            public Vector2 aimShift;  // dời điểm nhắm (unit tranh; x = phía trước)
            public float delay;       // ra đòn trễ hơn xúc tu kia bấy nhiêu giây
        }

        /// <summary>Một đòn: các xúc tu theo vai (trước / giữa / sau, null = đứng làm trụ như thường) và dáng thân.</summary>
        sealed class ActClip
        {
            public float length;          // thời lượng chuẩn của các mốc (giây)
            public float head, tail;      // đòn dài hơn chuẩn: đoạn đầu / cuối giữ nguyên nhịp, chỉ đoạn giữa giãn ra (0 = giãn đều)
            public float blendIn = 0.05f; // nối từ dáng đang hiện sang dáng đòn trong bấy nhiêu giây
            public float lag = 0.03f;     // chóp trễ sau gốc bấy nhiêu giây
            public ArmClip front, middle, back;
            public float[] lean;          // thân ngả (độ; dương = ngả ra sau)
            public float[] shiftX, shiftY;// thân dời (unit tranh; x = phía trước)
            public float[] stretch;       // co giãn (dương = dãn cao, âm = bẹp)
            public float[] nod;           // đầu ngả thêm (độ; dương = ra sau), qua lò xo đầu
            public float[] pivot;         // 0..1: điểm xoay cả người chuyển từ chân lên tâm thân
        }

        // Quất (đòn 1): xúc tu trước cuộn vòng ra sau, vươn cao sau đầu → vụt qua đầu quất xuống phía trước, chóp quất theo
        // sau gốc → vượt quá xuống thấp, chóp cong lên → về
        static readonly ActClip WhipClip = new ActClip
        {
            length = 0.34f, blendIn = 0.045f, lag = 0.035f,
            front = new ArmClip
            {
                angle = new[] { 0f, -45f, E.S, 0.045f, 112f, E.S, 0.1f, -30f, E.S, 0.16f, -40f, E.S, 0.25f, -38f, E.S, 0.34f, -45f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.045f, -2f, E.S, 0.085f, 0f, E.S, 0.12f, 2f, E.S, 0.2f, 4f, E.S, 0.34f, 2f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.045f, -20f, E.S, 0.085f, 10f, E.S, 0.12f, 25f, E.S, 0.2f, 30f, E.S, 0.34f, 15f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.045f, 1.15f, E.S, 0.085f, 1.15f, E.O, 0.13f, 1.12f, E.S, 0.22f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.08f, 0f, E.S, 0.12f, 6f, E.S, 0.3f, 2f, E.S, 0.34f, 0f, E.S },
            },
            lean = new[] { 0f, 0f, E.S, 0.045f, 7f, E.S, 0.1f, -9f, E.O, 0.18f, -5f, E.S, 0.34f, 0f, E.S },
            shiftX = new[] { 0f, 0f, E.S, 0.045f, -0.04f, E.S, 0.1f, 0.07f, E.O, 0.2f, 0.04f, E.S, 0.34f, 0f, E.S },
            shiftY = new[] { 0f, 0f, E.S, 0.045f, 0.03f, E.S, 0.1f, -0.03f, E.O, 0.34f, 0f, E.S },
            stretch = new[] { 0f, 0f, E.S, 0.045f, 0.06f, E.S, 0.09f, -0.08f, E.O, 0.15f, 0.03f, E.S, 0.25f, -0.01f, E.S, 0.34f, 0f, E.S },
            nod = new[] { 0f, 0f, E.S, 0.045f, 12f, E.S, 0.1f, -14f, E.O, 0.2f, -4f, E.S, 0.34f, 0f, E.S },
        };

        // Quất hất (đòn 2): xúc tu giữa cuộn lại dưới thân, thân thụp xuống → vụt từ dưới lên trước mặt, thân nhổm lên ngả sau
        static readonly ActClip WhipAltClip = new ActClip
        {
            length = 0.34f, blendIn = 0.045f, lag = 0.035f,
            middle = new ArmClip
            {
                angle = new[] { 0f, -94f, E.S, 0.045f, -125f, E.S, 0.105f, 70f, E.S, 0.14f, 78f, E.O, 0.2f, 62f, E.S, 0.34f, -40f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.045f, 14f, E.S, 0.09f, 0f, E.S, 0.13f, -4f, E.S, 0.2f, -6f, E.S, 0.34f, 2f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.045f, 15f, E.S, 0.09f, -5f, E.S, 0.13f, -25f, E.S, 0.22f, -30f, E.S, 0.34f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.045f, 0.78f, E.S, 0.095f, 1.15f, E.O, 0.14f, 1.1f, E.S, 0.24f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.09f, 0f, E.S, 0.14f, 6f, E.S, 0.34f, 0f, E.S },
            },
            lean = new[] { 0f, 0f, E.S, 0.045f, -5f, E.S, 0.1f, 8f, E.O, 0.2f, 4f, E.S, 0.34f, 0f, E.S },
            shiftX = new[] { 0f, 0f, E.S, 0.045f, -0.02f, E.S, 0.1f, 0.05f, E.O, 0.34f, 0f, E.S },
            shiftY = new[] { 0f, 0f, E.S, 0.045f, -0.06f, E.S, 0.1f, 0.06f, E.O, 0.2f, 0.03f, E.S, 0.34f, 0f, E.S },
            stretch = new[] { 0f, 0f, E.S, 0.045f, -0.1f, E.S, 0.1f, 0.12f, E.O, 0.18f, -0.02f, E.S, 0.34f, 0f, E.S },
            nod = new[] { 0f, 0f, E.S, 0.045f, -8f, E.S, 0.1f, 12f, E.O, 0.22f, 3f, E.S, 0.34f, 0f, E.S },
        };

        // Nện (đòn 3): hai xúc tu (trước, giữa) giơ cao quá đầu, thân nhổm lên ngả sau → dồn lực đập xuống đất phía trước,
        // thân bẹp xuống chúi tới → nảy nhẹ → về
        static readonly ActClip SlamClip = new ActClip
        {
            length = 0.5f, blendIn = 0.14f, lag = 0.03f,
            front = new ArmClip
            {
                angle = new[] { 0f, -45f, E.S, 0.16f, 92f, E.S, 0.2f, 100f, E.S, 0.25f, -28f, E.I, 0.3f, -22f, E.O, 0.36f, -28f, E.S, 0.5f, -45f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.16f, -3f, E.S, 0.2f, -4f, E.S, 0.25f, 2f, E.I, 0.36f, 2f, E.S, 0.5f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.16f, -16f, E.S, 0.2f, -22f, E.S, 0.25f, 8f, E.I, 0.3f, 18f, E.S, 0.5f, 10f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.16f, 1.12f, E.S, 0.2f, 1.08f, E.S, 0.25f, 1.12f, E.I, 0.32f, 1.05f, E.S, 0.5f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.08f, 3f, E.S, 0.2f, 3f, E.S, 0.24f, 0f, E.S, 0.28f, 6f, E.S, 0.45f, 0f, E.S },
            },
            middle = new ArmClip
            {
                delay = 0.02f,
                angle = new[] { 0f, -94f, E.S, 0.16f, 98f, E.S, 0.2f, 106f, E.S, 0.25f, -40f, E.I, 0.3f, -34f, E.O, 0.36f, -40f, E.S, 0.5f, -94f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.16f, -3f, E.S, 0.2f, -4f, E.S, 0.25f, 2f, E.I, 0.36f, 2f, E.S, 0.5f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.16f, -16f, E.S, 0.2f, -22f, E.S, 0.25f, 8f, E.I, 0.3f, 18f, E.S, 0.5f, 10f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.16f, 1.12f, E.S, 0.2f, 1.08f, E.S, 0.25f, 1.1f, E.I, 0.32f, 1.05f, E.S, 0.5f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.08f, 3f, E.S, 0.2f, 3f, E.S, 0.24f, 0f, E.S, 0.28f, 6f, E.S, 0.45f, 0f, E.S },
            },
            lean = new[] { 0f, 0f, E.S, 0.16f, 9f, E.S, 0.2f, 11f, E.S, 0.25f, -10f, E.I, 0.3f, -7f, E.S, 0.4f, -2f, E.S, 0.5f, 0f, E.S },
            shiftX = new[] { 0f, 0f, E.S, 0.16f, -0.05f, E.S, 0.25f, 0.08f, E.I, 0.5f, 0f, E.S },
            shiftY = new[] { 0f, 0f, E.S, 0.16f, 0.1f, E.S, 0.2f, 0.12f, E.S, 0.25f, -0.08f, E.I, 0.32f, -0.04f, E.S, 0.5f, 0f, E.S },
            stretch = new[] { 0f, 0f, E.S, 0.16f, 0.1f, E.S, 0.2f, 0.12f, E.S, 0.25f, -0.22f, E.I, 0.31f, 0.06f, E.O, 0.38f, -0.02f, E.S, 0.5f, 0f, E.S },
            nod = new[] { 0f, 0f, E.S, 0.16f, 12f, E.S, 0.2f, 14f, E.S, 0.25f, -16f, E.I, 0.32f, -6f, E.S, 0.5f, 0f, E.S },
        };

        // Móc kéo: xúc tu trước cuộn gọn dựng lên như rắn hổ mang lấy đà → phóng thẳng như mũi tên tới điểm nhắm (kéo dài ra) → căng rung → giật mạnh về,
        // cuộn lại, thân ngả ra sau như kéo → về
        static readonly ActClip ReachClip = new ActClip
        {
            length = 0.6f, blendIn = 0.07f, lag = 0.02f,
            front = new ArmClip
            {
                aimStretch = true,
                angle = new[] { 0f, -45f, E.S, 0.075f, 95f, E.S, 0.14f, 0f, E.O, 0.26f, 0f, E.S, 0.38f, 75f, E.I, 0.48f, 45f, E.S, 0.6f, -45f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.075f, -14f, E.S, 0.14f, 0f, E.O, 0.26f, 0f, E.S, 0.38f, -10f, E.I, 0.5f, -4f, E.S, 0.6f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.075f, -10f, E.S, 0.14f, 12f, E.O, 0.26f, 12f, E.S, 0.38f, -30f, E.I, 0.6f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.075f, 0.72f, E.S, 0.14f, 1f, E.O, 0.26f, 1f, E.S, 0.38f, 0.8f, E.I, 0.5f, 0.95f, E.S, 0.6f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.14f, 0f, E.S, 0.16f, 5f, E.S, 0.26f, 2f, E.S, 0.32f, 8f, E.S, 0.45f, 3f, E.S, 0.6f, 0f, E.S },
                aim = new[] { 0f, 0f, E.S, 0.08f, 0f, E.S, 0.14f, 1f, E.O, 0.26f, 1f, E.S, 0.36f, 0f, E.I },
            },
            lean = new[] { 0f, 0f, E.S, 0.075f, 8f, E.S, 0.14f, -9f, E.O, 0.26f, -6f, E.S, 0.36f, 10f, E.I, 0.46f, 3f, E.S, 0.6f, 0f, E.S },
            shiftX = new[] { 0f, 0f, E.S, 0.075f, -0.05f, E.S, 0.14f, 0.08f, E.O, 0.26f, 0.06f, E.S, 0.36f, -0.06f, E.I, 0.6f, 0f, E.S },
            shiftY = new[] { 0f, 0f, E.S, 0.075f, 0.02f, E.S, 0.14f, -0.02f, E.O, 0.6f, 0f, E.S },
            stretch = new[] { 0f, 0f, E.S, 0.075f, -0.07f, E.S, 0.14f, 0.08f, E.O, 0.24f, 0.02f, E.S, 0.36f, -0.08f, E.I, 0.45f, 0.04f, E.S, 0.6f, 0f, E.S },
            nod = new[] { 0f, 0f, E.S, 0.075f, 10f, E.S, 0.14f, -12f, E.O, 0.26f, -6f, E.S, 0.36f, 14f, E.I, 0.5f, 2f, E.S, 0.6f, 0f, E.S },
        };

        // Xoay tròn: thụp xuống, thu xúc tu, xoay ngược lấy đà → bật lên xoay tít, 3 xúc tu xoè ra như cánh quạt, chóp cong
        // ngược chiều xoay → giảm tốc, vượt quá rồi về. Góc xoay cả người tính riêng (SpinAngle)
        static readonly ActClip SpinClip = new ActClip
        {
            length = 0.6f, head = 0.16f, tail = 0.14f, blendIn = 0.07f, lag = 0.04f,
            front = new ArmClip
            {
                angle = new[] { 0f, -45f, E.S, 0.08f, -75f, E.S, 0.16f, -12f, E.O, 0.46f, -12f, E.S, 0.6f, -45f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.08f, 10f, E.S, 0.16f, 1f, E.O, 0.46f, 1f, E.S, 0.6f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.08f, 15f, E.S, 0.16f, 10f, E.O, 0.46f, 10f, E.S, 0.6f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.08f, 0.85f, E.S, 0.16f, 1.12f, E.O, 0.46f, 1.12f, E.S, 0.6f, 1f, E.S },
            },
            middle = new ArmClip
            {
                angle = new[] { 0f, -94f, E.S, 0.08f, -100f, E.S, 0.16f, -90f, E.O, 0.46f, -90f, E.S, 0.6f, -94f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.08f, 10f, E.S, 0.16f, 1f, E.O, 0.46f, 1f, E.S, 0.6f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.08f, 15f, E.S, 0.16f, 10f, E.O, 0.46f, 10f, E.S, 0.6f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.08f, 0.85f, E.S, 0.16f, 1.12f, E.O, 0.46f, 1.12f, E.S, 0.6f, 1f, E.S },
            },
            back = new ArmClip
            {
                angle = new[] { 0f, -135f, E.S, 0.08f, -110f, E.S, 0.16f, -168f, E.O, 0.46f, -168f, E.S, 0.6f, -135f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.08f, -10f, E.S, 0.16f, -1f, E.O, 0.46f, -1f, E.S, 0.6f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.08f, -15f, E.S, 0.16f, -10f, E.O, 0.46f, -10f, E.S, 0.6f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.08f, 0.85f, E.S, 0.16f, 1.12f, E.O, 0.46f, 1.12f, E.S, 0.6f, 1f, E.S },
            },
            shiftY = new[] { 0f, 0f, E.S, 0.08f, -0.04f, E.S, 0.16f, 0.14f, E.O, 0.46f, 0.12f, E.S, 0.54f, -0.02f, E.S, 0.6f, 0f, E.S },
            stretch = new[] { 0f, 0f, E.S, 0.08f, -0.12f, E.S, 0.16f, 0.06f, E.O, 0.46f, 0.04f, E.S, 0.53f, -0.06f, E.S, 0.6f, 0f, E.S },
            pivot = new[] { 0f, 0f, E.S, 0.06f, 0f, E.S, 0.16f, 1f, E.S, 0.46f, 1f, E.S, 0.6f, 0f, E.S },
        };

        // Chồm lên (vận sức laze lớn / kraken): thụp xuống thu xúc tu → nhổm cao, dãn người, hai xúc tu bên giơ cao toả rộng
        // quặp chóp ra ngoài, gợn sóng đe doạ → hạ xuống. Xúc tu giữa làm trụ
        static readonly ActClip RearClip = new ActClip
        {
            length = 0.75f, head = 0.3f, tail = 0.2f, blendIn = 0.12f, lag = 0.05f,
            front = new ArmClip
            {
                angle = new[] { 0f, -45f, E.S, 0.12f, -70f, E.S, 0.3f, 72f, E.O, 0.55f, 66f, E.S, 0.75f, -45f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.12f, 8f, E.S, 0.3f, -5f, E.O, 0.55f, -4f, E.S, 0.75f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.12f, 15f, E.S, 0.3f, -28f, E.O, 0.55f, -24f, E.S, 0.75f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.12f, 0.85f, E.S, 0.3f, 1.15f, E.O, 0.55f, 1.1f, E.S, 0.75f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.25f, 0f, E.S, 0.35f, 7f, E.S, 0.55f, 6f, E.S, 0.75f, 0f, E.S },
            },
            back = new ArmClip
            {
                angle = new[] { 0f, -135f, E.S, 0.12f, -112f, E.S, 0.3f, -252f, E.O, 0.55f, -246f, E.S, 0.75f, -135f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.12f, -8f, E.S, 0.3f, 5f, E.O, 0.55f, 4f, E.S, 0.75f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.12f, -15f, E.S, 0.3f, 28f, E.O, 0.55f, 24f, E.S, 0.75f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.12f, 0.85f, E.S, 0.3f, 1.15f, E.O, 0.55f, 1.1f, E.S, 0.75f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.25f, 0f, E.S, 0.35f, 7f, E.S, 0.55f, 6f, E.S, 0.75f, 0f, E.S },
            },
            lean = new[] { 0f, 0f, E.S, 0.12f, -5f, E.S, 0.3f, 6f, E.O, 0.55f, 4f, E.S, 0.75f, 0f, E.S },
            shiftX = new[] { 0f, 0f, E.S, 0.12f, 0.03f, E.S, 0.3f, -0.03f, E.O, 0.75f, 0f, E.S },
            shiftY = new[] { 0f, 0f, E.S, 0.12f, -0.08f, E.S, 0.3f, 0.2f, E.O, 0.55f, 0.17f, E.S, 0.75f, 0f, E.S },
            stretch = new[] { 0f, 0f, E.S, 0.12f, -0.12f, E.S, 0.3f, 0.16f, E.O, 0.4f, 0.08f, E.S, 0.55f, 0.1f, E.S, 0.75f, 0f, E.S },
            nod = new[] { 0f, 0f, E.S, 0.12f, -6f, E.S, 0.3f, 12f, E.O, 0.4f, 6f, E.S, 0.55f, 8f, E.S, 0.75f, 0f, E.S },
        };

        // Co rúm phun mực: bẹp hẳn xuống, thu xúc tu dưới thân → bung dài người phóng lùi, cả 3 xúc tu duỗi về phía trước
        // (kéo theo sau khi lùi), gợn sóng → xoè ra rồi về dáng bay
        static readonly ActClip SquashClip = new ActClip
        {
            length = 0.45f, blendIn = 0.04f, lag = 0.05f,
            front = new ArmClip
            {
                angle = new[] { 0f, -45f, E.S, 0.05f, -75f, E.S, 0.16f, -12f, E.O, 0.3f, -22f, E.S, 0.45f, -45f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.05f, 12f, E.S, 0.16f, 2f, E.O, 0.3f, 4f, E.S, 0.45f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.05f, 20f, E.S, 0.16f, 15f, E.S, 0.3f, 25f, E.S, 0.45f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.05f, 0.8f, E.S, 0.16f, 1.12f, E.O, 0.3f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.1f, 0f, E.S, 0.18f, 8f, E.S, 0.45f, 0f, E.S },
            },
            middle = new ArmClip
            {
                angle = new[] { 0f, -94f, E.S, 0.05f, -100f, E.S, 0.16f, -35f, E.O, 0.3f, -45f, E.S, 0.45f, -94f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.05f, 12f, E.S, 0.16f, 2f, E.O, 0.3f, 4f, E.S, 0.45f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.05f, 20f, E.S, 0.16f, 15f, E.S, 0.3f, 25f, E.S, 0.45f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.05f, 0.8f, E.S, 0.16f, 1.12f, E.O, 0.3f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.1f, 0f, E.S, 0.18f, 8f, E.S, 0.45f, 0f, E.S },
            },
            back = new ArmClip
            {
                angle = new[] { 0f, -135f, E.S, 0.05f, -115f, E.S, 0.16f, -58f, E.O, 0.3f, -68f, E.S, 0.45f, -135f, E.S },
                curl = new[] { 0f, 0f, E.S, 0.05f, 12f, E.S, 0.16f, 2f, E.O, 0.3f, 4f, E.S, 0.45f, 0f, E.S },
                tip = new[] { 0f, 0f, E.S, 0.05f, 20f, E.S, 0.16f, 15f, E.S, 0.3f, 25f, E.S, 0.45f, 0f, E.S },
                scale = new[] { 0f, 1f, E.S, 0.05f, 0.8f, E.S, 0.16f, 1.12f, E.O, 0.3f, 1f, E.S },
                wave = new[] { 0f, 0f, E.S, 0.1f, 0f, E.S, 0.18f, 8f, E.S, 0.45f, 0f, E.S },
            },
            lean = new[] { 0f, 0f, E.S, 0.05f, -6f, E.S, 0.16f, 14f, E.O, 0.3f, 8f, E.S, 0.45f, 0f, E.S },
            shiftY = new[] { 0f, 0f, E.S, 0.05f, -0.06f, E.S, 0.16f, 0f, E.S },
            stretch = new[] { 0f, 0f, E.S, 0.05f, -0.3f, E.O, 0.16f, 0.22f, E.O, 0.28f, -0.04f, E.S, 0.45f, 0f, E.S },
            nod = new[] { 0f, 0f, E.S, 0.05f, -6f, E.S, 0.16f, 16f, E.O, 0.3f, 6f, E.S, 0.45f, 0f, E.S },
        };

        // Lao tới (nhện cắn; bạch tuộc không dùng): lùi lấy đà → chúi người lao tới → về
        static readonly ActClip LungeClip = new ActClip
        {
            length = 0.5f, blendIn = 0.06f,
            lean = new[] { 0f, 0f, E.S, 0.1f, 5f, E.S, 0.25f, -14f, E.O, 0.5f, 0f, E.S },
            shiftX = new[] { 0f, 0f, E.S, 0.1f, -0.05f, E.S, 0.25f, 0.25f, E.O, 0.5f, 0f, E.S },
            stretch = new[] { 0f, 0f, E.S, 0.1f, -0.08f, E.S, 0.25f, 0.1f, E.O, 0.5f, 0f, E.S },
        };

        static readonly float[] SpinWind = { 0f, 0f, E.S, 0.08f, 1f, E.S, 0.18f, 0f, E.S };
        static readonly float[] SpinOver = { 0f, 0f, E.S, 0.08f, 1f, E.O, 0.16f, 0f, E.S };

        // Hết đòn: xúc tu, thân chuyển về dáng thường trong bấy nhiêu giây
        const float ActBlendOut = 0.12f;
        // Chuyển dáng: chóp xong muộn hơn gốc bấy nhiêu giây (sóng chạy ra chóp)
        const float BlendLag = 0.035f;

        static ActClip ClipOf(CombatPose pose) => pose switch
        {
            CombatPose.Whip => WhipClip,
            CombatPose.WhipAlt => WhipAltClip,
            CombatPose.Slam => SlamClip,
            CombatPose.Spin => SpinClip,
            CombatPose.Reach => ReachClip,
            CombatPose.Rear => RearClip,
            CombatPose.Lunge => LungeClip,
            CombatPose.Squash => SquashClip,
            _ => null,
        };

        /// <summary>Diễn dáng ra đòn trong duration giây; point = điểm nhắm (world) cho quất / nện / móc.</summary>
        public void Act(CombatPose pose, float duration, Vector2 point)
        {
            ActClip clip = ClipOf(pose);
            if (clip == null)
            {
                if (_act != CombatPose.None)
                    EndAct();
                return;
            }

            // Đang ra đòn khác thì nối từ dáng đang hiện (thân và xúc tu đều chuyển mượt, không giật)
            CaptureActFrom(clip.blendIn + 0.03f);
            _act = pose;
            _clip = clip;
            _actTime = 0f;
            _actDuration = Mathf.Max(0.05f, duration);
            _actPoint = point;
            _actDir = _artDir;
            _actFacing = controller != null && controller.Facing < 0f ? -1f : 1f;
            _actSerial++;
        }

        void EndAct()
        {
            CaptureActFrom(ActBlendOut);
            _act = CombatPose.None;
            _clip = null;
        }

        // Giữ dáng thân hiện tại làm độ lệch, giảm dần về 0 trong blend giây
        void CaptureActFrom(float blend)
        {
            _fromShift = _actShift;
            _fromLean = _actLean;
            _fromStretch = _actStretch;
            _fromNod = _actNod;
            _fromSpin = Mathf.DeltaAngle(0f, _actSpin);
            _fromPivot = _actPivot;
            _actBlendTime = 0f;
            _actBlendLength = Mathf.Max(blend, 0.01f);
        }

        // Đồng hồ đòn theo thời lượng chuẩn của clip: đòn dài / ngắn hơn chuẩn thì đoạn giữa giãn / co, lấy đà và kết giữ nhịp
        float ActClock()
        {
            float t = _actTime, d = _actDuration, length = _clip.length;
            if (Mathf.Abs(d - length) < 1e-4f)
                return t;
            float head = _clip.head, tail = _clip.tail;
            if (head + tail <= 0f || d <= head + tail + 0.02f || length <= head + tail + 0.02f)
                return t * length / d;
            if (t <= head)
                return t;
            if (t >= d - tail)
                return length - (d - t);
            return head + (t - head) * (length - head - tail) / (d - head - tail);
        }

        // Thân trong đòn: dời, ngả, co giãn, đầu ngả, xoay tròn — theo clip, cộng độ lệch lúc đổi đòn đang giảm dần
        void UpdateAct(float dt, bool live)
        {
            if (!live || !Application.isPlaying)
                return;
            if (_act != CombatPose.None)
            {
                _actTime += dt;
                if (_actTime >= _actDuration)
                    EndAct();
            }

            _actEngage = Mathf.MoveTowards(_actEngage, _act != CombatPose.None ? 1f : 0f, dt * 6f);
            Vector2 shift = Vector2.zero;
            float lean = 0f, stretch = 0f, nod = 0f, spin = 0f, pivot = 0f, bend = 0f;
            if (_clip != null)
            {
                float t = ActClock();
                shift = new Vector2(Key(_clip.shiftX, t) * _actDir, Key(_clip.shiftY, t));
                lean = Key(_clip.lean, t) * _actDir;
                stretch = Key(_clip.stretch, t);
                nod = Key(_clip.nod, t) * _actDir;
                pivot = Key(_clip.pivot, t);
                if (_act == CombatPose.Spin)
                {
                    spin = SpinAngle(_actTime);
                    float speed = Mathf.Abs(spin - SpinAngle(_actTime - 0.02f)) / 0.02f; // độ / giây
                    bend = Mathf.Min(speed * 0.0045f, 8f);              // xúc tu cong ngược chiều xoay như cánh chong chóng
                    nod += _actDir * Mathf.Min(speed * 0.008f, 12f);    // đầu trễ theo
                }
            }

            _actBlendTime += dt;
            float from = 1f - Mathf.SmoothStep(0f, 1f, _actBlendTime / _actBlendLength);
            _actShift = shift + _fromShift * from;
            _actLean = lean + _fromLean * from;
            _actStretch = stretch + _fromStretch * from;
            _actNod = nod + _fromNod * from;
            _actSpin = spin + _fromSpin * from;
            _actPivot = Mathf.Clamp01(pivot + _fromPivot * from);
            _actBend = bend;
        }

        // Xoay tròn (độ, world) tại giây t của đòn: xoay ngược lấy đà, tăng tốc, quay đều, giảm tốc vượt quá một chút rồi về
        float SpinAngle(float t)
        {
            float d = _actDuration;
            int turns = Mathf.Max(1, Mathf.RoundToInt(d / 0.5f));
            float start = 0.08f, end = Mathf.Max(start + 0.05f, d - 0.12f);
            float progress = Ramp(t, start, end, Mathf.Clamp(0.4f * (end - start), 0.01f, 0.16f));
            float wind = Key(SpinWind, t);
            float over = Key(SpinOver, t - (d - 0.16f));
            return -_actFacing * (360f * turns * progress + 18f * over - 25f * wind);
        }

        // 0 → 1 từ giây a tới b: tăng tốc đều trong ramp giây, chạy đều, giảm tốc đều trong ramp giây cuối
        static float Ramp(float t, float a, float b, float ramp)
        {
            float span = b - a, x = t - a;
            if (x <= 0f)
                return 0f;
            if (x >= span)
                return 1f;
            ramp = Mathf.Min(ramp, span * 0.5f);
            float v = 1f / (span - ramp);
            if (x < ramp)
                return v * x * x / (2f * ramp);
            if (x > span - ramp)
                return 1f - v * (span - x) * (span - x) / (2f * ramp);
            return v * (x - ramp * 0.5f);
        }

        // Clip của xúc tu index trong đòn đang diễn (null = đứng làm trụ như thường)
        ArmClip ArmFor(int index, Mode mode)
        {
            if (_clip == null)
                return null;
            int front = _actDir > 0f ? 2 : 0;
            if (mode == Mode.Swing)
            {
                if (index == GrabLimb)
                    return null; // xúc tu giữa đang nắm đá
                if (index == 2 - front && _clip.front == null && _clip.back == null)
                    return _clip.middle; // đòn của xúc tu giữa: xúc tu sau đánh thay
            }

            return index == front ? _clip.front : index == 1 ? _clip.middle : _clip.back;
        }

        // Xúc tu có vai trong đòn thì theo dáng đòn; đổi cách điều khiển (vào đòn, nối đòn, hết đòn) thì chuyển dáng mượt
        void ActLimb(Limb limb, int index, Mode mode, float dt, bool live)
        {
            if (!live || !Application.isPlaying)
                return;

            ArmClip arm = _act != CombatPose.None ? ArmFor(index, mode) : null;
            bool acting = arm != null;
            if (acting)
            {
                ArmPose(limb, arm);
                limb.footGripping = false;
            }

            if (acting != limb.acting || (acting && limb.actSerial != _actSerial))
            {
                limb.blendPending = true;
                limb.blendLength = acting ? _clip.blendIn : ActBlendOut;
            }

            limb.acting = acting;
            limb.actSerial = _actSerial;
            BlendLimb(limb, dt);
        }

        // Dáng đòn của một xúc tu → đích từng xương. Tính trong khung quay phải (góc tuyệt đối trong Torso), mỗi xương lấy
        // dáng ở thời điểm trễ hơn gốc lag·i/(n-1) giây; xong nhắm điểm, tránh xuyên mặt đất, rồi lật gương nếu quay trái
        void ArmPose(Limb limb, ArmClip arm)
        {
            int n = limb.Count;
            float t = ActClock();
            float[] abs = limb.actAngles;
            float tipSum = 0f;
            for (int i = 0; i < n; i++)
            {
                float s = n > 1 ? (float)i / (n - 1) : 0f;
                float tau = t - arm.delay - _clip.lag * s;
                if (i > 0)
                    tipSum += Mathf.SmoothStep(0f, 1f, (s - 0.35f) / 0.65f);
                float wave = Key(arm.wave, tau) * Mathf.Lerp(0.3f, 1f, s) * Mathf.Sin(_time * 14f - i * 0.75f);
                abs[i] = Key(arm.angle, tau) + (Key(arm.curl, tau) + _actBend) * i + Key(arm.tip, tau) * tipSum + wave;
                limb.actSpacing[i] = limb.bindLengths[i] * Key(arm.scale, tau, 1f);
            }

            var root = new Vector2(limb.root.x * _actDir, limb.root.y);
            float aim = Key(arm.aim, t - arm.delay);
            if (aim > 0f)
                AimArm(limb, arm, root, aim);
            if (_actGround)
                ClampToGround(limb, root);

            for (int i = 0; i < n; i++)
            {
                float local = i == 0 ? abs[0] : abs[i] - abs[i - 1];
                if (_actDir < 0f)
                    local = i == 0 ? 180f - local : -local;
                limb.targets[i] = Mathf.DeltaAngle(0f, local);
                limb.spacingTargets[i] = limb.actSpacing[i];
            }
        }

        // Xoay cả xúc tu quanh gốc (và kéo dài / co lại nếu aimStretch) cho chóp trúng điểm nhắm; weight 0..1
        void AimArm(Limb limb, ArmClip arm, Vector2 root, float weight)
        {
            int n = limb.Count;
            Vector2 point = torso.InverseTransformPoint(_actPoint);
            Vector2 want = new Vector2(point.x * _actDir, point.y) + arm.aimShift - root;
            Vector2 end = root;
            for (int i = 0; i < n; i++)
                end += Dir(limb.actAngles[i]) * limb.actSpacing[i];
            Vector2 have = end - root;
            if (have.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f)
                return;

            float turn = Mathf.DeltaAngle(Mathf.Atan2(have.y, have.x) * Mathf.Rad2Deg, Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg) * weight;
            float grow = arm.aimStretch ? Mathf.Lerp(1f, Mathf.Clamp(want.magnitude / have.magnitude, 0.6f, 5f), weight) : 1f;
            for (int i = 0; i < n; i++)
            {
                limb.actAngles[i] += turn;
                limb.actSpacing[i] *= grow;
            }
        }

        // Mặt đất / mặt đá dưới thân trong khung đòn (Torso, lật theo hướng đòn), để xúc tu ra đòn không xuyên qua
        void PrepareActGround(Snapshot snap)
        {
            _actGround = _act != CombatPose.None && Application.isPlaying && OnSurface(snap.Mode);
            if (!_actGround)
                return;

            bool cling = snap.Mode == Mode.Cling;
            Vector2 surface = cling ? snap.ClingPoint : snap.Position + Vector2.down * groundOffset;
            Vector2 up = cling ? snap.ClingNormal : Vector2.up;
            Vector2 p = torso.InverseTransformPoint(surface);
            Vector2 along = torso.InverseTransformVector(new Vector2(up.y, -up.x));
            Vector2 normal = new Vector2(-along.y, along.x).normalized;
            Vector2 above = (Vector2)torso.InverseTransformPoint(surface + up) - p;
            if (Vector2.Dot(above, normal) < 0f)
                normal = -normal;
            _actGroundPoint = new Vector2(p.x * _actDir, p.y);
            _actGroundNormal = new Vector2(normal.x * _actDir, normal.y);
        }

        // Xương nào chúi xuống dưới mặt thì nằm rạp theo mặt (khớp cuối của nó vừa chạm mặt), các xương sau giữ dáng
        void ClampToGround(Limb limb, Vector2 root)
        {
            int n = limb.Count;
            float lift = snakeThickness * 0.8f;
            float beta = Mathf.Atan2(_actGroundNormal.y, _actGroundNormal.x) * Mathf.Rad2Deg;
            Vector2 p = root;
            float previous = 0f; // hướng xương trước (xương gốc: phía trước)
            for (int i = 0; i < n; i++)
            {
                float length = limb.actSpacing[i];
                float a = limb.actAngles[i];
                float h = Vector2.Dot(p - _actGroundPoint, _actGroundNormal);
                if (length > 1e-5f && h + length * Vector2.Dot(Dir(a), _actGroundNormal) < lift)
                {
                    float spread = Mathf.Acos(Mathf.Clamp((lift - h) / length, -1f, 1f)) * Mathf.Rad2Deg;
                    float c1 = beta + spread, c2 = beta - spread;
                    float s1 = Mathf.Abs(Mathf.DeltaAngle(c1, a)) + 0.3f * Mathf.Abs(Mathf.DeltaAngle(c1, previous));
                    float s2 = Mathf.Abs(Mathf.DeltaAngle(c2, a)) + 0.3f * Mathf.Abs(Mathf.DeltaAngle(c2, previous));
                    a += Mathf.DeltaAngle(a, s1 <= s2 ? c1 : c2);
                    limb.actAngles[i] = a;
                }

                p += Dir(a) * length;
                previous = a;
            }
        }

        // Chuyển dáng mượt: lần đầu sau khi đổi cách điều khiển, lấy độ lệch giữa dáng đang hiện và đích mới; sau đó cộng
        // độ lệch đó vào đích, giảm dần về 0 (gốc xong trước, chóp xong sau)
        static void BlendLimb(Limb limb, float dt)
        {
            int n = limb.Count;
            if (limb.blendPending)
            {
                limb.blendPending = false;
                limb.blendTime = 0f;
                for (int i = 0; i < n; i++)
                {
                    limb.blendAngles[i] = Mathf.DeltaAngle(limb.targets[i], limb.angles[i]);
                    limb.blendSpacing[i] = limb.spacing[i] - limb.spacingTargets[i];
                }
            }

            if (limb.blendTime >= limb.blendLength + BlendLag)
                return;
            float length = Mathf.Max(limb.blendLength, 1e-3f);
            for (int i = 0; i < n; i++)
            {
                float s = n > 1 ? (float)i / (n - 1) : 0f;
                float w = 1f - Mathf.SmoothStep(0f, 1f, (limb.blendTime - BlendLag * s) / length);
                limb.targets[i] += limb.blendAngles[i] * w;
                limb.spacingTargets[i] += limb.blendSpacing[i] * w;
            }

            limb.blendTime += dt;
        }

        // Giá trị đường mốc khoá (bộ ba: giây, giá trị, kiểu nối từ mốc trước) tại t; trước mốc đầu / sau mốc cuối giữ nguyên
        static float Key(float[] keys, float t, float fallback = 0f)
        {
            if (keys == null || keys.Length < 3)
                return fallback;
            if (t <= keys[0])
                return keys[1];
            for (int i = 3; i < keys.Length; i += 3)
            {
                if (t > keys[i])
                    continue;
                float u = (t - keys[i - 3]) / Mathf.Max(keys[i] - keys[i - 3], 1e-5f);
                return Mathf.LerpUnclamped(keys[i - 2], keys[i + 1], Ease(u, keys[i + 2]));
            }

            return keys[keys.Length - 2];
        }

        static float Ease(float u, float kind)
        {
            switch ((int)kind)
            {
                case 1:
                {
                    float v = 1f - u;
                    return 1f - v * v * v;
                }
                case 2:
                    return u * u;
                case 3:
                    return u;
                default:
                    return u * u * (3f - 2f * u);
            }
        }

        // Trục x của tranh (world) khi treo ngược dưới mặt đá có pháp tuyến normal (đúng góc xoay Pose lúc bám)
        static Vector2 ClingArtAxis(Vector2 normal)
        {
            float angle = Mathf.Atan2(-normal.y, -normal.x) * Mathf.Rad2Deg - 90f;
            return Quaternion.Euler(0f, 0f, angle) * Vector2.right;
        }

        // Vector world → hướng trong tranh (bỏ xoay và lật của Pose), giữ độ lớn
        Vector2 ToArt(Vector2 world)
        {
            Vector2 art = Quaternion.Euler(0f, 0f, -(_angle + _hangRoll)) * world;
            art.x *= Mathf.Sign(_flip);
            art.y *= Mathf.Sign(_hang);
            return art;
        }

        // ---------- Xúc tu: tính góc đích ----------

        // Trên đất: chóp mỗi xúc tu theo tay cầm của clip bò (hoặc thông số Bò bằng code); giải IK cho điểm chạm đất.
        // Kiểu trườn: cả 3 xúc tu nằm trên đất uốn lượn hình rắn. Kiểu sâu đo: hai xúc tu bên đi theo dáng vòm.
        // Hai kiểu này trộn với dáng nghỉ theo mức đang bò
        void GroundTargets(Limb limb, int index, Vector2 torsoOffset, float dt, bool live)
        {
            bool snake = crawlStyle == CrawlStyle.Slither;
            bool inchSide = crawlStyle == CrawlStyle.Inchworm && limb.side != 0f;
            RestTargets(limb, index, torsoOffset, dt, live, inchSide || snake);
            limb.exactUpTo = limb.footBone;
            limb.exactPath = false;
            limb.contactJoint = -1;
            if (!(snake || inchSide) || _walkWeight <= 0f)
                return;

            Array.Copy(limb.targets, limb.restTargets, limb.Count);
            Array.Copy(limb.spacingTargets, limb.restSpacing, limb.Count);
            if (snake)
                SnakeTargets(limb, index, torsoOffset, dt, live);
            else
                InchTargets(limb, index, torsoOffset, dt, live);
            for (int i = 0; i < limb.Count; i++)
            {
                limb.targets[i] = Mathf.LerpAngle(limb.restTargets[i], limb.targets[i], _walkWeight);
                limb.spacingTargets[i] = Mathf.Lerp(limb.restSpacing[i], limb.spacingTargets[i], _walkWeight);
            }

            limb.exactPath = true;
            limb.exactUpTo = limb.Count - 1;
            limb.contactJoint = snake ? SnakeBase(limb) : InchBase(limb);
        }

        // Dáng nghỉ / bước: IK cho điểm chạm đất (restOnly: chỉ dáng nghỉ, để trộn với dáng rắn / sâu đo)
        void RestTargets(Limb limb, int index, Vector2 torsoOffset, float dt, bool live, bool restOnly)
        {
            float idle = 1f - _walkWeight;
            float restCurl = 1f + idleCurl * Mathf.Sin(_time * 0.8f + index * 2.1f);
            Vector2 foot;
            float curl;
            bool gripping;
            if (restOnly)
            {
                foot = RestFoot(limb);
                curl = restCurl;
                gripping = false;
            }
            else if (UsesCrawlClip)
            {
                // Clip làm cho hướng sang phải; đi sang trái thì xúc tu đổi vai: đọc tay cầm bên kia, lật gương
                int source = _artDir > 0f ? index : 2 - index;
                OctopusCrawlFoot handle = crawlFeet[source];
                Vector2 offset = (Vector2)handle.transform.localPosition - RestFoot(limbs[source]);
                offset.x *= _artDir;
                foot = RestFoot(limb) + offset * _walkWeight;
                curl = Mathf.Lerp(restCurl, handle.curl, _walkWeight);
                gripping = handle.contact >= 0.5f;
                foot.x = FootLock(limb, foot.x, footLock && live && gripping && _walkWeight > 0.5f, dt);
            }
            else
            {
                CrawlStep(index, _walkPhase, _artDir, out Vector2 step, out curl, out gripping);
                foot = RestFoot(limb) + step * _walkWeight;
                curl = Mathf.Lerp(restCurl, curl, _walkWeight);
            }

            // Xúc tu là cơ mềm: chỗ bám xa hơn lúc nghỉ thì duỗi dài ra, gần hơn thì co ngắn lại
            Vector2 target = foot - torsoOffset;
            float rest = Mathf.Max((limb.home - limb.root).magnitude, 0.01f);
            float stretch = Mathf.Clamp((target - limb.root).magnitude / rest, armStretch.x, armStretch.y);
            limb.stretch = stretch;
            limb.footTarget = target;
            limb.footGripping = gripping && _walkWeight > 0.5f;
            limb.ik = SolveFoot(limb, target, limb.ik, stretch);

            for (int i = 0; i < limb.Count; i++)
            {
                float angle = limb.bindAngles[i];
                if (i == 0)
                    angle += limb.ik.x;
                else if (i <= limb.footBone)
                    angle += limb.ik.y;
                else
                    angle = angle * curl + tipWiggle * idle * Mathf.Sin(_time * 1.7f + i * 0.6f + index * 2.3f);
                limb.targets[i] = angle + limb.wave[i]; // đoạn tới chân: IK đã tính cả sóng nên chân vẫn đứng yên
                limb.spacingTargets[i] = limb.bindLengths[i] * (i < limb.footBone ? stretch : 1f);
            }
        }

        // ---------- Trườn hình rắn ----------

        // Khớp chạm đất: ít nhất Snake Base Bone, thêm xương nếu đoạn gốc không đủ dài để với từ gốc tới mặt đất / mặt đá
        // (treo ngược dưới đá thì thân ở xa đá hơn nên cần nhiều xương hơn). Tính theo dáng đích nên không nhảy khi đang bò
        int SnakeBase(Limb limb)
        {
            float crouch = _mode == Mode.Cling ? clingCrouch : idleCrouch + crawlCrouch;
            float rise = Mathf.Max(limb.root.y - crouch - snakeThickness, 0f);
            float across = snakeBaseOut * (limb.side != 0f ? 1f : 0.5f);
            float needed = Mathf.Sqrt(rise * rise + across * across) * SnakeSlack;
            int last = limb.Count - 3;
            int kb = Mathf.Clamp(snakeBaseBone, 2, last);
            float length = 0f;
            for (int i = 0; i < kb; i++)
                length += limb.bindLengths[i];
            while (length < needed && kb < last)
                length += limb.bindLengths[kb++];
            return kb;
        }

        // Đoạn gốc dài hơn khoảng cách gốc → chỗ chạm đất bấy nhiêu lần để cong mềm chứ không thẳng đơ
        const float SnakeSlack = 1.06f;

        // Hướng đoạn nằm trên đất khi đi theo hướng dir: hai xúc tu bên luôn ra phía ngoài; xúc tu giữa theo hướng đi
        // (hoặc kéo lê phía sau)
        float SnakeOutward(Limb limb, float dir) => limb.side != 0f ? -Mathf.Sign(limb.side) : dir * (snakeMiddleForward ? 1f : -1f);

        // Chỗ xúc tu chạm đất (Pose) khi trườn theo hướng dir, chưa tính Reach / Slide: dưới gốc, lệch ra phía ngoài
        // (xúc tu giữa lệch ít hơn). Tay cầm Foot L / M / R trong clip nằm đúng chỗ này
        Vector2 SnakeBasePoint(Limb limb, float dir) =>
            new Vector2(limb.root.x + SnakeOutward(limb, dir) * snakeBaseOut * (limb.side != 0f ? 1f : 0.5f), snakeThickness);

        static readonly float[] SnakeU = new float[97], SnakeV = new float[97], SnakeS = new float[97];
        static readonly float[] TipCurlShape = { 10f, 20f, 30f };

        // Đoạn gốc hạ xuống chạm đất ở khớp SnakeBase; từ đó ra tận ngọn nằm trên đất theo đường hình sin,
        // sóng chạy dọc xúc tu về phía sau so với hướng đi như rắn trườn
        void SnakeTargets(Limb limb, int index, Vector2 torsoOffset, float dt, bool live)
        {
            int n = limb.Count;
            int kb = SnakeBase(limb);
            float outward = SnakeOutward(limb, _artDir);
            Vector2 step;
            float curl;
            if (UsesCrawlClip)
            {
                // Tay cầm Foot L / M / R = chỗ chạm đất; Curl = chóp cong lên. Clip làm cho hướng sang phải,
                // đi sang trái thì đổi vai và lật gương
                int source = _artDir > 0f ? index : 2 - index;
                OctopusCrawlFoot handle = crawlFeet[source];
                step = (Vector2)handle.transform.localPosition - SnakeBasePoint(limbs[source], 1f);
                step.x *= _artDir;
                curl = handle.curl;
            }
            else
            {
                CrawlStep(index, _walkPhase, _artDir, out step, out curl, out _);
            }

            // Pha sóng theo quãng đường bò (dừng thì sóng dừng); xúc tu phía trước sóng chạy từ ngọn về gốc, phía sau ngược lại
            float flow = 2f * Mathf.PI * snakeFlow * _artDir * outward;
            limb.snakePhase = live ? Mathf.Repeat(limb.snakePhase + flow * _walkAdvance, 2f * Mathf.PI) : flow * _walkPhase;

            // Đoạn gốc: IK đưa khớp kb chạm đất; co / giãn cho dài hơn khoảng cách một chút để cong mềm
            Vector2 baseTorso = SnakeBasePoint(limb, _artDir) + step - torsoOffset;
            float baseLength = 0f;
            for (int i = 0; i < kb; i++)
                baseLength += limb.bindLengths[i];
            float stretch = Mathf.Clamp((baseTorso - limb.root).magnitude * SnakeSlack / Mathf.Max(baseLength, 0.01f), armStretch.x, armStretch.y);
            limb.ik2 = SolveJoint(limb, baseTorso, limb.ik2, stretch, kb);
            float absolute = 0f;
            for (int i = 0; i < kb; i++)
            {
                limb.targets[i] = limb.bindAngles[i] + (i == 0 ? limb.ik2.x : limb.ik2.y) + limb.wave[i];
                limb.spacingTargets[i] = limb.bindLengths[i] * stretch;
                absolute += limb.targets[i];
            }

            // Đoạn trên đất: xếp từng xương dọc theo đường hình sin; vài xương cuối cong lên theo Curl
            float length = 0f;
            for (int j = kb; j < n; j++)
                length += limb.bindLengths[j];
            BuildSnake(length, limb.snakePhase + index * 2.1f);
            float travelled = 0f;
            int curlStart = n - TipCurlShape.Length;
            float curlSum = 0f;
            for (int j = kb; j < n; j++)
            {
                PathPoint(SnakeU, SnakeV, SnakeS, travelled, out Vector2 p0);
                travelled += limb.bindLengths[j];
                PathPoint(SnakeU, SnakeV, SnakeS, travelled, out Vector2 p1);
                float direction = Mathf.Atan2(p1.y - p0.y, outward * (p1.x - p0.x)) * Mathf.Rad2Deg;
                if (j >= curlStart)
                    curlSum += Mathf.Max(curl, 0f) * TipCurlShape[j - curlStart];
                float wanted = direction + outward * curlSum;
                limb.targets[j] = Mathf.DeltaAngle(absolute, wanted);
                absolute += limb.targets[j];
                limb.spacingTargets[j] = limb.bindLengths[j];
            }

            limb.footTarget = baseTorso;
            limb.footGripping = false;
        }

        // Đường trườn (u ra ngoài tính từ chỗ chạm đất, v lên trên): trục xúc tu cách đất nửa bề dày (mỏng dần ra ngọn)
        // cộng các khúc gồ hình sin; khúc đầu lớn dần từ chỗ chạm đất cho khỏi gãy
        void BuildSnake(float length, float phase)
        {
            int count = SnakeU.Length;
            float k = 2f * Mathf.PI / Mathf.Max(snakeLength, 0.05f);
            float ramp = Mathf.Max(snakeLength * 0.5f, 1e-3f);
            for (int i = 0; i < count; i++)
            {
                float u = length * i / (count - 1);
                float hump = 0.5f - 0.5f * Mathf.Cos(k * u + phase);
                SnakeU[i] = u;
                SnakeV[i] = snakeThickness * (Mathf.Lerp(1f, 0.4f, u / length) - 1f) + snakeHeight * Mathf.SmoothStep(0f, 1f, u / ramp) * hump;
                SnakeS[i] = i == 0 ? 0f : SnakeS[i - 1] + Mathf.Sqrt(Mathf.Pow(u - SnakeU[i - 1], 2f) + Mathf.Pow(SnakeV[i] - SnakeV[i - 1], 2f));
            }
        }

        // ---------- Sâu đo ----------

        int InchBase(Limb limb) => Mathf.Clamp(inchBaseBone, 2, limb.Count - 3);

        // Chỗ đoạn gốc chạm đất (trong Pose): dưới gốc, lệch ra phía ngoài
        Vector2 InchBasePoint(Limb limb) => new Vector2(limb.root.x - limb.side * inchBaseOut, inchThickness);

        float InchLength(Limb limb)
        {
            float length = 0f;
            for (int i = InchBase(limb); i < limb.Count; i++)
                length += limb.bindLengths[i];
            return length;
        }

        // Một vòng sâu đo của xúc tu phía trước tại t (0..1), đo theo hướng ra ngoài tính từ chỗ gốc chạm đất:
        //  bám (chóp đứng yên, thân tiến lên nên chóp lùi về gốc: thẳng → gồ → vòm cao, chóp quặp) →
        //  nhấc chóp vươn ra trước (vòm lệch về gốc) → hạ xuống nằm thẳng. Xúc tu phía sau chạy t ngược lại.
        void InchCycle(float t, float length, out float reach, out float height, out float peak, out float hook, out bool gripping)
        {
            float shortest = inchArch * length;
            float grip = Mathf.Clamp((length - shortest) / Mathf.Max(cycleDistance, 0.01f), 0.15f, 0.85f);
            if (t < grip)
            {
                float k = t / grip;
                reach = Mathf.Lerp(length, shortest, k);
                height = 0f;
                peak = 0.5f;
                hook = Mathf.Lerp(0.15f, 1f, k) * inchHook;
                gripping = true;
            }
            else
            {
                float k = (t - grip) / (1f - grip);
                float e = Mathf.SmoothStep(0f, 1f, k);
                float lift = Mathf.Sin(Mathf.PI * k);
                reach = Mathf.Lerp(shortest, length, e);
                height = inchLift * lift;
                peak = Mathf.Lerp(0.5f, inchSkew, lift);
                hook = Mathf.Lerp(1f, 0.15f, e) * inchHook;
                gripping = false;
            }
        }

        // Pha sâu đo của xúc tu bên index khi đang đi theo hướng dir: xúc tu phía trước chạy xuôi, phía sau chạy ngược
        float InchPhase(int index, float w, float dir)
        {
            bool front = limbs[index].side == -dir;
            return front ? Mathf.Repeat(w, 1f) : 1f - Mathf.Repeat(w + inchRearPhase, 1f);
        }

        // Chóp xúc tu bên (Pose) khi bò kiểu sâu đo theo thông số, đi theo hướng dir
        void InchTip(int index, float w, float dir, out Vector2 tip, out float peak, out float hook, out bool gripping)
        {
            Limb limb = limbs[index];
            InchCycle(InchPhase(index, w, dir), InchLength(limb), out float reach, out float height, out peak, out hook, out gripping);
            tip = InchBasePoint(limb) + new Vector2(-limb.side * reach, height);
        }

        static readonly float[] ArchU = new float[33], ArchV = new float[33], ArchS = new float[33];

        void InchTargets(Limb limb, int index, Vector2 torsoOffset, float dt, bool live)
        {
            int n = limb.Count;
            int kb = InchBase(limb);
            float outward = -limb.side;
            Vector2 basePose = InchBasePoint(limb);
            Vector2 tip;
            float peak, hook;
            bool gripping;
            if (UsesCrawlClip)
            {
                // Tay cầm Foot L / R = chóp xúc tu; clip làm cho hướng sang phải, đi sang trái thì đổi vai và lật gương
                int source = _artDir > 0f ? index : 2 - index;
                OctopusCrawlFoot handle = crawlFeet[source];
                Vector2 offset = (Vector2)handle.transform.localPosition - InchBasePoint(limbs[source]);
                offset.x *= _artDir;
                tip = basePose + offset;
                peak = handle.peak;
                hook = handle.curl;
                gripping = handle.contact >= 0.5f;
                tip.x = FootLock(limb, tip.x, footLock && live && gripping && _walkWeight > 0.5f, dt);
            }
            else
            {
                InchTip(index, _walkPhase, _artDir, out tip, out peak, out hook, out gripping);
            }

            // Đoạn gốc: IK đưa khớp kb chạm đất ở chỗ gốc
            Vector2 baseTorso = basePose - torsoOffset;
            float baseLength = 0f;
            for (int i = 0; i < kb; i++)
                baseLength += limb.bindLengths[i];
            float stretch = Mathf.Clamp((baseTorso - limb.root).magnitude / Mathf.Max(baseLength, 0.01f), armStretch.x, armStretch.y);
            limb.ik2 = SolveJoint(limb, baseTorso, limb.ik2, stretch, kb);
            float absolute = 0f;
            for (int i = 0; i < kb; i++)
            {
                limb.targets[i] = limb.bindAngles[i] + (i == 0 ? limb.ik2.x : limb.ik2.y) + limb.wave[i];
                limb.spacingTargets[i] = limb.bindLengths[i] * stretch;
                absolute += limb.targets[i];
            }

            // Đoạn uốn: vòm có chiều dài cố định từ chỗ gốc chạm đất tới chóp
            float length = InchLength(limb);
            float reach = Mathf.Max(outward * (tip.x - basePose.x), 0.02f);
            float height = tip.y - basePose.y;
            float scale = ArchPath(length, reach, height, Mathf.Clamp(peak, 0.1f, 0.9f));
            float travelled = 0f;
            int hookStart = n - 4;
            float hookSum = 0f;
            for (int j = kb; j < n; j++)
            {
                ArchPoint(travelled, out Vector2 p0);
                travelled += limb.bindLengths[j] * scale;
                ArchPoint(travelled, out Vector2 p1);
                float direction = Mathf.Atan2(p1.y - p0.y, outward * (p1.x - p0.x)) * Mathf.Rad2Deg;
                if (j >= hookStart)
                    hookSum += hook * HookShape[j - hookStart]; // chóp quặp xuống đất
                float wanted = direction - outward * hookSum;
                limb.targets[j] = Mathf.DeltaAngle(absolute, wanted) + limb.wave[j] * 0.25f;
                absolute += limb.targets[j];
                limb.spacingTargets[j] = limb.bindLengths[j] * scale;
            }

            limb.footTarget = baseTorso;
            limb.footGripping = gripping && _walkWeight > 0.5f;
        }

        static readonly float[] HookShape = { 20f, 30f, 40f, 40f };

        // Dựng vòm (toạ độ: u ra ngoài, v lên trên) từ (0,0) tới chóp (reach, height), đỉnh ở peak phần dây cung, dài đúng length
        // (dây cung dài hơn length thì kéo thẳng và trả về hệ số giãn). Trả về hệ số giãn của đoạn uốn
        float ArchPath(float length, float reach, float height, float peak)
        {
            float chord = Mathf.Sqrt(reach * reach + height * height);
            float scale = 1f;
            float bump = 0f;
            if (chord >= length * 0.999f)
            {
                scale = Mathf.Min(chord / length, armStretch.y);
            }
            else
            {
                float lo = 0f, hi = length;
                for (int it = 0; it < 18; it++)
                {
                    bump = 0.5f * (lo + hi);
                    if (ArchLength(reach, height, peak, bump) > length)
                        hi = bump;
                    else
                        lo = bump;
                }
            }

            BuildArch(reach, height, peak, bump);
            // Chuẩn hoá chiều dài bảng về đúng length × scale (vòm hoặc dây cung bị kéo)
            float total = ArchS[ArchS.Length - 1];
            float wanted = length * scale;
            for (int i = 0; i < ArchS.Length; i++)
                ArchS[i] *= wanted / Mathf.Max(total, 1e-5f);
            return scale;
        }

        static float ArchHeight(float x, float reach, float height, float peak, float bump)
        {
            float xp = peak * reach;
            float g = x < xp
                ? Mathf.Pow(Mathf.Sin(0.5f * Mathf.PI * x / Mathf.Max(xp, 1e-5f)), 2f)
                : Mathf.Pow(Mathf.Cos(0.5f * Mathf.PI * (x - xp) / Mathf.Max(reach - xp, 1e-5f)), 2f);
            return height * x / Mathf.Max(reach, 1e-5f) + bump * g;
        }

        static float ArchLength(float reach, float height, float peak, float bump)
        {
            BuildArch(reach, height, peak, bump);
            return ArchS[ArchS.Length - 1];
        }

        static void BuildArch(float reach, float height, float peak, float bump)
        {
            int count = ArchU.Length;
            for (int i = 0; i < count; i++)
            {
                float x = reach * i / (count - 1);
                ArchU[i] = x;
                ArchV[i] = ArchHeight(x, reach, height, peak, bump);
                ArchS[i] = i == 0 ? 0f : ArchS[i - 1] + Mathf.Sqrt(Mathf.Pow(x - ArchU[i - 1], 2f) + Mathf.Pow(ArchV[i] - ArchV[i - 1], 2f));
            }
        }

        // Điểm trên vòm ở quãng đường s tính từ gốc (sau ArchPath); trả về chỉ số đoạn
        static float ArchPoint(float s, out Vector2 point) => PathPoint(ArchU, ArchV, ArchS, s, out point);

        // Điểm trên đường (bảng toạ độ us, vs và quãng đường cộng dồn ss) ở quãng đường s; trả về chỉ số đoạn
        static float PathPoint(float[] us, float[] vs, float[] ss, float s, out Vector2 point)
        {
            int count = ss.Length;
            for (int i = 1; i < count; i++)
            {
                if (s <= ss[i] || i == count - 1)
                {
                    float span = Mathf.Max(ss[i] - ss[i - 1], 1e-6f);
                    float u = Mathf.Clamp01((s - ss[i - 1]) / span);
                    // Quá cuối đường (chóp bị kéo dài hơn): đi tiếp theo hướng đoạn cuối
                    if (s > ss[i])
                        u = (s - ss[i - 1]) / span;
                    point = new Vector2(Mathf.LerpUnclamped(us[i - 1], us[i], u), Mathf.LerpUnclamped(vs[i - 1], vs[i], u));
                    return i;
                }
            }

            point = new Vector2(us[count - 1], vs[count - 1]);
            return count - 1;
        }

        // Chỗ nghỉ của chóp xúc tu: như tranh, hai xúc tu bên hạ sát đất và xoè rộng ra
        Vector2 RestFoot(Limb limb)
        {
            Vector2 foot = limb.home;
            if (limb.side != 0f)
            {
                foot.y = Mathf.Lerp(foot.y, 0f, plantSideTentacles);
                foot.x -= limb.side * idleSpread;
            }

            return foot;
        }

        ArmSlither Slither(int index) => slither != null && slither.Length > 0 ? slither[Mathf.Min(index, slither.Length - 1)] : DefaultSlither()[index];

        // Bò bằng code, xúc tu index tại pha chung w (0..1), đi theo hướng dir (1 phải, -1 trái):
        // chóp lệch khỏi chỗ nghỉ bao nhiêu, cuộn bao nhiêu, có bám không. Thông số đặt theo vai (sau / giữa / trước)
        void CrawlStep(int index, float w, float dir, out Vector2 step, out float curl, out bool gripping)
        {
            int role = dir > 0f ? index : 2 - index;
            if (crawlStyle != CrawlStyle.Step)
            {
                // Trườn (và xúc tu giữa ở kiểu sâu đo): chóp nằm sát đất, chỉ trượt nhẹ tới lui; xúc tu uốn như rắn nhờ sóng
                ArmSlither a = Slither(role);
                float angle = 2f * Mathf.PI * Mathf.Repeat(w + a.phase, 1f);
                step = new Vector2((a.reach + a.slide * Mathf.Sin(angle)) * dir, 0f);
                curl = a.curl + a.curlSwing * Mathf.Sin(angle + 1.3f);
                gripping = false;
                return;
            }

            CrawlStepStep(role, w, out step, out curl, out gripping);
            step.x *= dir;
        }

        void CrawlStepStep(int index, float w, out Vector2 step, out float curl, out bool gripping)
        {
            ArmGait g = Gait(index);
            float u = Mathf.Repeat(w + g.phase, 1f);
            if (u < g.grip)
            {
                // Bám: chóp đứng yên trên đất, thân trôi qua nên chóp lùi dần về sau so với thân
                step = new Vector2(g.plantAhead - cycleDistance * u, 0f);
                curl = g.gripCurl;
                gripping = true;
            }
            else
            {
                // Nhấc lên, đưa về chỗ bám mới phía trước
                float k = (u - g.grip) / (1f - g.grip);
                float from = g.plantAhead - cycleDistance * g.grip;
                step = new Vector2(Mathf.Lerp(from, g.plantAhead, Mathf.SmoothStep(0f, 1f, k)), g.lift * Mathf.Sin(Mathf.PI * k));
                curl = Mathf.Lerp(g.gripCurl, g.swingCurl, Mathf.Sin(Mathf.PI * k));
                gripping = false;
            }
        }

        // Thân khi bò bằng code tại pha chung w: lệch so với chỗ đứng (chồm / nhấp nhô), đầu ngả sau bao nhiêu độ
        void BodyCrawl(float w, out Vector2 offset, out float lean)
        {
            const float Tau = 2f * Mathf.PI;
            if (crawlStyle == CrawlStyle.Slither)
            {
                // Trườn: thân lướt êm, chỉ dập dềnh nhẹ hai lần mỗi nhịp
                offset = new Vector2(0f, -crawlBob * (0.5f - 0.5f * Mathf.Cos(2f * Tau * w)));
                lean = mantleTrail + mantleSurge * Mathf.Sin(Tau * w);
                return;
            }

            float u = Mathf.Repeat(w + Gait(FrontLimb).phase, 1f);
            offset = new Vector2(surge * Mathf.Sin(Tau * (u - 0.25f)), -crawlBob * (0.5f - 0.5f * Mathf.Cos(Tau * u)));
            lean = mantleTrail + mantleSurge * Mathf.Cos(Tau * (u - 0.25f));
        }

        // Khoá chân: lúc chóp bám đất, giữ nó đứng yên so với mặt đất (thân trôi qua bao nhiêu thì chóp lùi bấy nhiêu);
        // lúc nhấc lên thì trả dần về đường cong của clip để không giật
        float FootLock(Limb limb, float x, bool hold, float dt)
        {
            if (hold)
            {
                if (!limb.locked)
                {
                    limb.locked = true;
                    limb.lockGround = x + limb.release + _travel;
                }

                limb.release = 0f;
                return limb.lockGround - _travel;
            }

            if (limb.locked)
            {
                limb.locked = false;
                limb.release = limb.lockGround - _travel - x;
            }

            limb.release *= Mathf.Exp(-25f * dt);
            return x + limb.release;
        }

        // Trên không: duỗi xúc tu và xoay gốc cho cả xúc tu chỉ ngược hướng bay (kéo theo sau)
        void AirTargets(Limb limb, int index, Vector2 artVelocity)
        {
            float speed = artVelocity.magnitude;
            float strength = Mathf.InverseLerp(trailSpeed, fullTrailSpeed, speed);
            float uncurl = strength * maxUncurl;
            int n = limb.Count;

            for (int i = 0; i < n; i++)
            {
                limb.targets[i] = i == 0 ? limb.bindAngles[0] : limb.bindAngles[i] * (1f - uncurl);
                limb.spacingTargets[i] = limb.bindLengths[i];
            }

            if (strength > 0f)
                TrailRoot(limb, artVelocity, speed, strength);

            // Sóng uốn lượn tới tận ngọn
            for (int i = 0; i < n; i++)
                limb.targets[i] += limb.wave[i];
        }

        void TrailRoot(Limb limb, Vector2 artVelocity, float speed, float strength)
        {
            int n = limb.Count;
            // Hướng chính của xúc tu: từ gốc tới khớp ở 3/4 chiều dài
            Vector2 from = limb.root;
            Vector2 to = Joint(limb, n * 3 / 4, limb.targets, limb.spacingTargets);
            float main = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            Vector2 trail = -artVelocity / speed + new Vector2(-0.35f * limb.side, 0f);
            float wanted = Mathf.Atan2(trail.y, trail.x) * Mathf.Rad2Deg;

            // Quay đường dài thì đi vòng ra ngoài (xúc tu trái qua bên trái), không xuyên qua thân
            float delta = Mathf.DeltaAngle(main, wanted);
            float outward = -limb.Bulge;
            if (Mathf.Abs(delta) > 120f && Mathf.Sign(delta) != outward)
                delta -= Mathf.Sign(delta) * 360f;
            limb.targets[0] += delta * strength * trailFollow;
        }

        // Đầu (áo): ngả ra sau khi bò, lắc theo nhịp chồm, ngả ngược hướng bay; xương trên mềm hơn xương dưới
        void MantleTargets(Snapshot snap, Vector2 artVelocity)
        {
            float sway = 1.5f * Mathf.Sin(_time * 1.3f);
            switch (snap.Mode)
            {
                case Mode.Ground:
                case Mode.Cling:
                    BodyCrawl(_walkPhase, out _, out float lean);
                    sway += _walkWeight * _artDir * (UsesCrawlClip ? crawlBody.mantle : lean);
                    break;
                case Mode.Air:
                case Mode.Swing:
                    sway += mantleDrag * Mathf.Clamp(artVelocity.x / 12f, -1f, 1f);
                    break;
            }

            sway += LaserSway() + _laserNod + _actNod; // ra đòn / vận sức: đầu ngả theo (lò xo đầu cho trễ, nảy sau thân)
            for (int i = 0; i < mantle.Count; i++)
            {
                mantle.targets[i] = mantle.bindAngles[i] + sway * (i == 0 ? 0.2f : 0.45f);
                mantle.spacingTargets[i] = mantle.bindLengths[i];
            }
        }

        // Vươn tới một điểm world: đoạn gốc thành cung tròn từ gốc tới điểm nắm, đoạn chóp giữ nét cuộn của tranh
        void ReachTargets(Limb limb, Vector2 targetWorld, float dt, float duration, float curl)
        {
            int k = Mathf.Clamp(reachBones, 1, limb.Count - 1);
            if (!limb.reaching)
            {
                limb.reaching = true;
                limb.reach = 0f;
                limb.reachFrom = JointWorld(limb, k);
            }

            limb.reach = duration > 0f && dt > 0f ? Mathf.MoveTowards(limb.reach, 1f, dt / duration) : 1f;
            float e = 1f - Mathf.Pow(1f - limb.reach, 3f);
            Vector2 from = torso.InverseTransformPoint(limb.reachFrom);
            Vector2 to = torso.InverseTransformPoint(targetWorld);
            Vector2 grab = Vector2.Lerp(from, to, e);

            // Đang phóng ra thì chóp duỗi thẳng như mũi tên, tới nơi mới cuộn lại quấn vào đá
            float curlNow = Mathf.Lerp(1f, 0.35f, Mathf.Sin(Mathf.PI * limb.reach)) * Mathf.Lerp(1f, curl, limb.reach);
            ArcTargets(limb, k, grab, curlNow, grabWave);
        }

        void ArcTargets(Limb limb, int k, Vector2 grab, float curl, float wave)
        {
            int n = limb.Count;
            Vector2 chord = grab - limb.root;
            float distance = Mathf.Max(chord.magnitude, 1e-4f);
            float phi = Mathf.Atan2(chord.y, chord.x) * Mathf.Rad2Deg;

            float natural = 0f;
            for (int i = 0; i < k; i++)
                natural += limb.bindLengths[i];

            // Ngắn quá thì uốn thành cung (giữ chiều dài tối thiểu) thay vì ép ảnh co rúm
            float arc = Mathf.Max(distance, minCompression * natural);
            float theta = ArcAngle(arc / distance);
            float turn = theta / k * Mathf.Rad2Deg * limb.Bulge;
            float chordLength = theta > 1e-4f ? 2f * (arc / theta) * Mathf.Sin(theta / (2f * k)) : arc / k;

            for (int i = 0; i < n; i++)
            {
                if (i < k)
                {
                    float sway = wave * Mathf.Sin(_time * 9f - i * 0.9f);
                    limb.targets[i] = i == 0 ? phi + theta * Mathf.Rad2Deg * 0.5f * limb.Bulge - turn * 0.5f : -turn + sway;
                    limb.spacingTargets[i] = chordLength;
                }
                else
                {
                    limb.targets[i] = limb.bindAngles[i] * curl;
                    limb.spacingTargets[i] = limb.bindLengths[i];
                }
            }

            // Sóng làm lệch đầu đoạn gốc: xoay lại gốc và co giãn cho trúng điểm nắm
            Vector2 end = Joint(limb, k, limb.targets, limb.spacingTargets) - limb.root;
            limb.targets[0] += Mathf.DeltaAngle(Mathf.Atan2(end.y, end.x) * Mathf.Rad2Deg, phi);
            float fix = distance / Mathf.Max(end.magnitude, 1e-4f);
            for (int i = 0; i < k; i++)
                limb.spacingTargets[i] *= fix;
        }

        // Tổng góc ngoặt θ (rad) của cung tròn có chiều dài cung / dây cung = ratio: θ / (2 sin(θ/2)) = ratio
        static float ArcAngle(float ratio)
        {
            if (ratio <= 1.0001f)
                return 0f;
            float theta = Mathf.Min(Mathf.Sqrt(24f * (ratio - 1f)), 5.5f);
            for (int i = 0; i < 8; i++)
            {
                float f = theta - 2f * ratio * Mathf.Sin(theta * 0.5f);
                float df = 1f - ratio * Mathf.Cos(theta * 0.5f);
                if (Mathf.Abs(df) < 1e-5f)
                    break;
                theta = Mathf.Clamp(theta - f / df, 0.01f, 5.5f);
            }

            return theta;
        }

        // ---------- Sóng uốn lượn ----------

        // Đang di chuyển nhiều hay ít (0..1): bò nhanh, bay, đu thì sóng mạnh; đứng yên thì nhẹ
        float Motion(Snapshot snap)
        {
            switch (snap.Mode)
            {
                case Mode.Ground:
                case Mode.Cling:
                    return _walkWeight;
                case Mode.Swing:
                    return 1f;
                default:
                    return Mathf.Clamp01(snap.Velocity.magnitude / 6f);
            }
        }

        // Sóng chạy từ gốc ra ngọn, ngọn uốn mạnh hơn gốc; mỗi xúc tu lệch pha nhau
        void UpdateWave(Limb limb, int index, float motion)
        {
            int n = limb.Count;
            float amount = waveAmount * Mathf.Lerp(waveIdle, 1f, motion);
            float k = 2f * Mathf.PI / Mathf.Max(waveLength, 1f);
            for (int i = 0; i < n; i++)
            {
                // Uốn đều cả thân xúc tu như rắn, ngọn mạnh hơn gốc một chút
                float t = n > 1 ? (float)i / (n - 1) : 0f;
                float envelope = Mathf.Lerp(0.4f, waveTipBoost, Mathf.Pow(t, 1.5f));
                limb.wave[i] = amount * envelope * Mathf.Sin(_wavePhase - i * k + index * 2.1f);
            }
        }

        // Vây quanh đầu: sóng chạy vòng quanh đầu, mỗi vây lắc qua lại và vươn ra / thu vào lệch pha nhau
        void ApplyFins(float motion)
        {
            if (headFins == null || headFinAngles == null)
                return;
            int count = Mathf.Min(headFins.Length, headFinAngles.Length);
            float amount = Mathf.Lerp(finIdle, 1f, motion);
            for (int i = 0; i < count; i++)
            {
                if (headFins[i] == null)
                    continue;
                float phase = _time * finSpeed - i * 2f * Mathf.PI * finWaves / Mathf.Max(count, 1);
                headFins[i].localRotation = Quaternion.Euler(0f, 0f, headFinAngles[i] + finSway * amount * Mathf.Sin(phase));
                headFins[i].localScale = new Vector3(1f + finPulse * amount * Mathf.Sin(phase + 1.2f), 1f, 1f);
            }
        }

        // ---------- Lò xo và ghi vào xương ----------

        void Integrate(Limb limb, float dt, bool instant, float rootOmega, float tipOmega, float zeta)
        {
            int n = limb.Count;
            if (instant)
            {
                Array.Copy(limb.targets, limb.angles, n);
                Array.Copy(limb.spacingTargets, limb.spacing, n);
                Array.Clear(limb.velocities, 0, n);
                return;
            }

            if (limb.swapBlend > 0f)
                limb.swapBlend = Mathf.Max(0f, limb.swapBlend - dt);

            if (limb.acting)
            {
                // Ra đòn: theo đúng dáng đòn (trễ, vượt quá, quán tính đã có sẵn trong clip; chuyển dáng đã mượt)
                Array.Copy(limb.targets, limb.angles, n);
                Array.Copy(limb.spacingTargets, limb.spacing, n);
                Array.Clear(limb.velocities, 0, n);
                return;
            }

            if (limb.exactPath && limb.planted >= 1f)
            {
                // Sâu đo / rắn: cả xúc tu theo đúng đường vẽ sẵn; vừa đổi hướng thì chuyển dáng mượt
                float follow = limb.swapBlend > 0f ? 1f - Mathf.Exp(-18f * dt) : 1f;
                for (int i = 0; i < n; i++)
                {
                    limb.angles[i] = Mathf.LerpAngle(limb.angles[i], limb.targets[i], follow);
                    limb.spacing[i] = Mathf.Lerp(limb.spacing[i], limb.spacingTargets[i], follow);
                    limb.velocities[i] = 0f;
                }

                return;
            }

            if (limb.reaching)
            {
                // Bám sát đích để chóp trúng điểm nắm; vừa bắt đầu vươn thì nối mượt từ dáng cũ
                float blend = limb.reach >= 1f ? 1f : 1f - Mathf.Exp(-reachSnap * dt);
                for (int i = 0; i < n; i++)
                {
                    limb.angles[i] = Mathf.LerpAngle(limb.angles[i], limb.targets[i], blend);
                    limb.spacing[i] = Mathf.Lerp(limb.spacing[i], limb.spacingTargets[i], blend);
                    limb.velocities[i] = 0f;
                }

                return;
            }

            // Hai bước nhỏ cho lò xo cứng không mất ổn định khi frame dài
            float h = dt * 0.5f;
            float spacingBlend = 1f - Mathf.Exp(-spacingRate * dt);
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? (float)i / (n - 1) : 0f;
                float omega = Mathf.Lerp(rootOmega, tipOmega, t);
                for (int s = 0; s < 2; s++)
                {
                    float error = Mathf.DeltaAngle(limb.angles[i], limb.targets[i]);
                    limb.velocities[i] += (omega * omega * error - 2f * zeta * omega * limb.velocities[i]) * h;
                    limb.angles[i] += limb.velocities[i] * h;
                }

                limb.spacing[i] = Mathf.Lerp(limb.spacing[i], limb.spacingTargets[i], spacingBlend);

                // Đứng trên đất: đoạn từ gốc tới chân theo đúng IK để chân đang bám không trượt (lò xo phản ứng
                // chậm hơn nhịp bò); chóp vẫn theo lò xo nên vẫn nảy. Vừa tiếp đất thì chuyển dần sang IK
                if (i <= limb.exactUpTo && limb.planted > 0f)
                {
                    limb.angles[i] = Mathf.LerpAngle(limb.angles[i], limb.targets[i], limb.planted);
                    limb.spacing[i] = Mathf.Lerp(limb.spacing[i], limb.spacingTargets[i], limb.planted);
                    limb.velocities[i] *= 1f - limb.planted;
                }
            }
        }

        static void Apply(Limb limb)
        {
            for (int i = 0; i < limb.Count; i++)
            {
                Transform bone = limb.bones[i];
                bone.localPosition = i == 0 ? (Vector3)limb.root : new Vector3(limb.spacing[i - 1], 0f, 0f);
                bone.localRotation = Quaternion.Euler(0f, 0f, limb.angles[i]);
            }
        }

        // ---------- Động học thuận / IK ----------

        static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        // Vị trí khớp thứ index (0 = gốc, Count = chóp) trong Torso
        static Vector2 Joint(Limb limb, int index, float[] angles, float[] lengths)
        {
            Vector2 p = limb.root;
            float a = 0f;
            for (int i = 0; i < index && i < limb.Count; i++)
            {
                a += angles[i];
                p += Dir(a) * lengths[i];
            }

            return p;
        }

        Vector2 JointWorld(Limb limb, int index) => torso.TransformPoint(Joint(limb, index, limb.angles, limb.spacing));

        static Vector2 Foot(Limb limb, float[] angles, float[] lengths)
        {
            Vector2 p = limb.root;
            float a = 0f;
            for (int i = 0; i <= limb.footBone; i++)
            {
                a += angles[i];
                if (i == limb.footBone)
                    return p + (Vector2)(Quaternion.Euler(0f, 0f, a) * limb.footOffset);
                p += Dir(a) * lengths[i];
            }

            return p;
        }

        // Điểm chạm đất khi xoay gốc thêm ik.x độ, cuộn mỗi xương tới chân thêm ik.y độ, đoạn tới chân dài stretch lần
        static Vector2 FootFor(Limb limb, Vector2 ik, float stretch)
        {
            Vector2 p = limb.root;
            float a = 0f;
            for (int i = 0; i <= limb.footBone; i++)
            {
                a += limb.bindAngles[i] + (i == 0 ? ik.x : ik.y) + (limb.wave != null ? limb.wave[i] : 0f);
                if (i == limb.footBone)
                    return p + (Vector2)(Quaternion.Euler(0f, 0f, a) * limb.footOffset);
                p += Dir(a) * limb.bindLengths[i] * stretch;
            }

            return p;
        }

        // Vị trí khớp index khi xoay gốc thêm ik.x, cuộn các xương trước đó thêm ik.y, các xương dài stretch lần
        static Vector2 JointFor(Limb limb, Vector2 ik, float stretch, int index)
        {
            Vector2 p = limb.root;
            float a = 0f;
            for (int i = 0; i < index; i++)
            {
                a += limb.bindAngles[i] + (i == 0 ? ik.x : ik.y) + (limb.wave != null ? limb.wave[i] : 0f);
                p += Dir(a) * limb.bindLengths[i] * stretch;
            }

            return p;
        }

        // Như SolveFoot nhưng đưa khớp index (không phải điểm chạm đất) tới đích
        static Vector2 SolveJoint(Limb limb, Vector2 target, Vector2 guess, float stretch, int index)
        {
            Vector2 x = guess;
            const float h = 0.5f;
            for (int iteration = 0; iteration < 4; iteration++)
            {
                Vector2 f0 = JointFor(limb, x, stretch, index);
                Vector2 error = f0 - target;
                if (error.sqrMagnitude < 1e-7f)
                    break;

                Vector2 ja = (JointFor(limb, x + new Vector2(h, 0f), stretch, index) - f0) / h;
                Vector2 jc = (JointFor(limb, x + new Vector2(0f, h), stretch, index) - f0) / h;
                float m11 = Vector2.Dot(ja, ja), m12 = Vector2.Dot(ja, jc), m22 = Vector2.Dot(jc, jc);
                float lambda = (m11 + m22) * 0.01f + 1e-9f;
                m11 += lambda;
                m22 += lambda;
                float r1 = -Vector2.Dot(ja, error), r2 = -Vector2.Dot(jc, error);
                float det = m11 * m22 - m12 * m12;
                if (Mathf.Abs(det) < 1e-12f)
                    break;

                var step = new Vector2((r1 * m22 - m12 * r2) / det, (m11 * r2 - m12 * r1) / det);
                x += new Vector2(Mathf.Clamp(step.x, -8f, 8f), Mathf.Clamp(step.y, -4f, 4f));
                x = new Vector2(Mathf.Clamp(x.x, -70f, 70f), Mathf.Clamp(x.y, -30f, 30f));
            }

            return x;
        }

        // Gauss-Newton 2 ẩn (xoay gốc, cuộn) để điểm chạm đất tới đích; bắt đầu từ nghiệm frame trước
        static Vector2 SolveFoot(Limb limb, Vector2 target, Vector2 guess, float stretch)
        {
            Vector2 x = guess;
            const float h = 0.5f;
            for (int iteration = 0; iteration < 4; iteration++)
            {
                Vector2 f0 = FootFor(limb, x, stretch);
                Vector2 error = f0 - target;
                if (error.sqrMagnitude < 1e-7f)
                    break;

                Vector2 ja = (FootFor(limb, x + new Vector2(h, 0f), stretch) - f0) / h;
                Vector2 jc = (FootFor(limb, x + new Vector2(0f, h), stretch) - f0) / h;
                float m11 = Vector2.Dot(ja, ja), m12 = Vector2.Dot(ja, jc), m22 = Vector2.Dot(jc, jc);
                float lambda = (m11 + m22) * 0.01f + 1e-9f;
                m11 += lambda;
                m22 += lambda;
                float r1 = -Vector2.Dot(ja, error), r2 = -Vector2.Dot(jc, error);
                float det = m11 * m22 - m12 * m12;
                if (Mathf.Abs(det) < 1e-12f)
                    break;

                var step = new Vector2((r1 * m22 - m12 * r2) / det, (m11 * r2 - m12 * r1) / det);
                x += new Vector2(Mathf.Clamp(step.x, -8f, 8f), Mathf.Clamp(step.y, -4f, 4f));
                x = new Vector2(Mathf.Clamp(x.x, -45f, 45f), Mathf.Clamp(x.y, -14f, 14f));
            }

            return x;
        }

        // Đọc dáng gốc từ xương trong ảnh: sửa xương trong Skinning Editor (Apply) là code dùng dáng mới
        void RefreshBind()
        {
            RefreshBind(mantle);
            foreach (Limb limb in limbs)
                RefreshBind(limb);
        }

        void RefreshBind(Limb limb)
        {
            if (limb == null || limb.renderer == null || limb.renderer.sprite == null)
                return;

            SpriteBone[] spriteBones = limb.renderer.sprite.GetBones();
            if (spriteBones.Length == 0)
                return;
            int n = limb.bones.Length;
            bool hasExtra = limb == mantle; // ảnh đầu: sau chuỗi đầu là các xương vây
            if (spriteBones.Length < n || (!hasExtra && spriteBones.Length != n))
            {
                if (!_warnedBones)
                    Debug.LogWarning($"[Octopus] {limb.name}: ảnh có {spriteBones.Length} xương nhưng rig có {limb.bones.Length}. " +
                                     "Chạy Octopus → Dựng lại bạch tuộc để tạo lại xương.", this);
                _warnedBones = true;
                return;
            }

            float signature = 0f;
            for (int i = 0; i < spriteBones.Length; i++)
            {
                SpriteBone b = spriteBones[i];
                signature += (i + 1) * (b.position.x * 3.1f + b.position.y * 7.3f + b.rotation.z * 11.7f + b.length * 5.9f);
            }

            if (Mathf.Approximately(signature, limb.bindSignature) && limb.angles != null)
                return;

            limb.bindSignature = signature;
            if (limb.bindAngles.Length != n)
                limb.bindAngles = new float[n];
            if (limb.bindLengths.Length != n)
                limb.bindLengths = new float[n];
            for (int i = 0; i < n; i++)
            {
                limb.bindAngles[i] = Mathf.DeltaAngle(0f, spriteBones[i].rotation.eulerAngles.z);
                limb.bindLengths[i] = i + 1 < n ? spriteBones[i + 1].position.x : spriteBones[i].length;
            }

            limb.root = (Vector2)limb.renderer.transform.localPosition + (Vector2)spriteBones[0].position;
            limb.angles = null; // khởi tạo lại trạng thái chạy

            if (hasExtra && headFinAngles != null)
            {
                for (int i = 0; i < headFinAngles.Length && n + i < spriteBones.Length; i++)
                    headFinAngles[i] = Mathf.DeltaAngle(0f, spriteBones[n + i].rotation.eulerAngles.z);
            }
        }

        void EnsureRuntime()
        {
            EnsureRuntime(mantle);
            foreach (Limb limb in limbs)
                EnsureRuntime(limb);

            if (!_pupilBindStored)
            {
                _pupilBind = pupil.localPosition;
                _pupilBindStored = true;
            }
        }

        static void EnsureRuntime(Limb limb)
        {
            int n = limb.Count;
            if (limb.blendAngles == null || limb.blendAngles.Length != n)
            {
                limb.actAngles = new float[n];
                limb.actSpacing = new float[n];
                limb.blendAngles = new float[n];
                limb.blendSpacing = new float[n];
                limb.blendTime = 1f;
                limb.blendLength = 0f;
                limb.blendPending = false;
                limb.acting = false;
            }

            if (limb.angles != null && limb.angles.Length == n)
                return;

            limb.angles = (float[])limb.bindAngles.Clone();
            limb.targets = (float[])limb.bindAngles.Clone();
            limb.velocities = new float[n];
            limb.spacing = (float[])limb.bindLengths.Clone();
            limb.spacingTargets = (float[])limb.bindLengths.Clone();
            limb.wave = new float[n];
            limb.restTargets = new float[n];
            limb.restSpacing = new float[n];
            limb.home = Foot(limb, limb.bindAngles, limb.bindLengths);
            limb.ik = Vector2.zero;
        }

        // ---------- Mắt ----------

        void UpdateEye(Snapshot snap, float dt, bool live)
        {
            float open = 1f;
            if (live)
            {
                _blinkTimer -= dt;
                if (_blinkTimer <= 0f)
                {
                    _blinkClock = 0f;
                    _blinkTimer = UnityEngine.Random.Range(blinkInterval.x, blinkInterval.y);
                }

                if (_blinkClock >= 0f)
                {
                    _blinkClock += dt;
                    if (_blinkClock >= blinkTime)
                        _blinkClock = -1f;
                    else
                        open = 1f - 0.94f * Mathf.Sin(Mathf.PI * _blinkClock / blinkTime);
                }
            }
            else if (previewTime > 0.9f)
            {
                open = 0.06f; // kéo hết thanh thời gian xem trước để xem mắt nhắm
            }

            // Chiêu laze: vận sức thì mắt mở to, không chớp; bắn xong thì lim dim một lúc
            float slit = 1f;
            if (_laser != null && Application.isPlaying)
            {
                switch (_laser.Phase)
                {
                    case LaserPhase.Charging:
                    case LaserPhase.Firing:
                    {
                        float power = _laser.Phase == LaserPhase.Firing ? 1f : Mathf.SmoothStep(0f, 1f, _laser.PhaseProgress);
                        open = 1f + laserEyeWiden * power;
                        slit = Mathf.Lerp(1f, laserPupilSlit, power);
                        _blinkClock = -1f;
                        _blinkTimer = Mathf.Max(_blinkTimer, 1f);
                        break;
                    }
                    case LaserPhase.Cooldown when _laser.Elapsed < laserTiredTime:
                        open = Mathf.Lerp(0.4f, 1f, Mathf.SmoothStep(0.6f, 1f, _laser.Elapsed / laserTiredTime));
                        break;
                }
            }

            eye.localScale = new Vector3(1f, open, 1f);
            pupil.localScale = new Vector3(slit, 1f, 1f);

            // Con ngươi: nhìn về phía đá khi đu, liếc theo hướng đi khi bò / bay, thỉnh thoảng nhìn quanh khi đứng yên
            Vector2 look;
            bool aiming = _laser != null && Application.isPlaying && _laser.Phase is LaserPhase.Charging or LaserPhase.Firing;
            switch (snap.Mode)
            {
                case var _ when aiming:
                    look = new Vector2(Mathf.Sign(ToArt(_laser.Direction).x), 0f); // nhìn thẳng theo hướng bắn laze
                    break;
                case var _ when _act != CombatPose.None && Application.isPlaying:
                    look = new Vector2(_actDir, 0f); // ra đòn: nhìn thẳng về phía đánh
                    break;
                case Mode.Swing:
                    look = ToArt(snap.Anchor - snap.Position).normalized;
                    break;
                default:
                {
                    Vector2 v = ToArt(snap.Velocity);
                    look = new Vector2(Mathf.Clamp(v.x / 6f, -1f, 1f), Mathf.Clamp(v.y / 14f, -1f, 1f));
                    if (look.sqrMagnitude < 0.04f && live)
                    {
                        _glanceTimer -= dt;
                        if (_glanceTimer <= 0f)
                        {
                            _glanceTimer = UnityEngine.Random.Range(1.2f, 3f);
                            _glanceTarget = UnityEngine.Random.value < 0.5f
                                ? Vector2.zero
                                : new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-0.6f, 0.6f));
                        }

                        look = _glanceTarget;
                    }

                    break;
                }
            }

            Vector2 offset = Vector2.Scale(Vector2.ClampMagnitude(look, 1f), pupilRange);
            _pupilOffset = live ? Vector2.Lerp(_pupilOffset, offset, 1f - Mathf.Exp(-12f * dt)) : offset;
            pupil.localPosition = _pupilBind + _pupilOffset;
            if (pupilUpright)
                pupil.rotation = Quaternion.identity;
            else
                pupil.localRotation = Quaternion.identity;
        }

        // ---------- Sự kiện → nhịp, co giãn ----------

        void OnLaserPhase(LaserPhase phase)
        {
            if (phase == LaserPhase.Firing)
                Kick(-laserKick); // tia bung ra: thân bẹp lại như bị giật lùi
        }

        // Đầu bị đẩy ngả ra sau (ngược hướng bắn) khi đang bắn, rung theo tia. Góc đầu dương = ngả ra sau khi quay phải
        // (như đầu ngả khi bò / dáng đòn), nên "ra sau" = cùng dấu với hướng bắn trong tranh
        float LaserSway()
        {
            if (_laser == null || !Application.isPlaying || _laser.Phase != LaserPhase.Firing)
                return 0f;

            float start = Mathf.SmoothStep(0f, 1f, _laser.Elapsed / 0.15f);
            float end = Mathf.SmoothStep(0f, 1f, (_laser.FireTime - _laser.Elapsed) / 0.25f);
            return (_laserFwd * laserRecoil + 1.5f * Mathf.Sin(_time * 47f)) * start * end;
        }

        // Dáng vận sức laze: nhổm lên, ngả người và ngửa đầu ra sau (ngược hướng bắn), run mạnh dần → tia bung thì vẫn gồng
        // một phần, đầu bị đẩy ra sau → tắt tia thì về dáng thường. Xúc tu vẫn bám đất (IK tự duỗi theo thân).
        // Biên độ theo thời gian vận sức (quét laze vận sức ngắn thì gồng nhẹ, nhanh); đang ra đòn cùng lúc (đại pháo chồm lên)
        // thì phần thân giảm bớt để cộng với dáng đòn không quá đà
        void UpdateLaserPose(float dt, bool live)
        {
            if (!live || !Application.isPlaying)
                return;

            float target = 0f, tremble = 0f;
            if (_laser != null && _laser.Phase is LaserPhase.Charging or LaserPhase.Firing)
            {
                _laserFwd = Mathf.Sign(ToArt(_laser.Direction).x + 1e-4f);
                _laserAmount = Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(0.3f, 0.9f, _laser.ChargeTime));
                if (_laser.Phase == LaserPhase.Charging)
                {
                    float p = _laser.PhaseProgress;
                    target = 1f - (1f - p) * (1f - p); // vào thế nhanh rồi gồng dần tới lúc bắn
                    tremble = p * p;
                }
                else
                {
                    float t = _laser.Elapsed;
                    float end = Mathf.SmoothStep(0f, 1f, (_laser.FireTime - t) / 0.3f);
                    target = Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0f, 1f, t / 0.2f)) * end;
                    tremble = 0.35f * end;
                }
            }

            _laserPose = Mathf.Lerp(_laserPose, target, 1f - Mathf.Exp(-dt / 0.06f));
            float w = _laserPose * _laserAmount;
            float body = w * (1f - 0.6f * _actEngage);
            _laserLean = laserChargeLean * body * _laserFwd;
            _laserNod = laserChargeNod * w * (1f - 0.4f * _actEngage) * _laserFwd;
            _laserShift = new Vector2(-0.4f * _laserFwd, 1f) * (laserChargeRise * body);
            _laserStretch = 0.5f * laserChargeRise * body;

            // Run: lệch nhỏ, đổi liên tục (vài tần số lệch nhau nên không thành nhịp đều)
            float shake = laserChargeTremble * tremble * _laserAmount;
            _laserShift += new Vector2(Mathf.Sin(_time * 67f) + 0.5f * Mathf.Sin(_time * 113f + 2f), 0.8f * Mathf.Sin(_time * 89f + 1f)) * shake;
            _laserLean += 60f * shake * Mathf.Sin(_time * 53f + 0.5f);
        }

        void OnJumped()
        {
            _takeoffTimer = takeoffTime;
            Kick(jumpStretch);
        }

        void OnAttached(Vector2 anchor)
        {
            limbs[GrabLimb].reaching = false; // vươn lại từ dáng hiện tại tới đá mới
            Kick(grabStretch);
        }

        void OnReleased()
        {
            limbs[GrabLimb].reaching = false;
            Kick(releaseStretch);
        }

        void OnStateChanged(OctopusState state)
        {
            if (state == OctopusState.Grounded && _lastState == OctopusState.Airborne)
            {
                _landImpact = 0.35f + 0.65f * Mathf.Clamp01(_fallSpeed / 18f);
                _landTimer = landTime;
                Kick(-landSquash * _landImpact);
                foreach (Limb limb in limbs)
                    limb.ik = Vector2.zero;
            }
            else if (state == OctopusState.Clinging)
            {
                Kick(-0.15f);
            }

            if (state != OctopusState.Airborne)
                _fallSpeed = 0f;
            _lastState = state;
        }

        void UpdateTimers(float dt)
        {
            _takeoffTimer = Mathf.Max(0f, _takeoffTimer - dt);
            _landTimer = Mathf.Max(0f, _landTimer - dt);
        }

        void Kick(float amount)
        {
            _squashVelocity += amount * Mathf.Sqrt(squashStiffness);
        }

        void UpdateSquash(float dt)
        {
            float acceleration = -squashStiffness * _squash - squashDamping * _squashVelocity;
            _squashVelocity += acceleration * dt;
            _squash = Mathf.Clamp(_squash + _squashVelocity * dt, -0.6f, 0.8f);
        }

        // ---------- Clip bò: tạo từ thông số Gait, đặt tay cầm theo clip ----------

        /// <summary>Một đường cong của clip bò: tay cầm (đường dẫn tính từ Rig Visual), loại component, thuộc tính.</summary>
        public struct CrawlCurve
        {
            public string Path;
            public Type Type;
            public string Property;
            public AnimationCurve Curve;
        }

        /// <summary>Đặt tay cầm theo clip bò tại pha phase (0..1 = một vòng).</summary>
        public void SampleCrawlClip(float phase)
        {
            if (crawlCurves != null)
                crawlCurves.Sample(transform, Mathf.Repeat(phase, 1f) * Mathf.Max(crawlCurves.length, 0.01f));
        }

        /// <summary>
        /// Đường cong cho clip bò dài 1 giây (một vòng) giống hệt dáng bò bằng code hiện tại (Crawl Style: Trườn hoặc Bước).
        /// Editor dùng để tạo / tạo lại Crawl.anim. Trườn: keyframe mỗi 10 frame; Bước: keyframe ở các mốc chính
        /// (bám, nhấc lên, giữa lúc đưa tới). Ít keyframe cho dễ sửa tay.
        /// </summary>
        public List<CrawlCurve> CrawlCurvesFromGait()
        {
            EnsureRuntime();
            var curves = new List<CrawlCurve>();
            for (int i = 0; i < 3 && i < crawlFeet.Length; i++)
            {
                int index = i;
                Vector2 rest = RestFoot(limbs[i]);
                string path = PathOf(crawlFeet[i].transform);

                Vector2 Step(float w)
                {
                    CrawlStep(index, w, 1f, out Vector2 step, out _, out _);
                    return step;
                }

                float Curl(float w)
                {
                    CrawlStep(index, w, 1f, out _, out float curl, out _);
                    return curl;
                }

                if (crawlStyle == CrawlStyle.Inchworm && limbs[i].side != 0f)
                {
                    // Sâu đo: tay cầm = chóp xúc tu; curl = chóp quặp; peak = đỉnh vòm; contact = chóp đang bám (khoá chân).
                    // 12 keyframe mỗi vòng (5 frame một cái); clip làm cho hướng sang phải
                    Vector2 Tip(float w)
                    {
                        InchTip(index, w, 1f, out Vector2 tip, out _, out _, out _);
                        return tip;
                    }

                    float Peak(float w)
                    {
                        InchTip(index, w, 1f, out _, out float peak, out _, out _);
                        return peak;
                    }

                    float Hook(float w)
                    {
                        InchTip(index, w, 1f, out _, out _, out float hook, out _);
                        return inchHook > 0f ? hook : 0f;
                    }

                    float Contact(float w)
                    {
                        InchTip(index, w, 1f, out _, out _, out _, out bool grip);
                        return grip ? 1f : 0f;
                    }

                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.x", Sampled(w => Tip(w).x, 12)));
                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.y", Sampled(w => Tip(w).y, 12)));
                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.z", Sampled(w => 0f, 1)));
                    curves.Add(Curve(path, typeof(OctopusCrawlFoot), "curl", Sampled(Hook, 12)));
                    curves.Add(Curve(path, typeof(OctopusCrawlFoot), "peak", Sampled(Peak, 12)));
                    curves.Add(Curve(path, typeof(OctopusCrawlFoot), "contact", SteppedFrames(Contact)));
                    continue;
                }

                if (crawlStyle == CrawlStyle.Slither)
                {
                    // Trườn: tay cầm = chỗ xúc tu chạm đất, trượt tới lui êm (6 keyframe mỗi vòng, 10 frame một cái);
                    // curl = chóp cong lên; không khoá chân
                    Vector2 touch = SnakeBasePoint(limbs[i], 1f);
                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.x", Sampled(w => touch.x + Step(w).x)));
                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.y", Sampled(w => touch.y, 1)));
                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.z", Sampled(w => 0f, 1)));
                    curves.Add(Curve(path, typeof(OctopusCrawlFoot), "curl", Sampled(Curl)));
                    curves.Add(Curve(path, typeof(OctopusCrawlFoot), "contact", Stepped(0f)));
                    continue;
                }

                if (crawlStyle != CrawlStyle.Step)
                {
                    // Xúc tu giữa ở kiểu sâu đo: chóp trượt tới lui êm, luôn sát đất, không khoá chân
                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.x", Sampled(w => rest.x + Step(w).x)));
                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.y", Sampled(w => rest.y, 1)));
                    curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.z", Sampled(w => 0f, 1)));
                    curves.Add(Curve(path, typeof(OctopusCrawlFoot), "curl", Sampled(Curl)));
                    curves.Add(Curve(path, typeof(OctopusCrawlFoot), "contact", Stepped(0f)));
                    continue;
                }

                ArmGait g = Gait(i);
                float swing = Mathf.Max(1f - g.grip, 0.01f);
                float mid = g.grip + swing * 0.5f;
                float lift = g.lift * Mathf.PI / swing;
                float curlSlope = (g.swingCurl - g.gripCurl) * Mathf.PI / swing;
                // Mốc tính theo pha của xúc tu u; pha chung w = u - phase
                Func<float, float> fx = u => rest.x + Step(u - g.phase).x;
                Func<float, float> fy = u => rest.y + Step(u - g.phase).y;
                Func<float, float> fc = u => Curl(u - g.phase);

                // x: lúc bám lùi đều theo thân (không trượt); lúc đưa tới theo đường êm (tiếp tuyến 0 ở hai đầu)
                curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.x", Periodic(g.phase, fx,
                    (0f, 0f, -cycleDistance), (g.grip, -cycleDistance, 0f))));
                // y: nhấc lên theo nửa hình sin
                curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.y", Periodic(g.phase, fy,
                    (0f, -lift, 0f), (g.grip, 0f, lift), (mid, 0f, 0f))));
                curves.Add(Curve(path, typeof(Transform), "m_LocalPosition.z", Periodic(g.phase, u => 0f, (0f, 0f, 0f))));
                curves.Add(Curve(path, typeof(OctopusCrawlFoot), "curl", Periodic(g.phase, fc,
                    (0f, -curlSlope, 0f), (g.grip, 0f, curlSlope), (mid, 0f, 0f))));
                // contact: bậc thang (giữ nguyên giá trị tới keyframe sau)
                curves.Add(Curve(path, typeof(OctopusCrawlFoot), "contact", Periodic(g.phase, u => u < g.grip ? 1f : 0f,
                    (0f, float.PositiveInfinity, float.PositiveInfinity), (g.grip, float.PositiveInfinity, float.PositiveInfinity))));
            }

            // Thân: lệch so với chỗ đứng và đầu ngả sau, lấy mẫu đều mỗi 10 frame
            string body = PathOf(crawlBody.transform);
            curves.Add(Curve(body, typeof(Transform), "m_LocalPosition.x", Sampled(w => BodyAt(w).x)));
            curves.Add(Curve(body, typeof(Transform), "m_LocalPosition.y", Sampled(w => BodyAt(w).y)));
            curves.Add(Curve(body, typeof(Transform), "m_LocalPosition.z", Sampled(w => 0f, 1)));
            curves.Add(Curve(body, typeof(OctopusCrawlBody), "mantle", Sampled(w => LeanAt(w))));
            return curves;
        }

        Vector2 BodyAt(float w)
        {
            BodyCrawl(w, out Vector2 offset, out _);
            return offset;
        }

        float LeanAt(float w)
        {
            BodyCrawl(w, out _, out float lean);
            return lean;
        }

        // Đường cong lặp vòng dài 1 giây lấy mẫu đều: keys keyframe (mặc định 6 = mỗi 10 frame), tiếp tuyến đúng độ dốc
        static AnimationCurve Sampled(Func<float, float> f, int keys = 6)
        {
            var frames = new Keyframe[keys + 1];
            for (int i = 0; i <= keys; i++)
            {
                float t = (float)i / keys;
                float slope = (f(Mathf.Repeat(t + 0.001f, 1f)) - f(Mathf.Repeat(t - 0.001f, 1f))) / 0.002f;
                frames[i] = new Keyframe(t, f(Mathf.Repeat(t, 1f)), slope, slope);
            }

            return new AnimationCurve(frames) { preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.Loop };
        }

        // Đường bậc thang lặp vòng: một keyframe mỗi lần giá trị đổi (xét từng frame trong 60 frame)
        static AnimationCurve SteppedFrames(Func<float, float> f)
        {
            var keys = new List<Keyframe> { new Keyframe(0f, f(0f), float.PositiveInfinity, float.PositiveInfinity) };
            float last = f(0f);
            for (int frame = 1; frame < 60; frame++)
            {
                float value = f(frame / 60f);
                if (Mathf.Approximately(value, last))
                    continue;
                keys.Add(new Keyframe(frame / 60f, value, float.PositiveInfinity, float.PositiveInfinity));
                last = value;
            }

            keys.Add(new Keyframe(1f, f(0f), float.PositiveInfinity, float.PositiveInfinity));
            return new AnimationCurve(keys.ToArray()) { preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.Loop };
        }

        static AnimationCurve Stepped(float value) => new AnimationCurve(
            new Keyframe(0f, value, float.PositiveInfinity, float.PositiveInfinity),
            new Keyframe(1f, value, float.PositiveInfinity, float.PositiveInfinity))
        {
            preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.Loop,
        };

        static CrawlCurve Curve(string path, Type type, string property, AnimationCurve curve) =>
            new CrawlCurve { Path = path, Type = type, Property = property, Curve = curve };

        string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && p != transform; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        // Đường cong lặp vòng dài 1 giây: keyframe ở các mốc (pha u của xúc tu, tiếp tuyến trái, phải), thời gian trong clip
        // = u - phase; làm tròn tới frame (60 fps) cho dễ sửa; thêm keyframe ở 0 và 1 giây để vòng sau nối liền vòng trước
        static AnimationCurve Periodic(float phase, Func<float, float> f, params (float u, float inTangent, float outTangent)[] events)
        {
            var keys = new List<Keyframe>();

            void Add(float time, float value, float inTangent, float outTangent)
            {
                foreach (Keyframe k in keys)
                {
                    if (Mathf.Abs(k.time - time) < 0.5f / 60f)
                        return;
                }

                keys.Add(new Keyframe(time, value, inTangent, outTangent));
            }

            foreach ((float u, float inTangent, float outTangent) in events)
            {
                float time = Mathf.Round(Mathf.Repeat(u - phase, 1f) * 60f) / 60f;
                float value = f(Mathf.Repeat(u, 1f));
                if (time <= 0f || time >= 1f)
                {
                    Add(0f, value, inTangent, outTangent);
                    Add(1f, value, inTangent, outTangent);
                }
                else
                {
                    Add(time, value, inTangent, outTangent);
                }
            }

            float u0 = Mathf.Repeat(phase, 1f);
            float slope = (f(Mathf.Repeat(u0 + 0.001f, 1f)) - f(Mathf.Repeat(u0 - 0.001f, 1f))) / 0.002f;
            if (float.IsNaN(slope) || Mathf.Abs(slope) > 1000f)
                slope = 0f;
            Add(0f, f(u0), slope, slope);
            Add(1f, f(u0), slope, slope);
            keys.Sort((a, b) => a.time.CompareTo(b.time));
            return new AnimationCurve(keys.ToArray()) { preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.Loop };
        }

        // ---------- Vẽ khung xương trong cửa sổ Scene ----------

        void OnDrawGizmos()
        {
            if (!showBones || !HasReferences || torso == null || limbs[0].angles == null)
                return;

            float r = 0.012f * size;
            DrawChain(mantle, new Color(0.4f, 0.9f, 1f), new Color(0.2f, 0.5f, 1f), r * 1.4f);
            for (int i = 0; i < limbs.Length; i++)
            {
                Limb limb = limbs[i];
                DrawChain(limb, new Color(1f, 0.95f, 0.3f), new Color(1f, 0.25f, 0.2f), r);

                if (OnSurface(_mode))
                {
                    // Đích IK của chân (dấu +) và chân đang bám đất (chấm xanh lá)
                    Vector3 target = torso.TransformPoint(limb.footTarget);
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(target + Vector3.left * r * 3f, target + Vector3.right * r * 3f);
                    Gizmos.DrawLine(target + Vector3.down * r * 3f, target + Vector3.up * r * 3f);
                    Vector3 foot = FootPosition(i);
                    Gizmos.color = limb.footGripping ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.6f, 0.2f);
                    Gizmos.DrawSphere(foot, r * (limb.footGripping ? 2.2f : 1.4f));
                }

                if (limb.reaching)
                {
                    Gizmos.color = new Color(1f, 0.9f, 0.2f);
                    Gizmos.DrawWireSphere(JointWorld(limb, Mathf.Min(reachBones, limb.Count - 1)), r * 3f);
                }
            }
        }

        void DrawChain(Limb limb, Color rootColor, Color tipColor, float radius)
        {
            if (limb.angles == null)
                return;
            for (int i = 0; i < limb.Count; i++)
            {
                Gizmos.color = Color.Lerp(rootColor, tipColor, (float)i / Mathf.Max(1, limb.Count - 1));
                Vector3 a = JointWorld(limb, i), b = JointWorld(limb, i + 1);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireSphere(a, radius);
            }
        }
    }
}
