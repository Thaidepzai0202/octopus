using UnityEngine;
using UnityEngine.InputSystem;

namespace Octopus
{
    /// <summary>
    /// Dịch chuột / cảm ứng thành cử chỉ: nhấn, chạm nhanh (tap), giữ (hold), vuốt ngang (drag).
    /// Các component khác chỉ đọc kết quả, nên đổi sơ đồ điều khiển chỉ cần sửa file này.
    /// Bàn phím để thử trong Editor: A/D hoặc ←/→ = vuốt, W hoặc ↑ = giữ, Space = tap.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Nhấn rồi nhả trong khoảng này (giây) mà không vuốt thì tính là tap; lâu hơn là giữ")]
        [SerializeField, Range(0.05f, 0.5f)] float tapMaxDuration = 0.2f;
        [Tooltip("Vuốt ngang quá khoảng này (tỉ lệ chiều rộng màn hình) thì tính là drag")]
        [SerializeField, Range(0.01f, 0.2f)] float dragThreshold = 0.04f;

        InputAction _press;
        InputAction _position;
        InputAction _keyMove;
        InputAction _keyHold;
        InputAction _keyTap;

        float _pressTime;
        bool _dragging;
        bool _consumed;

        /// <summary>Vừa đặt ngón tay / click xuống trong frame này.</summary>
        public bool PressedThisFrame { get; private set; }

        /// <summary>Vị trí màn hình lúc đặt tay xuống.</summary>
        public Vector2 PressPosition { get; private set; }

        /// <summary>Vừa chạm nhanh rồi nhả trong frame này (không tính lần nhấn đã bị ConsumePress).</summary>
        public bool TappedThisFrame { get; private set; }

        /// <summary>Đang giữ tay quá tapMaxDuration mà không vuốt.</summary>
        public bool IsHolding { get; private set; }

        /// <summary>-1, 0, 1: đang vuốt sang trái / không vuốt / sang phải, so với chỗ đặt tay.</summary>
        public float Drag { get; private set; }

        /// <summary>Lần nhấn hiện tại đã được dùng (ví dụ để bắn xúc tu) → nhả tay sẽ không tính là tap.</summary>
        public void ConsumePress() => _consumed = true;

        void Awake()
        {
            // <Pointer> bao cả chuột lẫn màn hình cảm ứng
            _press = new InputAction("Press", InputActionType.Button, "<Pointer>/press");
            _position = new InputAction("Position", InputActionType.PassThrough, "<Pointer>/position");

            _keyMove = new InputAction("KeyMove", InputActionType.Value);
            _keyMove.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            _keyMove.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");

            _keyHold = new InputAction("KeyHold", InputActionType.Button);
            _keyHold.AddBinding("<Keyboard>/w");
            _keyHold.AddBinding("<Keyboard>/upArrow");

            _keyTap = new InputAction("KeyTap", InputActionType.Button, "<Keyboard>/space");
        }

        void OnEnable()
        {
            _press.Enable();
            _position.Enable();
            _keyMove.Enable();
            _keyHold.Enable();
            _keyTap.Enable();
        }

        void OnDisable()
        {
            _press.Disable();
            _position.Disable();
            _keyMove.Disable();
            _keyHold.Disable();
            _keyTap.Disable();

            PressedThisFrame = TappedThisFrame = IsHolding = false;
            Drag = 0f;
        }

        void OnDestroy()
        {
            _press.Dispose();
            _position.Dispose();
            _keyMove.Dispose();
            _keyHold.Dispose();
            _keyTap.Dispose();
        }

        void Update()
        {
            Vector2 pointer = _position.ReadValue<Vector2>();
            float pressDuration = Time.unscaledTime - _pressTime;

            PressedThisFrame = _press.WasPressedThisFrame();
            if (PressedThisFrame)
            {
                _pressTime = Time.unscaledTime;
                pressDuration = 0f;
                PressPosition = pointer;
                _dragging = false;
                _consumed = false;
            }

            float drag = 0f;
            bool holding = false;
            if (_press.IsPressed())
            {
                float dx = (pointer.x - PressPosition.x) / Screen.width;
                if (Mathf.Abs(dx) > dragThreshold)
                    _dragging = true;

                // Đã vuốt thì giống cần điều khiển: kéo tay về gần chỗ đặt ban đầu là dừng
                if (_dragging)
                    drag = Mathf.Abs(dx) > dragThreshold * 0.5f ? Mathf.Sign(dx) : 0f;
                else
                    holding = pressDuration > tapMaxDuration;
            }

            TappedThisFrame = _press.WasReleasedThisFrame() && !_dragging && !_consumed && pressDuration <= tapMaxDuration;

            float keyMove = _keyMove.ReadValue<float>();
            if (keyMove != 0f)
                drag = Mathf.Sign(keyMove);
            if (_keyHold.IsPressed())
                holding = true;
            if (_keyTap.WasPressedThisFrame())
                TappedThisFrame = true;

            Drag = drag;
            IsHolding = holding;
        }
    }
}
