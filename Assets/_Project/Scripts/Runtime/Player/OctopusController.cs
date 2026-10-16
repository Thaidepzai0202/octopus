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
    /// </summary>
    [RequireComponent(typeof(SwingController), typeof(SurfaceMover), typeof(PlayerInputReader))]
    public class OctopusController : MonoBehaviour
    {
        [Tooltip("Chạm cách tảng đá trong khoảng này (unit) vẫn tính là chạm trúng đá")]
        [SerializeField, Min(0f)] float touchRadius = 0.8f;

        Rigidbody2D _body;
        SwingController _swing;
        SurfaceMover _mover;
        PlayerInputReader _input;
        Camera _camera;
        bool _holding;
        float _drag;
        float _holdFacingUntil;

        public OctopusState State { get; private set; } = OctopusState.Airborne;

        /// <summary>1 = quay sang phải, -1 = quay sang trái.</summary>
        public float Facing { get; private set; } = 1f;

        public SwingController Swing => _swing;
        public SurfaceMover Mover => _mover;

        public event Action<OctopusState> StateChanged;

        /// <summary>Vừa bật nhảy từ mặt đất.</summary>
        public event Action Jumped;

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _swing = GetComponent<SwingController>();
            _mover = GetComponent<SurfaceMover>();
            _input = GetComponent<PlayerInputReader>();
            _camera = Camera.main;
            _swing.ReachedAnchor += OnReachedAnchor;
        }

        void OnDestroy()
        {
            _swing.ReachedAnchor -= OnReachedAnchor;
        }

        void Update()
        {
            if (_input.isActiveAndEnabled)
                ReadInput();

            ApplyIntent();
        }

        void FixedUpdate()
        {
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

            bool holdFacing = State == OctopusState.Clinging || Time.time < _holdFacingUntil;
            float direction = _drag != 0f ? _drag : holdFacing ? 0f : _body.linearVelocity.x;
            if (Mathf.Abs(direction) > 0.5f)
                Facing = Mathf.Sign(direction);
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
            return true;
        }

        /// <summary>Chạm nhanh: dưới sàn thì nhảy, đang đu thì buông, đang bám đá thì thả xuống.</summary>
        public void Tap()
        {
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
            ApplyIntent();
        }

        void ApplyIntent()
        {
            _swing.Reeling = State == OctopusState.Swinging && _holding;
            _swing.PumpInput = State == OctopusState.Swinging ? _drag : 0f;
        }

        /// <summary>Giữ hướng mặt trong seconds giây dù thân bị hất lùi (chiêu bật ra sau); kéo tay vẫn quay được.</summary>
        public void HoldFacing(float seconds) => _holdFacingUntil = Mathf.Max(_holdFacingUntil, Time.time + seconds);

        public void ResetTo(Vector2 position)
        {
            _swing.Release(boost: false);
            _mover.StopCling();
            _body.position = position;
            _body.linearVelocity = Vector2.zero;
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
