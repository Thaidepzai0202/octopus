using System;
using System.Collections.Generic;
using UnityEngine;

namespace Octopus
{
    /// <summary>Hiệu ứng kèm theo một đòn đánh (ngoài sát thương).</summary>
    [Serializable]
    public struct HitEffect
    {
        [Tooltip("Đẩy văng: x = ra xa người đánh (âm = kéo lại gần), y = hất lên (unit/giây)")]
        public Vector2 knockback;
        [Tooltip("Choáng (giây)")]
        public float stun;
        [Tooltip("Bị tơ trói / mực làm chậm (giây)")]
        public float snare;
        [Tooltip("Độc: sát thương mỗi giây")]
        public float poisonDps;
        [Tooltip("Độc kéo dài (giây)")]
        public float poisonTime;
        [Tooltip("Bị cuốn kén tơ")]
        public bool cocoon;
        [Tooltip("Làm chậm bằng mực (mục tiêu nhuộm tối) thay vì tơ")]
        public bool ink;
        [Tooltip("Đòn nặng: số sát thương to, mục tiêu rung mạnh")]
        public bool heavy;
    }

    /// <summary>
    /// Máu của một mục tiêu đánh được (bia tập, boss) hoặc của người chơi (PlayerHealth).
    /// Đóng băng (Freeze, chiêu ngưng đọng thời gian): mục tiêu đứng hình N giây — bia tập đứng im, boss dừng cả đòn đang ra lẫn
    /// cầu lửa / thiên thạch / lửa cháy của nó. Vẫn nhận sát thương bình thường trong lúc đóng băng.
    /// </summary>
    public class Health : MonoBehaviour
    {
        static readonly List<Health> s_all = new List<Health>();

        [SerializeField, Min(1f)] float maxHealth = 600f;

        float _frozen;

        /// <summary>Mọi Health đang bật trong màn (chiêu tìm kẻ địch: SkillContext.Enemies, FreezeAll).</summary>
        public static IReadOnlyList<Health> All => s_all;

        // Editor tắt domain reload nên danh sách static còn từ lần Play trước
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_all.Clear();

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        /// <summary>Nhân vào mọi sát thương nhận vào (vd boss choáng gục nhận thêm 25% = 1.25).</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Đang bất tử: đòn đánh không trừ máu (người chơi vừa bị đánh trúng).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Vừa mất máu: lượng mất, vị trí người đánh (world), hiệu ứng kèm theo.</summary>
        public event Action<float, Vector2, HitEffect> Damaged;
        public event Action Died;

        /// <summary>Đang đóng băng (đứng hình).</summary>
        public bool IsFrozen => _frozen > 0f;

        /// <summary>Số giây đóng băng còn lại.</summary>
        public float FrozenRemaining => _frozen;

        /// <summary>Bắt đầu (true) / hết (false) đóng băng.</summary>
        public event Action<bool> FrozenChanged;

        void Awake() => Current = maxHealth;

        void OnEnable()
        {
            if (!s_all.Contains(this))
                s_all.Add(this);
        }

        void OnDisable() => s_all.Remove(this);

        /// <summary>Đóng băng seconds giây (đang đóng băng thì kéo dài tới mốc xa hơn). Đã gục thì thôi.</summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f || !IsAlive)
                return;
            bool was = IsFrozen;
            _frozen = Mathf.Max(_frozen, seconds);
            if (!was)
                FrozenChanged?.Invoke(true);
        }

        /// <summary>Thôi đóng băng ngay.</summary>
        public void Unfreeze()
        {
            if (!IsFrozen)
                return;
            _frozen = 0f;
            FrozenChanged?.Invoke(false);
        }

        void Update()
        {
            if (_frozen <= 0f)
                return;
            _frozen -= Time.deltaTime;
            if (_frozen <= 0f)
            {
                _frozen = 0f;
                FrozenChanged?.Invoke(false);
            }
        }

        /// <summary>Trúng đòn: trừ máu (không quá số máu còn), trả về lượng thật sự trừ.</summary>
        public float TakeHit(float amount, Vector2 from, HitEffect effect)
        {
            if (!IsAlive || Invulnerable)
                return 0f;

            float dealt = Mathf.Clamp(amount * DamageMultiplier, 0f, Current);
            Current -= dealt;
            Damaged?.Invoke(dealt, from, effect);
            if (Current <= 0f)
            {
                Unfreeze();
                Died?.Invoke();
            }

            return dealt;
        }

        public void Heal(float amount) => Current = Mathf.Min(maxHealth, Current + Mathf.Max(0f, amount));

        public void Restore() => Current = maxHealth;
    }
}
