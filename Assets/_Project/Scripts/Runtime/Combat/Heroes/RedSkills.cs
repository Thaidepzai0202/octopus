using System;
using System.Collections;
using UnityEngine;

namespace Octopus
{
    // =====================================================================================================================
    // RED — bộ 9 chiêu (tên theo tấm tham khảo của người dùng, Docs/nhan_vat_moi/red/<loại>/<tên chiêu>.png; mỗi chiêu diễn
    // đúng như tấm của nó). Thứ tự trong mảng: nút 1 thường / ↑ / ↓, nút 2 thường / ↑ / ↓, nút 3 thường / ↑ / ↓ (↑ / ↓ = kéo
    // cần điều hướng lên / xuống khi bấm nút). Nút 3 là chiêu đặc biệt, cần nộ qua mốc 1, đau theo mốc nộ (ctx.Power).
    // Hiệu ứng: vệt chém / tia lửa / sóng tròn của CombatFx (màu đỏ của RED) + hiệu ứng cắt thẳng từ các tấm chiêu (RedFx:
    // sóng lưỡi liềm, nổ tinh thể, chữ X, vòng phép, xích, lồng xích…).
    // Màn chơi dựng lại (menu Octopus → Dựng lại màn chơi) thì CombatBuilder điền RedSkills.Default() vào Combat Controller.
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
    //  Cỡ / tầm chiêu (ô "Cỡ / tầm chiêu" của Combat Controller, mặc định ×2): ctx.Size nhân vùng đánh trúng + cỡ hiệu ứng,
    //    ctx.Range nhân khoảng cách tới trước, tầm tìm mục tiêu, quãng (và tốc độ) sóng bay, quãng lướt. RED dựng VÙNG ĐÁNH
    //    qua RedSkills.Spot(ctx, tới trước, cao hơn bàn chân) để chiêu to ra vẫn mọc lên từ mặt đất; HÌNH vệt chém thì đặt ở
    //    chỗ vung kiếm thật (RedSkills.SwingCenter / ctx.WeaponTip) với cỡ hình riêng (ô fxGrow của từng chiêu).
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
    // =====================================================================================================================


    public static class RedSkills
    {
        /// <summary>Màu chủ đạo: số sát thương, tia lửa.</summary>
        public static readonly Color Color = new Color(1f, 0.22f, 0.2f);
        /// <summary>Thân vệt chém lưỡi liềm (lõi tự trắng rực, mép trong đỏ thẫm) như các vệt trong tranh.</summary>
        public static readonly Color SlashColor = new Color(1f, 0.1f, 0.12f);
        public static readonly Color EdgeColor = new Color(0.42f, 0.01f, 0.07f);
        /// <summary>Quầng sáng, vết nứt sáng, lửa hào quang.</summary>
        public static readonly Color GlowColor = new Color(1f, 0.26f, 0.2f);
        /// <summary>Lõi trắng hồng (chớp, tia lửa nóng).</summary>
        public static readonly Color HotColor = new Color(1f, 0.82f, 0.78f);
        /// <summary>Số sát thương đòn nặng / chiêu đặc biệt.</summary>
        public static readonly Color HeavyColor = new Color(1f, 0.42f, 0.3f);
        /// <summary>Đá đen ánh đỏ văng ra (như đá vụn trong tranh).</summary>
        public static readonly Color RockColor = new Color(0.24f, 0.12f, 0.14f);
        /// <summary>Vòng đánh trúng mờ (cho thấy vùng đánh thật khi hình hiệu ứng nhỏ hơn vùng đánh ×2).</summary>
        public static readonly Color ReachColor = new Color(1f, 0.26f, 0.2f, 0.45f);

        public static Skill[] Default() => new Skill[]
        {
            // nút 1 · thường
            Named(new RedComboSlash(), "Đánh thường", "Combo 3 đòn: chém ngang tới trước, chém ngược tay, giơ kiếm bổ xuống nổ đất"),
            // nút 1 · ↑
            Named(new RedRisingSlash(), "Đánh thường hất tung", "Khom người, bật lên chém hất từ dưới lên (vệt trăng dọc), hất tung kẻ địch"),
            // nút 1 · ↓
            Named(new RedDashSlash(), "Đánh thường áp sát", "Lướt thấp tới trước, chém vòng ngang, trượt dừng tung bụi; trúng mọi kẻ địch trên đường lướt"),
            // nút 2 · thường
            Named(new RedSpinBurst(), "Kĩ năng", "Kiếm tụ lực, xoay người thành các vòng chém đỏ quanh thân, nổ vòng tinh thể"),
            // nút 2 · ↑
            Named(new RedCrescentWave(), "Kĩ năng tầm xa", "Giơ kiếm, đâm tới phóng sóng lưỡi liềm đỏ bay xa, nổ tinh thể chỗ trúng"),
            // nút 2 · ↓
            Named(new RedGuardCircle(), "Kĩ năng thủ", "Cắm kiếm xuống đất, vòng phép đỏ dưới chân: chặn mọi đòn, kẻ địch đứng trong vòng bị đánh"),
            // nút 3 · thường
            Named(new RedFuryCross(), "Kĩ năng đặc biệt", "Bùng hào quang lửa, lao chém liên tiếp, chém chữ X khổng lồ nổ đá, sóng xung kích"),
            // nút 3 · ↑
            Named(new RedGrandWave(), "Kĩ năng đặc biệt tầm xa", "Bùng hào quang, giơ kiếm, phóng sóng lưỡi liềm khổng lồ bay rất xa, nổ cột đá đỏ"),
            // nút 3 · ↓
            Named(new RedChainPrison(), "Kĩ năng đặc biệt áp chế", "Phóng xích trói kẻ địch gần nhất phía trước, kéo lại đấm, giam trong lồng xích: choáng lâu, mất máu liên tục"),
        };

        static Skill Named(Skill skill, string name, string description)
        {
            skill.name = name;
            skill.description = description;
            return skill;
        }

        /// <summary>Lúc mũi kiếm ra đòn (giây tính từ lúc diễn dáng) theo hoạt ảnh của RED.</summary>
        internal static float StrikeTime(CombatPose pose, float duration) => HeroRigAnimator.StrikeAt(pose) * duration;

        /// <summary>Khung thân mục tiêu (world): boss = khung nhận đòn của boss; bia tập = collider (body).</summary>
        internal static Bounds BodyOf(Health target, LionBoss boss, Collider2D body)
        {
            if (boss != null)
                return boss.Body;
            if (body != null && body.enabled)
                return body.bounds;
            return new Bounds(SkillContext.Aim(target), new Vector3(1.1f, 1.9f, 0f));
        }

        /// <summary>Chỗ nhắm trên thân mục tiêu: boss to thì nhắm vào phía thân gần người chơi.</summary>
        internal static Vector2 AimAt(Health target, LionBoss boss, float facing)
        {
            if (boss == null)
                return SkillContext.Aim(target);
            Bounds b = boss.Body;
            return new Vector2(b.center.x - facing * b.extents.x * 0.55f, b.min.y + b.size.y * 0.45f);
        }

        /// <summary>Kiếm rực lên (0..1) / tắt dần — chiêu tụ lực.</summary>
        internal static IEnumerator WeaponGlow(SkillContext ctx, float from, float to, float seconds)
        {
            IHeroVisual visual = ctx.HeroVisual;
            if (visual == null)
                yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                visual.SetWeaponPower(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }

            visual.SetWeaponPower(to);
        }

        // ---------- Cỡ / tầm chiêu (ctx.Size, ctx.Range: ô "Cỡ / tầm chiêu" của Combat Controller, mặc định ×2) ----------
        // VÙNG ĐÁNH TRÚNG của RED dựng quanh bàn chân: khoảng cách tới trước × tầm, chiều cao / bán kính / bề dày × cỡ (chiêu to
        // ra vẫn mọc lên từ mặt đất); ở ×1 trùng đúng chỗ cũ (ngực = chân + 1, tâm thân = chân + 0.5). Không nhân: chỗ nằm trên
        // mục tiêu (nổ ở chỗ trúng, xích, lồng), sát thương, thời gian, độ hất lên, choáng, điểm nhắm của dáng.
        // HÌNH hiệu ứng thì KHÔNG nâng theo cỡ: vệt chém cắt từ tấm đặt ở chỗ vung kiếm thật (SwingCenter = ngực / chuôi kiếm
        // đọc từ hình lúc ra đòn), sóng bung ra từ mũi kiếm thật (ctx.WeaponTip), nổ đất ở mặt đất; cỡ hình = FxScale(ctx,
        // fxGrow) với fxGrow là ô chỉnh trong từng chiêu (0 = đúng cỡ tranh, 1 = to bằng vùng đánh) — vệt dài ra tới trước
        // theo tầm nhưng bề cao tăng ít, không che nhân vật; vùng đánh thật còn được cho thấy bằng tia lửa ở mép xa / vòng mờ.

        /// <summary>Bàn chân RED (world).</summary>
        internal static Vector2 Feet(SkillContext ctx) => ctx.Center + Vector2.down * HeroMotor.FeetOffset;

        /// <summary>Điểm của VÙNG ĐÁNH: trước mặt dx (× tầm), cao hơn bàn chân dy (× cỡ).</summary>
        internal static Vector2 Spot(SkillContext ctx, float dx, float dy) =>
            ctx.Center + new Vector2(ctx.Facing * dx * ctx.Range, dy * ctx.Size - HeroMotor.FeetOffset);

