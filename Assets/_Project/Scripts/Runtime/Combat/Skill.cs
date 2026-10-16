using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Octopus
{
    /// <summary>Dáng ra đòn mà hình nhân vật (bạch tuộc / nhện) tự diễn theo cách riêng của nó.</summary>
    public enum CombatPose
    {
        None,
        Whip,     // quất / đâm nhanh về phía trước (đòn 1 của combo)
        WhipAlt,  // quất bằng chi khác, hướng ngược lại (đòn 2)
        Slam,     // giơ cao rồi nện xuống đất phía trước
        Spin,     // xoay tròn cả người
        Reach,    // vươn chi tới một điểm (móc, phi tơ) và giữ
        Rear,     // chồm người lên, giơ chi (vận sức / chiêu cuối)
        Lunge,    // lao người tới trước (cắn)
        Squash,   // co rúm lại rồi bung ra (phun mực, đẻ nhện con)
    }

    /// <summary>Hình nhân vật diễn dáng ra đòn (OctopusRigAnimator, SpiderRigAnimator).</summary>
    public interface ICombatAnimator
    {
        /// <summary>Diễn dáng pose trong duration giây; point = điểm nhắm (world), dùng cho Whip / Slam / Reach.</summary>
        void Act(CombatPose pose, float duration, Vector2 point);
    }

    /// <summary>
    /// Một chiêu. Mỗi nhân vật có 9 chiêu: nút 1 (đánh thường), 2 (chiêu), 3 (chiêu cuối, cần nộ), mỗi nút 3 kiểu: chạm, vuốt
    /// sang nút này, vuốt sang nút kia. Thông số (sát thương, tầm, hồi chiêu…) chỉnh được trong Inspector của Combat Controller.
    /// </summary>
    [Serializable]
    public abstract class Skill
    {
        [Tooltip("Tên hiện trên màn hình khi chọn chiêu")]
        public string name;
        [TextArea(1, 3)] public string description;
        [Tooltip("Hồi chiêu sau khi dùng xong (giây). Đánh thường để 0")]
        public float cooldown;

        public abstract IEnumerator Run(SkillContext ctx);

        protected static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
                yield return null;
        }
    }

    /// <summary>Những gì một chiêu cần khi chạy: người chơi, hình nhân vật, hiệu ứng, nộ, và các hàm đánh trúng mục tiêu.</summary>
    public class SkillContext
    {
        static readonly Collider2D[] s_hits = new Collider2D[32];
        static readonly HashSet<Health> s_seen = new HashSet<Health>();

        public CombatController Combat;
        public OctopusController Player;
        public Rigidbody2D Body;
        public RageMeter Rage;
        public LaserSkill Laser;
        public LayerMask Targets;
        public ICombatAnimator Anim;

        /// <summary>Chiêu cuối: mốc nộ lúc dùng (1..3) và hệ số sát thương tương ứng; chiêu thường: 0 và 1.</summary>
        public int Tier;
        public float Power = 1f;
        /// <summary>Đánh trúng có cộng nộ không (chiêu cuối thì không).</summary>
        public bool GainsRage = true;

        public CombatFx Fx => CombatFx.Instance;
        public float Facing => Player.Facing < 0f ? -1f : 1f;
        public Vector2 Forward => new Vector2(Facing, 0f);

        /// <summary>Tâm thân người chơi (world).</summary>
        public Vector2 Center => Body.position;

        /// <summary>Được đẩy người đi không (đang đứng / đang bay; đu và bám đá thì không).</summary>
        public bool CanMove => Player.State is OctopusState.Grounded or OctopusState.Airborne;

        /// <summary>Điểm trên mặt đất (đáy collider) cách thân dx về phía trước.</summary>
        public Vector2 Ground(float dx) => Center + new Vector2(Facing * dx, -0.45f);

        public void Act(CombatPose pose, float duration, Vector2 point = default) => Anim?.Act(pose, duration, point);

        /// <summary>Góc (độ, world) của hướng a độ "lên trên so với phía trước": 0 = thẳng trước mặt, 90 = thẳng lên.</summary>
        public float Angle(float a) => Facing > 0f ? a : 180f - a;

        // ---------- Đánh trúng ----------

        // sparks = false: sát thương liên tục (laze, mực) thì không toé tia lửa mỗi frame
        public int HitCircle(Vector2 center, float radius, float damage, HitEffect effect, Color color, bool sparks = true) =>
            Apply(Physics2D.OverlapCircle(center, radius, Filter, s_hits), damage, effect, color, sparks);

        public int HitBox(Vector2 center, Vector2 size, float damage, HitEffect effect, Color color, bool sparks = true) =>
            Apply(Physics2D.OverlapBox(center, size, 0f, Filter, s_hits), damage, effect, color, sparks);

        /// <summary>Đánh mọi mục tiêu trên đoạn a → b dày radius (tia laze xuyên thấu).</summary>
        public int HitSegment(Vector2 a, Vector2 b, float radius, float damage, HitEffect effect, Color color, bool sparks = true)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 1e-3f)
                return HitCircle(a, radius, damage, effect, color, sparks);
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            return Apply(Physics2D.OverlapBox((a + b) * 0.5f, new Vector2(length, radius * 2f), angle, Filter, s_hits),
                damage, effect, color, sparks);
        }

        /// <summary>Mục tiêu còn sống gần nhất phía trước (trong tầm range, lệch dọc không quá maxDy).</summary>
        public Health NearestAhead(float range, float maxDy = 2f)
        {
            int count = Physics2D.OverlapCircle(Center, range, Filter, s_hits);
            Health best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Health h = s_hits[i].GetComponentInParent<Health>();
                if (h == null || !h.IsAlive)
                    continue;
                Vector2 to = (Vector2)h.transform.position - Center;
                if (to.x * Facing < -0.2f || Mathf.Abs(to.y) > maxDy)
                    continue;
                float distance = Mathf.Abs(to.x);
                if (distance < bestDistance)
                {
                    best = h;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Tâm thân mục tiêu (world): chỗ nhắm và chỗ hiện số sát thương.</summary>
        public static Vector2 Aim(Health target) => (Vector2)target.transform.position + Vector2.up * 0.9f;

        ContactFilter2D Filter
        {
            get
            {
                var filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(Targets);
                return filter;
            }
        }

        int Apply(int count, float damage, HitEffect effect, Color color, bool sparks)
        {
            s_seen.Clear();
            int hit = 0;
            float total = 0f;
            Vector2 push = Vector2.zero;
            bool ultimate = Tier > 0;
            for (int i = 0; i < count; i++)
            {
                Health h = s_hits[i].GetComponentInParent<Health>();
                if (h == null || !h.IsAlive || !s_seen.Add(h))
                    continue;

                // Đẩy văng tính theo phía mục tiêu đứng so với người đánh
                float away = Mathf.Sign(h.transform.position.x - Center.x + 1e-3f * Facing);
                HitEffect e = effect;
                e.knockback = new Vector2(effect.knockback.x * away, effect.knockback.y);
                float dealt = h.TakeHit(damage, Center, e);
                if (GainsRage)
                    Rage.AddFromDamage(dealt);
                DamageNumbers.Show(Aim(h) + Vector2.up * 0.7f, dealt, color, effect.heavy, h, ultimate && sparks ? 1.15f : 1f);
                if (sparks && Fx != null)
                    Fx.Burst(Aim(h), color, effect.heavy ? 12 : 6, effect.heavy ? 6f : 4f, 0.13f, 0.3f);
                total += dealt;
                push += e.knockback.sqrMagnitude > 1e-4f ? e.knockback.normalized : new Vector2(away, 0f);
                hit++;
            }

            // Cảm giác trúng đòn: cả loạt (chiêu lan trúng 3 mục tiêu) chỉ khựng hình + rung camera một lần.
            // Sát thương liên tục (sparks = false: laze, vùng mực, xoáy nước) thì không.
            if (hit > 0 && sparks)
            {
                HitStop.Hit(effect.heavy, ultimate, Power, total);
                CameraShake.Hit(push, effect.heavy, ultimate, Power);
            }

            return hit;
        }
    }
}
