using UnityEngine;

namespace Octopus
{
    /// <summary>Bụi phát sáng lơ lửng quanh camera: trôi lên chậm, đung đưa, lấp lánh. Hạt ra khỏi khung thì quấn sang phía đối diện.</summary>
    public class AmbientMotes : MonoBehaviour
    {
        [SerializeField] Sprite sprite;
        [SerializeField, Range(0, 200)] int count = 50;
        [Tooltip("Vùng quanh camera có bụi (unit)")]
        [SerializeField] Vector2 area = new Vector2(48f, 26f);
        [SerializeField] Vector2 sizeRange = new Vector2(0.08f, 0.24f);
        [SerializeField] Color color = new Color(0.55f, 0.95f, 1f, 0.75f);
        [SerializeField] float riseSpeed = 0.3f;
        [SerializeField] float swayAmount = 0.5f;
        [Tooltip("Một phần ba số hạt bay phía trước bạch tuộc, còn lại phía sau")]
        [SerializeField] int frontSortingOrder = 60;
        [SerializeField] int backSortingOrder = -300;

        struct Mote
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float Phase;
            public float Speed;
        }

        Mote[] _motes;
        Camera _camera;

        void Start()
        {
            _camera = Camera.main;
            _motes = new Mote[count];
            Vector2 center = _camera.transform.position;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(transform, false);
                go.transform.position = center + new Vector2((Random.value - 0.5f) * area.x, (Random.value - 0.5f) * area.y);
                go.transform.localScale = Vector3.one * Random.Range(sizeRange.x, sizeRange.y);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = i % 3 == 0 ? frontSortingOrder : backSortingOrder;

                _motes[i] = new Mote
                {
                    Transform = go.transform,
                    Renderer = renderer,
                    Phase = Random.value * 10f,
                    Speed = Random.Range(0.5f, 1.5f),
                };
            }
        }

        void LateUpdate()
        {
            if (_motes == null)
                return;

            Vector2 center = _camera.transform.position;
            Vector2 half = area * 0.5f;
            float time = Time.time;
            float dt = Time.deltaTime;

            for (int i = 0; i < _motes.Length; i++)
            {
                ref Mote mote = ref _motes[i];
                Vector2 position = mote.Transform.position;
                position.y += riseSpeed * mote.Speed * dt;
                position.x += Mathf.Sin(time * 0.7f * mote.Speed + mote.Phase) * swayAmount * dt;

                Vector2 offset = position - center;
                if (offset.x > half.x) position.x -= area.x;
                else if (offset.x < -half.x) position.x += area.x;
                if (offset.y > half.y) position.y -= area.y;
                else if (offset.y < -half.y) position.y += area.y;
                mote.Transform.position = position;

                Color tint = color;
                tint.a *= 0.5f + 0.5f * Mathf.Sin(time * 2f * mote.Speed + mote.Phase * 3f);
                mote.Renderer.color = tint;
            }
        }
    }
}
