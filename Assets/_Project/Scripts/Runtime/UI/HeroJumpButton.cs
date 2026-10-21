using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Nút NHẢY của nhân vật người (góc dưới bên phải, cạnh cụm nút chiêu): bấm = nhảy, bấm lần nữa trên không = nhảy đúp,
    /// giữ lâu thì nhảy cao hơn. Kéo cần xuống + bấm khi đứng trên tảng đá = thả xuống qua đá.
    /// Gửi vào PlayerInputReader.SetJumpButton; tự đăng ký vùng chặn nên bấm nút không tính là cử chỉ khác.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HeroJumpButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] PlayerInputReader input;
        [Tooltip("Mặt nút (phóng to / thu nhỏ khi bấm)")]
        [SerializeField] RectTransform face;
        [SerializeField] Image ring;
        [SerializeField] Image halo;
        [SerializeField] Color idleColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] Color pressedColor = Color.white;

        int _pointer = int.MinValue;
        float _pop;

        public bool IsPressed => _pointer != int.MinValue;

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
                if (IsPressed)
                    input.SetJumpButton(false);
            }

            _pointer = int.MinValue;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (IsPressed)
                return;
            _pointer = data.pointerId;
            _pop = 1f;
            if (input != null)
                input.SetJumpButton(true);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId != _pointer)
                return;
            _pointer = int.MinValue;
            if (input != null)
                input.SetJumpButton(false);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool held = IsPressed || (input != null && input.JumpHeld);
            _pop = Mathf.MoveTowards(_pop, 0f, dt * 5f);
            if (face != null)
                face.localScale = Vector3.one * ((held ? 0.92f : 1f) + 0.1f * _pop);
            Color c = held ? pressedColor : idleColor;
            if (ring != null)
                ring.color = c;
            if (halo != null)
                halo.color = new Color(c.r, c.g, c.b, held ? 0.7f : 0.15f);
        }
    }
}
