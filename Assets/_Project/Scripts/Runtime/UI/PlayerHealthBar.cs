using UnityEngine;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Thanh máu người chơi góc trên bên trái, ngay dưới thanh nộ: phần máu (xanh → vàng → đỏ khi máu thấp), thanh "máu vừa
    /// mất" tụt chậm phía sau, số máu, rung + loé đỏ khi bị đánh, máu thấp thì nhấp nháy.
    /// </summary>
    public class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] PlayerHealth player;
        [Tooltip("Phần máu (Image Filled ngang)")]
        [SerializeField] Image fill;
        [Tooltip("Thanh máu vừa mất (Image Filled ngang, sau phần máu)")]
        [SerializeField] Image trail;
        [SerializeField] Image glow;
        [SerializeField] Text label;

        [SerializeField] Color fullColor = new Color(0.35f, 0.95f, 0.55f);
        [SerializeField] Color midColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] Color lowColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] Color trailColor = new Color(1f, 0.95f, 0.85f);
        [Tooltip("Dưới mức này (phần máu) thì nhấp nháy báo nguy")]
        [SerializeField, Range(0f, 1f)] float lowAt = 0.3f;

        float _shown = 1f, _trail = 1f, _sinceHit, _shake, _flash;
        float _last = -1f;
        Vector2 _home;

        public float Shown => _shown;

        void Start()
        {
            if (player == null)
                player = FindAnyObjectByType<PlayerHealth>();
            _home = ((RectTransform)transform).anchoredPosition;
        }

        void Update()
        {
            if (player == null)
                return;

            float dt = Time.unscaledDeltaTime;
            float target = player.Current / player.Max;
            if (_last >= 0f && player.Current < _last - 0.01f)
            {
                _sinceHit = 0f;
                _shake = Mathf.Min(1f, _shake + 0.4f + (_last - player.Current) / player.Max * 6f);
                _flash = 1f;
            }

            _last = player.Current;
            _sinceHit += dt;
            _shown = target > _shown ? target : Mathf.MoveTowards(_shown, target, dt * 3f);
            if (_sinceHit > 0.5f)
                _trail = Mathf.MoveTowards(_trail, _shown, dt * 0.6f);
            _trail = Mathf.Max(_trail, _shown);

            fill.fillAmount = _shown;
            trail.fillAmount = _trail;
            Color color = target > 0.5f ? Color.Lerp(midColor, fullColor, (target - 0.5f) * 2f) : Color.Lerp(lowColor, midColor, target * 2f);
            float pulse = target < lowAt && target > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
            fill.color = Color.Lerp(color, Color.white, 0.55f * _flash + 0.25f * pulse);
            trail.color = trailColor;
            if (glow != null)
                glow.color = new Color(lowColor.r, lowColor.g, lowColor.b, 0.55f * _flash + 0.35f * pulse);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 5f);

            _shake = Mathf.MoveTowards(_shake, 0f, dt * 4f);
            ((RectTransform)transform).anchoredPosition = _home + Random.insideUnitCircle * (7f * _shake);
            if (label != null)
                label.text = $"{Mathf.CeilToInt(player.Current)}";
        }
    }
}
