using System;
using UnityEngine;

namespace Octopus
{
    public enum OctopusState
    {
        Grounded,
        Airborne,
        Swinging,
        Clinging,
    }

    /// <summary>
    /// "Bộ não" của bạch tuộc: máy trạng thái biến cử chỉ thành hành động.
    ///
    ///  Trạng thái | Chạm vào đá trong tầm | Chạm nhanh (tap) | Giữ tay           | Vuốt ngang
    ///  -----------|-----------------------|------------------|-------------------|----------------
    ///  Grounded   | bắn xúc tu + giật lên | nhảy             | —                 | bò trái / phải
    ///  Airborne   | bắn xúc tu            | —                | —                 | —
    ///  Swinging   | đổi sang đá khác      | buông, văng đi   | rút lên tới đá    | bơm đu
    ///  Clinging   | bắn sang đá khác      | thả xuống        | —                 | bò dọc theo đá
    ///
    /// Nhân vật người (RED, PINK, BLUE — có HeroMotor đang bật): không đu, không bám; cần điều hướng trái / phải
    /// để chạy, nút nhảy để nhảy / nhảy đúp, kéo lên / xuống để đổi kiểu chiêu. Lúc đó lớp này chuyển input sang HeroMotor
    /// (HeroMotor.Simulate mỗi bước vật lý) và chỉ có 2 trạng thái Grounded / Airborne; Facing, HoldFacing, Knockback, ResetTo,
    /// SetIntent vẫn dùng như cũ.
    /// </summary>
    [RequireComponent(typeof(SwingController), typeof(SurfaceMover), typeof(PlayerInputReader))]
    public class OctopusController : MonoBehaviour
    {
        [Tooltip("Chạm cách tảng đá trong khoảng này (unit) vẫn tính là chạm trúng đá")]
        [SerializeField, Min(0f)] float touchRadius = 0.8f;

        [Header("Hướng mặt")]
        [Tooltip("Không vuốt mà thân trôi ngược hướng mặt nhanh hơn mức này (unit/giây) trên sàn / trên không thì mới tính là " +
                 "đổi hướng. Đang đu, đang rút xúc tu, đang bám đá thì giữ nguyên hướng; vuốt thì quay ngay")]
        [SerializeField, Min(0f)] float turnSpeed = 1.5f;
        [Tooltip("...và trôi ngược liên tục trong bấy nhiêu giây (đổi chiều thoáng qua, bị hất lùi một chút thì không quay)")]
        [SerializeField, Min(0f)] float turnDelay = 0.25f;

        Rigidbody2D _body;
        SwingController _swing;
        SurfaceMover _mover;
        PlayerInputReader _input;
        HeroMotor _hero;
        CombatController _combat;
        Camera _camera;
        bool _holding;
        float _drag;
        float _holdFacingUntil;
        float _turnClock;

        public OctopusState State { get; private set; } = OctopusState.Airborne;

        /// <summary>1 = quay sang phải, -1 = quay sang trái.</summary>
        public float Facing { get; private set; } = 1f;

        public SwingController Swing => _swing;
        public SurfaceMover Mover => _mover;

        /// <summary>Bộ di chuyển của nhân vật người đang chơi; null với bạch tuộc / nhện.</summary>
        public HeroMotor Hero => _hero != null && _hero.Active ? _hero : null;

        /// <summary>Đang chơi nhân vật người (RED, PINK, BLUE).</summary>
        public bool IsHero => _hero != null && _hero.Active;

        public event Action<OctopusState> StateChanged;

        /// <summary>Vừa bật nhảy từ mặt đất.</summary>
        public event Action Jumped;

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _swing = GetComponent<SwingController>();
            _mover = GetComponent<SurfaceMover>();
            _input = GetComponent<PlayerInputReader>();
            _hero = GetComponent<HeroMotor>();
            _combat = GetComponent<CombatController>();
            _camera = Camera.main;
            _swing.ReachedAnchor += OnReachedAnchor;
            if (_hero != null)
                _hero.Jumped += OnHeroJumped;
        }

        void OnDestroy()
        {
            _swing.ReachedAnchor -= OnReachedAnchor;
            if (_hero != null)
                _hero.Jumped -= OnHeroJumped;
        }

        void Update()
        {
            if (IsHero)
            {
                if (_input.isActiveAndEnabled)
                    ReadHeroInput();
                UpdateHeroFacing();
                return;
            }

            if (_input.isActiveAndEnabled)
                ReadInput();

            ApplyIntent();
        }

