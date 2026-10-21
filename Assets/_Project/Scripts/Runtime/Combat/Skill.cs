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

    /// <summary>Hình nhân vật diễn dáng ra đòn (OctopusRigAnimator, SpiderRigAnimator, hình nhân vật người IHeroVisual).</summary>
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

    /// <summary>
    /// Những gì một chiêu cần khi chạy: người chơi, hình nhân vật, hiệu ứng, nộ, và các hàm đánh trúng mục tiêu.
    /// Nhân vật người có thêm: Hero (thân: nhảy, lướt, dịch chuyển, lơ lửng, bay), HeroVisual (hình: dáng, vũ khí, bóng ma),
    /// Variant / Direction (kiểu chiêu theo cần điều hướng), Health (hồi máu, đỡ đòn), Freeze (đóng băng thời gian), Buff
    /// (trạng thái dùng chung giữa các chiêu, vd một chiêu cường hoá làm đánh thường mạnh hơn).
    /// </summary>
    /// <summary>Hệ số cỡ và tầm chiêu của một nhân vật (CombatController, mỗi nhân vật một cặp).</summary>
    [Serializable]
    public struct SkillReach
    {
        [Tooltip("Cỡ chiêu: vùng đánh trúng (bán kính, khung) và cỡ hiệu ứng nhân với số này")]
        [Min(0.1f)] public float size;
        [Tooltip("Tầm chiêu: khoảng cách đánh tới, quãng đạn / sóng bay, quãng lướt nhân với số này")]
        [Min(0.1f)] public float range;

        public SkillReach(float size, float range)
        {
            this.size = size;
            this.range = range;
        }

        public static SkillReach Double => new SkillReach(2f, 2f);
    }

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

        /// <summary>Máu người chơi (màn đánh boss). Null ở màn hang động (không có gì đánh người chơi): Heal / BlockFor khi đó không làm gì.</summary>
        public PlayerHealth Health;

        /// <summary>Thân nhân vật người (Launch, JumpTo, Dash, TeleportTo, Hover, Root, SetGravityScale, BeginFlight…); null với bạch tuộc / nhện.</summary>
        public HeroMotor Hero;

        /// <summary>Nút đang dùng (0..2) và kiểu chiêu (0..2; nhân vật người: 0 = thường, 1 = ↑, 2 = ↓).</summary>
        public int Slot, Variant;

        /// <summary>Tổng sát thương lần đánh trúng gần nhất (HitBox / HitCircle / HitSegment): hút máu, dồn sát thương.</summary>
        public float LastDealt;

        /// <summary>Chiêu cuối: mốc nộ lúc dùng (1..3) và hệ số sát thương tương ứng; chiêu thường: 0 và 1.</summary>
        public int Tier;
        public float Power = 1f;
        /// <summary>Đánh trúng có cộng nộ không (chiêu cuối thì không).</summary>
        public bool GainsRage = true;

        /// <summary>
        /// Cỡ chiêu của nhân vật (×): mọi bán kính / khung đánh trúng và cỡ hiệu ứng của chiêu nhân với số này.
        /// </summary>
        public float Size = 1f;

        /// <summary>
        /// Tầm chiêu của nhân vật (×): khoảng cách đánh tới trước mặt, tầm tìm mục tiêu, quãng đạn / sóng bay (và tốc độ bay, để
        /// thời gian bay không đổi), quãng lướt nhân với số này.
        /// </summary>
        public float Range = 1f;

        /// <summary>Bản sao nông cho một lần dùng chiêu (CombatController.Use): sửa ngữ cảnh cho lần sau không ảnh hưởng lần trước.</summary>
        public SkillContext Snapshot() => (SkillContext)MemberwiseClone();

        public CombatFx Fx => CombatFx.Instance;
        public float Facing => Player.Facing < 0f ? -1f : 1f;
        public Vector2 Forward => new Vector2(Facing, 0f);

        /// <summary>Tâm thân người chơi (world).</summary>
        public Vector2 Center => Body.position;

        /// <summary>Được đẩy người đi không (đang đứng / đang bay; đu và bám đá thì không).</summary>
        public bool CanMove => Player.State is OctopusState.Grounded or OctopusState.Airborne;

        /// <summary>Đang đứng trên sàn / mặt đá.</summary>
        public bool Grounded => Player.State == OctopusState.Grounded;

        // ---------- Nhân vật người ----------

        public bool IsHero => Hero != null;

        /// <summary>Hình nhân vật người: Act, Facing, SetGhost, SetWeaponPower, Flash, WeaponTip, CastPoint, VisualBounds.</summary>
        public IHeroVisual HeroVisual => Hero != null ? Hero.Visual : null;

        /// <summary>Nhân vật người: hướng kéo cần lúc bấm chiêu: 1 = ↑, -1 = ↓, 0 = thường.</summary>
        public int Direction => Variant == 1 ? 1 : Variant == 2 ? -1 : 0;

        /// <summary>Ngực (world): nhân vật người cao 2 unit nên ngực cao hơn tâm thân; bạch tuộc / nhện = Center.</summary>
        public Vector2 Chest => Hero != null ? Hero.Chest : Center;

        /// <summary>Mũi vũ khí (world) của nhân vật người; con khác: trước mặt 1 unit.</summary>
        public Vector2 WeaponTip => HeroVisual != null ? HeroVisual.WeaponTip : Center + Forward;

        /// <summary>Điểm phóng phép (world) của nhân vật người; con khác: trước mặt 0.6 unit.</summary>
        public Vector2 CastPoint => HeroVisual != null ? HeroVisual.CastPoint : Center + Forward * 0.6f;

        // ---------- Máu người chơi ----------

        /// <summary>Hồi máu cho người chơi; trả về lượng hồi thật. Không có máu người chơi (màn hang động) thì không làm gì.</summary>
        public float Heal(float amount) => Health != null ? Health.Heal(amount) : 0f;

        /// <summary>
        /// Chặn mọi sát thương vào người chơi trong seconds giây (khiên, đỡ đòn, bất tử); mỗi đòn bị chặn gọi onBlocked
        /// (đòn gì, từ đâu). Trả về khiên để huỷ sớm (Cancel) — null nếu màn không có máu người chơi.
        /// </summary>
        public PlayerHealth.Block BlockFor(float seconds, Action<HitInfo> onBlocked = null) =>
            Health != null ? Health.BlockFor(seconds, onBlocked) : null;

        // ---------- Đóng băng thời gian ----------

        /// <summary>Đóng băng một mục tiêu seconds giây (đứng hình; boss: cả đòn đang ra và đạn của nó cũng dừng).</summary>
        public void Freeze(Health target, float seconds)
        {
            if (target != null && target.IsAlive)
                target.Freeze(seconds);
        }

        /// <summary>Đóng băng mọi kẻ địch còn sống (lớp Targets) trong seconds giây; trả về danh sách bị đóng băng.</summary>
        public List<Health> FreezeAll(float seconds)
        {
            List<Health> frozen = Enemies();
            foreach (Health h in frozen)
                h.Freeze(seconds);
            return frozen;
        }

        /// <summary>Mọi kẻ địch còn sống (lớp Targets) trong màn; radius &gt; 0 thì chỉ lấy trong bán kính đó quanh người chơi.</summary>
        public List<Health> Enemies(float radius = 0f)
        {
            var list = new List<Health>();
            foreach (Health h in global::Octopus.Health.All)
            {
                if (h == null || !h.IsAlive || (Targets.value & (1 << h.gameObject.layer)) == 0)
                    continue;
                if (radius > 0f && Vector2.Distance(Aim(h), Center) > radius)
                    continue;
                list.Add(h);
            }

            return list;
        }

        // ---------- Trạng thái dùng chung giữa các chiêu ----------

        /// <summary>Bật trạng thái key trong seconds giây với giá trị value (vd "CuongHoa": chiêu cường hoá làm đánh thường mạnh hơn).</summary>
        public void SetBuff(string key, float seconds, float value = 1f) => Combat.SetBuff(key, seconds, value);

        /// <summary>Giá trị trạng thái key còn hiệu lực, 0 nếu đã hết / chưa bật.</summary>
        public float Buff(string key) => Combat.Buff(key);

        public bool HasBuff(string key) => Combat.Buff(key) != 0f;

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

        /// <summary>
        /// Đánh đúng mục tiêu h (không lan sang mục tiêu khác đứng chồng lên nó): đòn lướt qua nhiều mục tiêu, mỗi mục tiêu
        /// trúng một lần. Cộng nộ, số sát thương, tia lửa, khựng hình như HitBox.
        /// </summary>
        public int HitTarget(Health h, float damage, HitEffect effect, Color color, bool sparks = true)
        {
            if (h == null || !h.IsAlive)
                return 0;
            return Apply(null, h, 1, damage, effect, color, sparks);
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

        int Apply(int count, float damage, HitEffect effect, Color color, bool sparks) =>
            Apply(s_hits, null, count, damage, effect, color, sparks);

        // hits = các collider vừa quét được (mục tiêu = Health cha của chúng); hits = null thì đánh đúng một mục tiêu single
        int Apply(Collider2D[] hits, Health single, int count, float damage, HitEffect effect, Color color, bool sparks)
        {
            // Người chơi đã gục (PlayerHealth tắt CombatController): đạn / nhện con / sóng đang bay không còn gây sát thương
            if (Combat != null && !Combat.isActiveAndEnabled)
                return 0;

            s_seen.Clear();
            int hit = 0;
            float total = 0f;
            Vector2 push = Vector2.zero;
            bool ultimate = Tier > 0;
            for (int i = 0; i < count; i++)
            {
                Health h = hits != null ? hits[i].GetComponentInParent<Health>() : single;
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

            LastDealt = total;

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
