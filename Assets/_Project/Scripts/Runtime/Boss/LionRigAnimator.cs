using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Hoạt ảnh boss sư tử gắn xương (Art/Lion/Rig, do Tools/art/lion_rig.py cắt từ Docs/boss_su_tu/su_tu/lion_component.png).
    /// AI boss chỉ nói chuyện qua ILionVisual:
    ///  - SetLocomotion: đứng thở (bờm lay như lửa, đuôi đung đưa), đi (chéo cặp: trước gần + sau xa, rồi trước xa + sau
    ///    gần; vai và hông nhấp nhô), chạy nhanh thì phi nước đại (thân co duỗi, 2 chân sau đạp rồi 2 chân trước).
    ///    Móng đang chạm đất đứng yên trên sàn (IK 2 xương + bàn chân giữ phẳng).
    ///  - Act: mỗi tư thế là chuỗi mốc theo nhịp hoạt hình (lấy đà → ra đòn → quá đà → về), đi ra từ đúng dáng đang hiện
    ///    nên nối đòn / cắt ngang không giật; hết đòn thì hoà về đi / đứng.
    ///  - Facing: quay đầu trong turnTime, đổi chiều ngay giữa lượt (thân chỉ co nhẹ, không bóp thành sợi chỉ).
    ///  - Flash, SetEnrage: chớp trắng → đỏ khi trúng đòn; nổi giận thì vết nứt / bờm / móng rực lên, có hào quang đỏ.
    /// Không Play vẫn xem được: chọn Preview rồi bấm ▶ Chạy thử trong Inspector.
    /// </summary>
    [ExecuteAlways]
    public class LionRigAnimator : MonoBehaviour, ILionVisual
    {
        public enum Preview { Idle, Walk, Run, Air }

        [Serializable]
        public class Leg
        {
            public Transform upper;
            public Transform lower;
            public Transform paw;
            public SpriteRenderer renderer;
            [Tooltip("Đùi: gốc → khớp (unit tranh)")]
            public float upperLength;
            [Tooltip("Cẳng: khớp → cổ chân (unit tranh)")]
            public float lowerLength;
            [Tooltip("Bàn: cổ chân → đầu móng (unit tranh)")]
            public float pawLength;
            [Tooltip("Cổ chân khi đứng như tranh gốc (toạ độ trong Pose, mặt sàn y = 0)")]
            public Vector2 rest;
            [Tooltip("Tâm bước khi đi / phi (Pose): dồn về dưới vai / hông hơn dáng đứng")]
            public Vector2 walkCenter;
            [Tooltip("Góc bàn chân khi đứng (độ, trong Pose)")]
            public float restPawAngle;
            [Tooltip("Góc bàn so với cẳng ở tranh gốc (độ)")]
            public float pawBend;
            public bool front;
            public bool near;
            [Tooltip("Thứ tự vẽ thường của chân")]
            public int sortingOrder;

            [NonSerialized] public Vector2 ankle;    // cổ chân đang đặt (Pose)
            [NonSerialized] public float curl;       // bàn chân gập thêm (độ)
            [NonSerialized] public Vector2 fromAnkle;
            [NonSerialized] public float fromCurl;
        }

        [Serializable]
        public class Chain
        {
            public Transform[] bones = Array.Empty<Transform>();
            [Tooltip("Góc gốc của từng xương (độ, so với cha)")]
            public float[] rest = Array.Empty<float>();
            [Tooltip("true = lọn lửa vẽ trên bờm (chỉ để biết, không ảnh hưởng chuyển động)")]
            public bool front;
        }

        [Header("Xem trước khi không Play (bấm ▶ Chạy thử trong Inspector)")]
        [SerializeField] Preview preview = Preview.Idle;
        [Tooltip("Tốc độ khi xem đi / chạy (unit world / giây)")]
        [SerializeField] float previewSpeed = 3f;
        [Tooltip("Xem trước: quay sang trái")]
        [SerializeField] bool previewLeft;

        [Header("Kích thước")]
        [Tooltip("Tỉ lệ unit tranh → world (LionRigBuilder đặt theo chiều cao boss)")]
        [SerializeField] float size = 1.8f;
        [Tooltip("Chiều cao con sư tử đứng (unit tranh, đỉnh bờm → sàn)")]
        [SerializeField] float rigHeight = 4.01f;
        [Tooltip("Thời gian quay đầu khi đổi hướng (giây)")]
        [SerializeField, Range(0.1f, 0.6f)] float turnTime = 0.25f;
        [Tooltip("Quay đầu: đổi chiều ngay giữa lượt, quanh lúc đổi thân chỉ co lại bấy nhiêu phần (không bóp dẹt)")]
        [SerializeField, Range(0f, 0.5f)] float turnSquash = 0.22f;

        [Header("Đi")]
        [Tooltip("Thân đi được bấy nhiêu (unit tranh) trong một chu kỳ bước")]
        [SerializeField, Min(0.2f)] float walkCycle = 1.55f;
        [Tooltip("Phần chu kỳ móng chạm đất khi đi")]
        [SerializeField, Range(0.4f, 0.8f)] float walkStance = 0.62f;
        [Tooltip("Móng nhấc cao bấy nhiêu khi bước (unit tranh)")]
        [SerializeField, Range(0f, 0.6f)] float walkLift = 0.24f;
        [Tooltip("Thân nhún mỗi bước (unit tranh)")]
        [SerializeField, Range(0f, 0.2f)] float walkBob = 0.045f;
        [Tooltip("Vai / hông nhấp nhô lệch nhau (độ)")]
        [SerializeField, Range(0f, 8f)] float walkRoll = 2.6f;
        [Tooltip("Đi chậm hơn mức này (unit tranh / giây) là đứng")]
        [SerializeField] float walkThreshold = 0.08f;
        [Tooltip("Khi đi, chỗ đặt chân dồn từ dáng đứng về tâm bước bấy nhiêu phần (0 = như tranh gốc)")]
        [SerializeField, Range(0f, 1f)] float walkGather = 1f;

        [Header("Chạy (phi)")]
        [Tooltip("Từ tốc độ x tới y (unit tranh / giây) chuyển dần từ đi sang phi")]
        [SerializeField] Vector2 runBlend = new Vector2(2.3f, 3.4f);
        [SerializeField, Min(0.5f)] float runCycle = 3.8f;
        [SerializeField, Range(0.2f, 0.6f)] float runStance = 0.34f;
        [SerializeField, Range(0f, 0.8f)] float runLift = 0.4f;
        [Tooltip("Thân chúi / ngóc theo nhịp phi (độ)")]
        [SerializeField, Range(0f, 15f)] float runPitch = 6.5f;
        [Tooltip("Lưng co / duỗi theo nhịp phi (độ)")]
        [SerializeField, Range(0f, 20f)] float runFlex = 10f;
        [SerializeField, Range(0f, 0.4f)] float runBob = 0.16f;

        [Header("Đứng thở")]
        [SerializeField, Range(0f, 0.05f)] float breathBob = 0.014f;
        [SerializeField, Range(0f, 3f)] float breathBend = 0.9f;
        [SerializeField] float breathSpeed = 1.7f;

        [Header("Bờm, lửa, đuôi")]
        [Tooltip("Bờm lay chậm (độ)")]
        [SerializeField, Range(0f, 10f)] float maneSway = 4f;
        [Tooltip("Lửa lập loè nhanh (độ)")]
        [SerializeField, Range(0f, 6f)] float maneFlicker = 2f;
        [Tooltip("Lọn lửa rời uốn như ngọn lửa (độ)")]
        [SerializeField, Range(0f, 20f)] float flameWave = 7f;
        [Tooltip("Đuôi đung đưa (độ)")]
        [SerializeField, Range(0f, 20f)] float tailSway = 6f;
        [Tooltip("Bờm / đuôi văng theo quán tính khi thân tăng / giảm tốc")]
        [SerializeField, Range(0f, 10f)] float inertia = 3f;

        [Header("Ra đòn")]
        [Tooltip("Hết đòn: hoà về đi / đứng trong bấy nhiêu giây (đòn chồm cao thì lâu hơn chút)")]
        [SerializeField, Range(0.1f, 0.6f)] float settleTime = 0.3f;
        [Tooltip("Chân không theo mốc riêng thì đi từ chỗ cũ về chỗ mới trong bấy nhiêu giây")]
        [SerializeField, Range(0.05f, 0.4f)] float legBlend = 0.16f;
        [Tooltip("Đầu thường ↔ đầu gầm chuyển trong bấy nhiêu giây")]
        [SerializeField, Range(0.02f, 0.2f)] float roarSwap = 0.07f;

        [Header("Chớp, nổi giận")]
        [SerializeField, Range(0.05f, 0.6f)] float flashTime = 0.22f;
        [SerializeField] Color flashWhite = Color.white;
        [SerializeField] Color flashRed = new Color(1f, 0.16f, 0.1f);
        [Tooltip("Màu hào quang nổi giận")]
        [SerializeField] Color auraColor = new Color(1f, 0.12f, 0.05f, 0.55f);
        [Tooltip("Màu quầng mắt")]
        [SerializeField] Color eyeColor = new Color(1f, 0.15f, 0.1f, 0.85f);
        [Tooltip("Nổi giận hẳn thì chỗ đỏ rực lên bấy nhiêu (1 = như tranh)")]
        [SerializeField, Range(1f, 3f)] float enrageEmber = 2.1f;

        [Header("Tham chiếu (đã nối sẵn, không cần sửa)")]
        [SerializeField] Transform pose;
        [SerializeField] Transform hips;
        [SerializeField] Transform chest;
        [SerializeField] Transform neck;
        [SerializeField] SpriteRenderer headArt;
        [SerializeField] SpriteRenderer roarArt;
        [SerializeField] Transform mouth;
        [SerializeField] SpriteRenderer eyeGlow;
        [SerializeField] SpriteRenderer aura;
        [SerializeField] Chain mane = new Chain();
        [Tooltip("Bờm toả lửa: 1 = vuốt ngược ra sau khi lao tới, -0.5 = vểnh lên (lửa sau lưng)")]
        [SerializeField] float[] maneSweep = Array.Empty<float>();
        [SerializeField] Chain tail = new Chain();
        [Tooltip("Chùm lửa ngọn đuôi dài bấy nhiêu (unit tranh): giữ ngọn đuôi không cắm xuống sàn")]
        [SerializeField] float tuftLength = 0.8f;
        [SerializeField] Chain[] flames = Array.Empty<Chain>();
        [Tooltip("Trước gần, trước xa, sau gần, sau xa")]
        [SerializeField] Leg[] legs = Array.Empty<Leg>();
        [SerializeField] SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        [Tooltip("Các điểm bao quanh thân / đầu / bờm / chân (không tính đuôi) để tính VisualBounds")]
        [SerializeField] Transform[] boundPoints = Array.Empty<Transform>();
        [SerializeField] float hipsAngle;
        [SerializeField] float chestAngle;
        [SerializeField] Vector2 hipsRest;
        [SerializeField] Vector2 chestRest;
        [SerializeField] float neckAngle;
        [Tooltip("Thứ tự vẽ chân trước xa khi giơ lên vả (trên bờm, sau đầu)")]
        [SerializeField] int raisedFarLegOrder = -27;

        const int NearFront = 0, FarFront = 1, NearHind = 2, FarHind = 3;

        float _previewDelta;
        bool _previewLeft;
        float _time;
        float _velocityX;
        bool _grounded = true;
        float _facing = 1f, _flip = 1f;
        float _speed;                  // tốc độ theo hướng mặt (unit tranh / giây, có dấu)
        float _move, _run, _air;       // trọng số đi / phi / trên không (0..1, chuyển mượt)
        float _phase;
        Vector2 _lastRoot;
        Vector2 _rootVelocity;         // vận tốc gốc (world)
        bool _hasLast;

        LionPose _act;
        float _actTime, _actDuration;
        float _actStartK;              // đòn bắt đầu giữa chừng (vd chết khi đang nằm gục: bỏ đoạn đổ xuống)
        bool _actHold;
        bool _releasing;
        float _releaseTime, _releaseDuration;
        Look _from, _shown;
        Vector2 _lastChest, _chestVelocity;
        bool _hasChest;
        float _maneSpring, _maneSpin, _tailSpring, _tailSpin;

        float _flash, _flashAge = 10f;
        float _enrage, _enrageTarget;
        float _roarShow;
        float _death;                  // lửa tắt dần khi chết (0..1)
        Material _material;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int EmberId = Shader.PropertyToID("_Ember");

        // ---------- ILionVisual ----------

        public float Facing
        {
            get => _facing;
            set => _facing = value < 0f ? -1f : 1f;
        }

        public void SetLocomotion(float velocityX, bool grounded)
        {
            _velocityX = velocityX;
            _grounded = grounded;
        }

        /// <summary>
        /// Diễn tư thế pose trong duration giây. Mốc đầu đi ra từ đúng dáng đang hiện nên nối đòn (RearUp → ClawSwipe →
        /// ClawSwipeAlt, Crouch → Leap → Land, ChargeWindup → Charge) liền mạch. hold = giữ dáng cuối tới khi có Act khác
        /// (Stagger giữ dáng nằm gục, Die luôn giữ). Act(None) = thôi đòn, hoà về đi / đứng.
        /// </summary>
        public void Act(LionPose pose, float duration, bool hold = false)
        {
            TakeSnapshot();
            if (pose == LionPose.None)
            {
                StartRelease();
                return;
            }

            _act = pose;
            _actDuration = Mathf.Max(0.05f, duration);
            // Đang nằm gục mà chết: bỏ đoạn gầm + đổ xuống, đi thẳng vào nằm im
            _actStartK = pose == LionPose.Die && _from.Offset.y < -0.6f ? 0.6f : 0f;
            _actTime = _actStartK * _actDuration;
            _actHold = hold || pose == LionPose.Die;
            _releasing = false;
            if (pose != LionPose.Die)
                _death = 0f;
        }

        public void Flash(float strength)
        {
            _flash = Mathf.Clamp01(Mathf.Max(strength, _flashAge < flashTime ? CurrentFlash() : 0f));
            _flashAge = 0f;
        }

        public void SetEnrage(float amount) => _enrageTarget = Mathf.Clamp01(amount);

        public Vector2 MouthPoint => mouth != null ? (Vector2)mouth.position : (Vector2)transform.position;

        public Vector2 PawPoint
        {
            get
            {
                if (legs.Length < 2)
                    return transform.position;
                Leg leg = _act == LionPose.ClawSwipeAlt ? legs[FarFront] : legs[NearFront];
                return leg.paw.TransformPoint(new Vector3(leg.pawLength, 0f, 0f));
            }
        }

        public Bounds VisualBounds
        {
            get
            {
                if (boundPoints.Length == 0)
                    return new Bounds(transform.position, Vector3.zero);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
                for (int i = 0; i < boundPoints.Length; i++)
                {
                    if (boundPoints[i] == null)
                        continue;
                    Vector2 p = boundPoints[i].position;
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }

                for (int i = 0; i < legs.Length; i++)
                {
                    Vector2 p = legs[i].paw.TransformPoint(new Vector3(legs[i].pawLength, 0f, 0f));
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }

                var bounds = new Bounds();
                bounds.SetMinMax(new Vector3(min.x, min.y, 0f), new Vector3(max.x, max.y, 0f));
                return bounds;
            }
        }

        /// <summary>Đòn đang diễn (None = đang đi / đứng).</summary>
        public LionPose CurrentPose => _act;

        /// <summary>Tiến độ đòn đang diễn (0..1).</summary>
        public float ActProgress => _act == LionPose.None ? 1f : Mathf.Clamp01(_actTime / _actDuration);

        /// <summary>Editor gọi mỗi frame khi bấm ▶ Chạy thử (không Play): cho thời gian trôi để xem chuyển động.</summary>
        public void TickPreview(float deltaTime) => _previewDelta = Mathf.Clamp(deltaTime, 0f, 0.05f);

        bool HasReferences
        {
            get
            {
                if (pose == null || hips == null || chest == null || neck == null || legs == null || legs.Length < 4)
                    return false;
                foreach (Leg leg in legs)
                {
                    if (leg == null || leg.upper == null || leg.lower == null || leg.paw == null)
                        return false;
                }

                return true;
            }
        }

        void Awake()
        {
            if (!Application.isPlaying || renderers.Length == 0 || renderers[0] == null)
                return;
            // Mỗi con boss một vật liệu riêng (chớp / rực không lây sang con khác); tạo một lần, không cấp phát mỗi frame
            Material shared = renderers[0].sharedMaterial;
            _material = new Material(shared) { name = "Lion (instance)" };
            foreach (SpriteRenderer r in renderers)
            {
                if (r != null && r.sharedMaterial == shared)
                    r.sharedMaterial = _material;
            }
        }

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        void OnEnable()
        {
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] != null)
                    legs[i].ankle = legs[i].rest;
            }

            _hasLast = false;
            _hasChest = false;
        }

        void LateUpdate()
        {
            if (!HasReferences)
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
            UpdateAct(dt, live);
            Look look = CurrentLook(live);
            _shown = look;
            PlacePose();
            PlaceBody(look, dt, live);
            PlaceLegs(look);
            UpdateGlow(look, dt, live);
        }

        void ApplyPreview()
        {
            float speed = preview == Preview.Walk ? previewSpeed : preview == Preview.Run ? previewSpeed * 4f : 0f;
            if (previewLeft != _previewLeft)
                _facing = previewLeft ? -1f : 1f; // đổi ô Preview Left thì quay theo; nút Quay đầu vẫn dùng được
            _previewLeft = previewLeft;
            _velocityX = speed * _facing;
            _grounded = preview != Preview.Air;
        }

        // ---------- Đi, chạy, quay đầu ----------

        void UpdateMotion(float dt, bool live)
        {
            Vector2 root = transform.position;
            if (live && _hasLast)
                _rootVelocity = Vector2.Lerp(_rootVelocity, (root - _lastRoot) / dt, 1f - Mathf.Exp(-12f * dt));
            _lastRoot = root;
            _hasLast = true;

            float scale = Mathf.Max(size, 0.01f);
            _speed = _velocityX * _facing / scale;
            float absSpeed = Mathf.Abs(_speed);
            bool grounded = _grounded;
            float move = grounded && absSpeed > walkThreshold ? 1f : 0f;
            float run = grounded ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(runBlend.x, runBlend.y, absSpeed)) : 0f;
            _move = live ? Mathf.MoveTowards(_move, move, dt * 4f) : move;
            _run = live ? Mathf.MoveTowards(_run, run, dt * 3f) : run;
            _air = live ? Mathf.MoveTowards(_air, grounded ? 0f : 1f, dt * 6f) : grounded ? 0f : 1f;

            // Nhịp bước theo quãng đường đã đi: móng đang chạm đất đứng yên dù đi nhanh hay chậm, đi lùi thì bước lùi
            float cycle = Mathf.Lerp(walkCycle, runCycle, _run);
            if (live)
                _phase = Mathf.Repeat(_phase + _speed * dt / cycle, 1f);
            _flip = live ? Mathf.MoveTowards(_flip, _facing, dt * 2f / Mathf.Max(turnTime, 0.01f)) : _facing;
        }

        void PlacePose()
        {
            // Quay đầu: không bóp qua 0 mà đổi chiều ngay giữa lượt; quanh lúc đổi thân co nhẹ rồi bung ra, hơi thấp xuống
            float turning = 1f - Mathf.Abs(_flip);
            float sx = (_flip >= 0f ? 1f : -1f) * (1f - turnSquash * turning) * size;
            float sy = (1f - 0.04f * turning) * size;
            pose.localScale = new Vector3(sx, sy, 1f);
            pose.localPosition = Vector3.zero;
            pose.localRotation = Quaternion.identity;
        }

        // ---------- Ra đòn ----------
        //
        // Mỗi tư thế là chuỗi mốc dáng thân (BodyKey) + mốc chân (LegKey) theo k = 0..1 của thời gian đòn. Mốc đầu tiên đi ra
        // từ dáng đang hiện lúc gọi Act (chụp lại), nên đòn nối đòn hay cắt ngang đều liền mạch. Góc / vị trí tính trong Pose
        // (phía trước là +x vì Pose đã lật theo hướng quay).

        enum Ease : byte { Linear, InOut, Out, Snap, In, Back }

        struct Look
        {
            public Vector2 Offset;   // dời hông (unit tranh, +x = phía trước)
            public float Tilt;       // cả thân ngả quanh hông (độ, + = ngóc lên)
            public float Bend;       // ngực gập so với hông (độ, + = ngực ngóc lên)
            public float Head;       // đầu ngẩng thêm (độ)
            public float Roar;       // 0..1: đầu gầm há miệng
            public float Mane;       // bờm bùng (1) / rủ xuống (âm)
            public float Tail;       // đuôi vểnh thêm (độ)
            public float Lash;       // đuôi quất (0..1)
            public float Stretch;    // lưng dài thêm (unit tranh)
            public float Shake;      // lắc bờm (0..1)

            public static Look Mix(in Look a, in Look b, float t) => new Look
            {
                Offset = Vector2.LerpUnclamped(a.Offset, b.Offset, t),
                Tilt = Mathf.LerpUnclamped(a.Tilt, b.Tilt, t),
                Bend = Mathf.LerpUnclamped(a.Bend, b.Bend, t),
                Head = Mathf.LerpUnclamped(a.Head, b.Head, t),
                Roar = Mathf.Lerp(a.Roar, b.Roar, t),
                Mane = Mathf.LerpUnclamped(a.Mane, b.Mane, t),
                Tail = Mathf.LerpUnclamped(a.Tail, b.Tail, t),
                Lash = Mathf.Lerp(a.Lash, b.Lash, t),
                Stretch = Mathf.LerpUnclamped(a.Stretch, b.Stretch, t),
                Shake = Mathf.Lerp(a.Shake, b.Shake, t),
            };
        }

        readonly struct BodyKey
        {
            public readonly float K;
            public readonly Ease Ease;
            public readonly Look Look;

            public BodyKey(float k, Ease ease, float x, float y, float tilt, float bend, float head, float roar, float mane,
                float tail, float lash, float stretch = 0f, float shake = 0f)
            {
                K = k;
                Ease = ease;
                Look = new Look
                {
                    Offset = new Vector2(x, y), Tilt = tilt, Bend = bend, Head = head, Roar = roar, Mane = mane, Tail = tail,
                    Lash = lash, Stretch = stretch, Shake = shake,
                };
            }
        }

        enum LegMode : byte { Ground, Polar }

        // Mốc chân: Ground = chỗ đặt chân thường (đứng / đi / phi) dời thêm (X, Y) unit tranh; Polar = móng ở góc X (độ, trong
        // Pose: 0 = phía trước, 90 = lên trên) quanh gốc chân, vươn Y phần chiều dài đùi + cẳng. Curl = bàn chân gập thêm (độ,
        // âm = cụp móng vào). Arc: đoạn tới mốc này nhấc cao lên bấy nhiêu (unit tranh) ở giữa đường
        readonly struct LegKey
        {
            public readonly float K;
            public readonly Ease Ease;
            public readonly LegMode Mode;
            public readonly float X, Y, Curl, Arc;

            public LegKey(float k, Ease ease, LegMode mode, float x, float y, float curl = 0f, float arc = 0f)
            {
                K = k;
                Ease = ease;
                Mode = mode;
                X = x;
                Y = y;
                Curl = curl;
                Arc = arc;
            }

            public static LegKey Ground(float k, Ease ease = Ease.InOut, float x = 0f, float y = 0f, float arc = 0f) =>
                new LegKey(k, ease, LegMode.Ground, x, y, 0f, arc);

            public static LegKey Polar(float k, Ease ease, float angle, float reach, float curl = 0f, float arc = 0f) =>
                new LegKey(k, ease, LegMode.Polar, angle, reach, curl, arc);
        }

        static readonly Look Neutral = default;

        // Thu mình (trước vồ / húc): hạ thấp người, dồn ra sau, cúi đầu, đuôi vểnh quất. Giữ dáng tới hết thời gian
        static readonly BodyKey[] CrouchBody =
        {
            new BodyKey(0.32f, Ease.InOut, -0.22f, -0.42f, -4f, -8f, -14f, 0.3f, 0.3f, 40f, 1f),
            new BodyKey(1f, Ease.Linear, -0.25f, -0.46f, -4f, -9f, -15f, 0.4f, 0.35f, 44f, 1f),
        };

        // Bay vồ: bật duỗi người thật nhanh (thân vươn dài, đầu gầm), 2 chân trước vươn tới, 2 chân sau duỗi ra sau → lượn →
        // gần cuối chúi xuống, chân trước với xuống đón đất
        static readonly BodyKey[] LeapBody =
        {
            new BodyKey(0.12f, Ease.Snap, 0.12f, 0.16f, 10f, 4f, 2f, 1f, 0.7f, -10f, 0f, 0.14f),
            new BodyKey(0.55f, Ease.InOut, 0.06f, 0.12f, 0f, 2f, 0f, 1f, 0.8f, -16f, 0f, 0.12f),
            new BodyKey(1f, Ease.InOut, 0f, 0.06f, -10f, -4f, -6f, 0.8f, 0.6f, 6f, 0f, 0.05f),
        };

        static readonly LegKey[] LeapNearFront =
        {
            LegKey.Polar(0.12f, Ease.Snap, -6f, 1f, 12f),
            LegKey.Polar(0.55f, Ease.InOut, -16f, 1f, 6f),
            LegKey.Polar(1f, Ease.InOut, -56f, 0.98f, 0f),
        };

        static readonly LegKey[] LeapFarFront =
        {
            LegKey.Polar(0.12f, Ease.Snap, 6f, 1f, 12f),
            LegKey.Polar(0.55f, Ease.InOut, -4f, 1f, 6f),
            LegKey.Polar(1f, Ease.InOut, -44f, 0.98f, 0f),
        };

        static readonly LegKey[] LeapNearHind =
        {
            LegKey.Polar(0.12f, Ease.Snap, -138f, 1f, 30f),
            LegKey.Polar(0.55f, Ease.InOut, -128f, 0.98f, 20f),
            LegKey.Polar(1f, Ease.InOut, -96f, 0.9f, -10f),
        };

        static readonly LegKey[] LeapFarHind =
        {
            LegKey.Polar(0.12f, Ease.Snap, -128f, 1f, 30f),
            LegKey.Polar(0.55f, Ease.InOut, -118f, 0.98f, 20f),
            LegKey.Polar(1f, Ease.InOut, -86f, 0.9f, -10f),
        };

        // Đáp đất nặng: 4 chân đập xuống, thân nhún sâu, đầu gục, bờm / đuôi văng theo → nảy lên → về
        static readonly BodyKey[] LandBody =
        {
            new BodyKey(0.14f, Ease.Out, 0.06f, -0.5f, -7f, -10f, -18f, 0.6f, 0.9f, 12f, 0f),
            new BodyKey(0.4f, Ease.InOut, 0.03f, -0.22f, -2f, -4f, -8f, 0.2f, 0.4f, -6f, 0f),
            new BodyKey(0.68f, Ease.InOut, 0f, 0.04f, 1f, 1f, 2f, 0f, 0.1f, 0f, 0f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] LandLeg = { LegKey.Ground(0.12f, Ease.Out) };

        // Chồm đứng bằng 2 chân sau (báo trước vả vuốt): nép xuống lấy đà → vươn đứng thẳng, chân trước gần giơ cao quá
        // đầu, chân trước xa co trước ngực, đầu gầm → giữ, nhấp nhô nhẹ (dáng như khung 2 hàng 2 tranh mẫu)
        static readonly BodyKey[] RearBody =
        {
            new BodyKey(0.22f, Ease.InOut, 0.05f, -0.2f, -4f, -6f, -10f, 0.2f, 0.3f, 10f, 0.4f),
            new BodyKey(0.62f, Ease.Out, -0.28f, -0.18f, 56f, 8f, -42f, 1f, 0.8f, -12f, 0.2f),
            new BodyKey(0.8f, Ease.InOut, -0.3f, -0.16f, 60f, 10f, -44f, 1f, 0.9f, -15f, 0f),
            new BodyKey(1f, Ease.InOut, -0.29f, -0.17f, 58f, 9f, -43f, 1f, 0.85f, -14f, 0f),
        };

        static readonly LegKey[] RearNearFront =
        {
            LegKey.Ground(0.22f, Ease.InOut, 0.05f),
            LegKey.Polar(0.62f, Ease.Out, 110f, 0.95f, -20f, 0.1f),
            LegKey.Polar(0.8f, Ease.InOut, 116f, 0.96f, -26f),
            LegKey.Polar(1f, Ease.InOut, 113f, 0.95f, -23f),
        };

        static readonly LegKey[] RearFarFront =
        {
            LegKey.Ground(0.22f, Ease.InOut),
            LegKey.Polar(0.62f, Ease.Out, -28f, 0.56f, -50f, 0.1f),
            LegKey.Polar(1f, Ease.InOut, -32f, 0.55f, -55f),
        };

        static readonly LegKey[] RearHind = { LegKey.Ground(0.62f, Ease.InOut, 0.12f) };

        // Vả vuốt 1 (chân trước gần): giơ vuốt cao ra sau đầu (lấy đà) → vả chéo xuống phía trước thật nhanh (móng qua
        // trước mặt ở k ≈ 0.42–0.5), thân chồm theo → quá đà, móng gần chạm đất → thu về dáng nửa chồm (nối được vả 2)
        static readonly BodyKey[] SwipeBody =
        {
            new BodyKey(0.3f, Ease.InOut, -0.25f, -0.15f, 48f, 12f, -30f, 1f, 0.6f, -10f, 0.3f),
            new BodyKey(0.46f, Ease.Snap, 0.22f, -0.24f, 16f, -14f, -14f, 1f, 0.9f, 10f, 0.6f),
            new BodyKey(0.62f, Ease.Out, 0.3f, -0.28f, 10f, -18f, -16f, 0.9f, 0.7f, 14f, 0.4f),
            new BodyKey(1f, Ease.InOut, 0.12f, -0.12f, 22f, -4f, -16f, 0.7f, 0.5f, 0f, 0.2f),
        };

        static readonly LegKey[] SwipeStrike =
        {
            LegKey.Polar(0.3f, Ease.InOut, 118f, 0.95f, -25f, 0.15f),
            LegKey.Polar(0.46f, Ease.Snap, -30f, 1f, 20f),
            LegKey.Polar(0.62f, Ease.Out, -62f, 0.95f, 10f),
            LegKey.Polar(1f, Ease.InOut, -42f, 0.85f, -10f),
        };

        static readonly LegKey[] SwipeGuard =
        {
            LegKey.Polar(0.3f, Ease.InOut, 12f, 0.6f, -40f),
            LegKey.Polar(0.46f, Ease.Snap, -40f, 0.75f, -20f),
            LegKey.Polar(0.62f, Ease.Out, -70f, 0.85f, -10f),
            LegKey.Polar(1f, Ease.InOut, -56f, 0.8f, -20f),
        };

        // Vả vuốt 2 (chân trước xa): giơ cao → vả xuống phía trước, thân chúi theo nhiều hơn → quá đà → gần như đứng lại
        static readonly BodyKey[] SwipeAltBody =
        {
            new BodyKey(0.28f, Ease.InOut, -0.18f, -0.14f, 40f, 14f, -26f, 1f, 0.6f, -8f, 0.3f),
            new BodyKey(0.44f, Ease.Snap, 0.3f, -0.26f, 8f, -16f, -10f, 1f, 1f, 12f, 0.6f),
            new BodyKey(0.62f, Ease.Out, 0.36f, -0.3f, 4f, -20f, -14f, 0.9f, 0.7f, 16f, 0.4f),
            new BodyKey(1f, Ease.InOut, 0.1f, -0.08f, 3f, -5f, -8f, 0.4f, 0.4f, 0f, 0.1f),
        };

        static readonly LegKey[] SwipeAltStrike =
        {
            LegKey.Polar(0.28f, Ease.InOut, 124f, 0.95f, -25f, 0.15f),
            LegKey.Polar(0.44f, Ease.Snap, -24f, 1f, 20f),
            LegKey.Polar(0.62f, Ease.Out, -64f, 0.95f, 10f),
            LegKey.Ground(1f, Ease.InOut, 0f, 0f, 0.12f),
        };

        static readonly LegKey[] SwipeAltGuard =
        {
            LegKey.Polar(0.28f, Ease.InOut, -18f, 0.6f, -40f),
            LegKey.Polar(0.44f, Ease.Snap, -60f, 0.9f, -20f),
            LegKey.Polar(0.62f, Ease.Out, -80f, 0.95f, -10f),
            LegKey.Ground(1f, Ease.InOut, 0f, 0f, 0.08f),
        };

        // Gầm: hít vào cúi đầu → ngửa đầu há miệng gầm, ngực ưỡn, bờm bùng, đuôi vểnh (đầu rung trong lúc gầm) → về
        static readonly BodyKey[] RoarBody =
        {
            new BodyKey(0.18f, Ease.InOut, -0.1f, -0.14f, -3f, -8f, -16f, 0f, 0.2f, 0f, 0.2f),
            new BodyKey(0.32f, Ease.Out, 0.08f, 0.02f, 3f, 16f, 26f, 1f, 1f, 18f, 0f),
            new BodyKey(0.82f, Ease.Linear, 0.06f, 0f, 3f, 14f, 22f, 1f, 1f, 16f, 0f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] RoarFront = { LegKey.Ground(0.3f, Ease.InOut, 0.06f, 0f, 0.08f), LegKey.Ground(1f) };

        // Lắc bờm (phóng cầu lửa): cúi đầu → lắc đầu và bờm dữ dội (bờm bùng, lửa văng) → về
        static readonly BodyKey[] ShakeBody =
        {
            new BodyKey(0.15f, Ease.InOut, -0.05f, -0.1f, -2f, -6f, -12f, 0.4f, 0.4f, 5f, 0.3f),
            new BodyKey(0.25f, Ease.InOut, 0f, -0.08f, 0f, -2f, -4f, 0.6f, 0.9f, 10f, 0.5f, 0f, 1f),
            new BodyKey(0.78f, Ease.Linear, 0f, -0.08f, 0f, -2f, -4f, 0.6f, 1f, 10f, 0.5f, 0f, 1f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        // Lấy đà húc: hạ thấp, cúi gằm đầu, đuôi quất; chân trước gần cào đất 2 lần (nhấc tới → cào ra sau). Giữ dáng
        static readonly BodyKey[] WindupBody =
        {
            new BodyKey(0.2f, Ease.InOut, -0.18f, -0.26f, -5f, -8f, -24f, 0.2f, 0.4f, 12f, 1f),
            new BodyKey(1f, Ease.Linear, -0.22f, -0.3f, -6f, -9f, -26f, 0.5f, 0.6f, 14f, 1f),
        };

        static readonly LegKey[] WindupScrape =
        {
            LegKey.Ground(0.25f, Ease.InOut, 0.32f, 0.26f),
            LegKey.Ground(0.4f, Ease.In, -0.38f, 0.02f),
            LegKey.Ground(0.55f, Ease.Out, 0.32f, 0.26f),
            LegKey.Ground(0.7f, Ease.In, -0.38f, 0.02f),
            LegKey.Ground(0.86f, Ease.InOut, 0f, 0f, 0.12f),
        };

        // Húc lao: cúi gằm đầu, thân hạ thấp chúi tới, há miệng; chân theo nhịp phi (AI cho chạy nhanh)
        static readonly BodyKey[] ChargeBody =
        {
            new BodyKey(0.08f, Ease.Out, 0.1f, -0.16f, -5f, -6f, -26f, 0.6f, 0.7f, -6f, 0f),
            new BodyKey(1f, Ease.Linear, 0.1f, -0.16f, -5f, -6f, -26f, 0.6f, 0.7f, -6f, 0f),
        };

        // Trúng đòn nặng: giật lùi, ngửa đầu gầm đau → chùng xuống → về
        static readonly BodyKey[] HurtBody =
        {
            new BodyKey(0.14f, Ease.Snap, -0.32f, 0.05f, 9f, 10f, 22f, 1f, 0.7f, 16f, 0.6f),
            new BodyKey(0.45f, Ease.Out, -0.16f, -0.08f, -3f, -3f, -8f, 0.5f, 0.3f, 0f, 0.2f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        // Choáng gục: khuỵu → đổ sụp xuống sàn, chân bè ra, đầu gục sát đất, đuôi rũ → nằm (đầu lắc lư choáng) → đứng dậy.
        // hold = nằm luôn (dừng ở StaggerHoldK)
        static readonly BodyKey[] StaggerBody =
        {
            new BodyKey(0.1f, Ease.Out, -0.12f, -0.25f, -6f, -6f, -20f, 1f, 0.4f, 5f, 0f),
            new BodyKey(0.28f, Ease.InOut, -0.05f, -1.02f, 6f, -4f, -40f, 0f, -0.3f, -25f, 0f),
            new BodyKey(0.84f, Ease.Linear, -0.05f, -1f, 6f, -4f, -39f, 0f, -0.3f, -25f, 0f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        const float StaggerHoldK = 0.5f;

        static readonly LegKey[] StaggerNearFront =
        {
            LegKey.Ground(0.28f, Ease.InOut, 0.5f, 0f), LegKey.Ground(0.84f, Ease.Linear, 0.5f, 0f), LegKey.Ground(1f),
        };

        static readonly LegKey[] StaggerFarFront =
        {
            LegKey.Ground(0.28f, Ease.InOut, 0.55f, 0.02f), LegKey.Ground(0.84f, Ease.Linear, 0.55f, 0.02f), LegKey.Ground(1f),
        };

        static readonly LegKey[] StaggerNearHind =
        {
            LegKey.Ground(0.28f, Ease.InOut, -0.15f, 0f), LegKey.Ground(0.84f, Ease.Linear, -0.15f, 0f), LegKey.Ground(1f),
        };

        static readonly LegKey[] StaggerFarHind =
        {
            LegKey.Ground(0.28f, Ease.InOut, 0.3f, 0.02f), LegKey.Ground(0.84f, Ease.Linear, 0.3f, 0.02f), LegKey.Ground(1f),
        };

        // Chết: ngửa đầu gầm đau → khuỵu → đổ sụp xuống (nảy nhẹ) → nằm im; lửa tắt dần. Luôn giữ dáng cuối
        static readonly BodyKey[] DieBody =
        {
            new BodyKey(0.12f, Ease.Out, -0.1f, 0f, 6f, 8f, 18f, 1f, 0.6f, 10f, 0f),
            new BodyKey(0.35f, Ease.InOut, -0.12f, -0.4f, -4f, -6f, -16f, 0.6f, 0.2f, -10f, 0f),
            new BodyKey(0.6f, Ease.In, -0.05f, -1.08f, 5f, -6f, -44f, 0f, -0.6f, -30f, 0f),
            new BodyKey(0.72f, Ease.Out, -0.05f, -1f, 6f, -4f, -40f, 0f, -0.7f, -28f, 0f),
            new BodyKey(1f, Ease.InOut, -0.05f, -1.05f, 5f, -5f, -46f, 0f, -0.8f, -30f, 0f),
        };

        static readonly LegKey[] DieNearFront = { LegKey.Ground(0.35f, Ease.InOut, 0.15f), LegKey.Ground(0.6f, Ease.In, 0.55f) };
        static readonly LegKey[] DieFarFront = { LegKey.Ground(0.35f, Ease.InOut, 0.2f), LegKey.Ground(0.6f, Ease.In, 0.6f, 0.02f) };
        static readonly LegKey[] DieNearHind = { LegKey.Ground(0.6f, Ease.In, -0.2f) };
        static readonly LegKey[] DieFarHind = { LegKey.Ground(0.6f, Ease.In, 0.35f, 0.02f) };

        BodyKey[] BodyKeys(LionPose act)
        {
            switch (act)
            {
                case LionPose.Crouch: return CrouchBody;
                case LionPose.Leap: return LeapBody;
                case LionPose.Land: return LandBody;
                case LionPose.RearUp: return RearBody;
                case LionPose.ClawSwipe: return SwipeBody;
                case LionPose.ClawSwipeAlt: return SwipeAltBody;
                case LionPose.Roar: return RoarBody;
                case LionPose.ManeShake: return ShakeBody;
                case LionPose.ChargeWindup: return WindupBody;
                case LionPose.Charge: return ChargeBody;
                case LionPose.Hurt: return HurtBody;
                case LionPose.Stagger: return StaggerBody;
                case LionPose.Die: return DieBody;
                default: return null;
            }
        }

        LegKey[] LegKeys(LionPose act, int leg)
        {
            switch (act)
            {
                case LionPose.Leap:
                    return leg == NearFront ? LeapNearFront : leg == FarFront ? LeapFarFront : leg == NearHind ? LeapNearHind : LeapFarHind;
                case LionPose.Land:
                    return LandLeg;
                case LionPose.RearUp:
                    return leg == NearFront ? RearNearFront : leg == FarFront ? RearFarFront : RearHind;
                case LionPose.ClawSwipe:
                    return leg == NearFront ? SwipeStrike : leg == FarFront ? SwipeGuard : null;
                case LionPose.ClawSwipeAlt:
                    return leg == FarFront ? SwipeAltStrike : leg == NearFront ? SwipeAltGuard : null;
                case LionPose.Roar:
                    return leg == NearFront || leg == FarFront ? RoarFront : null;
                case LionPose.ChargeWindup:
                    return leg == NearFront ? WindupScrape : null;
                case LionPose.Stagger:
                    return leg == NearFront ? StaggerNearFront : leg == FarFront ? StaggerFarFront : leg == NearHind ? StaggerNearHind : StaggerFarHind;
                case LionPose.Die:
                    return leg == NearFront ? DieNearFront : leg == FarFront ? DieFarFront : leg == NearHind ? DieNearHind : DieFarHind;
                default:
                    return null;
            }
        }

        void TakeSnapshot()
        {
            _from = _shown;
            for (int i = 0; i < legs.Length; i++)
            {
                legs[i].fromAnkle = legs[i].ankle;
                legs[i].fromCurl = legs[i].curl;
            }
        }

        void StartRelease()
        {
            _act = LionPose.None;
            _releasing = true;
            _releaseTime = 0f;
            // Đòn chồm cao / nằm gục thì về lâu hơn chút cho có sức nặng
            float big = Mathf.Max(Mathf.Abs(_from.Tilt) / 60f, Mathf.Abs(_from.Offset.y) / 1f);
            _releaseDuration = settleTime * (1f + Mathf.Clamp01(big));
        }

        void UpdateAct(float dt, bool live)
        {
            if (!live)
                return;
            _flashAge += dt;
            if (_releasing)
            {
                _releaseTime += dt;
                if (_releaseTime >= _releaseDuration)
                    _releasing = false;
            }

            if (_act == LionPose.None)
                return;

            _actTime += dt;
            float holdK = _act == LionPose.Stagger ? StaggerHoldK : 1f;
            if (_actHold && _actTime > _actDuration * holdK)
                _actTime = _actDuration * holdK;
            else if (!_actHold && _actTime >= _actDuration)
            {
                TakeSnapshot();
                StartRelease();
            }
        }

        // Dáng thân frame này: đòn đang diễn, đang hoà về, hoặc đi / đứng (Neutral)
        Look CurrentLook(bool live)
        {
            if (_act != LionPose.None)
            {
                float k = Mathf.Clamp01(_actTime / _actDuration);
                Look look = SampleBody(BodyKeys(_act), k);
                AddActMotion(ref look, k);
                return look;
            }

            if (_releasing)
                return Look.Mix(_from, Neutral, Eased(Ease.InOut, Mathf.Clamp01(_releaseTime / _releaseDuration)));
            return Neutral;
        }

        Look SampleBody(BodyKey[] keys, float k)
        {
            if (keys == null || keys.Length == 0)
                return _from;
            int i = 0;
            while (i < keys.Length - 1 && k > keys[i].K)
                i++;
            bool first = i == 0 || keys[i - 1].K <= _actStartK;
            float k0 = first ? _actStartK : keys[i - 1].K;
            Look a = first ? _from : keys[i - 1].Look;
            BodyKey b = keys[i];
            float x = Eased(b.Ease, Mathf.Clamp01((k - k0) / Mathf.Max(b.K - k0, 1e-4f)));
            return Look.Mix(a, b.Look, x);
        }

        // Phần chuyển động thêm bằng code theo từng đòn (rung, lắc, ngoe nguẩy)
        void AddActMotion(ref Look look, float k)
        {
            switch (_act)
            {
                case LionPose.Crouch:
                {
                    // Ngoe nguẩy mông như mèo sắp vồ
                    float on = Mathf.SmoothStep(0f, 1f, (k - 0.25f) / 0.15f);
                    look.Offset.x += 0.025f * Mathf.Sin(_time * 15f) * on;
                    look.Tilt += 0.8f * Mathf.Sin(_time * 15f + 1f) * on;
                    break;
                }
                case LionPose.Roar:
                {
                    float on = Mathf.Clamp01(Mathf.Min((k - 0.3f) / 0.05f, (0.85f - k) / 0.05f));
                    look.Head += 1.6f * Mathf.Sin(_time * 47f) * on;
                    look.Offset.y += 0.008f * Mathf.Sin(_time * 39f + 1f) * on;
                    break;
                }
                case LionPose.ManeShake:
                {
                    float s = look.Shake;
                    look.Head += 15f * Mathf.Sin(_time * 30f) * s;
                    look.Bend += 3.5f * Mathf.Sin(_time * 30f + 1.2f) * s;
                    look.Offset.x += 0.03f * Mathf.Sin(_time * 30f + 2f) * s;
                    break;
                }
                case LionPose.ChargeWindup:
                    look.Head += 2.5f * Mathf.Sin(_time * 9f) * Mathf.Clamp01(k / 0.2f);
                    break;
                case LionPose.Stagger:
                {
                    // Nằm choáng: đầu lắc lư
                    float on = Mathf.Clamp01(Mathf.Min((k - 0.3f) / 0.1f, (0.84f - k) / 0.08f));
                    if (_actHold)
                        on = Mathf.Clamp01((k - 0.3f) / 0.1f);
                    look.Head += 5f * Mathf.Sin(_time * 2.6f) * on;
                    look.Bend += 1.2f * Mathf.Sin(_time * 2.6f + 0.8f) * on;
                    break;
                }
            }
        }

        // ---------- Thân, đầu, bờm, đuôi ----------

        void PlaceBody(Look look, float dt, bool live)
        {
            float tau = 2f * Mathf.PI;
            float idle = (1f - _move) * (1f - _air) * (_act == LionPose.Die ? 1f - Mathf.Clamp01(_actTime / _actDuration * 2f) : 1f);
            float breath = Mathf.Sin(_time * breathSpeed);
            float walk = _move * (1f - _run);
            float run = _move * _run;

            // Đi: nhún 2 lần mỗi chu kỳ, vai và hông nhấp nhô lệch pha. Phi: cả thân chúi / ngóc, lưng co / duỗi, nảy lên xuống
            Vector2 offset = look.Offset;
            offset.y += breathBob * breath * idle;
            offset.y -= walkBob * (0.5f - 0.5f * Mathf.Cos(2f * tau * _phase)) * walk + 0.05f * walk;
            offset.y += runBob * Mathf.Sin(tau * _phase + 0.6f) * run - 0.1f * run;
            float tilt = look.Tilt;
            tilt += walkRoll * 0.6f * Mathf.Sin(tau * (_phase + 0.25f)) * walk;
            tilt += runPitch * Mathf.Sin(tau * _phase) * run;
            float bend = look.Bend + breathBend * Mathf.Sin(_time * breathSpeed + 0.5f) * idle;
            bend += walkRoll * Mathf.Sin(tau * _phase) * walk;
            bend -= runFlex * Mathf.Sin(tau * _phase + 1.2f) * run;
            float stretch = look.Stretch + 0.08f * Mathf.Sin(tau * _phase + 1.2f) * run;
            // Trên không (không đòn): thân ngả theo vận tốc dọc
            float vy = _rootVelocity.y / Mathf.Max(size, 0.01f);
            tilt += Mathf.Clamp(vy * 1.5f, -12f, 12f) * _air;

            hips.localPosition = hipsRest + offset;
            hips.localRotation = Quaternion.Euler(0f, 0f, hipsAngle + tilt);
            chest.localPosition = new Vector3(chestRest.x + stretch, chestRest.y, 0f);
            chest.localRotation = Quaternion.Euler(0f, 0f, chestAngle + bend);

            // Quán tính: ngực tăng / giảm tốc thì bờm, đuôi văng ngược rồi lắc lại
            Vector2 chestNow = pose.InverseTransformPoint(chest.position);
            if (live && !_hasChest)
            {
                _lastChest = chestNow;
                _chestVelocity = new Vector2(_speed, 0f);
                _hasChest = true;
            }

            if (live)
            {
                Vector2 velocity = (chestNow - _lastChest) / dt + new Vector2(_speed, 0f);
                Vector2 accel = (velocity - _chestVelocity) / dt;
                _chestVelocity = velocity;
                float push = Mathf.Clamp(accel.x * 0.04f - accel.y * 0.03f, -30f, 30f) * inertia;
                _maneSpin += (-push - 90f * _maneSpring - 9f * _maneSpin) * dt;
                _maneSpring += _maneSpin * dt;
                _tailSpin += (-push * 1.4f - 60f * _tailSpring - 7f * _tailSpin) * dt;
                _tailSpring += _tailSpin * dt;
                _maneSpring = Mathf.Clamp(_maneSpring, -14f, 14f);
                _tailSpring = Mathf.Clamp(_tailSpring, -25f, 25f);
            }

            _lastChest = chestNow;

            // Đầu: nhìn quanh chậm khi đứng, nhấp nhô ngược vai khi đi, gục / ngẩng theo đòn
            float head = look.Head + 2f * Mathf.Sin(_time * 0.45f) * idle - 1.6f * Mathf.Sin(2f * tau * _phase) * walk;
            head += -bend * 0.35f + 3f * Mathf.Sin(tau * _phase + 2f) * run;
            neck.localRotation = Quaternion.Euler(0f, 0f, neckAngle + head);

            PlaceMane(look, run);
            PlaceTail(look, tilt, walk, run);
            PlaceFlames(look, run);
        }

        void PlaceMane(Look look, float run)
        {
            float flare = Mathf.Max(look.Mane, 0f) + 0.35f * _enrage;
            float droop = Mathf.Max(-look.Mane, 0f);
            float alive = 1f - _death;
            for (int i = 0; i < mane.bones.Length; i++)
            {
                Transform bone = mane.bones[i];
                if (bone == null)
                    continue;
                if (i == 0)
                    continue; // gốc bờm đứng yên theo ngực
                float seed = i * 1.7f;
                float sway = maneSway * (0.6f * Mathf.Sin(_time * 1.9f + seed) + 0.4f * Mathf.Sin(_time * 3.1f + seed * 2.3f));
                float flick = maneFlicker * (Mathf.Sin(_time * 9.3f + seed * 3.1f) * 0.6f + Mathf.Sin(_time * 14.7f + seed) * 0.4f);
                float sweep = i - 1 < maneSweep.Length ? maneSweep[i - 1] : 1f;
                float angle = (sway + flick * (1f + flare)) * alive + _maneSpring * (0.6f + 0.4f * i / mane.bones.Length);
                angle += sweep * (9f * run + 5f * look.Shake * Mathf.Sin(_time * 30f + i));
                angle += look.Shake * 10f * Mathf.Sin(_time * 30f + i * 1.3f);
                angle += droop * 10f; // rủ: lửa ngả xuống / ra sau
                bone.localRotation = Quaternion.Euler(0f, 0f, mane.rest[i] + angle);
                // Lửa phập phồng: chùm lửa dài ra / co lại chút ít theo nhịp lập loè
                float s = 1f + 0.13f * flare - 0.08f * droop + 0.025f * alive * Mathf.Sin(_time * 7.3f + seed * 2.1f);
                bone.localScale = new Vector3(s, 1f + 0.05f * flare, 1f);
            }
        }

        void PlaceTail(Look look, float tilt, float walk, float run)
        {
            float tau = 2f * Mathf.PI;
            float lash = look.Lash;
            for (int i = 0; i < tail.bones.Length; i++)
            {
                Transform bone = tail.bones[i];
                if (bone == null)
                    continue;
                float lag = i * 0.7f;
                float angle = tailSway * (0.7f * Mathf.Sin(_time * 1.3f - lag) + 0.3f * Mathf.Sin(_time * 2.3f - lag * 1.3f)) * (0.6f + 0.25f * i);
                angle += 7f * Mathf.Sin(tau * _phase - lag) * walk + 10f * Mathf.Sin(tau * _phase - lag) * run;
                angle += 16f * lash * Mathf.Sin(_time * 11f - lag * 1.2f) * (0.5f + 0.3f * i);
                angle += _tailSpring * (0.5f + 0.3f * i);
                if (i == 0)
                    angle += look.Tail - 12f * run - 0.8f * tilt; // thân chồm / chúi thì đuôi vẫn ngả ra sau
                if (_act == LionPose.Die)
                    angle *= 1f - _death;
                bone.localRotation = Quaternion.Euler(0f, 0f, tail.rest[i] + angle);
            }

            // Ngọn đuôi không cắm xuống sàn (thân hạ thấp / chồm đứng): nhấc cả đuôi từ gốc lên vừa đủ
            if (tail.bones.Length < 2 || tail.bones[0] == null || tail.bones[tail.bones.Length - 1] == null)
                return;
            Transform tip = tail.bones[tail.bones.Length - 1];
            Vector2 baseAt = pose.InverseTransformPoint(tail.bones[0].position);
            Vector2 end = pose.InverseTransformPoint(tip.TransformPoint(new Vector3(tuftLength, 0f, 0f)));
            Vector2 tuft = pose.InverseTransformPoint(tip.position);
            const float floor = 0.42f; // chùm lửa dày: giữ trục ngọn cao hơn sàn chừng nửa bề dày chùm
            float deficit = Mathf.Max(floor - end.y, floor + 0.15f - tuft.y);
            if (deficit <= 0f)
                return;
            float r = Mathf.Max((end - baseAt).magnitude, 0.1f);
            float lift = Mathf.Asin(Mathf.Clamp(deficit / r, 0f, 0.9f)) * Mathf.Rad2Deg;
            // Đuôi chĩa ra sau (−x trong Pose): quay ngược chiều kim đồng hồ thì ngọn đi xuống, nên nhấc = quay thuận
            tail.bones[0].localRotation *= Quaternion.Euler(0f, 0f, end.x < baseAt.x ? -lift : lift);
        }

        void PlaceFlames(Look look, float run)
        {
            float flare = Mathf.Max(look.Mane, 0f) + 0.5f * _enrage;
            float alive = 1f - _death;
            for (int f = 0; f < flames.Length; f++)
            {
                Chain chain = flames[f];
                float seed = f * 2.3f;
                for (int i = 0; i < chain.bones.Length; i++)
                {
                    if (chain.bones[i] == null)
                        continue;
                    float wave = flameWave * (Mathf.Sin(_time * 4.2f + seed - i * 0.9f) * 0.7f + Mathf.Sin(_time * 7.9f + seed * 1.7f - i * 1.4f) * 0.3f);
                    float angle = wave * (0.5f + 0.35f * i) * (1f + 0.6f * flare) * alive;
                    angle += look.Shake * 14f * Mathf.Sin(_time * 30f + seed - i);
                    angle += _maneSpring * 0.8f + 6f * run;
                    chain.bones[i].localRotation = Quaternion.Euler(0f, 0f, chain.rest[i] + angle);
                    if (i == 0)
                    {
                        float s = 1f + 0.18f * flare - 0.25f * _death;
                        chain.bones[i].localScale = new Vector3(s, s, 1f);
                    }
                }
            }
        }

        // ---------- Chân ----------

        void PlaceLegs(Look look)
        {
            Matrix4x4 toPose = pose.worldToLocalMatrix;
            for (int i = 0; i < legs.Length; i++)
            {
                Leg leg = legs[i];
                Matrix4x4 parent = toPose * leg.upper.parent.localToWorldMatrix;
                Vector2 root = parent.MultiplyPoint3x4(leg.upper.localPosition);
                float parentAngle = Mathf.Atan2(parent.m10, parent.m00) * Mathf.Rad2Deg;

                Vector2 ground = GaitTarget(leg, i, out float gaitLift);
                Vector2 target = ground;
                float curl = -45f * Mathf.Clamp01(gaitLift / 0.2f);
                ActLeg(leg, i, root, ground, ref target, ref curl);
                leg.ankle = target;
                leg.curl = curl;
                SolveLeg(leg, root, parentAngle, target, curl);

                // Vả vuốt 2: chân trước xa giơ lên thì vẽ trên bờm (sau đầu) cho thấy rõ móng
                if (leg.renderer != null && i == FarFront)
                {
                    bool raised = _act == LionPose.ClawSwipeAlt && _actTime / _actDuration > 0.08f && _actTime / _actDuration < 0.7f;
                    leg.renderer.sortingOrder = raised ? raisedFarLegOrder : leg.sortingOrder;
                }
            }
        }

        // Chỗ đặt cổ chân theo dáng thường: đứng như tranh gốc, đi chéo cặp, phi (2 sau rồi 2 trước), trên không duỗi chân
        Vector2 GaitTarget(Leg leg, int index, out float lift)
        {
            float walkOffset = index == NearFront ? 0f : index == FarHind ? 0.06f : index == FarFront ? 0.5f : 0.56f;
            float runOffset = index == NearHind ? 0f : index == FarHind ? 0.1f : index == FarFront ? 0.48f : 0.58f;
            float stance = Mathf.Lerp(walkStance, runStance, _run);
            float cycle = Mathf.Lerp(walkCycle, runCycle, _run);
            float liftHeight = Mathf.Lerp(walkLift, runLift, _run) * (leg.front ? 1f : 0.85f);
            float u = Mathf.Repeat(_phase + Mathf.Lerp(walkOffset, runOffset, _run), 1f);
            float stride = cycle * stance;
            float x, y = 0f;
            if (u < stance)
            {
                x = stride * (0.5f - u / stance);
            }
            else
            {
                float k = (u - stance) / (1f - stance);
                x = stride * (Mathf.SmoothStep(0f, 1f, k) - 0.5f);
                y = liftHeight * Mathf.Sin(Mathf.PI * k);
            }

            // Khi đi, tâm bước dồn về dưới vai / hông (dáng tranh gốc chân dạng rộng)
            Vector2 center = Vector2.Lerp(leg.rest, leg.walkCenter, walkGather * _move);
            Vector2 target = center + new Vector2(x, y) * _move;
            lift = y * _move;

            if (_air > 0f)
            {
                // Trên không: chân trước vươn tới, chân sau duỗi ra sau
                Vector2 air = leg.front ? leg.rest + new Vector2(0.25f, 0.35f) : leg.rest + new Vector2(-0.35f, 0.3f);
                target = Vector2.Lerp(target, air, _air);
                lift = Mathf.Max(lift, 0.3f * _air);
            }

            return target;
        }

        // Chân theo đòn: có mốc riêng thì theo mốc, không thì đi từ chỗ cũ về chỗ đặt chân thường
        void ActLeg(Leg leg, int index, Vector2 root, Vector2 ground, ref Vector2 target, ref float curl)
        {
            float reach = leg.upperLength + leg.lowerLength;
            if (_act != LionPose.None)
            {
                LegKey[] keys = LegKeys(_act, index);
                float k = Mathf.Clamp01(_actTime / _actDuration);
                if (keys != null)
                {
                    SampleLeg(keys, k, leg, root, ground, reach, out target, out curl);
                    return;
                }

                float w = Eased(Ease.InOut, Mathf.Clamp01(_actTime / legBlend));
                target = Vector2.Lerp(leg.fromAnkle, target, w);
                curl = Mathf.Lerp(leg.fromCurl, curl, w);
                return;
            }

            if (_releasing)
            {
                float w = Eased(Ease.InOut, Mathf.Clamp01(_releaseTime / _releaseDuration));
                Vector2 from = leg.fromAnkle;
                float bump = 0.12f * Mathf.Sin(Mathf.PI * w) * Mathf.Clamp01((from - target).magnitude / 0.3f);
                target = Vector2.Lerp(from, target, w) + Vector2.up * bump;
                curl = Mathf.Lerp(leg.fromCurl, curl, w);
            }
        }

        void SampleLeg(LegKey[] keys, float k, Leg leg, Vector2 root, Vector2 ground, float reach, out Vector2 target, out float curl)
        {
            int i = 0;
            while (i < keys.Length - 1 && k > keys[i].K)
                i++;
            LegKey b = keys[i];
            bool first = i == 0 || keys[i - 1].K <= _actStartK;
            float k0 = first ? _actStartK : keys[i - 1].K;
            float x = Eased(b.Ease, Mathf.Clamp01((k - k0) / Mathf.Max(b.K - k0, 1e-4f)));
            if (k > b.K)
                x = 1f;

            // Điểm đầu đoạn: mốc trước, hoặc chỗ chân lúc gọi đòn
            bool aPolar;
            float aAngle, aReach, aCurl;
            Vector2 aPoint;
            if (first)
            {
                aPoint = leg.fromAnkle;
                Vector2 rel = aPoint - root;
                aAngle = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
                aReach = rel.magnitude / reach;
                aCurl = leg.fromCurl;
                aPolar = b.Mode == LegMode.Polar;
            }
            else
            {
                LegKey a = keys[i - 1];
                aPolar = a.Mode == LegMode.Polar;
                aAngle = a.X;
                aReach = a.Y;
                aCurl = a.Curl;
                aPoint = KeyPoint(a, root, ground, reach);
            }

            curl = Mathf.Lerp(aCurl, b.Curl, x);
            float bump = b.Arc * Mathf.Sin(Mathf.PI * x);
            if (aPolar && b.Mode == LegMode.Polar)
            {
                // Hai mốc đều là góc: móng đi theo cung quanh gốc chân (không đi thẳng)
                float angle = Mathf.Lerp(aAngle, b.X, x);
                float r = Mathf.Lerp(aReach, b.Y, x) * reach;
                target = root + Polar(angle, r) + Vector2.up * bump;
                return;
            }

            target = Vector2.Lerp(aPoint, KeyPoint(b, root, ground, reach), x) + Vector2.up * bump;
            if (b.Mode == LegMode.Ground && x >= 1f)
                target.y = Mathf.Max(target.y, leg.rest.y);
        }

        static Vector2 KeyPoint(in LegKey key, Vector2 root, Vector2 ground, float reach) =>
            key.Mode == LegMode.Polar ? root + Polar(key.X, key.Y * reach) : ground + new Vector2(key.X, key.Y);

        static Vector2 Polar(float degrees, float length)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * length;
        }

        // IK 2 xương trong Pose: đặt cổ chân vào target, khớp (khuỷu / gót) gập về phía sau; bàn chân chạm đất thì phẳng
        // như tranh gốc, nhấc lên thì theo cẳng + gập thêm curl
        void SolveLeg(Leg leg, Vector2 root, float parentAngle, Vector2 target, float curl)
        {
            float l1 = Mathf.Max(leg.upperLength, 1e-4f), l2 = Mathf.Max(leg.lowerLength, 1e-4f);
            Vector2 toGoal = target - root;
            float d = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f);
            float baseAngle = Mathf.Atan2(toGoal.y, toGoal.x);
            float bend = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f));
            float upper = baseAngle - bend; // khớp nằm bên phải (chiều kim đồng hồ) của đường gốc → cổ chân = phía sau
            Vector2 joint = root + new Vector2(Mathf.Cos(upper), Mathf.Sin(upper)) * l1;
            float lower = Mathf.Atan2(target.y - joint.y, target.x - joint.x);
            float upperDeg = upper * Mathf.Rad2Deg, lowerDeg = lower * Mathf.Rad2Deg;

            float planted = 1f - Mathf.Clamp01((target.y - leg.rest.y) / 0.18f);
            float lifted = Mathf.DeltaAngle(0f, lowerDeg + leg.pawBend + curl);
            float paw = Mathf.LerpAngle(lifted, leg.restPawAngle, planted);

            leg.upper.localRotation = Quaternion.Euler(0f, 0f, upperDeg - parentAngle);
            leg.lower.localRotation = Quaternion.Euler(0f, 0f, lowerDeg - upperDeg);
            leg.paw.localRotation = Quaternion.Euler(0f, 0f, Mathf.DeltaAngle(lowerDeg, paw));
        }

        // ---------- Đầu gầm, mắt, hào quang, chớp ----------

        float CurrentFlash()
        {
            float k = Mathf.Clamp01(_flashAge / flashTime);
            return _flash * (1f - k) * (1f - k);
        }

        void UpdateGlow(Look look, float dt, bool live)
        {
            _enrage = live ? Mathf.MoveTowards(_enrage, _enrageTarget, dt * 2f) : _enrageTarget;
            float fadeFrom = Mathf.Max(0.25f, _actStartK);
            float deathTarget = _act == LionPose.Die ? Mathf.Clamp01((_actTime / _actDuration - fadeFrom) / (1f - fadeFrom)) : 0f;
            _death = live ? Mathf.MoveTowards(_death, deathTarget, dt * 3f) : deathTarget;
            float stagger = _act == LionPose.Stagger ? Mathf.Clamp01(_actTime / _actDuration / 0.28f) : 0f;

            // Đầu gầm: hiện đè lên đầu thường rồi mới tắt đầu thường (không thấy 2 đầu mờ chồng nhau)
            float roarTarget = look.Roar > 0.5f ? 1f : 0f;
            _roarShow = live ? Mathf.MoveTowards(_roarShow, roarTarget, dt / Mathf.Max(roarSwap, 0.01f)) : roarTarget;
            if (roarArt != null)
            {
                roarArt.enabled = _roarShow > 0.001f;
                roarArt.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, _roarShow));
            }

            if (headArt != null)
                headArt.enabled = _roarShow < 0.999f;

            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 3.1f);
            float eye = (0.65f + 0.25f * pulse + 0.4f * _enrage + 0.3f * look.Roar) * (1f - _death) * (1f - 0.6f * stagger);
            if (eyeGlow != null)
            {
                eyeGlow.color = new Color(eyeColor.r, eyeColor.g, eyeColor.b, Mathf.Clamp01(eyeColor.a * eye));
                float s = 0.32f * (1f + 0.35f * _enrage + 0.25f * look.Roar + 0.08f * pulse);
                eyeGlow.transform.localScale = new Vector3(s, s, 1f);
            }

            if (aura != null)
            {
                float a = _enrage * (0.75f + 0.25f * Mathf.Sin(_time * 4.3f)) * (1f - _death);
                aura.enabled = a > 0.01f;
                aura.color = new Color(auraColor.r, auraColor.g, auraColor.b, auraColor.a * a);
                float s = 5.2f * (1f + 0.06f * Mathf.Sin(_time * 2.7f));
                aura.transform.localScale = new Vector3(s * 1.25f, s, 1f);
            }

            if (_material == null)
                return;
            float flash = CurrentFlash();
            Color color = Color.Lerp(flashWhite, flashRed, Mathf.Clamp01(_flashAge / (flashTime * 0.35f)));
            color.a = flash * 0.8f; // không phủ kín: vẫn thấy nét vẽ dưới lớp chớp
            _material.SetColor(FlashId, color);
            float ember = 1f + (enrageEmber - 1f) * _enrage * (0.85f + 0.15f * Mathf.Sin(_time * 5f));
            ember *= (1f - _death) * (1f - 0.35f * stagger);
            _material.SetFloat(EmberId, ember);
        }

        // InOut: êm hai đầu. Out: nhanh rồi chậm dần. Snap: vụt cực nhanh, phanh gấp (ra đòn). In: chậm rồi nhanh dần.
        // Back: tới đích lố một chút rồi về
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
                case Ease.Back:
                {
                    float u = x - 1f;
                    return 1f + 2.4f * u * u * u + 1.4f * u * u;
                }
                default:
                    return x;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!HasReferences)
                return;
            Bounds b = VisualBounds;
            Gizmos.color = new Color(1f, 0.4f, 0.2f);
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.DrawWireSphere(MouthPoint, 0.15f);
            Gizmos.DrawWireSphere(PawPoint, 0.15f);
        }
    }
}
