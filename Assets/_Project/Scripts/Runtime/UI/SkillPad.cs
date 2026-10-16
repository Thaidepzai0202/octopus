using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Cụm 3 nút chiêu góc dưới bên phải: nút 2 ở góc, nút 1 bên trái nút 2, nút 3 phía trên nút 2.
    /// Chạm rồi nhả = chiêu chính của nút. Giữ nút rồi vuốt về phía nút khác = kiểu chiêu khác (nút đích sáng lên, tên chiêu
    /// hiện ra; nhả tay là ra chiêu, kiểu vuốt-ngắm của Brawl Stars / Liên Quân). Nút 3 khoá tới khi nộ qua mốc 1.
    /// Mỗi chiêu hồi riêng: nút hiện hồi chiêu của kiểu chạm, đang giữ / vuốt thì hiện hồi chiêu của kiểu đang chọn.
    /// Chạm vào nút không làm bạch tuộc nhảy / bắn xúc tu.
    /// </summary>
    public class SkillPad : MonoBehaviour
    {
        [Serializable]
        public class PadButton
        {
            public RectTransform rect;
            public Image icon;
            [Tooltip("Lớp tối quét theo hồi chiêu / nộ còn thiếu")]
            public Image shade;
            public Image ring;
            public Image halo;
            [Tooltip("Số giây hồi chiêu / chữ trên nút")]
            public Text label;
        }

        [SerializeField] CombatController combat;
        [SerializeField] PlayerInputReader input;
        [Tooltip("Nút 1, 2, 3")]
        [SerializeField] PadButton[] buttons = new PadButton[3];
        [Tooltip("Tên chiêu đang chọn khi vuốt")]
        [SerializeField] Text skillName;
        [Tooltip("3 chấm báo mốc nộ trên nút 3")]
        [SerializeField] Image[] tierPips = Array.Empty<Image>();
        [SerializeField] Sprite[] octopusIcons = new Sprite[3];
        [SerializeField] Sprite[] spiderIcons = new Sprite[3];
        [Tooltip("Vuốt xa hơn bấy nhiêu (px, màn hình cao 1080) mới tính là vuốt sang nút khác")]
        [SerializeField] float swipeThreshold = 55f;
        [SerializeField] Color readyColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] Color targetColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] Color rageColor = new Color(1f, 0.45f, 0.3f);

        int _slot = -1;       // nút đang giữ
        int _pointer;
        Vector2 _down;
        int _variant;
        float _nameTimer;
        readonly float[] _pop = new float[3];

        public CombatController Combat => combat;

        /// <summary>Kiểu chiêu đang chọn khi vuốt (-1 = không giữ nút nào).</summary>
        public int HeldSlot => _slot;
        public int HeldVariant => _variant;

        void OnEnable()
        {
            if (input == null)
                return;
            foreach (PadButton b in buttons)
                input.AddBlocker(b.rect);
        }

        void OnDisable()
        {
            if (input == null)
                return;
            foreach (PadButton b in buttons)
                input.RemoveBlocker(b.rect);
        }

        // ---------- Chạm / vuốt (SkillPadButton gọi) ----------

        public void Press(int slot, PointerEventData data)
        {
            if (_slot >= 0)
                return; // đang giữ nút khác
            _slot = slot;
            _pointer = data.pointerId;
            _down = data.position;
            _variant = 0;
            ShowName();
        }

        public void Drag(int slot, PointerEventData data)
        {
            if (slot != _slot || data.pointerId != _pointer)
                return;
            int variant = Classify(slot, data.position - _down, Centers(), swipeThreshold * CanvasScale);
            if (variant != _variant)
            {
                _variant = variant;
                ShowName();
            }
        }

        public void Release(int slot, PointerEventData data)
        {
            if (slot != _slot || data.pointerId != _pointer)
                return;
            Drag(slot, data);
            Fire(_slot, _variant);
            _slot = -1;
        }

        /// <summary>Ra chiêu nút slot kiểu variant (như chạm / vuốt thật).</summary>
        public bool Fire(int slot, int variant)
        {
            bool used = combat != null && combat.Use(slot, variant);
            _pop[slot] = used ? 1f : -1f; // nảy khi ra chiêu, lắc khi chưa được
            return used;
        }

        /// <summary>Vuốt từ nút slot một đoạn drag (px): 0 = chưa vuốt / vuốt lệch, 1 / 2 = về phía nút khác theo thứ tự.</summary>
        public static int Classify(int slot, Vector2 drag, Vector2[] centers, float threshold)
        {
            if (drag.magnitude < threshold)
                return 0;

            int best = -1;
            float bestScore = 0.4f; // lệch quá ~65° so với hướng tới nút nào cũng không tính
            for (int other = 0; other < centers.Length; other++)
            {
                if (other == slot)
                    continue;
                float score = Vector2.Dot(drag.normalized, (centers[other] - centers[slot]).normalized);
                if (score > bestScore)
                {
                    best = other;
                    bestScore = score;
                }
            }

            return best < 0 ? 0 : CombatController.VariantToward(slot, best);
        }

        Vector2[] Centers()
        {
            var centers = new Vector2[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
                centers[i] = ScreenCenter(buttons[i].rect);
            return centers;
        }

        /// <summary>Tâm nút (px màn hình, canvas Overlay).</summary>
        public static Vector2 ScreenCenter(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        public RectTransform ButtonRect(int slot) => buttons[slot].rect;

        float CanvasScale
        {
            get
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                return canvas != null ? canvas.scaleFactor : 1f;
            }
        }

        void ShowName()
        {
            _nameTimer = 1.2f;
            Skill skill = combat != null ? combat.Get(_slot, _variant) : null;
            if (skillName == null || skill == null)
                return;
            float remaining = combat.CooldownRemaining(_slot, _variant);
            skillName.text = remaining > 0f ? $"{skill.name}  ({Mathf.CeilToInt(remaining)}s)" : skill.name;
        }

        // ---------- Hình ----------

        void Update()
        {
            if (combat == null)
                return;

            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            bool spider = combat.Character == PlayableCharacter.Spider;
            int tier = combat.Rage.Tier;
            int target = _slot >= 0 && _variant > 0 ? TargetOf(_slot, _variant) : -1;

            for (int i = 0; i < buttons.Length; i++)
            {
                PadButton b = buttons[i];
                Sprite[] icons = spider ? spiderIcons : octopusIcons;
                if (icons != null && i < icons.Length && icons[i] != null)
                    b.icon.sprite = icons[i];

                int variant = i == _slot ? _variant : 0; // kiểu chiêu nút này đang hiện hồi chiêu
                bool locked = i == 2 && tier < 1;
                float remaining = combat.CooldownRemaining(i, variant);
                float shade = locked ? 1f - Mathf.Clamp01(combat.Rage.Value / RageMeter.PerTier) : combat.CooldownFraction(i, variant);
                b.shade.fillAmount = shade;
                b.shade.enabled = shade > 0.001f;
                b.icon.color = locked || remaining > 0f ? new Color(0.45f, 0.45f, 0.55f) : Color.white;

                b.label.text = remaining > 0.5f ? Mathf.CeilToInt(remaining).ToString() : locked ? "NỘ" : "";

                // Viền: nút đang giữ sáng trắng, nút đích khi vuốt màu vàng, nút 3 đủ nộ đỏ rực
                Color ring = i == target ? targetColor : i == _slot ? Color.white : i == 2 && !locked ? rageColor : readyColor;
                if (locked || remaining > 0f)
                    ring = Color.Lerp(ring, new Color(0.35f, 0.35f, 0.45f), 0.6f);
                b.ring.color = ring;
                float glow = i == target || i == _slot ? 0.8f : i == 2 && !locked ? 0.45f + 0.3f * Mathf.Sin(time * 5f) : 0f;
                b.halo.color = new Color(ring.r, ring.g, ring.b, glow);

                _pop[i] = Mathf.MoveTowards(_pop[i], 0f, dt * 4f);
                float scale = 1f + (i == target ? 0.12f : 0f) + (i == _slot ? 0.06f : 0f) + 0.15f * Mathf.Max(0f, _pop[i]);
                float shake = _pop[i] < 0f ? 8f * _pop[i] * Mathf.Sin(time * 70f) : 0f;
                b.icon.rectTransform.parent.localScale = Vector3.one * scale;
                b.icon.rectTransform.parent.localPosition = new Vector3(shake, 0f, 0f);
            }

            for (int i = 0; i < tierPips.Length; i++)
                tierPips[i].color = i < tier ? rageColor : new Color(0.25f, 0.2f, 0.3f, 0.9f);

            if (skillName != null)
            {
                if (_slot >= 0)
                    ShowName(); // cập nhật số giây hồi chiêu khi đang giữ
                _nameTimer = _slot >= 0 ? 1.2f : Mathf.Max(0f, _nameTimer - dt);
                Color c = skillName.color;
                c.a = Mathf.Clamp01(_nameTimer * 3f);
                skillName.color = c;
            }
        }

        static int TargetOf(int slot, int variant)
        {
            int order = 0;
            for (int other = 0; other < 3; other++)
            {
                if (other == slot)
                    continue;
                if (++order == variant)
                    return other;
            }

            return -1;
        }
    }
}
