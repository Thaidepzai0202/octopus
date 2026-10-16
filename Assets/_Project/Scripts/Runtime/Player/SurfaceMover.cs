using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Di chuyển trên bề mặt: bò trên sàn, nhảy, bám dưới đá và bò dọc theo đá.
    /// Không tự đọc input; OctopusController gọi các hàm này theo trạng thái.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class SurfaceMover : MonoBehaviour
    {
        [Header("Trên sàn")]
        [SerializeField] float crawlSpeed = 4f;
        [Tooltip("Tăng / giảm tốc khi bò (unit/giây²)")]
        [SerializeField] float crawlAcceleration = 30f;
        [SerializeField] float jumpSpeed = 13f;

        [Header("Bám đá")]
        [SerializeField] float clingCrawlSpeed = 3f;
        [Tooltip("Vận tốc rời khỏi đá khi chạm để thả xuống")]
        [SerializeField] float dropSpeed = 2f;

        Rigidbody2D _body;
        float _radius;
        Collider2D _clingSurface;

        public bool IsClinging => _clingSurface != null;
        public Collider2D ClingSurface => _clingSurface;
        public Vector2 ClingPoint { get; private set; }
        public Vector2 ClingNormal { get; private set; } = Vector2.down;
        /// <summary>Vận tốc bò dọc mặt đá trong bước vật lý vừa rồi (thân kinematic nên Rigidbody không tự có).</summary>
        public Vector2 ClingVelocity { get; private set; }

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            var circle = GetComponent<CircleCollider2D>();
            _radius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        }

        public bool CheckGrounded()
        {
            var filter = new ContactFilter2D();
            filter.SetNormalAngle(45f, 135f); // chỉ tính mặt tiếp xúc hướng lên
            return _body.IsTouching(filter);
        }

        public void CrawlOnGround(float input)
        {
            Vector2 velocity = _body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, input * crawlSpeed, crawlAcceleration * Time.fixedDeltaTime);
            _body.linearVelocity = velocity;
        }

        public void Jump()
        {
            _body.linearVelocity = new Vector2(_body.linearVelocity.x, jumpSpeed);
        }

        public void StartCling(Collider2D surface, Vector2 point, Vector2 normal)
        {
            _clingSurface = surface;
            ClingPoint = point;
            ClingNormal = normal.normalized;

            // Kinematic: không rơi, chỉ đi theo mặt đá
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.linearVelocity = Vector2.zero;
            _body.position = point + ClingNormal * _radius;
            ClingVelocity = Vector2.zero;
        }

        // Bò dọc mặt đá; ClosestPoint giữ điểm bám luôn nằm trên đá nên tới mép là dừng
        public void CrawlAlongCling(float input)
        {
            ClingVelocity = Vector2.zero;
            if (!IsClinging || input == 0f)
                return;

            Vector2 previous = ClingPoint;
            Vector2 next = ClingPoint + SurfaceTangent(ClingNormal) * (input * clingCrawlSpeed * Time.fixedDeltaTime);
            ClingPoint = _clingSurface.ClosestPoint(next + ClingNormal * 0.05f);
            ClingVelocity = (ClingPoint - previous) / Time.fixedDeltaTime;
            _body.MovePosition(ClingPoint + ClingNormal * _radius);
        }

        public void StopCling(bool drop = false)
        {
            if (!IsClinging)
                return;

            _clingSurface = null;
            ClingVelocity = Vector2.zero;
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.linearVelocity = drop ? ClingNormal * dropSpeed : Vector2.zero;
        }

        /// <summary>Hướng "sang phải" dọc theo bề mặt; với mặt đứng thì "sang phải" là đi lên.</summary>
        public static Vector2 SurfaceTangent(Vector2 normal)
        {
            var tangent = new Vector2(-normal.y, normal.x);
            bool flip = Mathf.Abs(tangent.x) > 0.1f ? tangent.x < 0f : tangent.y < 0f;
            return flip ? -tangent : tangent;
        }
    }
}
