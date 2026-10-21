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
    ///
    /// Nhân vật người (RED, PINK, BLUE): không vuốt; bấm nút là ra chiêu ngay, kiểu chiêu theo cần điều hướng
    /// (thả / ngang = thường, kéo lên = ↑, kéo xuống = ↓). Đang kéo lên / xuống thì cả 3 nút hiện kiểu đang chọn: huy hiệu ▲ / ▼
    /// và tên chiêu dưới nút. Cụm nút dịch vào trong một chút (Hero Offset) để chừa góc cho nút NHẢY.
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
            [Tooltip("Nhân vật người: huy hiệu ▲ / ▼ khi đang kéo cần lên / xuống")]
            public Image badge;
            [Tooltip("Mũi tên trong huy hiệu (hình tam giác chỉ lên; ▼ thì xoay ngược)")]
            public Image badgeArrow;
            [Tooltip("Nhân vật người: tên chiêu kiểu đang chọn, dưới nút")]
            public Text variantName;
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
        [SerializeField] Sprite[] redIcons = new Sprite[3];
        [SerializeField] Sprite[] pinkIcons = new Sprite[3];
        [SerializeField] Sprite[] blueIcons = new Sprite[3];
        [Tooltip("Nhân vật người: dịch cả cụm nút đi bấy nhiêu (đơn vị canvas) để chừa góc cho nút NHẢY")]
        [SerializeField] Vector2 heroOffset = new Vector2(-185f, 90f);
        [Tooltip("Màu huy hiệu ▲ (chiêu ↑) và ▼ (chiêu ↓)")]
        [SerializeField] Color upColor = new Color(1f, 0.82f, 0.3f);
        [SerializeField] Color downColor = new Color(0.45f, 0.85f, 1f);
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
        Vector2 _home;
        bool _homeSet;

        public CombatController Combat => combat;

        bool Hero => combat != null && combat.IsHero;

        /// <summary>Nhân vật người: kiểu chiêu đang chọn bằng cần điều hướng (0 = thường, 1 = ↑, 2 = ↓).</summary>
        public int HeroVariant => input != null && input.isActiveAndEnabled ? HeroMotor.VariantOf(input.Aim) : combat != null ? combat.HeroVariant : 0;

        /// <summary>Icon nút slot của một nhân vật.</summary>
        public Sprite IconOf(PlayableCharacter character, int slot)
        {
            Sprite[] icons = character switch
            {
                PlayableCharacter.Spider => spiderIcons,
                PlayableCharacter.Red => redIcons,
                PlayableCharacter.Pink => pinkIcons,
                PlayableCharacter.Blue => blueIcons,
                _ => octopusIcons,
            };
            return icons != null && slot < icons.Length ? icons[slot] : null;
        }

        /// <summary>Icon đang hiện trên nút slot.</summary>
        public Sprite ShownIcon(int slot) => buttons[slot].icon.sprite;

        /// <summary>Huy hiệu ▲ / ▼ của nút slot đang hiện không.</summary>
        public bool BadgeShown(int slot) => buttons[slot].badge != null && buttons[slot].badge.gameObject.activeSelf;

        /// <summary>Tên chiêu đang hiện dưới nút slot (nhân vật người kéo cần lên / xuống), rỗng nếu không hiện.</summary>
        public string ShownVariantName(int slot) =>
            buttons[slot].variantName != null && buttons[slot].variantName.gameObject.activeSelf ? buttons[slot].variantName.text : "";

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
            if (Hero)
            {
                // Nhân vật người: ra chiêu ngay lúc bấm, kiểu theo cần điều hướng
                int variant = HeroVariant;
                Fire(slot, variant);
                ShowName(slot, variant);
                return;
            }

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
            if (Hero || slot != _slot || data.pointerId != _pointer)
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
            if (Hero || slot != _slot || data.pointerId != _pointer)
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

        void ShowName() => ShowName(_slot, _variant);

        void ShowName(int slot, int variant)
        {
            _nameTimer = 1.2f;
            Skill skill = combat != null && slot >= 0 ? combat.Get(slot, variant) : null;
            if (skillName == null || skill == null)
                return;
            float remaining = combat.CooldownRemaining(slot, variant);
            skillName.text = remaining > 0f ? $"{skill.name}  ({Mathf.CeilToInt(remaining)}s)" : skill.name;
        }

        // ---------- Hình ----------

        void Update()
        {
            if (combat == null)
                return;

            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            bool hero = Hero;
            int heroVariant = hero ? HeroVariant : 0;
            int tier = combat.Rage.Tier;
            int target = !hero && _slot >= 0 && _variant > 0 ? TargetOf(_slot, _variant) : -1;
            PlaceForCharacter(hero);

            for (int i = 0; i < buttons.Length; i++)
            {
                PadButton b = buttons[i];
                Sprite icon = IconOf(combat.Character, i);
                if (icon != null)
                    b.icon.sprite = icon;

                // kiểu chiêu nút này đang hiện hồi chiêu: nhân vật người theo cần điều hướng, bạch tuộc / nhện theo nút đang vuốt
                int variant = hero ? heroVariant : i == _slot ? _variant : 0;
                ShowVariant(b, i, hero ? heroVariant : 0);
                bool locked = i == 2 && tier < 1;
                float remaining = combat.CooldownRemaining(i, variant);
                float shade = locked ? 1f - Mathf.Clamp01(combat.Rage.Value / RageMeter.PerTier) : combat.CooldownFraction(i, variant);
                b.shade.fillAmount = shade;
                b.shade.enabled = shade > 0.001f;
                b.icon.color = locked || remaining > 0f ? new Color(0.45f, 0.45f, 0.55f) : Color.white;

                b.label.text = remaining > 0.5f ? Mathf.CeilToInt(remaining).ToString() : locked ? "NỘ" : "";

                // Viền: nút đang giữ sáng trắng, nút đích khi vuốt màu vàng, nút 3 đủ nộ đỏ rực; nhân vật người đang chọn chiêu ↑ / ↓
                // thì viền theo màu huy hiệu
                Color ring = i == target ? targetColor : i == _slot ? Color.white : i == 2 && !locked ? rageColor : readyColor;
                if (heroVariant != 0)
                    ring = Color.Lerp(ring, heroVariant == 1 ? upColor : downColor, 0.7f);
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

        // Nhân vật người đang kéo cần lên / xuống: huy hiệu ▲ / ▼ ở góc nút và tên chiêu kiểu đó dưới nút
        void ShowVariant(PadButton b, int slot, int variant)
        {
            bool show = variant != 0;
            if (b.badge != null)
            {
                if (b.badge.gameObject.activeSelf != show)
                    b.badge.gameObject.SetActive(show);
                if (show)
                {
                    Color c = variant == 1 ? upColor : downColor;
                    b.badge.color = c;
                    if (b.badgeArrow != null)
                        b.badgeArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, variant == 1 ? 0f : 180f);
                }
            }

            if (b.variantName != null)
            {
                Skill skill = show ? combat.Get(slot, variant) : null;
                bool named = skill != null;
                if (b.variantName.gameObject.activeSelf != named)
                    b.variantName.gameObject.SetActive(named);
                if (named)
                {
                    b.variantName.text = skill.name;
                    b.variantName.color = variant == 1 ? upColor : downColor;
                }
            }
        }

        // Nhân vật người: cả cụm dịch vào trong để chừa góc cho nút NHẢY
        void PlaceForCharacter(bool hero)
        {
            var rect = (RectTransform)transform;
            if (!_homeSet)
            {
                _home = rect.anchoredPosition;
                _homeSet = true;
            }

            Vector2 want = hero ? _home + heroOffset : _home;
            if (rect.anchoredPosition != want)
                rect.anchoredPosition = want;
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