        /// <summary>
        /// Cỡ HÌNH vệt cắt từ tấm theo cỡ / tầm chiêu: x (dọc hướng chém) = 1 + (tầm - 1) × grow.x, y (bề cao) = 1 + (cỡ - 1) ×
        /// grow.y. grow = (0, 0): đúng cỡ tranh (vừa tầm kiếm của hình); (1, 1): to bằng vùng đánh.
        /// </summary>
        internal static Vector2 FxScale(SkillContext ctx, Vector2 grow) =>
            new Vector2(1f + (ctx.Range - 1f) * grow.x, 1f + (ctx.Size - 1f) * grow.y);

        /// <summary>Hệ số cỡ hình đều hai chiều (vòng, nổ): 1 + (cỡ - 1) × grow.</summary>
        internal static float FxSize(SkillContext ctx, float grow) => 1f + (ctx.Size - 1f) * grow;

        /// <summary>
        /// Tâm vung kiếm THẬT (world) lúc gọi: ngang = ngực, cao = ngực hạ theo chuôi kiếm trong tay khi tay thấp hơn ngực
        /// (đọc từ hình — lao thấp, khom người thì thấp theo; tay đang giơ kiếm quá đầu thì không nâng). Các vệt chém cắt từ
        /// tấm có pivot ở ngực RED trong khung tranh nên đặt vào đây là đúng chỗ như tranh, không bị nâng lên khi cỡ chiêu ×2.
        /// </summary>
        internal static Vector2 SwingCenter(SkillContext ctx)
        {
            Vector2 chest = ctx.Chest;
            float drop = Mathf.Clamp(ctx.CastPoint.y + 0.2f - chest.y, -0.5f, 0f);
            return new Vector2(chest.x, chest.y + drop * 0.6f);
        }

        /// <summary>Mặt đất ngay dưới x (độ cao bàn chân RED lúc gọi).</summary>
        internal static Vector2 GroundAt(SkillContext ctx, float x) => new Vector2(x, Feet(ctx).y);

        /// <summary>
        /// Vệt chém cắt từ tấm: thân đỏ (ảnh body) + lõi trắng riêng (ảnh core, cộng sáng, tắt sau ~0.1 giây — lõi mảnh, không
        /// chồng quầng), đặt ở at theo pivot, cỡ scale (x dọc hướng chém), xoay angle độ (0 = như tranh, quay mặt theo hướng
        /// nhìn), lúc đầu lệch thêm sweep độ rồi quét về chỗ trong 0.07 giây (vệt đi theo đường kiếm).
        /// </summary>
        internal static void SheetSlash(SkillContext ctx, string body, string core, Vector2 at, Vector2 scale, float life,
            float angle = 0f, float sweep = 0f, int order = RedFx.FrontOrder, Func<Vector2> follow = null, float delay = 0f,
            float glow = 0.85f, float fadeIn = 0.02f)
        {
            RedFx.Instance.Pop(body, at, scale.y, life, ctx.Facing, order, 0.07f, 0.82f, false, glow, fadeIn, life * 0.6f, angle,
                follow: follow, delay: delay, core: core, sweep: sweep, stretchX: scale.x / Mathf.Max(0.01f, scale.y), coreFade: 0.1f);
        }

        /// <summary>Tia lửa nhỏ ở mép xa vùng đánh (vệt hình ngắn hơn vùng đánh ×2: vẫn thấy được tầm thật).</summary>
        internal static void ReachSpark(SkillContext ctx, Vector2 at)
        {
            ctx.Fx?.Burst(at, HotColor, 4, 3.5f, 0.1f, 0.2f, 2f, ctx.Forward * 0.6f);
        }

        /// <summary>Tia lửa toé (CombatFx.Burst) theo cỡ chiêu: hạt to hơn, toé xa hơn, cùng thời gian (tốc độ, trọng lực × cỡ).</summary>
        internal static void Sparks(SkillContext ctx, Vector2 at, Color color, int count, float speed, float size, float life,
            float gravity = 6f, Vector2 bias = default)
        {
            float k = ctx.Size;
            ctx.Fx?.Burst(at, color, count, speed * k, size * k, life, gravity * k, bias);
        }

        /// <summary>Đá văng (CombatFx.Debris) theo cỡ chiêu: viên to hơn, văng cao / xa hơn, cùng thời gian.</summary>
        internal static void Rocks(SkillContext ctx, Vector2 at, Color color, int count, float speed, float size, float gravity = 18f)
        {
            float k = ctx.Size;
            ctx.Fx?.Debris(at, color, count, speed * k, size * k, gravity * k);
        }

        /// <summary>Hào quang lửa đỏ bùng quanh người (chiêu đặc biệt khung 1): lửa sau lưng, chớp sau lưng, nứt đất, đá văng.</summary>
        internal static void Aura(SkillContext ctx, float seconds)
        {
            CombatFx fx = ctx.Fx;
            if (fx == null)
                return;
            float s = FxSize(ctx, 0.5f);
            Vector2 feet = ctx.Ground(0f);
            Vector2 chest = ctx.Chest;
            // lửa ôm người: lớp sau lưng rộng, lớp trước mảnh (khối lửa quanh thân, không phải cột lửa thẳng)
            fx.Flames(feet, 1.2f * s, GlowColor, seconds, true);
            fx.Flames(feet, 0.55f * s, SlashColor, seconds * 0.7f);
            fx.Flash(chest, 2.2f * s, GlowColor, seconds * 0.5f, true);
            fx.Cracks(feet, 1.1f * s, GlowColor, seconds + 0.3f);
            Rocks(ctx, feet, RockColor, 6, 5f, 0.24f);
            Sparks(ctx, chest, HotColor, 8, 5f, 0.12f, 0.4f, -2f);
        }

        /// <summary>Vệt chém procedural (CombatFx) cho đòn không có hình riêng trong tấm: thân đỏ + vệt mảnh bên trong.</summary>
        internal static void Crescent(SkillContext ctx, Vector2 center, float radius, float from, float to, float width, float life,
            float squash = 1f, float delay = 0f)
        {
            CombatFx fx = ctx.Fx;
            if (fx == null)
                return;
            fx.Slash(center, radius, ctx.Angle(from), ctx.Angle(to), SlashColor, EdgeColor, width, life, squash, delay);
            fx.Slash(center, radius * 0.8f, ctx.Angle(from + (to - from) * 0.08f), ctx.Angle(to - (to - from) * 0.06f), SlashColor, EdgeColor,
                width * 0.38f, life * 0.85f, squash, delay + 0.03f);
        }
    }

    /// <summary>
    /// Nút 1 · thường — Đánh thường (tấm Đánh thường): combo 3 đòn khi bấm liên tục. Đòn 1 giơ kiếm qua đầu rồi chém ngang
    /// tới trước (khung 2 → 3, vệt lưỡi liềm ngang ngực); đòn 2 chém ngược tay (khung 4, vệt dài từ sau lưng trên cao quét
    /// xuống trước thấp); đòn 3 giơ kiếm bổ xuống đất (khung 5: vệt từ sau đầu vòng qua trên xuống tới đất, nổ đất), nặng hơn.
    /// Mỗi đòn dùng bộ vệt riêng cắt từ đúng khung của tấm, đặt ở chỗ vung kiếm thật.
    /// </summary>
    [Serializable]
    public class RedComboSlash : Skill
    {
        public float damage = 20f;
        public float finisherDamage = 40f;
        [Tooltip("Tầm chém trước mặt (unit)")]
        public float reach = 2.1f;
        [Tooltip("Bấm lại trong bấy nhiêu giây sau đòn trước thì ra đòn tiếp của combo")]
        public float comboWindow = 0.7f;
        [Tooltip("Cỡ HÌNH vệt chém theo Cỡ / tầm chiêu (vùng đánh vẫn ×2 đầy đủ): x = phần tầm cộng vào chiều dài vệt tới " +
                 "trước, y = phần cỡ cộng vào bề cao. 0 = đúng cỡ tranh, 1 = to bằng vùng đánh")]
        public Vector2 fxGrow = new Vector2(0.85f, 0.4f);

        int _step;
        float _lastEnd = -10f;