        void FixedUpdate()
        {
            if (IsHero)
            {
                _hero.Simulate(Time.fixedDeltaTime);
                SetState(_hero.Grounded ? OctopusState.Grounded : OctopusState.Airborne);
                UpdateHeroFacing();
                return;
            }

            switch (State)
            {
                case OctopusState.Grounded:
                    _mover.CrawlOnGround(_drag);
                    if (!_mover.CheckGrounded())
                        SetState(OctopusState.Airborne);
                    break;

                case OctopusState.Airborne:
                    if (_mover.CheckGrounded())
                        SetState(OctopusState.Grounded);
                    break;

                case OctopusState.Clinging:
                    _mover.CrawlAlongCling(_drag);
                    break;
            }

            UpdateFacing();
        }

        // Vuốt thì quay ngay theo tay. Không vuốt: đang đu / rút xúc tu (nhịp đu đổi chiều liên tục, vòng qua đá thì vận tốc
        // ngang đảo dấu mấy lần), đang bám đá, hoặc chiêu đang giữ hướng → giữ nguyên. Trên sàn / trên không chỉ quay theo
        // vận tốc khi thân trôi ngược hướng mặt rõ ràng và đủ lâu, nên không lật qua lật lại
        void UpdateFacing()
        {
            if (_drag != 0f)
            {
                if (Mathf.Abs(_drag) > 0.5f)
                    Facing = Mathf.Sign(_drag);
                _turnClock = 0f;
                return;
            }

            bool hold = State is OctopusState.Swinging or OctopusState.Clinging || Time.time < _holdFacingUntil;
            if (hold || -_body.linearVelocity.x * Facing < turnSpeed)
            {
                _turnClock = 0f;
                return;
            }

            _turnClock += Time.fixedDeltaTime;
            if (_turnClock >= turnDelay)
            {
                Facing = -Facing;
                _turnClock = 0f;
            }
        }

        // Nhân vật người: cần điều hướng + nút nhảy (cần trên màn hình hoặc A / D, W / S, Space)
        void ReadHeroInput()
        {
            _hero.Stick = _input.Stick;
            if (_input.JumpPressedThisFrame)
                _hero.PressJump();
            else
                _hero.JumpHeld = _input.JumpHeld;
        }

        // Nhân vật người quay theo cần chạy ngay; đang ra chiêu / chiêu giữ hướng / đang lướt thì giữ nguyên hướng
        void UpdateHeroFacing()
        {
            float run = _hero.Run;
            bool hold = Time.time < _holdFacingUntil || (_combat != null && _combat.Busy) || _hero.IsDashing;
            if (!hold && Mathf.Abs(run) > 0.2f)
                Facing = Mathf.Sign(run);
            _turnClock = 0f;
        }

        void OnHeroJumped(bool air)
        {
            if (!air)
                Jumped?.Invoke();
        }

        void ReadInput()
        {
            if (_input.PressedThisFrame && Grab(_camera.ScreenToWorldPoint(_input.PressPosition)))
                _input.ConsumePress();

            if (_input.TappedThisFrame)
                Tap();

            _holding = _input.IsHolding;
            _drag = _input.Drag;
        }

        /// <summary>Chạm vào một điểm: có đá trong tầm xúc tu ở đó thì bắn xúc tu tới.</summary>
        public bool Grab(Vector2 worldPoint)
        {
            if (IsHero)
                return false; // nhân vật người không đu dây
            Collider2D rock = Physics2D.OverlapCircle(worldPoint, touchRadius, _swing.AnchorMask);
            if (rock == null)
                return false;

            // Nhắm vào bên trong đá một chút để tia không trượt qua mép
            Vector2 aim = worldPoint;
            if (!rock.OverlapPoint(worldPoint))
            {
                Vector2 edge = rock.ClosestPoint(worldPoint);
                aim = edge + (edge - worldPoint).normalized * 0.1f;
            }

            if (!_swing.FindAnchor(aim, out RaycastHit2D hit))
                return false;
            if (State == OctopusState.Clinging && hit.collider == _mover.ClingSurface)
                return false;

            bool launchFromGround = State == OctopusState.Grounded;
            _mover.StopCling();
            _swing.Attach(hit, launchFromGround);
            SetState(OctopusState.Swinging);

            // Đu thì giữ nguyên hướng mặt suốt nhịp đu, nên quay luôn về phía đá vừa bám (đá hẳn về một bên) cho khỏi đu giật lùi
            float side = hit.point.x - _body.position.x;
            if (Mathf.Abs(side) > 1f && Time.time >= _holdFacingUntil)
                Facing = Mathf.Sign(side);
            _turnClock = 0f;
            return true;
        }

