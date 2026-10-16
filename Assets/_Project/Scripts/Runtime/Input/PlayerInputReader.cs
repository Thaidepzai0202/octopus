using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Octopus
{
    /// <summary>
    /// Dịch chuột / cảm ứng thành cử chỉ: nhấn, chạm nhanh (tap), giữ (hold), vuốt ngang (drag).
    /// Các component khác chỉ đọc kết quả, nên đổi sơ đồ điều khiển chỉ cần sửa file này.
    /// Bàn phím để thử trong Editor: A/D hoặc ←/→ = vuốt, W hoặc ↑ = giữ, Space = tap; 1 / 2 / 3 = nút chiêu (giữ một số rồi
    /// bấm số khác = vuốt từ nút này sang nút kia), R = thêm nộ để thử chiêu cuối.
    /// Chạm bắt đầu trên nút trên màn hình (nút chiêu…) thì bỏ qua cả lần chạm đó, để bấm nút không làm nhảy / bắn xúc tu.
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
        readonly InputAction[] _keySkills = new InputAction[3];
        InputAction _keyRage;

        readonly List<RectTransform> _blockers = new List<RectTransform>();
        float _pressTime;
        bool _dragging;
        bool _consumed;
        bool _blocked; // lần chạm hiện tại bắt đầu trên nút: không tính là cử chỉ điều khiển

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

        /// <summary>Phím chiêu 1 / 2 / 3 (slot 0..2) vừa bấm xuống trong frame này. Trên điện thoại dùng nút trên màn hình.</summary>
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

            _keyHold = new InputAction("KeyHold", InputActionType.Button);
            _keyHold.AddBinding("<Keyboard>/w");
            _keyHold.AddBinding("<Keyboard>/upArrow");

            _keyTap = new InputAction("KeyTap", InputActionType.Button, "<Keyboard>/space");
            for (int i = 0; i < _keySkills.Length; i++)
                _keySkills[i] = new InputAction($"KeySkill{i + 1}", InputActionType.Button, $"<Keyboard>/{i + 1}");
            _keyRage = new InputAction("KeyRage", InputActionType.Button, "<Keyboard>/r");
        }

        void OnEnable()
        {
            _press.Enable();
            _position.Enable();
            _keyMove.Enable();
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
            _keyHold.Disable();
            _keyTap.Disable();
            foreach (InputAction key in _keySkills)
                key.Disable();
            _keyRage.Disable();

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
        }
    }
}