        public override IEnumerator Run(SkillContext ctx)
        {
            if (Time.time - _lastEnd > comboWindow)
                _step = 0;
            int step = _step;
            _step = (_step + 1) % 3;
            Vector2 fwd = ctx.Forward;
            float s = ctx.Size, r = ctx.Range;
            Vector2 k = RedSkills.FxScale(ctx, fxGrow);

            if (step == 0)
            {
                const float duration = 0.4f;
                ctx.Act(CombatPose.Whip, duration, ctx.Chest + fwd * reach);
                float strike = RedSkills.StrikeTime(CombatPose.Whip, duration);
                yield return Wait(strike - 0.05f);
                // khung 3: vệt ngang ở chỗ vung kiếm thật, quét từ trên xuống theo lưỡi kiếm
                RedSkills.SheetSlash(ctx, RedFx.ComboSlashSprite, RedFx.ComboSlashCore, RedSkills.SwingCenter(ctx), k, 0.24f, -8f, 40f);
                yield return Wait(0.05f);
                ctx.HitBox(RedSkills.Spot(ctx, reach * 0.55f, 0.95f), new Vector2(reach * 1.15f * s, 2f * s), damage,
                    new HitEffect { knockback = new Vector2(2.5f, 0.6f) }, RedSkills.Color);
                RedSkills.ReachSpark(ctx, RedSkills.SwingCenter(ctx) + fwd * (reach * 1.05f * r));
                yield return Wait(duration - strike);
            }
            else if (step == 1)
            {
                const float duration = 0.42f;
                ctx.Act(CombatPose.WhipAlt, duration, ctx.Chest + fwd * reach + Vector2.down * 0.3f);
                float strike = RedSkills.StrikeTime(CombatPose.WhipAlt, duration);
                yield return Wait(strike - 0.06f);
                // khung 4: vệt chéo dài từ sau lưng trên cao quét qua thân xuống trước thấp
                // (vệt nằm chéo qua thân, phần lớn sau lưng như tranh: kéo dài ít hơn, neo lùi ra trước theo lưỡi kiếm)
                Vector2 kb = new Vector2(Mathf.Lerp(k.y, k.x, 0.4f), k.y);
                RedSkills.SheetSlash(ctx, RedFx.ComboBackSprite, RedFx.ComboBackCore, RedSkills.SwingCenter(ctx) + fwd * 0.35f, kb,
                    0.26f, -10f, 30f);
                yield return Wait(0.06f);
                ctx.HitBox(RedSkills.Spot(ctx, reach * 0.5f, 0.8f), new Vector2(reach * 1.1f * s, 1.9f * s), damage,
                    new HitEffect { knockback = new Vector2(2.5f, 1.2f) }, RedSkills.Color);
                RedSkills.ReachSpark(ctx, RedSkills.SwingCenter(ctx) + fwd * (reach * 1.0f * r) + Vector2.down * 0.5f);
                yield return Wait(duration - strike);
            }
            else
            {
                const float duration = 0.55f;
                ctx.Act(CombatPose.Slam, duration, ctx.Ground(1.45f));
                float strike = RedSkills.StrikeTime(CombatPose.Slam, duration);
                yield return Wait(strike - 0.07f);
                // khung 5: cung từ sau đầu vòng qua trên bổ xuống đất trước mặt (pivot = chân RED), quét theo lưỡi kiếm
                Vector2 feet = RedSkills.GroundAt(ctx, ctx.Chest.x);
                RedSkills.SheetSlash(ctx, RedFx.ComboSlamSprite, RedFx.ComboSlamCore, feet, k, 0.3f, 0f, 28f);
                yield return Wait(0.07f);
                Vector2 ground = ctx.Ground(1.45f * r);
                // nổ đất của chính tấm (khung 5) ở giữa vùng nổ + nứt / đá / bụi procedural phủ đúng bán kính vùng nổ
                RedFx.Instance.Pop(RedFx.ComboSlamHitSprite, ground, 1.15f * RedSkills.FxSize(ctx, 0.6f), 0.45f, ctx.Facing, rise: true,
                    grow: 0.1f, scale0: 0.3f, glow: 0.7f, fadeOut: 0.28f);
                ctx.Fx?.GroundSlam(ground, 1.3f * s, RedSkills.GlowColor, RedSkills.RockColor, 8, 1f);
                ctx.HitCircle(ground + Vector2.up * (0.6f * s), 1.3f * s, finisherDamage,
                    new HitEffect { knockback = new Vector2(3.5f, 4.5f), heavy = true }, RedSkills.HeavyColor);
                yield return Wait(duration - strike);
            }

            _lastEnd = Time.time;
        }
    }

    /// <summary>
    /// Nút 1 · ↑ — Đánh thường hất tung (tấm cùng tên): khom người, bật lên chém hất từ dưới lên, kẻ địch bị hất tung lên, RED
    /// bật nhẹ lên theo rồi đáp xuống. Vệt là chuỗi 4 pha của tấm (khung 2 quệt đất → 3 quét lên → 4 vượt quá đầu → 5 thu vệt),
    /// cùng mốc ngực RED nên đổi pha không giật và bám theo người khi bật lên.
    /// </summary>
    [Serializable]
    public class RedRisingSlash : Skill
    {
        public float damage = 22f;
        [Tooltip("Hất kẻ địch lên (unit/giây)")]
        public float launch = 9f;
        [Tooltip("RED bật lên theo cao bấy nhiêu unit")]
        public float hop = 1.2f;
        [Tooltip("Cỡ HÌNH vệt (x = bề ngang, y = bề cao) theo Cỡ / tầm chiêu: 0 = đúng cỡ tranh, 1 = to bằng vùng đánh")]
        public Vector2 fxGrow = new Vector2(0.5f, 0.3f);

        public RedRisingSlash() => cooldown = 0.8f;

        public override IEnumerator Run(SkillContext ctx)
        {
            const float duration = 0.55f;
            Vector2 fwd = ctx.Forward;
            float s = ctx.Size, r = ctx.Range;
            ctx.Act(CombatPose.Whip, duration, ctx.Chest + fwd * 1f + Vector2.up * 1.6f); // nhắm cao: dáng chém hất lên
            bool grounded = ctx.Grounded;
            if (grounded)
                ctx.Fx?.DustCloud(ctx.Ground(0f), 0.6f * s, SkillColors.Dust, 3);
            float strike = RedSkills.StrikeTime(CombatPose.Whip, duration);
            yield return Wait(strike - 0.08f);

            // 4 pha nối nhau (mờ chồng lên nhau), bám ngực thật
            Vector2 k = RedSkills.FxScale(ctx, fxGrow);
            Func<Vector2> chest = () => RedSkills.SwingCenter(ctx);
            RedFx red = RedFx.Instance;
            float sy = k.y, sx = k.x / k.y;
            red.Pop(RedFx.RiseSprite0, chest(), sy, 0.09f, ctx.Facing, RedFx.FrontOrder, 0.05f, 0.9f, glow: 0.5f, fadeIn: 0.02f,
                fadeOut: 0.05f, follow: chest, stretchX: sx);
            red.Pop(RedFx.RiseSprite1, chest(), sy, 0.1f, ctx.Facing, RedFx.FrontOrder, 0.05f, 0.9f, glow: 0.9f, fadeIn: 0.02f,
                fadeOut: 0.05f, follow: chest, delay: 0.04f, core: RedFx.RiseCore1, stretchX: sx, coreFade: 0.08f);
            red.Pop(RedFx.RisingSlashSprite, chest(), sy, 0.18f, ctx.Facing, RedFx.FrontOrder, 0.06f, 0.9f, glow: 0.9f, fadeIn: 0.03f,
                fadeOut: 0.1f, follow: chest, delay: 0.1f, core: RedFx.RisingSlashCore, stretchX: sx, coreFade: 0.1f);
            red.Pop(RedFx.RiseSprite3, chest(), sy, 0.2f, ctx.Facing, RedFx.FrontOrder, 0.05f, 0.95f, glow: 0.3f, fadeIn: 0.04f,
                fadeOut: 0.14f, follow: chest, delay: 0.22f, stretchX: sx);
            yield return Wait(0.03f);

            // RED bật lên theo: độ cao không nhân (chỉ vùng chém to ra)
            if (ctx.Hero != null)
                ctx.Hero.JumpTo(grounded ? hop : hop * 0.6f);
            if (grounded)
            {
                ctx.Fx?.DustCloud(ctx.Ground(0.3f * r), 0.8f * s, SkillColors.Dust, 5);
                RedSkills.Sparks(ctx, ctx.Ground(0.9f * r), RedSkills.GlowColor, 6, 4f, 0.12f, 0.3f, 6f, Vector2.up);
            }

            yield return Wait(0.05f);
            ctx.HitBox(RedSkills.Spot(ctx, 1f, 1.5f), new Vector2(2.1f * s, 3f * s), damage,
                new HitEffect { knockback = new Vector2(1f, launch), stun = 0.3f }, RedSkills.Color);

            // bay lên theo rồi đáp
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                if (t > 0.15f && ctx.Grounded)
                    break;
                yield return null;
            }

