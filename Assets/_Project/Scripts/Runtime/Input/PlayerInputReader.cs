using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Octopus
{
    /// <summary>
    /// Dịch chuột / cảm ứng thành cử chỉ: nhấn, chạm nhanh (tap), giữ (hold), vuốt ngang (drag).
    /// Các component khác chỉ đọc kết quả, nên đổi sơ đồ điều khiển chỉ cần sửa file này.
    /// Bàn phím để thử trong Editor: A/D hoặc ←/→ = vuốt, W hoặc ↑ = giữ, Space = tap; J / K / L = nút chiêu 1 / 2 / 3 (giữ một
    /// phím rồi bấm phím kia = vuốt từ nút này sang nút kia), R = thêm nộ để thử chiêu cuối.
    /// Chạm bắt đầu trên nút trên màn hình (nút chiêu…) thì bỏ qua cả lần chạm đó, để bấm nút không làm nhảy / bắn xúc tu.
    ///
    /// Nhân vật người (RED, PINK, BLUE) dùng cần điều hướng + nút nhảy thay cho cử chỉ: Stick (cần trên màn hình
    /// VirtualJoystick gửi vào bằng SetStick, hoặc A / D, W / S, phím mũi tên), Run / Aim (chạy, kéo lên / xuống đổi kiểu chiêu),
    /// JumpHeld / JumpPressedThisFrame (nút NHẢY trên màn hình gửi vào bằng SetJumpButton, hoặc Space).
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
        InputAction _keyVertical;
        InputAction _keyHold;
        InputAction _keyTap;
        // Phím chiêu trên máy tính: J = nút 1 (đánh thường), K = nút 2 (chiêu), L = nút 3 (chiêu đặc biệt)
        static readonly string[] SkillKeys = { "j", "k", "l" };
        readonly InputAction[] _keySkills = new InputAction[3];
        InputAction _keyRage;

        readonly List<RectTransform> _blockers = new List<RectTransform>();
        float _pressTime;
        bool _dragging;
        bool _consumed;
        bool _blocked; // lần chạm hiện tại bắt đầu trên nút: không tính là cử chỉ điều khiển
        Vector2 _screenStick;
        bool _jumpButton, _jumpButtonPressed;

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

        /// <summary>Nhân vật người: cần điều hướng -1..1 (x = chạy, y = kéo lên / xuống), từ cần trên màn hình hoặc bàn phím.</summary>
        public Vector2 Stick { get; private set; }

        /// <summary>Nhân vật người: chạy -1..1 (0 khi đang kéo cần lên / xuống).</summary>
        public float Run => HeroMotor.RunOf(Stick);

        /// <summary>Nhân vật người: kéo cần lên (1) / xuống (-1) / không (0) — kiểu chiêu khi bấm nút chiêu.</summary>
        public int Aim => HeroMotor.AimOf(Stick);

        /// <summary>Nhân vật người: đang giữ nút nhảy (nút NHẢY trên màn hình hoặc Space).</summary>
        public bool JumpHeld { get; private set; }

        /// <summary>Nhân vật người: vừa bấm nút nhảy trong frame này.</summary>
        public bool JumpPressedThisFrame { get; private set; }

        /// <summary>Cần điều hướng trên màn hình (VirtualJoystick) gửi giá trị -1..1 vào đây mỗi khi kéo / thả.</summary>
        public void SetStick(Vector2 value) => _screenStick = Vector2.ClampMagnitude(value, 1f);

        /// <summary>Nút NHẢY trên màn hình gửi vào đây lúc bấm (true) / nhả (false).</summary>
        public void SetJumpButton(bool held)
        {
            if (held && !_jumpButton)
                _jumpButtonPressed = true;
            _jumpButton = held;
        }

        /// <summary>Phím chiêu J / K / L (nút 1 / 2 / 3, slot 0..2) vừa bấm xuống trong frame này. Trên điện thoại dùng nút trên màn hình.</summary>
        public bool SkillKeyDown(int slot) => _keySkills[slot] != null && _keySkills[slot].WasPressedThisFrame();

        /// <summary>Phím chiêu vừa nhả ra trong frame này.</summary>
        public bool SkillKeyUp(int slot) => _keySkills[slot] != null && _keySkills[slot].WasReleasedThisFrame();

        /// <summary>Phím R (thêm nộ để thử chiêu cuối), chỉ trong Editor / bản build thử.</summary>
        public bool RageCheatPressed => Debug.isDebugBuild && _keyRage != null && _keyRage.WasPressedThisFrame();

        /// <summary>Lần nhấn hiện tại đã được dùng (ví dụ để bắn xúc tu) → nhả tay sẽ không tính là tap.</summary>
        public void ConsumePress() => _consumed = true;

        /// <summary>Vùng trên màn hình (nút UI) mà chạm vào đó không tính là cử chỉ điều khiển bạch tuộc.</summary>
        public void AddBlocker(RectTransform area)
        {
            if (area != null && !_blockers.Contains(area))
                _blockers.Add(area);
        }

        public void RemoveBlocker(RectTransform area) => _blockers.Remove(area);

        /// <summary>Điểm màn hình nằm trên một nút đang hiện (canvas Screen Space - Overlay).</summary>
        public bool IsOverBlocker(Vector2 screenPoint)
        {
            foreach (RectTransform area in _blockers)
            {
                if (area != null && area.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(area, screenPoint, null))
                    return true;
            }

            return false;
        }

        void Awake()
        {
            // <Pointer> bao cả chuột lẫn màn hình cảm ứng
            _press = new InputAction("Press", InputActionType.Button, "<Pointer>/press");
            _position = new InputAction("Position", InputActionType.PassThrough, "<Pointer>/position");

            _keyMove = new InputAction("KeyMove", InputActionType.Value);
            _keyMove.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            _keyMove.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");

            _keyVertical = new InputAction("KeyVertical", InputActionType.Value);
            _keyVertical.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/s").With("Positive", "<Keyboard>/w");
            _keyVertical.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");

            _keyHold = new InputAction("KeyHold", InputActionType.Button);
            _keyHold.AddBinding("<Keyboard>/w");
            _keyHold.AddBinding("<Keyboard>/upArrow");

            _keyTap = new InputAction("KeyTap", InputActionType.Button, "<Keyboard>/space");
            for (int i = 0; i < _keySkills.Length; i++)
                _keySkills[i] = new InputAction($"KeySkill{i + 1}", InputActionType.Button, $"<Keyboard>/{SkillKeys[i]}");
            _keyRage = new InputAction("KeyRage", InputActionType.Button, "<Keyboard>/r");
        }

        void OnEnable()
        {
            _press.Enable();
            _position.Enable();
            _keyMove.Enable();
            _keyVertical.Enable();
            _keyHold.Enable();
            _keyTap.Enable();
            foreach (InputAction key in _keySkills)
                key.Enable();
            _keyRage.Enable();
        }

        void OnDisable()
        {
            _press.Disable();
            _position.Disable();
            _keyMove.Disable();
            _keyVertical.Disable();
            _keyHold.Disable();
            _keyTap.Disable();
            foreach (InputAction key in _keySkills)
                key.Disable();
            _keyRage.Disable();

            PressedThisFrame = TappedThisFrame = IsHolding = false;
            Drag = 0f;
            Stick = Vector2.zero;
            JumpHeld = JumpPressedThisFrame = false;
            _jumpButtonPressed = false;
        }

        void OnDestroy()
        {
            _press.Dispose();
            _position.Dispose();
            _keyMove.Dispose();
            _keyVertical.Dispose();
            _keyHold.Dispose();
            _keyTap.Dispose();
            foreach (InputAction key in _keySkills)
                key.Dispose();
            _keyRage.Dispose();
        }

        void Update()
        {
            Vector2 pointer = _position.ReadValue<Vector2>();
            float pressDuration = Time.unscaledTime - _pressTime;

            bool pressed = _press.WasPressedThisFrame();
            if (pressed)
                _blocked = IsOverBlocker(pointer);
            PressedThisFrame = pressed && !_blocked;
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
            if (_press.IsPressed() && !_blocked)
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

            TappedThisFrame = _press.WasReleasedThisFrame() && !_blocked && !_dragging && !_consumed && pressDuration <= tapMaxDuration;

            float keyMove = _keyMove.ReadValue<float>();
            if (keyMove != 0f)
                drag = Mathf.Sign(keyMove);
            if (_keyHold.IsPressed())
                holding = true;
            if (_keyTap.WasPressedThisFrame())
                TappedThisFrame = true;

            Drag = drag;
            IsHolding = holding;

            // Nhân vật người: phím thắng cần trên màn hình theo từng trục
            float keyVertical = _keyVertical.ReadValue<float>();
            Vector2 stick = _screenStick;
            if (keyMove != 0f)
                stick.x = Mathf.Sign(keyMove);
            if (keyVertical != 0f)
                stick.y = Mathf.Sign(keyVertical);
            Stick = Vector2.ClampMagnitude(stick, 1.5f);
            bool jumpKey = _keyTap.IsPressed();
            JumpPressedThisFrame = _keyTap.WasPressedThisFrame() || _jumpButtonPressed;
            _jumpButtonPressed = false;
            JumpHeld = jumpKey || _jumpButton;
        }
    }
}
