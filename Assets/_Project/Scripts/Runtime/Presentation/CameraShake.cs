using Unity.Cinemachine;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Rung camera khi trúng đòn nặng và chiêu cuối: một cú hích theo hướng đòn rồi lò xo nảy tắt dần, kèm chút rung lắc.
    /// Gắn vào Follow Camera như một extension của Cinemachine: độ lệch cộng vào sau khi camera đã bám theo người chơi
    /// (PositionCorrection), nên không phá việc bám và không làm lệch damping. Chạy theo thời gian thật nên vẫn rung trong
    /// lúc khựng hình (HitStop). Rung nhẹ thôi vì màn hình điện thoại nhỏ.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraShake : CinemachineExtension
    {
        [Tooltip("Bật / tắt rung camera khi trúng đòn")]
        [SerializeField] bool shakeOnHit = true;
        [Tooltip("Hệ số rung chung, nhân vào mọi cú hích")]
        [SerializeField, Range(0f, 2f)] float intensity = 1f;
        [Tooltip("Cú hích của một đòn nặng: camera giật lệch tối đa bấy nhiêu unit")]
        [SerializeField, Min(0f)] float kick = 0.09f;
        [Tooltip("Đòn thường của chiêu cuối (không phải đòn nặng): rung bằng tỉ lệ này của đòn nặng. Đòn thường của chiêu thường không rung")]
        [SerializeField, Range(0f, 1f)] float ultimateLight = 0.4f;
        [Tooltip("Chiêu cuối rung mạnh gấp mấy lần ở mốc nộ 1")]
        [SerializeField, Min(0f)] float ultimateScale = 1.2f;
        [Tooltip("Chiêu cuối rung mạnh thêm bấy nhiêu cho mỗi 1 đơn vị hệ số nộ trên 1 (mốc 3 = hệ số 2.6)")]
        [SerializeField, Min(0f)] float powerBoost = 0.5f;
        [Tooltip("Trần độ lệch (unit), dù nhiều cú hích dồn lại")]
        [SerializeField, Min(0f)] float maxOffset = 0.28f;
        [Tooltip("Tần số nảy của lò xo kéo camera về (lần / giây)")]
        [SerializeField, Range(1f, 12f)] float frequency = 5f;
        [Tooltip("Tắt dần (0..1): càng lớn càng ít nảy qua lại")]
        [SerializeField, Range(0.05f, 0.95f)] float damping = 0.3f;
        [Tooltip("Rung lắc ngẫu nhiên thêm vào (tỉ lệ so với cú hích); 0 = chỉ giật theo hướng đòn")]
        [SerializeField, Range(0f, 1f)] float noise = 0.3f;
        [Tooltip("Rung lắc ngẫu nhiên tắt sau bấy nhiêu giây")]
        [SerializeField, Min(0.01f)] float noiseTime = 0.3f;
        [Tooltip("Nghiêng camera tối đa (độ) theo cú hích ngang; 0 = không nghiêng")]
        [SerializeField, Range(0f, 3f)] float roll = 0.4f;

        static CameraShake s_instance;

        Vector2 _offset, _velocity, _shake;
        float _trauma, _time, _roll;

        // Domain reload tắt: static còn giữ từ lần Play trước
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        /// <summary>Độ lệch camera hiện tại (world, unit).</summary>
        public Vector2 Offset => _shake;

        /// <summary>
        /// Một loạt đòn vừa trúng: rung theo hướng direction (hướng đẩy văng). Đòn nặng rung, chiêu cuối rung (mạnh dần theo
        /// hệ số nộ power), đòn thường của chiêu thường thì không.
        /// </summary>
        public static void Hit(Vector2 direction, bool heavy, bool ultimate, float power)
        {
            if (s_instance == null)
                return;
            float amount = heavy ? 1f : ultimate ? s_instance.ultimateLight : 0f;
            if (ultimate)
                amount *= s_instance.ultimateScale + s_instance.powerBoost * Mathf.Max(0f, power - 1f);
            Impulse(direction, amount);
        }

        /// <summary>Cú hích theo hướng direction, amount = 1 cỡ một đòn nặng.</summary>
        public static void Impulse(Vector2 direction, float amount)
        {
            if (s_instance != null)
                s_instance.Add(direction, amount);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            s_instance = this;
        }

        void OnDisable()
        {
            if (s_instance == this)
                s_instance = null;
            _offset = _velocity = _shake = Vector2.zero;
            _trauma = _roll = 0f;
        }

        void Add(Vector2 direction, float amount)
        {
            if (!shakeOnHit || amount <= 0f || intensity <= 0f)
                return;

            Vector2 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.right;
            float size = Mathf.Min(kick * intensity * amount, maxOffset);
            // Vận tốc ban đầu để lò xo vọt tới đúng độ lệch size ở đỉnh đầu tiên
            float w = 2f * Mathf.PI * frequency;
            float z = damping;
            float root = Mathf.Sqrt(1f - z * z);
            float peak = Mathf.Exp(-z / root * Mathf.Atan2(root, z));
            _velocity += dir * (size * w / peak);
            _velocity = Vector2.ClampMagnitude(_velocity, maxOffset * w / peak);
            _trauma = Mathf.Min(1f, _trauma + 0.5f * amount);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_trauma <= 0f && _offset.sqrMagnitude < 1e-8f && _velocity.sqrMagnitude < 1e-6f)
            {
                _offset = _velocity = _shake = Vector2.zero;
                _roll = 0f;
                return;
            }

            // Lò xo tắt dần, chia bước nhỏ cho ổn định
            float w = 2f * Mathf.PI * frequency;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 240f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                _velocity += (-w * w * _offset - 2f * damping * w * _velocity) * h;
                _offset += _velocity * h;
            }

            _offset = Vector2.ClampMagnitude(_offset, maxOffset);
            _time += dt;
            _trauma = Mathf.Max(0f, _trauma - dt / noiseTime);
            float shake = noise * _trauma * _trauma * kick * intensity;
            var jitter = new Vector2(Mathf.PerlinNoise(_time * 31f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, _time * 29f) - 0.5f);
            _shake = _offset + jitter * (2f * shake);
            _roll = kick > 0f ? Mathf.Clamp(-_offset.x / kick, -2f, 2f) * roll : 0f;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise || deltaTime < 0f || !shakeOnHit)
                return;
            state.PositionCorrection += state.RawOrientation * (Vector3)_shake;
            if (_roll != 0f)
                state.OrientationCorrection *= Quaternion.AngleAxis(_roll, Vector3.forward);
        }
    }
}
