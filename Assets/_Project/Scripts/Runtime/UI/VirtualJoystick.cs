using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Cần điều hướng góc dưới bên trái cho nhân vật người. Đặt ngón tay ở đâu trong vùng thì đế cần hiện ra ở đó (cần nổi),
    /// kéo núm đi: trái / phải = chạy (kéo nhẹ đi chậm, kéo hết cỡ chạy nhanh nhất), lên / xuống = không đi mà đổi kiểu chiêu
    /// khi bấm nút chiêu (mũi tên ▲ / ▼ sáng lên). Nhả tay thì đế về chỗ cũ. Giá trị gửi vào PlayerInputReader.SetStick.
    /// Vùng cần tự đăng ký chặn với PlayerInputReader nên chạm vào đây không tính là cử chỉ bò / nhảy / đu.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] PlayerInputReader input;
        [Tooltip("Đế cần (vòng tròn lớn), con của vùng này")]
        [SerializeField] RectTransform stickBase;
        [Tooltip("Núm cần, con của đế")]
        [SerializeField] RectTransform knob;
        [Tooltip("Mũi tên ▲ (lên) — sáng khi đang kéo lên để chọn chiêu ↑")]
        [SerializeField] Image upArrow;
        [Tooltip("Mũi tên ▼ (xuống) — sáng khi đang kéo xuống để chọn chiêu ↓")]
        [SerializeField] Image downArrow;
        [Tooltip("Mũi tên ◀ ▶ — sáng khi đang chạy sang bên đó")]
        [SerializeField] Image leftArrow;
        [SerializeField] Image rightArrow;
        [Tooltip("Kéo núm xa nhất bấy nhiêu (đơn vị canvas, màn hình cao 1080)")]
        [SerializeField, Min(10f)] float radius = 115f;
        [Tooltip("Kéo trong vùng này (phần bán kính) thì coi như chưa kéo")]
        [SerializeField, Range(0f, 0.6f)] float deadZone = 0.2f;
        [Tooltip("Bật: đặt ngón tay ở đâu trong vùng thì đế hiện ở đó. Tắt: đế cố định")]
        [SerializeField] bool floating = true;
        [SerializeField] Color idleColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] Color activeColor = new Color(1f, 0.85f, 0.35f, 1f);

        RectTransform _zone;
        Vector2 _home;
        int _pointer = int.MinValue;
        Vector2 _value;

        /// <summary>Giá trị cần -1..1 (đã trừ vùng chết).</summary>
        public Vector2 Value => _value;

        /// <summary>Đang có ngón tay giữ cần.</summary>
        public bool IsHeld => _pointer != int.MinValue;

        public RectTransform Zone => _zone != null ? _zone : (RectTransform)transform;
        public RectTransform Base => stickBase;

        void Awake()
        {
            _zone = (RectTransform)transform;
            if (stickBase != null)
                _home = stickBase.anchoredPosition;
        }

        void OnEnable()
        {
            if (input != null)
                input.AddBlocker((RectTransform)transform);
        }

        void OnDisable()
        {
            if (input != null)
            {
                input.RemoveBlocker((RectTransform)transform);
                input.SetStick(Vector2.zero);
            }

            Release();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (IsHeld)
                return;
            _pointer = data.pointerId;
            if (floating && stickBase != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, data.position, data.pressEventCamera, out Vector2 local))
            {
                // Đế hiện dưới ngón tay nhưng không lòi ra ngoài vùng
                Rect r = _zone.rect;
                float m = stickBase.rect.width * 0.5f;
                local.x = Mathf.Clamp(local.x, r.xMin + m, r.xMax - m);
                local.y = Mathf.Clamp(local.y, r.yMin + m, r.yMax - m);
                stickBase.localPosition = local;
            }

            OnDrag(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId != _pointer || stickBase == null)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stickBase, data.position, data.pressEventCamera, out Vector2 local))
                return;
            Set(local / radius);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId != _pointer)
                return;
            Release();
        }

        /// <summary>Đặt cần theo độ lệch offset (đơn vị bán kính, -1..1) như đang kéo thật (test, hướng dẫn).</summary>
        public void Set(Vector2 offset)
        {
            offset = Vector2.ClampMagnitude(offset, 1f);
            if (knob != null)
                knob.anchoredPosition = offset * radius;
            float magnitude = offset.magnitude;
            _value = magnitude <= deadZone ? Vector2.zero : offset / magnitude * ((magnitude - deadZone) / (1f - deadZone));
            if (input != null)
                input.SetStick(_value);
        }

        public void Release()
        {
            _pointer = int.MinValue;
            _value = Vector2.zero;
            if (knob != null)
                knob.anchoredPosition = Vector2.zero;
            if (stickBase != null && _zone != null)
                stickBase.anchoredPosition = _home;
            if (input != null)
                input.SetStick(Vector2.zero);
        }

        void Update()
        {
            // Mũi tên sáng theo hướng đang chọn (lấy từ input: cả bàn phím W / S, A / D cũng sáng)
            Vector2 stick = input != null ? input.Stick : _value;
            int aim = HeroMotor.AimOf(stick);
            float run = HeroMotor.RunOf(stick);
            Tint(upArrow, aim > 0);
            Tint(downArrow, aim < 0);
            Tint(leftArrow, run < -0.05f);
            Tint(rightArrow, run > 0.05f);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 10f);
            Scale(upArrow, aim > 0 ? pulse : 1f);
            Scale(downArrow, aim < 0 ? pulse : 1f);
        }

        void Tint(Image arrow, bool on)
        {
            if (arrow != null)
                arrow.color = on ? activeColor : idleColor;
        }

        static void Scale(Image arrow, float scale)
        {
            if (arrow != null)
                arrow.rectTransform.localScale = Vector3.one * scale;
        }
    }
}
