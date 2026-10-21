using System;
using System.Collections;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Chiêu tạm của nhân vật người (RED, PINK, BLUE) để nối đủ từ nút bấm tới đánh trúng trong lúc chờ chiêu thật:
    /// diễn một dáng vung vũ khí rồi đánh một vùng theo kiểu chiêu (thường = trước mặt, ↑ = trước mặt + trên cao, ↓ = sát đất
    /// phía trước). ranged = phép bắn xa (vệt sáng từ điểm phóng phép). Chiêu cuối (nút 3) mạnh theo mốc nộ.
    /// RedSkills / PinkSkills / BlueSkills.Default() dùng tạm lớp này; chiêu thật thay hẳn, không cần sửa lớp này.
    /// </summary>
    [Serializable]
    public class HeroPlaceholderSkill : Skill
    {
        public float damage = 20f;
        [Tooltip("Tầm đánh trước mặt (unit)")]
        public float reach = 2.2f;
        [Tooltip("Phép bắn xa: vệt sáng bay từ điểm phóng phép tới chỗ trúng")]
        public bool ranged;
        public CombatPose pose = CombatPose.Whip;
        [Tooltip("Cả chiêu dài bấy nhiêu giây")]
        public float duration = 0.4f;
        public Color color = Color.white;
        public Vector2 knockback = new Vector2(2.5f, 1f);

        public HeroPlaceholderSkill() { }

        public HeroPlaceholderSkill(string name, string description, Color color, float damage, float cooldown = 0f,
            CombatPose pose = CombatPose.Whip, bool ranged = false, float reach = 2.2f)
        {
            this.name = name;
            this.description = description;
            this.color = color;
            this.damage = damage;
            this.cooldown = cooldown;
            this.pose = pose;
            this.ranged = ranged;
            this.reach = ranged ? Mathf.Max(reach, 5f) : reach;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            int dir = ctx.Direction;
            bool ultimate = ctx.Tier > 0;
            float f = ctx.Facing;
            float reachNow = ultimate ? reach * 1.4f : reach;
            Vector2 c = ctx.Center;

            // Vùng đánh: thường = trước mặt; ↑ = trước mặt và trên cao; ↓ = sát đất phía trước
            Vector2 center, size;
            if (dir > 0)
            {
                center = c + new Vector2(f * reachNow * 0.45f, 1f);
                size = new Vector2(reachNow * 1.1f, 3.2f);
            }
            else if (dir < 0)
            {
                center = c + new Vector2(f * reachNow * 0.5f, -0.1f);
                size = new Vector2(reachNow * 1.2f, 1.4f);
            }
            else
            {
                center = c + new Vector2(f * reachNow * 0.5f, 0.4f);
                size = new Vector2(reachNow * 1.15f, 2f);
            }

            Vector2 aim = center + new Vector2(f * size.x * 0.4f, dir > 0 ? size.y * 0.3f : 0f);
            if (ultimate)
            {
                ctx.Act(CombatPose.Rear, duration * 0.6f, aim);
                ctx.Fx?.Flash(ctx.CastPoint, 1.6f, color, duration * 0.6f, true);
                yield return Wait(duration * 0.6f);
            }

            ctx.Act(pose, duration, aim);
            yield return Wait(duration * 0.3f);

            Color edge = Color.Lerp(color, Color.black, 0.55f);
            if (ranged)
            {
                ctx.Fx?.Streak(ctx.CastPoint, aim, color, edge, ultimate ? 0.6f : 0.35f, 0.3f);
                ctx.Fx?.Flash(aim, ultimate ? 2.2f : 1.2f, color, 0.25f);
            }
            else
            {
                float from = dir > 0 ? -30f : dir < 0 ? 110f : 80f;
                float to = dir > 0 ? 120f : dir < 0 ? -60f : -50f;
                ctx.Fx?.Slash(c + new Vector2(f * 0.3f, dir > 0 ? 0.5f : 0.2f), reachNow * 0.7f, ctx.Angle(from), ctx.Angle(to), color, edge,
                    ultimate ? 0.7f : 0.45f, 0.28f);
            }

            if (dir < 0 && ctx.Fx != null)
                ctx.Fx.Shockwave(ctx.Ground(reachNow * 0.6f), reachNow * 0.5f, color);
            float power = ultimate ? ctx.Power : 1f;
            ctx.HitBox(center, size, damage * power,
                new HitEffect { knockback = new Vector2(knockback.x, dir > 0 ? 6f : knockback.y), heavy = ultimate || dir != 0 }, color);
            yield return Wait(duration * 0.7f);
        }
    }
}
