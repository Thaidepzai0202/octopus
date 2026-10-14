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
    public class OctopusRigAnimator : MonoBehaviour
    {
        public enum Preview { Idle, Crawl, Jump, Fall, Swing, Cling }

        /// <summary>Trườn: xúc tu sát đất uốn sóng như rắn, thân lướt êm. Bước: nhấc từng xúc tu lên đặt xuống.</summary>
        public enum CrawlStyle { Slither, Step }

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
            [NonSerialized] public Vector2 grip;        // world: điểm đang nắm khi bám đá
            [NonSerialized] public bool gripping;
            [NonSerialized] public float planted;       // 0..1: đoạn tới chân theo đúng nghiệm IK (đứng trên đất)
            [NonSerialized] public float bindSignature;
            [NonSerialized] public bool locked;         // khoá chân: chóp đang bám được giữ đứng yên trên đất
            [NonSerialized] public float lockGround;    // vị trí chóp đang khoá, đo dọc mặt đất (unit tranh)
            [NonSerialized] public float release;       // độ lệch còn lại sau khi mở khoá, giảm dần về 0

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
            [Tooltip("Chóp nằm cách chỗ nghỉ bao xa (unit tranh; dương = phía trước). Âm cho xúc tu sau để nó duỗi dài ra sau")]
            public float reach;
            [Tooltip("Chóp trượt tới lui bao nhiêu theo nhịp (unit tranh)")]
            public float slide = 0.06f;
            [Tooltip("Chóp cuộn bao nhiêu (1 = như tranh, nhỏ hơn = duỗi dài sát đất)")]
            public float curl = 0.8f;
            [Tooltip("Độ cuộn chóp lên xuống theo nhịp")]
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
        [SerializeField, Range(0f, 0.4f)] float crawlCrouch = 0.15f;
        [Tooltip("Đầu dẹt xuống một chút khi bò (0 = giữ nguyên)")]
        [SerializeField, Range(0f, 0.3f)] float crawlFlatten = 0.05f;

        [Header("Sóng uốn lượn trên xúc tu (tới tận ngọn)")]
        [Tooltip("Biên độ sóng khi di chuyển (độ mỗi xương ở gốc; ngọn mạnh hơn theo Wave Tip Boost)")]
        [SerializeField, Range(0f, 20f)] float waveAmount = 9f;
        [Tooltip("Ngọn uốn mạnh gấp bao nhiêu lần gốc")]
        [SerializeField, Range(1f, 5f)] float waveTipBoost = 1.8f;
        [Tooltip("Số xương trên một bước sóng (nhỏ = sóng dày, gợn nhiều)")]
        [SerializeField, Range(3f, 20f)] float waveLength = 9f;
        [Tooltip("Tốc độ sóng chạy từ gốc ra ngọn")]
        [SerializeField, Range(0f, 20f)] float waveSpeed = 5f;
        [Tooltip("Đứng yên thì sóng bằng bao nhiêu phần lúc di chuyển")]
        [SerializeField, Range(0f, 1f)] float waveIdle = 0.45f;

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
        [Tooltip("Trườn = xúc tu sát đất uốn sóng như rắn, thân lướt êm. Bước = nhấc từng xúc tu lên đặt xuống như đi bộ")]
        [SerializeField] CrawlStyle crawlStyle = CrawlStyle.Slither;
        [Tooltip("Kiểu Trườn: 0 xúc tu trái (duỗi ra sau) · 1 giữa · 2 phải (vươn ra trước)")]
        [SerializeField] ArmSlither[] slither = DefaultSlither();
        [Tooltip("Kiểu Bước: 0 xúc tu trái = đẩy phía sau · 1 giữa = lăn dưới thân · 2 phải = vươn tới kéo phía trước")]
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
        [SerializeField, Range(0f, 0.4f)] float idleCrouch = 0.22f;
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

        [Header("Bám đá")]
        [Tooltip("Hai xúc tu bên nắm đá cách giữa đầu bấy nhiêu (unit tranh)")]
        [SerializeField] float gripSpread = 0.32f;
        [Tooltip("Thân bò ra xa điểm nắm quá mức này (unit) thì xúc tu buông ra nắm chỗ mới")]
        [SerializeField] float regripDistance = 0.4f;
        [SerializeField] float regripTime = 0.14f;

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

        [Header("Co giãn (squash & stretch)")]
        [SerializeField] float squashStiffness = 260f;
        [SerializeField] float squashDamping = 13f;
        [SerializeField, Range(0f, 1f)] float jumpStretch = 0.3f;
        [SerializeField, Range(0f, 1f)] float landSquash = 0.35f;
        [SerializeField, Range(0f, 1f)] float grabStretch = 0.15f;
        [SerializeField, Range(0f, 1f)] float releaseStretch = 0.2f;

        [Header("Vệt tốc độ")]
        [SerializeField] float speedTrailThreshold = 12f;

        [Header("Kích thước tranh (dựng tự động từ rig.json)")]
        [Tooltip("Tâm vật lý cao hơn mặt đất bấy nhiêu (bán kính collider, unit world)")]
        [SerializeField] float groundOffset = 0.5f;
        [Tooltip("Tâm thân cao bấy nhiêu tính từ chân (unit tranh; điểm xoay khi ở trên không)")]
        [SerializeField] float bodyCenter = 0.75f;
        [Tooltip("Đỉnh đầu cao bấy nhiêu tính từ chân (unit tranh; chỗ áp vào đá khi bám)")]
        [SerializeField] float headTop = 1.45f;

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
        OctopusState _lastState;
        float _previewDelta;
        float _travel;
        float _wavePhase;
        float _editorPhase = -1f;
        float _time;
        float _squash;
        float _squashVelocity;
        float _walkPhase;
        float _walkWeight;
        float _crawlDirection = 1f;
        float _groundBlend = 1f;
        float _clingBlend;
        Vector2 _clingOffset;
        float _angle;
        float _flip = 1f;
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
            new ArmSlither { phase = 0.5f, reach = -0.15f, slide = 0.07f, curl = 0.75f, curlSwing = 0.15f },
            new ArmSlither { phase = 0.25f, reach = -0.08f, slide = 0.05f, curl = 0.9f, curlSwing = 0.2f },
            new ArmSlither { phase = 0f, reach = 0.2f, slide = 0.08f, curl = 0.7f, curlSwing = 0.2f },
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
        public Vector2 FootPosition(int i) => torso.TransformPoint(Foot(limbs[i], limbs[i].angles, limbs[i].spacing));

        /// <summary>Xúc tu i đang bám đất (chóp đứng yên trong khi thân trôi qua).</summary>
        public bool FootGripping(int i) => limbs[i].footGripping;

        /// <summary>Chóp xúc tu i, toạ độ world.</summary>
        public Vector2 TipPosition(int i) => JointWorld(limbs[i], limbs[i].Count);

        /// <summary>Đỉnh đầu (cuối xương đầu trên cùng), toạ độ world.</summary>
        public Vector2 HeadTipPosition => JointWorld(mantle, mantle.Count);

        /// <summary>Đang nhắm mắt (chớp).</summary>
        public bool EyeClosed => eye != null && eye.localScale.y < 0.5f;

        /// <summary>Editor gán: cửa sổ Animation đang xem trước / ghi clip (AnimationMode). Khi đó tay cầm do cửa sổ Animation đặt.</summary>
        public static Func<bool> EditorAnimationModeActive;

        /// <summary>Đang dùng clip bò (Crawl.anim) và đủ tay cầm.</summary>
        public bool UsesCrawlClip
        {
            get
            {
                if (!useCrawlClip || crawlBody == null || crawlFeet == null || crawlFeet.Length != 3)
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
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (!_subscribed || controller == null)
                return;

            controller.StateChanged -= OnStateChanged;
            controller.Jumped -= OnJumped;
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
            bool animationWindow = !playing && UsesCrawlClip && EditorAnimationModeActive != null && EditorAnimationModeActive();
            bool crawlPanel = !playing && !live && UsesCrawlClip && _editorPhase >= 0f;
            Snapshot snap = playing ? Capture() : PreviewSnapshot(animationWindow || crawlPanel);
            _mode = snap.Mode;
            _time = live ? _time + dt : previewTime * 6f;

            UpdateTimers(dt);
            UpdateSquash(dt);
            UpdateBlend(snap, dt, live);
            if (snap.Mode == Mode.Ground && UsesCrawlClip && !animationWindow && crawlCurves != null)
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
                for (int i = 0; i < limbs.Length; i++)
                {
                    Limb limb = limbs[i];
                    limb.footGripping = false;
                    UpdateWave(limb, i, motion);
                    switch (snap.Mode)
                    {
                        case Mode.Ground:
                            limb.reaching = false;
                            limb.planted = live ? Mathf.MoveTowards(limb.planted, 1f, dt / Mathf.Max(plantTime, 0.01f)) : 1f;
                            GroundTargets(limb, i, torsoOffset, dt, live);
                            break;

                        case Mode.Swing when i == GrabLimb:
                            ReachTargets(limb, snap.Anchor, dt, shootDuration, holdCurl);
                            break;

                        case Mode.Cling when i != GrabLimb:
                            ClingTargets(limb, snap, dt);
                            break;

                        default:
                            limb.reaching = false;
                            AirTargets(limb, i, snap.Mode == Mode.Cling ? Vector2.zero : artVelocity);
                            break;
                    }

                    if (snap.Mode != Mode.Cling)
                        limb.gripping = false;
                    if (snap.Mode != Mode.Ground)
                        limb.planted = 0f;

                    Integrate(limb, dt, !live, rootStiffness, tipStiffness, damping);
                    Apply(limb);
                }

                MantleTargets(snap, artVelocity);
                Integrate(mantle, dt, !live, mantleStiffness, mantleStiffness * 0.8f, mantleDamping);
                Apply(mantle);
                float breath = Mathf.Sin(_time * breathSpeed) * breathAmount * (snap.Mode == Mode.Ground ? 1f : 0.4f);
                float flat = snap.Mode == Mode.Ground ? crawlFlatten * _walkWeight : 0f;
                mantle.bones[Mathf.Min(1, mantle.Count - 1)].localScale =
                    new Vector3(1f + breath * 0.6f + flat * 0.5f, 1f + breath - flat, 1f);
                ApplyFins(motion);
            }

            UpdateEye(snap, dt, live);

            if (glow != null)
                glow.transform.position = snap.Position + (Vector2)(pose.rotation * Vector2.up) * (bodyCenter * size - groundOffset);
            if (speedTrail != null)
            {
                speedTrail.transform.position = snap.Position;
                speedTrail.emitting = playing && snap.Velocity.magnitude > speedTrailThreshold &&
                                      snap.Mode is Mode.Swing or Mode.Air;
            }

            if (snap.Mode == Mode.Air)
                _fallSpeed = Mathf.Max(_fallSpeed, -snap.Velocity.y);
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
                    snap.Velocity = Vector2.zero;
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
                    snap.Mode = Mode.Cling;
                    snap.ClingNormal = Vector2.down;
                    snap.ClingPoint = snap.Position + Vector2.up * groundOffset;
                    break;
                default:
                    snap.Mode = Mode.Ground;
                    break;
            }

            return snap;
        }

        void UpdateBlend(Snapshot snap, float dt, bool live)
        {
            float ground = snap.Mode == Mode.Ground ? 1f : 0f;
            float cling = snap.Mode == Mode.Cling ? 1f : 0f;
            _groundBlend = live ? Mathf.MoveTowards(_groundBlend, ground, dt * 10f) : ground;
            _clingBlend = live ? Mathf.MoveTowards(_clingBlend, cling, dt * 8f) : cling;
            if (snap.Mode == Mode.Cling)
                _clingOffset = snap.ClingPoint - snap.Position;

            // Tốc độ bò tính theo unit tranh, dấu theo hướng mặt: bò lùi thì nhịp chạy ngược, chân vẫn không trượt
            float worldSpeed = snap.Mode == Mode.Ground ? snap.Velocity.x : 0f;
            float artSpeed = worldSpeed * Mathf.Sign(snap.Facing) / size;
            float walk = Mathf.Abs(worldSpeed) > crawlThreshold ? 1f : 0f;
            _walkWeight = live ? Mathf.MoveTowards(_walkWeight, walk, dt * 6f) : walk;
            if (Mathf.Abs(artSpeed) > 0.01f)
                _crawlDirection = Mathf.Sign(artSpeed);
            _walkPhase = live ? Mathf.Repeat(_walkPhase + artSpeed * dt / cycleDistance, 1f) : _editorPhase >= 0f ? _editorPhase : previewTime;
            if (live)
                _travel += artSpeed * dt;
        }

        ArmGait Gait(int index) => gait != null && gait.Length > 0 ? gait[Mathf.Min(index, gait.Length - 1)] : DefaultGait()[index];

        // ---------- Thân: vị trí, xoay, co giãn ----------

        Vector2 TorsoOffset(Snapshot snap)
        {
            var offset = Vector2.zero;
            if (snap.Mode != Mode.Ground)
                return offset;

            if (UsesCrawlClip)
            {
                // Tay cầm thân trong clip: chồm tới / lùi lại, nhấp nhô
                offset = (Vector2)crawlBody.transform.localPosition * _walkWeight;
            }
            else
            {
                BodyCrawl(_walkPhase, out Vector2 body, out _);
                offset = body * _walkWeight;
            }

            // Đầu hạ thấp lúc đứng, thấp thêm lúc bò
            offset.y -= idleCrouch + crawlCrouch * _walkWeight;
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

            // Chỉ quay mặt khi đứng / bám (đang bay hay đu mà lật thì trông như tấm giấy lật giữa trời)
            if (!live)
                _flip = snap.Facing;
            else if (snap.Mode is Mode.Ground or Mode.Cling)
                _flip = Mathf.MoveTowards(_flip, snap.Facing, dt * 2f / Mathf.Max(turnTime, 0.01f));

            // Co giãn giữ thể tích: dài ra thì hẹp lại; lật trái / phải bằng scale âm
            float stretch = 1f + Mathf.Max(-0.5f, _squash);
            float flip = Mathf.Sign(_flip) * Mathf.Max(Mathf.Abs(_flip), 0.35f);
            var scale = new Vector2(flip / Mathf.Sqrt(stretch), stretch) * size;

            // Điểm neo q (unit tranh) đặt vào vị trí at (unit world, tính từ tâm vật lý):
            // chân khi đứng (bẹp xuống mặt đất), tâm thân khi bay / đu, đỉnh đầu khi bám đá
            var feet = new Vector2(0f, -groundOffset);
            Vector2 q = Vector2.Lerp(new Vector2(0f, bodyCenter), Vector2.zero, _groundBlend);
            Vector2 at = feet + q * size;
            q = Vector2.Lerp(q, new Vector2(0f, headTop), _clingBlend);
            at = Vector2.Lerp(at, _clingOffset, _clingBlend);

            Quaternion rotation = Quaternion.Euler(0f, 0f, _angle);
            pose.localPosition = at - (Vector2)(rotation * Vector2.Scale(scale, q));
            pose.localRotation = rotation;
            pose.localScale = new Vector3(scale.x, scale.y, 1f);
        }

        // Vector world → hướng trong tranh (bỏ xoay và lật của Pose), giữ độ lớn
        Vector2 ToArt(Vector2 world)
        {
            Vector2 art = Quaternion.Euler(0f, 0f, -_angle) * world;
            art.x *= Mathf.Sign(_flip);
            return art;
        }

        // ---------- Xúc tu: tính góc đích ----------

        // Trên đất: chóp mỗi xúc tu theo tay cầm của clip bò (hoặc nhịp ArmGait khi bò bằng code); giải IK cho điểm chạm đất
        void GroundTargets(Limb limb, int index, Vector2 torsoOffset, float dt, bool live)
        {
            float idle = 1f - _walkWeight;
            float restCurl = 1f + idleCurl * Mathf.Sin(_time * 0.8f + index * 2.1f);
            Vector2 foot;
            float curl;
            bool gripping;
            if (UsesCrawlClip)
            {
                OctopusCrawlFoot handle = crawlFeet[index];
                foot = Vector2.Lerp(RestFoot(limb), handle.transform.localPosition, _walkWeight);
                curl = Mathf.Lerp(restCurl, handle.curl, _walkWeight);
                gripping = handle.contact >= 0.5f;
                foot.x = FootLock(limb, foot.x, footLock && live && gripping && _walkWeight > 0.5f, dt);
            }
            else
            {
                CrawlStep(index, _walkPhase, out Vector2 step, out curl, out gripping);
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

        // Bò bằng code, xúc tu index tại pha chung w (0..1): chóp lệch khỏi chỗ nghỉ bao nhiêu, cuộn bao nhiêu, có bám không
        void CrawlStep(int index, float w, out Vector2 step, out float curl, out bool gripping)
        {
            if (crawlStyle == CrawlStyle.Slither)
            {
                // Trườn: chóp nằm sát đất, chỉ trượt nhẹ tới lui; xúc tu uốn như rắn nhờ sóng
                ArmSlither a = Slither(index);
                float angle = 2f * Mathf.PI * Mathf.Repeat(w + a.phase, 1f);
                step = new Vector2(a.reach + a.slide * Mathf.Sin(angle), 0f);
                curl = a.curl + a.curlSwing * Mathf.Sin(angle + 1.3f);
                gripping = false;
                return;
            }

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
                    BodyCrawl(_walkPhase, out _, out float lean);
                    sway += _walkWeight * _crawlDirection * (UsesCrawlClip ? crawlBody.mantle : lean);
                    break;
                case Mode.Air:
                case Mode.Swing:
                    sway += mantleDrag * Mathf.Clamp(artVelocity.x / 12f, -1f, 1f);
                    break;
            }

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

        void ClingTargets(Limb limb, Snapshot snap, float dt)
        {
            // Điểm nắm mong muốn: hai bên đỉnh đầu, chiếu lên mặt đá
            Vector2 desired = pose.TransformPoint(new Vector2(limb.side * -gripSpread, headTop + 0.05f));
            if (snap.ClingSurface != null)
                desired = snap.ClingSurface.ClosestPoint(desired);

            if (!limb.gripping)
            {
                limb.gripping = true;
                limb.grip = desired;
                limb.reaching = false;
            }
            else if (Vector2.Distance(limb.grip, desired) > regripDistance)
            {
                // Buông chỗ cũ, với tới chỗ mới hơi vượt lên trước theo hướng đang bò
                Vector2 lead = (desired - limb.grip).normalized * regripDistance * 0.5f;
                limb.grip = snap.ClingSurface != null ? snap.ClingSurface.ClosestPoint(desired + lead) : desired + lead;
                limb.reaching = false;
            }

            ReachTargets(limb, limb.grip, dt, regripTime, holdCurl);
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
                    return _walkWeight;
                case Mode.Swing:
                    return 1f;
                case Mode.Cling:
                    return 0.5f;
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
                if (i <= limb.footBone && limb.planted > 0f)
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
            if (limb.angles != null && limb.angles.Length == n)
                return;

            limb.angles = (float[])limb.bindAngles.Clone();
            limb.targets = (float[])limb.bindAngles.Clone();
            limb.velocities = new float[n];
            limb.spacing = (float[])limb.bindLengths.Clone();
            limb.spacingTargets = (float[])limb.bindLengths.Clone();
            limb.wave = new float[n];
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

            eye.localScale = new Vector3(1f, open, 1f);

            // Con ngươi: nhìn về phía đá khi đu / bám, liếc theo hướng đi khi chạy nhảy, thỉnh thoảng nhìn quanh khi đứng
            Vector2 look;
            switch (snap.Mode)
            {
                case Mode.Swing:
                    look = ToArt(snap.Anchor - snap.Position).normalized;
                    break;
                case Mode.Cling:
                    look = new Vector2(0.4f, 0.8f);
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
                    CrawlStep(index, w, out Vector2 step, out _, out _);
                    return step;
                }

                float Curl(float w)
                {
                    CrawlStep(index, w, out _, out float curl, out _);
                    return curl;
                }

                if (crawlStyle == CrawlStyle.Slither)
                {
                    // Trườn: chóp trượt tới lui êm (6 keyframe mỗi vòng, 10 frame một cái), luôn sát đất, không khoá chân
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

                if (_mode == Mode.Ground)
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
