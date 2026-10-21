using System;
using System.Collections.Generic;
using UnityEngine;

namespace Octopus
{
    /// <summary>Một đòn đánh vào người chơi (PlayerHealth.Hit / Burn): báo cho khiên / chiêu đỡ đòn biết đòn bị chặn.</summary>
    public struct HitInfo
    {
        public float damage;
        /// <summary>Vị trí người đánh (world).</summary>
        public Vector2 from;
        /// <summary>Lực đẩy văng đòn đó lẽ ra gây ra (đã tính hướng ra xa người đánh).</summary>
        public Vector2 knockback;
        public bool heavy;
        /// <summary>Sát thương liên tục (đứng trên sàn cháy).</summary>
        public bool continuous;
    }

    /// <summary>
    /// Máu của người chơi trong màn đánh boss (dùng Health trên người chơi, Max = 300 cho cả bạch tuộc lẫn nhện).
    /// Bị đánh trúng: trừ máu, số sát thương đỏ, bị đẩy văng ra xa người đánh, cộng nộ (RageMeter, 0.5 nộ / 1 máu mất), rồi
    /// bất tử một lúc và nhấp nháy. Sát thương liên tục (đứng trên sàn cháy) chỉ trừ máu, không đẩy, không bất tử.
    /// Hết máu: tắt điều khiển và chiêu, nổ khói, ẩn hình, báo Died (màn THẤT BẠI hiện ra).
    /// Chiêu dùng: Heal (hồi máu, số xanh), BlockFor (chặn mọi sát thương một lúc — khiên, đỡ đòn, bất tử — mỗi đòn
    /// bị chặn báo về cho chiêu).
    /// </summary>
    [RequireComponent(typeof(Health), typeof(OctopusController))]
    public class PlayerHealth : MonoBehaviour
    {
        [Tooltip("Bất tử bấy nhiêu giây sau khi bị đánh trúng")]
        [SerializeField, Min(0f)] float invulnerableTime = 0.8f;
        [Tooltip("Nhấp nháy bấy nhiêu lần mỗi giây trong lúc bất tử")]
        [SerializeField, Min(1f)] float blinkRate = 12f;
        [Tooltip("Nhân vào lực đẩy văng của đòn đánh")]
        [SerializeField, Min(0f)] float knockbackScale = 1f;
        [Tooltip("Màu số sát thương người chơi nhận")]
        [SerializeField] Color damageColor = new Color(1f, 0.32f, 0.28f);
        [Tooltip("Màu số sát thương do cháy")]
        [SerializeField] Color burnColor = new Color(1f, 0.6f, 0.2f);
        [Tooltip("Hiện màn thua sau khi gục bấy nhiêu giây")]
        [SerializeField, Min(0f)] float deathDelay = 1.2f;
        [Tooltip("Màu số máu hồi")]
        [SerializeField] Color healColor = new Color(0.4f, 1f, 0.55f);
        [Tooltip("Màu chữ khi đòn bị chặn")]
        [SerializeField] Color blockColor = new Color(0.75f, 0.9f, 1f);

        /// <summary>Khiên chặn sát thương do BlockFor tạo; Cancel để tắt sớm (khiên vỡ).</summary>
        public sealed class Block
        {
            internal float Until;
            internal Action<HitInfo> OnBlocked;
            internal bool Cancelled;

            /// <summary>Còn chặn không.</summary>
            public bool Active => !Cancelled && Time.time < Until;

            /// <summary>Số giây còn lại.</summary>
            public float Remaining => Active ? Until - Time.time : 0f;

            /// <summary>Số đòn đã chặn.</summary>
            public int Count { get; internal set; }

            public void Cancel() => Cancelled = true;
        }

        Health _health;
        OctopusController _controller;
        Rigidbody2D _body;
        RageMeter _rage;
        CombatController _combat;
        PlayerInputReader _input;
        readonly List<Renderer> _renderers = new List<Renderer>();
        float _invulnerable;
        float _blink;
        bool _hidden;
        readonly List<Block> _blocks = new List<Block>();
        float _lastBlocked = -10f;

        public Health Health => _health;
        public float Current => _health.Current;
        public float Max => _health.Max;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => _invulnerable > 0f;

        /// <summary>Tâm thân người chơi (world): chỗ boss nhắm / đánh trúng. Nhân vật người cao 2 unit nên là ngực.</summary>
        public Vector2 Center => _controller != null && _controller.IsHero ? _body.position + Vector2.up * HeroMotor.ChestOffset : _body.position;

