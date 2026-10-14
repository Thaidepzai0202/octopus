using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Hoạt ảnh bạch tuộc bằng khung hình vẽ tay (Assets/_Project/Art/Octopus): mỗi frame chọn khung theo trạng thái
    /// và vận tốc, cộng thêm co giãn lò xo, xoay theo dây khi đu, và một xúc tu vẽ bằng code để nắm đá ở mọi khoảng cách.
    ///
    ///  Đứng yên   — idle (octopus.png), thở nhẹ
    ///  Bò         — 6 khung walk, đổi khung theo quãng đường đi được nên chân không trượt trên đất
    ///  Nhảy       — jump: bật (tia bụi) → lên → đỉnh → rơi → tiếp đất (bụi) → ngồi thụp → đứng
    ///  Rơi nhanh  — 6 khung fall, rơi càng nhanh càng chúc đầu
    ///  Đu         — swing_1 khi đu nhanh (có vệt chuyển động), swing_2 khi chậm; xoay cho đỉnh đầu hướng về đá,
    ///               xúc tu code nối từ gốc xúc tu trên đỉnh đầu tới đá
    ///  Bám đá     — swing_2 treo dưới đá, gốc xúc tu áp vào mặt đá
    ///  Vừa buông  — release
    /// </summary>
    [ExecuteAlways]
    public class OctopusSpriteAnimator : MonoBehaviour
    {
        [Serializable]
        public class Frame
        {
            public Sprite sprite;
            [Tooltip("Đặt khung theo chân chạm đất (bật) hay theo tâm thân (tắt)")]
            public bool ground;
            [Tooltip("Gốc xúc tu, toạ độ local (unit) tính từ pivot. Chỉ dùng cho khung đu / bám")]
            public Vector2 attach;
        }

        [Header("Khung hình (dựng tự động từ Art/Octopus/frames.json)")]
        [SerializeField] Frame idle = new Frame();
        [SerializeField] Frame[] walk = Array.Empty<Frame>();
        [Tooltip("0 ngồi thụp, 1 bật, 2 lên, 3 đỉnh, 4 rơi, 5 tiếp đất")]
        [SerializeField] Frame[] jump = Array.Empty<Frame>();
        [Tooltip("Rơi tự do: càng về sau càng chúc đầu")]
        [SerializeField] Frame[] fall = Array.Empty<Frame>();
        [Tooltip("0 đu nhanh (có vệt chuyển động), 1 đu chậm / bám")]
        [SerializeField] Frame[] swing = Array.Empty<Frame>();
        [SerializeField] Frame release = new Frame();

        [Header("Nhịp khung hình")]
        [Tooltip("Quãng đường (unit) đi hết một vòng các khung bò")]
        [SerializeField] float walkCycleLength = 1.6f;
        [Tooltip("Bò nhanh hơn mức này mới dùng khung bò")]
        [SerializeField] float crawlThreshold = 0.4f;
        [SerializeField] float takeoffTime = 0.12f;
        [Tooltip("Thời gian khung tiếp đất (bụi), sau đó tới khung ngồi thụp")]
        [SerializeField] float landTime = 0.14f;
        [SerializeField] float crouchTime = 0.1f;
        [SerializeField] float releaseTime = 0.25f;
        [Tooltip("Vận tốc dọc phân biệt đang lên / tới đỉnh / đang rơi")]
        [SerializeField] float apexSpeed = 2.5f;
        [Tooltip("Rơi nhanh hơn mức này thì chuyển sang khung rơi tự do")]
        [SerializeField] float freeFallSpeed = 9f;
        [Tooltip("Rơi nhanh cỡ này thì dùng khung chúc đầu nhất")]
        [SerializeField] float freeFallFullSpeed = 24f;
        [Tooltip("Đu nhanh hơn mức này thì dùng khung có vệt chuyển động")]
        [SerializeField] float fastSwingSpeed = 6f;

        [Header("Dáng")]
        [Tooltip("Tâm vật lý cao hơn mặt đất bấy nhiêu (bán kính collider)")]
        [SerializeField] float groundOffset = 0.5f;
        [Tooltip("Nghiêng thân theo vận tốc ngang khi ở trên không (độ)")]
        [SerializeField] float airLean = 15f;
        [SerializeField, Range(0f, 0.1f)] float breathAmount = 0.025f;
        [SerializeField] float breathSpeed = 2.2f;
        [SerializeField] float rotationSmoothing = 14f;

        [Header("Co giãn (squash & stretch)")]
        [SerializeField] float squashStiffness = 260f;
        [SerializeField] float squashDamping = 13f;
        [SerializeField, Range(0f, 1f)] float jumpStretch = 0.3f;
        [SerializeField, Range(0f, 1f)] float landSquash = 0.35f;
        [SerializeField, Range(0f, 1f)] float grabStretch = 0.15f;
        [SerializeField, Range(0f, 1f)] float releaseStretch = 0.2f;

        [Header("Xúc tu nắm đá")]
        [SerializeField] float shootDuration = 0.08f;
        [SerializeField] AnimationCurve tentacleWidth = new AnimationCurve(new Keyframe(0f, 0.24f), new Keyframe(1f, 0.09f));
        [Tooltip("Khoảng cách giữa các giác hút (unit)")]
        [SerializeField, Min(0.02f)] float suckerSpacing = 0.17f;
        [SerializeField, Range(0.02f, 1f)] float tipSize = 0.16f;
        [SerializeField] Color tipColor = new Color(0.35f, 0.9f, 1f);

        [Header("Vệt tốc độ")]
        [SerializeField] float speedTrailThreshold = 12f;

        [Header("Tham chiếu (đã nối sẵn, không cần sửa)")]
        [SerializeField] OctopusController controller;
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] TrailRenderer speedTrail;
        [SerializeField] Tentacle grab = new Tentacle();

        Rigidbody2D _rigidbody;
        float _squash;
        float _squashVelocity;
        float _walkPhase;
        float _takeoffTimer;
        float _landTimer;
        float _releaseTimer;
        float _fallSpeed;
        float _angle;
        bool _lookDirty = true;
        OctopusState _lastState;
        bool _subscribed;

        /// <summary>Độ co giãn hiện tại: dương = đang dãn dài, âm = đang bẹp.</summary>
        public float Squash => _squash;

        /// <summary>Khung đang hiển thị (để test và debug).</summary>
        public Sprite CurrentSprite => body != null ? body.sprite : null;

        public bool TentacleVisible => grab.IsValid && grab.line.enabled;

        bool HasReferences => controller != null && body != null && glow != null && speedTrail != null &&
                              grab.IsValid && idle.sprite != null;

        void Start()
        {
            if (!Application.isPlaying || controller == null)
                return;

            _rigidbody = controller.GetComponent<Rigidbody2D>();
            controller.StateChanged += OnStateChanged;
            controller.Jumped += OnJumped;
            controller.Swing.Attached += OnAttached;
            controller.Swing.Released += OnReleased;
            _lastState = controller.State;
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

        void OnValidate()
        {
            _lookDirty = true;
        }

        void LateUpdate()
        {
            if (!HasReferences)
                return;

            if (_lookDirty)
            {
                grab.ApplyLook(tentacleWidth, Color.white, tipColor, tipSize, 1f / suckerSpacing);
                _lookDirty = false;
            }

            if (!Application.isPlaying)
            {
                // Không Play: đứng yên trên mặt đất để xem và chỉnh trong Scene
                Place(idle, (Vector2)controller.transform.position + Vector2.down * groundOffset, 0f, false, 0f);
                ShowTentacle(false);
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UpdateTimers(dt);
            UpdateSquash(dt);

            Vector2 position = controller.transform.position;
            Vector2 velocity = _rigidbody.linearVelocity;
            bool flip = controller.Facing < 0f;
            float breath = 0f;

            switch (controller.State)
            {
                case OctopusState.Swinging:
                {
                    Vector2 anchor = controller.Swing.AnchorPoint;
                    Frame frame = Pick(swing, velocity.magnitude > fastSwingSpeed ? 0 : 1);
                    Place(frame, position, AimAngle(frame, flip, anchor - position), flip, 0f);
                    ShowTentacle(true);
                    grab.Grab(body.transform.TransformPoint(frame.attach), anchor, shootDuration, dt);
                    break;
                }

                case OctopusState.Clinging:
                {
                    // Treo dưới đá: xoay gốc xúc tu hướng vào mặt đá rồi dời thân để gốc xúc tu áp sát mặt đá
                    Frame frame = Pick(swing, 1);
                    Vector2 normal = controller.Mover.ClingNormal;
                    Vector2 surface = controller.Mover.ClingPoint;
                    float angle = AimAngle(frame, flip, -normal);
                    Place(frame, position, angle, flip, 0f);
                    Vector2 attachWorld = body.transform.TransformPoint(frame.attach);
                    body.transform.position += (Vector3)(surface + normal * 0.05f - attachWorld);
                    ShowTentacle(false);
                    break;
                }

                case OctopusState.Grounded:
                {
                    Frame frame = GroundFrame(velocity, dt);
                    if (frame == idle)
                        breath = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
                    Place(frame, frame.ground ? position + Vector2.down * groundOffset : position, 0f, flip, breath);
                    ShowTentacle(false);
                    break;
                }

                default:
                {
                    Frame frame = AirFrame(velocity);
                    float lean = -Mathf.Clamp(velocity.x / 20f, -1f, 1f) * airLean;
                    Place(frame, frame.ground ? position + Vector2.down * groundOffset : position, lean, flip, 0f);
                    ShowTentacle(false);
                    break;
                }
            }

            glow.transform.position = position;
            speedTrail.transform.position = position;
            speedTrail.emitting = velocity.magnitude > speedTrailThreshold &&
                                  controller.State is OctopusState.Swinging or OctopusState.Airborne;
            if (controller.State == OctopusState.Airborne)
                _fallSpeed = Mathf.Max(_fallSpeed, -velocity.y);
        }

        // ---------- Chọn khung ----------

        Frame GroundFrame(Vector2 velocity, float dt)
        {
            if (_takeoffTimer > 0f)
                return Pick(jump, 1); // vừa bấm nhảy, vật lý chưa kịp rời đất
            if (_landTimer > 0f)
                return _landTimer > crouchTime ? Pick(jump, 5) : Pick(jump, 0);

            float speed = Mathf.Abs(velocity.x);
            if (speed > crawlThreshold && walk.Length > 0)
            {
                // Pha bước tăng theo quãng đường đi được, không theo thời gian → chân không trượt
                _walkPhase = Mathf.Repeat(_walkPhase + speed * dt / walkCycleLength, 1f);
                return walk[Mathf.FloorToInt(_walkPhase * walk.Length) % walk.Length];
            }

            return idle;
        }

        Frame AirFrame(Vector2 velocity)
        {
            if (_releaseTimer > 0f && release.sprite != null)
                return release;
            if (_takeoffTimer > 0f)
                return Pick(jump, 1);

            float vy = velocity.y;
            if (vy > apexSpeed)
                return Pick(jump, 2);
            if (vy > -apexSpeed)
                return Pick(jump, 3);
            if (-vy < freeFallSpeed || fall.Length == 0)
                return Pick(jump, 4);

            float t = Mathf.InverseLerp(freeFallSpeed, freeFallFullSpeed, -vy);
            return fall[Mathf.Min(fall.Length - 1, Mathf.FloorToInt(t * fall.Length))];
        }

        Frame Pick(Frame[] frames, int index) =>
            frames.Length == 0 ? idle : frames[Mathf.Clamp(index, 0, frames.Length - 1)];

        // Góc xoay để vector từ pivot tới gốc xúc tu (đã tính lật) chỉ theo hướng cho trước
        static float AimAngle(Frame frame, bool flip, Vector2 direction)
        {
            Vector2 attach = frame.attach;
            if (flip)
                attach.x = -attach.x;
            float from = Mathf.Atan2(attach.y, attach.x) * Mathf.Rad2Deg;
            float to = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return to - from;
        }

        void Place(Frame frame, Vector2 position, float angle, bool flip, float breath)
        {
            body.sprite = frame.sprite;
            float blend = Application.isPlaying ? 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime) : 1f;
            _angle = Mathf.LerpAngle(_angle, angle, blend);
            body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, _angle));

            // Co giãn giữ thể tích: dài ra thì hẹp lại; lật trái / phải bằng scale âm để gốc xúc tu lật theo
            float stretch = 1f + Mathf.Max(-0.5f, _squash) + breath;
            body.transform.localScale = new Vector3((flip ? -1f : 1f) / Mathf.Sqrt(stretch), stretch, 1f);
        }

        void ShowTentacle(bool visible)
        {
            if (grab.line.enabled == visible)
                return;
            grab.line.enabled = visible;
            grab.tip.gameObject.SetActive(visible);
            if (visible)
                grab.Init(body.transform.position); // phóng ra từ đầu, không từ chỗ cũ
        }

        // ---------- Sự kiện → nhịp khung hình, co giãn ----------

        void OnJumped()
        {
            _takeoffTimer = takeoffTime;
            Kick(jumpStretch);
        }

        void OnAttached(Vector2 anchor)
        {
            _releaseTimer = 0f;
            Kick(grabStretch);
        }

        void OnReleased()
        {
            _releaseTimer = releaseTime;
            Kick(releaseStretch);
        }

        void OnStateChanged(OctopusState state)
        {
            if (state == OctopusState.Grounded && _lastState == OctopusState.Airborne)
            {
                _landTimer = landTime + crouchTime;
                Kick(-landSquash * (0.35f + 0.65f * Mathf.Clamp01(_fallSpeed / 18f)));
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
            _releaseTimer = Mathf.Max(0f, _releaseTimer - dt);
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
    }
}
