using System.Collections.Generic;
using UnityEngine;

namespace Octopus
{
    /// <summary>Vẽ vòng tròn bán kính xúc tu quanh bạch tuộc và làm sáng các tảng đá nằm trong tầm.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ReachIndicator : MonoBehaviour
    {
        [SerializeField] SwingController swing;
        [SerializeField, Range(16, 128)] int segments = 64;

        LineRenderer _line;
        readonly List<Collider2D> _hits = new List<Collider2D>();
        readonly HashSet<Rock> _inReach = new HashSet<Rock>();
        readonly HashSet<Rock> _stillInReach = new HashSet<Rock>();

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = segments;
        }

        void LateUpdate()
        {
            Vector2 center = swing.transform.position;
            float radius = swing.Reach;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                _line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            HighlightRocks(center, radius);
        }

        void HighlightRocks(Vector2 center, float radius)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(swing.AnchorMask);
            Physics2D.OverlapCircle(center, radius, filter, _hits);

            _stillInReach.Clear();
            foreach (Collider2D hit in _hits)
            {
                if (hit.TryGetComponent(out Rock rock))
                    _stillInReach.Add(rock);
            }

            foreach (Rock rock in _inReach)
            {
                if (rock != null && !_stillInReach.Contains(rock))
                    rock.SetInReach(false);
            }

            foreach (Rock rock in _stillInReach)
                rock.SetInReach(true);

            _inReach.Clear();
            _inReach.UnionWith(_stillInReach);
        }
    }
}