            if (ctx.Grounded)
                ctx.Fx?.DustCloud(ctx.Ground(0f), 0.6f * s, SkillColors.Dust, 3);
        }
    }

    /// <summary>
    /// Nút 1 · ↓ — Đánh thường áp sát (tấm cùng tên): lướt thấp tới trước để lại dải tốc độ đỏ (khung 2–3), cuối đường lướt
    /// chém vòng NGANG tới trước (khung 4), trượt dừng tung bụi (khung 5). Trúng mọi kẻ địch trên đường lướt và trước mặt.
    /// </summary>
    [Serializable]
    public class RedDashSlash : Skill
    {
        public float damage = 26f;
        [Tooltip("Quãng lướt (unit)")]
        public float distance = 3.2f;
        public float dashTime = 0.2f;
        [Tooltip("Cỡ HÌNH vệt chém (x = dài tới trước, y = bề cao) theo Cỡ / tầm chiêu: 0 = đúng cỡ tranh, 1 = to bằng vùng đánh")]
        public Vector2 fxGrow = new Vector2(0.9f, 0.35f);

        public RedDashSlash() => cooldown = 1.2f;

        public override IEnumerator Run(SkillContext ctx)
        {
            const float duration = 0.62f;
            Vector2 fwd = ctx.Forward;
            float s = ctx.Size, r = ctx.Range;
            Vector2 start = RedSkills.Spot(ctx, 0f, 1f);
            Vector2 k = RedSkills.FxScale(ctx, fxGrow);
            ctx.Act(CombatPose.Lunge, duration, ctx.Chest + fwd * 2f);
            // lướt xa × tầm trong cùng thời gian (lướt nhanh hơn)
            if (ctx.Hero != null)
                ctx.Hero.Dash(fwd, distance * r, dashTime, 0f, true, RedSkills.SlashColor);
            if (ctx.Grounded)
                ctx.Fx?.DustCloud(ctx.Ground(-0.2f * r), 0.6f * s, SkillColors.Dust, 4);
            // dải tốc độ đỏ của tấm bám sau lưng (mép phải dải = lưng RED), vẽ sau người
            Func<Vector2> back = () => RedSkills.SwingCenter(ctx) - fwd * 0.15f;
            RedFx.Instance.Pop(RedFx.DashLinesSprite, back(), k.y, 0.26f, ctx.Facing, RedFx.BackOrder, 0.05f, 0.7f, glow: 0.4f,
                fadeIn: 0.03f, fadeOut: 0.12f, follow: back, stretchX: k.x / k.y * 1.3f);
            float strike = RedSkills.StrikeTime(CombatPose.Lunge, duration);
            for (float t = 0f; t < strike - 0.04f; t += Time.deltaTime)
            {
                ctx.Fx?.SpeedLines(ctx.Chest + Vector2.down * 0.3f, fwd * (16f * r), RedSkills.SlashColor); // vệt tốc độ đỏ sát đất
                yield return null;
            }

            // khung 4: chém vòng ngang tới trước (vệt của chính tấm áp sát, không dùng lưỡi liềm dọc của hất tung)
            RedSkills.SheetSlash(ctx, RedFx.DashSlashSprite, RedFx.DashSlashCore, RedSkills.SwingCenter(ctx), k, 0.26f, -4f, 35f);
            yield return Wait(0.04f);
            Vector2 end = RedSkills.Spot(ctx, 0f, 1f);
            ctx.HitSegment(start, end + fwd * (1.3f * r), 0.9f * s, damage, new HitEffect { knockback = new Vector2(3f, 1.5f) },
                RedSkills.Color);
            RedSkills.ReachSpark(ctx, RedSkills.SwingCenter(ctx) + fwd * (1.3f * r));

            // trượt dừng: bụi tung về phía sau
            Vector2 feet = ctx.Ground(0f);
            if (ctx.Grounded)
            {
                RedFx.Instance.Pop(RedFx.DustSprite, feet + fwd * (0.2f * r), RedSkills.FxSize(ctx, 0.6f), 0.55f, ctx.Facing, RedFx.BackOrder,
                    0.15f, 0.4f, glow: 0f, fadeOut: 0.35f, velocity: -fwd * (0.8f * s));
                ctx.Fx?.DustCloud(feet, 0.8f * s, SkillColors.Dust, 5);
            }

            yield return Wait(duration - strike - 0.04f);
        }
    }

    /// <summary>
    /// Nút 2 · thường — Kĩ năng (tấm cùng tên): kiếm tụ lực đỏ rực, tia sét đỏ quanh kiếm (khung 1), xoay người: MỘT vòng chém
    /// (khung 3) → NHIỀU vòng xoáy (khung 4) → nổ vòng tinh thể (khung 5). Vòng nào cũng tách nửa sau lưng / nửa trước người nên
    /// RED vẫn đọc được ở giữa. Đánh mọi kẻ địch quanh mình.
    /// </summary>
    [Serializable]
    public class RedSpinBurst : Skill
    {
        public float spinDamage = 16f;
        public float burstDamage = 32f;
        public float radius = 2.1f;
        [Tooltip("Tụ lực bấy nhiêu giây trước khi xoay")]
        public float charge = 0.35f;
        [Tooltip("Cỡ HÌNH vòng chém / vòng nổ theo Cỡ chiêu (x = vòng chém, y = vòng nổ): 0 = đúng cỡ tranh, 1 = to bằng " +
                 "vùng đánh (vùng đánh thật được cho thấy bằng sóng tròn mờ)")]
        public Vector2 fxGrow = new Vector2(0.7f, 0.5f);

        public RedSpinBurst() => cooldown = 4f;

        public override IEnumerator Run(SkillContext ctx)
        {
            const float spin = 0.7f;
            CombatFx fx = ctx.Fx;
            RedFx red = RedFx.Instance;
            Vector2 fwd = ctx.Forward;
            float s = ctx.Size;
            float rad = radius * s; // bán kính vùng xoay × cỡ chiêu
            float ring = RedSkills.FxSize(ctx, fxGrow.x);
            ctx.Hero?.Root(charge + spin + 0.15f);
            ctx.Combat.Linger(RedSkills.WeaponGlow(ctx, 0f, 1f, charge));
            fx?.Flash(ctx.Chest, 1.3f * RedSkills.FxSize(ctx, 0.5f), RedSkills.GlowColor, charge + 0.1f, true);
            for (float t = 0f; t < charge; t += 0.09f)
            {
                // khung 1: tia sét đỏ (cắt từ tấm) chớp quanh mũi kiếm, kèm một vệt sét mảnh dồn về kiếm
                Vector2 tip = ctx.WeaponTip;
                red.Pop(RedFx.SpinBoltSprite, tip + UnityEngine.Random.insideUnitCircle * 0.35f, RedSkills.FxSize(ctx, 0.4f), 0.12f,
                    UnityEngine.Random.value < 0.5f ? 1f : -1f, RedFx.FrontOrder, 0.04f, 0.7f, glow: 0.6f, fadeIn: 0.01f, fadeOut: 0.06f,
                    angle: UnityEngine.Random.Range(-40f, 40f));
                if (fx != null)
                {
                    Vector2 around = RedSkills.Feet(ctx) + new Vector2(UnityEngine.Random.Range(-0.9f, 0.9f),
                        1f + UnityEngine.Random.Range(-0.7f, 1f));
                    fx.Streak(around, tip, RedSkills.SlashColor, RedSkills.EdgeColor, 0.05f, 0.12f);
                    RedSkills.Sparks(ctx, tip, RedSkills.HotColor, 2, 2f, 0.08f, 0.25f, -1f);
                }

                yield return Wait(0.09f);
            }

            ctx.Act(CombatPose.Spin, spin);
            float from = RedSkills.StrikeTime(CombatPose.Spin, spin);
            yield return Wait(from - 0.02f);
            // khung 3: một vòng chém quanh thân (nửa sau vẽ sau lưng, nửa trước vẽ trước người)
            Vector2 c = RedSkills.Spot(ctx, 0f, 0.5f);
            Vector2 up = Vector2.up * s;
            Vector2 body = RedSkills.SwingCenter(ctx);
            red.Pop(RedFx.SpinRingBack, body, ring, 0.26f, ctx.Facing, RedFx.BackOrder, 0.08f, 0.55f, glow: 0.5f, fadeIn: 0.02f,
                fadeOut: 0.12f, squashY: 0.9f);
            red.Pop(RedFx.SpinRingFront, body, ring, 0.26f, ctx.Facing, RedFx.FrontOrder, 0.08f, 0.55f, glow: 0.5f, fadeIn: 0.02f,
                fadeOut: 0.12f, squashY: 0.9f);
            fx?.Shockwave(ctx.Ground(0f), rad, RedSkills.ReachColor, 0.35f); // vùng xoay thật: sóng dẹt mờ trên mặt đất
            ctx.HitCircle(c + up * 0.2f, rad, spinDamage, new HitEffect { knockback = new Vector2(1.5f, 0.6f) }, RedSkills.Color);
            yield return Wait(0.2f);
            // khung 4: nhiều vòng xoáy quanh thân (quay mặt ngược lại: vòng như đang xoáy tiếp)
            body = RedSkills.SwingCenter(ctx);
            red.Pop(RedFx.SpinRingsBack, body, ring, 0.3f, -ctx.Facing, RedFx.BackOrder, 0.08f, 0.8f, glow: 0.5f, fadeIn: 0.03f,
                fadeOut: 0.14f);
            red.Pop(RedFx.SpinRingsFront, body, ring, 0.3f, -ctx.Facing, RedFx.FrontOrder, 0.08f, 0.8f, glow: 0.5f, fadeIn: 0.03f,
                fadeOut: 0.14f);
            ctx.HitCircle(c + up * 0.2f, rad, spinDamage, new HitEffect { knockback = new Vector2(1.5f, 0.6f) }, RedSkills.Color);
            yield return Wait(0.2f);

            // khung 5: nổ vòng tinh thể quanh người (pivot = chân): vụ nổ sau lưng, tia / đá toé dưới chân vẽ trước
            Vector2 chest = RedSkills.Spot(ctx, 0f, 1f);
            Vector2 feet = RedSkills.GroundAt(ctx, ctx.Chest.x);
            float burst = RedSkills.FxSize(ctx, fxGrow.y);
            red.Pop(RedFx.CrystalRingBack, feet, burst, 0.55f, ctx.Facing, RedFx.BackOrder, 0.12f, 0.45f, glow: 0.6f, fadeOut: 0.3f);
            red.Pop(RedFx.CrystalRingFront, feet, burst, 0.5f, ctx.Facing, RedFx.FrontOrder, 0.12f, 0.45f, glow: 0.5f, fadeOut: 0.28f);
            if (fx != null)
            {
                fx.Shockwave(ctx.Ground(0f), rad * 1.1f, RedSkills.ReachColor, 0.4f); // vùng nổ thật
                fx.Flash(ctx.Chest, 1.6f * burst, RedSkills.GlowColor, 0.2f, true);
                // tia lửa toé ở hai mép vòng nổ (không chớp trắng đè lên người)
                RedSkills.Sparks(ctx, ctx.Chest + fwd * (1.4f * burst), RedSkills.HotColor, 6, 6f, 0.12f, 0.3f);
                RedSkills.Sparks(ctx, ctx.Chest - fwd * (1.4f * burst), RedSkills.HotColor, 6, 6f, 0.12f, 0.3f);
                RedSkills.Rocks(ctx, feet + Vector2.up * 0.3f, RedSkills.RockColor, 6, 6f, 0.26f, 16f);
            }

            ctx.HitCircle(chest, rad * 1.15f, burstDamage, new HitEffect { knockback = new Vector2(5f, 3.5f), heavy = true },
                RedSkills.HeavyColor);
            ctx.Combat.Linger(RedSkills.WeaponGlow(ctx, 1f, 0f, 0.3f));
            yield return Wait(0.2f);
        }
    }

    /// <summary>
    /// Nút 2 · ↑ — Kĩ năng tầm xa (tấm cùng tên): kiếm rực đỏ (khung 1), giơ kiếm lên cao (khung 2), đâm tới: cung bung NGAY
    /// mũi kiếm (khung 3) rồi thành sóng lưỡi liềm đỏ bay xa (khung 4: đầu sóng + đuôi riêng, đuôi dài dần), trúng kẻ địch đầu
    /// tiên hoặc hết tầm thì nổ tinh thể đỏ (khung 5).
    /// </summary>
    [Serializable]
    public class RedCrescentWave : Skill
    {
        public float damage = 36f;
        public float speed = 15f;
        public float range = 9f;
        [Tooltip("Bán kính vụ nổ tinh thể (unit)")]
        public float burstRadius = 1.2f;
        [Tooltip("Cỡ HÌNH sóng / nổ theo Cỡ chiêu: 0 = đúng cỡ tranh, 1 = to bằng vùng đánh")]
        public float fxGrow = 0.75f;

        public RedCrescentWave() => cooldown = 3.5f;

        public override IEnumerator Run(SkillContext ctx)
        {
            Vector2 fwd = ctx.Forward;
            float facing = ctx.Facing;
            float s = ctx.Size, r = ctx.Range;
            float v = RedSkills.FxSize(ctx, fxGrow);
            ctx.Combat.Linger(RedSkills.WeaponGlow(ctx, 0.3f, 1f, 0.3f));
            ctx.Act(CombatPose.Rear, 0.34f, ctx.Chest + fwd + Vector2.up * 1.5f);
            yield return Wait(0.14f);
            ctx.Fx?.Flash(ctx.WeaponTip, 0.9f * v, RedSkills.GlowColor, 0.25f); // kiếm giơ lên tới đỉnh: mũi kiếm loé
            yield return Wait(0.14f);

            const float thrust = 0.45f;
            ctx.Act(CombatPose.Reach, thrust, ctx.Chest + fwd * 4f);
            float strike = RedSkills.StrikeTime(CombatPose.Reach, thrust);
            yield return Wait(strike);

            // khung 3: cung bung ở mũi kiếm thật (pivot ảnh = mũi kiếm) — sóng bung ra từ đó rồi mới lên đường bay
            Vector2 tip = ctx.WeaponTip;
            RedFx.Instance.Pop(RedFx.WaveLaunchSprite, tip, 0.9f * v, 0.18f, facing, RedFx.FrontOrder, 0.05f, 0.5f, glow: 0.9f,
                fadeIn: 0.01f, fadeOut: 0.1f, core: RedFx.WaveLaunchCore, coreFade: 0.08f);
            // đường bay (vùng đánh) như cũ: × cỡ cao hơn cho đáy sóng vẫn sát đất, bay xa × tầm với tốc độ × tầm
            Vector2 from = RedSkills.Spot(ctx, 0.9f, 1f);
            ctx.Fx?.Flash(tip, 0.9f * v, RedSkills.GlowColor, 0.18f);
            float dmg = damage;
            float radius = burstRadius * s;
            CombatFx fx = ctx.Fx;
            bool exploded = false;
            void Explode(Vector2 at)
            {
                if (exploded)
                    return;
                exploded = true;
                // nổ tinh thể: gốc vụ nổ ở dưới đầu sóng (mặt đất)
                RedFx.Instance.Pop(RedFx.WaveBurstSprite, at + Vector2.down * s, 0.75f * v, 0.55f, facing, rise: true, grow: 0.12f,
                    scale0: 0.3f, glow: 0.8f, fadeOut: 0.3f);
                fx?.Flash(at, 1.3f * v, RedSkills.GlowColor, 0.22f);
                RedSkills.Sparks(ctx, at, RedSkills.HotColor, 10, 7f, 0.14f, 0.35f);
                RedSkills.Rocks(ctx, at + Vector2.down * (0.8f * s), RedSkills.RockColor, 6, 5f, 0.24f);
                ctx.HitCircle(at, radius, dmg * 0.4f, new HitEffect { knockback = new Vector2(3f, 2.5f) }, RedSkills.Color);
            }

            RedFx.Instance.Wave(RedFx.WaveSprite, RedFx.WaveTailSprite, from, facing, speed * r, range * r, 0.85f * v,
                RedSkills.SlashColor,
                at => ctx.HitCircle(at - fwd * (0.5f * s), 0.8f * s, dmg, new HitEffect { knockback = new Vector2(4f, 2f), heavy = true },
                    RedSkills.HeavyColor) > 0,
                Explode, tip);
            ctx.Combat.Linger(RedSkills.WeaponGlow(ctx, 1f, 0f, 0.3f));
            yield return Wait(thrust - strike);
        }
    }

    /// <summary>
    /// Nút 2 · ↓ — Kĩ năng thủ (tấm cùng tên): cắm kiếm xuống đất (nứt đỏ toả ra từ mũi kiếm, khung 1–2), vòng phép đỏ hiện
    /// dưới chân (khung 3; nửa sau sát đất sau người, nửa trước trước chân), cột sáng, pha lê và lửa bay lên quanh người, dày
    /// dần (khung 4–6). Trong lúc vòng phép còn: chặn mọi đòn đánh vào RED, kẻ địch đứng trong vòng mất máu liên tục.
    /// </summary>
    [Serializable]
    public class RedGuardCircle : Skill
    {
        [Tooltip("Cả thế thủ dài bấy nhiêu giây (chặn đòn suốt thời gian này)")]
        public float duration = 2f;
        public float tickDamage = 9f;
        public float tickInterval = 0.3f;
        [Tooltip("Bề ngang vòng phép (unit)")]
        public float width = 2.9f;
        [Tooltip("Cỡ HÌNH cột sáng / pha lê / nứt đất theo Cỡ chiêu (vòng phép luôn rộng bằng vùng đánh): 0 = đúng cỡ tranh")]
        public float fxGrow = 0.4f;

        public RedGuardCircle() => cooldown = 6f;

        public override IEnumerator Run(SkillContext ctx)
        {
            CombatFx fx = ctx.Fx;
            Vector2 fwd = ctx.Forward;
            float d = duration;
            float s = ctx.Size, r = ctx.Range;
            float v = RedSkills.FxSize(ctx, fxGrow);
            // vòng phép rộng × cỡ chiêu; vùng mất máu là chính vòng phép nên to theo; chặn đòn thì vẫn như cũ (chặn mọi đòn)
            float w = width * s;
            ctx.Hero?.Root(d);
            if (!ctx.Grounded)
                ctx.Hero?.Hover(d);
            ctx.Act(CombatPose.Squash, d, ctx.Ground(0.6f));
            // đòn bị chặn: PlayerHealth tự hiện chữ "CHẶN"; vòng phép loé đỏ
            PlayerHealth.Block block = ctx.BlockFor(d, hit =>
            {
                fx?.Flash(ctx.Chest, 1.2f * v, RedSkills.GlowColor, 0.2f);
                fx?.Ring(ctx.Chest, 1.2f * v, RedSkills.GlowColor, 0.25f);
            });

            float plant = RedSkills.StrikeTime(CombatPose.Squash, d);
            yield return Wait(plant);
            // cắm kiếm: nứt đất đỏ của tấm (khung 2) toả ra từ mũi kiếm, loé, tia lửa
            Vector2 feet = ctx.Hero != null ? ctx.Hero.Feet : ctx.Ground(0f);
            Vector2 tip = new Vector2(ctx.WeaponTip.x, feet.y);
            RedFx.Instance.Pop(RedFx.GuardCracksSprite, tip, 1.1f * v, d - plant + 0.2f, ctx.Facing, RedFx.GroundOrder, 0.12f, 0.3f,
                glow: 0.6f, fadeOut: 0.35f, pulse: 0.15f);
            if (fx != null)
            {
                fx.Flash(tip + Vector2.up * 0.2f, 1.1f * v, RedSkills.GlowColor, 0.25f);
                RedSkills.Sparks(ctx, tip + Vector2.up * 0.1f, RedSkills.HotColor, 10, 5f, 0.12f, 0.35f, 8f, Vector2.up);
            }

            yield return Wait(0.08f);
            Vector2 center = feet + fwd * (0.25f * r);
            float left = d - plant - 0.08f;
            RedFx.Instance.Circle(center, w, left + 0.25f);
            fx?.Shockwave(center, w * 0.5f, RedSkills.GlowColor, 0.4f);
            fx?.Flames(feet, 0.9f * v, RedSkills.GlowColor, left * 0.9f, true); // lửa quấn sau lưng
            RedFx red = RedFx.Instance;
            for (float t = 0f; t < left; t += tickInterval)
            {
                // cột sáng mảnh và pha lê bay lên quanh người, dày dần theo thời gian (khung 4 → 6)
                int pillars = 1 + Mathf.RoundToInt(2f * t / Mathf.Max(0.01f, left));
                for (int i = 0; i < pillars; i++)
                {
                    float x = UnityEngine.Random.Range(-0.48f, 0.48f) * w;
                    red.Pillar(center + new Vector2(x, UnityEngine.Random.Range(-0.1f, 0.15f)), UnityEngine.Random.Range(1.5f, 2.6f) * v,
                        0.55f, UnityEngine.Random.Range(0f, 0.15f), i % 2 == 0 ? RedFx.BackOrder : RedFx.FrontOrder);
                }

                red.Crystal(center + new Vector2(UnityEngine.Random.Range(-0.5f, 0.5f) * w, UnityEngine.Random.Range(0.4f, 1.8f) * v),
                    UnityEngine.Random.Range(0.28f, 0.45f) * v, new Vector2(0f, UnityEngine.Random.Range(0.6f, 1.3f) * v), 0.8f, 0f,
                    UnityEngine.Random.value < 0.5f);

                // kẻ địch đứng trong vòng: mất máu liên tục
                int hit = ctx.HitBox(center + Vector2.up * (1.1f * s), new Vector2(w, 2.4f * s), tickDamage,
                    new HitEffect { knockback = new Vector2(1.2f, 0.4f) }, RedSkills.Color, false);
                if (hit > 0)
                    RedSkills.Sparks(ctx, center + Vector2.up * s + fwd * (w * 0.35f), RedSkills.GlowColor, 3, 3f, 0.1f, 0.3f);
                yield return Wait(tickInterval);
            }

            block?.Cancel();
        }
    }

    /// <summary>
    /// Nút 3 · thường — Kĩ năng đặc biệt (tấm cùng tên): bùng hào quang lửa đỏ (khung 1), lao tới chém cung lửa lớn (khung 2)
    /// rồi cung thứ hai (khung 3), chém chữ X khổng lồ: nhánh "\" rồi nhánh "/" hiện riêng, mảnh vỡ / đá bung ra (khung 4), sóng
    /// xung kích quét quanh, đá văng (khung 5). Đau theo mốc nộ.
    /// </summary>
    [Serializable]
    public class RedFuryCross : Skill
    {
        public float slashDamage = 22f;
        public float crossDamage = 60f;
        public float shockDamage = 30f;
        [Tooltip("Lao tới tối đa bấy nhiêu unit (dừng trước kẻ địch gần nhất)")]
        public float dash = 3.2f;
        [Tooltip("Cỡ HÌNH cung chém (x = dài tới trước, y = bề cao) theo Cỡ / tầm chiêu: 0 = đúng cỡ tranh, 1 = to bằng vùng đánh")]
        public Vector2 fxGrow = new Vector2(0.6f, 0.4f);
        [Tooltip("Cỡ HÌNH chữ X theo Cỡ chiêu: 0 = đúng cỡ tranh, 1 = to bằng vùng đánh")]
        public float crossGrow = 0.3f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float power = ctx.Power;
            Vector2 fwd = ctx.Forward;
            CombatFx fx = ctx.Fx;
            RedFx red = RedFx.Instance;
            float s = ctx.Size, r = ctx.Range;
            Vector2 k = RedSkills.FxScale(ctx, fxGrow);
            ctx.HeroVisual?.SetWeaponPower(1f);

            // 1. hào quang lửa
            ctx.Hero?.Root(0.5f);
            ctx.Act(CombatPose.Rear, 0.55f, ctx.Chest + fwd + Vector2.up * 1.4f);
            RedSkills.Aura(ctx, 0.6f);
            yield return Wait(0.48f);

            // 2. lao tới chém lưỡi liềm: tìm mục tiêu và lao xa × tầm (cùng thời gian), dừng cách mục tiêu × tầm cho nhát chém
            // (vùng chém cũng cách xa × tầm) vẫn trúng
            Health target = ctx.NearestAhead((dash + 2f) * r, 2.5f);
            float go = target != null
                ? Mathf.Clamp(Mathf.Abs(target.transform.position.x - ctx.Center.x) - 1.3f * r, 0.4f * r, dash * r)
                : dash * 0.8f * r;
            if (ctx.Hero != null)
                ctx.Hero.Dash(fwd, go, 0.16f, 0f, true, RedSkills.SlashColor);
            const float lunge = 0.42f;
            ctx.Act(CombatPose.Lunge, lunge, ctx.Chest + fwd * 2f);
            yield return Wait(RedSkills.StrikeTime(CombatPose.Lunge, lunge) - 0.04f);
            // khung 2: cung lửa lớn ở chỗ vung kiếm thật (bám người vì còn đang lao)
            Func<Vector2> swing = () => RedSkills.SwingCenter(ctx);
            RedSkills.SheetSlash(ctx, RedFx.BigSlashSprite, RedFx.BigSlashCore, swing(), k, 0.28f, 0f, 35f, follow: swing);
            yield return Wait(0.04f);
            ctx.HitBox(RedSkills.Spot(ctx, 1.2f, 1f), new Vector2(2.6f, 2.8f) * s, slashDamage * power,
                new HitEffect { knockback = new Vector2(1.2f, 0.6f) }, RedSkills.HeavyColor);
            yield return Wait(0.1f);

            const float back = 0.36f;
            ctx.Act(CombatPose.WhipAlt, back, ctx.Chest + fwd * 2f);
            yield return Wait(RedSkills.StrikeTime(CombatPose.WhipAlt, back) - 0.04f);
            // khung 3: cung thứ hai cắt chéo lên (nối sang chữ X)
            RedSkills.SheetSlash(ctx, RedFx.SecondArcSprite, RedFx.SecondArcCore, RedSkills.SwingCenter(ctx), k, 0.28f, 0f, -30f);
            yield return Wait(0.04f);
            ctx.HitBox(RedSkills.Spot(ctx, 1.2f, 1f), new Vector2(2.6f, 2.8f) * s, slashDamage * power,
                new HitEffect { knockback = new Vector2(1.2f, 0.6f) }, RedSkills.HeavyColor);
            yield return Wait(0.08f);

            // 3. chém chữ X khổng lồ: hai nhánh hiện lần lượt, mảnh vỡ / đá bung ra
            const float slam = 0.5f;
            ctx.Act(CombatPose.Slam, slam, ctx.Ground(1.8f));
            yield return Wait(RedSkills.StrikeTime(CombatPose.Slam, slam) - 0.05f);
            Vector2 cross = RedSkills.Spot(ctx, 1.9f, 1.3f);
            float xs = RedSkills.FxSize(ctx, crossGrow);
            // nét chém mảnh vạch trước nhánh X (lõi), nhánh "\" rồi nhánh "/"
            var arm = new Vector2(fwd.x * 1.4f, 1.8f) * xs;
            var arm2 = new Vector2(arm.x, -arm.y);
            fx?.Streak(cross - arm2, cross + arm2, RedSkills.SlashColor, RedSkills.EdgeColor, 0.2f * xs, 0.16f);
            red.Pop(RedFx.XArmASprite, cross, xs, 0.55f, ctx.Facing, RedFx.FrontOrder, 0.06f, 0.6f, glow: 0.7f, fadeIn: 0.02f,
                fadeOut: 0.3f, sweep: 12f);
            yield return Wait(0.05f);
            fx?.Streak(cross - arm, cross + arm, RedSkills.SlashColor, RedSkills.EdgeColor, 0.2f * xs, 0.16f);
            red.Pop(RedFx.XArmBSprite, cross, xs, 0.5f, ctx.Facing, RedFx.FrontOrder, 0.06f, 0.6f, glow: 0.7f, fadeIn: 0.02f,
                fadeOut: 0.3f, sweep: -12f);
            red.Pop(RedFx.XDebrisSprite, cross, xs, 0.6f, ctx.Facing, RedFx.FrontOrder - 1, 0.14f, 0.7f, glow: 0.3f, fadeIn: 0.03f,
                fadeOut: 0.35f, delay: 0.03f);
            if (fx != null)
            {
                fx.Flash(cross, 1.8f * xs, RedSkills.GlowColor, 0.25f);
                RedSkills.Sparks(ctx, cross, RedSkills.HotColor, 14, 9f, 0.16f, 0.4f);
                RedSkills.Rocks(ctx, cross, RedSkills.RockColor, 10, 8f, 0.3f, 14f);
                fx.Cracks(ctx.Ground(1.9f * r), 1.6f * s, RedSkills.GlowColor, 0.9f);
            }

            ctx.HitBox(cross, new Vector2(3.4f, 3.6f) * s, crossDamage * power,
                new HitEffect { knockback = new Vector2(3f, 4f), stun = 0.6f, heavy = true }, RedSkills.HeavyColor);
            yield return Wait(0.28f);

            // 4. sóng xung kích quét quanh, đá văng (khung 5)
            Vector2 ground = ctx.Ground(1.4f * r);
            if (fx != null)
            {
                fx.Shockwave(ground, 3.4f * s, RedSkills.GlowColor, 0.5f);
                fx.Ring(RedSkills.Spot(ctx, 1.2f, 1f), 3f * s, RedSkills.ReachColor, 0.4f);
                RedSkills.Rocks(ctx, ground, RedSkills.RockColor, 10, 7f, 0.3f);
                fx.DustCloud(ground, 1.6f * s, SkillColors.Dust, 6);
            }

            ctx.HitCircle(RedSkills.Spot(ctx, 1.3f, 0.5f), 3.2f * s, shockDamage * power,
                new HitEffect { knockback = new Vector2(6f, 4f), heavy = true }, RedSkills.HeavyColor);
            ctx.Combat.Linger(RedSkills.WeaponGlow(ctx, 1f, 0f, 0.4f));
            yield return Wait(0.3f);
        }
    }

    /// <summary>
    /// Nút 3 · ↑ — Kĩ năng đặc biệt tầm xa (tấm cùng tên): bùng hào quang (khung 1), giơ kiếm tụ lực (khung 2), đâm tới bung
    /// lưỡi liềm khổng lồ ở mũi kiếm thật (khung 3), sóng lưỡi liềm khổng lồ bay rất xa (khung 4: đầu sóng lõi trắng + đuôi lửa
    /// đỏ đen riêng, dài dần), trúng kẻ địch hoặc hết tầm thì nổ lớn, cột đá đỏ trồi lên (khung 5). Đau theo mốc nộ.
    /// </summary>
    [Serializable]
    public class RedGrandWave : Skill
    {
        public float waveDamage = 40f;
        public float burstDamage = 70f;
        public float speed = 20f;
        public float range = 16f;
        public float burstRadius = 2.4f;
        [Tooltip("Cỡ HÌNH sóng / nổ theo Cỡ chiêu: 0 = đúng cỡ tranh, 1 = to bằng vùng đánh")]
        public float fxGrow = 0.75f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float power = ctx.Power;
            float facing = ctx.Facing;
            Vector2 fwd = ctx.Forward;
            CombatFx fx = ctx.Fx;
            float s = ctx.Size, r = ctx.Range;
            float v = RedSkills.FxSize(ctx, fxGrow);
            ctx.HeroVisual?.SetWeaponPower(1f);
            ctx.Hero?.Root(1.1f);
            if (!ctx.Grounded)
                ctx.Hero?.Hover(1.1f);

            ctx.Act(CombatPose.Rear, 0.55f, ctx.Chest + fwd + Vector2.up * 1.5f);
            RedSkills.Aura(ctx, 0.8f);
            yield return Wait(0.22f);
            fx?.Flash(ctx.WeaponTip, 1.2f * v, RedSkills.GlowColor, 0.4f); // kiếm giơ lên tới đỉnh: mũi kiếm loé
            yield return Wait(0.28f);

            const float thrust = 0.5f;
            ctx.Act(CombatPose.Reach, thrust, ctx.Chest + fwd * 6f);
            yield return Wait(RedSkills.StrikeTime(CombatPose.Reach, thrust));

            // khung 3: lưỡi liềm khổng lồ bung ngay mũi kiếm thật; sóng bung ra từ đó rồi lên đường bay (đường bay = vùng đánh
            // như cũ: × cỡ cao hơn cho đáy sóng vẫn sát đất, bay xa × tầm với tốc độ × tầm)
            Vector2 tip = ctx.WeaponTip;
            Vector2 from = RedSkills.Spot(ctx, 1.3f, 1f);
            RedFx.Instance.Pop(RedFx.BigWaveLaunchSprite, tip, 0.8f * v, 0.26f, facing, RedFx.FrontOrder, 0.07f, 0.45f, glow: 0.9f,
                fadeIn: 0.01f, fadeOut: 0.16f, core: RedFx.BigWaveLaunchCore, coreFade: 0.1f);
            fx?.Flash(tip, 1.6f * v, RedSkills.GlowColor, 0.22f);
            bool done = false;
            float waveSpeed = speed * r, waveRange = range * r;
            RedFx.Instance.Wave(RedFx.BigWaveSprite, RedFx.BigWaveTailSprite, from, facing, waveSpeed, waveRange, 0.95f * v,
                RedSkills.SlashColor,
                at => ctx.HitBox(at - fwd * (0.7f * s), new Vector2(1.6f, 2.6f) * s, waveDamage * power,
                    new HitEffect { knockback = new Vector2(2f, 1f), heavy = true }, RedSkills.HeavyColor) > 0,
                at =>
                {
                    done = true;
                    // nổ lớn: cột đá đỏ trồi lên từ mặt đất dưới chỗ nổ
                    Vector2 foot = at + Vector2.down * s;
                    RedFx.Instance.Pop(RedFx.BigBurstSprite, foot, 0.95f * v, 0.75f, facing, RedFx.FrontOrder, 0.14f, 0.25f, true,
                        0.8f, fadeOut: 0.4f);
                    if (fx != null)
                    {
                        fx.GroundSlam(foot, 2f * s, RedSkills.GlowColor, RedSkills.RockColor, 10, 1.2f);
                        fx.Flash(at, 2.2f * v, RedSkills.GlowColor, 0.3f);
                        RedSkills.Sparks(ctx, at, RedSkills.HotColor, 14, 9f, 0.16f, 0.4f);
                    }

                    ctx.HitCircle(at, burstRadius * s, burstDamage * power,
                        new HitEffect { knockback = new Vector2(5f, 6f), stun = 1f, heavy = true }, RedSkills.HeavyColor);
                }, tip, 1.9f);

            // chờ sóng nổ (thấy được cả đường bay), tối đa hết tầm
            for (float t = 0f; t < waveRange / waveSpeed + 0.3f && !done; t += Time.deltaTime)
                yield return null;
            ctx.Combat.Linger(RedSkills.WeaponGlow(ctx, 1f, 0f, 0.4f));
            yield return Wait(0.25f);
        }
    }

    /// <summary>
    /// Nút 3 · ↓ — Kĩ năng đặc biệt áp chế (tấm cùng tên): chém tới (khung 1), phóng 3 sợi xích đỏ từ tay trói kẻ địch gần
    /// nhất phía trước ở đầu / ngực / chân (khung 2, xích mảnh theo tranh, không phình theo cỡ chiêu), kéo nó lại (khung 3), đấm
    /// (khung 4: chớp cú đấm riêng), nhấc lên giam trong lồng xích tách lớp (khung 5–6: vòng xích trên đầu, 3 vòng quấn thân nửa
    /// trước / nửa sau, gai, vòng phép dưới chân): choáng lâu + mất máu liên tục. Boss bị giam ngắn hơn (boss chỉ chịu một phần
    /// thời gian choáng). Đau theo mốc nộ.
    /// </summary>
    [Serializable]
    public class RedChainPrison : Skill
    {
        public float slashDamage = 14f;
        public float bindDamage = 18f;
        public float punchDamage = 26f;
        [Tooltip("Sát thương mỗi nhịp trong lồng")]
        public float cageDamage = 10f;
        public float cageInterval = 0.4f;
        [Tooltip("Giam (choáng) bấy nhiêu giây ở mốc nộ 1; mỗi mốc thêm 0.5 giây")]
        public float cageTime = 3.5f;
        public float range = 7f;
        [Tooltip("Bề dày sợi xích (unit) — như tranh, không nhân theo cỡ chiêu (×2 làm 3 sợi dính thành một bó)")]
        public float chainWidth = 0.13f;

        public override IEnumerator Run(SkillContext ctx)
        {
            float power = ctx.Power;
            int tier = Mathf.Max(1, ctx.Tier);
            float facing = ctx.Facing;
            Vector2 fwd = ctx.Forward;
            CombatFx fx = ctx.Fx;
            RedFx red = RedFx.Instance;
            float s = ctx.Size, r = ctx.Range;
            ctx.HeroVisual?.SetWeaponPower(0.8f);

            // 1. chém tới (tấm không có vệt riêng cho nhát này: vệt procedural mảnh ở chỗ vung kiếm thật)
            const float whip = 0.36f;
            ctx.Act(CombatPose.Whip, whip, ctx.Chest + fwd * 2f);
            yield return Wait(RedSkills.StrikeTime(CombatPose.Whip, whip) - 0.04f);
            RedSkills.Crescent(ctx, RedSkills.SwingCenter(ctx), 1.4f * RedSkills.FxSize(ctx, 0.5f), 70f, -35f, 0.45f, 0.24f);
            yield return Wait(0.04f);
            ctx.HitBox(RedSkills.Spot(ctx, 1.1f, 0.95f), new Vector2(2.3f, 2f) * s, slashDamage * power,
                new HitEffect { knockback = new Vector2(1f, 0.3f) }, RedSkills.Color);
            yield return Wait(0.12f);

            // 2. phóng xích từ tay: tầm tìm mục tiêu × tầm chiêu; xích mảnh như tranh, 3 sợi bám 3 chỗ trên thân (đọc rõ từng sợi)
            float reachRange = range * r;
            Health target = ctx.NearestAhead(reachRange, 3f);
            const float reach = 0.6f;
            float cw = chainWidth;
            if (target == null)
            {
                Vector2 miss = ctx.CastPoint + fwd * (reachRange * 0.7f);
                ctx.Act(CombatPose.Reach, reach * 0.7f, miss);
                yield return Wait(0.08f);
                red.Chain(() => ctx.CastPoint, () => miss + Vector2.up * 0.3f, cw, 0.3f);
                red.Chain(() => ctx.CastPoint, () => miss + Vector2.down * 0.3f, cw * 0.9f, 0.28f, null, 0.03f);
                yield return Wait(0.4f);
                ctx.HeroVisual?.SetWeaponPower(0f);
                yield break;
            }

            LionBoss lion = target.GetComponentInParent<LionBoss>();
            Collider2D body = lion == null ? target.GetComponentInChildren<Collider2D>() : null;
            bool boss = lion != null;
            Func<Vector2> aim = () => RedSkills.AimAt(target, lion, facing);
            Func<Bounds> bounds = () => RedSkills.BodyOf(target, lion, body);
            Func<bool> held = () => target != null && target.IsAlive;
            ctx.Act(CombatPose.Reach, reach, aim());
            yield return Wait(0.08f);
            float chainLife = 1.15f;
            float half = Mathf.Min(bounds().extents.y, 1.2f);
            red.Chain(() => ctx.CastPoint + Vector2.up * 0.06f, () => aim() + new Vector2(0f, half * 0.6f), cw, chainLife, held);
            red.Chain(() => ctx.CastPoint, () => aim(), cw * 0.95f, chainLife, held, 0.03f);
            red.Chain(() => ctx.CastPoint + Vector2.down * 0.06f, () => aim() + new Vector2(0f, -half * 0.6f), cw * 0.9f, chainLife, held, 0.06f);
            yield return Wait(0.12f);

            // 3. trói, kéo lại (bia tập dừng ngay trước mặt; boss da cứng không bị kéo)
            Vector2 at = aim();
            float distance = Mathf.Abs(target.transform.position.x - ctx.Center.x);
            fx?.Flash(at, 0.9f * RedSkills.FxSize(ctx, 0.4f), RedSkills.GlowColor, 0.2f);
            RedSkills.Sparks(ctx, at, RedSkills.HotColor, 8, 5f, 0.12f, 0.3f);
            DealTo(ctx, target, bindDamage * power, new HitEffect { knockback = new Vector2(-Mathf.Max(0f, distance - 1.4f) * 12f, 1f), stun = 1f },
                false);
            yield return Wait(0.38f);

            // 4. đấm: chớp cú đấm của tấm (khung 4) ở chỗ nắm tay chạm mục tiêu
            const float punch = 0.36f;
            ctx.Act(CombatPose.Lunge, punch, aim());
            yield return Wait(RedSkills.StrikeTime(CombatPose.Lunge, punch));
            if (target.IsAlive)
            {
                at = aim();
                Vector2 fist = new Vector2(Mathf.Lerp(ctx.CastPoint.x, at.x, 0.7f), Mathf.Lerp(ctx.CastPoint.y, at.y, 0.5f));
                red.Pop(RedFx.PunchFlashSprite, fist, 1.7f * RedSkills.FxSize(ctx, 0.3f), 0.2f, facing, RedFx.FrontOrder + 2, 0.05f, 0.4f,
                    glow: 1f, fadeIn: 0.01f, fadeOut: 0.12f, angle: UnityEngine.Random.Range(-15f, 15f));
                ctx.Fx?.Burst(fist + fwd * 0.3f, RedSkills.GlowColor, 6, 6f, 0.1f, 0.22f, 2f, fwd);
                // đấm tại chỗ (giật nhẹ về: vẫn đứng ngay trước mặt để giam)
                DealTo(ctx, target, punchDamage * power, new HitEffect { knockback = new Vector2(-6f, 1.5f), stun = 1f, heavy = true }, true);
            }

            yield return Wait(0.06f);

            // 5. nhấc lên, giam trong lồng xích: choáng lâu + mất máu liên tục
            if (target.IsAlive)
            {
                float stun = cageTime + 0.5f * (tier - 1);
                // nhấc bổng lên (giữ ngay trước mặt) và giam
                float drift = Mathf.Max(0f, Mathf.Abs(target.transform.position.x - ctx.Center.x) - 1.4f);
                DealTo(ctx, target, cageDamage * power,
                    new HitEffect { knockback = new Vector2(-HoldPull(drift), boss ? 0f : 3.5f), stun = stun }, true);
                // thời gian giam thật: boss chỉ chịu một phần thời gian choáng (LionBoss.stunScale)
                float life = lion != null ? Mathf.Max(0.8f, lion.StunRemaining) : stun;
                // lồng vừa thân mục tiêu (không nhân cỡ); lớp sau vẽ sau mọi phần hình của mục tiêu
                red.Cage(bounds, life, held, BackOrderBehind(lion != null ? lion.gameObject : target.gameObject));
                Bounds b = bounds();
                float ring = Mathf.Min(b.size.x * s, Mathf.Max(b.size.x, 3f));
                fx?.Flash(new Vector2(b.center.x, b.min.y + 0.2f), 1.5f * RedSkills.FxSize(ctx, 0.4f), RedSkills.GlowColor, 0.3f);
                fx?.Shockwave(new Vector2(b.center.x, b.min.y), ring, RedSkills.GlowColor, 0.4f);
                ctx.Combat.Linger(CageTicks(ctx, target, bounds, life, cageDamage * power, boss));
            }

            ctx.Combat.Linger(RedSkills.WeaponGlow(ctx, 0.8f, 0f, 0.3f));
            yield return Wait(0.2f);
        }

        // Thứ tự vẽ cho các lớp lồng phía sau: sau mọi phần hình của mục tiêu (sư tử vẽ ở dải thấp hơn bia tập)
        static int BackOrderBehind(GameObject target)
        {
            int order = RedFx.BackOrder;
            foreach (SpriteRenderer sr in target.GetComponentsInChildren<SpriteRenderer>())
                if (sr.enabled)
                    order = Mathf.Min(order, sr.sortingOrder - 2);
            return Mathf.Max(order, -60);
        }

        // Cú giật giữ mục tiêu trong lồng (unit/giây): đủ thắng lò xo kéo về của bia tập (bia tự dừng ở vạch trước mặt nên giật dư
        // không sao). drift = đã trôi khỏi chỗ lồng bao xa
        static float HoldPull(float drift) => 12f + drift * 20f;

        // Sát thương lên đúng một mục tiêu (không lan sang kẻ khác đứng gần)
        static void DealTo(SkillContext ctx, Health target, float damage, HitEffect effect, bool heavy)
        {
            if (target == null || !target.IsAlive)
                return;
            effect.heavy = heavy;
            float away = Mathf.Sign(target.transform.position.x - ctx.Center.x + 1e-3f * ctx.Facing);
            effect.knockback.x *= away;
            float dealt = target.TakeHit(damage, ctx.Center, effect);
            DamageNumbers.Show(SkillContext.Aim(target) + Vector2.up * 0.7f, dealt, RedSkills.HeavyColor, heavy, target, 1.15f);
            if (dealt > 0f && heavy)
            {
                HitStop.Hit(true, true, ctx.Power, dealt);
                CameraShake.Hit(new Vector2(away, 0.3f), true, true, ctx.Power);
            }
        }

        // Trong lồng: mất máu đều, tia lửa đỏ toé ra từ xích. Lồng giữ mục tiêu tại chỗ bị giam ngay trước mặt RED: bia tập có lò
        // xo kéo về chỗ cũ nên hễ trôi ra là giật về (giật không sát thương; bia tự dừng ở vạch trước mặt người kéo). Boss da cứng
        // không bị dời nên không cần giữ.
        IEnumerator CageTicks(SkillContext ctx, Health target, Func<Bounds> bounds, float life, float damage, bool boss)
        {
            float x = target.transform.position.x;
            float side = Mathf.Sign(x - ctx.Center.x + 1e-3f * ctx.Facing);
            float cageX = ctx.Center.x + side * Mathf.Min(1.4f, Mathf.Abs(x - ctx.Center.x)); // ngay trước mặt RED
            var anchor = new Vector2(cageX - side * 1.4f, target.transform.position.y);
            float nextTick = cageInterval, nextPull = 0f;
            for (float t = 0f; t < life; t += Time.deltaTime)
            {
                if (target == null || !target.IsAlive)
                    yield break;
                float drift = (target.transform.position.x - cageX) * side;
                if (!boss && t >= nextPull && drift > 0.06f)
                {
                    target.TakeHit(0f, anchor, new HitEffect { knockback = new Vector2(-side * HoldPull(drift), 0f) });
                    nextPull = t + 0.08f;
                }

                if (t >= nextTick)
                {
                    nextTick += cageInterval;
                    float dealt = target.TakeHit(damage, anchor, default);
                    DamageNumbers.Show(SkillContext.Aim(target) + Vector2.up * 0.7f, dealt, RedSkills.Color, false, target);
                    Bounds b = bounds();
                    RedSkills.Sparks(ctx, new Vector2(b.center.x + UnityEngine.Random.Range(-0.4f, 0.4f) * b.size.x,
                        b.center.y + UnityEngine.Random.Range(-0.3f, 0.3f) * b.size.y), RedSkills.GlowColor, 3, 3f, 0.1f, 0.3f);
                }

                yield return null;
            }
        }
    }
}
