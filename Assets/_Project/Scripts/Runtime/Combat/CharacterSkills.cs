using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Octopus
{
    // Bộ chiêu 2 nhân vật. Mỗi nút 3 kiểu: chạm, vuốt sang nút này, vuốt sang nút kia (thứ tự trong mảng: nút 1 chạm, →2, →3,
    // nút 2 chạm, →1, →3, nút 3 chạm, →1, →2). Phân tích đầy đủ: Docs/ky_nang/PHAN_TICH_KY_NANG.md
    //
    // Bạch tuộc — đấu sĩ tầm trung, khống chế: xúc tu dài (quất, xoay, móc kéo), mực (trốn + làm chậm), mắt laze (rỉa máu
    // từ xa). Chiêu cuối là "kraken": xúc tu khổng lồ, xoáy nước, đại pháo laze. Tham khảo: Illaoi (LoL), Tidehunter (Dota 2).
    // Nhện — sát thủ nhanh nhẹn: cào tẩm độc, vồ / phi tơ áp sát, bắn tơ trói. Chiêu cuối: kén tơ, bầy nhện con, lưới khổng lồ.
    // Tham khảo: Elise (LoL), Broodmother (Dota 2), Spider-Man.

    static class SkillColors
    {
        public static readonly Color Octopus = new Color(0.5f, 0.85f, 1f);
        public static readonly Color OctopusHeavy = new Color(0.8f, 0.55f, 1f);
        /// <summary>Mép trong vệt chém bạch tuộc (lõi trắng → xanh → tím)</summary>
        public static readonly Color OctopusEdge = new Color(0.42f, 0.3f, 1f);
        /// <summary>Thân vệt chém bạch tuộc: xanh đậm hơn màu số sát thương cho nổi trên nền hang tím</summary>
        public static readonly Color OctopusSlash = new Color(0.22f, 0.7f, 1f);
        /// <summary>Lửa tím, vết nứt sáng của bạch tuộc</summary>
        public static readonly Color OctopusGlow = new Color(0.62f, 0.3f, 1f);
        public static readonly Color Laser = new Color(0.75f, 0.92f, 1f);
        public static readonly Color Ink = new Color(0.52f, 0.28f, 0.9f, 0.95f);
        public static readonly Color InkHit = new Color(0.62f, 0.45f, 0.95f);
        public static readonly Color InkGlow = new Color(0.78f, 0.55f, 1f);
        public static readonly Color Water = new Color(0.3f, 0.75f, 1f);
        public static readonly Color Foam = new Color(0.75f, 0.95f, 1f);
        public static readonly Color Rock = new Color(0.42f, 0.36f, 0.55f);
        public static readonly Color Spider = new Color(1f, 0.45f, 0.9f);
        /// <summary>Mép trong vết cào nhện (hồng → tím đỏ)</summary>
        public static readonly Color SpiderEdge = new Color(0.72f, 0.18f, 0.95f);
        /// <summary>Thân vết cào nhện: hồng đậm</summary>
        public static readonly Color SpiderSlash = new Color(1f, 0.3f, 0.8f);
        public static readonly Color Web = new Color(0.95f, 0.95f, 1f);
        /// <summary>Quầng sáng của tơ (trắng ngả hồng)</summary>
        public static readonly Color Silk = new Color(1f, 0.85f, 0.97f);
        public static readonly Color Poison = new Color(0.55f, 1f, 0.35f);
        /// <summary>Khói độc xanh mỏng sau lưng nhện lúc đẻ bầy nhện con</summary>
        public static readonly Color PoisonSmoke = new Color(0.4f, 0.85f, 0.2f, 0.4f);
        /// <summary>Bụi đất tung lên khi bật nhảy / tiếp đất</summary>
        public static readonly Color Dust = new Color(0.5f, 0.42f, 0.62f, 0.55f);

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }

    /// <summary>
    /// Cỡ / tầm chiêu (SkillContext.Size / Range) của bạch tuộc và nhện. Quy ước chung: khoảng cách tới trước mặt (tâm vùng
    /// đánh, tầm tìm mục tiêu, quãng bay / lướt) nhân Range; bán kính, khung đánh trúng và cỡ hiệu ứng nhân Size. Điểm gắn
    /// với thân (miệng, gốc xúc tu) và điểm nằm trên mục tiêu giữ nguyên.
    /// </summary>
    static class SkillScale
    {
        /// <summary>
        /// Độ dày vệt chém mảnh (&lt; 0.3 unit) nhân Size nhưng giữ dưới ngưỡng 0.3 của CombatFx.Slash: qua ngưỡng đó CombatFx
        /// đổi sang ảnh vệt dày có lõi sáng, vệt mảnh lót bên trong sẽ thành một vệt dày nữa chồng lên vệt chính.
        /// </summary>
        public static float Thin(float width, float size) => Mathf.Min(width * size, 0.29f);
    }

    /// <summary>Chiêu bắn laze (dùng LaserSkill: mắt sáng, tia, tia lửa); mục tiêu trên đường tia mất máu liên tục.</summary>
    [Serializable]
    public abstract class LaserBeamSkill : Skill
    {
        public LaserSkill.LaserShot shot;
        [Tooltip("Sát thương mỗi giây cho mọi mục tiêu tia đi qua")]
        public float damagePerSecond = 100f;
        [Tooltip("Chiêu cuối: sát thương nhân theo mốc nộ")]
        public bool scalesWithRage;

        public override IEnumerator Run(SkillContext ctx)
        {
            // Cỡ chiêu: tia to gấp Size lần — cả hình tia (LaserBeamView đọc WidthScale) lẫn bề dày vùng trúng bên dưới.
            // Tầm chiêu: tia dài gấp Range lần ô Range của Laser Skill (vẫn dừng ở vách / sàn)
            LaserSkill.LaserShot cast = shot;
            cast.width = (shot.width > 0f ? shot.width : 1f) * ctx.Size;
            cast.rangeScale = ctx.Range;
            if (ctx.Laser == null || !ctx.Laser.TryCast(cast))
                yield break;
            if (scalesWithRage)
                ctx.Act(CombatPose.Rear, shot.charge + 0.3f);

            float power = scalesWithRage ? ctx.Power : 1f;
            while (ctx.Laser.Phase is LaserPhase.Charging or LaserPhase.Firing)
            {
                if (ctx.Laser.Phase == LaserPhase.Firing)
                    ctx.HitSegment(ctx.Laser.Origin, ctx.Laser.BeamEnd, 0.35f * ctx.Laser.WidthScale,
                        damagePerSecond * power * Time.deltaTime, default, SkillColors.Laser, false);
                yield return null;
            }
        }
    }

    // =====================================================================================================================
    // BẠCH TUỘC
    // =====================================================================================================================

    /// <summary>Nút 1 · chạm — Quất xúc tu: combo 3 đòn (quất xuống, quất hất lên, nện xúc tu xuống đất).</summary>
    [Serializable]
    public class TentacleCombo : Skill
    {
        public float damage = 20f;
        public float finisherDamage = 38f;
        public float reach = 1.9f;
        [Tooltip("Bấm lại trong bấy nhiêu giây sau đòn trước thì ra đòn tiếp của combo")]
        public float comboWindow = 0.7f;

        int _step;
        float _lastEnd = -10f;

        public override IEnumerator Run(SkillContext ctx)
        {
            if (Time.time - _lastEnd > comboWindow)
                _step = 0;
            int step = _step;
            _step = (_step + 1) % 3;
            Vector2 c = ctx.Center;
            float size = ctx.Size, far = ctx.Range;

            if (step < 2)
            {
                bool alt = step == 1;
                // Điểm nhắm xa gấp Range lần (cả phần lệch dọc, nên góc quất của xúc tu giữ nguyên như đã chỉnh)
                ctx.Act(alt ? CombatPose.WhipAlt : CombatPose.Whip, 0.42f, c + (ctx.Forward * reach + Vector2.up * (alt ? 0.6f : -0.1f)) * far);
                yield return Wait(0.105f); // xúc tu lấy đà vươn cao sau đầu rồi mới quất
                // Vệt lưỡi liềm theo đường ngọn xúc tu + vệt mảnh bên trong cho dày dặn
                Vector2 arc = c + ctx.Forward * (0.3f * far);
                ctx.Fx?.Slash(arc, reach * 0.75f * size, ctx.Angle(alt ? -55f : 75f), ctx.Angle(alt ? 70f : -45f),
                    SkillColors.OctopusSlash, SkillColors.OctopusEdge, 0.55f * size, 0.26f);
                ctx.Fx?.Slash(arc, reach * 0.58f * size, ctx.Angle(alt ? -45f : 65f), ctx.Angle(alt ? 60f : -35f),
                    SkillColors.OctopusSlash, SkillColors.OctopusEdge, SkillScale.Thin(0.22f, size), 0.2f, 1f, 0.03f);
                ctx.HitBox(c + ctx.Forward * (reach * 0.55f * far), new Vector2(reach * 1.15f, 1.5f) * size, damage,
                    new HitEffect { knockback = new Vector2(2.5f, alt ? 2f : 0.3f) }, SkillColors.Octopus);
                yield return Wait(0.185f);
            }
            else
            {
                Vector2 ground = ctx.Ground(1.4f * far);
                ctx.Act(CombatPose.Slam, 0.5f, ground);
                yield return Wait(0.15f);
                ctx.Fx?.Slash(c + ctx.Forward * (0.6f * far), 1.1f * size, ctx.Angle(110f), ctx.Angle(-40f), SkillColors.OctopusSlash,
                    SkillColors.OctopusEdge, 0.55f * size, 0.24f);
                yield return Wait(0.1f);
                ctx.Fx?.GroundSlam(ground, 1.4f * size, SkillColors.OctopusGlow, SkillColors.Rock, 9);
                ctx.HitCircle(ground + Vector2.up * (0.5f * size), 1.15f * size, finisherDamage,
                    new HitEffect { knockback = new Vector2(3.5f, 4f), heavy = true }, SkillColors.OctopusHeavy);
                yield return Wait(0.24f);
            }

            _lastEnd = Time.time;
        }
    }

    /// <summary>Nút 1 · vuốt sang 2 — Quất xoáy: xoay tròn, cả 3 xúc tu quất 2 vòng quanh người.</summary>
    [Serializable]
    public class SpinLash : Skill
    {
        public float damage = 24f;
        public float radius = 1.9f;

        public SpinLash() => cooldown = 0.6f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float size = ctx.Size, r = radius * size;
            ctx.Act(CombatPose.Spin, 0.6f);
            for (int i = 0; i < 2; i++)
            {
                yield return Wait(i == 0 ? 0.12f : 0.2f);
                // Nhiều vệt lưỡi liềm quấn quanh người ở các bán kính khác nhau
                float start = 90f + i * 180f, turn = -360f * ctx.Facing;
                ctx.Fx?.Slash(ctx.Center, r * 0.85f, start, start + turn, SkillColors.OctopusSlash, SkillColors.OctopusEdge, 0.5f * size, 0.32f, 0.8f);
                ctx.Fx?.Slash(ctx.Center, r * 0.6f, start + 60f, start + 60f + turn * 0.8f, SkillColors.OctopusSlash, SkillColors.OctopusEdge,
                    0.3f * size, 0.28f, 1f, 0.03f);
                ctx.Fx?.Slash(ctx.Center + Vector2.up * (0.1f * size), r, start - 50f, start - 50f + turn * 0.7f, SkillColors.OctopusHeavy,
                    SkillColors.OctopusEdge, SkillScale.Thin(0.26f, size), 0.3f, 0.55f, 0.05f);
                ctx.HitCircle(ctx.Center, r, damage, new HitEffect { knockback = new Vector2(2.5f, 1f) }, SkillColors.Octopus);
            }

            ctx.Fx?.Ring(ctx.Center, r, SkillColors.WithAlpha(SkillColors.Octopus, 0.6f));
            ctx.Fx?.Shockwave(ctx.Ground(0f), r, SkillColors.Octopus);
            yield return Wait(0.2f);
        }
    }

    /// <summary>Nút 1 · vuốt sang 3 — Móc kéo: phóng xúc tu xa móc mục tiêu gần nhất phía trước, giật về sát người.</summary>
    [Serializable]
    public class TentacleHook : Skill
    {
        public float damage = 30f;
        public float range = 6f;
        // Lực giật về (unit/giây) cho mỗi unit mục tiêu đứng xa: giật dư cũng được vì mục tiêu tự dừng ngay trước mặt
        const float PullStrength = 12f;

        public TentacleHook() => cooldown = 2f;

        public override IEnumerator Run(SkillContext ctx)
        {
            // Tầm móc nhân Range, dải lệch dọc tìm mục tiêu và cỡ hiệu ứng nhân Size; chỗ giật về (trước mặt) giữ nguyên
            float size = ctx.Size, reach = range * ctx.Range;
            Health target = ctx.NearestAhead(reach, 1.8f * size);
            Vector2 point = target != null ? SkillContext.Aim(target) : ctx.Center + ctx.Forward * reach;
            ctx.Act(CombatPose.Reach, 0.6f, point); // xúc tu tự kéo dài tới điểm móc (rig giới hạn độ dài tối đa)
            yield return Wait(0.08f);
            ctx.Fx?.Streak(ctx.Center + ctx.Forward * 0.4f, point, SkillColors.OctopusSlash, SkillColors.OctopusEdge, 0.3f * size, 0.22f);
            yield return Wait(0.08f);
            if (target != null && target.IsAlive)
            {
                float distance = Mathf.Abs(target.transform.position.x - ctx.Center.x);
                ctx.Fx?.Flash(point, 1.2f * size, SkillColors.Octopus);
                ctx.Fx?.Ring(point, 0.7f * size, SkillColors.Octopus, 0.25f);
                ctx.Fx?.Streak(point, ctx.Center + ctx.Forward * 0.8f, SkillColors.OctopusHeavy, SkillColors.OctopusEdge, 0.18f * size, 0.3f);
                // Giật mạnh (xa bao nhiêu cũng kéo về tới nơi); mục tiêu tự dừng ngay trước mặt, không chồng lên người
                // (bia tập: TrainingDummy.pullStandoff)
                ctx.HitCircle(point, 0.7f * size, damage,
                    new HitEffect { knockback = new Vector2(-Mathf.Max(0f, distance - 1f) * PullStrength, 1f), stun = 0.6f }, SkillColors.Octopus);
            }

            yield return Wait(0.4f);
        }
    }

    /// <summary>Nút 2 · chạm — Laze mắt: vận sức 1 giây rồi bắn tia xuyên thấu 2 giây.</summary>
    [Serializable]
    public class EyeLaser : LaserBeamSkill
    {
        public EyeLaser()
        {
            cooldown = 5f;
            damagePerSecond = 110f;
            shot = new LaserSkill.LaserShot { charge = 1f, fire = 2f, width = 1f };
        }
    }

    /// <summary>Nút 2 · vuốt sang 1 — Phun mực: phun đám mực trước mặt rồi bật lùi; đứng trong mực bị chậm, mất máu dần.</summary>
    [Serializable]
    public class InkBurst : Skill
    {
        public float damagePerTick = 9f;
        public float radius = 1.8f;
        public float duration = 2.5f;
        [Tooltip("Tốc độ bật lùi ra sau (unit/giây)")]
        public float dash = 8f;

        public InkBurst() => cooldown = 4f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float size = ctx.Size, far = ctx.Range;
            Vector2 at = ctx.Center + ctx.Forward * (1.4f * far);
            Vector2 mouth = ctx.Center + ctx.Forward * 0.5f;
            ctx.Act(CombatPose.Squash, 0.45f);
            ctx.Fx?.Cloud(at, radius * size, SkillColors.Ink, duration, 14);
            // Mực bay tới đám mây xa gấp Range lần trong cùng thời gian: tốc độ và trọng lực giọt nhân Range (giữ dáng vòng cung)
            ctx.Fx?.Spray(mouth, ctx.Forward, SkillColors.Ink, 18, 7f * far, 28f, 0.24f * size, false, 4f * far);
            ctx.Fx?.Spray(mouth, ctx.Forward, SkillColors.InkGlow, 8, 6f * far, 20f, 0.14f * size, true, 4f * far);
            if (ctx.CanMove)
            {
                // Bật lùi xa gấp Range lần (cùng thời gian: tốc độ ngang nhân Range, độ nảy lên giữ nguyên) cho ra khỏi đám mực to
                ctx.Body.linearVelocity = new Vector2(-ctx.Facing * dash * far, 4f);
                ctx.Player.HoldFacing(1.5f); // lùi mà vẫn nhìn về phía trước
            }
            ctx.Combat.Linger(InkZone(ctx, at));
            yield return Wait(0.45f);
        }

        IEnumerator InkZone(SkillContext ctx, Vector2 at)
        {
            float r = radius * ctx.Size;
            for (float t = 0f; t < duration; t += 0.3f)
            {
                ctx.HitCircle(at, r, damagePerTick, new HitEffect { snare = 0.5f, ink = true }, SkillColors.InkHit, false);
                ctx.Fx?.Bubbles(at + Random.insideUnitCircle * r * 0.4f, r * 0.3f, SkillColors.InkGlow, 2); // bọt tím nổi lên cho dễ thấy vùng mực
                yield return Wait(0.3f);
            }
        }
    }

    /// <summary>Nút 2 · vuốt lên 3 — Quét laze: tia ngắn quét từ dưới lên trên trước mặt, trúng cả mục tiêu trên cao.</summary>
    [Serializable]
    public class LaserSweep : LaserBeamSkill
    {
        public LaserSweep()
        {
            cooldown = 4f;
            damagePerSecond = 150f;
            shot = new LaserSkill.LaserShot { charge = 0.35f, fire = 0.7f, width = 0.75f, sweepFrom = -30f, sweepTo = 65f };
        }
    }

    /// <summary>
    /// Nút 3 · chạm — Kraken trỗi dậy: rống lên, xúc tu khổng lồ lần lượt trồi lên khỏi mặt đất phía trước, hất tung và làm
    /// choáng. Mốc nộ càng cao càng nhiều xúc tu, cao hơn, đau hơn.
    /// </summary>
    [Serializable]
    public class KrakenRise : Skill
    {
        public float damage = 45f;
        public float spacing = 1.05f;
        [Tooltip("Số xúc tu ở mốc nộ 1, 2, 3")]
        public int[] tentacles = { 3, 4, 6 };

        public override IEnumerator Run(SkillContext ctx)
        {
            float size = ctx.Size, far = ctx.Range;
            ctx.Act(CombatPose.Rear, 0.75f);
            // Quầng sáng và lửa tím bùng sau lưng lúc rống: hào quang quanh bóng bạch tuộc, không phủ lên người
            ctx.Fx?.Flash(ctx.Center + Vector2.up * 0.8f, 2.4f * size, SkillColors.OctopusHeavy, 0.5f, true);
            ctx.Fx?.Flames(ctx.Ground(0f), 1.9f * size, SkillColors.OctopusGlow, 0.8f, true);
            ctx.Fx?.Cracks(ctx.Ground(0f), 1.3f * size, SkillColors.OctopusHeavy, 1f);
            yield return Wait(0.45f);

            // Hàng xúc tu trải xa gấp Range lần (khoảng cách nhân Range); mỗi xúc tu cao / to gấp Size lần (ảnh xúc tu rộng
            // theo chiều cao)
            int count = tentacles[Mathf.Clamp(ctx.Tier - 1, 0, tentacles.Length - 1)];
            float height = (2.2f + 0.35f * ctx.Tier) * size, width = 1.1f * size;
            for (int i = 0; i < count; i++)
            {
                Vector2 ground = ctx.Ground((1.1f + i * spacing) * far);
                ctx.Fx?.Tentacle(ground, height, 1.1f, -ctx.Facing * 8f + Random.Range(-6f, 6f));
                ctx.Fx?.Eruption(ground, width, SkillColors.OctopusGlow, SkillColors.Rock, 1.1f); // cọc đá, lửa tím, đá văng
                ctx.HitBox(ground + Vector2.up * (height * 0.45f), new Vector2(width, height * 0.9f), damage * ctx.Power,
                    new HitEffect { knockback = new Vector2(0.8f, 7f), stun = 1.2f, heavy = true }, SkillColors.OctopusHeavy);
                yield return Wait(0.11f);
            }

            yield return Wait(0.35f);
        }
    }

    /// <summary>Nút 3 · vuốt về 1 — Xoáy nước: xoay tít tạo xoáy hút mục tiêu vào giữa, đánh liên tục rồi nổ tung hất văng.</summary>
    [Serializable]
    public class Maelstrom : Skill
    {
        public float tickDamage = 10f;
        public float finisherDamage = 55f;
        public float radius = 2.6f;
        public float duration = 1.8f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float size = ctx.Size;
            float r = (radius + 0.4f * ctx.Tier) * size;
            ctx.Act(CombatPose.Spin, duration);
            ctx.Fx?.Vortex(ctx.Center, r, SkillColors.Water, duration + 0.2f);
            for (float t = 0f; t < duration; t += 0.18f)
            {
                ctx.HitCircle(ctx.Center, r, tickDamage * ctx.Power, new HitEffect { knockback = new Vector2(-3.5f, 0.3f) },
                    SkillColors.Octopus, false);
                yield return Wait(0.18f);
            }

            // Nổ tung: vòng sáng, sóng dẹt, nước bắn tung toé
            ctx.Fx?.Ring(ctx.Center, r * 1.2f, SkillColors.Foam, 0.45f);
            ctx.Fx?.Shockwave(ctx.Ground(0f), r * 1.3f, SkillColors.Water, 0.5f);
            ctx.Fx?.Ring(ctx.Center, r * 0.8f, SkillColors.Water, 0.35f);
            ctx.Fx?.Flash(ctx.Center, 3f * size, SkillColors.Water, 0.4f, true); // chớp sau lưng: bóng bạch tuộc nổi trên nền sáng
            // Nước bắn to gấp Size lần: cỡ giọt, tốc độ và trọng lực cùng nhân Size (bắn cao / xa gấp Size lần, cùng thời gian)
            ctx.Fx?.Spray(ctx.Center, Vector2.up, SkillColors.Foam, 30, 10f * size, 85f, 0.28f * size, true, 10f * size);
            ctx.Fx?.Spray(ctx.Center, Vector2.up, SkillColors.Water, 20, 8f * size, 70f, 0.3f * size, false, 10f * size);
            ctx.Fx?.Burst(ctx.Center, SkillColors.Foam, 14, 8f * size, 0.2f * size, 0.45f, 6f * size);
            ctx.HitCircle(ctx.Center, r, finisherDamage * ctx.Power,
                new HitEffect { knockback = new Vector2(6f, 5f), stun = 0.8f, heavy = true }, SkillColors.OctopusHeavy);
            yield return Wait(0.25f);
        }
    }

    /// <summary>Nút 3 · vuốt về 2 — Đại pháo laze: vận sức rồi bắn tia laze khổng lồ xuyên thấu.</summary>
    [Serializable]
    public class TitanBeam : LaserBeamSkill
    {
        public TitanBeam()
        {
            damagePerSecond = 200f;
            scalesWithRage = true;
            shot = new LaserSkill.LaserShot { charge = 0.8f, fire = 1.6f, width = 2.6f };
        }
    }

    // =====================================================================================================================
    // NHỆN
    // =====================================================================================================================

    /// <summary>Nút 1 · chạm — Cào độc: combo 3 đòn (cào chân trước, cào chân kia, cắn), đòn nào cũng tẩm độc.</summary>
    [Serializable]
    public class ClawCombo : Skill
    {
        public float damage = 16f;
        public float biteDamage = 30f;
        public float poisonDps = 6f;
        public float poisonTime = 2.5f;
        public float reach = 1.7f;
        public float comboWindow = 0.7f;

        int _step;
        float _lastEnd = -10f;

        public override IEnumerator Run(SkillContext ctx)
        {
            if (Time.time - _lastEnd > comboWindow)
                _step = 0;
            int step = _step;
            _step = (_step + 1) % 3;
            Vector2 c = ctx.Center;
            float size = ctx.Size, far = ctx.Range;

            if (step < 2)
            {
                bool alt = step == 1;
                ctx.Act(alt ? CombatPose.WhipAlt : CombatPose.Whip, 0.3f, c + ctx.Forward * (reach * far));
                yield return Wait(0.09f);
                // Đòn 1: 3 vạch cào song song theo đường quét của chân; đòn 2: chân kia bổ một vệt lưỡi liềm hồng dày từ dưới lên
                if (alt)
                    ctx.Fx?.Slash(c + ctx.Forward * (0.45f * far), reach * 0.75f * size, ctx.Angle(-65f), ctx.Angle(100f), SkillColors.SpiderSlash,
                        SkillColors.SpiderEdge, 0.62f * size, 0.3f);
                else
                    ctx.Fx?.Claw(c + ctx.Forward * (0.4f * far), reach * 0.65f * size, ctx.Angle(80f), ctx.Angle(-35f), SkillColors.SpiderSlash,
                        SkillColors.SpiderEdge, 0.22f * size, 0.28f, 3, 0.2f * size);
                ctx.HitBox(c + ctx.Forward * (reach * 0.55f * far), new Vector2(reach * 1.1f, 1.3f) * size, damage,
                    new HitEffect { knockback = new Vector2(1.5f, 0.3f), poisonDps = poisonDps, poisonTime = poisonTime }, SkillColors.Spider);
                yield return Wait(0.17f);
            }
            else
            {
                ctx.Act(CombatPose.Lunge, 0.42f, c + ctx.Forward * (1.2f * far));
                yield return Wait(0.14f);
                Vector2 bite = c + ctx.Forward * (1.1f * far);
                // Hai răng nanh khép lại + độc toé
                float fang = 0.5f * size, fangWidth = SkillScale.Thin(0.24f, size);
                ctx.Fx?.Slash(bite, fang, ctx.Angle(125f), ctx.Angle(15f), SkillColors.SpiderSlash, SkillColors.SpiderEdge, fangWidth, 0.2f);
                ctx.Fx?.Slash(bite, fang, ctx.Angle(-125f), ctx.Angle(-15f), SkillColors.SpiderSlash, SkillColors.SpiderEdge, fangWidth, 0.2f);
                ctx.Fx?.Flash(bite, 1f * size, SkillColors.Poison); // nọc độc xanh toé ở miệng
                ctx.Fx?.Splash(bite, SkillColors.Poison, 0.9f * size);
                ctx.HitBox(bite, new Vector2(1.5f, 1.2f) * size, biteDamage,
                    new HitEffect { knockback = new Vector2(3f, 1f), poisonDps = poisonDps * 1.5f, poisonTime = poisonTime, heavy = true },
                    SkillColors.Spider);
                yield return Wait(0.24f);
            }

            _lastEnd = Time.time;
        }
    }

    /// <summary>Nút 1 · vuốt sang 2 — Vồ mồi: bật nhảy vồ tới mục tiêu phía trước, đáp xuống cắn mạnh.</summary>
    [Serializable]
    public class Pounce : Skill
    {
        public float damage = 42f;
        public float range = 5.5f;

        public Pounce() => cooldown = 1.5f;

        public override IEnumerator Run(SkillContext ctx)
        {
            // Tầm tìm con mồi và quãng vồ (khi không có con mồi) nhân Range, cùng thời gian bay (độ cao nhảy giữ nguyên);
            // có con mồi thì vẫn đáp ngay trước nó
            float size = ctx.Size, far = ctx.Range;
            Health target = ctx.NearestAhead(range * far);
            float distance = target != null ? Mathf.Max(0.5f, Mathf.Abs(target.transform.position.x - ctx.Center.x) - 0.8f) : 3.5f * far;
            Vector2 prey = target != null ? SkillContext.Aim(target) : ctx.Center + ctx.Forward * (distance + 0.8f);
            ctx.Act(CombatPose.Reach, 0.6f, prey); // thu mình lấy đà rồi vươn vuốt về phía con mồi suốt cú nhảy
            yield return Wait(0.07f);
            if (ctx.CanMove)
                ctx.Body.linearVelocity = new Vector2(ctx.Facing * distance / 0.55f, 8.5f);
            // bật nhảy: bụi đất, sỏi văng sau chân
            ctx.Fx?.DustCloud(ctx.Ground(0f), 0.8f * size, SkillColors.Dust, 6);
            ctx.Fx?.Debris(ctx.Ground(-0.2f), SkillColors.Rock, 5, 3.5f * size, 0.2f * size, 18f * size);
            yield return Wait(0.18f);
            for (float t = 0f; t < 0.8f && ctx.Player.State != OctopusState.Grounded; t += Time.deltaTime)
            {
                ctx.Fx?.SpeedLines(ctx.Center, ctx.Body.linearVelocity, SkillColors.SpiderSlash); // vệt gió sau lưng lúc bay vồ
                yield return null;
            }

            ctx.Act(CombatPose.Lunge, 0.3f, ctx.Center + ctx.Forward * far);
            Vector2 ground = ctx.Ground(0.6f * far);
            ctx.Fx?.GroundSlam(ground, 1.1f * size, SkillColors.Spider, SkillColors.Rock, 7, 0.8f);
            ctx.Fx?.Claw(ctx.Center + ctx.Forward * (0.7f * far), 0.7f * size, ctx.Angle(110f), ctx.Angle(-30f), SkillColors.SpiderSlash,
                SkillColors.SpiderEdge, 0.16f * size, 0.26f, 3, 0.17f * size);
            ctx.HitCircle(ctx.Center + ctx.Forward * (0.6f * far), 1.4f * size, damage,
                new HitEffect { knockback = new Vector2(3f, 2f), stun = 0.4f, heavy = true }, SkillColors.Spider);
            yield return Wait(0.22f);
        }
    }

    /// <summary>Nút 1 · vuốt sang 3 — Lộn nhào: bật lên lộn một vòng, 8 chân quét quanh người, hất tung mục tiêu.</summary>
    [Serializable]
    public class FlipKick : Skill
    {
        public float damage = 20f;
        public float radius = 1.7f;

        public FlipKick() => cooldown = 0.8f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float size = ctx.Size, r = radius * size; // độ cao bật lên giữ nguyên
            ctx.Act(CombatPose.Spin, 0.5f);
            yield return Wait(0.05f); // co chân lấy đà rồi mới bật lên
            if (ctx.CanMove)
                ctx.Body.linearVelocity = new Vector2(ctx.Body.linearVelocity.x, 7f);
            ctx.Fx?.DustCloud(ctx.Ground(0f), 0.9f * size, SkillColors.Dust, 6);
            for (int i = 0; i < 2; i++)
            {
                yield return Wait(i == 0 ? 0.07f : 0.18f);
                // 8 chân quét: vòng lưỡi liềm hồng quanh người + vết cào ở vòng ngoài
                float start = -90f + i * 180f, turn = 360f * ctx.Facing;
                ctx.Fx?.Slash(ctx.Center, r * 0.75f, start, start + turn, SkillColors.SpiderSlash, SkillColors.SpiderEdge, 0.45f * size, 0.3f, 0.85f);
                ctx.Fx?.Slash(ctx.Center, r * 0.5f, start + 120f, start + 120f + turn * 0.8f, SkillColors.SpiderSlash, SkillColors.SpiderEdge,
                    SkillScale.Thin(0.26f, size), 0.26f, 1f, 0.03f);
                ctx.Fx?.Claw(ctx.Center, r * 0.95f, start + 60f, start + 60f + turn * 0.7f, SkillColors.SpiderSlash, SkillColors.SpiderEdge,
                    0.15f * size, 0.28f, 3, 0.13f * size);
                ctx.HitCircle(ctx.Center, r, damage, new HitEffect { knockback = new Vector2(1f, 6f) }, SkillColors.Spider);
                ctx.Fx?.Ring(ctx.Center, r * 0.9f, SkillColors.WithAlpha(SkillColors.SpiderSlash, 0.7f), 0.3f);
            }

            yield return Wait(0.2f);
        }
    }

    /// <summary>Nút 2 · chạm — Bắn tơ: phóng cục tơ thẳng phía trước, trúng thì trói mục tiêu đứng im.</summary>
    [Serializable]
    public class WebShot : Skill
    {
        public float damage = 28f;
        public float snare = 2.5f;
        public float speed = 15f;
        public float range = 9f;

        public WebShot() => cooldown = 3f;

        public override IEnumerator Run(SkillContext ctx)
        {
            // Cục tơ bay xa gấp Range lần trong cùng thời gian (tốc độ cũng nhân Range), to gấp Size lần
            float size = ctx.Size, far = ctx.Range;
            float hitRadius = 0.5f * size, net = 0.9f * size;
            ctx.Act(CombatPose.Rear, 0.35f);
            yield return Wait(0.12f);
            Vector2 from = ctx.Center + ctx.Forward * 0.5f + Vector2.up * 0.25f; // miệng nhện: gắn với thân, không nhân
            ctx.Fx?.Flash(from, 0.7f * size, SkillColors.Silk, 0.15f);
            ctx.Fx?.Projectile(from, ctx.Forward * (speed * far), range * far, SkillColors.Web, 0.6f * size, position =>
            {
                int hit = ctx.HitCircle(position, hitRadius, damage, new HitEffect { snare = snare }, SkillColors.Web);
                if (hit > 0)
                    ctx.Fx?.WebNet(position, net, SkillColors.Web, 0.6f, null, 0.25f, SkillColors.Silk);
                return hit > 0;
            });
            yield return Wait(0.25f);
        }
    }

    /// <summary>Nút 2 · vuốt sang 1 — Phi tơ: bắn sợi tơ dính vào mục tiêu (hoặc phía trước), kéo mình lao tới đá văng.</summary>
    [Serializable]
    public class WebZip : Skill
    {
        public float damage = 36f;
        public float range = 6.5f;
        public float speed = 20f;

        public WebZip() => cooldown = 3f;

        public override IEnumerator Run(SkillContext ctx)
        {
            // Tầm phi tơ và tốc độ lao nhân Range (lao xa gấp đôi trong cùng thời gian); không có mục tiêu thì điểm bám xa gấp
            // Range lần theo cùng hướng. Sợi tơ, vệt đá, vùng trúng to gấp Size lần
            float size = ctx.Size, far = ctx.Range;
            Health target = ctx.NearestAhead(range * far);
            Vector2 point = target != null ? SkillContext.Aim(target) : ctx.Center + (ctx.Forward * (range * 0.7f) + Vector2.up * 0.4f) * far;
            float gap = 0.2f * size;
            ctx.Fx?.Thread(() => ctx.Center, () => point, SkillColors.Web, 0.05f * size, 0.5f);
            ctx.Fx?.Thread(() => ctx.Center + Vector2.up * gap, () => point + Vector2.down * gap, SkillColors.Web, 0.035f * size, 0.45f); // hai sợi tơ
            ctx.Act(CombatPose.Reach, 0.45f, point);
            yield return Wait(0.12f);

            if (ctx.CanMove)
            {
                Vector2 to = point - ctx.Center;
                ctx.Body.linearVelocity = to.normalized * (speed * far) + Vector2.up * 2f;
                for (float t = 0f; t < 0.35f && Vector2.Distance(ctx.Center, point) > 1.1f; t += Time.deltaTime)
                {
                    ctx.Fx?.SpeedLines(ctx.Center, ctx.Body.linearVelocity, SkillColors.SpiderSlash);
                    yield return null;
                }
                // Quán tính còn lại như ở tầm ×1 (lao nhanh gấp Range lần thì hãm mạnh hơn), để vẫn dừng ngay trước mục tiêu
                ctx.Body.linearVelocity *= 0.3f / Mathf.Max(1f, far);
            }

            ctx.Act(CombatPose.Lunge, 0.3f, point);
            ctx.Fx?.Slash(point - ctx.Forward * (0.3f * size), 0.8f * size, ctx.Angle(100f), ctx.Angle(-40f), SkillColors.SpiderSlash, SkillColors.SpiderEdge,
                0.4f * size, 0.22f);
            ctx.Fx?.Flash(point, 1.3f * size, SkillColors.Spider);
            ctx.Fx?.Ring(point, 0.9f * size, SkillColors.Spider, 0.3f);
            ctx.Fx?.Debris(point, SkillColors.Rock, 6, 5f * size, 0.22f * size, 14f * size, ctx.Forward); // đá vỡ văng khỏi mục tiêu
            ctx.HitCircle(point, 1.1f * size, damage, new HitEffect { knockback = new Vector2(5f, 3f), heavy = true }, SkillColors.Spider);
            yield return Wait(0.2f);
        }
    }

    /// <summary>Nút 2 · vuốt lên 3 — Laze tám mắt: tám con mắt cùng bắn một tia laze.</summary>
    [Serializable]
    public class EightEyeBeam : LaserBeamSkill
    {
        public EightEyeBeam()
        {
            cooldown = 5f;
            damagePerSecond = 120f;
            shot = new LaserSkill.LaserShot { charge = 0.5f, fire = 1.2f, width = 0.8f };
        }
    }

    /// <summary>Nút 3 · chạm — Kén tơ: chồm lên phun lưới tơ tròn quanh mình, cuốn mọi mục tiêu thành kén rồi siết nổ tung.</summary>
    [Serializable]
    public class CocoonBurst : Skill
    {
        public float wrapDamage = 30f;
        public float burstDamage = 75f;
        public float radius = 2.6f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float size = ctx.Size;
            float r = (radius + 0.5f * ctx.Tier) * size;
            ctx.Act(CombatPose.Rear, 0.8f);
            ctx.Fx?.Flames(ctx.Ground(0f), 1.2f * size, SkillColors.SpiderEdge, 0.45f); // khí tím đỏ bùng quanh người lúc chồm lên
            yield return Wait(0.3f);
            ctx.Fx?.Flash(ctx.Center, 1.8f * size, SkillColors.Spider, 0.3f);
            // vòm lưới phát sáng hồng, để trong (nửa đậm) cho vẫn thấy nhện chồm lên giật tơ bên trong
            ctx.Fx?.WebNet(ctx.Center, r, new Color(SkillColors.Web.r, SkillColors.Web.g, SkillColors.Web.b, 0.5f), 1.5f, null, 0.3f,
                new Color(SkillColors.Spider.r, SkillColors.Spider.g, SkillColors.Spider.b, 0.45f));
            ctx.Fx?.Silk(ctx.Center, r, SkillColors.Silk, 16);
            ctx.HitCircle(ctx.Center, r, wrapDamage * ctx.Power, new HitEffect { cocoon = true, stun = 1.6f }, SkillColors.Web);
            yield return Wait(0.92f);
            ctx.Act(CombatPose.Lunge, 0.35f, ctx.Center); // giật người kéo căng tơ siết kén
            yield return Wait(0.08f);
            ctx.Fx?.Ring(ctx.Center, r * 1.15f, SkillColors.Spider, 0.45f);
            ctx.Fx?.Shockwave(ctx.Ground(0f), r * 1.2f, SkillColors.Spider, 0.5f);
            ctx.Fx?.Flash(ctx.Center, 2.4f * size, SkillColors.Spider, 0.35f);
            ctx.Fx?.Silk(ctx.Center, r, SkillColors.Silk, 24); // kén nổ: tơ rách bay tứ tung
            // Tia lửa, đá văng xa / to gấp Size lần (cùng thời gian: tốc độ và trọng lực nhân Size)
            ctx.Fx?.Burst(ctx.Center, SkillColors.Spider, 16, 8f * size, 0.22f * size, 0.5f, 6f * size);
            ctx.Fx?.Debris(ctx.Center, SkillColors.Rock, 8, 6f * size, 0.24f * size, 18f * size);
            ctx.HitCircle(ctx.Center, r, burstDamage * ctx.Power, new HitEffect { knockback = new Vector2(4f, 4f), heavy = true }, SkillColors.Spider);
            yield return Wait(0.3f);
        }
    }

    /// <summary>
    /// Nút 3 · vuốt về 1 — Bầy nhện con: đẻ ra cả bầy nhện con bò lên phía trước, con nào gặp mục tiêu thì cắn tẩm độc.
    /// Mốc nộ càng cao càng nhiều nhện con, cắn càng đau.
    /// </summary>
    [Serializable]
    public class Brood : Skill
    {
        public float biteDamage = 18f;
        public float poisonDps = 6f;
        [Tooltip("Số nhện con ở mốc nộ 1, 2, 3")]
        public int[] counts = { 4, 6, 9 };
        public float range = 8f;
        public float speed = 5f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float size = ctx.Size, far = ctx.Range;
            ctx.Act(CombatPose.Squash, 0.6f);
            // Làn khói độc mỏng sát đất sau lưng nhện mẹ (không che nhện mẹ), vũng độc nhỏ dưới chân
            ctx.Fx?.Cloud(ctx.Ground(-0.25f) + Vector2.up * 0.25f, 0.7f * size, SkillColors.PoisonSmoke, 0.8f, 5, true);
            ctx.Fx?.Splash(ctx.Ground(0.3f) + Vector2.up * 0.05f, SkillColors.Poison, 0.5f * size);
            yield return Wait(0.25f);

            // Nhện con bò xa gấp Range lần trong cùng thời gian (tốc độ nhân Range), to gấp Size lần (vùng cắn theo cỡ nhện con)
            int count = counts[Mathf.Clamp(ctx.Tier - 1, 0, counts.Length - 1)];
            float power = ctx.Power;
            float bite = 0.45f * size, lift = 0.4f * size;
            Vector2 splash = ctx.Forward * (0.35f * size) + Vector2.up * (0.5f * size);
            for (int i = 0; i < count; i++)
            {
                // Từng con chui ra từ dưới bụng mẹ (lệch về phía sau cho kịp thấy cả bầy bò ra), to nhỏ khác nhau
                Vector2 from = ctx.Ground(Random.Range(-0.4f, 0.2f));
                ctx.Fx?.Crawler(from, ctx.Facing, speed * far * Random.Range(0.8f, 1.25f), range * far, position =>
                {
                    int hit = ctx.HitCircle(position + Vector2.up * lift, bite, biteDamage * power,
                        new HitEffect { poisonDps = poisonDps * power, poisonTime = 3f }, SkillColors.Poison);
                    if (hit > 0)
                        ctx.Fx?.Splash(position + splash, SkillColors.Poison, 0.45f * size); // độc toé trên mục tiêu, không che nhện con
                    return hit > 0;
                }, Random.Range(0.85f, 1.05f) * size);
                yield return Wait(0.06f);
            }

            yield return Wait(0.2f);
        }
    }

    /// <summary>Nút 3 · vuốt về 2 — Lưới khổng lồ: phóng tấm lưới tơ khổng lồ chụp cả vùng phía trước, trói lại rồi giật siết.</summary>
    [Serializable]
    public class GiantWeb : Skill
    {
        public float damage = 50f;
        public float pullDamage = 45f;
        public float distance = 3.6f;

        public override IEnumerator Run(SkillContext ctx)
        {
            // Lưới chụp xa gấp Range lần, to gấp Size lần (độ nâng tâm lưới khỏi mặt đất theo cỡ lưới); chỗ giật về giữ nguyên
            float size = ctx.Size, far = ctx.Range;
            float r = (2.4f + 0.4f * ctx.Tier) * size;
            Vector2 at = ctx.Center + ctx.Forward * (distance * far) + Vector2.up * (0.3f * size);
            var area = new Vector2(r * 2f, r * 1.6f);
            ctx.Act(CombatPose.Rear, 0.5f);
            yield return Wait(0.15f);
            ctx.Fx?.WebNet(at, r, SkillColors.Web, 1.8f, ctx.Center, 0.35f, SkillColors.Spider); // lưới khổng lồ trắng phát sáng hồng
            yield return Wait(0.3f);
            ctx.HitBox(at, area, damage * ctx.Power, new HitEffect { snare = 3f }, SkillColors.Web);
            Vector2 anchor = ctx.Center;
            ctx.Fx?.Thread(() => ctx.Center, () => at, SkillColors.Web, 0.06f * size, 1.1f);
            yield return Wait(0.82f);
            ctx.Act(CombatPose.Lunge, 0.3f, anchor);
            yield return Wait(0.08f);
            ctx.Fx?.Streak(at, anchor + ctx.Forward * 0.6f, SkillColors.SpiderSlash, SkillColors.SpiderEdge, 0.3f * size, 0.3f);
            ctx.Fx?.Silk(at, r * 0.6f, SkillColors.Silk, 14);
            ctx.HitBox(at, area, pullDamage * ctx.Power, new HitEffect { knockback = new Vector2(-6f, 1f), heavy = true }, SkillColors.Spider);
            yield return Wait(0.3f);
        }
    }

    /// <summary>Bộ chiêu mặc định (thứ tự: nút 1 chạm, →2, →3; nút 2 chạm, →1, →3; nút 3 chạm, →1, →2).</summary>
    public static class DefaultSkills
    {
        public static Skill[] Octopus() => new Skill[]
        {
            Named(new TentacleCombo(), "Quất xúc tu", "Combo 3 đòn: quất xuống, quất hất lên, nện xúc tu xuống đất"),
            Named(new SpinLash(), "Quất xoáy", "Xoay tròn, cả 3 xúc tu quất 2 vòng quanh người"),
            Named(new TentacleHook(), "Móc kéo", "Phóng xúc tu móc mục tiêu phía trước, giật về sát người, làm choáng"),
            Named(new EyeLaser(), "Laze mắt", "Vận sức 1 giây, bắn tia laze xuyên thấu 2 giây"),
            Named(new InkBurst(), "Phun mực", "Phun mực rồi bật lùi; ai đứng trong mực bị chậm, mất máu dần"),
            Named(new LaserSweep(), "Quét laze", "Tia laze quét từ dưới lên trên, trúng cả mục tiêu trên cao"),
            Named(new KrakenRise(), "Kraken trỗi dậy", "Xúc tu khổng lồ trồi lên khỏi mặt đất, hất tung và làm choáng (3 / 4 / 6 xúc tu theo nộ)"),
            Named(new Maelstrom(), "Xoáy nước", "Xoay tít hút mục tiêu vào giữa xoáy, đánh liên tục rồi nổ tung"),
            Named(new TitanBeam(), "Đại pháo laze", "Tia laze khổng lồ xuyên thấu"),
        };

        public static Skill[] Spider() => new Skill[]
        {
            Named(new ClawCombo(), "Cào độc", "Combo 3 đòn: cào chân trước, cào chân kia, cắn; đòn nào cũng tẩm độc"),
            Named(new Pounce(), "Vồ mồi", "Bật nhảy vồ tới mục tiêu phía trước, đáp xuống cắn mạnh"),
            Named(new FlipKick(), "Lộn nhào", "Bật lên lộn một vòng, 8 chân quét quanh người, hất tung mục tiêu"),
            Named(new WebShot(), "Bắn tơ", "Phóng cục tơ, trúng thì trói mục tiêu đứng im"),
            Named(new WebZip(), "Phi tơ", "Bắn sợi tơ vào mục tiêu, kéo mình lao tới đá văng"),
            Named(new EightEyeBeam(), "Laze tám mắt", "Tám con mắt cùng bắn một tia laze"),
            Named(new CocoonBurst(), "Kén tơ", "Lưới tơ tròn cuốn mọi mục tiêu quanh mình thành kén rồi siết nổ tung"),
            Named(new Brood(), "Bầy nhện con", "Đẻ ra bầy nhện con bò lên cắn độc (4 / 6 / 9 con theo nộ)"),
            Named(new GiantWeb(), "Lưới khổng lồ", "Tấm lưới tơ khổng lồ chụp cả vùng phía trước, trói rồi giật siết"),
        };

        static Skill Named(Skill skill, string name, string description)
        {
            skill.name = name;
            skill.description = description;
            return skill;
        }
    }
}
