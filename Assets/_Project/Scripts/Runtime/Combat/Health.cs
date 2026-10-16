using System;
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

    /// <summary>Máu của một mục tiêu đánh được (bia tập, sau này là quái).</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] float maxHealth = 600f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        /// <summary>Vừa mất máu: lượng mất, vị trí người đánh (world), hiệu ứng kèm theo.</summary>
        public event Action<float, Vector2, HitEffect> Damaged;
        public event Action Died;

        void Awake() => Current = maxHealth;

        /// <summary>Trúng đòn: trừ máu (không quá số máu còn), trả về lượng thật sự trừ.</summary>
        public float TakeHit(float amount, Vector2 from, HitEffect effect)
        {
            if (!IsAlive)
                return 0f;

            float dealt = Mathf.Clamp(amount, 0f, Current);
            Current -= dealt;
            Damaged?.Invoke(dealt, from, effect);
            if (Current <= 0f)
                Died?.Invoke();
            return dealt;
        }

        public void Heal(float amount) => Current = Mathf.Min(maxHealth, Current + Mathf.Max(0f, amount));

        public void Restore() => Current = maxHealth;
    }
}
