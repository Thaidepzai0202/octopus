using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Nộ của người chơi: đầy dần khi di chuyển và khi đánh trúng. Có 3 mốc (100 / 200 / 300): qua mốc 1 mới dùng được
    /// chiêu cuối; mốc càng cao chiêu cuối càng đau. Dùng chiêu cuối là xả hết nộ (kiểu thanh Super 3 nấc của game đối kháng).
    /// </summary>
    public class RageMeter : MonoBehaviour
    {
        public const int Tiers = 3;
        public const float PerTier = 100f;

        [Tooltip("Nộ tăng thêm mỗi unit di chuyển (bò, bay, đu, bò ngược)")]
        [SerializeField, Min(0f)] float perUnitMoved = 2.5f;
        [Tooltip("Nộ tăng thêm mỗi điểm sát thương gây ra (chiêu cuối không cộng nộ)")]
        [SerializeField, Min(0f)] float perDamage = 0.35f;
        [Tooltip("Nộ tăng thêm mỗi điểm máu bị mất (bị boss đánh): bị dồn ép thì nhanh có chiêu cuối để lật thế")]
        [SerializeField, Min(0f)] float perDamageTaken = 0.5f;
        [Tooltip("Chiêu cuối mạnh gấp mấy lần ở mốc 1, 2, 3")]
        [SerializeField] float[] tierMultiplier = { 1f, 1.7f, 2.6f };

        Rigidbody2D _body;
        Vector2 _last;
        bool _hasLast;

        public float Value { get; private set; }
        public float Max => Tiers * PerTier;

        /// <summary>Mốc đã đạt: 0 (chưa đủ dùng chiêu cuối) … 3 (đầy).</summary>
        public int Tier => Mathf.Clamp(Mathf.FloorToInt(Value / PerTier + 1e-4f), 0, Tiers);

        public event Action<float> Changed;
        public event Action<int> TierReached;

        /// <summary>Chiêu cuối ở mốc tier mạnh gấp mấy lần (0 nếu chưa đủ mốc 1).</summary>
        public float Multiplier(int tier) =>
            tier < 1 ? 0f : tierMultiplier[Mathf.Clamp(tier - 1, 0, tierMultiplier.Length - 1)];

        void Awake() => _body = GetComponent<Rigidbody2D>();

        void FixedUpdate()
        {
            if (_body == null)
                return;

            Vector2 position = _body.position;
            float moved = _hasLast ? Vector2.Distance(position, _last) : 0f;
            _last = position;
            _hasLast = true;
            if (moved < 2f) // dịch chuyển tức thời (hồi sinh, đặt lại chỗ) thì không tính
                Add(moved * perUnitMoved);
        }

        public void AddFromDamage(float damage) => Add(damage * perDamage);

        /// <summary>Bị đánh mất damage máu: cộng nộ theo perDamageTaken.</summary>
        public void AddFromDamageTaken(float damage) => Add(damage * perDamageTaken);

        public void Add(float amount)
        {
            if (amount <= 0f || Value >= Max)
                return;

            int before = Tier;
            Value = Mathf.Min(Max, Value + amount);
            Changed?.Invoke(Value);
            if (Tier > before)
                TierReached?.Invoke(Tier);
        }

        /// <summary>Xả hết nộ để dùng chiêu cuối; false nếu chưa qua mốc 1.</summary>
        public bool TrySpend(out int tier)
        {
            tier = Tier;
            if (tier < 1)
                return false;

            Value = 0f;
            Changed?.Invoke(Value);
            return true;
        }
    }
}
