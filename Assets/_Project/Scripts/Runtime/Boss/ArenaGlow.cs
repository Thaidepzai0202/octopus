using UnityEngine;

namespace Octopus
{
    /// <summary>Khe dung nham / quầng sáng của đấu trường: độ sáng phập phồng chậm như than hồng thở.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ArenaGlow : MonoBehaviour
    {
        [Tooltip("Độ đậm (alpha) thấp nhất / cao nhất")]
        [SerializeField] Vector2 alpha = new Vector2(0.55f, 1f);
        [Tooltip("Số nhịp sáng mỗi giây")]
        [SerializeField, Min(0f)] float speed = 0.6f;
        [Tooltip("Lệch nhịp (để các khe không sáng cùng lúc)")]
        [SerializeField] float phase;

        SpriteRenderer _renderer;

        void Awake() => _renderer = GetComponent<SpriteRenderer>();

        void Update()
        {
            float t = Time.time * speed * Mathf.PI * 2f + phase;
            float k = 0.5f + 0.35f * Mathf.Sin(t) + 0.15f * Mathf.Sin(t * 2.7f + 1.3f);
            Color c = _renderer.color;
            c.a = Mathf.Lerp(alpha.x, alpha.y, k);
            _renderer.color = c;
        }
    }
}
