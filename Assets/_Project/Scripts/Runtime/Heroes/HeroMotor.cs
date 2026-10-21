using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Di chuyển của nhân vật người (RED, PINK, BLUE) kiểu game đối kháng đi cảnh: cần trái chạy trái / phải (có tăng / giảm
    /// tốc), nút NHẢY để nhảy (giữ thì cao, nhả sớm thì thấp), bấm lần nữa trên không để nhảy đúp; rời mép sàn một chút vẫn nhảy
    /// được (coyote time), bấm nhảy sớm trước khi chạm sàn vẫn tính (jump buffer), dáng đáp đất. Kéo cần lên / xuống không làm
    /// đi mà chỉ đổi kiểu chiêu. Cả 3 con dùng chung một bộ thông số mặc định (HeroMoveSettings.Default), chỉnh riêng được trong
    /// Inspector; lượn / nhảy đúp kiểu khác là tuỳ chọn, mặc định tắt.
    ///
    /// Tảng đá là bục một chiều, chỉ với thân hình người: nhảy xuyên từ dưới lên, đứng được trên mặt đá, kéo cần xuống + nhảy
    /// thì thả xuống qua đá. Bạch tuộc / nhện vẫn đu, bám dưới đá như cũ (chỉ thân hình người bỏ qua va chạm với đá). Sàn, gờ
    /// đá, vách, trần vẫn cứng.
    ///
    /// Nằm trên người chơi cạnh OctopusController, chỉ chạy khi PlayerCharacter chọn nhân vật người (Activate). Khi đó thân
    /// đổi sang CapsuleCollider2D cao như người (tắt vòng tròn của bạch tuộc), SwingController / SurfaceMover tắt; OctopusController
    /// chuyển input sang đây (FixedUpdate gọi Simulate) và vẫn báo State Grounded / Airborne, Facing… như cũ, nên phần còn lại
    /// của game coi nhân vật người như "người chơi" bình thường.
    ///
    /// Chiêu của nhân vật người điều khiển thân qua SkillContext.Hero: Launch, JumpTo, Dash, TeleportTo, Hover, Root,
    /// SetGravityScale, BeginFlight… (tóm tắt ở đầu file Combat/Heroes/RedSkills.cs).
    /// </summary>
    [DisallowMultipleComponent]
    public class HeroMotor : MonoBehaviour
    {
        /// <summary>Tâm thân (Rigidbody) cao hơn bàn chân bấy nhiêu unit, như bạch tuộc (vòng tròn bán kính 0.5).</summary>
        public const float FeetOffset = 0.5f;

        /// <summary>Ngực (chỗ nhắm, chỗ bị đánh) cao hơn tâm thân bấy nhiêu unit (nhân vật cao 2 unit).</summary>
        public const float ChestOffset = 0.5f;

        // Kéo cần lên / xuống quá mức này (và nhiều hơn kéo ngang) mới tính là đổi kiểu chiêu
        const float AimThreshold = 0.5f;

        [Tooltip("Thân hình người: bật khi chọn nhân vật người (vòng tròn của bạch tuộc tắt đi)")]
        [SerializeField] CapsuleCollider2D heroCollider;

        [Header("Thông số từng nhân vật")]
        [SerializeField] HeroMoveSettings red = HeroMoveSettings.Default();
        [SerializeField] HeroMoveSettings pink = HeroMoveSettings.Default();
        [SerializeField] HeroMoveSettings blue = HeroMoveSettings.Default();

        [Header("Chung")]
        [Tooltip("Bị đánh văng: mất điều khiển bấy nhiêu giây")]
        [SerializeField, Min(0f)] float hurtLock = 0.3f;
        [Tooltip("Dáng đáp đất (nhún) kéo dài bấy nhiêu giây")]
        [SerializeField, Min(0f)] float landTime = 0.14f;
        [Tooltip("Kéo xuống + nhảy trên tảng đá: rơi qua đá với vận tốc này (unit/giây)")]
        [SerializeField, Min(0f)] float dropSpeed = 4f;

        Rigidbody2D _body;
        CircleCollider2D _circle;
        SwingController _swing;
        SurfaceMover _mover;
        CombatController _combat;
        OctopusController _controller;
        PhysicsMaterial2D _slick;
        LayerMask _rockMask;
        SkillContext _moveCtx;
        float _savedGravity = 1f;
        bool _ready;

        // Input
        Vector2 _stick;
        float _jumpPressedAt = -10f;

        // Trạng thái
        bool _grounded;
        Collider2D _ground, _lastGround;
        float _lastGroundTime = -10f;
        bool _jumping;   // đã rời đất bằng cú nhảy / bị hất (hết coyote time)
        bool _rising;    // đang bay lên do cú nhảy (nhả nút sớm thì nhảy thấp lại)
        bool _jumpCut;
        int _airJumpsUsed;
        float _glideLeft;
        bool _gliding, _glideLifted;
        bool _dropHold; // vừa kéo xuống + nhảy qua đá: nút nhảy còn giữ không tính là lượn tới khi nhả
        float _glideFx;
        HeroMove _pose;
        float _poseLeft;
        float _hurtLeft, _rootLeft, _hoverLeft, _gravityLeft, _flyLeft;
        float _gravityScale = 1f, _flySpeed;
        float _lastVy;

        // Lướt (chiêu lướt; tuỳ chọn nhảy đúp SoulStep)
        bool _dashing;
        Vector2 _dashVelocity, _dashFrom;
        float _dashLeft, _dashHop;
        bool _dashTrail, _dashIsStep;
        Color _dashColor;

        // Bục một chiều
        Collider2D _dropRock;
        float _dropLeft;
        readonly Collider2D[] _near = new Collider2D[16];
        readonly ContactPoint2D[] _contacts = new ContactPoint2D[8];

        /// <summary>Đang là nhân vật người (PlayerCharacter đã Activate).</summary>
        public bool Active { get; private set; }

        /// <summary>Nhân vật người đang chơi.</summary>
        public PlayableCharacter Character { get; private set; } = PlayableCharacter.Red;

        /// <summary>Thông số di chuyển của nhân vật đang chơi.</summary>
        public HeroMoveSettings Settings => SettingsOf(Character);

        /// <summary>Hình nhân vật (hoạt ảnh gắn xương / hình tạm).</summary>
        public IHeroVisual Visual { get; private set; }

        public Rigidbody2D Body => _body;
        public CapsuleCollider2D Collider => heroCollider;

        /// <summary>Lớp đá (bục một chiều).</summary>
        public LayerMask RockMask => _rockMask;

        public HeroMoveSettings SettingsOf(PlayableCharacter character) => character switch
        {
            PlayableCharacter.Pink => pink,
            PlayableCharacter.Blue => blue,
            _ => red,
        };

        // ---------- Input (OctopusController / test gán) ----------

        /// <summary>Cần điều hướng -1..1 (đã trừ vùng chết): x = chạy, y = lên / xuống (đổi kiểu chiêu; bay tự do thì là bay lên / xuống).</summary>
        public Vector2 Stick
        {
            get => _stick;
            set => _stick = Vector2.ClampMagnitude(value, 1f);
        }

        /// <summary>Chạy -1..1 (0 khi đang kéo cần lên / xuống).</summary>
        public float Run => RunOf(_stick);

        /// <summary>Kéo cần lên (1) / xuống (-1) / không (0): kiểu chiêu khi bấm nút chiêu.</summary>
        public int Aim => AimOf(_stick);

        /// <summary>Đang giữ nút nhảy (giữ thì nhảy cao; bật tuỳ chọn lượn thì giữ trên không là lượn).</summary>
        public bool JumpHeld { get; set; }

        /// <summary>Bấm nút nhảy (và giữ tới khi JumpHeld = false).</summary>
        public void PressJump()
        {
            _jumpPressedAt = Time.time;
            JumpHeld = true;
        }

        /// <summary>Kéo cần lên / xuống: 1 / -1; ngang hoặc không kéo: 0. Lên / xuống thắng khi kéo chéo đúng 45°.</summary>
        public static int AimOf(Vector2 stick) =>
            Mathf.Abs(stick.y) >= AimThreshold && Mathf.Abs(stick.y) >= Mathf.Abs(stick.x) ? (stick.y > 0f ? 1 : -1) : 0;

        /// <summary>Phần chạy của cần: kéo lên / xuống thì không chạy.</summary>
        public static float RunOf(Vector2 stick) => AimOf(stick) != 0 ? 0f : Mathf.Clamp(stick.x, -1f, 1f);

        /// <summary>Kiểu chiêu theo hướng kéo: 0 = thường, 1 = ↑, 2 = ↓ (thứ tự trong bộ 9 chiêu).</summary>
        public static int VariantOf(int aim) => aim > 0 ? 1 : aim < 0 ? 2 : 0;

        // ---------- Trạng thái ----------

        public bool Grounded => _grounded;

        /// <summary>Mặt đang đứng (sàn, gờ đá, tảng đá); null khi trên không.</summary>
        public Collider2D Ground => _grounded ? _ground : null;

        /// <summary>Đang đứng trên một tảng đá (bục một chiều).</summary>
        public bool OnRock => _grounded && IsRock(_ground);

        public Vector2 Velocity
        {
            get => _body.linearVelocity;
            set => _body.linearVelocity = value;
        }

        /// <summary>Điểm giữa hai bàn chân (world).</summary>
        public Vector2 Feet => _body.position + Vector2.down * FeetOffset;

        /// <summary>Ngực (world): chỗ nhắm, chỗ bắn phép ra nếu không cần chính xác.</summary>
        public Vector2 Chest => _body.position + Vector2.up * ChestOffset;

        public bool IsDashing => _dashing;
        public bool IsGliding => _gliding;
        public bool IsFlying => _flyLeft > 0f;
        public bool IsHurt => _hurtLeft > 0f;
        public int AirJumpsLeft => Mathf.Max(0, Settings.airJumps - _airJumpsUsed);

        /// <summary>Thời gian lượn còn lại (giây).</summary>
        public float GlideLeft => _glideLeft;

        /// <summary>Dáng di chuyển đang báo cho hình.</summary>
        public HeroMove Move => CurrentMove();

        /// <summary>Vừa nhảy: false = nhảy từ đất, true = nhảy trên không (nhảy đúp).</summary>
        public event Action<bool> Jumped;

        /// <summary>Vừa chạm đất: vận tốc rơi lúc chạm (unit/giây).</summary>
        public event Action<float> Landed;

        // =============================================================================================================
        // Bật / tắt
        // =============================================================================================================

        void Init()
        {
            if (_ready)
                return;
            _ready = true;
            _body = GetComponent<Rigidbody2D>();
            _circle = GetComponent<CircleCollider2D>();
            _swing = GetComponent<SwingController>();
            _mover = GetComponent<SurfaceMover>();
            _combat = GetComponent<CombatController>();
            _controller = GetComponent<OctopusController>();
            if (heroCollider == null)
                heroCollider = GetComponent<CapsuleCollider2D>();
        }

        void Awake() => Init();

        /// <summary>Chọn nhân vật người: đổi thân sang hình người, tắt đu dây / bám đá, nhận input.</summary>
        public void Activate(PlayableCharacter character, IHeroVisual visual)
        {
            Init();
            Character = character;
            Visual = visual;
            if (Active)
                return;
            Active = true;

            if (_swing != null)
            {
                _rockMask = _swing.AnchorMask;
                _swing.enabled = false;
            }

            if (_mover != null)
                _mover.enabled = false;
            if (_rockMask.value == 0)
                _rockMask = LayerMask.GetMask("Anchorable");

            if (heroCollider == null)
            {
                heroCollider = gameObject.AddComponent<CapsuleCollider2D>();
                heroCollider.size = new Vector2(0.8f, 1.95f);
                heroCollider.offset = new Vector2(0f, heroCollider.size.y * 0.5f - FeetOffset);
            }

            _slick = new PhysicsMaterial2D("Hero") { friction = 0f, bounciness = 0f };
            heroCollider.sharedMaterial = _slick;
            heroCollider.enabled = true;
            if (_circle != null)
                _circle.enabled = false;

            _savedGravity = _body.gravityScale;
            _body.gravityScale = 0f; // trọng lực tự tính (nhảy cao / thấp, rơi nhanh, lượn)

            // Vòng tầm xúc tu và đá sáng lên khi trong tầm: chỉ dành cho đu dây
            var reach = GetComponentInChildren<ReachIndicator>(true);
            if (reach != null)
                reach.gameObject.SetActive(false);

            _glideLeft = Settings.glideTime;
            _airJumpsUsed = 0;
        }

        /// <summary>Thôi nhân vật người (chọn lại bạch tuộc / nhện): trả thân, đu dây, bám đá như cũ.</summary>
        public void Deactivate()
        {
            Init();
            if (!Active)
                return;
            Active = false;
            _dashing = false;
            _flyLeft = 0f;
            if (heroCollider != null)
                heroCollider.enabled = false;
            if (_circle != null)
                _circle.enabled = true;
            if (_swing != null)
                _swing.enabled = true;
            if (_mover != null)
                _mover.enabled = true;
            _body.gravityScale = _savedGravity;
            var reach = GetComponentInChildren<ReachIndicator>(true);
            if (reach != null)
                reach.gameObject.SetActive(true);
        }

        void OnDestroy()
        {
            if (_slick != null)
                Destroy(_slick);
        }

        // =============================================================================================================
        // Hàm cho chiêu (SkillContext.Hero)
        // =============================================================================================================

        /// <summary>Đặt thẳng vận tốc thân (hất mình lên, bật lùi, lao xuống…). Coi như đã rời đất; không bị nhả nút nhảy cắt ngắn.</summary>
        public void Launch(Vector2 velocity)
        {
            _body.linearVelocity = velocity;
            _rising = _jumpCut = false;
            _gliding = false;
            _hoverLeft = 0f;
            if (velocity.y > 0.05f)
            {
                _jumping = true;
                _grounded = false;
                _lastGroundTime = -10f;
            }
        }

        /// <summary>Bật thẳng lên cao height unit (giữ vận tốc ngang).</summary>
        public void JumpTo(float height) => Launch(new Vector2(_body.linearVelocity.x, Settings.LaunchSpeed(height)));

        /// <summary>
        /// Lướt theo direction một đoạn distance trong duration giây (không rơi, không điều khiển được, vách chặn lại). Lướt
        /// xong: hop &gt; 0 thì bật lên với vận tốc đó, không thì giữ một phần đà. trail = để lại vệt sáng màu color.
        /// Chờ lướt xong: while (ctx.Hero.IsDashing) yield return null;
        /// </summary>
        public void Dash(Vector2 direction, float distance, float duration, float hop = 0f, bool trail = false, Color color = default)
        {
            if (direction.sqrMagnitude < 1e-6f)
                direction = new Vector2(Facing, 0f);
            duration = Mathf.Max(0.02f, duration);
            _dashing = true;
            _dashIsStep = false;
            _dashVelocity = direction.normalized * (distance / duration);
            _dashLeft = duration;
            _dashHop = hop;
            _dashTrail = trail;
            _dashColor = color == default ? Settings.stepColor : color;
            _dashFrom = Chest;
            _gliding = false;
            _rising = _jumpCut = false;
            _hoverLeft = 0f;
            if (_dashVelocity.y > 0.05f)
            {
                _jumping = true;
                _grounded = false;
            }
        }

        /// <summary>Dừng lướt ngay (giữ một phần đà).</summary>
        public void StopDash()
        {
            if (!_dashing)
                return;
            Vector2 v = _body.linearVelocity;
            EndDash(ref v);
            _body.linearVelocity = v;
        }

        /// <summary>
        /// Dịch chuyển tức thời: bàn chân tới feet (world). Chỗ đó vướng sàn / vách thì nhích lên tới chỗ trống gần nhất.
        /// Trả về chỗ bàn chân thật sự đứng.
        /// </summary>
        public Vector2 TeleportTo(Vector2 feet)
        {
            Vector2 center = feet + Vector2.up * FeetOffset;
            for (int i = 0; i < 16 && Blocked(center); i++)
                center.y += 0.25f;
            _body.position = center;
            transform.position = center;
            _body.linearVelocity = Vector2.zero;
            _dashing = false;
            _gliding = false;
            _rising = _jumpCut = false;
            _grounded = false;
            _jumping = true;
            return center + Vector2.down * FeetOffset;
        }

        /// <summary>Lơ lửng tại chỗ seconds giây (đánh trên không): không rơi, đà ngang hãm dần.</summary>
        public void Hover(float seconds)
        {
            _hoverLeft = Mathf.Max(_hoverLeft, seconds);
            Vector2 v = _body.linearVelocity;
            v.y = 0f;
            _body.linearVelocity = v;
            _rising = _jumpCut = false;
        }

        /// <summary>Đứng im (bỏ qua cần chạy) seconds giây: thế thủ, vận chiêu.</summary>
        public void Root(float seconds) => _rootLeft = Mathf.Max(_rootLeft, seconds);

        /// <summary>Nhân trọng lực với scale trong seconds giây (0 = không rơi, 0.3 = rơi chậm, 2 = rơi nhanh).</summary>
        public void SetGravityScale(float scale, float seconds)
        {
            _gravityScale = Mathf.Max(0f, scale);
            _gravityLeft = seconds;
        }

        /// <summary>
        /// Bay tự do seconds giây (vd chiêu bay): không rơi, cần điều hướng đẩy đi mọi hướng với tốc độ speed, xuyên
        /// qua tảng đá. Bất tử / trong mờ là việc của chiêu (ctx.BlockFor, Visual.SetGhost).
        /// </summary>
        public void BeginFlight(float seconds, float speed)
        {
            _flyLeft = seconds;
            _flySpeed = speed;
            _dashing = false;
            _gliding = false;
            _hoverLeft = 0f;
            _rising = _jumpCut = false;
            _jumping = true;
        }

        public void EndFlight() => _flyLeft = 0f;

        /// <summary>Hồi lại lượt nhảy trên không và thời gian lượn (chiêu hất mình lên cho nhảy tiếp được).</summary>
        public void RefillAirMoves()
        {
            _airJumpsUsed = 0;
            _glideLeft = Settings.glideTime;
            _glideLifted = false;
        }

        // =============================================================================================================
        // Bị đánh, đặt lại (OctopusController gọi)
        // =============================================================================================================

        public void Knockback(Vector2 velocity)
        {
            _dashing = false;
            _gliding = false;
            _hoverLeft = 0f;
            _rising = _jumpCut = false;
            _body.linearVelocity = velocity;
            _hurtLeft = hurtLock;
            if (velocity.y > 0.5f)
            {
                _jumping = true;
                _grounded = false;
                _lastGroundTime = -10f;
            }
        }

        public void ResetTo(Vector2 position)
        {
            _body.position = position;
            transform.position = position;
            _body.linearVelocity = Vector2.zero;
            _dashing = false;
            _gliding = _glideLifted = false;
            _flyLeft = _hoverLeft = _rootLeft = _hurtLeft = _gravityLeft = _poseLeft = 0f;
            _rising = _jumpCut = false;
            _jumping = true;
            _grounded = false;
            _airJumpsUsed = 0;
            _glideLeft = Settings.glideTime;
            _jumpPressedAt = -10f;
            _dropRock = null;
            _ground = _lastGround = null;
        }

        // =============================================================================================================
        // Mỗi bước vật lý (OctopusController.FixedUpdate gọi)
        // =============================================================================================================

        public void Simulate(float dt)
        {
            if (!Active || dt <= 0f)
                return;

            HeroMoveSettings s = Settings;
            UpdatePlatforms(dt);

            Vector2 v = _body.linearVelocity;
            bool wasGrounded = _grounded;
            _grounded = CheckGround(v);
            if (_grounded)
            {
                _lastGroundTime = Time.time;
                _lastGround = _ground;
                if (!wasGrounded)
                    Land(-_lastVy);
            }

            Tick(ref _hurtLeft, dt);
            Tick(ref _rootLeft, dt);
            Tick(ref _poseLeft, dt);
            if (Tick(ref _gravityLeft, dt))
                _gravityScale = 1f;

            if (_dashing)
                StepDash(ref v, dt);
            else if (_flyLeft > 0f)
                StepFlight(ref v, dt);
            else
            {
                if (v.y <= 0f)
                    _rising = _jumpCut = false; // qua đỉnh cú nhảy
                StepJump(ref v, s);
                if (!_dashing) // nhảy đúp kiểu lướt vừa bắt đầu thì giữ nguyên vận tốc lướt
                {
                    StepGlide(ref v, s, dt);
                    StepRun(ref v, s, dt);
                    StepGravity(ref v, s, dt);
                }
            }

            _lastVy = v.y;
            _body.linearVelocity = v;
        }

        static bool Tick(ref float timer, float dt)
        {
            if (timer <= 0f)
                return false;
            timer -= dt;
            if (timer > 0f)
                return false;
            timer = 0f;
            return true;
        }

        void Land(float impact)
        {
            _jumping = _rising = _jumpCut = false;
            _gliding = _glideLifted = false;
            _airJumpsUsed = 0;
            _glideLeft = Settings.glideTime;
            if (impact >= Settings.landSpeed && _poseLeft <= 0f)
            {
                _pose = HeroMove.Land;
                _poseLeft = landTime;
                if (CombatFx.Instance != null && impact > Settings.landSpeed * 1.6f)
                    CombatFx.Instance.DustCloud(Feet, 0.6f, SkillColors.Dust, 3);
            }

            Landed?.Invoke(impact);
        }

        void StepJump(ref Vector2 v, HeroMoveSettings s)
        {
            bool buffered = Time.time - _jumpPressedAt <= s.jumpBuffer;
            if (!buffered || _hurtLeft > 0f)
                return;

            bool coyote = !_jumping && Time.time - _lastGroundTime <= s.coyoteTime;
            if (_grounded || coyote)
            {
                _jumpPressedAt = -10f;
                Collider2D under = _grounded ? _ground : _lastGround;
                if (Aim < 0 && IsRock(under))
                {
                    DropThrough(under, ref v);
                    return;
                }

                v.y = s.LaunchSpeed(s.jumpHeight);
                BeginRise();
                Jumped?.Invoke(false);
                if (CombatFx.Instance != null)
                    CombatFx.Instance.DustCloud(Feet, 0.45f, SkillColors.Dust, 2);
                return;
            }

            if (_airJumpsUsed >= s.airJumps)
                return; // không còn lượt: giữ lại lần bấm, chạm đất trong jumpBuffer thì nhảy luôn

            _jumpPressedAt = -10f;
            _airJumpsUsed++;
            AirJump(ref v, s);
            Jumped?.Invoke(true);
        }

        void BeginRise()
        {
            _jumping = true;
            _rising = true;
            _jumpCut = false;
            _grounded = false;
            _gliding = false;
            _hoverLeft = 0f;
        }

        void AirJump(ref Vector2 v, HeroMoveSettings s)
        {
            switch (s.airJump)
            {
                case HeroAirJump.SoulStep:
                {
                    float dir = Mathf.Abs(Run) > 0.3f ? Mathf.Sign(Run) : Facing;
                    Dash(new Vector2(dir, 0f), s.stepDistance, s.stepTime, s.stepHop, true, s.stepColor);
                    _dashIsStep = true;
                    v = _dashVelocity;
                    break;
                }

                default:
                    v.y = s.LaunchSpeed(s.airJumpHeight);
                    BeginRise();
                    _pose = HeroMove.DoubleJump;
                    _poseLeft = s.airJumpPoseTime;
                    if (s.airJump == HeroAirJump.Spin)
                        SpinSlash(s);
                    else if (CombatFx.Instance != null)
                        CombatFx.Instance.Ring(Feet, 0.7f, new Color(1f, 1f, 1f, 0.5f), 0.25f); // vòng gió dưới chân
                    break;
            }
        }

        // Tuỳ chọn Spin: nhảy đúp xoay vũ khí một vòng quanh người, chém trúng ai ở sát bên
        void SpinSlash(HeroMoveSettings s)
        {
            Vector2 c = Chest;
            CombatFx fx = CombatFx.Instance;
            if (fx != null)
            {
                float f = Facing;
                Color edge = Color.Lerp(s.spinColor, Color.black, 0.55f);
                fx.Slash(c, s.spinRadius * 0.85f, 90f, 90f - 360f * f, s.spinColor, edge, 0.45f, 0.3f, 0.8f);
                fx.Slash(c, s.spinRadius * 0.6f, 150f, 150f - 300f * f, s.spinColor, edge, 0.25f, 0.26f, 1f, 0.03f);
            }

            if (_combat == null || s.spinDamage <= 0f)
                return;
            _moveCtx ??= _combat.CreateContext();
            _moveCtx.HitCircle(c, s.spinRadius, s.spinDamage, new HitEffect { knockback = new Vector2(2.5f, 3f) }, s.spinColor);
        }

        void DropThrough(Collider2D rock, ref Vector2 v)
        {
            _dropRock = rock;
            _dropLeft = 0.35f;
            Physics2D.IgnoreCollision(heroCollider, rock, true);
            v.y = -dropSpeed;
            _dropHold = true;
            _grounded = false;
            _jumping = true;
            _lastGroundTime = -10f;
        }

        void StepGlide(ref Vector2 v, HeroMoveSettings s, float dt)
        {
            if (!s.canGlide)
            {
                _gliding = false;
                return;
            }

            if (!JumpHeld)
                _dropHold = false;
            bool can = !_grounded && JumpHeld && !_dropHold && _glideLeft > 0f && _hurtLeft <= 0f && _hoverLeft <= 0f;
            if (_gliding && (!can || _rising))
                _gliding = false;
            if (!_gliding && can && !_rising && v.y <= 0.5f)
            {
                _gliding = true;
                if (!_glideLifted)
                {
                    _glideLifted = true;
                    v.y = Mathf.Max(v.y, s.glideLift); // nhấc nhẹ lên lúc bắt đầu lượn (mỗi lần rời đất một lần)
                }
            }

            if (!_gliding)
                return;
            _glideLeft = Mathf.Max(0f, _glideLeft - dt);
            _glideFx -= dt;
            if (_glideFx <= 0f && CombatFx.Instance != null)
            {
                _glideFx = 0.08f;
                CombatFx.Instance.Burst(Feet + Vector2.up * 0.3f, SkillColors.WithAlpha(s.stepColor, 0.8f), 1, 1.2f, 0.09f, 0.45f, -1.2f);
            }
        }

        void StepRun(ref Vector2 v, HeroMoveSettings s, float dt)
        {
            float input = Run;
            if (_rootLeft > 0f || _hurtLeft > 0f)
                input = 0f;
            else if (_combat != null && _combat.Busy)
                input *= s.attackMoveScale;

            float max = _gliding ? s.glideSpeed : s.runSpeed;
            float target = input * max;
            bool turning = Mathf.Abs(target) > 0.01f && Mathf.Abs(v.x) > 0.1f && Mathf.Sign(target) != Mathf.Sign(v.x);
            float accel;
            if (_grounded)
                accel = Mathf.Abs(input) < 0.01f ? s.groundDeceleration : turning ? s.turnAcceleration : s.groundAcceleration;
            else
                accel = Mathf.Abs(input) < 0.01f ? s.airDeceleration : turning ? s.airAcceleration * 1.5f : s.airAcceleration;
            if (_hoverLeft > 0f)
                accel = Mathf.Max(accel, 30f);
            v.x = Mathf.MoveTowards(v.x, target, accel * dt);
        }

        void StepGravity(ref Vector2 v, HeroMoveSettings s, float dt)
        {
            if (_hoverLeft > 0f)
            {
                _hoverLeft -= dt;
                v.y = Mathf.MoveTowards(v.y, 0f, 60f * dt);
                return;
            }

            if (_grounded && v.y <= 0f)
            {
                v.y = -1f; // ép nhẹ xuống mặt đang đứng cho luôn chạm (trọng lực của Rigidbody đang tắt)
                return;
            }

            if (_gliding)
            {
                v.y = Mathf.Max(v.y - s.glideGravity * _gravityScale * dt, -s.glideFallSpeed);
                return;
            }

            if (_rising && v.y > 0f && !JumpHeld)
                _jumpCut = true;
            if (v.y <= 0f)
                _rising = _jumpCut = false;

            float g = s.Gravity * _gravityScale;
            if (v.y > 0f && _jumpCut)
                g *= s.jumpCutGravityScale;
            else if (v.y <= 0f)
                g *= s.fallGravityScale;
            v.y = Mathf.Max(v.y - g * dt, -s.maxFallSpeed);
        }

        void StepDash(ref Vector2 v, float dt)
        {
            _dashLeft -= dt;
            if (_dashLeft <= 0f)
            {
                EndDash(ref v);
                return;
            }

            v = _dashVelocity;
            if (_dashTrail && CombatFx.Instance != null)
                CombatFx.Instance.SpeedLines(Chest, _dashVelocity, _dashColor);
        }

        void EndDash(ref Vector2 v)
        {
            _dashing = false;
            Vector2 to = Chest;
            if (_dashHop > 0f)
            {
                v = new Vector2(_dashVelocity.x * 0.3f, _dashHop);
                _jumping = true;
                _grounded = false;
            }
            else
                v = _dashVelocity * 0.3f;

            if (_dashIsStep)
            {
                _pose = HeroMove.DoubleJump;
                _poseLeft = Settings.airJumpPoseTime;
            }

            CombatFx fx = CombatFx.Instance;
            if (_dashTrail && fx != null)
            {
                // Vệt bóng: dải sáng từ chỗ bắt đầu lướt tới chỗ dừng, đốm sáng bay lên dọc đường, loé nhẹ ở chỗ dừng
                Color edge = Color.Lerp(_dashColor, Color.black, 0.5f);
                fx.Streak(_dashFrom, to, SkillColors.WithAlpha(_dashColor, 0.85f), edge, 0.9f, 0.45f);
                fx.Streak(_dashFrom + Vector2.down * 0.55f, to + Vector2.down * 0.55f, SkillColors.WithAlpha(_dashColor, 0.5f), edge, 0.5f, 0.38f);
                for (int i = 0; i <= 4; i++)
                    fx.Burst(Vector2.Lerp(_dashFrom, to, i / 4f), SkillColors.WithAlpha(_dashColor, 0.75f), 2, 1.2f, 0.12f, 0.5f, -1.5f);
                fx.Flash(to, 1.3f, _dashColor, 0.2f, true);
            }

            _dashIsStep = false;
        }

        void StepFlight(ref Vector2 v, float dt)
        {
            _flyLeft -= dt;
            Vector2 want = _stick * _flySpeed;
            if (_rootLeft > 0f || _hurtLeft > 0f)
                want = Vector2.zero;
            v = Vector2.MoveTowards(v, want, 70f * dt);
            _gliding = false;
            if (_flyLeft <= 0f)
                _jumping = true;
        }

        // ---------- Sàn, bục một chiều ----------

        bool CheckGround(Vector2 v)
        {
            _ground = null;
            if (v.y > 0.05f || _dashing && _dashVelocity.y > 0.05f)
                return false;

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetNormalAngle(45f, 135f); // chỉ mặt tiếp xúc hướng lên
            int count = _body.GetContacts(filter, _contacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D c = _contacts[i];
                Collider2D other = c.collider != null && c.collider.attachedRigidbody == _body ? c.otherCollider : c.collider;
                if (other == null || other == _dropRock)
                    continue;
                _ground = other;
                return true;
            }

            return false;
        }

        // Tảng đá gần người: chân đã ở trên mặt đá thì va chạm (đứng lên được), chân còn thấp hơn mặt đá thì xuyên qua
        void UpdatePlatforms(float dt)
        {
            if (_dropRock != null)
            {
                _dropLeft -= dt;
                if (_dropLeft <= 0f || Feet.y < _dropRock.bounds.min.y)
                    _dropRock = null;
            }

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(_rockMask);
            int count = Physics2D.OverlapBox(_body.position + Vector2.up * 0.5f, new Vector2(6f, 9f), 0f, filter, _near);
            float feet = Feet.y;
            bool flying = _flyLeft > 0f;
            for (int i = 0; i < count; i++)
            {
                Collider2D rock = _near[i];
                bool solid = !flying && rock != _dropRock && feet >= rock.bounds.max.y - 0.06f;
                if (Physics2D.GetIgnoreCollision(heroCollider, rock) == solid)
                    Physics2D.IgnoreCollision(heroCollider, rock, !solid);
            }
        }

        bool IsRock(Collider2D c) => c != null && (_rockMask.value & (1 << c.gameObject.layer)) != 0;

        // Thân hình người đặt ở center có vướng sàn / vách / trần không (đá là bục một chiều nên không tính)
        bool Blocked(Vector2 center)
        {
            if (heroCollider == null)
                return false;
            int mask = Physics2D.GetLayerCollisionMask(gameObject.layer) & ~_rockMask.value;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(mask);
            Vector2 size = heroCollider.size * 0.95f;
            int count = Physics2D.OverlapCapsule(center + heroCollider.offset, size, heroCollider.direction, 0f, filter, _near);
            for (int i = 0; i < count; i++)
            {
                if (_near[i].attachedRigidbody != _body)
                    return true;
            }

            return false;
        }

        float Facing => _controller != null ? _controller.Facing : 1f;

        // =============================================================================================================
        // Hình
        // =============================================================================================================

        void LateUpdate()
        {
            if (!Active)
                return;
            if (Visual == null || (Visual is UnityEngine.Object o && o == null))
                return;
            Visual.Facing = Facing;
            Visual.SetMove(Move, _body.linearVelocity);
        }

        HeroMove CurrentMove()
        {
            Vector2 v = _body.linearVelocity;
            if (_hurtLeft > 0f)
                return HeroMove.Hurt;
            if (_dashing)
                return HeroMove.Dash;
            if (_gliding || _flyLeft > 0f)
                return HeroMove.Glide;
            if (_poseLeft > 0f && (_pose != HeroMove.Land || _grounded))
                return _pose;
            if (!_grounded)
                return v.y > 0.3f ? HeroMove.Jump : HeroMove.Fall;
            return Mathf.Abs(v.x) > 0.4f ? HeroMove.Run : HeroMove.Idle;
        }
    }
}
