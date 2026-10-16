using UnityEngine;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Thanh nộ góc trên bên trái: đầy dần khi di chuyển / đánh trúng, chia 3 mốc. Qua mốc nào thì vạch mốc đó sáng, thanh
    /// đổi màu (cam → đỏ → tím rực) và rung nhẹ báo hiệu; đủ mốc 1 thì nhấp nháy mời dùng chiêu cuối (nút 3).
    /// </summary>
    public class RageBar : MonoBehaviour
    {
        [SerializeField] RageMeter rage;
        [Tooltip("Phần đầy (Image Filled ngang)")]
        [SerializeField] Image fill;
        [SerializeField] Image glow;
        [Tooltip("3 vạch mốc")]
        [SerializeField] Image[] marks = new Image[3];
        [SerializeField] Text label;
        [SerializeField] Color[] tierColors =
        {
            new Color(1f, 0.62f, 0.25f), new Color(1f, 0.45f, 0.25f), new Color(1f, 0.25f, 0.35f), new Color(0.9f, 0.45f, 1f),
        };

        float _shown;
        float _bump;
        int _tier;

        void Update()
        {
            if (rage == null)
                return;

            float dt = Time.unscaledDeltaTime;
            float target = rage.Value / rage.Max;
            _shown = Mathf.MoveTowards(_shown, target, dt * 1.5f);
            if (target < _shown)
                _shown = target; // xả nộ: tụt ngay
            fill.fillAmount = _shown;

            int tier = rage.Tier;
            if (tier > _tier)
                _bump = 1f;
            _tier = tier;
            _bump = Mathf.MoveTowards(_bump, 0f, dt * 2.5f);

            float pulse = tier >= 1 ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            Color color = tierColors[Mathf.Clamp(tier, 0, tierColors.Length - 1)];
            fill.color = Color.Lerp(color, Color.white, 0.25f * pulse + 0.5f * _bump);
            glow.color = new Color(color.r, color.g, color.b, 0.2f * tier + 0.3f * pulse + 0.6f * _bump);
            for (int i = 0; i < marks.Length; i++)
                marks[i].color = i < tier ? Color.Lerp(color, Color.white, 0.5f) : new Color(0.3f, 0.25f, 0.35f, 0.9f);

            transform.localScale = Vector3.one * (1f + 0.08f * _bump);
            label.text = tier >= 1 ? $"NỘ  {new string('I', tier)}" : "NỘ";
        }
    }
}