        /// <summary>Đang có khiên chặn sát thương (BlockFor).</summary>
        public bool IsBlocking
        {
            get
            {
                foreach (Block b in _blocks)
                {
                    if (b.Active)
                        return true;
                }

                return false;
            }
        }

        public OctopusController Controller => _controller;

        /// <summary>Vừa bị trúng đòn mất amount máu.</summary>
        public event Action<float> Hurt;

        /// <summary>Hết máu (gọi ngay khi gục).</summary>
        public event Action Died;

        /// <summary>Vừa hồi amount máu.</summary>
        public event Action<float> Healed;

        /// <summary>Một đòn vừa bị khiên chặn.</summary>
        public event Action<HitInfo> Blocked;

        /// <summary>Đã gục xong, tới lúc hiện màn thua.</summary>
        public event Action Defeated;

        void Awake()
        {
            _health = GetComponent<Health>();
            _controller = GetComponent<OctopusController>();
            _body = GetComponent<Rigidbody2D>();
            _rage = GetComponent<RageMeter>();
            _combat = GetComponent<CombatController>();
            _input = GetComponent<PlayerInputReader>();
            _health.Died += OnDied;
        }

        void Start()
        {
            // Hình để nhấp nháy: mọi renderer của nhân vật, trừ tia laze và vòng tầm xúc tu
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<LaserBeamView>(true) != null || r.GetComponent<ReachIndicator>() != null)
                    continue;
                _renderers.Add(r);
            }
        }

        void OnDestroy() => _health.Died -= OnDied;

        /// <summary>
        /// Trúng đòn từ from (world): trừ damage máu, đẩy văng knockback (x = ra xa from, y = hất lên), rồi bất tử
        /// invulnerableTime giây. Đang bất tử / đã gục thì không trúng (trả false).
        /// </summary>
        public bool Hit(float damage, Vector2 from, Vector2 knockback, bool heavy = false)
        {
            if (IsDead || _invulnerable > 0f || !_health.IsAlive)
                return false;

            float away = Mathf.Sign(_body.position.x - from.x + 1e-3f);
            var push = new Vector2(knockback.x * away, knockback.y) * knockbackScale;
            // Khiên: không mất máu, không bị đẩy; báo cho chiêu. Trả true (đòn đã chạm người) để boss thôi đánh lại nhát đó
            if (TryBlock(new HitInfo { damage = damage, from = from, knockback = push, heavy = heavy }))
                return true;

            float dealt = _health.TakeHit(damage, from, new HitEffect { knockback = push, heavy = heavy });
            if (dealt <= 0f)
                return false;

            Hurt?.Invoke(dealt);
            if (_rage != null)
                _rage.AddFromDamageTaken(dealt);
            DamageNumbers.Show(_body.position + Vector2.up * 1.1f, dealt, damageColor, heavy, this);
            if (CombatFx.Instance != null)
                CombatFx.Instance.Burst(_body.position, damageColor, heavy ? 12 : 7, heavy ? 6f : 4f, 0.13f, 0.3f);
            CameraShake.Impulse(push.sqrMagnitude > 1e-4f ? push : Vector2.down, heavy ? 1.3f : 0.7f);
            HitStop.Request(heavy ? 0.07f : 0.04f);

            if (!_health.IsAlive)
                return true;

            if (push.sqrMagnitude > 1e-4f)
                _controller.Knockback(push);
            _invulnerable = invulnerableTime;
            _health.Invulnerable = true;
            _blink = 0f;
            return true;
        }

        /// <summary>Sát thương liên tục (cháy): trừ máu, không đẩy, không bất tử; đang bất tử thì không mất.</summary>
        public float Burn(float damage, Vector2 from)
        {
            if (IsDead || _invulnerable > 0f || IsBlocking)
                return 0f;
            float dealt = _health.TakeHit(damage, from, default);
            if (dealt > 0f)
            {
                if (_rage != null)
                    _rage.AddFromDamageTaken(dealt);
                DamageNumbers.Show(_body.position + Vector2.up * 1.1f, dealt, burnColor, false, this);
            }

            return dealt;
        }

        /// <summary>Hồi đầy máu, hết bất tử (thử lại / test).</summary>
        public void Restore()
        {
            _health.Restore();
            _invulnerable = 0f;
            _health.Invulnerable = false;
            _blocks.Clear();
            SetHidden(false);
        }

        /// <summary>Hồi amount máu (không quá tối đa), hiện số xanh. Trả về lượng hồi thật; đã gục thì 0.</summary>
        public float Heal(float amount)
        {
            if (IsDead || amount <= 0f || !_health.IsAlive)
                return 0f;
            float before = _health.Current;
            _health.Heal(amount);
            float healed = _health.Current - before;
            if (healed > 0.01f)
            {
                DamageNumbers.Show(Center + Vector2.up * 1.1f, healed, healColor, false, this);
                Healed?.Invoke(healed);
            }

            return healed;
        }

        /// <summary>
        /// Chặn mọi đòn đánh vào người chơi trong seconds giây (không mất máu, không bị đẩy, không cháy). Mỗi đòn bị chặn gọi
        /// onBlocked (đòn gì, từ đâu) — chiêu đỡ đòn dùng để biết vừa bị đánh. Nhiều khiên chồng nhau thì đòn bị chặn báo cho cả hai.
        /// Trả về khiên để tắt sớm (Cancel).
        /// </summary>
        public Block BlockFor(float seconds, Action<HitInfo> onBlocked = null)
        {
            var block = new Block { Until = Time.time + Mathf.Max(0f, seconds), OnBlocked = onBlocked };
            _blocks.Add(block);
            return block;
        }

        bool TryBlock(HitInfo hit)
        {
            _blocks.RemoveAll(b => !b.Active);
            if (_blocks.Count == 0)
                return false;

            // Cùng một đòn chạm nhiều frame liền (thân boss đè) chỉ báo một lần
            bool fresh = Time.time - _lastBlocked > 0.2f;
            _lastBlocked = Time.time;
            if (!fresh)
                return true;

            foreach (Block b in _blocks.ToArray())
            {
                b.Count++;
                b.OnBlocked?.Invoke(hit);
            }

            Blocked?.Invoke(hit);
            DamageNumbers.ShowText(Center + Vector2.up * 1.1f, "CHẶN", blockColor, this);
            if (CombatFx.Instance != null)
            {
                CombatFx.Instance.Ring(Center, 1.1f, blockColor, 0.25f);
                CombatFx.Instance.Burst(Center, blockColor, 6, 4f, 0.12f, 0.25f);
            }

            return true;
        }

        void Update()
        {
            if (_invulnerable <= 0f)
                return;

            _invulnerable -= Time.deltaTime;
            _blink += Time.deltaTime * blinkRate;
            if (_invulnerable <= 0f)
            {
                _invulnerable = 0f;
                _health.Invulnerable = false;
                SetHidden(IsDead);
                return;
            }

            SetHidden(IsDead || Mathf.Repeat(_blink, 1f) > 0.5f);
        }

        void OnDied()
        {
            if (IsDead)
                return;
            IsDead = true;
            _blocks.Clear();
            _invulnerable = 0f;
            _health.Invulnerable = false;
            Died?.Invoke();

            // Thôi điều khiển: buông xúc tu / rời đá, rơi xuống sàn
            if (_input != null)
                _input.enabled = false;
            if (_combat != null)
                _combat.enabled = false;
            _controller.SetIntent(false, 0f);
            if (_controller.State is OctopusState.Swinging or OctopusState.Clinging)
                _controller.ResetTo(_body.position);

            if (CombatFx.Instance != null)
            {
                Vector2 at = _body.position;
                CombatFx.Instance.Flash(at, 2.2f, damageColor, 0.35f);
                CombatFx.Instance.Cloud(at, 1.1f, new Color(0.25f, 0.12f, 0.3f, 0.9f), 1.2f, 10);
                CombatFx.Instance.Burst(at, damageColor, 16, 6f, 0.16f, 0.45f);
            }

            SetHidden(true);
            StartCoroutine(AfterDeath());
        }

        System.Collections.IEnumerator AfterDeath()
        {
            for (float t = 0f; t < deathDelay; t += Time.deltaTime)
                yield return null;
            Defeated?.Invoke();
        }

        void SetHidden(bool hidden)
        {
            if (_hidden == hidden)
                return;
            _hidden = hidden;
            foreach (Renderer r in _renderers)
            {
                if (r != null)
                    r.forceRenderingOff = hidden;
            }
        }
    }
}
