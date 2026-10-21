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
        [Tooltip("Rút tới khi dây còn ngắn hơn mức này (unit) là vào đoạn cuối: đà đu hãm dần, người được lái về phía mặt đá " +
                 "đang bám để lên đá gọn (bám từ bên dưới), không bị văng vòng qua đỉnh đá")]
        [SerializeField, Min(0.5f)] float approachDistance = 3f;
        [Tooltip("Đang rút dưới mặt dưới của đá: đà đu không được hất người lên cao quá chỗ bám trừ đi khoảng này (unit), " +
                 "để khỏi va mép đá. Dây còn dài (trên Approach Distance) thì được lên cao hơn")]
        [SerializeField, Min(0f)] float approachClearance = 0.6f;

        [Header("Đu")]
        [Tooltip("Lực đẩy theo phương tiếp tuyến khi vuốt trái/phải")]
        [SerializeField] float pumpForce = 15f;
        [Tooltip("Không vuốt, không giữ tay thì giữ nguyên đà đu: không tự mạnh lên, không yếu dần")]
        [SerializeField] bool holdSwingEnergy = true;
        [Tooltip("Bắn xúc tu khi đang đứng trên sàn thì bị giật về phía đá với vận tốc này")]
        [SerializeField, Min(0f)] float groundLaunchSpeed = 12f;
        [Tooltip("Nhân vận tốc lúc buông xúc tu cho cảm giác văng")]
        [SerializeField, Range(1f, 2f)] float releaseBoost = 1.15f;
        [SerializeField] float maxSpeed = 30f;

        Rigidbody2D _body;
        DistanceJoint2D _rope;
        float _swingEnergy; // cơ năng / khối lượng của nhịp đu đang giữ
        bool _swingEnergyStale = true;

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
                HoldEnergy();
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
            _swingEnergyStale = true;
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
            if (PumpInput == 0f)
                return;

            Vector2 toAnchor = (_rope.connectedAnchor - _body.position).normalized;
            Vector2 tangent = new Vector2(toAnchor.y, -toAnchor.x);
            _body.AddForce(tangent * (PumpInput * pumpForce));
        }

        // Không vuốt, không rút: giữ nguyên cơ năng (động năng + thế năng) của nhịp đu, nên mỗi nhịp lên đúng độ cao
        // nhịp trước — không tự mạnh lên, cũng không bị bộ giải vật lý ăn mòn dần. Vuốt / rút / dây chùng / va vào đá
        // thì cơ năng đổi; buông tay ra là giữ mức mới.
        // Chỉ va chạm thật mới tính; đi xuyên vùng trigger (bia tập, vùng rơi) không làm mất cơ năng
        static readonly ContactFilter2D SolidContacts = new ContactFilter2D { useTriggers = false };

        /// <summary>
        /// Vận tốc vừa bị đổi từ ngoài (bị đánh văng khi đang đu): lấy cơ năng mới làm mức giữ, kẻo bước vật lý sau kéo
        /// vận tốc về mức cũ và triệt mất cú hất.
        /// </summary>
        public void ResetHeldEnergy() => _swingEnergyStale = true;

        void HoldEnergy()
        {
            bool taut = Vector2.Distance(_body.position, _rope.connectedAnchor) >= _rope.distance - 0.05f;
            if (!holdSwingEnergy || PumpInput != 0f || Reeling || !taut || _body.IsTouching(SolidContacts))
            {
                _swingEnergyStale = true;
                return;
            }

            float potential = -Vector2.Dot(Physics2D.gravity * _body.gravityScale, _body.position);
            float kinetic = 0.5f * _body.linearVelocity.sqrMagnitude;
            if (_swingEnergyStale)
            {
                _swingEnergy = kinetic + potential;
                _swingEnergyStale = false;
                return;
            }

            if (kinetic < 0.0001f)
                return; // đứng yên ở đỉnh nhịp: để trọng lực tự kéo quay đầu
            float targetKinetic = Mathf.Max(0f, _swingEnergy - potential);
            _body.linearVelocity *= Mathf.Sqrt(targetKinetic / kinetic);
        }

        // Rút xúc tu = lực hướng tâm nên mô-men động lượng bảo toàn: r · v_tiếp_tuyến = hằng số.
        // Dây ngắn lại thì vận tốc tiếp tuyến tăng; rút đúng lúc đi qua điểm thấp nhất (lực căng lớn nhất)
        // thì mỗi nhịp đu cao hơn nhịp trước — cộng hưởng giống nhún xích đu.
        // Nhưng dây ngắn tới gần đá mà vẫn bảo toàn thì vận tốc tăng vọt (4.5 → 0.9 unit là gấp 5), người văng vòng qua
        // đỉnh đá rồi mới quặt xuống bám. Nên vào đoạn cuối (dây ngắn hơn approachDistance) thì bỏ dần bảo toàn, chặn
        // không cho đà đu hất người lên quá mặt dưới đá, rồi lái người về đúng phía mặt đá đang bám.
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
            float approach = Mathf.InverseLerp(approachDistance, clingDistance, newLength); // 0 = chưa vào đoạn cuối, 1 = sát đá
            float tangentialSpeed = Vector2.Dot(_body.linearVelocity, tangent) * Mathf.Pow(distance / newLength, 1f - approach);
            tangentialSpeed = LimitRise(offset, tangent, tangentialSpeed, newLength);
            if (approach > 0f)
                tangentialSpeed = Mathf.Lerp(tangentialSpeed, SteerToFace(radial, newLength), approach);

            float radialSpeed = Mathf.Min(Vector2.Dot(_body.linearVelocity, radial), -reelSpeed);
            _body.linearVelocity = tangent * tangentialSpeed + radial * radialSpeed;
        }

        // Treo dưới mặt dưới của đá: đang đi lên thì chỉ cho đà đu đủ đưa người tới "trần" = chỗ bám − approachClearance
        // (dây còn dài hơn approachDistance thì trần nới lên bấy nhiêu). Đang rơi xuống thì để trọng lực làm việc.
        float LimitRise(Vector2 offset, Vector2 tangent, float tangentialSpeed, float length)
        {
            Vector2 gravity = Physics2D.gravity * _body.gravityScale;
            float g = gravity.magnitude;
            if (g < 0.0001f)
                return tangentialSpeed;

            Vector2 up = -gravity / g;
            if (Vector2.Dot(AnchorNormal, up) > -0.5f || tangentialSpeed * Vector2.Dot(tangent, up) <= 0f)
                return tangentialSpeed; // không phải mặt dưới của đá, hoặc đang đi xuống

            float ceiling = length - approachDistance - approachClearance; // trần cao hơn chỗ bám bấy nhiêu
            float room = ceiling - Vector2.Dot(offset, up);
            float limit = Mathf.Sqrt(2f * g * Mathf.Max(0f, room));
            return Mathf.Clamp(tangentialSpeed, -limit, limit);
        }

        // Đoạn cuối: vận tốc tiếp tuyến để khi dây rút còn clingDistance thì người nằm đúng phía mặt đá đang bám (dưới mặt
        // dưới, cạnh mặt bên) — đúng chỗ bám đá sẽ đặt người vào, nên lên đá không bị giật ngang
        float SteerToFace(Vector2 radial, float length)
        {
            float angle = Vector2.SignedAngle(radial, AnchorNormal) * Mathf.Deg2Rad;
            float timeLeft = (length - clingDistance) / Mathf.Max(reelSpeed, 0.01f) + Time.fixedDeltaTime;
            float limit = reelSpeed * 1.2f;
            return Mathf.Clamp(angle / timeLeft * length, -limit, limit);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.8f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
