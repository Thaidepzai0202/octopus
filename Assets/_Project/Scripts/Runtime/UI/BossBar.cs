using UnityEngine;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Thanh máu boss giữa phía trên màn hình: tên "SƯ TỬ HẮC VIÊM", phần máu, thanh "máu vừa mất" sáng màu tụt chậm phía sau,
    /// vạch mốc 60% và 30% (sang pha), rung + loé khi trúng đòn, đổi màu khi nổi giận, dòng trạng thái (GỤC NGÃ / CHOÁNG).
    /// Hiện dần khi boss gầm mở màn. Màn hẹp (thanh nộ bên trái và nút Đổi nhân vật bên phải chừa không đủ chỗ) thì tự xuống
    /// dòng dưới thanh máu người chơi.
    /// </summary>
    public class BossBar : MonoBehaviour
    {
        [SerializeField] LionBoss boss;
        [Tooltip("Phần máu (Image Filled ngang)")]
        [SerializeField] Image fill;
        [Tooltip("Thanh máu vừa mất (Image Filled ngang, sau phần máu)")]
        [SerializeField] Image trail;
        [SerializeField] Image glow;
        [SerializeField] Text nameLabel;
        [SerializeField] Text statusLabel;
        [Tooltip("Vạch mốc pha 2, pha 3")]
        [SerializeField] Image[] marks = new Image[2];
        [SerializeField] CanvasGroup group;

        [Header("Màu")]
        [SerializeField] Color fillColor = new Color(0.95f, 0.22f, 0.12f);
        [SerializeField] Color enragedColor = new Color(1f, 0.05f, 0.2f);
        [SerializeField] Color trailColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] Color markColor = new Color(1f, 0.85f, 0.55f, 0.95f);

        [Header("Thanh máu vừa mất")]
        [Tooltip("Đứng yên bấy nhiêu giây sau đòn cuối rồi mới tụt")]
        [SerializeField] float trailDelay = 0.6f;
        [Tooltip("Tụt bấy nhiêu phần thanh mỗi giây")]
        [SerializeField] float trailSpeed = 0.35f;

        [Header("Bố cục")]
        [Tooltip("Rộng nhất (px, màn cao 1080)")]
        [SerializeField] float maxWidth = 900f;
        [Tooltip("Hẹp nhất còn đứng hàng trên cùng; hẹp hơn thì xuống dòng")]
        [SerializeField] float minWidth = 560f;
        [Tooltip("Chừa hai bên cho thanh nộ / nút Đổi nhân vật (px)")]
        [SerializeField] float sideReserve = 680f;
        [SerializeField] float topY = -34f;
        [Tooltip("Xuống dòng: cách mép trên bấy nhiêu (dưới thanh nộ + thanh máu người chơi)")]
        [SerializeField] float lowerY = -205f;

        float _shown = 1f, _trail = 1f, _sinceHit, _shake, _flash, _visible;
        float _lastHealth = -1f;
        Vector2 _home;
        float _layoutWidth = -1f;

        public float Shown => _shown;
        public float Trail => _trail;
        public bool Visible => _visible > 0.5f;

        void Start()
        {
            if (boss == null)
                boss = FindAnyObjectByType<LionBoss>();
            if (group != null)
                group.alpha = 0f;
            Layout();
        }

        // Đứng hàng trên giữa thanh nộ và nút Đổi nhân vật nếu đủ chỗ, không thì xuống dòng
        void Layout()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            float width = parent != null ? parent.rect.width : 1920f;
            if (Mathf.Approximately(width, _layoutWidth))
                return;
            _layoutWidth = width;
            float room = width - 2f * sideReserve;
            bool top = room >= minWidth;
            float barWidth = top ? Mathf.Min(maxWidth, room) : Mathf.Min(maxWidth, width - 80f);
            rect.sizeDelta = new Vector2(barWidth, rect.sizeDelta.y);
            _home = new Vector2(0f, top ? topY : lowerY);
            rect.anchoredPosition = _home;
        }

        void Update()
        {
            Layout();
            if (boss == null || boss.Health == null)
                return;

            float dt = Time.unscaledDeltaTime;
            Health h = boss.Health;
            float target = h.Current / h.Max;

            // Hiện ra khi boss gầm mở màn
            float visible = boss.Engaged ? 1f : 0f;
            _visible = Mathf.MoveTowards(_visible, visible, dt * 2.5f);
            if (group != null)
                group.alpha = _visible;

            if (_lastHealth >= 0f && h.Current < _lastHealth - 0.01f)
            {
                float lost = (_lastHealth - h.Current) / h.Max;
                _sinceHit = 0f;
                _shake = Mathf.Min(1f, _shake + 0.3f + lost * 20f);
                _flash = 1f;
            }

            _lastHealth = h.Current;
            _sinceHit += dt;
            _shown = Mathf.MoveTowards(_shown, target, dt * 3f);
            if (target > _shown)
                _shown = target;
            if (_sinceHit > trailDelay)
                _trail = Mathf.MoveTowards(_trail, _shown, dt * trailSpeed);
            _trail = Mathf.Max(_trail, _shown);

            fill.fillAmount = _shown;
            trail.fillAmount = _trail;
            Color main = Color.Lerp(fillColor, enragedColor, boss.Phase >= 3 ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f);
            fill.color = Color.Lerp(main, Color.white, 0.6f * _flash);
            trail.color = trailColor;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 6f);
            if (glow != null)
                glow.color = new Color(main.r, main.g, main.b, 0.25f + 0.35f * _flash + (boss.Phase >= 3 ? 0.2f : 0f));

            // Vạch mốc pha: pha đã qua thì tối đi
            for (int i = 0; i < marks.Length; i++)
            {
                if (marks[i] != null)
                    marks[i].color = boss.Phase > i + 1 ? new Color(0.35f, 0.2f, 0.2f, 0.8f) : markColor;
            }

            _shake = Mathf.MoveTowards(_shake, 0f, dt * 4f);
            ((RectTransform)transform).anchoredPosition = _home + Random.insideUnitCircle * (8f * _shake);

            if (nameLabel != null)
                nameLabel.text = boss.Phase >= 3 ? LionBoss.DisplayName + "  ·  NỔI GIẬN" : LionBoss.DisplayName;
            if (statusLabel != null)
            {
                string status = boss.IsDead ? "ĐÃ GỤC" : boss.IsStaggered ? "GỤC NGÃ! ĐÁNH MẠNH +25%" : boss.IsStunned ? "CHOÁNG" : "";
                statusLabel.text = status;
                statusLabel.color = new Color(1f, 0.9f, 0.55f, string.IsNullOrEmpty(status) ? 0f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f));
            }
        }
    }
}
