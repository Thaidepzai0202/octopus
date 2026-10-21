using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Octopus
{
    // =====================================================================================================================
    // PINK (trượng pha lê tím) — bộ 9 chiêu, làm đúng theo các tấm dáng chiêu Docs/nhan_vat_moi/pink/{basic_attack, skill,
    // special_skill} (mỗi tấm 6–7 khung). Thứ tự trong mảng: nút 1 thường / ↑ / ↓, nút 2 thường / ↑ / ↓, nút 3 thường / ↑ / ↓
    // (↑ / ↓ = kéo cần điều hướng lên / xuống khi bấm nút). Nút 3 cần nộ qua mốc 1. Tên chiêu = tên tấm tranh.
    // Hiệu ứng: ảnh cắt từ chính tấm tranh của TỪNG chiêu, tách lớp (lõi / thân / đuôi / mảnh vỡ / đá / nửa trước – nửa sau
    // quanh người) và diễn theo pha của tranh (Combat/Heroes/PinkFx.cs, Tools/art/hero_fx_pink.py); CombatFx chỉ còn phần nền
    // (sóng mặt đất, nứt đất, chớp nhỏ, tia lửa). Vệt chém / vòng phép neo vào mũi trượng thật (ctx.WeaponTip lấy mẫu suốt lúc
    // vung), hiệu ứng mặt đất đặt ngay dưới mũi trượng. Ảnh hiệu ứng KHÔNG to bằng cỡ chiêu: cỡ ảnh = max(1, Size × fxScale)
    // (field fxScale của từng chiêu, mặc định 0.65–0.8) — vùng đánh trúng vẫn ×Size, ảnh trôi / lan ra phủ vùng đó mà không
    // che lấp nhân vật. Dáng: HeroRigAnimator (trượng): Whip = kéo trượng ra sau rồi
    // đâm 2 tay (nhắm cao: hất trượng lên), Reach = chĩa trượng phóng phép, Squash = cắm trượng, Lunge = lao thấp chĩa trượng
    // rồi quét, Spin = xoay người, Slam = giơ trượng rồi dộng xuống, Rear = giơ trượng quá đầu.
    // Màn chơi dựng lại (menu Octopus → Dựng lại màn chơi) thì CombatBuilder điền PinkSkills.Default() vào Combat Controller.
    // ---------------------------------------------------------------------------------------------------------------------
    // API CHO CHIÊU NHÂN VẬT NGƯỜI — mọi thứ đi qua SkillContext ctx (Combat/Skill.cs), không cần sửa file dùng chung nào.
    //  Viết chiêu: class [Serializable] kế thừa Skill, override IEnumerator Run(SkillContext ctx); field public chỉnh được trong
    //    Inspector của Combat Controller; field cooldown = hồi chiêu (giây). Đặt tên class bắt đầu bằng tên nhân vật (Red…,
    //    Pink…, Blue…) để không trùng file khác. Chờ: yield return Wait(giây). Việc kéo dài sau khi chiêu xong (vật bay, hộ vệ,
    //    vùng lửa): ctx.Combat.Linger(coroutine).
    //  Kiểu chiêu: ctx.Variant (0 thường, 1 ↑, 2 ↓), ctx.Direction (0 / 1 / -1), ctx.Slot (0..2), ctx.Grounded (đứng trên
    //    sàn / mặt đá hay đang trên không). Chiêu cuối (nút 3): ctx.Tier (mốc nộ 1..3), ctx.Power (hệ số sát thương theo mốc).
    //  Vị trí: ctx.Center (tâm thân = bàn chân + 0.5), ctx.Chest (ngực), ctx.Facing (1 / -1), ctx.Forward, ctx.Ground(dx)
    //    (mặt đất trước mặt dx), ctx.Angle(độ) (0 = trước mặt, 90 = thẳng lên), ctx.WeaponTip, ctx.CastPoint.
    //  Dáng: ctx.Act(CombatPose, giây, điểm nhắm) — Whip / WhipAlt / Slam / Spin / Reach / Rear / Lunge / Squash (ý nghĩa
    //    từng dáng: Heroes/IHeroVisual.cs). Hình (ctx.HeroVisual): SetGhost(0..1), SetWeaponPower(0..1), Flash, VisualBounds.
    //  Đánh: ctx.HitBox / HitCircle / HitSegment(…, sát thương, HitEffect, màu) → số mục tiêu trúng; ctx.LastDealt = tổng sát
    //    thương vừa gây (hút máu). HitEffect: knockback (x = ra xa, âm = kéo lại), stun, snare, poisonDps/Time, heavy.
    //    Tìm mục tiêu: ctx.NearestAhead(tầm), ctx.Enemies(bán kính), SkillContext.Aim(health); Health.Current / Max.
    //  Hiệu ứng: ctx.Fx (CombatFx): Slash, Claw, Streak, Burst, SpeedLines, Ring, Shockwave, Flash, GroundSlam, Cracks,
    //    DustCloud, Spikes, Flames, Cloud, Spray, Thread…
    //  Thân (ctx.Hero, HeroMotor): Launch(vận tốc) (hất mình lên / lao xuống), JumpTo(độ cao), Dash(hướng, quãng, giây,
    //    hop, trail, màu) + IsDashing / StopDash(), TeleportTo(chỗ bàn chân) → chỗ đứng thật, Hover(giây) (lơ lửng đánh trên
    //    không), Root(giây) (đứng im), SetGravityScale(hệ số, giây), BeginFlight(giây, tốc độ) / EndFlight() (bay tự do theo
    //    cần, xuyên đá), RefillAirMoves(), Velocity, Feet, Grounded, OnRock, RockMask (lớp đá), sự kiện Landed / Jumped.
    //  Máu người chơi (chỉ có ở màn đánh boss; màn hang động thì các hàm này không làm gì): ctx.Heal(lượng) → lượng hồi thật;
    //    ctx.BlockFor(giây, hit => …) → chặn mọi đòn trong lúc đó, mỗi đòn bị chặn gọi lại kèm HitInfo (chiêu đỡ đòn dùng để biết
    //    vừa bị đánh); trả về khiên, Cancel() để tắt sớm. ctx.Health (PlayerHealth): IsBlocking, Healed, Blocked.
    //  Đóng băng (ngưng đọng thời gian): ctx.Freeze(health, giây), ctx.FreezeAll(giây) → danh sách bị đóng băng (bia tập, boss,
    //    cả cầu lửa / thiên thạch / lửa của boss dừng lại). Health.IsFrozen, FrozenChanged, Damaged để dồn sát thương rồi nổ.
    //  Trạng thái dùng chung giữa các chiêu (vd chiêu cường hoá làm đánh thường mạnh hơn): ctx.SetBuff("Tên", giây, giá trị),
    //    ctx.Buff("Tên") (0 khi hết), ctx.HasBuff("Tên").
    // ---------------------------------------------------------------------------------------------------------------------
    //  Cỡ / tầm chiêu (ctx.Size, ctx.Range — ô "Cỡ / tầm chiêu" của Combat Controller, mặc định ×2): mỗi chiêu đọc một lần ở
    //    đầu Run. Size nhân mọi vùng đánh trúng (bán kính, khung, độ dày) và cỡ hiệu ứng (ảnh PinkFx, vệt chém, chớp, sóng…);
    //    Range nhân mọi khoảng cách đánh tới trước mặt (tâm vùng đánh, tầm tìm mục tiêu, quãng lướt — cùng thời gian lướt —,
    //    quãng bay và tốc độ bay của rồng / tia pha lê). Không nhân: vị trí thứ đặt trên người mục tiêu (lồng, xích quấn, chỗ
    //    nổ), sát thương, hồi chiêu, thời gian, độ cao bật lên / hất lên, thời gian choáng. Số trong Inspector = cỡ / tầm ở ×1.
    //    Ảnh hiệu ứng: PinkSkills.FxSize(ctx, fxScale) — to chậm hơn vùng đánh (×2 → ảnh ×1.3–1.6), xem fxScale từng chiêu.
    // =====================================================================================================================

    public static class PinkSkills
    {
        /// <summary>Màu chủ đạo: tím pha lê (số sát thương, thân vệt chém).</summary>
        public static readonly Color Color = new Color(0.78f, 0.42f, 1f);
        /// <summary>Mép trong vệt chém: tím sẫm.</summary>
        public static readonly Color Edge = new Color(0.42f, 0.12f, 0.85f);
        /// <summary>Trắng tím: vòng sáng, chữ cổ, lõi chớp.</summary>
        public static readonly Color Pale = new Color(0.93f, 0.82f, 1f);
        /// <summary>Đòn nặng / chiêu cuối: hồng tím đậm hơn.</summary>
        public static readonly Color Heavy = new Color(0.95f, 0.5f, 1f);
        /// <summary>Vòng sáng xoắn quanh người (đánh thường thủ, dùng chiêu thủ, kĩ năng đặc biệt): tím hồng rực.</summary>
        public static readonly Color Ring = new Color(0.82f, 0.3f, 1f);

        public static Skill[] Default() => new Skill[]
        {
            // nút 1 · thường / ↑ / ↓
            new PinkStaffCombo(),
            new PinkRisingCrescent(),
            new PinkCrystalGuard(),
            // nút 2 · thường / ↑ / ↓
            new PinkDashSweep(),
            new PinkDragonBolt(),
            new PinkCrystalWard(),
            // nút 3 · thường / ↑ / ↓ (cần nộ)
            new PinkCrystalNova(),
            new PinkStarLance(),
            new PinkChainSeal(),
        };

        /// <summary>Chỗ giữa hai bàn chân PINK (world).</summary>
        public static Vector2 Feet(SkillContext ctx) => ctx.Hero != null ? ctx.Hero.Feet : ctx.Center + Vector2.down * 0.5f;

        /// <summary>Chỗ trên mặt đất ngay dưới mũi trượng (x mũi trượng, y chân): gốc của hiệu ứng mọc từ đất theo đường trượng.</summary>
        public static Vector2 UnderTip(SkillContext ctx) => new Vector2(ctx.WeaponTip.x, Feet(ctx).y);

        /// <summary>
        /// Cỡ ảnh hiệu ứng theo cỡ chiêu: vùng đánh trúng to ×ctx.Size, còn ảnh chỉ to max(1, Size × scale) — ở ×2 hiệu ứng
        /// vẫn lớn hơn ×1 nhưng không vùi lấp nhân vật (ảnh trôi / lan ra để phủ vùng đánh). scale ≤ 0 (dữ liệu cũ chưa có
        /// field) → fallback. Cỡ chiêu &lt; 1 thì ảnh nhỏ theo đúng cỡ chiêu.
        /// </summary>
        public static float FxSize(SkillContext ctx, float scale, float fallback)
        {
            float s = ctx.Size;
            float k = scale > 0f ? scale : fallback;
            return s <= 1f ? s : Mathf.Max(1f, s * k);
        }

        /// <summary>Chân (giữa đáy) và khung thân của mục tiêu: theo collider trúng đòn (bia tập, thân boss).</summary>
        public static Bounds BodyOf(Health target)
        {
            var box = target.GetComponent<Collider2D>();
            if (box != null && box.enabled)
                return box.bounds;
            return new Bounds(target.transform.position + Vector3.up * 0.95f, new Vector3(1f, 1.9f, 0f));
        }

        public static Vector2 FeetOf(Health target)
        {
            Bounds b = BodyOf(target);
            return new Vector2(b.center.x, b.min.y);
        }

        /// <summary>Chờ tới phần k (0..1) của một dáng dài duration giây, tính từ lúc bắt đầu dáng (đã chờ waited giây).</summary>
        internal static float Until(float duration, float k, float waited = 0f) => Mathf.Max(0f, duration * k - waited);

        /// <summary>
        /// point dời theo tầm chiêu: khoảng cách ngang từ tâm thân PINK tới point nhân với range, giữ độ cao (vệt trăng / chớp
        /// sao ở mũi trượng hiện xa ra cùng vùng đánh trúng).
        /// </summary>
        internal static Vector2 Ahead(SkillContext ctx, Vector2 point, float range) =>
            new Vector2(ctx.Center.x + (point.x - ctx.Center.x) * range, point.y);

        /// <summary>
        /// Khung đánh trúng trước mặt: tâm cách thân dx (đã nhân tầm), rộng × cao size (đã nhân cỡ), đáy khung ở bottom so với
        /// tâm thân — khung to ra thì cao lên từ mặt đất, không lún xuống dưới sàn.
        /// </summary>
        internal static Vector2 BoxCenter(SkillContext ctx, float dx, Vector2 size, float bottom) =>
            ctx.Center + new Vector2(ctx.Facing * dx, bottom + size.y * 0.5f);
    }

    // =====================================================================================================================
    // Nút 1 — đánh thường
    // =====================================================================================================================

    /// <summary>
    /// Nút 1 · thường — "Đánh thường" (basic_attack/đánh thường.png): combo 3 đòn khi bấm liên tiếp. Đòn 1, 2: kéo trượng ra
    /// sau rồi đâm tới, vệt trăng tím ở mũi trượng diễn 2 pha như tranh — K3 (nhỏ) lúc trượng sắp tới, K4 (lớn + lõi trắng)
    /// đúng lúc trúng — bám mũi trượng thật rồi trôi tới. Đòn 3: đâm trượng ngắn, chớp sao K5 ở mũi trượng + cụm mảnh pha lê
    /// toả ra (lớp riêng), đẩy văng.
    /// </summary>
    [Serializable]
    public class PinkStaffCombo : Skill
    {
        public float damage = 18f;
        public float secondDamage = 20f;
        [Tooltip("Đòn 3: đâm trượng")]
        public float thrustDamage = 32f;
        [Tooltip("Tầm vệt trăng trước mặt (unit, tính từ tâm thân; ×1)")]
        public float reach = 2.4f;
        [Tooltip("Đòn 3: bán kính chớp sao ở mũi trượng (×1)")]
        public float thrustRadius = 0.9f;
        [Tooltip("Bấm lại trong bấy nhiêu giây sau đòn trước thì ra đòn tiếp của combo")]
        public float comboWindow = 0.7f;
        [Tooltip("Ảnh hiệu ứng to theo cỡ chiêu: cỡ ảnh = max(1, cỡ chiêu × số này); vùng đánh vẫn to đúng cỡ chiêu (≤ 0: 0.75)")]
        public float fxScale = 0.75f;

        int _step;
        float _lastEnd = -10f;

        public PinkStaffCombo()
        {
            name = "Đánh thường";
            description = "Combo 3 đòn: 2 lần đâm trượng bung vệt trăng tím, đòn 3 đâm trượng nổ chớp sao.";
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size, rg = ctx.Range;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.75f);
            if (Time.time - _lastEnd > comboWindow)
                _step = 0;
            int step = _step;
            _step = (_step + 1) % 3;
            float f = ctx.Facing;

            if (step < 2)
            {
                // Khung 2 → 3 / 4: kéo trượng 2 tay ra sau → đâm thẳng tới (mốc đâm 0.44 của dáng Whip trượng)
                bool big = step == 1;
                float duration = big ? 0.42f : 0.38f;
                ctx.Act(CombatPose.Whip, duration, ctx.Chest + ctx.Forward * (reach * rg));
                // Pha K3 hiện từ lúc mũi trượng qua hướng nhắm (StrikeAt), pha K4 thay vào đúng lúc trúng (0.44) — cùng gốc ở
                // mũi trượng thật, lấy mẫu mỗi frame nên vệt trăng đi theo đường trượng, không bị đẩy ra xa khi tầm ×2
                float lead = duration * Mathf.Max(0.02f, 0.44f - HeroRigAnimator.StrikeAt(CombatPose.Whip));
                yield return Wait(duration * 0.44f - lead);
                PinkFx.Instance.Crescent(() => ctx.WeaponTip, f, lead, fx, big ? 1f : 0.82f);
                yield return Wait(lead);
                var box = new Vector2(reach * 1.1f, 2f) * s;
                ctx.HitBox(PinkSkills.BoxCenter(ctx, reach * 0.55f * rg, box, -0.6f), box, big ? secondDamage : damage,
                    new HitEffect { knockback = new Vector2(big ? 2.5f : 1.8f, 0.4f) }, PinkSkills.Color);
                yield return Wait(duration * 0.56f * 0.85f); // nhả sớm một chút cho combo nối mượt
            }
            else
            {
                // Khung 5: đâm trượng ngắn — chớp sao ở mũi trượng thật, cụm mảnh pha lê toả ra trôi tới tâm vùng đánh (vùng
                // đánh dời ra theo tầm, to theo cỡ)
                const float duration = 0.5f;
                ctx.Act(CombatPose.Reach, duration, ctx.Chest + ctx.Forward * (2f * rg));
                yield return Wait(duration * 0.42f);
                Vector2 real = ctx.WeaponTip + ctx.Forward * 0.15f;
                Vector2 tip = PinkSkills.Ahead(ctx, real, rg);
                PinkFx.Instance.Sparkle(() => ctx.WeaponTip + ctx.Forward * 0.15f, f, fx, Mathf.Abs(tip.x - real.x));
                ctx.Fx?.Flash(real, 0.8f * fx, PinkSkills.Color, 0.18f);
                ctx.HitCircle(tip, thrustRadius * s, thrustDamage, new HitEffect { knockback = new Vector2(4.5f, 1.5f), heavy = true }, PinkSkills.Heavy);
                yield return Wait(duration * 0.58f * 0.8f);
            }

            _lastEnd = Time.time;
        }
    }

    /// <summary>
    /// Nút 1 · ↑ — "Đánh thường hất tung" (basic_attack/Đánh thường hất tung.png): khom thấp, pha lê quệt sát đất (khung 1 → 2),
    /// vung trượng hất lên: cung nhỏ K3 → cung lớn K4 dựng đứng mọc từ mặt đất ngay dưới mũi trượng (+ lõi trắng, pha lê toé
    /// trên đất trước chân), pha lê bay vút lên (khung 5). Hất mục tiêu bay lên.
    /// </summary>
    [Serializable]
    public class PinkRisingCrescent : Skill
    {
        public float damage = 24f;
        [Tooltip("Hất mục tiêu lên (unit/giây)")]
        public float launch = 10f;
        [Tooltip("Vùng trúng trước mặt: rộng × cao (unit, ×1)")]
        public Vector2 area = new Vector2(2.2f, 3.4f);
        [Tooltip("Ảnh hiệu ứng to theo cỡ chiêu: cỡ ảnh = max(1, cỡ chiêu × số này); vùng đánh vẫn to đúng cỡ chiêu (≤ 0: 0.6)")]
        public float fxScale = 0.6f;

        public PinkRisingCrescent()
        {
            name = "Đánh thường hất tung";
            description = "Khom thấp vung trượng hất lên thành vệt trăng dựng đứng, pha lê trồi khỏi đất bay vút lên, hất tung kẻ địch.";
            cooldown = 0.8f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size, rg = ctx.Range;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.6f);
            float f = ctx.Facing;
            const float duration = 0.55f;
            // nhắm cao (> 28°) → dáng Whip của trượng là hất từ dưới lên
            ctx.Act(CombatPose.Whip, duration, ctx.Chest + new Vector2(f * 0.8f, 2.6f));
            yield return Wait(duration * 0.2f);
            // Khung 2: pha lê đầu trượng quệt sát đất, lửa tím liếm quanh
            ctx.Fx?.Flames(PinkSkills.UnderTip(ctx), 0.7f * fx, PinkSkills.Color, 0.25f);
            yield return Wait(duration * 0.24f);

            // Khung 3 → 4: hất trượng lên — cung nhỏ rồi cung lớn dựng đứng mọc từ đất ngay dưới mũi trượng, lõi trắng, pha lê
            // toé trên đất; nứt đất phủ tới giữa vùng đánh
            // gốc cung: dưới mũi trượng, nhưng trượng hất lên quá đầu thì mũi trượng ở ngay trên người — đẩy gốc ra trước ít nhất
            // như trong tranh (mũi trượng trước thân ~0.5 unit) để cung mọc trước mặt, không đè lên PINK
            Vector2 feet = PinkSkills.Feet(ctx);
            Vector2 root = PinkSkills.UnderTip(ctx);
            float ahead = (root.x - feet.x) * f;
            float least = 0.75f * fx;
            if (ahead < least)
                root.x = feet.x + f * least;
            PinkFx.Instance.RiseSlash(root, f, fx);
            ctx.Fx?.Cracks(ctx.Ground(1.1f * rg), 0.55f * s, PinkSkills.Color, 0.5f);
            Vector2 box = area * s;
            ctx.HitBox(PinkSkills.BoxCenter(ctx, area.x * 0.5f * rg, box, -0.6f), box, damage,
                new HitEffect { knockback = new Vector2(1.2f, launch), heavy = true }, PinkSkills.Color);
            yield return Wait(0.12f);

            // Khung 5: pha lê bay vút lên trước mặt (+ viên rời bay cao)
            PinkFx.Instance.RiseCrystals(feet, f, fx);
            yield return Wait(Mathf.Max(0f, duration * 0.56f - 0.12f));
        }
    }

    /// <summary>
    /// Nút 1 · ↓ — "Đánh thường thủ" (basic_attack/Đánh thường thủ.png): dựng trượng, vòng chữ xoắn quanh người (khung 2 —
    /// nửa trước trước người, nửa sau sau lưng), cắm trượng xuống đất, tia sáng toé (khung 3), gai pha lê trồi lên quanh mình
    /// ra tới mép vùng đánh (khung 4 → 5: cụm sau lưng, cụm trước, chân gai thấp trước chân, gai rời). Chặn sát thương trong
    /// lúc thủ thế; kẻ địch đứng sát bị đánh và đẩy ra.
    /// </summary>
    [Serializable]
    public class PinkCrystalGuard : Skill
    {
        public float damage = 20f;
        [Tooltip("Bán kính gai pha lê quanh người (unit, ×1)")]
        public float radius = 1.9f;
        [Tooltip("Đẩy kẻ địch ra (unit/giây)")]
        public float push = 5f;
        [Tooltip("Cả thế thủ dài bấy nhiêu giây (chặn sát thương suốt lúc đó)")]
        public float duration = 0.8f;
        [Tooltip("Ảnh hiệu ứng to theo cỡ chiêu: cỡ ảnh = max(1, cỡ chiêu × số này); gai rời vẫn lan tới mép vùng đánh (≤ 0: 0.7)")]
        public float fxScale = 0.7f;

        public PinkCrystalGuard()
        {
            name = "Đánh thường thủ";
            description = "Dựng trượng, vòng sáng xoắn quanh người, cắm trượng làm gai pha lê trồi lên quanh mình: chặn đòn, đẩy kẻ địch ra.";
            cooldown = 1.5f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.7f);
            float f = ctx.Facing;
            if (ctx.Grounded)
                ctx.Hero?.Root(duration);
            ctx.BlockFor(duration, hit => ctx.Fx?.Flash(ctx.Chest, 1.2f * fx, PinkSkills.Pale, 0.2f));
            ctx.Act(CombatPose.Squash, duration, ctx.Ground(0.6f));

            // Khung 2: vòng chữ xoắn quanh người, từ dưới lên — nửa trước trước người, nửa sau sau lưng
            PinkFx.Instance.GuardRings(() => ctx.Center, f, 3, duration * 0.42f, fx);

            yield return Wait(duration * 0.3f);

            // Khung 3 → 4: cắm trượng — tia sáng toé ở đầu nhọn, gai pha lê trồi lên quanh mình tới mép vùng đánh (bán kính × cỡ)
            Vector2 feet = PinkSkills.Feet(ctx);
            float r = radius * s;
            PinkFx.Instance.GuardRay(PinkSkills.UnderTip(ctx), f, fx);
            ctx.Fx?.Cracks(feet, 0.8f * s, PinkSkills.Color, 0.6f);
            ctx.Fx?.Shockwave(feet, r * 0.8f, PinkSkills.Color, 0.35f);
            PinkFx.Instance.Spikes(feet, f, duration * 0.7f, fx, r);
            ctx.HitCircle(ctx.Center + Vector2.up * 0.2f, r, damage, new HitEffect { knockback = new Vector2(push, 2.5f), heavy = true },
                PinkSkills.Color);
            yield return Wait(duration * 0.7f);
        }
    }

    // =====================================================================================================================
    // Nút 2 — dùng chiêu
    // =====================================================================================================================

    /// <summary>
    /// Nút 2 · thường — "Dùng chiêu áp sát" (skill/dùng chiêu áp sát.png): khom người lấy đà (khung 1), lướt tới để lại bóng
    /// mờ cả dáng lao và vệt tốc độ dài / ngắn (khung 2 → 3), quét trượng: cung lưỡi liềm + dải quét sát đất ngay dưới mũi
    /// trượng (khung 4), trượt dừng tung bụi trước / sau chân (khung 5). Kẻ địch trên đường lướt bị đánh; nhát quét đánh mạnh
    /// phía trước.
    /// </summary>
    [Serializable]
    public class PinkDashSweep : Skill
    {
        [Tooltip("Sát thương kẻ địch trên đường lướt")]
        public float pathDamage = 18f;
        [Tooltip("Sát thương nhát quét cuối")]
        public float sweepDamage = 26f;
        [Tooltip("Quãng lướt (unit, ×1; tầm chiêu nhân quãng, thời gian lướt giữ nguyên)")]
        public float distance = 3.6f;
        [Tooltip("Lướt hết bấy nhiêu giây")]
        public float dashTime = 0.22f;
        [Tooltip("Ảnh hiệu ứng to theo cỡ chiêu: cỡ ảnh = max(1, cỡ chiêu × số này); vùng đánh vẫn to đúng cỡ chiêu (≤ 0: 0.75)")]
        public float fxScale = 0.75f;

        public PinkDashSweep()
        {
            name = "Dùng chiêu áp sát";
            description = "Lướt tới để lại bóng mờ, đánh kẻ địch trên đường, quét trượng lưỡi liềm rồi trượt dừng tung bụi.";
            cooldown = 3f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size, rg = ctx.Range;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.75f);
            float f = ctx.Facing;
            const float duration = 0.78f;
            ctx.Act(CombatPose.Lunge, duration, ctx.Chest + ctx.Forward * (3f * rg));
            yield return Wait(0.1f);

            // Khung 2 → 3: lướt tới, bóng mờ + vệt tốc độ sau lưng. Kẻ địch cỡ người thì lướt xuyên qua (đánh trên đường); kẻ
            // địch khổng lồ (sư tử) thì dừng ngay trước thân nó để quét, không lướt chui vào trong. Quãng lướt nhân tầm chiêu,
            // thời gian lướt giữ nguyên (lướt nhanh hơn)
            Vector2 start = ctx.Center;
            float dist = distance * rg;
            float reach = dist, time = dashTime;
            Health wall = ctx.NearestAhead(dist + 1.5f, 3f);
            if (wall != null)
            {
                Bounds b = PinkSkills.BodyOf(wall);
                if (b.size.y > 3f)
                {
                    float edge = f > 0f ? b.min.x - start.x : start.x - b.max.x;
                    reach = Mathf.Clamp(edge - 0.7f, 0.3f, dist);
                    time = Mathf.Max(0.08f, dashTime * reach / dist);
                }
            }

            bool dash = ctx.Hero != null && ctx.CanMove;
            if (dash)
                ctx.Hero.Dash(ctx.Forward, reach, time);
            else if (ctx.CanMove)
                ctx.Body.linearVelocity = ctx.Forward * (reach / time);
            // lướt xa hơn trong cùng thời gian: bóng mờ dày hơn cho đều khoảng cách giữa các bóng; mỗi bóng kèm vệt tốc độ
            // dài sát chân + ngắn ngang ngực (cắt từ khung 2) đọng lại sau lưng
            float ghostEvery = 0.08f / Mathf.Max(1f, rg);
            float ghost = 0f;
            for (float t = 0f; t < time + 0.05f; t += Time.deltaTime)
            {
                ghost -= Time.deltaTime;
                if (ghost <= 0f)
                {
                    ghost = ghostEvery;
                    PinkFx.Instance.Afterimage(PinkSkills.Feet(ctx), f, 0.5f);
                    PinkFx.Instance.Streaks(PinkSkills.Feet(ctx), ctx.Chest, f, fx);
                }

                yield return null;
                if (dash && !ctx.Hero.IsDashing && t > 0.03f)
                    break;
            }

            // Hết lướt thân còn trượt theo đà (0.3 × vận tốc lướt). Tầm ×2 lướt nhanh gấp đôi thì đà trượt xa gấp 4 (trượt khỏi
            // mép bệ): giữ đà trượt như ở ×1
            if (dash && !ctx.Hero.IsDashing && rg > 1f)
            {
                Vector2 v = ctx.Body.linearVelocity;
                ctx.Body.linearVelocity = new Vector2(v.x / rg, v.y);
            }

            Vector2 end = ctx.Center;
            ctx.HitSegment(start, end + ctx.Forward * (0.4f * rg), 0.85f * s, pathDamage, new HitEffect { knockback = new Vector2(1.5f, 1f) },
                PinkSkills.Color);

            // Khung 4: quét trượng — cung lưỡi liềm + dải quét sát đất mọc ngay dưới mũi trượng (dải kéo về sau qua chân)
            Vector2 feet = PinkSkills.Feet(ctx);
            PinkFx.Instance.Sweep(PinkSkills.UnderTip(ctx), f, fx);
            var box = new Vector2(2.5f, 1.5f) * s;
            ctx.HitBox(PinkSkills.BoxCenter(ctx, 1.25f * rg, box, -0.8f), box, sweepDamage,
                new HitEffect { knockback = new Vector2(4f, 2f), heavy = true }, PinkSkills.Heavy);
            yield return Wait(0.08f);

            // Khung 5: trượt dừng tung bụi (ảnh bụi mép mờ elip — không còn khung vuông — nên to theo cỡ ảnh), đá vụn
            feet = PinkSkills.Feet(ctx);
            PinkFx.Instance.Dust(feet, f, fx);
            ctx.Fx?.DustCloud(feet, 0.7f * fx, PinkFx.DustColor, 4);
            ctx.Fx?.Debris(feet + ctx.Forward * (0.3f * rg), PinkFx.Rock, 4, 3f, 0.18f * fx);
            yield return Wait(Mathf.Max(0.1f, duration - 0.1f - time - 0.08f));
        }
    }

    /// <summary>
    /// Nút 2 · ↑ — "Dùng chiêu tầm xa" (skill/dùng chiêu tầm xa.png): rút trượng về, vòng phép bung trước trượng (K2), vòng
    /// nghiêng đi + mũi pha lê nhọn ở mũi trượng (K3), rồng pha lê bay (K4: vòng đọng ở trượng, đầu rồng đủ cỡ ngay từ đầu,
    /// thân + lõi kéo dài từ trượng tới đầu), nổ quầng + đá vụn (lớp riêng) ở chỗ trúng; đầu + thân rồng đọng lại một nhịp
    /// cùng vụ nổ nên luôn thấy rõ rồng kể cả khi trúng mục tiêu gần.
    /// </summary>
    [Serializable]
    public class PinkDragonBolt : Skill
    {
        public float damage = 34f;
        [Tooltip("Tốc độ bay (unit/giây, ×1; nhân tầm chiêu cùng quãng bay để thời gian bay giữ nguyên)")]
        public float speed = 12f;
        [Tooltip("Quãng bay tối đa (unit, ×1)")]
        public float range = 9f;
        [Tooltip("Bán kính đầu rồng chạm mục tiêu (unit, ×1)")]
        public float radius = 0.8f;
        [Tooltip("Ảnh hiệu ứng to theo cỡ chiêu: cỡ ảnh = max(1, cỡ chiêu × số này); vùng đánh vẫn to đúng cỡ chiêu (≤ 0: 0.75)")]
        public float fxScale = 0.75f;

        public PinkDragonBolt()
        {
            name = "Dùng chiêu tầm xa";
            description = "Vòng phép bung trước trượng, phóng luồng pha lê tím hình rồng bay xa, nổ đá vụn ở chỗ trúng.";
            cooldown = 3.5f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size, rg = ctx.Range;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.75f);
            float f = ctx.Facing;
            const float duration = 0.85f;
            ctx.Act(CombatPose.Reach, duration, ctx.Chest + ctx.Forward * (6f * rg));
            ctx.HeroVisual?.SetWeaponPower(0.8f);
            yield return Wait(0.05f);

            // Khung 2: vòng phép trước mũi trượng (bám mũi trượng), cuối pha bóp dẹt → khung 3: vòng nghiêng + mũi pha lê
            float lead = 0.35f * fx;
            Func<Vector2> front = () => ctx.WeaponTip + ctx.Forward * lead;
            Func<Vector2> tip = () => ctx.WeaponTip;
            float launch = PinkSkills.Until(duration, 0.42f, 0.05f);
            float side = Mathf.Min(0.17f, launch * 0.55f);
            PinkFx.Instance.CastCircle(front, f, side + 0.06f, fx);
            ctx.Fx?.Flash(ctx.WeaponTip, 0.7f * fx, PinkSkills.Color, 0.3f, true);
            yield return Wait(side);
            PinkFx.Instance.CastSide(tip, f, launch - side + 0.08f, fx);
            yield return Wait(launch - side);

            // Khung 4: rồng pha lê bay thẳng tới, trúng thì nổ đá vụn. Quãng bay và tốc độ nhân tầm chiêu, rồng to theo cỡ ảnh
            Vector2 from = ctx.WeaponTip + ctx.Forward * 0.4f;
            ctx.Fx?.Flash(from, 0.9f * fx, PinkSkills.Color, 0.2f);
            float dmg = damage;
            float r = radius * s;
            float back = 0.2f * s;
            PinkFx.Instance.Dragon(from, f, speed * rg, range * rg,
                head => ctx.HitCircle(head - new Vector2(f * back, 0f), r, dmg, new HitEffect { knockback = new Vector2(3f, 1.5f), heavy = true },
                    PinkSkills.Color) > 0,
                at => Explode(ctx, at, f, fx), fx);
            ctx.HeroVisual?.SetWeaponPower(0f);
            yield return Wait(duration * 0.58f);
        }

        static void Explode(SkillContext ctx, Vector2 at, float f, float size)
        {
            PinkFx.Instance.DragonBlast(at, f, size);
            CombatFx fx = ctx.Fx;
            if (fx == null)
                return;
            fx.Flash(at, 0.9f * size, PinkSkills.Color, 0.2f);
            fx.Burst(at, PinkSkills.Pale, 8, 6f, 0.12f * size, 0.35f);
        }
    }

    /// <summary>
    /// Nút 2 · ↓ — "Dùng chiêu thủ" (skill/dùng chiêu thủ.png): cắm trượng, đứng nhắm mắt niệm phép; vòng phép sáng dưới
    /// chân (khung 3 → 6: nửa trước trước chân, nửa sau sau lưng), đoạn vòng xoắn quay quanh người (ra trước / vòng ra sau),
    /// 2 mẫu pha lê bay quanh, tia sáng mảnh dựng lên quanh vòng. Trong lúc vòng phép còn: chặn mọi sát thương; kẻ địch chạm
    /// vòng phép bị đánh nhẹ liên tục.
    /// </summary>
    [Serializable]
    public class PinkCrystalWard : Skill
    {
        [Tooltip("Vòng phép giữ bấy nhiêu giây (chặn sát thương suốt lúc đó)")]
        public float duration = 1.5f;
        [Tooltip("Sát thương mỗi nhịp cho kẻ địch chạm vòng phép")]
        public float tickDamage = 6f;
        public float tickInterval = 0.3f;
        [Tooltip("Bán kính vòng phép (unit, ×1)")]
        public float radius = 1.7f;
        [Tooltip("Ảnh hiệu ứng to theo cỡ chiêu: cỡ ảnh = max(1, cỡ chiêu × số này); vòng phép dưới chân vẫn phủ vùng đánh (≤ 0: 0.7)")]
        public float fxScale = 0.7f;

        public PinkCrystalWard()
        {
            name = "Dùng chiêu thủ";
            description = "Cắm trượng niệm phép: vòng phép dưới chân, vòng sáng và pha lê bay quanh người. Chặn mọi sát thương, kẻ địch chạm vòng phép mất máu.";
            cooldown = 6f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.7f);
            float f = ctx.Facing;
            bool grounded = ctx.Grounded;
            if (grounded)
                ctx.Hero?.Root(duration);
            else
                ctx.Hero?.Hover(duration);
            float until = Time.time + duration;
            ctx.BlockFor(duration, hit =>
            {
                ctx.Fx?.Flash(ctx.Chest, 1.2f * fx, PinkSkills.Pale, 0.25f);
                ctx.Fx?.Ring(ctx.Chest, 1.1f * fx, SkillColors.WithAlpha(PinkSkills.Color, 0.8f), 0.3f);
            });
            ctx.Act(CombatPose.Squash, duration, ctx.Ground(0.6f));
            ctx.HeroVisual?.SetWeaponPower(1f); // pha lê đầu trượng rực lên ngay khi niệm
            float plant = duration * 0.3f;
            yield return Wait(plant);

            // Khung 2 → 3: cắm trượng — chớp ở đầu nhọn, vòng phép bung ra dưới chân (vòng phép to theo cỡ chiêu = vùng đánh;
            // vòng xoắn, pha lê bay quanh to theo cỡ ảnh)
            Vector2 feet = PinkSkills.Feet(ctx);
            float r = radius * s;
            Func<bool> active = () => Time.time < until;
            ctx.Fx?.Flash(new Vector2(ctx.WeaponTip.x, feet.y + 0.1f), 0.8f * fx, PinkSkills.Color, 0.25f);
            PinkFx.Instance.WardCircle(feet, duration - plant + 0.15f, active, fx, r);
            PinkFx.Instance.OrbitDiamonds(() => ctx.Center + Vector2.up * 0.25f, 6, 1.05f * fx, duration - plant, active, fx);
            // Khung 4 → 6: đoạn vòng xoắn quay quanh người ở 3 độ cao (nửa quay ra trước vẽ trước người)
            PinkFx.Instance.Swirls("ward_swirl", () => ctx.Center, f, 3, -0.55f, 0.75f, duration - plant, 1.6f, active, fx);

            // tia sáng mảnh dựng lên quanh vòng; kẻ địch chạm vòng phép mất máu
            float tick = 0f, ray = 0f;
            for (float t = plant; t < duration; t += Time.deltaTime)
            {
                tick -= Time.deltaTime;
                ray -= Time.deltaTime;
                if (tick <= 0f)
                {
                    tick = tickInterval;
                    ctx.HitCircle(ctx.Center, r, tickDamage, new HitEffect { knockback = new Vector2(1.2f, 0f) }, PinkSkills.Color, false);
                }

                if (ray <= 0f && ctx.Fx != null)
                {
                    ray = 0.11f;
                    Vector2 at = feet + new Vector2(Random.Range(-r, r) * 0.9f, Random.Range(-0.05f, 0.1f));
                    ctx.Fx.Streak(at, at + Vector2.up * (Random.Range(0.8f, 1.6f) * fx), PinkSkills.Pale, PinkSkills.Color, 0.045f * fx, 0.35f);
                }

                yield return null;
            }

            ctx.HeroVisual?.SetWeaponPower(0f);
        }
    }

    // =====================================================================================================================
    // Nút 3 — kĩ năng đặc biệt (cần nộ; mốc nộ càng cao càng đau)
    // =====================================================================================================================

    /// <summary>
    /// Nút 3 · thường — "Kĩ năng đặc biệt" (special_skill/kĩ năng đặt biêt.png): khom người, hào quang tím bùng (khung 1),
    /// vòng chữ cổ quay quanh người (khung 2, đoạn vòng ra trước / sau lưng), bay lên giơ trượng, vệt cung lớn cuộn quanh
    /// (khung 3), dộng trượng — mái vòm pha lê nổ quanh mình 2 pha (K4 → K5, lõi dựng lại sắc nét), vòng chữ cổ của vòm dâng
    /// lên sau lưng, chân vòm / gai sát đất trước người, đá vụn văng (lớp riêng). Đánh mọi kẻ địch quanh mình, mốc nộ càng cao
    /// càng đau, vòm càng to.
    /// </summary>
    [Serializable]
    public class PinkCrystalNova : Skill
    {
        public float damage = 60f;
        [Tooltip("Bán kính vụ nổ ở mốc nộ 1 (mỗi mốc thêm 0.3; ×1)")]
        public float radius = 2.4f;
        [Tooltip("Bay lên cao bấy nhiêu unit trước khi dộng trượng")]
        public float rise = 1.1f;
        [Tooltip("Ảnh hiệu ứng to theo cỡ chiêu: cỡ ảnh = max(1, cỡ chiêu × số này); sóng nổ mặt đất vẫn phủ vùng đánh (≤ 0: 0.7)")]
        public float fxScale = 0.7f;

        public PinkCrystalNova()
        {
            name = "Kĩ năng đặc biệt";
            description = "Vòng phép xoáy quanh người, bay lên rồi dộng trượng: mái vòm pha lê lớn nổ quanh mình. Nộ càng cao càng đau.";
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.7f);
            float f = ctx.Facing;
            float power = ctx.Power;
            int tier = Mathf.Max(1, ctx.Tier);
            bool grounded = ctx.Grounded;
            float floor = PinkSkills.Feet(ctx).y;

            // Khung 1 → 2: khom người, lửa tím bùng sau lưng; vòng chữ cổ quay quanh người (đoạn vòng ra trước / sau lưng),
            // pha lê của tấm bay quanh
            ctx.Act(CombatPose.Spin, 0.6f);
            ctx.HeroVisual?.SetWeaponPower(1f);
            ctx.Fx?.Flames(ctx.Ground(0f), 1.3f * fx, PinkSkills.Color, 0.75f, true);
            ctx.Fx?.Flash(ctx.Chest, 1.6f * fx, PinkSkills.Color, 0.45f, true);
            PinkFx.Instance.Swirls("nova_runes", () => ctx.Center, f, 3, -0.45f, 0.75f, 0.62f, 1.3f, null, fx);
            PinkFx.Instance.OrbitDiamonds(() => ctx.Center + Vector2.up * 0.2f, 3, 1.25f * fx, 0.6f, null, fx * 0.8f, "nova_shard", "nova_shard");
            yield return Wait(0.55f);

            // Khung 3: bay lên (độ cao không đổi theo cỡ chiêu), giơ trượng quá đầu, vệt trăng cuộn quanh người
            const float slam = 0.75f;
            ctx.Act(CombatPose.Slam, slam, ctx.Ground(0.3f));
            bool jumped = ctx.Hero != null && ctx.CanMove;
            if (jumped)
                ctx.Hero.JumpTo(rise);
            PinkFx.Instance.NovaArc(() => ctx.Chest, f, fx);
            yield return Wait(0.22f);

            // Khung 4: dộng trượng xuống — lao xuống đất (đang đứng trên sàn thì về lại sàn), chạm đất mới nổ
            float waited = 0.22f;
            if (jumped && !ctx.Hero.Grounded)
            {
                ctx.Hero.Launch(new Vector2(0f, -16f));
                for (float t = 0f; t < 0.35f && !ctx.Hero.Grounded; t += Time.deltaTime)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            // Khung 4 → 5: mái vòm pha lê nổ quanh mình (2 pha, vòng chữ cổ, chân vòm trước người, đá vụn). Vòm gốc ở chân, to
            // theo cỡ ảnh; vùng nổ to theo cỡ chiêu (sóng mặt đất phủ đúng bán kính vùng đánh)
            Vector2 feet = PinkSkills.Feet(ctx);
            Vector2 at = new Vector2(feet.x, grounded && ctx.Hero != null && !ctx.Hero.Grounded ? floor : feet.y);
            float r = (radius + 0.3f * (tier - 1)) * s;
            Vector2 core = at + Vector2.up * (1f * s);
            PinkFx.Instance.Dome(at, f, (0.95f + 0.12f * (tier - 1)) * fx);
            if (ctx.Fx != null)
            {
                // nền mặt đất nhẹ (không dùng GroundSlam: quầng tím lớn của nó vẽ đè trước người); đá vụn đã là lớp nova_rocks
                ctx.Fx.Cracks(at, r * 0.5f, PinkSkills.Color, 0.7f);
                ctx.Fx.Shockwave(at, r * 1.1f, PinkSkills.Color, 0.5f);
                ctx.Fx.Flash(at + Vector2.up * (0.9f * fx), 1.8f * fx, PinkSkills.Heavy, 0.3f, true);
            }

            ctx.HitCircle(core, r, damage * power, new HitEffect { knockback = new Vector2(6f, 5f), stun = 0.6f, heavy = true },
                PinkSkills.Heavy);
            ctx.HeroVisual?.SetWeaponPower(0f);
            yield return Wait(Mathf.Max(0.2f, slam - waited) + 0.1f);
        }
    }

    /// <summary>
    /// Nút 3 · ↑ — "Kĩ năng đặc biệt tầm xa" (special_skill/kĩ năng đặc biệt tầm xa.png): giơ trượng, cầu lửa tụ ở đầu
    /// trượng (khung 2), vòng phép bung quanh đầu trượng (khung 3), chĩa trượng phóng tia pha lê lớn (khung 4: thân + lõi
    /// trắng trồi ra từ trượng, mũi pha lê, 2 vệt đuôi dài dần), nổ hình sao + tia sao chéo ở chỗ trúng / cuối đường bay
    /// (khung 5). Mốc nộ càng cao càng đau.
    /// </summary>
    [Serializable]
    public class PinkStarLance : Skill
    {
        public float damage = 75f;
        [Tooltip("Tốc độ bay (unit/giây, ×1; nhân tầm chiêu cùng quãng bay để thời gian bay giữ nguyên)")]
        public float speed = 20f;
        [Tooltip("Quãng bay tối đa (unit, ×1)")]
        public float range = 12f;
        [Tooltip("Bán kính mũi tia chạm mục tiêu / vụ nổ cuối đường bay (unit, ×1)")]
        public float radius = 1f;
        [Tooltip("Ảnh hiệu ứng to theo cỡ chiêu: cỡ ảnh = max(1, cỡ chiêu × số này); vùng đánh vẫn to đúng cỡ chiêu (≤ 0: 0.8)")]
        public float fxScale = 0.8f;

        public PinkStarLance()
        {
            name = "Kĩ năng đặc biệt tầm xa";
            description = "Tụ cầu sáng đầu trượng, vòng phép bung ra, phóng tia pha lê lớn bay xa, nổ hình sao ở chỗ trúng. Nộ càng cao càng đau.";
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size, rg = ctx.Range;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.8f);
            float f = ctx.Facing;
            float power = ctx.Power;
            int tier = Mathf.Max(1, ctx.Tier);
            if (ctx.Grounded)
                ctx.Hero?.Root(1.35f);

            // Khung 2: giơ trượng quá đầu, cầu lửa tụ ở đầu trượng
            ctx.Act(CombatPose.Rear, 0.6f);
            ctx.HeroVisual?.SetWeaponPower(1f);
            Func<Vector2> tip = () => ctx.CastPoint;
            PinkFx.Instance.Orb(tip, f, 0.62f, fx);
            ctx.Fx?.Flash(ctx.CastPoint, 0.8f * fx, PinkSkills.Color, 0.45f, true);
            yield return Wait(0.3f);

            // Khung 3: vòng phép bung quanh đầu trượng (sau lưng trượng, tâm không còn đầu trượng thứ hai)
            PinkFx.Instance.LanceCircle(tip, f, 0.8f, fx);
            yield return Wait(0.25f);

            // Khung 4: chĩa trượng, phóng tia pha lê lớn. Quãng bay và tốc độ nhân tầm chiêu, tia và vụ nổ to theo cỡ
            const float fire = 0.65f;
            ctx.Act(CombatPose.Reach, fire, ctx.Chest + ctx.Forward * (8f * rg));
            yield return Wait(fire * 0.42f);
            Vector2 from = ctx.WeaponTip + ctx.Forward * 0.5f;
            ctx.Fx?.Flash(from, 1.2f * fx, PinkSkills.Pale, 0.22f);
            float dmg = damage * power;
            float r = radius * s;
            float back = 0.3f * s;
            float size = (1f + 0.1f * (tier - 1)) * fx;
            var effect = new HitEffect { knockback = new Vector2(5f, 2.5f), stun = 0.5f, heavy = true };
            bool hit = false;
            PinkFx.Instance.Lance(from, f, speed * rg, range * rg,
                head =>
                {
                    hit = ctx.HitCircle(head - new Vector2(f * back, 0f), r, dmg, effect, PinkSkills.Heavy) > 0;
                    return hit;
                },
                at =>
                {
                    // hết tầm mà chưa trúng ai: vẫn nổ, đánh quanh chỗ nổ
                    if (!hit)
                        ctx.HitCircle(at, r * 1.5f, dmg, effect, PinkSkills.Heavy);
                    Blast(ctx, at, f, size);
                }, size);
            ctx.HeroVisual?.SetWeaponPower(0f);
            yield return Wait(fire * 0.58f);
        }

        static void Blast(SkillContext ctx, Vector2 at, float f, float size)
        {
            PinkFx.Instance.StarBlast(at, f, size);
            CombatFx fx = ctx.Fx;
            if (fx == null)
                return;
            fx.Flash(at, 1.2f * size, PinkSkills.Heavy, 0.22f);
            fx.Burst(at, PinkSkills.Pale, 10, 9f, 0.14f * size, 0.4f);
        }
    }

    /// <summary>
    /// Nút 3 · ↓ — "Kĩ năng đặc biệt áp chế" (special_skill/kĩ năng đặc biệt áp chế.png): chĩa trượng, xích phóng ra trói kẻ
    /// địch gần nhất phía trước (khung 2; 2 sợi × 6 đoạn Tiled), xích quấn quanh người nó (khung 3, không còn hình nộm: lớp
    /// trước đè lên mục tiêu + lớp sau ở sau mục tiêu, siết lại), giơ trượng giam nó trong lồng (khung 4 → 6: cột giáo trước
    /// + nửa trước vòng đáy trước mục tiêu, cột giáo giữa + quầng + nửa sau vòng đáy sau mục tiêu, hạt pha lê tan): choáng
    /// lâu, mất máu liên tục. Boss da cứng: choáng chỉ còn một phần (LionBoss), lồng tan sớm theo. Mốc nộ càng cao càng đau.
    /// </summary>
    [Serializable]
    public class PinkChainSeal : Skill
    {
        [Tooltip("Sát thương lúc xích trói")]
        public float damage = 30f;
        [Tooltip("Sát thương mỗi nhịp khi bị giam")]
        public float tickDamage = 9f;
        public float tickInterval = 0.5f;
        [Tooltip("Choáng / giam bấy nhiêu giây (boss tự giảm theo da cứng)")]
        public float stun = 3f;
        [Tooltip("Tầm tìm kẻ địch phía trước (unit, ×1)")]
        public float range = 8f;
        [Tooltip("Mắt xích to theo cỡ chiêu: cỡ = max(1, cỡ chiêu × số này) (≤ 0: 0.75); lồng / xích quấn luôn vừa với mục tiêu")]
        public float fxScale = 0.75f;

        public PinkChainSeal()
        {
            name = "Kĩ năng đặc biệt áp chế";
            description = "Phóng xích từ trượng trói kẻ địch gần nhất phía trước, giam trong lồng xích có cột giáo pha lê: choáng lâu, mất máu liên tục.";
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float s = ctx.Size, rg = ctx.Range;
            float fx = PinkSkills.FxSize(ctx, fxScale, 0.75f);
            float f = ctx.Facing;
            float power = ctx.Power;
            // Tầm tìm kẻ địch nhân tầm chiêu; xích, lồng giam vẫn đặt đúng trên người mục tiêu
            float reach = range * rg;
            Health target = ctx.NearestAhead(reach, 3f);
            Vector2 aim = target != null ? SkillContext.Aim(target) : ctx.Chest + ctx.Forward * reach;
            if (ctx.Grounded)
                ctx.Hero?.Root(1.5f);

            // Khung 1 → 2: chĩa trượng về phía kẻ địch, xích phóng ra (mắt xích to theo cỡ)
            const float point = 0.6f;
            ctx.Act(CombatPose.Reach, point, aim);
            ctx.HeroVisual?.SetWeaponPower(1f);
            yield return Wait(point * 0.42f);
            bool holding = true;
            Func<Vector2> tip = () => ctx.WeaponTip;
            Func<bool> held = () => holding;
            if (target != null)
            {
                Health t = target;
                PinkFx.Instance.Chains(tip, () => SkillContext.Aim(t) + Vector2.up * 0.15f, 1.3f, () => holding && t != null && t.IsAlive, 0.25f * fx, fx);
                PinkFx.Instance.Chains(tip, () => SkillContext.Aim(t) + Vector2.down * 0.3f, 1.3f, () => holding && t != null && t.IsAlive, -0.2f * fx, fx);
            }
            else
            {
                PinkFx.Instance.Chains(tip, () => aim, 0.45f, held, 0.2f * fx, fx);
            }

            ctx.Fx?.Flash(ctx.WeaponTip, 0.8f * fx, PinkSkills.Color, 0.25f);
            yield return Wait(0.12f);
            if (target == null || !target.IsAlive)
            {
                // không có ai phía trước: xích rơi lại
                yield return Wait(0.35f);
                holding = false;
                ctx.HeroVisual?.SetWeaponPower(0f);
                yield break;
            }

            // Khung 3: xích quấn quanh người — trói (choáng) + sát thương
            ctx.HitCircle(SkillContext.Aim(target), 0.45f * s, damage * power, new HitEffect { stun = stun, heavy = true }, PinkSkills.Heavy);
            var lion = target.GetComponentInParent<LionBoss>();
            float hold = lion != null ? Mathf.Max(0.5f, lion.StunRemaining) : stun;
            Bounds body = PinkSkills.BodyOf(target);
            float height = Mathf.Max(1.2f, body.size.y);
            Health victim = target;
            Func<Vector2> feet = () => PinkSkills.FeetOf(victim);
            Func<bool> alive = () => victim != null && victim.IsAlive;
            // Mục tiêu cỡ người (bia tập): xích quấn cả người như tranh. Mục tiêu khổng lồ (sư tử cao ~9 unit): một vòng xích
            // quấn ngang thân, không phủ kín cả con
            bool huge = height > 3f;
            float wrap = huge ? Mathf.Min(3.5f, height * 0.4f) : height;
            Func<Vector2> waist = huge ? () => PinkSkills.FeetOf(victim) + Vector2.up * (height * 0.3f) : feet;
            // Xích quấn ôm sát thân mục tiêu và lồng giam vừa với mục tiêu: không to theo cỡ chiêu (giống lồng của RED, BLUE).
            // Nửa sau (xích vòng ra sau lưng, cột giáo giữa) vẽ dưới mọi hình của mục tiêu
            const float cage = 1f;
            int behind = PinkFx.BehindOf(target);
            PinkFx.Instance.ChainWrap(waist, wrap, Mathf.Min(body.size.x, huge ? 5f : 99f) * 1.05f, hold, behind, alive);
            ctx.Fx?.Flash(SkillContext.Aim(target), 1.1f * fx, PinkSkills.Color, 0.25f);
            yield return Wait(0.22f);

            // Khung 4 → 6: giơ trượng (xích thả ra), lồng xích + cột giáo pha lê + vòng phép giam kẻ địch
            holding = false;
            ctx.Act(CombatPose.Rear, 0.7f);
            PinkFx.Instance.Cage(feet, (huge ? height * 1.1f : height * 1.8f) * cage, body.size.x * (huge ? 0.9f : 1.1f) * cage,
                Mathf.Max(0.3f, hold - 0.2f), behind, alive);
            ctx.Fx?.Shockwave(feet(), Mathf.Max(1f, body.size.x * 0.7f) * s, PinkSkills.Color, 0.4f);
            ctx.Fx?.Flash(feet() + Vector2.up * height * 0.5f, 1.5f * fx, PinkSkills.Color, 0.3f, true);
            ctx.Combat.Linger(Seal(ctx, victim, hold, tickDamage * power));
            yield return Wait(0.6f);
            ctx.HeroVisual?.SetWeaponPower(0f);
        }

        // Bị giam: mất máu từng nhịp tới khi hết giam (boss: hết choáng). ctx là bản ngữ cảnh của lần dùng này (cỡ chiêu lúc dùng)
        IEnumerator Seal(SkillContext ctx, Health target, float hold, float tick)
        {
            float s = ctx.Size;
            for (float t = tickInterval; t <= hold + 1e-3f; t += tickInterval)
            {
                yield return Wait(tickInterval);
                if (target == null || !target.IsAlive)
                    yield break;
                ctx.HitCircle(SkillContext.Aim(target), 0.45f * s, tick, default, PinkSkills.Color, false);
                ctx.Fx?.Burst(SkillContext.Aim(target), PinkSkills.Pale, 4, 2.5f, 0.1f * s, 0.3f);
            }
        }
    }
}
