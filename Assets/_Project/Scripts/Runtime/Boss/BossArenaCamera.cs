using Unity.Cinemachine;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Camera màn đánh boss: Follow Camera bám theo object này thay vì bám thẳng người chơi. Điểm bám nằm giữa người chơi và
    /// boss (lệch về phía người chơi), độ cao gần như cố định để luôn thấy sàn lẫn 3 tầng đá; khung hình lùi xa hơn màn thường
    /// và lùi thêm khi người chơi ở xa boss, để luôn thấy cả hai. Không đi quá vách hai đầu. Rung camera (CameraShake) vẫn
    /// chạy vì là extension của Follow Camera.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class BossArenaCamera : MonoBehaviour
    {
        [SerializeField] Rigidbody2D player;
        [SerializeField] LionBoss boss;
        [SerializeField] CinemachineCamera followCamera;

        [Header("Khung hình")]
        [Tooltip("Cỡ khung (nửa chiều cao, unit) bình thường. Màn hang động thường là 6")]
        [SerializeField, Min(1f)] float baseSize = 10f;
        [Tooltip("Lùi xa tối đa tới cỡ này khi người chơi ở xa boss")]
        [SerializeField, Min(1f)] float maxSize = 12.5f;
        [Tooltip("Chừa thêm bấy nhiêu unit quanh người chơi và boss")]
        [SerializeField] float margin = 3f;
        [Tooltip("Đổi cỡ khung mượt (1/giây)")]
        [SerializeField] float zoomSmoothing = 2f;

        [Header("Điểm bám")]
        [Tooltip("Lệch về phía boss bao nhiêu (0 = bám người chơi, 1 = bám boss)")]
        [SerializeField, Range(0f, 1f)] float bossWeight = 0.35f;
        [Tooltip("Độ cao giữa khung hình khi người chơi ở dưới sàn")]
        [SerializeField] float centerY = 8f;
        [Tooltip("Theo độ cao người chơi bao nhiêu (0 = cố định)")]
        [SerializeField, Range(0f, 1f)] float followY = 0.25f;
        [Tooltip("Mép trong 2 vách (x): camera không lộ quá vách bấy nhiêu")]
        [SerializeField] Vector2 arenaX = new Vector2(-28f, 28f);
        [SerializeField] float wallOverscan = 3f;

        float _size;

        public float Size => _size;

        void Start()
        {
            _size = baseSize;
            Apply(1f);
        }

        void Update() => Apply(Time.deltaTime);

        void Apply(float dt)
        {
            if (player == null)
                return;
            Camera main = Camera.main;
            float aspect = main != null ? Mathf.Max(0.5f, main.aspect) : 16f / 9f;

            Vector2 p = player.position;
            float focus = p.x;
            float want = baseSize;
            float lo = p.x - margin, hi = p.x + margin;
            if (boss != null && !boss.IsDead)
            {
                Bounds b = boss.Body;
                lo = Mathf.Min(lo, b.min.x - margin);
                hi = Mathf.Max(hi, b.max.x + margin);
                focus = Mathf.Lerp(p.x, b.center.x, bossWeight);
                want = Mathf.Clamp((hi - lo) * 0.5f / aspect, baseSize, maxSize);
            }

            _size = dt >= 1f ? want : Mathf.Lerp(_size, want, 1f - Mathf.Exp(-zoomSmoothing * dt));
            float half = _size * aspect;
            // Người chơi luôn trong khung; vừa khung thì thấy trọn cả người chơi lẫn boss, không vừa thì lùi về phía boss hết cỡ
            focus = Mathf.Clamp(focus, p.x - (half - margin), p.x + (half - margin));
            if (hi - lo <= 2f * half)
                focus = Mathf.Clamp(focus, hi - half, lo + half);
            float y = Mathf.Max(centerY, centerY + (p.y - centerY * 0.5f) * followY);
            float minX = arenaX.x - wallOverscan + half;
            float maxX = arenaX.y + wallOverscan - half;
            focus = minX <= maxX ? Mathf.Clamp(focus, minX, maxX) : (arenaX.x + arenaX.y) * 0.5f;
            transform.position = new Vector3(focus, y, 0f);

            if (followCamera != null)
            {
                LensSettings lens = followCamera.Lens;
                lens.OrthographicSize = _size;
                followCamera.Lens = lens;
            }
        }
    }
}
