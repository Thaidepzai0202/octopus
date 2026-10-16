using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Hoạt ảnh con nhện gắn xương (Art/Spider/Rig) khi người chơi chọn nhện. Đọc trạng thái từ OctopusController
    /// (cùng cách điều khiển với bạch tuộc):
    ///  - Đứng: thân phập phồng thở, dồn trọng tâm qua lại (móng đứng yên nên chân tự co duỗi), thỉnh thoảng một chân
    ///    nhấc lên gõ xuống, răng nanh mấp máy, mắt sáng nhấp nháy.
    ///  - Bò: dáng đi của nhện thật, 4 chân một nhóm thay nhau (trái 1, 3 + phải 2, 4 rồi tới 4 chân kia). Móng đang
    ///    bám đứng yên trên đất, chân kia nhấc lên vươn tới; thân nhún và nghiêng nhẹ, bụng lắc theo quán tính.
    ///  - Nhảy lên: co chân sát thân. Rơi: xoè chân ra. Đu: nhả tơ từ đầu bụng tới đá, treo người theo sợi tơ.
    ///  - Bám dưới đá: lật ngược người, 8 chân bám mặt dưới đá; vuốt ngang thì bò ngược như trên sàn.
    ///  - Ra đòn (bộ chiêu gọi Act): lấy đà → ra đòn thật nhanh → quá đà → bật lại → về dáng cũ; vuốt chân đi theo cung
    ///    quanh gốc chân, thân chồm / ngả / co giãn theo đòn, bụng lắc theo quán tính. Đổi dáng luôn hoà mượt, không giật.
    ///  - Bắn laze: hạ thấp người, xoạc chân trụ vững, run lên khi vận sức, giật lùi khi tia bắn ra.
    /// Mỗi chân 2 xương (đùi, cẳng); IK đặt móng đúng chỗ, gối luôn nhô lên phía lưng. Tranh nhìn nghiêng sang phải
    /// nên đi sang trái thì quay cả con. Không Play vẫn xem được: chọn Preview rồi bấm ▶ Chạy thử trong Inspector.
    /// </summary>
    [ExecuteAlways]
    public class SpiderRigAnimator : MonoBehaviour, ILaserEye, ICombatAnimator
    {
        public enum Preview { Idle, Walk, Jump, Fall, Swing, Cling, ClingWalk }

        enum Mode { Ground, Air, Swing, Cling }

        [Serializable]
        public class Leg
        {
            public Transform femur;
            public Transform tibia;
            [Tooltip("Đùi: gốc → gối (unit tranh)")]
            public float femurLength;
            [Tooltip("Cẳng: gối → móng (unit tranh)")]
            public float tibiaLength;
            [Tooltip("Chỗ gắn chân trên thân (toạ độ trong Body)")]
            public Vector2 root;
            [Tooltip("Chỗ đặt móng khi đứng (toạ độ trong Pose, mặt đất y = 0)")]
            public Vector2 rest;
            [Tooltip("1 = bên trái tranh, -1 = bên phải")]
            public int side = 1;
            [Tooltip("0 = chân trước … 3 = chân sau")]
            public int slot;

            [NonSerialized] public Vector2 foot;     // móng đang ở đâu (Pose)
            [NonSerialized] public Vector2 air;      // dáng chân trên không (Pose), chuyển mượt
            [NonSerialized] public float lift;       // móng đang nhấc khỏi đất bao nhiêu (unit tranh)
        }

        struct Snapshot
        {
            public Mode Mode;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Facing;
            public Vector2 Anchor;
            public Vector2 ClingPoint;
            public Vector2 ClingNormal;
        }

        [Tooltip("Người chơi (đã nối sẵn)")]
        [SerializeField] OctopusController controller;

        [Header("Xem trước khi không Play (bấm ▶ Chạy thử trong Inspector)")]
        [SerializeField] Preview preview = Preview.Idle;
        [Tooltip("Kéo để xem từng khoảnh khắc khi không chạy thử")]
        [SerializeField, Range(0f, 1f)] float previewTime;
        [Tooltip("Tốc độ bò khi xem trước (unit/giây)")]
        [SerializeField] float previewSpeed = 3f;

        [Header("Kích thước")]
        [SerializeField, Range(0.4f, 2f)] float size = 0.85f;
        [Tooltip("Thời gian quay đầu khi đổi hướng (giây)")]
        [SerializeField, Range(0.05f, 0.6f)] float turnTime = 0.2f;
        [Tooltip("Tâm vật lý cao hơn mặt đất bấy nhiêu (bán kính collider, unit world)")]
        [SerializeField] float groundOffset = 0.5f;
        [Tooltip("Tâm thân cao bấy nhiêu tính từ mặt đất (unit tranh; điểm xoay khi ở trên không / đu)")]
        [SerializeField] float bodyCenter = 0.75f;

        [Header("Bò")]
        [Tooltip("Thân đi được bấy nhiêu (unit tranh) trong một chu kỳ bước (mỗi chân bước một lần). Lớn = sải dài, nhịp chậm")]
        [SerializeField, Min(0.1f)] float cycleDistance = 0.9f;
        [Tooltip("Phần chu kỳ móng bám đất (còn lại là nhấc lên vươn tới). Trên 0.5 thì luôn có chân của cả hai nhóm chạm đất")]
        [SerializeField, Range(0.4f, 0.8f)] float stance = 0.55f;
        [Tooltip("Móng nhấc cao bao nhiêu khi bước (unit tranh)")]
        [SerializeField, Range(0f, 0.5f)] float stepHeight = 0.16f;
        [Tooltip("Chân sau bước trước chân trước một chút (sóng bước chạy từ sau ra trước), tính theo phần chu kỳ")]
        [SerializeField, Range(0f, 0.15f)] float stepWave = 0.05f;
        [Tooltip("Đi nhanh hơn mức này (unit/giây) mới bò; chậm hơn là đứng")]
        [SerializeField] float walkThreshold = 0.15f;
        [Tooltip("Thân nhún xuống mỗi nhịp bước (unit tranh)")]
        [SerializeField, Range(0f, 0.15f)] float walkBob = 0.035f;
        [Tooltip("Thân nghiêng qua lại theo nhóm chân đang bám (độ)")]
        [SerializeField, Range(0f, 10f)] float walkRoll = 2.5f;
        [Tooltip("Thân chúi về phía trước khi bò (độ)")]
        [SerializeField, Range(0f, 15f)] float walkLean = 3f;

        [Header("Đứng yên")]
        [Tooltip("Bụng phồng xẹp như thở (tỉ lệ)")]
        [SerializeField, Range(0f, 0.1f)] float breathAmount = 0.03f;
        [SerializeField] float breathSpeed = 1.8f;
        [Tooltip("Thân dồn trọng tâm qua lại (unit tranh)")]
        [SerializeField, Range(0f, 0.15f)] float swayAmount = 0.04f;
        [SerializeField] float swaySpeed = 0.75f;
        [Tooltip("Thỉnh thoảng một chân nhấc lên gõ xuống: cách nhau bao lâu (giây, ngẫu nhiên trong khoảng)")]
        [SerializeField] Vector2 tapInterval = new Vector2(1.8f, 4.5f);
        [SerializeField, Range(0f, 0.4f)] float tapLift = 0.13f;
        [SerializeField, Range(0.1f, 1f)] float tapTime = 0.45f;

        [Header("Nhảy, rơi, đu")]
        [Tooltip("Nhảy lên thì co chân sát thân: móng nâng lên bấy nhiêu (unit tranh)")]
        [SerializeField, Range(0f, 0.8f)] float tuckHeight = 0.32f;
        [Tooltip("Thân nghiêng theo vận tốc ngang khi bay (độ)")]
        [SerializeField] float airLean = 10f;
        [Tooltip("Chân trên không đổi dáng nhanh / chậm")]
        [SerializeField] float airLegSpeed = 14f;
        [Tooltip("Vừa chạm đất / đá, chân chuyển từ dáng bay sang bám trong thời gian này (giây)")]
        [SerializeField, Range(0.02f, 0.5f)] float landTime = 0.12f;
        [SerializeField] float rotationSmoothing = 12f;
        [Tooltip("Sợi tơ khi đu (đã nối sẵn)")]
        [SerializeField] LineRenderer web;
        [Tooltip("Chỗ tơ nhả ra: đầu bụng (đã nối sẵn)")]
        [SerializeField] Transform spinneret;
        [Tooltip("Tơ bắn từ bụng tới đá trong bấy nhiêu giây")]
        [SerializeField] float webShootTime = 0.1f;

        [Header("Bám ngược dưới đá")]
        [Tooltip("Thời gian lật ngược người khi bám vào đá / rời đá (giây)")]
        [SerializeField, Range(0.05f, 1f)] float hangFlipTime = 0.25f;
        [Tooltip("Móng ép sát vào đá thêm bấy nhiêu (unit tranh) cho khỏi hở với mép đá")]
        [SerializeField, Range(0f, 0.3f)] float clingReach = 0.03f;

        [Header("Bụng, nanh, mắt")]
        [Tooltip("Bụng lắc theo quán tính khi tăng / giảm tốc (độ cho mỗi unit/giây²)")]
        [SerializeField] float abdomenInertia = 4f;
        [SerializeField] float abdomenStiffness = 9f;
        [SerializeField, Range(0.1f, 1.5f)] float abdomenDamping = 0.45f;
        [Tooltip("Răng nanh mấp máy (độ)")]
        [SerializeField, Range(0f, 30f)] float fangTwitch = 12f;
        [SerializeField] Vector2 fangInterval = new Vector2(1.2f, 3.5f);
        [Tooltip("Mắt sáng lên tối xuống")]
        [SerializeField, Range(0f, 0.5f)] float eyePulse = 0.18f;
        [SerializeField] Vector2 eyeFlickerInterval = new Vector2(3f, 7f);
        [Tooltip("Màu mắt lúc sáng nhất")]
        [SerializeField] Color eyeColor = Color.white;
        [Tooltip("Quầng sáng quanh mắt lúc sáng nhất")]
        [SerializeField] Color glowColor = new Color(1f, 0.5f, 1f, 0.5f);
        [Tooltip("Cỡ quầng sáng quanh mắt")]
        [SerializeField] float glowSize = 0.42f;
        [Tooltip("Vận sức / bắn laze thì quầng sáng mắt to thêm bấy nhiêu lần")]
        [SerializeField, Range(0f, 3f)] float laserGlow = 1.2f;

        [Header("Ra đòn")]
        [Tooltip("Vào đòn, nối đòn, hết đòn: dáng cũ hoà sang dáng mới trong bấy nhiêu giây (không giật)")]
        [SerializeField, Range(0.02f, 0.2f)] float actBlend = 0.07f;
        [Tooltip("Bụng lắc theo khi thân chồm tới / giật lùi lúc ra đòn")]
        [SerializeField, Range(0f, 6f)] float actAbdomenKick = 2.5f;

        [Header("Tham chiếu (đã nối sẵn, không cần sửa)")]
        [SerializeField] Transform pose;
        [SerializeField] Transform body;
        [SerializeField] Transform abdomen;
        [SerializeField] Transform head;
        [SerializeField] SpriteRenderer eyes;
        [SerializeField] SpriteRenderer eyeGlow;
        [Tooltip("Trái ngoài, phải ngoài, trái trong, phải trong")]
        [SerializeField] Transform[] fangs = Array.Empty<Transform>();
        [SerializeField] Leg[] legs = Array.Empty<Leg>();

        float _previewDelta;
        Rigidbody2D _rigidbody;
        LaserSkill _laser;
        Mode _mode;
        float _speedX, _accel;
        float _facing = 1f, _flip = 1f, _hang = 1f, _angle;
        float _surfaceBlend = 1f, _clingBlend, _plant = 1f;
        Vector2 _clingOffset;
        float _walkWeight, _phase, _time;
        float _abdomenAngle, _abdomenSpin;
        int _tapLeg = -1;
        float _tapClock, _tapTimer = 1.5f, _tapShift;
        float _fangClock = -1f, _fangTimer = 1f;
        float _flickerClock = -1f, _flickerTimer = 3f;
        float _webTime;
        CombatPose _act;              // dáng ra đòn đang diễn (bộ chiêu gọi Act)
        float _actTime, _actDuration;
        Vector2 _actPoint;            // điểm nhắm (world)
        Vector2 _actPointPose, _actPointBody;
        LungeKind _lunge;
        float _lungeAim;
        Look _shown, _from;           // phần dáng ra đòn đang hiện / chụp lại lúc đổi dáng để hoà sang
        float _blend = 1f;            // đã hoà từ dáng chụp lại được bao lâu (giây)
        bool _hasLook;
        Vector2[] _fromFeet = Array.Empty<Vector2>();
        float[] _fromLift = Array.Empty<float>();
        Matrix4x4 _bodyMatrix = Matrix4x4.identity, _bodyInverse = Matrix4x4.identity;
        Vector2 _lastActOffset, _actVelocity;
        float _lastActTilt, _actTurn;
        float _brace;                 // thế đứng bắn laze (0..1, chuyển mượt)
        float _sinceAct = 10f;        // hết đòn được bao lâu (giây)
        float _airVy, _landClock = -1f, _landPower;
        Mode _lastMode;
        Vector2 _velocity;
        Look _land;                   // đáp đất sau đòn trên không: dẹt xuống, chân bè ra rồi nảy lên
        Vector2 _braceOffset;
        float _braceTilt, _braceSquash, _braceHead, _braceFang, _braceSplay;

        /// <summary>Đang bò (0..1, chuyển mượt).</summary>
        public float WalkWeight => _walkWeight;

        /// <summary>Hướng đang quay trong tranh: 1 = phải, -1 = trái.</summary>
        public float Facing => _facing;

        public int LegCount => legs.Length;

        /// <summary>Móng chân i (world).</summary>
        public Vector2 FootPosition(int i) => pose.TransformPoint(legs[i].foot);

        /// <summary>Móng chân i đang nhấc khỏi đất / đá bao nhiêu (unit tranh); 0 = đang bám.</summary>
        public float FootLift(int i) => legs[i].lift;

        /// <summary>Gối chân i (world).</summary>
        public Vector2 KneePosition(int i) => legs[i].tibia.position;

        /// <summary>Đầu mút xương cẳng chân i (world): chỗ móng thật sự nằm sau khi IK.</summary>
        public Vector2 TipPosition(int i) => legs[i].tibia.TransformPoint(new Vector3(legs[i].tibiaLength, 0f, 0f));

        public Transform PoseTransform => pose;
        public LineRenderer Web => web;

        /// <summary>Tâm cụm mắt (world): chỗ tia laze bắn ra.</summary>
        public Vector2 EyePosition => eyes != null ? (Vector2)eyes.transform.position : (Vector2)transform.position;

        /// <summary>Editor gọi mỗi frame khi bấm ▶ Chạy thử (không Play): cho thời gian trôi để xem chuyển động.</summary>
        public void TickPreview(float deltaTime) => _previewDelta = Mathf.Clamp(deltaTime, 0f, 0.05f);

        bool HasReferences
        {
            get
            {
                if (pose == null || body == null || legs == null)
                    return false;
                foreach (Leg leg in legs)
                {
                    if (leg == null || leg.femur == null || leg.tibia == null)
                        return false;
                }

                return true;
            }
        }

        void LateUpdate()
        {
            if (!HasReferences)
                return;

            bool playing = Application.isPlaying;
            if (playing && Time.deltaTime <= 0f)
                return; // khựng hình khi đánh trúng (hit-stop) / tạm dừng: giữ nguyên dáng đang hiện, đòn cũng dừng theo
            float dt = playing ? Mathf.Min(Time.deltaTime, 0.05f) : _previewDelta;
            _previewDelta = 0f;
            bool live = dt > 0f; // thời gian có trôi (Play hoặc ▶ Chạy thử); không thì là ảnh tĩnh theo Preview Time

            Snapshot snap = Capture(playing);
            _mode = snap.Mode;
            if (live)
            {
                _blend += dt;
                if (_act != CombatPose.None)
                {
                    _actTime += dt;
                    if (_actTime >= _actDuration)
                    {
                        TakeSnapshot(); // hết đòn: hoà về dáng thường
                        _act = CombatPose.None;
                    }
                }
            }
            UpdateMotion(snap, dt, live);
            UpdateLanding(snap, dt, live);
            UpdateLook(dt, live);
            PlacePose(snap, dt, live);
            PlaceBody(live, dt);
            for (int i = 0; i < legs.Length; i++)
                SolveLeg(legs[i], FootTarget(legs[i], i, snap, dt, live));
            UpdateWeb(snap, dt, live);
            UpdateFangs(dt, live);
            UpdateEyes(dt, live);
            _hasLook = playing;
        }

        // ---------- Trạng thái ----------

        Snapshot Capture(bool playing)
        {
            var snap = new Snapshot { Position = transform.position, Facing = 1f, ClingNormal = Vector2.down };
            if (playing && controller != null)
            {
                if (_rigidbody == null)
                    _rigidbody = controller.GetComponent<Rigidbody2D>();
                if (_laser == null)
                    _laser = controller.GetComponent<LaserSkill>();
                snap.Velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;
                snap.Facing = controller.Facing;
                switch (controller.State)
                {
                    case OctopusState.Grounded:
                        snap.Mode = Mode.Ground;
                        break;
                    case OctopusState.Swinging:
                        snap.Mode = Mode.Swing;
                        snap.Anchor = controller.Swing.AnchorPoint;
                        break;
                    case OctopusState.Clinging:
                        snap.Mode = Mode.Cling;
                        snap.ClingPoint = controller.Mover.ClingPoint;
                        snap.ClingNormal = controller.Mover.ClingNormal;
                        snap.Velocity = controller.Mover.ClingVelocity;
                        break;
                    default:
                        snap.Mode = Mode.Air;
                        break;
                }

                return snap;
            }

            // Xem trước trong Editor
            switch (preview)
            {
                case Preview.Walk:
                    snap.Velocity = new Vector2(previewSpeed, 0f);
                    break;
                case Preview.Jump:
                    snap.Mode = Mode.Air;
                    snap.Velocity = new Vector2(2f, 9f);
                    break;
                case Preview.Fall:
                    snap.Mode = Mode.Air;
                    snap.Velocity = new Vector2(2f, -12f);
                    break;
                case Preview.Swing:
                    snap.Mode = Mode.Swing;
                    snap.Anchor = snap.Position + new Vector2(1.6f, 3.6f);
                    snap.Velocity = new Vector2(6f, -2f);
                    break;
                case Preview.Cling:
                case Preview.ClingWalk:
                    snap.Mode = Mode.Cling;
                    snap.ClingPoint = snap.Position + Vector2.up * groundOffset;
                    snap.Velocity = preview == Preview.ClingWalk ? new Vector2(previewSpeed, 0f) : Vector2.zero;
                    break;
            }

            return snap;
        }

        // Trục x của tranh (world) khi treo ngược dưới mặt đá có pháp tuyến normal (đúng góc xoay Pose lúc bám)
        static Vector2 ClingAxis(Vector2 normal) =>
            Quaternion.Euler(0f, 0f, Mathf.Atan2(-normal.y, -normal.x) * Mathf.Rad2Deg - 90f) * Vector2.right;

        void UpdateMotion(Snapshot snap, float dt, bool live)
        {
            bool surface = snap.Mode is Mode.Ground or Mode.Cling;
            float facing = snap.Facing < 0f ? -1f : 1f;
            float along; // vận tốc dọc mặt đang bám, theo trục x của tranh chưa lật
            if (snap.Mode == Mode.Cling)
            {
                // Treo ngược: trục x tranh nằm dọc mặt đá; vuốt sang phải là đi theo hướng "sang phải" của mặt đá
                Vector2 axis = ClingAxis(snap.ClingNormal);
                facing *= Mathf.Sign(Vector2.Dot(axis, SurfaceMover.SurfaceTangent(snap.ClingNormal)) + 1e-4f);
                along = Vector2.Dot(snap.Velocity, axis);
            }
            else
            {
                along = snap.Velocity.x;
            }

            float previous = _speedX;
            _speedX = along;
            _velocity = snap.Velocity;
            _accel = live ? Mathf.Lerp(_accel, (along - previous) / Mathf.Max(dt, 1e-4f), 1f - Mathf.Exp(-10f * dt)) : 0f;
            _facing = facing;
            _flip = live ? Mathf.MoveTowards(_flip, _facing, dt * 2f / Mathf.Max(turnTime, 0.01f)) : _facing;

            float walk = surface && Mathf.Abs(along) > walkThreshold ? 1f : 0f;
            _walkWeight = live ? Mathf.MoveTowards(_walkWeight, walk, dt * 5f) : walk;

            // Nhịp bước theo quãng đường đã đi, nên móng đang bám đứng yên dù đi nhanh hay chậm
            float artSpeed = surface ? Mathf.Abs(along) / Mathf.Max(size, 0.01f) : 0f;
            _phase = live ? Mathf.Repeat(_phase + artSpeed * dt / cycleDistance, 1f) : previewTime;
            _time = live ? _time + dt : previewTime * 10f;

            float target = surface ? 1f : 0f;
            _surfaceBlend = live ? Mathf.MoveTowards(_surfaceBlend, target, dt * 10f) : target;
            _plant = live ? Mathf.MoveTowards(_plant, target, dt / Mathf.Max(landTime, 0.01f)) : target;
            float cling = snap.Mode == Mode.Cling ? 1f : 0f;
            _clingBlend = live ? Mathf.MoveTowards(_clingBlend, cling, dt * 8f) : cling;
            if (snap.Mode == Mode.Cling)
                _clingOffset = snap.ClingPoint - snap.Position - snap.ClingNormal * (clingReach * size);
        }

        void PlacePose(Snapshot snap, float dt, bool live)
        {
            float target;
            switch (snap.Mode)
            {
                case Mode.Swing:
                {
                    Vector2 toAnchor = snap.Anchor - snap.Position;
                    target = Mathf.Atan2(toAnchor.y, toAnchor.x) * Mathf.Rad2Deg - 90f; // treo theo sợi tơ
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
            float spin = _facing * _shown.Spin; // lộn vòng khi ra đòn (Pose đã lật theo hướng quay nên nhân hướng)
            _hang = live
                ? Mathf.MoveTowards(_hang, snap.Mode == Mode.Cling ? -1f : 1f, dt * 2f / Mathf.Max(hangFlipTime, 0.01f))
                : snap.Mode == Mode.Cling ? -1f : 1f;

            // Quay đầu / lật ngược: lật qua 0 (bóp dẹt thoáng qua). Tranh nhìn nghiêng sang phải
            float flip = Mathf.Sign(_flip) * Mathf.Max(Mathf.Abs(_flip), 0.3f);
            float hang = Mathf.Sign(_hang) * Mathf.Max(Mathf.Abs(_hang), 0.3f);
            var scale = new Vector2(flip * size, hang * size);

            // Điểm neo q (unit tranh) đặt vào vị trí at (world, tính từ tâm vật lý): chân khi đứng / bám (mặt đất hoặc
            // mặt đá ở y = 0 của tranh), tâm thân khi bay / đu / lộn vòng
            var feet = new Vector2(0f, -groundOffset);
            var center = new Vector2(0f, bodyCenter);
            Vector2 q = Vector2.Lerp(Vector2.Lerp(center, Vector2.zero, _surfaceBlend), center, _shown.Pivot);
            Vector2 at = Vector2.Lerp(feet + q * size, _clingOffset, _clingBlend);
            Quaternion rotation = Quaternion.Euler(0f, 0f, _angle + spin);
            pose.localPosition = at - (Vector2)(rotation * Vector2.Scale(scale, q));
            pose.localRotation = rotation;
            pose.localScale = new Vector3(scale.x, scale.y, 1f);
        }

        void PlaceBody(bool live, float dt)
        {
            float idle = (1f - _walkWeight) * _surfaceBlend;
            float tau = 2f * Mathf.PI;
            float breath = Mathf.Sin(_time * breathSpeed);
            Vector2 offset = new Vector2(swayAmount * Mathf.Sin(_time * swaySpeed) * idle, 0.012f * breath * idle);
            offset.y -= walkBob * (0.5f - 0.5f * Mathf.Cos(2f * tau * _phase)) * _walkWeight; // nhún mỗi khi một nhóm chân chạm đất
            float roll = walkRoll * Mathf.Sin(tau * _phase) * _walkWeight;
            float lean = -walkLean * _walkWeight;
            // Ra đòn (chồm, ngả, co giãn) + thế đứng bắn laze. Thân co giãn quanh gốc Body (mặt đất) nên dẹt là hạ xuống
            Vector2 actOffset = _shown.Offset + _braceOffset + _land.Offset;
            float actTilt = _shown.Tilt + _braceTilt;
            float squash = _shown.Squash + _braceSquash + _land.Squash;
            // Ngả quanh điểm sau thân ngang đầu: chồm lên thì đầu ngóc cao (đứng trên chân sau), chúi xuống thì đầu gục tới
            Vector2 pivotShift = TiltPivot - (Vector2)(Quaternion.Euler(0f, 0f, actTilt) * TiltPivot);
            body.localPosition = offset + actOffset + pivotShift;
            body.localRotation = Quaternion.Euler(0f, 0f, roll + lean + actTilt);
            body.localScale = new Vector3(1f - 0.6f * squash, 1f + squash, 1f);
            _bodyMatrix = Matrix4x4.TRS(body.localPosition, body.localRotation, body.localScale);
            _bodyInverse = _bodyMatrix.inverse;
            if (_act == CombatPose.Reach)
            {
                _actPointPose = pose.InverseTransformPoint(_actPoint);
                _actPointBody = _bodyInverse.MultiplyPoint3x4(_actPointPose);
            }

            // Bụng: lò xo góc, bị hất ngược khi tăng tốc, nảy nhẹ theo nhịp bước; phập phồng như thở. Ra đòn: thân
            // chồm tới / giật lùi / ngả đột ngột thì bụng bị hất theo quán tính rồi lắc lại (động tác theo sau)
            if (abdomen != null)
            {
                float target = -_accel * Mathf.Sign(_flip) * abdomenInertia / Mathf.Max(size, 0.01f) * 0.1f +
                               1.5f * Mathf.Sin(2f * tau * _phase + 1f) * _walkWeight + _shown.Abdomen;
                if (live)
                {
                    Vector2 velocity = (actOffset - _lastActOffset) / dt;
                    float turn = (actTilt - _lastActTilt) / dt;
                    _abdomenSpin += Mathf.Clamp((velocity.x - _actVelocity.x) * actAbdomenKick - (turn - _actTurn) * 0.05f * actAbdomenKick,
                        -160f, 160f);
                    _actVelocity = velocity;
                    _actTurn = turn;
                    _abdomenSpin += (abdomenStiffness * abdomenStiffness * (target - _abdomenAngle) -
                                     2f * abdomenDamping * abdomenStiffness * _abdomenSpin) * dt;
                    _abdomenAngle += _abdomenSpin * dt;
                }
                else
                {
                    _abdomenAngle = target;
                    _abdomenSpin = 0f;
                }

                abdomen.localRotation = Quaternion.Euler(0f, 0f, _abdomenAngle);
                float puff = breathAmount * breath + _shown.Pump;
                abdomen.localScale = new Vector3(1f + puff * 0.6f, 1f + puff, 1f);
            }

            _lastActOffset = actOffset;
            _lastActTilt = actTilt;

            // Đầu: nhìn quanh chậm rãi khi đứng, hơi cúi về phía trước khi bò; ra đòn thì ngóc / gục theo đòn
            if (head != null)
                head.localRotation = Quaternion.Euler(0f, 0f,
                    2.5f * Mathf.Sin(_time * 0.55f) * idle - 2f * _walkWeight + _shown.Head + _braceHead);
        }

        // ---------- Chân ----------

        // Chỗ đặt móng (Pose): trên mặt đất / đá thì theo nhịp bước (hoặc gõ chân khi đứng), trên không thì co / xoè
        Vector2 FootTarget(Leg leg, int index, Snapshot snap, float dt, bool live)
        {
            float lift;
            Vector2 ground = SurfaceTarget(leg, index, out lift);
            if (index == 0)
                UpdateTap(dt, live);

            Vector2 air = AirTarget(leg, snap);
            leg.air = live ? Vector2.Lerp(leg.air, air, 1f - Mathf.Exp(-airLegSpeed * dt)) : air;
            Vector2 foot = Vector2.Lerp(leg.air, ground, _plant);
            if (_plant >= 1f)
                leg.air = foot; // rời mặt đất thì chân bắt đầu co / xoè từ đúng dáng đang đứng

            foot = ActFoot(leg, foot, ref lift);
            if (_blend < actBlend && index < _fromFeet.Length)
            {
                // Vừa đổi dáng: móng đi mượt từ chỗ cũ sang chỗ mới
                float w = BlendWeight;
                foot = Vector2.Lerp(_fromFeet[index], foot, w);
                lift = Mathf.Max(lift, _fromLift[index] * (1f - w));
            }

            leg.foot = foot;
            leg.lift = _plant >= 1f ? lift : Mathf.Max(lift, 1f - _plant);
            return foot;
        }

        // ---------- Ra đòn (bộ chiêu) ----------
        //
        // Mỗi dáng ra đòn là một chuỗi mốc theo nhịp hoạt hình: lấy đà → ra đòn thật nhanh (chậm dần khi tới đích) → quá
        // đà → bật lại / giữ → về dáng cũ (hơi lố rồi dừng). Thân dời / ngả / co giãn theo mốc (BodyKey); chân ra đòn đi
        // theo cung quanh gốc chân (LegKey: góc + độ vươn) chứ không đi thẳng. Đổi dáng giữa chừng (đòn mới chen vào, hết
        // đòn) thì chụp lại dáng đang hiện rồi hoà sang dáng mới trong actBlend giây nên không giật. Mọi thứ tính trong
        // Pose (phía trước là +x vì Pose đã lật theo hướng quay) nên quay trái, treo ngược, đang bay đều đúng.

        enum Ease : byte { Linear, InOut, Out, Snap, In, Back }

        // Cắn có lấy đà (combo) / vồ xuống ngoạm luôn (vồ mồi đáp đất) / đạp bay (phi tơ tới nơi) / giật ngược sợi tơ về
        // phía mình (siết kén, giật lưới)
        enum LungeKind : byte { Bite, Impact, Kick, Yank }

        // Phần dáng do ra đòn thêm vào (mặc định 0 = không ra đòn)
        struct Look
        {
            public Vector2 Offset;   // thân dời (unit tranh, +x = phía trước)
            public float Tilt;       // thân ngả (độ, + = ngóc đầu lên)
            public float Squash;     // < 0 = dẹt xuống bè ra, > 0 = vươn cao thon lại
            public float Head;       // đầu ngẩng (+) / gục (-) thêm (độ)
            public float Fang;       // nanh mở bao nhiêu
            public float FangWeight; // 1 = nanh theo đòn, 0 = nanh tự mấp máy
            public float Spin;       // cả con lộn vòng (độ, âm = lộn về phía trước)
            public float Pivot;      // điểm xoay: 0 = chân, 1 = tâm thân
            public float Pump;       // bụng phồng thêm (đẻ nhện con)
            public float Abdomen;    // bụng chúc / vểnh thêm (độ, đích của lò xo bụng; âm = chúc tới trước)

            public static Look Mix(in Look a, in Look b, float t) => new Look
            {
                Offset = Vector2.Lerp(a.Offset, b.Offset, t),
                Tilt = Mathf.Lerp(a.Tilt, b.Tilt, t),
                Squash = Mathf.Lerp(a.Squash, b.Squash, t),
                Head = Mathf.Lerp(a.Head, b.Head, t),
                Fang = Mathf.Lerp(a.Fang, b.Fang, t),
                FangWeight = Mathf.Lerp(a.FangWeight, b.FangWeight, t),
                Spin = Mathf.LerpAngle(a.Spin, b.Spin, t),
                Pivot = Mathf.Lerp(a.Pivot, b.Pivot, t),
                Pump = Mathf.Lerp(a.Pump, b.Pump, t),
                Abdomen = Mathf.Lerp(a.Abdomen, b.Abdomen, t),
            };
        }

        // Mốc dáng thân tại k (0..1 của đòn); đoạn từ mốc trước tới mốc này đi theo kiểu Ease
        readonly struct BodyKey
        {
            public readonly float K;
            public readonly Ease Ease;
            public readonly Vector2 Offset;
            public readonly float Tilt, Squash, Head, Fang, Abdomen;

            public BodyKey(float k, Ease ease, float x, float y, float tilt, float squash, float head, float fang, float abdomen = 0f)
            {
                K = k;
                Ease = ease;
                Offset = new Vector2(x, y);
                Tilt = tilt;
                Squash = squash;
                Head = head;
                Fang = fang;
                Abdomen = abdomen;
            }
        }

        // Mốc móng chân ra đòn: góc (độ quanh gốc chân trong Body; 0 = phía trước, 90 = lên trên) và độ vươn (unit tranh).
        // Reach = 0: đúng chỗ móng đang đặt (đứng / bò / bay). Arc: đoạn tới mốc này võng ra bấy nhiêu (giữa hai mốc có
        // góc thì vươn thêm, âm = co vào; có một đầu là dáng gốc thì nhấc cao lên). Aim: góc tính từ hướng tới điểm nhắm,
        // độ vươn không quá khoảng cách tới điểm nhắm.
        readonly struct LegKey
        {
            public readonly float K;
            public readonly Ease Ease;
            public readonly float Angle, Reach, Arc;
            public readonly bool Aim;

            public LegKey(float k, Ease ease, float angle, float reach, float arc = 0f, bool aim = false)
            {
                K = k;
                Ease = ease;
                Angle = angle;
                Reach = reach;
                Arc = arc;
                Aim = aim;
            }

            public static LegKey Rest(float k, Ease ease = Ease.InOut, float arc = 0f) => new LegKey(k, ease, 0f, 0f, arc);
        }

        static readonly BodyKey Still = new BodyKey(0f, Ease.Linear, 0f, 0f, 0f, 0f, 0f, 0f);

        // Cào 1 (0.3 s, trúng ở 0.09 s): ngả ra sau giơ vuốt cao cong như lưỡi hái (0.05 s), gồng thêm một nhịp → chém xéo
        // xuống thật nhanh (chạm ở ~0.085 s), thân chồm theo, vuốt cắm xuống đất phía trước → bật lại → về
        static readonly BodyKey[] WhipBody =
        {
            Still,
            new BodyKey(0.17f, Ease.InOut, -0.1f, 0.05f, 11f, 0.06f, 8f, 0.5f),
            new BodyKey(0.24f, Ease.InOut, -0.11f, 0.06f, 12f, 0.07f, 9f, 0.6f),
            new BodyKey(0.34f, Ease.Snap, 0.15f, -0.06f, -10f, -0.1f, -9f, 1f),
            new BodyKey(0.46f, Ease.Out, 0.18f, -0.07f, -12f, -0.12f, -10f, 0.8f),
            new BodyKey(0.64f, Ease.InOut, 0.1f, -0.03f, -6f, -0.03f, -5f, 0.3f),
            new BodyKey(1f, Ease.Back, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] WhipLeg =
        {
            LegKey.Rest(0f),
            new LegKey(0.08f, Ease.In, 50f, 0.85f),
            new LegKey(0.17f, Ease.Out, 100f, 1.2f),
            new LegKey(0.24f, Ease.InOut, 107f, 1.15f),
            new LegKey(0.34f, Ease.Snap, -16f, 1.45f, 0.12f),
            new LegKey(0.46f, Ease.Out, -32f, 1.32f),
            new LegKey(0.64f, Ease.InOut, -27f, 1.24f),
            LegKey.Rest(0.95f, Ease.InOut, 0.16f),
            LegKey.Rest(1f),
        };

        // Cào 2: hạ thấp, kéo vuốt chân trước bên kia ra sau sát đất → vung vòng cung hất ngược lên, thân ngửa theo
        static readonly BodyKey[] WhipAltBody =
        {
            Still,
            new BodyKey(0.17f, Ease.InOut, -0.07f, -0.09f, -9f, -0.11f, -8f, 0.4f),
            new BodyKey(0.24f, Ease.InOut, -0.08f, -0.1f, -10f, -0.12f, -9f, 0.5f),
            new BodyKey(0.38f, Ease.Snap, 0.12f, 0.09f, 12f, 0.1f, 10f, 1.1f),
            new BodyKey(0.5f, Ease.Out, 0.13f, 0.11f, 14f, 0.12f, 11f, 0.9f),
            new BodyKey(0.66f, Ease.InOut, 0.08f, 0.06f, 7f, 0.05f, 6f, 0.4f),
            new BodyKey(1f, Ease.Back, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] WhipAltLeg =
        {
            LegKey.Rest(0f),
            new LegKey(0.17f, Ease.InOut, -160f, 0.9f, 0.1f),
            new LegKey(0.24f, Ease.InOut, -165f, 0.92f),
            new LegKey(0.38f, Ease.Snap, 62f, 1.45f, -0.45f),
            new LegKey(0.5f, Ease.Out, 78f, 1.32f),
            new LegKey(0.66f, Ease.InOut, 72f, 1.22f),
            LegKey.Rest(0.95f, Ease.InOut, 0.14f),
            LegKey.Rest(1f),
        };

        // Nện: giơ cả hai chân trước lên cao → nện xuống đất phía trước, thân dẹt xuống theo
        static readonly BodyKey[] SlamBody =
        {
            Still,
            new BodyKey(0.4f, Ease.InOut, -0.06f, 0.12f, 14f, 0.06f, 6f, 1f),
            new BodyKey(0.52f, Ease.Snap, 0.12f, -0.1f, -10f, -0.16f, -8f, 0.4f),
            new BodyKey(0.64f, Ease.Out, 0.1f, -0.06f, -6f, -0.06f, -4f, 0.3f),
            new BodyKey(1f, Ease.Back, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] SlamLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.4f, Ease.InOut, 75f, 1.25f),
            new LegKey(0.52f, Ease.Snap, -35f, 1.3f),
            new LegKey(0.8f, Ease.InOut, -30f, 1.22f),
            LegKey.Rest(1f, Ease.InOut, 0.12f),
        };

        static readonly LegKey[] SlamOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.4f, Ease.InOut, 110f, 1.25f),
            new LegKey(0.52f, Ease.Snap, -20f, 1.3f),
            new LegKey(0.8f, Ease.InOut, -18f, 1.22f),
            LegKey.Rest(1f, Ease.InOut, 0.12f),
        };

        // Chồm rồi phun / quăng tơ (bắn tơ 0.35 s, lưới khổng lồ 0.5 s; tơ phóng ra ở k ≈ 0.33): nép xuống lấy đà → đứng
        // lên trên chân sau, hai chân trước giơ cao chụm trên đầu (gom tơ), nanh banh → chúi người hất tới, bụng chúc theo,
        // hai chân trước quăng xuống phía trước → giật lại → về
        static readonly BodyKey[] RearBody =
        {
            Still,
            new BodyKey(0.12f, Ease.InOut, 0.04f, -0.12f, -6f, -0.14f, -5f, 0.4f),
            new BodyKey(0.28f, Ease.Out, -0.1f, 0.28f, 32f, 0.11f, 13f, 1.4f, 6f),
            new BodyKey(0.37f, Ease.Snap, 0.16f, 0.02f, -8f, -0.08f, -10f, 1.5f, -14f),
            new BodyKey(0.5f, Ease.Out, 0.1f, 0.04f, -4f, -0.03f, -5f, 0.9f, -6f),
            new BodyKey(0.8f, Ease.InOut, 0.02f, 0.01f, 1f, 0.01f, 0f, 0.4f),
            new BodyKey(1f, Ease.Back, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] RearLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.12f, Ease.InOut, -70f, 0.55f),
            new LegKey(0.28f, Ease.Out, 78f, 1.3f, 0.1f),
            new LegKey(0.37f, Ease.Snap, -8f, 1.42f),
            new LegKey(0.5f, Ease.Out, -20f, 1.32f),
            LegKey.Rest(0.85f, Ease.InOut, 0.12f),
            LegKey.Rest(1f),
        };

        static readonly LegKey[] RearOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.12f, Ease.InOut, -110f, 0.55f),
            new LegKey(0.28f, Ease.Out, 98f, 1.3f, 0.1f),
            new LegKey(0.37f, Ease.Snap, 20f, 1.4f),
            new LegKey(0.5f, Ease.Out, 6f, 1.3f),
            LegKey.Rest(0.85f, Ease.InOut, 0.12f),
            LegKey.Rest(1f),
        };

        // Cặp chân thứ hai nhấc lên lúc đứng trên chân sau (bên kia lật đối xứng)
        static readonly LegKey[] RearSecond =
        {
            LegKey.Rest(0f),
            LegKey.Rest(0.12f),
            new LegKey(0.28f, Ease.Out, 8f, 1.2f),
            LegKey.Rest(0.45f, Ease.InOut, 0.1f),
            LegKey.Rest(1f),
        };

        // Kén tơ (0.8 s, lưới bung ra ở 0.3 s): nép xuống → đứng thẳng lên thật cao trên chân sau, hai chân trước dang rộng
        // giơ cao, nanh banh (gầm) → lưới bung: cả người vươn bật lên, chân trước xoè thêm → giữ, gồng run → hạ xuống nhún
        static readonly BodyKey[] RearTallBody =
        {
            Still,
            new BodyKey(0.12f, Ease.InOut, 0.04f, -0.13f, -6f, -0.15f, -5f, 0.4f),
            new BodyKey(0.32f, Ease.Out, -0.12f, 0.38f, 40f, 0.13f, 16f, 1.6f, 10f),
            new BodyKey(0.38f, Ease.Snap, -0.08f, 0.46f, 36f, 0.18f, 12f, 1.6f, 14f),
            new BodyKey(0.5f, Ease.Out, -0.1f, 0.4f, 38f, 0.12f, 14f, 1.5f, 10f),
            new BodyKey(0.8f, Ease.InOut, -0.09f, 0.37f, 37f, 0.1f, 13f, 1.4f, 8f),
            new BodyKey(0.92f, Ease.In, 0.04f, -0.07f, -4f, -0.12f, -4f, 0.4f),
            new BodyKey(1f, Ease.Out, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] RearTallLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.12f, Ease.InOut, -70f, 0.55f),
            new LegKey(0.32f, Ease.Out, 30f, 1.38f, 0.1f),
            new LegKey(0.38f, Ease.Snap, 14f, 1.45f),
            new LegKey(0.5f, Ease.Out, 25f, 1.36f),
            new LegKey(0.8f, Ease.InOut, 27f, 1.34f),
            LegKey.Rest(0.92f, Ease.In, 0.15f),
            LegKey.Rest(1f),
        };

        static readonly LegKey[] RearTallOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.12f, Ease.InOut, -110f, 0.55f),
            new LegKey(0.32f, Ease.Out, 92f, 1.36f, 0.1f),
            new LegKey(0.38f, Ease.Snap, 104f, 1.44f),
            new LegKey(0.5f, Ease.Out, 95f, 1.36f),
            new LegKey(0.8f, Ease.InOut, 94f, 1.34f),
            LegKey.Rest(0.92f, Ease.In, 0.15f),
            LegKey.Rest(1f),
        };

        static readonly LegKey[] RearTallSecond =
        {
            LegKey.Rest(0f),
            LegKey.Rest(0.12f),
            new LegKey(0.32f, Ease.Out, 10f, 1.22f),
            new LegKey(0.38f, Ease.Snap, 2f, 1.28f),
            new LegKey(0.8f, Ease.InOut, 8f, 1.2f),
            LegKey.Rest(0.92f, Ease.In, 0.12f),
            LegKey.Rest(1f),
        };

        // Cắn (combo đòn 3, 0.42 s, trúng ở 0.14 s): lùi lấy đà ngóc đầu, nanh hé → phóng tới nanh banh → ngoạm: nanh sập,
        // hai chân trước quắp lấy → giữ → lùi về
        static readonly BodyKey[] BiteBody =
        {
            Still,
            new BodyKey(0.19f, Ease.InOut, -0.18f, -0.07f, 11f, -0.09f, 8f, 0.9f),
            new BodyKey(0.31f, Ease.Snap, 0.52f, -0.02f, -10f, 0.12f, -4f, 1.5f, 6f),
            new BodyKey(0.37f, Ease.Out, 0.58f, -0.08f, -15f, -0.07f, -11f, 0f, -4f),
            new BodyKey(0.55f, Ease.InOut, 0.44f, -0.04f, -9f, -0.02f, -6f, 0.15f),
            new BodyKey(0.9f, Ease.InOut, -0.03f, 0f, 1.5f, 0.01f, 1f, 0.1f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] BiteLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.19f, Ease.InOut, 25f, 0.85f, 0.1f),
            new LegKey(0.31f, Ease.Snap, -5f, 1.42f),
            new LegKey(0.37f, Ease.Out, -32f, 1.3f),
            new LegKey(0.55f, Ease.InOut, -30f, 1.22f),
            LegKey.Rest(0.92f, Ease.InOut, 0.12f),
            LegKey.Rest(1f),
        };

        static readonly LegKey[] BiteOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.19f, Ease.InOut, 130f, 0.9f, 0.1f),
            new LegKey(0.31f, Ease.Snap, 70f, 1.38f),
            new LegKey(0.37f, Ease.Out, 48f, 1.3f),
            new LegKey(0.55f, Ease.InOut, 58f, 1.2f),
            LegKey.Rest(0.92f, Ease.InOut, 0.12f),
            LegKey.Rest(1f),
        };

        // Vồ xuống (vồ mồi đáp đất, trúng ngay lúc gọi): dẹt xuống bè chân ra, hai chân trước móc quặp lấy mục tiêu, nanh
        // banh → ngoạm → nảy lên → về
        static readonly BodyKey[] ImpactBody =
        {
            Still,
            new BodyKey(0.1f, Ease.Out, 0.18f, -0.14f, -7f, -0.22f, -6f, 1.5f, 8f),
            new BodyKey(0.28f, Ease.Snap, 0.32f, -0.05f, -12f, 0.05f, -10f, 0f, -4f),
            new BodyKey(0.46f, Ease.Out, 0.24f, 0.02f, -6f, 0.04f, -5f, 0.15f),
            new BodyKey(1f, Ease.Back, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] ImpactLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.1f, Ease.Out, 15f, 1.4f),
            new LegKey(0.28f, Ease.Snap, -42f, 1.24f),
            new LegKey(0.5f, Ease.InOut, -36f, 1.2f),
            LegKey.Rest(1f, Ease.InOut, 0.12f),
        };

        static readonly LegKey[] ImpactOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.1f, Ease.Out, 85f, 1.32f),
            new LegKey(0.28f, Ease.Snap, 40f, 1.3f),
            new LegKey(0.5f, Ease.InOut, 52f, 1.2f),
            LegKey.Rest(1f, Ease.InOut, 0.12f),
        };

        // Cú đạp bay (phi tơ tới nơi, đang bay): hai chân trước đạp thẳng vào điểm nhắm, thân vươn dài → lộn một vòng theo
        // đà (động tác theo sau) → về
        static readonly BodyKey[] KickBody =
        {
            Still,
            new BodyKey(0.12f, Ease.Snap, 0.3f, 0.02f, -10f, 0.14f, -6f, 1.5f, 8f),
            new BodyKey(0.3f, Ease.Out, 0.32f, 0f, -12f, 0.04f, -8f, 0.3f),
            new BodyKey(0.62f, Ease.InOut, 0.12f, 0f, -4f, 0.02f, -2f, 0.2f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] KickLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.12f, Ease.Snap, 0f, 1.46f, 0f, true),
            new LegKey(0.3f, Ease.Out, -12f, 1.36f, 0f, true),
            LegKey.Rest(0.8f, Ease.InOut, 0.1f),
            LegKey.Rest(1f),
        };

        static readonly LegKey[] KickOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.12f, Ease.Snap, 14f, 1.4f, 0f, true),
            new LegKey(0.3f, Ease.Out, 4f, 1.32f, 0f, true),
            LegKey.Rest(0.8f, Ease.InOut, 0.1f),
            LegKey.Rest(1f),
        };

        // Giật tơ (siết kén, giật lưới về): chồm tới bám tơ → giật ngược người ra sau, thân hạ thấp, hai chân trước kéo tơ
        // vòng lên qua đầu ra sau → về
        static readonly BodyKey[] YankBody =
        {
            Still,
            new BodyKey(0.16f, Ease.InOut, 0.1f, -0.08f, -8f, -0.1f, -6f, 0.6f),
            new BodyKey(0.34f, Ease.Snap, -0.3f, -0.04f, 14f, 0.04f, 10f, 1.4f, 6f),
            new BodyKey(0.5f, Ease.Out, -0.34f, -0.05f, 16f, 0.03f, 11f, 1.1f, 4f),
            new BodyKey(1f, Ease.Back, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] YankLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.16f, Ease.InOut, 0f, 1.42f),
            new LegKey(0.34f, Ease.Snap, 112f, 1.05f, 0.1f),
            new LegKey(0.5f, Ease.Out, 118f, 1f),
            LegKey.Rest(1f, Ease.InOut, 0.12f),
        };

        static readonly LegKey[] YankOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.16f, Ease.InOut, 40f, 1.36f),
            new LegKey(0.34f, Ease.Snap, 135f, 0.95f, 0.1f),
            new LegKey(0.5f, Ease.Out, 140f, 0.92f),
            LegKey.Rest(1f, Ease.InOut, 0.12f),
        };

        // Bật đi (vồ mồi 0.6 s bật ở 0.07 s, phi tơ 0.45 s lao ở 0.12 s): nép sát đất như lò xo nén, co hai chân trước →
        // phóng thẳng hai chân về điểm nhắm, thân vươn dài, nanh banh → giữ suốt lúc bay (chân sau quặp ra sau) → thả về
        static readonly BodyKey[] ReachBody =
        {
            Still,
            new BodyKey(0.13f, Ease.InOut, -0.1f, -0.17f, 7f, -0.2f, 6f, 0.6f),
            new BodyKey(0.27f, Ease.Snap, 0.16f, 0.05f, -4f, 0.14f, -3f, 1.5f, 6f),
            new BodyKey(0.8f, Ease.InOut, 0.12f, 0.03f, -3f, 0.1f, -2f, 1.3f, 4f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] ReachLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.13f, Ease.InOut, 118f, 0.82f),
            new LegKey(0.27f, Ease.Snap, 0f, 1.46f, 0f, true),
            new LegKey(0.8f, Ease.InOut, 3f, 1.42f, 0f, true),
            LegKey.Rest(1f, Ease.InOut, 0.12f),
        };

        static readonly LegKey[] ReachOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.13f, Ease.InOut, 128f, 0.78f),
            new LegKey(0.27f, Ease.Snap, 12f, 1.36f, 0f, true),
            new LegKey(0.8f, Ease.InOut, 14f, 1.32f, 0f, true),
            LegKey.Rest(1f, Ease.InOut, 0.12f),
        };

        // Lộn vòng (0.5 s, bật lên ở 0.05 s, trúng ở 0.12 s và 0.3 s): nép xuống co chân, ngả ra sau lấy đà → bật lên chân
        // vẫn co → lộn tới thật nhanh, 8 chân duỗi thẳng ra tứ phía như ngôi sao → chậm dần, lố một chút rồi về
        static readonly BodyKey[] SpinBody =
        {
            Still,
            new BodyKey(0.1f, Ease.InOut, 0f, -0.1f, 0f, -0.16f, 0f, 0.6f),
            new BodyKey(0.2f, Ease.Snap, 0f, 0.04f, 0f, 0.12f, 0f, 1.2f),
            new BodyKey(0.72f, Ease.InOut, 0f, 0.01f, 0f, 0.03f, 0f, 1f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        // Góc duỗi chân khi lộn vòng (trong Body, chân phía mặt; bên kia lật đối xứng), theo slot trước → sau
        static readonly float[] SpinFan = { -70f, -30f, 12f, 48f };

        // Đẻ nhện con (0.6 s, nhện con ra từ 0.25 s, cứ 0.06 s một con): đứng lên trên chân sau, hai chân trước giơ cao, nanh
        // banh, bụng phồng to (hít vào) → gồng → ập xuống ép dẹt, chân bè ra → nảy nhẹ → giữ, bụng bơm từng nhịp
        static readonly BodyKey[] SquashBody =
        {
            Still,
            new BodyKey(0.2f, Ease.InOut, -0.08f, 0.26f, 26f, 0.12f, 12f, 1.5f, 12f),
            new BodyKey(0.27f, Ease.InOut, -0.08f, 0.28f, 28f, 0.13f, 13f, 1.6f, 14f),
            new BodyKey(0.38f, Ease.Snap, 0.02f, -0.17f, -6f, -0.22f, -7f, 1.2f, -6f),
            new BodyKey(0.46f, Ease.Out, 0.01f, -0.12f, -3f, -0.13f, -4f, 1f, -3f),
            new BodyKey(0.85f, Ease.InOut, 0f, -0.13f, -3f, -0.15f, -3f, 0.8f, -3f),
            new BodyKey(1f, Ease.Back, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        static readonly LegKey[] SquashLead =
        {
            LegKey.Rest(0f),
            new LegKey(0.2f, Ease.Out, 60f, 1.25f, 0.1f),
            new LegKey(0.27f, Ease.InOut, 64f, 1.22f),
            new LegKey(0.38f, Ease.Snap, -40f, 1.3f),
            new LegKey(0.85f, Ease.InOut, -38f, 1.28f),
            LegKey.Rest(1f, Ease.InOut, 0.1f),
        };

        static readonly LegKey[] SquashOther =
        {
            LegKey.Rest(0f),
            new LegKey(0.2f, Ease.Out, 115f, 1.2f, 0.1f),
            new LegKey(0.27f, Ease.InOut, 118f, 1.18f),
            new LegKey(0.38f, Ease.Snap, -150f, 1.25f),
            new LegKey(0.85f, Ease.InOut, -152f, 1.24f),
            LegKey.Rest(1f, Ease.InOut, 0.1f),
        };

        // Đáp đất ngay sau đòn trên không (lộn vòng, đạp bay…), theo tỉ lệ 0..1 của landTime đòn: dẹt xuống, chân bè ra →
        // nảy lên → về
        static readonly BodyKey[] LandBody =
        {
            Still,
            new BodyKey(0.18f, Ease.Out, 0f, -0.1f, -3f, -0.2f, -3f, 0f),
            new BodyKey(0.5f, Ease.Out, 0f, 0.03f, 1f, 0.06f, 1f, 0f),
            new BodyKey(1f, Ease.InOut, 0f, 0f, 0f, 0f, 0f, 0f),
        };

        const float LandBounceTime = 0.3f;

        // Điểm sau thân ngang đầu (Body): thân ngả quanh đây khi ra đòn
        static readonly Vector2 TiltPivot = new Vector2(-0.5f, 0.6f);

        /// <summary>Diễn dáng ra đòn trong duration giây; point = điểm nhắm (world) cho cào / móc / phi tơ / cắn.</summary>
        public void Act(CombatPose act, float duration, Vector2 point)
        {
            TakeSnapshot(); // dáng đang hiện (kể cả đòn trước chưa xong) hoà sang đòn mới
            _act = act;
            _actTime = 0f;
            _actDuration = Mathf.Max(0.05f, duration);
            _actPoint = point;
            if (act != CombatPose.Lunge)
                return;

            // Điểm cắn sát thân = giật sợi tơ về; đòn dài = cắn có lấy đà; đòn ngắn lúc đang bay / lao vụt tới = đạp bay;
            // còn lại (vừa đáp đất) = vồ xuống ngoạm luôn
            _lunge = ((Vector2)transform.position - point).sqrMagnitude < 0.45f * 0.45f ? LungeKind.Yank
                : _actDuration >= 0.36f ? LungeKind.Bite
                : Airborne || Mathf.Abs(_speedX) > 10f ? LungeKind.Kick : LungeKind.Impact;
            _landClock = -1f; // vồ xuống có nhún riêng
            Vector2 local = pose != null ? (Vector2)pose.InverseTransformPoint(point) - HeadPoint : Vector2.right;
            _lungeAim = Mathf.Clamp(Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg, -20f, 20f);
        }

        // Đang bay theo trạng thái người chơi ngay lúc này (dáng của rig cập nhật cuối frame nên có thể chậm một nhịp)
        bool Airborne => controller != null && Application.isPlaying ? controller.State == OctopusState.Airborne : _mode == Mode.Air;

        float ActProgress => _actDuration > 0f ? Mathf.Clamp01(_actTime / _actDuration) : 1f;

        float BlendWeight => Mathf.SmoothStep(0f, 1f, _blend / Mathf.Max(actBlend, 1e-3f));

        // Tâm đầu (Pose / Body lúc đứng yên): gốc để tính hướng nhắm
        static Vector2 HeadPoint => new Vector2(0.12f, 0.66f);

        // Chụp lại dáng đang hiện để hoà sang dáng mới
        void TakeSnapshot()
        {
            // Đang ở dáng thường thì khỏi chụp: mốc đầu của đòn đã đi ra từ đúng dáng đang đứng / bò / bay
            if (!_hasLook || !Application.isPlaying || (_act == CombatPose.None && _blend >= actBlend))
            {
                _blend = actBlend;
                return;
            }

            _from = _shown;
            if (_fromFeet.Length != legs.Length)
            {
                _fromFeet = new Vector2[legs.Length];
                _fromLift = new float[legs.Length];
            }

            for (int i = 0; i < legs.Length; i++)
            {
                _fromFeet[i] = legs[i].foot;
                _fromLift[i] = legs[i].lift;
            }

            _blend = 0f;
        }

        // Dáng ra đòn frame này (hoà từ dáng chụp lại nếu vừa đổi) + thế đứng bắn laze
        void UpdateLook(float dt, bool live)
        {
            Look look = ActLook();
            _shown = _blend < actBlend ? Look.Mix(_from, look, BlendWeight) : look;

            // Laze: vận sức thì hạ thấp, xoạc chân, run dần lên; tia bắn ra thì giật lùi, ngóc đầu rồi rung đều
            float charge = 0f, fire = -1f;
            bool on = false;
            if (_laser != null && Application.isPlaying)
            {
                if (_laser.Phase == LaserPhase.Charging)
                {
                    on = true;
                    charge = _laser.PhaseProgress;
                }
                else if (_laser.Phase == LaserPhase.Firing)
                {
                    on = true;
                    fire = _laser.PhaseProgress * _laser.PhaseDuration;
                }
            }

            _brace = live ? Mathf.MoveTowards(_brace, on ? 1f : 0f, dt / 0.15f) : 0f;
            float b = Mathf.SmoothStep(0f, 1f, _brace);
            float shake = (fire >= 0f ? 0.012f : 0.004f + 0.012f * charge) * b;
            _braceOffset = new Vector2(-0.04f - 0.04f * charge, -0.1f - 0.04f * charge) * b +
                           new Vector2(Mathf.Sin(_time * 61f), 0.6f * Mathf.Sin(_time * 47f + 1f)) * shake;
            _braceTilt = -3f * b;
            _braceSquash = (-0.1f - 0.04f * charge) * b;
            _braceHead = -3f * b;
            _braceFang = 1f * b;
            _braceSplay = 0.13f * b;
            if (fire >= 0f)
            {
                float kick = (1f - Mathf.Exp(-fire * 60f)) * Mathf.Exp(-fire * 8f) * b;
                _braceOffset.x -= 0.22f * kick;
                _braceTilt += 10f * kick;
                _braceHead += 6f * kick;
                _braceSquash += 0.12f * kick;
            }
        }

        // Phần dáng thân do đòn đang diễn thêm vào
        Look ActLook()
        {
            var look = new Look();
            if (_act == CombatPose.None || !Application.isPlaying)
                return look;

            float k = ActProgress;
            BodyKey[] keys = BodyKeys();
            if (keys != null)
                Sample(keys, k, ref look);
            float enter = Mathf.Clamp01(_actTime / 0.04f); // nanh, điểm xoay chuyển sang theo đòn trong vài frame đầu
            look.FangWeight = enter;
            switch (_act)
            {
                case CombatPose.Spin:
                    look.Spin = SpinAngle(k);
                    look.Pivot = enter;
                    break;
                case CombatPose.Reach:
                {
                    // Thân ngả theo hướng phi tơ
                    float hold = Mathf.SmoothStep(0f, 1f, (k - 0.13f) / 0.14f) * Mathf.SmoothStep(0f, 1f, (1f - k) / 0.2f);
                    Vector2 to = _actPointPose - HeadPoint;
                    look.Tilt += Mathf.Clamp(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, -35f, 35f) * 0.35f * hold;
                    break;
                }
                case CombatPose.Lunge when _lunge != LungeKind.Yank:
                    look.Offset = Quaternion.Euler(0f, 0f, _lungeAim * 0.6f) * look.Offset; // lao về phía điểm cắn
                    if (_lunge == LungeKind.Kick)
                    {
                        // Đạp xong lộn một vòng theo đà
                        look.Spin = k < 0.15f ? 0f : -360f * Eased(Ease.InOut, Mathf.Clamp01((k - 0.15f) / 0.6f));
                        look.Pivot = enter;
                    }
                    break;
                case CombatPose.Rear when _actDuration > 0.6f:
                {
                    // Chiêu cuối: gồng run người trong lúc đứng thẳng
                    float hold = Mathf.Clamp01(Mathf.Min((k - 0.4f) / 0.05f, (0.8f - k) / 0.05f));
                    look.Offset += new Vector2(Mathf.Sin(_time * 53f), Mathf.Sin(_time * 41f + 1f)) * (0.012f * hold);
                    break;
                }
                case CombatPose.Rear:
                {
                    // Lưới khổng lồ (đòn dài hơn bắn tơ) chồm cao hơn, quăng mạnh hơn
                    float amount = 1f + 0.2f * Mathf.Clamp01((_actDuration - 0.35f) / 0.15f);
                    look.Offset *= amount;
                    look.Tilt *= amount;
                    break;
                }
                case CombatPose.Squash:
                {
                    // Hít vào: bụng phồng to; ép xuống thì xẹp, rồi bơm một nhịp cho mỗi con nhện con
                    float swell = 0.28f * Mathf.SmoothStep(0f, 1f, k / 0.25f) * (1f - Mathf.SmoothStep(0f, 1f, (k - 0.34f) / 0.05f));
                    float s = k > 0.38f && k < 0.88f ? Mathf.Sin(Mathf.PI * (k - 0.38f) / 0.1f) : 0f;
                    look.Pump = swell + 0.12f * s * s;
                    break;
                }
            }

            // Đang bay vụt đi (vồ mồi, phi tơ): thân vươn dài, chúc theo hướng bay
            if (_act == CombatPose.Reach || (_act == CombatPose.Lunge && _lunge == LungeKind.Kick))
            {
                float flight = FlightWeight;
                float heading = Mathf.Atan2(_velocity.y, _velocity.x * _facing) * Mathf.Rad2Deg;
                look.Squash += 0.08f * flight;
                look.Tilt += Mathf.Clamp(heading, -30f, 30f) * 0.5f * flight;
            }

            return look;
        }

        // Đang bay vụt đi bao nhiêu (0..1): trên không và nhanh
        float FlightWeight => _mode == Mode.Air ? Mathf.SmoothStep(0f, 1f, (_velocity.magnitude - 4f) / 6f) : 0f;

        // Đáp đất ngay sau đòn trên không (lộn vòng, đạp bay…): dẹt xuống, chân bè ra rồi nảy lên. Không đòn thì đáp như cũ
        void UpdateLanding(Snapshot snap, float dt, bool live)
        {
            _land = default;
            if (!live || !Application.isPlaying)
                return;

            _sinceAct = _act != CombatPose.None ? 0f : _sinceAct + dt;
            if (snap.Mode == Mode.Air)
                _airVy = snap.Velocity.y;
            else if (_lastMode == Mode.Air && snap.Mode == Mode.Ground && _sinceAct < 0.6f &&
                     _act is CombatPose.None or CombatPose.Spin or CombatPose.Whip or CombatPose.WhipAlt)
            {
                _landClock = 0f;
                _landPower = Mathf.Clamp(-_airVy / 9f, 0.35f, 1f);
            }

            _lastMode = snap.Mode;
            if (_landClock < 0f)
                return;

            _landClock += dt;
            float k = _landClock / LandBounceTime;
            if (k >= 1f)
            {
                _landClock = -1f;
                return;
            }

            Sample(LandBody, k, ref _land);
            _land.Offset *= _landPower;
            _land.Squash *= _landPower;
        }

        BodyKey[] BodyKeys()
        {
            switch (_act)
            {
                case CombatPose.Whip: return WhipBody;
                case CombatPose.WhipAlt: return WhipAltBody;
                case CombatPose.Slam: return SlamBody;
                case CombatPose.Spin: return SpinBody;
                case CombatPose.Reach: return ReachBody;
                case CombatPose.Rear: return _actDuration > 0.6f ? RearTallBody : RearBody;
                case CombatPose.Squash: return SquashBody;
                case CombatPose.Lunge:
                    switch (_lunge)
                    {
                        case LungeKind.Yank: return YankBody;
                        case LungeKind.Impact: return ImpactBody;
                        case LungeKind.Kick: return KickBody;
                        default: return BiteBody;
                    }
                default: return null;
            }
        }

        // Chân nào ra đòn thì đi theo chuỗi mốc nào (mirror: lật đối xứng cho chân bên kia)
        LegKey[] LegKeys(Leg leg, out bool mirror)
        {
            mirror = false;
            bool lead = leg.slot == 0 && leg.side < 0; // chân trước phía đang quay mặt
            bool other = leg.slot == 0 && leg.side > 0;
            switch (_act)
            {
                case CombatPose.Whip: return lead ? WhipLeg : null;
                case CombatPose.WhipAlt: return other ? WhipAltLeg : null;
                case CombatPose.Slam: return lead ? SlamLead : other ? SlamOther : null;
                case CombatPose.Reach: return lead ? ReachLead : other ? ReachOther : null;
                case CombatPose.Rear:
                {
                    bool tall = _actDuration > 0.6f;
                    if (leg.slot == 1)
                    {
                        mirror = leg.side > 0;
                        return tall ? RearTallSecond : RearSecond;
                    }

                    return lead ? (tall ? RearTallLead : RearLead) : other ? (tall ? RearTallOther : RearOther) : null;
                }
                case CombatPose.Squash: return lead ? SquashLead : other ? SquashOther : null;
                case CombatPose.Lunge:
                    switch (_lunge)
                    {
                        case LungeKind.Yank: return lead ? YankLead : other ? YankOther : null;
                        case LungeKind.Impact: return lead ? ImpactLead : other ? ImpactOther : null;
                        case LungeKind.Kick: return lead ? KickLead : other ? KickOther : null;
                        default: return lead ? BiteLead : other ? BiteOther : null;
                    }
                default: return null;
            }
        }

        // Lộn vòng: ngả ra sau lấy đà → lộn tới thật nhanh rồi chậm dần → lố một chút rồi về đúng vòng
        float SpinAngle(float k)
        {
            float full = -360f * Mathf.Max(1f, Mathf.Round(_actDuration / 0.5f));
            if (k < 0.1f)
                return 24f * Eased(Ease.InOut, k / 0.1f);
            if (k < 0.72f)
                return Mathf.Lerp(24f, full - 18f, Eased(Ease.Out, (k - 0.1f) / 0.62f));
            return Mathf.Lerp(full - 18f, full, Eased(Ease.InOut, (k - 0.72f) / 0.28f));
        }

        // Đặt móng theo đòn (Pose). foot = chỗ móng theo dáng thường (đứng / bò / bay)
        Vector2 ActFoot(Leg leg, Vector2 foot, ref float lift)
        {
            if (!Application.isPlaying || (_act == CombatPose.None && _braceSplay <= 0f && _land.Squash >= 0f))
                return foot;

            Vector2 target = foot;
            float splay = _braceSplay + Mathf.Max(0f, -_land.Squash) * 0.6f; // bắn laze, đáp đất: chân bè ra
            bool driven = false;
            if (_act != CombatPose.None)
            {
                float k = ActProgress;
                LegKey[] keys = LegKeys(leg, out bool mirror);
                driven = keys != null || _act == CombatPose.Spin;
                if (keys != null)
                    target = SampleLeg(keys, k, leg, foot, mirror);
                else if (_act == CombatPose.Spin)
                    target = SpinFoot(leg, foot, k);
                // Dẹt người (đẻ nhện con, đáp đất) thì các chân còn lại bè ra
                if (_act == CombatPose.Squash || (_act == CombatPose.Lunge && _lunge == LungeKind.Impact))
                    splay += Mathf.Max(0f, -_shown.Squash) * 0.7f;
            }

            if (!driven)
            {
                target.x += (target.x - HeadPoint.x) * splay;
                if (_act == CombatPose.Reach || (_act == CombatPose.Lunge && _lunge == LungeKind.Kick))
                {
                    // Đang bay vụt đi: các chân còn lại quặp ra sau cho gọn
                    float flight = FlightWeight;
                    target += new Vector2(-(0.2f + 0.12f * leg.slot) * (leg.side < 0 ? 1.4f : 0.8f), 0.1f) * flight;
                }
            }
            if (_plant > 0.5f)
                target.y = Mathf.Max(target.y, 0f); // không đâm xuyên mặt đất / mặt đá
            lift = Mathf.Max(lift, target.y - foot.y);
            return target;
        }

        // Lộn vòng: co chân sát thân lấy đà → duỗi thẳng ra tứ phía → về dáng thường
        Vector2 SpinFoot(Leg leg, Vector2 foot, float k)
        {
            // Co chân lúc lấy đà và lúc bật lên (tới k 0.16), rồi mới bung ra
            float tuck = k < 0.1f ? Eased(Ease.InOut, k / 0.1f) : 1f - Eased(Ease.Out, Mathf.Clamp01((k - 0.16f) / 0.12f));
            float open = k < 0.16f ? 0f
                : k < 0.28f ? Eased(Ease.Snap, (k - 0.16f) / 0.12f)
                : k < 0.72f ? 1f
                : 1f - Eased(Ease.InOut, (k - 0.72f) / 0.28f);
            float angle = SpinFan[Mathf.Clamp(leg.slot, 0, SpinFan.Length - 1)];
            if (leg.side > 0)
                angle = 180f - angle;
            Vector2 star = _bodyMatrix.MultiplyPoint3x4(leg.root + Polar(angle, (leg.femurLength + leg.tibiaLength) * 0.9f));
            Vector2 tucked = _bodyMatrix.MultiplyPoint3x4(Vector2.Lerp(leg.root, leg.rest, 0.35f) + new Vector2(0f, 0.12f));
            return Vector2.Lerp(Vector2.Lerp(foot, tucked, tuck), star, open);
        }

        // Móng chân theo chuỗi mốc: giữa hai mốc có góc thì đi theo cung quanh gốc chân, có dáng gốc thì đi thẳng + nhấc vòng
        Vector2 SampleLeg(LegKey[] keys, float k, Leg leg, Vector2 foot, bool mirror)
        {
            int i = 1;
            while (i < keys.Length - 1 && k > keys[i].K)
                i++;
            LegKey a = keys[i - 1], b = keys[i];
            float x = Eased(b.Ease, Mathf.Clamp01((k - a.K) / Mathf.Max(b.K - a.K, 1e-4f)));
            float bump = b.Arc * Mathf.Sin(Mathf.PI * Mathf.Clamp01(x));
            if (a.Reach > 0f && b.Reach > 0f)
            {
                float angle = Mathf.LerpUnclamped(KeyAngle(a, leg, mirror), KeyAngle(b, leg, mirror), x);
                float reach = Mathf.LerpUnclamped(KeyReach(a, leg), KeyReach(b, leg), x) + bump;
                return _bodyMatrix.MultiplyPoint3x4(leg.root + Polar(angle, reach));
            }

            Vector2 from = a.Reach > 0f ? KeyPoint(a, leg, mirror) : foot;
            Vector2 to = b.Reach > 0f ? KeyPoint(b, leg, mirror) : foot;
            return Vector2.LerpUnclamped(from, to, x) + Vector2.up * bump;
        }

        float KeyAngle(in LegKey key, Leg leg, bool mirror)
        {
            float angle = mirror ? 180f - key.Angle : key.Angle;
            if (key.Aim)
            {
                Vector2 to = _actPointBody - leg.root;
                angle += Mathf.Clamp(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, -60f, 80f);
            }

            return angle;
        }

        float KeyReach(in LegKey key, Leg leg) =>
            key.Aim ? Mathf.Clamp((_actPointBody - leg.root).magnitude, 0.6f, key.Reach) : key.Reach;

        Vector2 KeyPoint(in LegKey key, Leg leg, bool mirror) =>
            _bodyMatrix.MultiplyPoint3x4(leg.root + Polar(KeyAngle(key, leg, mirror), KeyReach(key, leg)));

        static Vector2 Polar(float degrees, float length)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * length;
        }

        static void Sample(BodyKey[] keys, float k, ref Look look)
        {
            int i = 1;
            while (i < keys.Length - 1 && k > keys[i].K)
                i++;
            BodyKey a = keys[i - 1], b = keys[i];
            float x = Eased(b.Ease, Mathf.Clamp01((k - a.K) / Mathf.Max(b.K - a.K, 1e-4f)));
            look.Offset = Vector2.LerpUnclamped(a.Offset, b.Offset, x);
            look.Tilt = Mathf.LerpUnclamped(a.Tilt, b.Tilt, x);
            look.Squash = Mathf.LerpUnclamped(a.Squash, b.Squash, x);
            look.Head = Mathf.LerpUnclamped(a.Head, b.Head, x);
            look.Fang = Mathf.Max(0f, Mathf.LerpUnclamped(a.Fang, b.Fang, x));
            look.Abdomen = Mathf.LerpUnclamped(a.Abdomen, b.Abdomen, x);
        }

        // InOut: êm hai đầu. Out: nhanh rồi chậm dần. Snap: vụt cực nhanh, phanh gấp (ra đòn). In: chậm rồi nhanh dần.
        // Back: tới đích lố một chút rồi về (dừng lại có đà)
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

        Vector2 SurfaceTarget(Leg leg, int index, out float lift)
        {
            Vector2 target = leg.rest;
            lift = 0f;

            if (_walkWeight > 0f)
            {
                // Hai nhóm thay nhau: trái 1, 3 + phải 2, 4 | phải 1, 3 + trái 2, 4. Trong nhóm, chân sau đi trước một chút
                int group = (leg.slot + (leg.side > 0 ? 0 : 1)) % 2;
                float u = Mathf.Repeat(_phase + group * 0.5f + (3 - leg.slot) * stepWave, 1f);
                float stride = cycleDistance * stance; // thân đi được bấy nhiêu trong lúc móng bám, móng lùi đúng bấy nhiêu
                float x, y = 0f;
                if (u < stance)
                {
                    x = stride * (0.5f - u / stance);
                }
                else
                {
                    float k = (u - stance) / (1f - stance);
                    x = stride * (Mathf.SmoothStep(0f, 1f, k) - 0.5f);
                    y = stepHeight * Mathf.Sin(Mathf.PI * k);
                }

                target += new Vector2(x, y) * _walkWeight;
                lift = y * _walkWeight;
            }

            if (index == _tapLeg)
            {
                float k = Mathf.Clamp01(_tapClock / tapTime);
                float up = Mathf.Sin(Mathf.PI * k);
                target += new Vector2(_tapShift * up, tapLift * up * up) * (1f - _walkWeight);
                lift = Mathf.Max(lift, tapLift * up * up * (1f - _walkWeight));
            }

            return target;
        }

        // Trên không: nhảy lên thì co chân sát thân, rơi thì xoè chân ra; đu thì nửa co nửa xoè, hai chân trước giơ
        // về phía sợi tơ; chân quờ quạng nhẹ
        Vector2 AirTarget(Leg leg, Snapshot snap)
        {
            var tuck = new Vector2(leg.rest.x * 0.5f, tuckHeight + 0.04f * leg.slot);
            var spread = new Vector2(leg.rest.x * 1.05f, 0.08f + 0.05f * (3 - leg.slot));
            float open = snap.Mode == Mode.Swing ? 0.45f : Mathf.Clamp01(0.2f - snap.Velocity.y / 10f);
            Vector2 target = Vector2.Lerp(tuck, spread, open);
            target.y += 0.03f * Mathf.Sin(_time * 9f + leg.slot * 1.3f + leg.side);
            if (snap.Mode == Mode.Swing && leg.slot == 0)
                target.y += 0.15f;
            return target;
        }

        // Thỉnh thoảng một chân nhấc lên gõ xuống (chỉ khi đứng yên trên mặt đất / đá)
        void UpdateTap(float dt, bool live)
        {
            if (!live || legs.Length == 0)
                return;

            if (_tapLeg >= 0)
            {
                _tapClock += dt;
                if (_tapClock >= tapTime)
                    _tapLeg = -1;
                return;
            }

            _tapTimer -= dt;
            if (_tapTimer > 0f || _walkWeight > 0.1f || _plant < 1f || _act != CombatPose.None)
                return;

            _tapTimer = UnityEngine.Random.Range(tapInterval.x, tapInterval.y);
            _tapLeg = UnityEngine.Random.Range(0, legs.Length);
            _tapClock = 0f;
            _tapShift = UnityEngine.Random.Range(-0.05f, 0.08f) * -legs[_tapLeg].side; // hơi vươn ra ngoài
        }

        // IK 2 xương: đặt móng vào target (Pose); gối nhô lên phía lưng
        void SolveLeg(Leg leg, Vector2 target)
        {
            Vector2 goal = _bodyInverse.MultiplyPoint3x4(target);
            Vector2 root = leg.root;
            float l1 = Mathf.Max(leg.femurLength, 1e-4f), l2 = Mathf.Max(leg.tibiaLength, 1e-4f);
            Vector2 toGoal = goal - root;
            float d = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
            float baseAngle = Mathf.Atan2(toGoal.y, toGoal.x);
            float bend = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f));

            // Hai cách gập gối: chọn cách gối cao hơn (phía lưng)
            float a = baseAngle + bend, b = baseAngle - bend;
            float femurAngle = Mathf.Sin(a) >= Mathf.Sin(b) ? a : b;
            Vector2 knee = root + new Vector2(Mathf.Cos(femurAngle), Mathf.Sin(femurAngle)) * l1;
            float tibiaAngle = Mathf.Atan2(goal.y - knee.y, goal.x - knee.x);

            leg.femur.localPosition = root;
            leg.femur.localRotation = Quaternion.Euler(0f, 0f, femurAngle * Mathf.Rad2Deg);
            leg.tibia.localRotation = Quaternion.Euler(0f, 0f, (tibiaAngle - femurAngle) * Mathf.Rad2Deg);
        }

        // ---------- Tơ, nanh, mắt ----------

        // Đu: tơ bắn từ đầu bụng tới đá rồi căng giữa nhện và đá
        void UpdateWeb(Snapshot snap, float dt, bool live)
        {
            if (web == null)
                return;

            bool on = snap.Mode == Mode.Swing;
            _webTime = on ? (live ? _webTime + dt : webShootTime) : 0f;
            web.enabled = on;
            if (!on)
                return;

            Vector2 from = spinneret != null ? (Vector2)spinneret.position : (Vector2)body.position;
            float shot = Mathf.Clamp01(_webTime / Mathf.Max(webShootTime, 0.01f));
            web.SetPosition(0, from);
            web.SetPosition(1, Vector2.Lerp(from, snap.Anchor, shot));
        }

        void UpdateFangs(float dt, bool live)
        {
            if (fangs == null || fangs.Length == 0)
                return;

            float open = 0f;
            if (live)
            {
                _fangTimer -= dt;
                if (_fangTimer <= 0f && _fangClock < 0f)
                {
                    _fangClock = 0f;
                    _fangTimer = UnityEngine.Random.Range(fangInterval.x, fangInterval.y);
                }

                if (_fangClock >= 0f)
                {
                    _fangClock += dt;
                    float k = _fangClock / 0.35f;
                    open = k < 1f ? Mathf.Sin(Mathf.PI * k) : 0f;
                    if (k >= 1f)
                        _fangClock = -1f;
                }
            }

            // Bò thì nanh nhai nhẹ theo nhịp; trên không thì nanh mở ra
            open = Mathf.Max(open, 0.35f * _walkWeight * (0.5f + 0.5f * Mathf.Sin(4f * Mathf.PI * _phase)));
            open = Mathf.Max(open, 0.6f * (1f - _surfaceBlend));
            open = Mathf.Max(open, _braceFang);
            open = Mathf.Lerp(open, _shown.Fang, _shown.FangWeight); // ra đòn: nanh banh / sập theo đòn
            for (int i = 0; i < fangs.Length; i++)
            {
                if (fangs[i] == null)
                    continue;
                float outward = i % 2 == 0 ? -1f : 1f; // trái mở sang trái, phải mở sang phải
                float amount = i < 2 ? 1f : 0.5f;
                fangs[i].localRotation = Quaternion.Euler(0f, 0f, outward * fangTwitch * amount * open);
            }
        }

        void UpdateEyes(float dt, bool live)
        {
            if (eyes == null)
                return;

            float glow = 1f - eyePulse * (0.5f + 0.5f * Mathf.Sin(_time * 2.3f));
            if (live)
            {
                // Thỉnh thoảng mắt chớp tắt hai lần thật nhanh
                _flickerTimer -= dt;
                if (_flickerTimer <= 0f)
                {
                    _flickerTimer = UnityEngine.Random.Range(eyeFlickerInterval.x, eyeFlickerInterval.y);
                    _flickerClock = 0f;
                }

                if (_flickerClock >= 0f)
                {
                    _flickerClock += dt;
                    if (_flickerClock < 0.06f || (_flickerClock > 0.12f && _flickerClock < 0.18f))
                        glow *= 0.35f;
                    if (_flickerClock > 0.18f)
                        _flickerClock = -1f;
                }
            }

            // Chiêu laze: vận sức thì mắt sáng dần, đang bắn thì sáng rực
            float power = 0f;
            if (_laser != null)
            {
                if (_laser.Phase == LaserPhase.Charging)
                    power = _laser.PhaseProgress;
                else if (_laser.Phase == LaserPhase.Firing)
                    power = 1f;
            }

            glow = Mathf.Lerp(glow, 1f, power);
            eyes.color = new Color(eyeColor.r * glow, eyeColor.g * glow, eyeColor.b * glow, eyeColor.a);
            if (eyeGlow != null)
            {
                eyeGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, Mathf.Min(1f, glowColor.a * glow * (1f + power)));
                eyeGlow.transform.localScale = Vector3.one * (glowSize * (1f + laserGlow * power));
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!HasReferences)
                return;
            for (int i = 0; i < legs.Length; i++)
            {
                Gizmos.color = legs[i].lift > 0.01f ? new Color(1f, 0.6f, 0.2f) : new Color(0.3f, 1f, 0.4f);
                Gizmos.DrawWireSphere(FootPosition(i), 0.05f * size);
            }
        }
    }
}
