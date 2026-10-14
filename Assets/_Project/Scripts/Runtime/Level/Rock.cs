using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Tảng đá bám được. Viền dưới phát sáng mờ khi ở xa, sáng rực và nhấp nháy khi nằm trong bán kính xúc tu
    /// để người chơi biết chỗ nào chạm được.
    /// </summary>
    public class Rock : MonoBehaviour
    {
        [SerializeField] LineRenderer glowRim;
        [SerializeField] Color idleColor = new Color(0.35f, 0.85f, 1f, 0.55f);
        [SerializeField] Color inReachColor = new Color(0.55f, 0.98f, 1f, 1f);
        [SerializeField] float idleWidth = 0.26f;
        [SerializeField] float inReachWidth = 0.45f;
        [Tooltip("Tốc độ sáng lên / tối đi")]
        [SerializeField] float fadeSpeed = 6f;
        [SerializeField] float pulseSpeed = 5f;

        float _target;
        float _value = -1f;

        public void SetInReach(bool inReach)
        {
            _target = inReach ? 1f : 0f;
        }

        void Update()
        {
            if (glowRim == null)
                return;

            if (Mathf.Approximately(_value, _target) && _target == 0f)
                return;

            _value = Mathf.MoveTowards(Mathf.Max(_value, 0f), _target, fadeSpeed * Time.deltaTime);
            float pulse = _value * (0.82f + 0.18f * Mathf.Sin(Time.time * pulseSpeed));

            Color color = Color.Lerp(idleColor, inReachColor, pulse);
            glowRim.startColor = glowRim.endColor = color;
            glowRim.widthMultiplier = Mathf.Lerp(idleWidth, inReachWidth, pulse);
        }
    }
}
