using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Hoạt ảnh thủ tục cho bạch tuộc 3 xúc tu: thân hình giọt nước đuôi nhọn, một mắt biết chớp và nhìn theo hướng đi.
    /// Không dùng clip vẽ sẵn: mỗi frame đọc chuyển động → chọn tư thế → chuyển mượt → vẽ bằng LineRenderer.
    ///
    ///  Nghỉ    — ba xúc tu tựa xuống đất, thân thở phồng / xẹp, mắt liếc quanh.
    ///  Bò      — dáng đi 3 chân: mỗi xúc tu bám đất đứng yên rồi nhấc lên bước tới theo vòng cung,
    ///            lệch nhau 1/3 chu kỳ; thân nhấp nhô theo nhịp bước và chúi về phía trước.
    ///  Nhảy    — bật lên thì thân dãn dài, xúc tu chụm vươn lên; tới đỉnh xoè rộng như chiếc ô;
    ///            rơi xuống thì xúc tu bị gió hất ngược lên; chạm đất thì thân bẹp xuống rồi nảy lại (lò xo).
    ///  Đu      — đầu hướng về đá, xúc tu trước nắm đá, hai xúc tu kia quất theo quán tính phía sau.
    ///  Bay     — thân duỗi theo hướng bay, ba xúc tu chụm phía trước, vệt tốc độ sau đuôi.
    ///  Bám đá  — hai xúc tu bám đá, xúc tu giữa đung đưa, thân lủng lẳng.
    ///
    /// Chạy cả khi không Play: chọn "Preview Pose" để xem và chỉnh từng tư thế ngay trong Scene (Cmd+S để lưu).
    /// </summary>
    [ExecuteAlways]
    public class OctopusAnimator : MonoBehaviour
    {
        public enum PreviewPose
        {
            [InspectorName("Nghỉ")] Idle,
            [InspectorName("Bò")] Crawl,
            [InspectorName("Nhảy - bật lên")] JumpRise,
            [InspectorName("Nhảy - đỉnh")] JumpApex,
            [InspectorName("Nhảy - rơi")] JumpFall,
            [InspectorName("Đu")] Swing,
            [InspectorName("Bay")] Fly,
            [InspectorName("Bám đá")] Cling,
        }

        enum AirMode
        {
            Jump,
            Fly,
        }

        enum LimbMode
        {
            Reach,
            Grab,
            Foot,
        }

        [Header("Xem trước (khi không Play)")]
        [Tooltip("Chọn tư thế để xem và chỉnh ngay trong Scene, không cần bấm Play")]
        [SerializeField] PreviewPose previewPose = PreviewPose.Idle;
        [Tooltip("Thời điểm trong chu kỳ bò khi xem trước tư thế Bò (kéo để xem từng nhịp bước)")]
        [SerializeField, Range(0f, 1f)] float previewCrawlPhase = 0.15f;

        [Header("Ngoại hình")]
        [SerializeField] Color bodyColor = new Color(0.97f, 0.62f, 0.78f);
        [Tooltip("Màu viền, đuôi nhọn và giác hút đầu xúc tu")]
        [SerializeField] Color inkColor = new Color(0.09f, 0.04f, 0.14f);
        [Tooltip("Quầng sáng mờ quanh thân, giúp nổi bật trên nền hang tối")]
        [SerializeField] Color glowColor = new Color(1f, 0.55f, 0.85f, 0.22f);
        [Tooltip("Độ béo của thân: trục ngang từ đầu (0) tới đuôi (1)")]
        [SerializeField] AnimationCurve bodyWidth = new AnimationCurve(
            new Keyframe(0f, 0.66f), new Keyframe(0.2f, 0.84f), new Keyframe(0.55f, 0.52f),
            new Keyframe(0.85f, 0.18f), new Keyframe(1f, 0.02f));
        [SerializeField, Range(0f, 0.3f)] float outlineThickness = 0.09f;
        [Tooltip("Từ đâu dọc thân thì bắt đầu chuyển sang màu mực ở đuôi: 0 = đầu, 1 = đuôi")]
        [SerializeField, Range(0f, 1f)] float tailInkStart = 0.8f;
        [Tooltip("Độ to của xúc tu: trục ngang từ gốc (0) tới đầu mút (1)")]
        [SerializeField] AnimationCurve tentacleWidth = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.6f, 0.17f), new Keyframe(1f, 0.07f));
        [Tooltip("Khoảng cách giữa các giác hút dọc xúc tu (unit)")]
        [SerializeField, Min(0.02f)] float suckerSpacing = 0.15f;
        [SerializeField, Range(0.02f, 1f)] float tipSize = 0.12f;
        [Tooltip("Màu đầu mút xúc tu: 0 = màu thân, 1 = màu mực")]
        [SerializeField, Range(0f, 1f)] float tipInk = 0.55f;
        [SerializeField, Range(0.2f, 3f)] float eyeSize = 1f;
        [SerializeField, Range(0f, 8f)] float glowSize = 3.4f;
        [SerializeField] Color trailColor = new Color(1f, 0.7f, 0.9f, 0.45f);

        [Header("Dáng thân")]
        [SerializeField, Min(0.1f)] float bodyLength = 1.6f;
        [SerializeField, Range(4, 32)] int bodySegments = 18;
        [Tooltip("Thân dài ra tối đa bao nhiêu lần khi bay nhanh")]
        [SerializeField, Range(1f, 2f)] float maxStretch = 1.3f;
        [SerializeField] float speedForMaxStretch = 22f;
        [Tooltip("Độ cong của thân khi nghỉ (tư thế cuộn)")]
        [SerializeField, Range(0f, 1f)] float curlBend = 0.4f;
        [Tooltip("Độ cong tối đa khi đu: thân trễ lại theo quán tính")]
        [SerializeField, Range(0f, 1f)] float swingBend = 0.3f;
        [Tooltip("Vị trí mắt dọc thân: 0 = đầu, 1 = đuôi")]
        [SerializeField, Range(0f, 1f)] float eyeAlongBody = 0.27f;
        [Tooltip("Tốc độ chuyển giữa các tư thế; lớn thì dứt khoát, nhỏ thì mềm")]
        [SerializeField] float poseSmoothing = 10f;
        [Tooltip("Bay nhanh hơn mức này (sau khi buông xúc tu) thì chuyển sang tư thế bay")]
        [SerializeField] float flyPoseSpeed = 6f;
        [Tooltip("Thở khi đứng yên: thân phồng / xẹp nhẹ")]
        [SerializeField, Range(0f, 0.2f)] float breathAmount = 0.045f;
        [SerializeField] float breathSpeed = 2.4f;

        [Header("Co giãn (squash & stretch)")]
        [Tooltip("Độ cứng lò xo: lớn thì nảy nhanh")]
        [SerializeField] float squashStiffness = 260f;
        [Tooltip("Giảm chấn: nhỏ thì rung lâu")]
        [SerializeField] float squashDamping = 13f;
        [Tooltip("Thân dãn dài khi bật nhảy")]
        [SerializeField, Range(0f, 1f)] float jumpStretch = 0.4f;
        [Tooltip("Thân bẹp xuống khi chạm đất (tối đa, khi rơi mạnh)")]
        [SerializeField, Range(0f, 1f)] float landSquash = 0.45f;
        [SerializeField, Range(0f, 1f)] float grabStretch = 0.2f;
        [SerializeField, Range(0f, 1f)] float releaseStretch = 0.25f;

        [Header("Xúc tu")]
        [SerializeField] float tentacleLength = 1.6f;
        [Tooltip("Khoảng cách giữa gốc các xúc tu")]
        [SerializeField] float shoulderWidth = 0.2f;
        [Tooltip("Độ uốn lượn của xúc tu (độ)")]
        [SerializeField] float waveAngle = 22f;
        [SerializeField] float waveSpeed = 5f;
        [Tooltip("Đầu mút xúc tu cuộn lại bao nhiêu độ")]
        [SerializeField] float tipCurl = 100f;
        [Tooltip("Thời gian (giây) xúc tu phóng tới chỗ bám mới")]
        [SerializeField] float shootDuration = 0.08f;
        [Tooltip("Góc xoè của hai xúc tu tự do khi đang đu (độ so với hướng dây)")]
        [SerializeField] float swingFreeTentacleAngle = 60f;

        [Header("Bò")]
        [Tooltip("Những layer tính là mặt đất để đặt xúc tu")]
        [SerializeField] LayerMask groundMask = ~0;
        [Tooltip("Khoảng cách từ tâm bạch tuộc xuống mặt đất")]
        [SerializeField] float groundDepth = 0.5f;
        [Tooltip("Độ dài một bước của xúc tu")]
        [SerializeField] float strideLength = 0.75f;
        [Tooltip("Tỉ lệ thời gian xúc tu bám đất trong một bước; phần còn lại là nhấc lên bước tới")]
        [SerializeField, Range(0.3f, 0.9f)] float stanceRatio = 0.62f;
        [Tooltip("Xúc tu nhấc cao bao nhiêu khi bước")]
        [SerializeField] float stepHeight = 0.32f;
        [Tooltip("Thân nhấp nhô theo nhịp bước")]
        [SerializeField] float bodyBob = 0.06f;
        [Tooltip("Thân lắc nhẹ theo nhịp bước (độ)")]
        [SerializeField] float crawlSway = 4f;
        [Tooltip("Bò nhanh cỡ này thì dáng bò đầy đủ")]
        [SerializeField] float crawlSpeedReference = 3.5f;
        [Tooltip("Chỗ đặt 3 xúc tu khi đứng yên, tính từ đầu theo hướng mặt: trước / giữa / sau")]
        [SerializeField] float[] footHomes = { 0.6f, 0.1f, -0.5f };

        [Header("Nhảy")]
        [Tooltip("Thân ngóc đầu lên khi bật / chúi xuống khi rơi (độ)")]
        [SerializeField] float jumpPitch = 35f;
        [Tooltip("Lúc bật lên: xúc tu đạp về phía sau (độ so với phương thẳng đứng)")]
        [SerializeField] float riseKickAngle = 35f;
        [Tooltip("Góc xoè xúc tu khi đang bật lên")]
        [SerializeField] float riseSpread = 12f;
        [Tooltip("Góc xoè xúc tu ở đỉnh cú nhảy (như chiếc ô)")]
        [SerializeField] float apexSpread = 60f;
        [Tooltip("Góc xoè xúc tu khi rơi xuống, chĩa xuống đón đất")]
        [SerializeField] float fallSpread = 30f;

        [Header("Mắt")]
        [Tooltip("Chớp mắt ngẫu nhiên mỗi bấy nhiêu giây (nhỏ nhất, lớn nhất)")]
        [SerializeField] Vector2 blinkInterval = new Vector2(2.2f, 5f);
        [SerializeField] float blinkDuration = 0.14f;
        [Tooltip("Con ngươi di chuyển tối đa bao xa trong lòng mắt (unit)")]
        [SerializeField] float pupilTravel = 0.07f;
        [SerializeField] float lookSmoothing = 8f;

        [Header("Vệt tốc độ")]
        [SerializeField] float speedTrailThreshold = 12f;

        [Header("Tham chiếu (đã nối sẵn, không cần sửa)")]
        [SerializeField] OctopusController controller;
        [SerializeField] LineRenderer body;
        [SerializeField] LineRenderer bodyOutline;
        [SerializeField] Transform eye;
        [SerializeField] Transform pupil;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] TrailRenderer speedTrail;
        [SerializeField] Tentacle[] tentacles = { new Tentacle(), new Tentacle(), new Tentacle() };

        Rigidbody2D _body;
        Vector3[] _bodyPoints;
        Vector2 _axis = Vector2.right;
        Vector2 _look = Vector2.right;
        float _bend;
        float _stretch = 1f;
        float _squash;
        float _squashVelocity;
        float _crawlPhase;
        float _crawlWeight;
        float _blinkTimer = 3f;
        float _blinkLeft;
        AirMode _airMode;
        OctopusState _lastState;
        float _fallSpeed; // tốc độ rơi lớn nhất trong lần ở trên không gần nhất, để tính độ bẹp khi tiếp đất
        bool _lookDirty = true;
        bool _subscribed;

        /// <summary>Độ co giãn hiện tại: dương = đang dãn dài, âm = đang bẹp.</summary>
        public float Squash => _squash;

        public int TentacleCount => tentacles.Length;

        // Những gì hoạt ảnh cần biết về chuyển động; lúc Play đọc từ controller, lúc không Play lấy từ tư thế xem trước
        struct Motion
        {
            public OctopusState State;
            public AirMode Air;
            public Vector2 Velocity;
            public float Facing;
            public Vector2 Anchor;
            public Vector2 ClingPoint;
            public Vector2 ClingNormal;
        }

        struct Limb
        {
            public LimbMode Mode;
            public Vector2 Target;
            public Vector2 Direction;
            public float Length;
            public Vector2 Arch;
            public float Wave;
            public float Curl; // hệ số cuộn đầu mút, có dấu: + cuộn ngược chiều kim đồng hồ

            public static Limb GrabAt(Vector2 target) => new Limb { Mode = LimbMode.Grab, Target = target };

            public static Limb ReachTo(Vector2 direction, float length, float curl, float wave = 1f) =>
                new Limb { Mode = LimbMode.Reach, Direction = direction, Length = length, Curl = curl, Wave = wave };

            public static Limb FootAt(Vector2 foot, Vector2 arch) => new Limb { Mode = LimbMode.Foot, Target = foot, Arch = arch };
        }

        struct Pose
        {
            public Vector2 Axis; // hướng từ đuôi lên đầu
            public float Bend;   // dương = cong sang trái của trục
            public float Stretch;
            public float HeadLift;
            public Vector2 Look;
            public Limb A;
            public Limb B;
            public Limb C;

            public Limb this[int index] => index == 0 ? A : index == 1 ? B : C;
        }

        bool HasReferences
        {
            get
            {
                if (controller == null || body == null || bodyOutline == null || eye == null || pupil == null ||
                    glow == null || speedTrail == null || tentacles == null || tentacles.Length < 3)
                    return false;
                foreach (Tentacle tentacle in tentacles)
                    if (tentacle == null || !tentacle.IsValid)
                        return false;
                return true;
            }
        }

        void OnEnable()
        {
            _bodyPoints = null; // dựng lại ở frame kế tiếp, kể cả sau khi Unity compile lại code
            _lookDirty = true;
        }

        void Start()
        {
            if (!Application.isPlaying || controller == null)
                return;

            controller.StateChanged += OnStateChanged;
            controller.Jumped += OnJumped;
            controller.Swing.Attached += OnAttached;
            controller.Swing.Released += OnReleased;
            _subscribed = true;
            _lastState = controller.State;
            _blinkTimer = UnityEngine.Random.Range(blinkInterval.x, blinkInterval.y);
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

        void OnValidate()
        {
            _lookDirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); // vẽ lại Scene ngay khi chỉnh Inspector
#endif
        }

        void LateUpdate()
        {
            if (!HasReferences)
                return;

            bool playing = Application.isPlaying;
            float dt = playing ? Mathf.Min(Time.deltaTime, 0.05f) : 1f;
            float time = playing ? Time.time : 0.37f;

            Vector2 head = controller.transform.position;
            EnsureInitialized(head);
            if (_lookDirty)
            {
                ApplyLook();
                _lookDirty = false;
            }

            Motion motion = playing ? ReadMotion() : PreviewMotion(head);
            if (!playing && motion.State == OctopusState.Grounded)
                head.y = GroundY(head) + groundDepth; // xem trước: đặt bạch tuộc đứng đúng trên mặt sàn

            if (playing)
            {
                UpdateSquash(dt);
                UpdateBlink(dt);
            }
            else
            {
                _squash = _squashVelocity = _blinkLeft = 0f;
            }

            UpdateCrawl(motion, dt, playing);
            Pose pose = ComputePose(head, motion, time);

            float blend = playing ? 1f - Mathf.Exp(-poseSmoothing * dt) : 1f;
            _axis = Curves.RotateTowards(_axis, pose.Axis, blend);
            _bend = Mathf.Lerp(_bend, pose.Bend, blend);
            _stretch = Mathf.Lerp(_stretch, pose.Stretch, blend);
            _look = Curves.RotateTowards(_look, pose.Look, playing ? 1f - Mathf.Exp(-lookSmoothing * dt) : 1f);

            head += Vector2.up * pose.HeadLift;
            float squash = Mathf.Max(-0.6f, _squash);
            var side = new Vector2(-_axis.y, _axis.x);
            Vector2 tail = DrawBody(head, side, _stretch * (1f + squash), 1f / Mathf.Sqrt(1f + squash));

            float waveTime = time * waveSpeed;
            for (int i = 0; i < 3; i++)
            {
                // Gốc 3 xúc tu xếp quanh đầu: một bên, chính giữa (nhô lên một chút), bên kia
                Vector2 root = head + side * (shoulderWidth * (1 - i)) + _axis * (i == 1 ? 0.06f : 0f);
                Animate(tentacles[i], root, pose[i], waveTime + i * 2.1f, dt);
            }

            speedTrail.emitting = playing && motion.Velocity.magnitude > speedTrailThreshold &&
                                  (motion.State == OctopusState.Swinging ||
                                   (motion.State == OctopusState.Airborne && motion.Air == AirMode.Fly));
            speedTrail.transform.position = tail;
            if (motion.State == OctopusState.Airborne)
                _fallSpeed = Mathf.Max(_fallSpeed, -motion.Velocity.y);
        }

        void EnsureInitialized(Vector2 head)
        {
            if (_bodyPoints == null || _bodyPoints.Length != bodySegments)
            {
                _body = controller.GetComponent<Rigidbody2D>();
                _bodyPoints = new Vector3[bodySegments];
                body.useWorldSpace = bodyOutline.useWorldSpace = true;
                body.positionCount = bodyOutline.positionCount = bodySegments;
                _lookDirty = true;
            }

            foreach (Tentacle tentacle in tentacles)
                if (tentacle.NeedsInit)
                    tentacle.Init(head);
        }

        // Đổ màu, độ dày, kích thước từ các ô "Ngoại hình" xuống từng bộ phận
        void ApplyLook()
        {
            body.widthCurve = bodyWidth;
            body.colorGradient = Curves.TwoTone(bodyColor, inkColor, tailInkStart);
            bodyOutline.widthCurve = Curves.Thicker(bodyWidth, outlineThickness);
            bodyOutline.colorGradient = Curves.TwoTone(inkColor, inkColor, 1f);
            glow.color = glowColor;
            speedTrail.colorGradient = Curves.Fade(trailColor);
            Color tipColor = Color.Lerp(bodyColor, inkColor, tipInk);
            foreach (Tentacle tentacle in tentacles)
                tentacle.ApplyLook(tentacleWidth, bodyColor, tipColor, tipSize, 1f / suckerSpacing);
        }

        // ---------- Đọc chuyển động ----------

        Motion ReadMotion() => new Motion
        {
            State = controller.State,
            Air = _airMode,
            Velocity = _body.linearVelocity,
            Facing = controller.Facing,
            Anchor = controller.Swing.AnchorPoint,
            ClingPoint = controller.Mover.ClingPoint,
            ClingNormal = controller.Mover.ClingNormal,
        };

        Motion PreviewMotion(Vector2 head)
        {
            var motion = new Motion { State = OctopusState.Grounded, Air = AirMode.Jump, Facing = 1f };
            switch (previewPose)
            {
                case PreviewPose.Crawl:
                    motion.Velocity = new Vector2(crawlSpeedReference, 0f);
                    break;
                case PreviewPose.JumpRise:
                    motion.State = OctopusState.Airborne;
                    motion.Velocity = new Vector2(2f, 9f);
                    break;
                case PreviewPose.JumpApex:
                    motion.State = OctopusState.Airborne;
                    motion.Velocity = new Vector2(2f, 0.3f);
                    break;
                case PreviewPose.JumpFall:
                    motion.State = OctopusState.Airborne;
                    motion.Velocity = new Vector2(2f, -9f);
                    break;
                case PreviewPose.Swing:
                    motion.State = OctopusState.Swinging;
                    motion.Velocity = new Vector2(8f, -1f);
                    motion.Anchor = head + new Vector2(-1.5f, 4.5f);
                    break;
                case PreviewPose.Fly:
                    motion.State = OctopusState.Airborne;
                    motion.Air = AirMode.Fly;
                    motion.Velocity = new Vector2(14f, 6f);
                    break;
                case PreviewPose.Cling:
                    motion.State = OctopusState.Clinging;
                    motion.ClingPoint = head + Vector2.up * 0.5f;
                    motion.ClingNormal = Vector2.down;
                    break;
            }

            return motion;
        }

        // ---------- Sự kiện → co giãn lò xo ----------

        void OnJumped()
        {
            _airMode = AirMode.Jump;
            Kick(jumpStretch);
        }

        void OnAttached(Vector2 anchor)
        {
            Kick(grabStretch);
        }

        void OnReleased()
        {
            _airMode = AirMode.Fly;
            Kick(releaseStretch);
        }

        void OnStateChanged(OctopusState state)
        {
            if (state == OctopusState.Grounded && _lastState == OctopusState.Airborne)
            {
                float impact = Mathf.Clamp01(_fallSpeed / 18f);
                Kick(-landSquash * (0.35f + 0.65f * impact));
            }
            else if (state == OctopusState.Clinging)
            {
                Kick(-0.2f);
            }

            if (state == OctopusState.Grounded)
                _airMode = AirMode.Jump;
            if (state != OctopusState.Airborne)
                _fallSpeed = 0f;
            _lastState = state;
        }

        // Đẩy lò xo: biên độ nảy lên xấp xỉ amount
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

        void UpdateBlink(float dt)
        {
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blinkLeft = blinkDuration;
                _blinkTimer = UnityEngine.Random.Range(blinkInterval.x, blinkInterval.y);
            }

            _blinkLeft = Mathf.Max(0f, _blinkLeft - dt);
        }

        float EyeOpen => _blinkLeft > 0f ? 1f - 0.9f * Mathf.Sin(Mathf.PI * (1f - _blinkLeft / blinkDuration)) : 1f;

        // Pha bước tăng theo quãng đường đi được nên lúc bám đất xúc tu đứng yên so với mặt đất, không bị trượt
        void UpdateCrawl(Motion motion, float dt, bool playing)
        {
            float speedX = motion.State == OctopusState.Grounded ? Mathf.Abs(motion.Velocity.x) : 0f;
            float target = Mathf.Clamp01(speedX / crawlSpeedReference);
            if (!playing)
            {
                _crawlWeight = target;
                _crawlPhase = previewCrawlPhase;
                return;
            }

            _crawlWeight = Mathf.MoveTowards(_crawlWeight, target, dt * 5f);
            _crawlPhase = Mathf.Repeat(_crawlPhase + speedX * dt * stanceRatio / strideLength, 1f);
        }

        // ---------- Tư thế ----------

        Pose ComputePose(Vector2 head, Motion motion, float time)
        {
            float speed = motion.Velocity.magnitude;
            float speedStretch = Mathf.Lerp(1f, maxStretch, speed / speedForMaxStretch);
            var forward = new Vector2(motion.Facing, 0f);

            switch (motion.State)
            {
                case OctopusState.Swinging:
                    return SwingPose(head, motion, speed, speedStretch, forward);
                case OctopusState.Clinging:
                    return ClingPose(motion, time);
                case OctopusState.Grounded:
                    return GroundPose(head, motion, time);
                default:
                    return motion.Air == AirMode.Fly && speed > flyPoseSpeed
                        ? FlyPose(motion.Velocity / speed, speedStretch, motion.Facing)
                        : JumpPose(motion, forward);
            }
        }

        Pose GroundPose(Vector2 head, Motion motion, float time)
        {
            float facing = motion.Facing;
            float weight = _crawlWeight;
            float stepWave = Mathf.Sin(_crawlPhase * Mathf.PI * 2f * 3f); // 3 xúc tu → 3 nhịp mỗi chu kỳ
            float breath = Mathf.Sin(time * breathSpeed) * breathAmount * (1f - weight);
            float bob = stepWave * bodyBob * weight;

            Vector2 axis = Curves.Rotate(new Vector2(facing, Mathf.Lerp(-0.6f, -0.22f, weight)).normalized, stepWave * crawlSway * weight);
            float glance = (Mathf.PerlinNoise(time * 0.25f, 0.5f) - 0.5f) * 70f * (1f - weight);
            Vector2 look = Curves.Rotate(new Vector2(facing, 0.15f).normalized, glance * facing);

            return new Pose
            {
                Axis = axis,
                Bend = Mathf.Lerp(curlBend, 0.12f, weight) * facing,
                Stretch = 1f + breath + Mathf.Abs(bob) * 0.6f,
                HeadLift = bob,
                Look = look,
                A = Foot(0, head, facing, weight, time),
                B = Foot(1, head, facing, weight, time),
                C = Foot(2, head, facing, weight, time),
            };
        }

        // Chu kỳ một xúc tu: bám đất (lùi dần so với thân, tức đứng yên so với đất) → nhấc lên vòng cung bước tới
        Limb Foot(int index, Vector2 head, float facing, float weight, float time)
        {
            float phase = Mathf.Repeat(_crawlPhase + index / 3f, 1f);
            float stride;
            float lift;
            if (phase < stanceRatio)
            {
                stride = Mathf.Lerp(0.5f, -0.5f, phase / stanceRatio);
                lift = 0f;
            }
            else
            {
                float t = (phase - stanceRatio) / (1f - stanceRatio);
                stride = Mathf.Lerp(-0.5f, 0.5f, t * t * (3f - 2f * t));
                lift = Mathf.Sin(t * Mathf.PI);
            }

            float home = footHomes != null && footHomes.Length > index ? footHomes[index] : 0f;
            float x = head.x + facing * (home + stride * strideLength * weight);
            x += Mathf.Sin(time * breathSpeed * 0.8f + index * 1.7f) * 0.04f * (1f - weight); // nhúc nhích khi đứng yên
            var foot = new Vector2(x, GroundY(new Vector2(x, head.y)) + lift * stepHeight * weight);

            // Xúc tu cong ra ngoài như dáng chân "( | )", nhấc chân thì cong cao hơn
            var arch = new Vector2(facing * home * 0.12f, 0.24f + 0.16f * lift * weight);
            return Limb.FootAt(foot, arch);
        }

        // Thân nằm ngang theo hướng mặt: ngóc đầu khi bật lên, chúi đầu khi rơi, nên đuôi không cắm xuống đất lúc tiếp đất.
        // Xúc tu: bật lên thì đạp về phía sau-dưới, tới đỉnh xoè rộng như chiếc ô, rơi xuống thì chĩa xuống đón đất.
        Pose JumpPose(Motion motion, Vector2 forward)
        {
            Vector2 velocity = motion.Velocity;
            float facing = motion.Facing;
            float rise = Mathf.Clamp01(velocity.y / 8f);
            float fall = Mathf.Clamp01(-velocity.y / 9f);
            float apex = 1f - Mathf.Max(rise, fall);

            Vector2 axis = Curves.Rotate(forward, Mathf.Clamp(velocity.y / 10f, -1f, 1f) * jumpPitch * facing);
            float length = tentacleLength * (rise * 1f + apex * 0.85f + fall * 0.95f);
            float curl = tipCurl * (0.4f + 0.6f * apex);

            // Góc so với phương thẳng xuống, dương = về phía trước mặt
            Limb Arm(float side)
            {
                float angle = rise * (-riseKickAngle + riseSpread * side) + apex * (apexSpread * side) + fall * (10f + fallSpread * side);
                return Limb.ReachTo(Curves.Rotate(Vector2.down, angle * facing), length, curl * (side >= 0f ? facing : -facing), 1f + apex);
            }

            return new Pose
            {
                Axis = axis,
                Bend = curlBend * 0.6f * apex * facing,
                Stretch = 1f + 0.1f * rise,
                Look = velocity.sqrMagnitude > 1f ? velocity.normalized : forward,
                A = Arm(1f),
                B = Arm(0f),
                C = Arm(-1f),
            };
        }

        // Theo bản phác: xúc tu chụm vươn về phía trước, đuôi nhọn phía sau
        Pose FlyPose(Vector2 direction, float speedStretch, float facing)
        {
            float curl = tipCurl * 0.35f * facing;
            return new Pose
            {
                Axis = direction,
                Stretch = speedStretch,
                Look = direction,
                A = Limb.ReachTo(Curves.Rotate(direction, 10f), tentacleLength, curl),
                B = Limb.ReachTo(direction, tentacleLength * 1.05f, -curl * 0.5f),
                C = Limb.ReachTo(Curves.Rotate(direction, -10f), tentacleLength, -curl),
            };
        }

        Pose SwingPose(Vector2 head, Motion motion, float speed, float speedStretch, Vector2 forward)
        {
            Vector2 axis = Curves.SafeDirection(motion.Anchor - head, _axis);
            float sideSpeed = Vector2.Dot(motion.Velocity, new Vector2(-axis.y, axis.x));
            float trail = sideSpeed >= 0f ? -1f : 1f; // hai xúc tu tự do quất về phía sau chuyển động

            return new Pose
            {
                Axis = axis,
                Bend = -Mathf.Clamp(sideSpeed / 15f, -1f, 1f) * swingBend,
                Stretch = speedStretch,
                Look = speed > 1f ? motion.Velocity / speed : forward,
                A = Limb.GrabAt(motion.Anchor),
                B = Limb.ReachTo(Curves.Rotate(axis, trail * swingFreeTentacleAngle), tentacleLength, trail * tipCurl * 0.8f),
                C = Limb.ReachTo(Curves.Rotate(axis, trail * (swingFreeTentacleAngle + 40f)), tentacleLength * 0.9f, trail * tipCurl, 1.4f),
            };
        }

        Pose ClingPose(Motion motion, float time)
        {
            Vector2 normal = motion.ClingNormal;
            var tangent = new Vector2(-normal.y, normal.x);
            float sway = Mathf.Sin(time * 1.6f);

            return new Pose
            {
                Axis = Curves.Rotate(-normal, sway * 8f),
                Bend = sway * 0.15f,
                Stretch = 1f + Mathf.Sin(time * breathSpeed) * breathAmount,
                Look = new Vector2(motion.Facing * 0.5f, -0.85f),
                A = Limb.GrabAt(motion.ClingPoint + tangent * 0.6f),
                B = Limb.ReachTo(Curves.Rotate(normal, 50f * motion.Facing), tentacleLength * 0.8f, tipCurl * motion.Facing, 2f),
                C = Limb.GrabAt(motion.ClingPoint - tangent * 0.6f),
            };
        }

        // ---------- Vẽ ----------

        // Thân là một đường cong Bezier từ đầu tới đuôi; độ dày lấy từ bodyWidth
        Vector2 DrawBody(Vector2 head, Vector2 side, float lengthScale, float widthScale)
        {
            float length = bodyLength * lengthScale;
            Vector2 tail = head - _axis * length;
            Vector2 control = (head + tail) * 0.5f + side * (_bend * length);

            for (int i = 0; i < bodySegments; i++)
                _bodyPoints[i] = Curves.Bezier(head, control, tail, i / (bodySegments - 1f));
            body.SetPositions(_bodyPoints);
            bodyOutline.SetPositions(_bodyPoints);
            body.widthMultiplier = bodyOutline.widthMultiplier = widthScale;

            Vector2 eyePosition = Curves.Bezier(head, control, tail, eyeAlongBody);
            Vector2 eyeForward = eyePosition - Curves.Bezier(head, control, tail, eyeAlongBody + 0.05f);
            float angle = Mathf.Atan2(eyeForward.y, eyeForward.x) * Mathf.Rad2Deg;
            eye.SetPositionAndRotation(eyePosition, Quaternion.Euler(0f, 0f, angle));
            eye.localScale = new Vector3(eyeSize, eyeSize * EyeOpen, 1f);
            pupil.localPosition = (Vector2)(Quaternion.Euler(0f, 0f, -angle) * _look) * (pupilTravel / Mathf.Max(eyeSize, 0.01f));

            glow.transform.position = Curves.Bezier(head, control, tail, 0.35f);
            glow.transform.localScale = Vector3.one * glowSize;
            return tail;
        }

        void Animate(Tentacle tentacle, Vector2 root, Limb limb, float time, float dt)
        {
            switch (limb.Mode)
            {
                case LimbMode.Grab:
                    tentacle.Grab(root, limb.Target, shootDuration, dt);
                    break;
                case LimbMode.Foot:
                    tentacle.Plant(root, limb.Target, limb.Arch, tipCurl, dt);
                    break;
                default:
                    tentacle.Reach(root, limb.Direction, limb.Length, waveAngle * limb.Wave, limb.Curl, time, dt);
                    break;
            }
        }

        float GroundY(Vector2 from)
        {
            RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, groundDepth + 2f, groundMask);
            return hit.collider != null ? hit.point.y : from.y - groundDepth;
        }
    }

    /// <summary>
    /// Một xúc tu: chuỗi điểm vẽ bằng LineRenderer (texture lặp tạo giác hút), đầu mút gắn sprite giác hút.
    /// Điểm lưu theo toạ độ tương đối với gốc nên bay nhanh không bị kéo dãn; chỉ khi đổi hướng
    /// thì đầu mút trễ hơn gốc, tạo cảm giác xúc tu mềm quất theo.
    /// </summary>
    [Serializable]
    public class Tentacle
    {
        public LineRenderer line;
        public Transform tip;
        [Range(4, 24)] public int segments = 14;

        Vector2[] _offsets;
        Vector3[] _points;
        bool _grabbing;
        Vector2 _grabTarget;
        Vector2 _shootFrom;
        float _shootProgress = 1f;

        public bool IsValid => line != null && tip != null;
        public bool NeedsInit => _offsets == null || _offsets.Length != segments;

        public void Init(Vector2 root)
        {
            _offsets = new Vector2[segments];
            _points = new Vector3[segments];
            _grabbing = false;
            line.useWorldSpace = true;
            line.positionCount = segments;
            Draw(root);
        }

        public void ApplyLook(AnimationCurve width, Color color, Color tipColor, float tipSize, float suckersPerUnit)
        {
            line.widthCurve = width;
            line.startColor = line.endColor = color;
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = new Vector2(suckersPerUnit, 1f);
            tip.localScale = Vector3.one * tipSize;
            if (tip.TryGetComponent(out SpriteRenderer renderer))
                renderer.color = tipColor;
        }

        // Nắm một điểm cố định (đá): khi đổi chỗ bám thì phóng từ đầu mút hiện tại tới chỗ mới
        public void Grab(Vector2 root, Vector2 target, float shootDuration, float dt)
        {
            if (!_grabbing || (target - _grabTarget).sqrMagnitude > 0.25f)
            {
                _shootFrom = root + _offsets[segments - 1];
                _shootProgress = 0f;
            }

            _grabbing = true;
            _grabTarget = target;
            _shootProgress = shootDuration > 0f ? Mathf.MoveTowards(_shootProgress, 1f, dt / shootDuration) : 1f;

            Vector2 end = Vector2.Lerp(_shootFrom, target, _shootProgress);
            Vector2 control = (root + end) * 0.5f + Vector2.down * (0.05f * Vector2.Distance(root, end));
            for (int i = 0; i < segments; i++)
                _offsets[i] = Curves.Bezier(root, control, end, i / (segments - 1f)) - root;

            Draw(root);
        }

        // Vươn tự do theo một hướng: thân xúc tu uốn lượn, đầu mút cuộn lại. Dựng từng đốt nối tiếp nhau
        // nên độ dài luôn đúng; gốc bám chặt, càng về đầu mút càng trễ → xúc tu quất theo quán tính
        public void Reach(Vector2 root, Vector2 direction, float length, float waveDegrees, float curlDegrees, float time, float dt)
        {
            _grabbing = false;
            float segment = length / (segments - 1);
            Vector2 rest = Vector2.zero;

            for (int i = 1; i < segments; i++)
            {
                float t = i / (segments - 1f);
                float curl = curlDegrees * Mathf.Pow(Mathf.Max(0f, (t - 0.55f) / 0.45f), 2f);
                rest += Curves.Rotate(direction, Mathf.Sin(time - t * 3f) * waveDegrees * t + curl) * segment;
                float stiffness = Mathf.Lerp(40f, 8f, t);
                _offsets[i] = Vector2.Lerp(_offsets[i], rest, 1f - Mathf.Exp(-stiffness * dt));
            }

            Draw(root);
        }

        // Đặt xúc tu xuống đất: cong từ gốc xuống điểm chạm đất, phần đầu mút duỗi tiếp rồi cuộn ngược lên.
        // Làm mượt theo toạ độ world nên chỗ đang bám đất đứng yên, không bị kéo theo thân
        public void Plant(Vector2 root, Vector2 foot, Vector2 arch, float curlDegrees, float dt)
        {
            _grabbing = false;
            const int curlPoints = 4;
            int bodyPoints = segments - curlPoints;
            Vector2 control = (root + foot) * 0.5f + arch;
            float blend = 1f - Mathf.Exp(-30f * dt);

            for (int i = 1; i < bodyPoints; i++)
                MoveTowardsWorld(i, Curves.Bezier(root, control, foot, i / (bodyPoints - 1f)), root, blend);

            var flat = new Vector2(foot.x >= root.x ? 1f : -1f, 0f);
            Vector2 point = foot;
            for (int k = 0; k < curlPoints; k++)
            {
                point += Curves.Rotate(flat, curlDegrees * (k + 1) / curlPoints * flat.x) * 0.07f;
                MoveTowardsWorld(bodyPoints + k, point, root, blend);
            }

            Draw(root);
        }

        void MoveTowardsWorld(int index, Vector2 target, Vector2 root, float blend)
        {
            _offsets[index] = Vector2.Lerp(_points[index], target, blend) - root;
        }

        void Draw(Vector2 root)
        {
            _offsets[0] = Vector2.zero;
            for (int i = 0; i < segments; i++)
                _points[i] = root + _offsets[i];
            line.SetPositions(_points);

            Vector2 end = _points[segments - 1];
            Vector2 direction = end - (Vector2)_points[segments - 2];
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            tip.SetPositionAndRotation(end, Quaternion.Euler(0f, 0f, angle));
        }
    }

    static class Curves
    {
        public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        public static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;

        public static Vector2 SafeDirection(Vector2 v, Vector2 fallback) =>
            v.sqrMagnitude > 0.0001f ? v.normalized : fallback;

        public static Vector2 RotateTowards(Vector2 from, Vector2 to, float t)
        {
            float a = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float b = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(a, b, t) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public static Gradient TwoTone(Color main, Color end, float endStart)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(main, 0f),
                    new GradientColorKey(main, Mathf.Min(endStart, 0.99f)),
                    new GradientColorKey(end, Mathf.Min(endStart + 0.14f, 1f)),
                },
                new[] { new GradientAlphaKey(main.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }

        public static Gradient Fade(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        public static AnimationCurve Thicker(AnimationCurve curve, float amount)
        {
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
                keys[i].value += amount;
            return new AnimationCurve(keys);
        }
    }
}
