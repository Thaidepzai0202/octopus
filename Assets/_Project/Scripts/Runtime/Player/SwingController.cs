using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Xúc tu đu dây: bắn vào đá trong bán kính xúc tu, đu như con lắc, rút xúc tu để leo lên đá.
    /// Dây là DistanceJoint2D bật maxDistanceOnly: căng thì giữ, chùng thì rơi tự do.
    /// Không tự đọc input; OctopusController quyết định khi nào bắn / buông / rút.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(DistanceJoint2D))]
    public class SwingController : MonoBehaviour
    {
        [Header("Xúc tu")]
        [SerializeField] LayerMask anchorMask;
        [Tooltip("Bán kính xúc tu: chỉ bám được đá nằm trong khoảng này (unit)")]
        [SerializeField, Min(1f)] float reach = 7f;
        [Tooltip("Rút xúc tu tới khoảng cách này thì coi như đã tới đá → chuyển sang bám đá")]
        [SerializeField, Min(0.5f)] float clingDistance = 0.9f;
        [Tooltip("Tốc độ rút xúc tu khi giữ tay (unit/giây)")]
        [SerializeField, Min(0f)] float reelSpeed = 5f;

        [Header("Đu")]
        [Tooltip("Lực đẩy theo phương tiếp tuyến khi vuốt trái/phải")]
        [SerializeField] float pumpForce = 15f;
        [Tooltip("Không vuốt thì tự đẩy theo hướng đang đu với lực này. 0 = tắt")]
        [SerializeField, Min(0f)] float autoPumpForce = 3f;
        [Tooltip("Bắn xúc tu khi đang đứng trên sàn thì bị giật về phía đá với vận tốc này")]
        [SerializeField, Min(0f)] float groundLaunchSpeed = 12f;
        [Tooltip("Nhân vận tốc lúc buông xúc tu cho cảm giác văng")]
        [SerializeField, Range(1f, 2f)] float releaseBoost = 1.15f;
        [SerializeField] float maxSpeed = 30f;

        Rigidbody2D _body;
        DistanceJoint2D _rope;

        public LayerMask AnchorMask => anchorMask;
        public float Reach => reach;
        public bool IsAttached => _rope.enabled;
        public Vector2 AnchorPoint => _rope.connectedAnchor;
        public Vector2 AnchorNormal { get; private set; }
        public Collider2D AnchorCollider { get; private set; }
        public float RopeLength => _rope.distance;

        /// <summary>-1..1: bơm đu sang trái / phải. Do OctopusController gán mỗi frame.</summary>
        public float PumpInput { get; set; }

        /// <summary>Đang giữ tay → rút xúc tu. Do OctopusController gán mỗi frame.</summary>
        public bool Reeling { get; set; }

        public event Action<Vector2> Attached;
        public event Action Released;

        /// <summary>Đã rút xúc tu tới sát đá.</summary>
        public event Action ReachedAnchor;

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _rope = GetComponent<DistanceJoint2D>();

            // connectedBody = null → connectedAnchor là toạ độ world của điểm bám
            _rope.connectedBody = null;
            _rope.autoConfigureConnectedAnchor = false;
            _rope.autoConfigureDistance = false;
            _rope.maxDistanceOnly = true;
            _rope.enableCollision = true;
            _rope.enabled = false;
        }

        void FixedUpdate()
        {
            if (IsAttached)
            {
                Pump();
                if (Reeling)
                    ReelIn();
            }

            _body.linearVelocity = Vector2.ClampMagnitude(_body.linearVelocity, maxSpeed);
        }

        /// <summary>Bắn tia từ bạch tuộc về phía điểm cho trước, trúng đá trong bán kính xúc tu thì trả về true.</summary>
        public bool FindAnchor(Vector2 worldTarget, out RaycastHit2D hit)
        {
            hit = default;
            Vector2 direction = worldTarget - _body.position;
            if (direction.sqrMagnitude < 0.0001f)
                return false;

            hit = Physics2D.Raycast(_body.position, direction.normalized, reach, anchorMask);
            return hit.collider != null;
        }

        public void Attach(RaycastHit2D hit, bool launchFromGround)
        {
            if (launchFromGround)
                LaunchToward(hit.point);

            AnchorCollider = hit.collider;
            AnchorNormal = hit.normal;
            _rope.connectedAnchor = hit.point;
            _rope.distance = Mathf.Clamp(Vector2.Distance(_body.position, hit.point), clingDistance, reach);
            _rope.enabled = true;
            Attached?.Invoke(hit.point);
        }

        public void Release(bool boost = true)
        {
            if (!IsAttached)
                return;

            _rope.enabled = false;
            if (boost)
                _body.linearVelocity *= releaseBoost;
            Released?.Invoke();
        }

        // Đang đứng yên trên sàn thì dây không có gì để kéo: giật lên một cú để bắt đầu đu
        void LaunchToward(Vector2 anchor)
        {
            Vector2 direction = (anchor - _body.position).normalized;
            float inwardSpeed = Vector2.Dot(_body.linearVelocity, direction);
            if (inwardSpeed < groundLaunchSpeed)
                _body.linearVelocity += direction * (groundLaunchSpeed - inwardSpeed);
        }

        // Đẩy theo phương vuông góc với dây, giống đạp chân khi đánh đu
        void Pump()
        {
            Vector2 toAnchor = (_rope.connectedAnchor - _body.position).normalized;
            Vector2 tangent = new Vector2(toAnchor.y, -toAnchor.x);

            float force = PumpInput * pumpForce;
            if (force == 0f && !Reeling)
                force = AutoPump(tangent);

            if (force != 0f)
                _body.AddForce(tangent * force);
        }

        // Không vuốt thì đẩy nhẹ theo hướng đang đu, biên độ tăng dần như tự nhún
        float AutoPump(Vector2 tangent)
        {
            float tangentialSpeed = Vector2.Dot(_body.linearVelocity, tangent);
            return Mathf.Abs(tangentialSpeed) > 0.5f ? Mathf.Sign(tangentialSpeed) * autoPumpForce : 0f;
        }

        // Rút xúc tu = lực hướng tâm nên mô-men động lượng bảo toàn: r · v_tiếp_tuyến = hằng số.
        // Dây ngắn lại thì vận tốc tiếp tuyến tăng; rút đúng lúc đi qua điểm thấp nhất (lực căng lớn nhất)
        // thì mỗi nhịp đu cao hơn nhịp trước — cộng hưởng giống nhún xích đu.
        void ReelIn()
        {
            Vector2 offset = _body.position - _rope.connectedAnchor;
            float distance = offset.magnitude;
            if (distance <= clingDistance + 0.05f)
            {
                ReachedAnchor?.Invoke();
                return;
            }

            bool taut = distance >= _rope.distance - 0.05f;
            float newLength = Mathf.Max(clingDistance, _rope.distance - reelSpeed * Time.fixedDeltaTime);
            _rope.distance = newLength;
            if (!taut)
                return; // dây đang chùng: rút chỉ thu dây lại, chưa kéo gì

            Vector2 radial = offset / distance;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            float tangentialSpeed = Vector2.Dot(_body.linearVelocity, tangent) * distance / newLength;
            float radialSpeed = Mathf.Min(Vector2.Dot(_body.linearVelocity, radial), -reelSpeed);
            _body.linearVelocity = tangent * tangentialSpeed + radial * radialSpeed;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.8f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