        /// <summary>Chạm nhanh: dưới sàn thì nhảy, đang đu thì buông, đang bám đá thì thả xuống.</summary>
        public void Tap()
        {
            if (IsHero)
            {
                _hero.PressJump(); // chạm nhanh = bấm rồi nhả: nhảy thấp
                _hero.JumpHeld = false;
                return;
            }

            switch (State)
            {
                case OctopusState.Grounded:
                    _mover.Jump();
                    Jumped?.Invoke();
                    break;

                case OctopusState.Swinging:
                    _swing.Release();
                    SetState(OctopusState.Airborne);
                    break;

                case OctopusState.Clinging:
                    _mover.StopCling(drop: true);
                    SetState(OctopusState.Airborne);
                    break;
            }
        }

        /// <summary>Điều khiển trực tiếp (test, tutorial, AI). Tắt PlayerInputReader để input thật không ghi đè.</summary>
        public void SetIntent(bool holding, float drag)
        {
            _holding = holding;
            _drag = drag;
            if (IsHero)
            {
                _hero.Stick = new Vector2(drag, _hero.Stick.y); // nhân vật người: drag = chạy
                return;
            }

            ApplyIntent();
        }

        /// <summary>Nhân vật người: kéo cần lên (1) / xuống (-1) / thả (0) để chọn kiểu chiêu (test, AI). Kéo lên / xuống thì không chạy.</summary>
        public void SetAim(int aim)
        {
            if (IsHero)
                _hero.Stick = aim != 0 ? new Vector2(0f, Mathf.Sign(aim)) : new Vector2(_hero.Stick.x, 0f);
        }

        /// <summary>Nhân vật người: bấm nút nhảy và giữ (test, AI). Bấm lần nữa trên không = nhảy đúp.</summary>
        public void PressJump()
        {
            if (IsHero)
                _hero.PressJump();
        }

        /// <summary>Nhân vật người: nhả nút nhảy (đang bay lên thì nhảy thấp lại).</summary>
        public void ReleaseJump()
        {
            if (IsHero)
                _hero.JumpHeld = false;
        }

        void ApplyIntent()
        {
            if (IsHero)
                return;
            _swing.Reeling = State == OctopusState.Swinging && _holding;
            _swing.PumpInput = State == OctopusState.Swinging ? _drag : 0f;
        }

        /// <summary>Giữ hướng mặt trong seconds giây dù thân bị hất lùi (chiêu bật ra sau); kéo tay vẫn quay được.</summary>
        public void HoldFacing(float seconds) => _holdFacingUntil = Mathf.Max(_holdFacingUntil, Time.time + seconds);

        /// <summary>
        /// Bị đánh văng (đòn của boss): đang bám đá thì bị hất khỏi đá, đang đu thì vẫn giữ xúc tu nhưng bị đẩy theo nhịp đu,
        /// đứng / bay thì bay theo velocity. Giữ hướng mặt một lúc cho khỏi quay lưng vì bị hất lùi.
        /// </summary>
        public void Knockback(Vector2 velocity)
        {
            if (IsHero)
            {
                _hero.Knockback(velocity);
                if (velocity.y > 0.5f)
                    SetState(OctopusState.Airborne);
                HoldFacing(0.6f);
                _turnClock = 0f;
                return;
            }

            if (State == OctopusState.Clinging)
            {
                _mover.StopCling();
                SetState(OctopusState.Airborne);
            }

            _body.linearVelocity = State == OctopusState.Swinging ? _body.linearVelocity + velocity : velocity;
            if (State == OctopusState.Swinging)
                _swing.ResetHeldEnergy(); // cú hất cộng vào nhịp đu, không bị cơ chế giữ cơ năng triệt mất
            if (State == OctopusState.Grounded && velocity.y > 0.5f)
                SetState(OctopusState.Airborne);
            HoldFacing(0.6f);
            _turnClock = 0f;
        }

        public void ResetTo(Vector2 position)
        {
            if (IsHero)
            {
                _hero.ResetTo(position);
                _turnClock = 0f;
                SetState(OctopusState.Airborne);
                return;
            }

            _swing.Release(boost: false);
            _mover.StopCling();
            _body.position = position;
            _body.linearVelocity = Vector2.zero;
            _turnClock = 0f;
            SetState(OctopusState.Airborne);
        }

        void OnReachedAnchor()
        {
            Collider2D rock = _swing.AnchorCollider;
            Vector2 point = _swing.AnchorPoint;
            Vector2 normal = _swing.AnchorNormal;
            _swing.Release(boost: false);

            // Tới mặt trên của đá thì đứng lên đó như sàn; mặt dưới / mặt bên thì bám vào
            if (normal.y > 0.5f)
            {
                SetState(OctopusState.Airborne);
                return;
            }

            _mover.StartCling(rock, point, normal);
            SetState(OctopusState.Clinging);
        }

        void SetState(OctopusState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
