using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Octopus
{
    // =====================================================================================================================
    // BLUE — bộ 9 chiêu, làm đúng theo 9 tấm dáng chiêu của người dùng (Docs/nhan_vat_moi/blue/{basic_attack,skill,
    // special_skill}, 6–7 khung mỗi tấm). Thứ tự trong mảng: nút 1 thường / ↑ / ↓, nút 2 thường / ↑ / ↓, nút 3 thường / ↑ / ↓
    // (↑ / ↓ = kéo cần điều hướng lên / xuống khi bấm nút). Nút 3 cần nộ qua mốc 1. Tên chiêu = tên file ảnh.
    //  1 Đánh thường: combo 3 nhát quét lưỡi liềm lửa xanh · 1↑ hất tung: quét thấp, hất lên, bay lên trong cột lửa ·
    //  1↓ áp sát: lướt có vệt lửa, quét lưỡi liềm lớn, trượt dừng · 2 Dùng chiêu: xoay vòng lửa quanh thân rồi quét mạnh ·
    //  2↑ tầm xa: vòng phép + cầu sáng, phóng sóng lưỡi liềm bay xa nổ tinh thể · 2↓ thủ: vòng phép dưới chân, lửa + đầu lâu +
    //  tinh thể quanh người, chặn đòn, đốt kẻ địch sát bên · 3 Kĩ năng đặc biệt: xoáy chém bay lên rồi giáng xuống nổ tinh
    //  thể · 3↑ tầm xa: tụ cầu xoáy, quét ra luồng lửa khổng lồ chạy dọc đất, nổ tinh thể ở cuối · 3↓ áp chế: phóng xích trói
    //  kẻ địch gần nhất phía trước, giam trong lồng xích (choáng lâu + mất máu liên tục; boss bị giam ngắn hơn).
    // Hiệu ứng: ảnh cắt từ CHÍNH tấm của từng chiêu (BlueFx, Resources/HeroFx/Blue), tách lớp (thân, lõi trắng, trước / sau
    // người, đuôi, tàn lửa, bụi, đá, nứt đất) và diễn theo thứ tự các khung của tấm; hạt nhỏ (tia lửa, đá văng, vệt gió) của
    // CombatFx. Vệt chém neo ở ngực, xoay theo mũi lưỡi hái thật (BlueKit.SlashArc), không đặt theo tâm người + khoảng cách.
    // Cỡ / tầm chiêu (Combat Controller › Cỡ / tầm chiêu, ô của BLUE): mọi số trong chiêu ghi ở ×1. Lúc dùng, khoảng cách tới
    // trước mặt (tầm đánh, quãng lướt, quãng + tốc độ sóng bay, tầm tìm mục tiêu) nhân ctx.Range; bán kính / khung đánh trúng
    // và cỡ hiệu ứng nhân ctx.Size. Không nhân: chỗ đặt trên người mục tiêu (lồng giam dựng quanh mục tiêu), độ cao bật lên,
    // sát thương, thời gian, choáng. Hình đứng quanh người (lốc, hào quang, xoáy) chỉ to thêm một phần theo cỡ chiêu
    // (BlueKit.Visual, field fxScale mỗi chiêu) để không vùi nhân vật; vệt chém có lớp vọng nở từ lưỡi hái ra phủ vùng đánh.
    // Các hàm hiệu ứng của BlueKit (SlashArc, CrystalBurst, Sparks, Shards, Spray, Embers) nhận số ở ×1 và tự nhân cỡ.
    // Màn chơi dựng lại (menu Octopus → Dựng lại màn chơi) thì CombatBuilder điền BlueSkills.Default() vào Combat Controller.
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
    // =====================================================================================================================

    public static class BlueSkills
    {
        /// <summary>Màu chủ đạo: lửa hồn xanh (số sát thương, chớp sáng).</summary>
        public static readonly Color Color = new Color(0.3f, 0.78f, 1f);

        public static Skill[] Default() => new Skill[]
        {
            // nút 1 · thường / ↑ / ↓
            new BlueReapCombo(),
            new BlueRisingReap(),
            new BlueReaperDash(),
            // nút 2 · thường / ↑ / ↓
            new BlueSoulWhirl(),
            new BlueCrescentWave(),
            new BlueSoulGuard(),
            // nút 3 · thường / ↑ / ↓ (cần nộ)
            new BlueReaperStorm(),
            new BlueSoulTide(),
            new BlueSoulPrison(),
        };
    }

    /// <summary>Màu, vệt chém lửa xanh, tìm / đánh mục tiêu dùng chung cho 9 chiêu của BLUE.</summary>
    static class BlueKit
    {
        public static readonly Color Main = BlueSkills.Color;
        /// <summary>Thân vệt chém lửa xanh (đậm hơn màu số cho nổi trên nền hang)</summary>
        public static readonly Color Slash = new Color(0.16f, 0.58f, 1f);
        /// <summary>Mép trong vệt chém: xanh lam đậm</summary>
        public static readonly Color Edge = new Color(0.05f, 0.18f, 0.85f);
        /// <summary>Lửa hồn xanh</summary>
        public static readonly Color Flame = new Color(0.2f, 0.52f, 1f);
        /// <summary>Lõi trắng xanh của lửa, tia lửa</summary>
        public static readonly Color Hot = new Color(0.72f, 0.95f, 1f);
        /// <summary>Tinh thể đen ánh xanh (đá văng, cọc tinh thể)</summary>
        public static readonly Color Crystal = new Color(0.14f, 0.17f, 0.32f);
        /// <summary>Bụi xám lúc trượt dừng</summary>
        public static readonly Color Dust = new Color(0.42f, 0.44f, 0.52f, 0.6f);

        static readonly Collider2D[] s_hits = new Collider2D[32];
        static readonly List<Health> s_found = new List<Health>(16);
        static int s_floorMask = -1;

        public static BlueFx Fx => BlueFx.Instance;

        /// <summary>Lớp sàn / vách (không tính tảng đá: nhân vật người nhảy xuyên qua đá).</summary>
        public static int FloorMask => s_floorMask >= 0 ? s_floorMask : s_floorMask = LayerMask.GetMask("Default");

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>
        /// Điểm cách c dx unit tới trước mặt, cao hơn c dy unit. dx ghi ở ×1, nhân tầm chiêu (ctx.Range); dy không nhân.
        /// </summary>
        public static Vector2 Ahead(SkillContext ctx, Vector2 c, float dx, float dy = 0f) =>
            new Vector2(c.x + ctx.Facing * dx * ctx.Range, c.y + dy);

        /// <summary>
        /// Cỡ hình hiệu ứng theo cỡ chiêu: 1 + (ctx.Size − 1) × share. share = 1: hình to đúng như vùng đánh; nhỏ hơn: hình chỉ
        /// lớn thêm một phần (không vùi nhân vật) — vùng đánh vẫn nhân đủ ctx.Size. Mỗi chiêu có field fxScale chỉnh share.
        /// </summary>
        public static float Visual(SkillContext ctx, float share) => 1f + (ctx.Size - 1f) * Mathf.Max(0f, share);

        /// <summary>Thân nhân vật (hiệu ứng bám theo khi lướt / bay); null nếu không có.</summary>
        public static Transform BodyOf(SkillContext ctx) => ctx.Body != null ? ctx.Body.transform : null;

        /// <summary>
        /// Chỗ đặt điểm neo của ảnh piece để điểm mốc mark của nó (theo manifest, vd "feet" chân nhân vật trên tấm) nằm đúng ở
        /// world (ảnh cỡ size, lật theo hướng mặt f).
        /// </summary>
        public static Vector2 PlaceAt(string piece, string mark, Vector2 world, float size, float f)
        {
            Vector2 m = BlueFx.PointOf(piece, mark);
            return new Vector2(world.x - m.x * size * f, world.y - m.y * size);
        }

        /// <summary>
        /// Vệt chém cắt từ tấm của chính chiêu (piece có mốc "chest" = điểm neo, "tip" = mũi lưỡi hái trên khung đó):
        ///  - lớp vệt ở cỡ 1 (ôm đúng đường lưỡi hái như trên tấm: nhân vật trong game cùng cỡ tấm), neo ở ngực, bám người, xoay
        ///    theo mũi lưỡi hái thật trong trackTime giây đầu (vệt luôn dính lưỡi hái);
        ///  - lõi trắng _Core loé ngắn, cộng sáng (mỏng, không đè lên người lâu);
        ///  - lớp vọng (khi cỡ hình &gt; 1): cùng hình, nở từ chính vệt (cỡ 1) ra tới cỡ hình Visual(share), mờ hơn (echo) — vùng
        ///    đánh ×2 được phủ mà hình vẫn xuất phát từ lưỡi hái, không bị đẩy rời ra.
        /// angle = xoay thêm (độ, dương = ngửa lên theo hướng mặt). Trả về lớp vệt.
        /// </summary>
        public static BlueFx.Fx SlashArc(SkillContext ctx, string piece, float life, float share = 1f, float echo = 0.5f, float angle = 0f,
            float trackTime = 0.05f)
        {
            float f = ctx.Facing;
            Vector2 chest = ctx.Chest;
            Transform body = BodyOf(ctx);
            Vector2 offset = body != null ? chest - (Vector2)body.position : Vector2.zero;
            Vector2 tip = BlueFx.PointOf(piece, "tip");
            BlueFx.Fx trail = Fx.Play(piece, chest, 1f, life);
            SlashLayer(ctx, trail, body, offset, tip, f, angle, trackTime);
            trail.GrowFrom = 0.82f;
            trail.GrowTime = 0.06f;
            trail.FadeIn = 0.02f;
            trail.FadeOut = life * 0.6f;

            string core = BlueFx.CoreOf(piece);
            if (core != null)
            {
                BlueFx.Fx c = Fx.PlayAdd(core, chest, 1f, life * 0.45f, BlueFx.Front + 2);
                SlashLayer(ctx, c, body, offset, tip, f, angle, trackTime);
                c.Color = new Color(1f, 1f, 1f, 0.75f);
                c.FadeOut = life * 0.3f;
            }

            float vis = Visual(ctx, share);
            if (vis > 1.05f && echo > 0f)
            {
                BlueFx.Fx e = Fx.Play(piece, chest, vis, life * 1.1f, BlueFx.Front - 1);
                SlashLayer(ctx, e, body, offset, tip, f, angle, trackTime);
                e.GrowFrom = 1f / vis;
                e.GrowTime = Mathf.Min(0.14f, life * 0.5f);
                e.Color = new Color(0.7f, 0.9f, 1f, echo); // vọng ngả lam: lõi trắng của vọng không thành dải xám bạc
                e.FadeIn = 0.03f;
                e.FadeOut = life * 0.75f;
            }

            return trail;
        }

        static void SlashLayer(SkillContext ctx, BlueFx.Fx fx, Transform body, Vector2 offset, Vector2 tip, float f, float angle, float track)
        {
            fx.Stretch = new Vector2(f, 1f);
            fx.Angle = angle * f;
            if (body != null)
            {
                fx.Follow = body;
                fx.Offset = offset;
            }

            if (ctx.HeroVisual != null && track > 0f)
            {
                fx.Weapon = ctx.HeroVisual;
                fx.TrackPoint = tip;
                fx.TrackTime = track;
                fx.TrackLimit = 15f; // chỉ chỉnh nhẹ: giữ dáng cung như trên tấm, đầu cung luôn ở lưỡi hái
            }
        }

        static readonly string[] s_embers = { BlueFx.Ember1, BlueFx.Ember2, BlueFx.Ember3 };

        /// <summary>Tàn lửa (ảnh tàn lửa của tấm Đánh thường) bay ra từ at theo hướng dir, hãm dần rồi tắt. size ở ×1 (tự nhân cỡ hình).</summary>
        public static void Embers(SkillContext ctx, Vector2 at, Vector2 dir, int count, float speed, float size = 1f, float share = 1f)
        {
            float vis = Visual(ctx, share);
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * dir.normalized;
                BlueFx.Fx e = Fx.Play(s_embers[i % s_embers.Length], at + Random.insideUnitCircle * (0.25f * vis), size * vis * Random.Range(0.8f, 1.2f),
                    Random.Range(0.3f, 0.45f));
                e.Velocity = d * (speed * vis * Random.Range(0.7f, 1.2f));
                e.Drag = 3.5f;
                e.Angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                e.Stretch = new Vector2(ctx.Facing, 1f);
                e.FadeOut = 0.2f;
                e.Glow = 0.4f;
            }
        }

        /// <summary>Tia lửa toé (CombatFx.Burst); cỡ hạt, tốc độ văng, trọng lực ở ×1, tự nhân cỡ chiêu (văng xa gấp bấy nhiêu, cùng thời gian).</summary>
        public static void Sparks(SkillContext ctx, Vector2 at, Color color, int count, float speed, float size, float life,
            float gravity = 6f, Vector2 bias = default)
        {
            float k = ctx.Size;
            ctx.Fx?.Burst(at, color, count, speed * k, size * k, life, gravity * k, bias);
        }

        /// <summary>Mảnh tinh thể / đá văng (CombatFx.Debris); tốc độ, cỡ, trọng lực ở ×1, tự nhân cỡ chiêu.</summary>
        public static void Shards(SkillContext ctx, Vector2 at, Color color, int count, float speed, float size, float gravity = 18f,
            Vector2 bias = default)
        {
            float k = ctx.Size;
            ctx.Fx?.Debris(at, color, count, speed * k, size * k, gravity * k, bias);
        }

        /// <summary>Phun tia lửa theo hướng dir (CombatFx.Spray); tốc độ, cỡ, trọng lực ở ×1, tự nhân cỡ chiêu.</summary>
        public static void Spray(SkillContext ctx, Vector2 at, Vector2 dir, Color color, int count, float speed, float spread, float size,
            bool additive, float gravity)
        {
            float k = ctx.Size;
            ctx.Fx?.Spray(at, dir, color, count, speed * k, spread, size * k, additive, gravity * k);
        }

        /// <summary>Mọi kẻ địch còn sống có collider chạm hộp (danh sách dùng lại, đọc ngay).</summary>
        public static List<Health> Touching(SkillContext ctx, Vector2 center, Vector2 size)
        {
            s_found.Clear();
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(ctx.Targets);
            int n = Physics2D.OverlapBox(center, size, 0f, filter, s_hits);
            for (int i = 0; i < n; i++)
            {
                Health h = s_hits[i].GetComponentInParent<Health>();
                if (h != null && h.IsAlive && !s_found.Contains(h))
                    s_found.Add(h);
            }

            return s_found;
        }

        /// <summary>Đánh đúng một mục tiêu (đòn lướt qua nhiều mục tiêu, mỗi mục tiêu chỉ trúng một lần).</summary>
        public static int HitOne(SkillContext ctx, Health h, float damage, HitEffect effect, Color color, bool sparks = true)
        {
            return ctx.HitTarget(h, damage, effect, color, sparks); // chỉ đúng h, không lan sang mục tiêu đứng chồng lên
        }

        /// <summary>Khung thân mục tiêu (collider); bia tập 1 × 1.9, boss sư tử ~10 × 8.5.</summary>
        public static Bounds BoundsOf(Health h)
        {
            Collider2D c = h.GetComponent<Collider2D>();
            if (c == null)
                c = h.GetComponentInChildren<Collider2D>();
            return c != null ? c.bounds : new Bounds(SkillContext.Aim(h), new Vector3(1f, 1.8f, 0f));
        }

        /// <summary>Kẻ địch gần nhất phía trước (theo mép thân, nên boss to cũng tính đúng), trong tầm range.</summary>
        public static Health NearestAhead(SkillContext ctx, float range, float maxDy = 4f)
        {
            Health best = null;
            float bestGap = float.MaxValue;
            Vector2 c = ctx.Center;
            IReadOnlyList<Health> all = Health.All;
            for (int i = 0; i < all.Count; i++)
            {
                Health h = all[i];
                if (h == null || !h.IsAlive || (ctx.Targets.value & (1 << h.gameObject.layer)) == 0)
                    continue;
                Bounds b = BoundsOf(h);
                float side = (b.center.x - c.x) * ctx.Facing;
                float gap = Mathf.Max(0f, Mathf.Abs(b.center.x - c.x) - b.extents.x);
                if (side < 0f && gap > 0f)
                    continue; // sau lưng
                float dy = Mathf.Max(0f, Mathf.Abs(c.y - b.center.y) - b.extents.y);
                if (gap > range || dy > maxDy || gap >= bestGap)
                    continue;
                best = h;
                bestGap = gap;
            }

            return best;
        }

        /// <summary>Mặt sàn / mặt đá ngay dưới at (y), không có thì fallback.</summary>
        public static float FloorBelow(SkillContext ctx, Vector2 at, float fallback, float depth = 14f)
        {
            RaycastHit2D hit = Physics2D.Raycast(at, Vector2.down, depth, GroundMask(ctx));
            return hit.collider != null ? hit.point.y : fallback;
        }

        /// <summary>Lớp mặt đứng được: sàn, gờ đá + tảng đá (bục một chiều của nhân vật người).</summary>
        public static int GroundMask(SkillContext ctx) => FloorMask | (ctx.Hero != null ? ctx.Hero.RockMask.value : 0);

        /// <summary>
        /// Đi dọc mặt đất từ ground (y = mặt đất) theo hướng dir được bao xa (tối đa range): dừng ở vách hoặc ở mép bục (phía
        /// trước hụt chân, không có mặt đất trong khoảng 0.6 unit dưới).
        /// </summary>
        public static float AlongGround(SkillContext ctx, Vector2 ground, float dir, float range, float step = 0.35f)
        {
            float wall = Clear(ground + Vector2.up * 0.8f, dir, range);
            int mask = GroundMask(ctx);
            for (float d = step; d <= wall; d += step)
            {
                var probe = new Vector2(ground.x + dir * d, ground.y + 0.4f);
                if (Physics2D.Raycast(probe, Vector2.down, 1f, mask).collider == null)
                    return Mathf.Max(0.5f, d - step * 0.5f);
            }

            return wall;
        }

        /// <summary>Đi thẳng theo hướng dir từ from được bao xa trước khi đụng vách (tối đa range).</summary>
        public static float Clear(Vector2 from, float dir, float range)
        {
            RaycastHit2D hit = Physics2D.Raycast(from, new Vector2(dir, 0f), range, FloorMask);
            return hit.collider != null ? Mathf.Max(0.5f, hit.distance - 0.2f) : range;
        }

        /// <summary>
        /// Vụ nổ tinh thể (khung 5 tấm Dùng chiêu tầm xa), nhiều lớp đổi dần: lõi trắng loé trước (cộng sáng, tắt nhanh) → thân
        /// nổ nảy to → lớp nổ thứ hai (lật, xoay, nhỏ hơn) bùng trễ một nhịp cho vụ nổ biến đổi bên trong → đá đen văng, tia lửa.
        /// size ở ×1 (tự nhân cỡ chiêu).
        /// </summary>
        public static void CrystalBurst(SkillContext ctx, Vector2 at, float size, float life = 0.6f)
        {
            float k = size * ctx.Size;
            float f = ctx.Facing;
            BlueFx.Fx core = Fx.PlayAdd(BlueFx.CoreOf(BlueFx.Burst) ?? BlueFx.Burst, at, k * 0.9f, life * 0.35f);
            core.Pop = true;
            core.GrowFrom = 0.3f;
            core.GrowTime = 0.08f;
            core.Stretch = new Vector2(f, 1f);
            core.Color = new Color(1f, 1f, 1f, 0.8f);
            BlueFx.Fx fx = Fx.Play(BlueFx.Burst, at, k, life);
            fx.Pop = true;
            fx.GrowFrom = 0.35f;
            fx.GrowTime = 0.14f;
            fx.EndSize = 1.1f;
            fx.FadeOut = life * 0.5f;
            fx.Glow = 0.25f;
            fx.Stretch = new Vector2(f, 1f);
            BlueFx.Fx late = Fx.Play(BlueFx.Burst, at + new Vector2(f * 0.15f, 0.1f) * k, k * 0.72f, life * 0.8f);
            late.Delay = 0.07f;
            late.Pop = true;
            late.GrowFrom = 0.4f;
            late.GrowTime = 0.12f;
            late.EndSize = 1.15f;
            late.Angle = 25f * f;
            late.Stretch = new Vector2(-f, 1f);
            late.Color = new Color(1f, 1f, 1f, 0.7f);
            late.FadeOut = life * 0.5f;
            if (ctx.Fx == null)
                return;
            ctx.Fx.Flash(at, size * 1.5f * ctx.Size, Main, 0.2f);
            Shards(ctx, at, Crystal, 7, 6f, 0.24f, 14f, Vector2.up * 0.3f);
            Sparks(ctx, at, Hot, 10, 6f, 0.13f, 0.35f);
        }
    }

    // =====================================================================================================================
    // NÚT 1 — ĐÁNH THƯỜNG
    // =====================================================================================================================

    /// <summary>
    /// Nút 1 · thường — Đánh thường (tấm "Đánh thường"): giơ lưỡi hái lên → quét ngang lưỡi liềm lửa xanh → quét tiếp một vòng
    /// lớn từ sau lưng qua dưới ra trước → thu về. Bấm liên tiếp ra combo 3 nhát, nhát 3 quét rộng và nặng nhất.
    /// Hình: nhát 1 = cung dài khung 3, nhát 2 = lưỡi liềm lớn + dải thấp khung 4 + mũi vệt sát đất khung 1/6, nhát 3 = lưỡi
    /// liềm khung 4 nở to + mũi vệt + tàn lửa. Vệt neo ở ngực, xoay theo mũi lưỡi hái thật (BlueKit.SlashArc).
    /// </summary>
    [Serializable]
    public class BlueReapCombo : Skill
    {
        public float damage = 20f;
        public float secondDamage = 22f;
        public float finisherDamage = 34f;
        [Tooltip("Tầm quét trước mặt (unit, ở ×1, nhân Tầm chiêu): lưỡi hái cán dài")]
        public float reach = 2.3f;
        [Tooltip("Bấm lại trong bấy nhiêu giây sau nhát trước thì ra nhát tiếp của combo")]
        public float comboWindow = 0.7f;
        [Tooltip("Phần Cỡ chiêu áp vào hình (0..1): lớp vọng của vệt chém nở tới 1 + (Cỡ chiêu − 1) × số này. Vùng đánh vẫn nhân đủ Cỡ chiêu")]
        public float fxScale = 1f;

        int _step;
        float _lastEnd = -10f;

        public BlueReapCombo()
        {
            name = "Đánh thường";
            description = "Quét lưỡi hái lửa xanh, bấm liên tiếp ra combo 3 nhát.";
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            if (Time.time - _lastEnd > comboWindow)
                _step = 0;
            int step = _step;
            _step = (_step + 1) % 3;
            float f = ctx.Facing;
            float size = ctx.Size; // khung đánh × cỡ chiêu; tâm khung tới trước × tầm chiêu (BlueKit.Ahead)
            Vector2 c = ctx.Center;

            if (step == 0)
            {
                // Khung 2 → 3: giơ lưỡi hái lên cao rồi quét ngang tới trước — cung dài khung 3 bám lưỡi hái
                ctx.Act(CombatPose.Whip, 0.4f, c + new Vector2(f * reach, 0.3f));
                yield return Wait(0.12f);
                c = ctx.Center;
                BlueKit.SlashArc(ctx, BlueFx.ReapArcA, 0.28f, fxScale, 0.36f);
                BlueKit.Embers(ctx, ctx.WeaponTip, new Vector2(f, 0.4f), 2, 3f);
                yield return Wait(0.035f);
                ctx.HitBox(BlueKit.Ahead(ctx, c, reach * 0.5f, 0.35f), new Vector2(reach * 1.1f, 1.9f) * size, damage,
                    new HitEffect { knockback = new Vector2(2.5f, 0.6f) }, BlueKit.Main);
                yield return Wait(0.15f);
            }
            else if (step == 1)
            {
                // Khung 4: quét tiếp một vòng lớn từ sau lưng, qua dưới chân, hất ra trước — lưỡi liềm lớn + mũi vệt sát đất
                ctx.Act(CombatPose.WhipAlt, 0.42f, c + new Vector2(f * reach, -0.1f));
                yield return Wait(0.13f);
                c = ctx.Center;
                BlueKit.SlashArc(ctx, BlueFx.ReapArcB, 0.3f, fxScale, 0.42f, -8f);
                GroundTip(ctx, c, 0.26f);
                yield return Wait(0.03f);
                ctx.HitBox(BlueKit.Ahead(ctx, c, 1.05f, 0.25f), new Vector2(2.6f, 1.9f) * size, secondDamage,
                    new HitEffect { knockback = new Vector2(2.8f, 1.6f) }, BlueKit.Main);
                yield return Wait(0.16f);
            }
            else
            {
                // Nhát kết: giơ cao quét lưỡi liềm lớn nhất, vọng nở rộng hơn, mũi vệt sát đất, tàn lửa
                ctx.Act(CombatPose.Whip, 0.5f, c + new Vector2(f * reach, 0.3f));
                yield return Wait(0.16f);
                c = ctx.Center;
                BlueKit.SlashArc(ctx, BlueFx.ReapArcB, 0.34f, fxScale * 1.1f, 0.55f, 4f);
                GroundTip(ctx, c, 0.3f);
                BlueKit.Embers(ctx, ctx.WeaponTip, new Vector2(f, 0.2f), 3, 4f);
                ctx.Fx?.Flash(ctx.WeaponTip, 0.9f * BlueKit.Visual(ctx, 0.5f), BlueKit.Main, 0.16f);
                yield return Wait(0.03f);
                ctx.HitBox(BlueKit.Ahead(ctx, c, 1.25f, 0.4f), new Vector2(3f, 2.3f) * size, finisherDamage,
                    new HitEffect { knockback = new Vector2(4.5f, 2.2f), heavy = true }, BlueKit.Main);
                BlueKit.Sparks(ctx, ctx.Ground(1.8f * ctx.Range) + Vector2.up * 0.1f, BlueKit.Hot, 8, 4f, 0.1f, 0.3f, 6f,
                    new Vector2(f * 0.6f, 0.5f));
                yield return Wait(0.22f);
            }

            _lastEnd = Time.time;
        }

        // Mũi vệt quét sát đất (khung 1 / 6): đuôi lửa thấp chạy dọc mặt đất phía trước, dài theo cỡ hình
        void GroundTip(SkillContext ctx, Vector2 c, float life)
        {
            float f = ctx.Facing;
            float vis = BlueKit.Visual(ctx, fxScale);
            Vector2 at = c + new Vector2(f * 0.15f, -0.5f + 0.42f);
            BlueFx.Fx tip = BlueKit.Fx.Play(BlueFx.ReapTip, BlueKit.PlaceAt(BlueFx.ReapTip, "chest", at, 1f, f), 1f, life);
            tip.Stretch = new Vector2(f * vis, 1f);
            tip.GrowFrom = 0.6f;
            tip.GrowTime = 0.08f;
            tip.FadeOut = life * 0.6f;
        }
    }

    /// <summary>
    /// Nút 1 · ↑ — Đánh thường hất tung (tấm "Đánh thường hất tung"): khom thấp quét sát đất → hất lưỡi hái lên thành vệt lửa
    /// dọc, hất mục tiêu bay lên → BLUE bật lên theo trong cột lửa xanh (cột lửa đốt thêm một nhát) → rơi xuống đáp đất.
    /// Hình theo thứ tự như tấm: quét thấp (khung 2) → cung hất nhỏ (khung 3) đổi sang cung hất lớn (khung 4), cùng điểm neo
    /// chân nên không giật → SAU cung mới tới cột lửa (khung 5: lửa hai bên + chân cột + tia sáng giữa kéo cao).
    /// </summary>
    [Serializable]
    public class BlueRisingReap : Skill
    {
        public float damage = 22f;
        [Tooltip("Cột lửa đốt thêm mục tiêu đang bị hất lên")]
        public float pillarDamage = 10f;
        [Tooltip("Hất mục tiêu lên (unit/giây)")]
        public float launch = 11f;
        [Tooltip("Tự bật lên cao bấy nhiêu unit (dùng trên không: hơn nửa chừng này)")]
        public float rise = 2.4f;
        [Tooltip("Phần Cỡ chiêu áp vào hình (0..1): cung hất / cột lửa to 1 + (Cỡ chiêu − 1) × số này (vừa với độ bật người). Vùng đánh vẫn nhân đủ Cỡ chiêu")]
        public float fxScale = 0.6f;

        public BlueRisingReap()
        {
            name = "Đánh thường hất tung";
            description = "Quét thấp rồi hất lưỡi hái lên, hất kẻ địch bay lên, bật lên theo trong cột lửa xanh.";
            cooldown = 0.9f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float f = ctx.Facing;
            float size = ctx.Size;
            float vis = BlueKit.Visual(ctx, fxScale);
            bool grounded = ctx.Grounded;
            Vector2 c = ctx.Center;
            ctx.Act(CombatPose.Whip, 0.6f, c + new Vector2(f * 1f, 2.6f)); // nhắm cao: dáng hất lên
            yield return Wait(0.09f);

            // Khung 2: quét thấp sát đất trước mặt
            c = ctx.Center;
            Vector2 feet = c + Vector2.down * 0.5f;
            BlueFx.Fx low = BlueKit.Fx.Play(BlueFx.RiseLow, feet, vis, 0.2f);
            low.Stretch = new Vector2(f, 1f);
            low.GrowFrom = 0.7f;
            low.GrowTime = 0.06f;
            low.FadeOut = 0.12f;
            BlueKit.Sparks(ctx, ctx.Ground(1.3f * ctx.Range) + Vector2.up * 0.1f, BlueKit.Hot, 6, 3.5f, 0.1f, 0.25f, 6f, new Vector2(f * 0.5f, 0.5f));

            yield return Wait(0.11f);

            // Khung 3 → 4: hất lưỡi hái lên, cung nhỏ đổi sang cung lớn dựng đứng (chung điểm neo chân), mục tiêu bay lên
            c = ctx.Center;
            feet = c + Vector2.down * 0.5f;
            BlueFx.Fx arc1 = BlueKit.Fx.Play(BlueFx.RiseArc1, feet, vis, 0.11f);
            arc1.Stretch = new Vector2(f, 1f);
            arc1.RiseTime = 0.06f;
            arc1.FadeOut = 0.05f;
            BlueFx.Fx arc = BlueKit.Fx.Play(BlueFx.Rise, feet, vis, 0.4f);
            arc.Delay = 0.07f;
            arc.Stretch = new Vector2(f, 1f);
            arc.FadeIn = 0.03f;
            arc.FadeOut = 0.22f;
            arc.Glow = 0.2f;
            BlueFx.Fx core = BlueKit.Fx.PlayAdd(BlueFx.CoreOf(BlueFx.Rise) ?? BlueFx.Rise, feet, vis, 0.16f);
            core.Delay = 0.07f;
            core.Stretch = new Vector2(f, 1f);
            core.Color = new Color(1f, 1f, 1f, 0.7f);
            ctx.HitBox(BlueKit.Ahead(ctx, c, 1.05f, 0.9f), new Vector2(2.3f, 3f) * size, damage,
                new HitEffect { knockback = new Vector2(0.8f, launch), stun = 0.35f }, BlueKit.Main);

            // Khung 5 (sau cung): cột lửa ở chỗ bật lên — lửa hai bên + chân cột mọc từ đất, tia sáng giữa kéo cao theo độ bật
            BlueFx.Fx pillar = BlueKit.Fx.Play(BlueFx.Pillar, feet, vis, 0.6f, BlueFx.Behind);
            pillar.Delay = 0.12f;
            pillar.RiseTime = 0.14f;
            pillar.Stretch = new Vector2(f, 1f);
            pillar.Glow = 0.25f;
            pillar.FadeOut = 0.3f;
            float beamHeight = (rise + 2f) * Mathf.Min(1.25f, vis);
            BlueFx.Fx beam = BlueKit.Fx.Play(BlueFx.PillarBeam, feet, vis, 0.5f, BlueFx.Behind);
            beam.Delay = 0.12f;
            beam.RiseTime = 0.16f;
            beam.Stretch = new Vector2(1f, beamHeight / (BlueFx.SizeOf(BlueFx.PillarBeam).y * vis));
            beam.Glow = 0.35f;
            beam.FadeOut = 0.28f;

            if (ctx.Hero != null)
                ctx.Hero.Launch(new Vector2(f * 1.2f, ctx.Hero.Settings.LaunchSpeed(grounded ? rise : rise * 0.55f)));
            yield return Wait(0.16f);

            // Cột lửa đốt tiếp mục tiêu đang bay lên: cột mọc từ chân (đáy giữ ở chân, cao 4 × cỡ chiêu)
            ctx.HitBox(BlueKit.Ahead(ctx, feet, 0.9f, 0.2f + 2f * size), new Vector2(2f, 4f) * size, pillarDamage,
                new HitEffect { knockback = new Vector2(0.3f, 7f) }, BlueKit.Main);
            yield return Wait(0.24f);
        }
    }

    /// <summary>
    /// Nút 1 · ↓ — Đánh thường áp sát (tấm "Đánh thường áp sát"): đạp đất toé tia lửa → lướt thấp tới trước để lại vệt lửa
    /// xanh, chém mọi mục tiêu trên đường → quét lưỡi liềm lớn ở cuối đường → trượt dừng tung bụi.
    /// Hình: tia đạp đất sau gót (khung 2) → dải lửa tốc độ kéo sau lưng suốt đường lướt (khung 3) → lưỡi liềm cuối đường bám
    /// lưỡi hái (khung 4) → bụi xám + đá đen sau gót (khung 5).
    /// </summary>
    [Serializable]
    public class BlueReaperDash : Skill
    {
        [Tooltip("Sát thương mục tiêu trên đường lướt")]
        public float pathDamage = 16f;
        [Tooltip("Sát thương nhát quét lưỡi liềm ở cuối đường")]
        public float damage = 26f;
        [Tooltip("Lướt xa bấy nhiêu unit (ở ×1, nhân Tầm chiêu, cùng thời gian lướt; vách chặn lại)")]
        public float distance = 3.6f;
        public float dashTime = 0.22f;
        [Tooltip("Phần Cỡ chiêu áp vào hình (0..1): lưỡi liềm cuối đường nở tới 1 + (Cỡ chiêu − 1) × số này; tia đạp / dải lửa / bụi theo một nửa. Vùng đánh vẫn nhân đủ Cỡ chiêu")]
        public float fxScale = 1f;

        public BlueReaperDash()
        {
            name = "Đánh thường áp sát";
            description = "Lướt tới để lại vệt lửa xanh, chém trên đường, quét lưỡi liềm lớn ở cuối.";
            cooldown = 1.2f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float f = ctx.Facing;
            float size = ctx.Size;
            float small = BlueKit.Visual(ctx, fxScale * 0.5f); // tia đạp, dải lửa, bụi: to vừa phải (bám thân)
            Vector2 start = ctx.Center;
            Vector2 feet = start + Vector2.down * 0.5f;
            ctx.Act(CombatPose.Lunge, 0.64f, start + new Vector2(f * 3f, 0f));

            // Khung 2: đạp đất, tia lửa xanh toé lên sau gót
            BlueFx.Fx kick = BlueKit.Fx.Play(BlueFx.DashKick, BlueKit.PlaceAt(BlueFx.DashKick, "feet", feet + new Vector2(-f * 0.15f, 0f), small, f),
                small, 0.32f);
            kick.Stretch = new Vector2(f, 1f);
            kick.RiseTime = 0.08f;
            kick.FadeOut = 0.2f;
            BlueKit.Spray(ctx, feet + new Vector2(-f * 0.1f, 0.1f), new Vector2(-f * 0.35f, 1f), BlueKit.Hot, 10, 6f, 35f, 0.12f, true, 6f);

            // Khung 3: lướt thấp, dải lửa tốc độ kéo sau lưng (thả lại dọc đường, tắt dần) + vệt gió.
            // Quãng lướt × tầm chiêu, cùng thời gian (lướt nhanh hơn)
            if (ctx.Hero != null)
            {
                ctx.Hero.Dash(ctx.Forward, distance * ctx.Range, dashTime, 0f, true, BlueKit.Main);
                float emit = 0f;
                for (float t = 0f; t < dashTime + 0.15f && ctx.Hero.IsDashing; t += Time.deltaTime)
                {
                    ctx.Fx?.SpeedLines(ctx.Chest, ctx.Hero.Velocity, BlueKit.Main);
                    emit -= Time.deltaTime;
                    if (emit <= 0f)
                    {
                        emit = 0.05f;
                        Vector2 back = ctx.Center + new Vector2(-f * 0.2f, -0.05f);
                        BlueFx.Fx streak = BlueKit.Fx.Play(BlueFx.DashStreak, back, small, 0.24f);
                        streak.Stretch = new Vector2(f, 1f);
                        streak.FadeIn = 0.02f;
                        streak.FadeOut = 0.18f;
                        streak.Color = new Color(1f, 1f, 1f, 0.85f);
                    }

                    yield return null;
                }
            }
            else
                yield return Wait(dashTime);

            Vector2 end = ctx.Center;
            ctx.HitSegment(start + Vector2.up * 0.3f, end + Vector2.up * 0.3f, 0.8f * size, pathDamage,
                new HitEffect { knockback = new Vector2(1.5f, 0.8f) }, BlueKit.Main);

            // Khung 4: quét lưỡi liềm lớn ở cuối đường (bám lưỡi hái, vọng nở phủ vùng đánh)
            BlueKit.SlashArc(ctx, BlueFx.DashArc, 0.3f, fxScale, 0.45f);
            ctx.HitBox(BlueKit.Ahead(ctx, end, 1.15f, 0.4f), new Vector2(2.4f, 2.1f) * size, damage,
                new HitEffect { knockback = new Vector2(4f, 1.6f), heavy = true }, BlueKit.Main);

            // Khung 5: trượt dừng, bụi xám + đá đen tung sau gót
            Vector2 heel = end + new Vector2(-f * 0.35f, -0.5f);
            BlueFx.Fx dust = BlueKit.Fx.Play(BlueFx.DashDust, BlueKit.PlaceAt(BlueFx.DashDust, "feet", heel, small, f), small, 0.5f);
            dust.Stretch = new Vector2(f, 1f);
            dust.GrowFrom = 0.5f;
            dust.GrowTime = 0.12f;
            dust.EndSize = 1.15f;
            dust.Velocity = new Vector2(-f * 0.6f, 0.3f);
            dust.Drag = 3f;
            dust.FadeOut = 0.3f;
            BlueKit.Shards(ctx, heel, BlueKit.Crystal, 4, 3f, 0.16f);

            yield return Wait(0.3f);
        }
    }

    // =====================================================================================================================
    // NÚT 2 — DÙNG CHIÊU
    // =====================================================================================================================

    /// <summary>
    /// Nút 2 · thường — Dùng chiêu (tấm "Dùng chiêu"): khom người, lửa xanh bùng quanh chân → xoay người thành các vòng lửa
    /// xanh quanh thân (đánh mọi thứ quanh mình vài nhát) → quét mạnh một lưỡi liềm lửa tới trước, đẩy văng.
    /// Hình theo tấm: lửa hai bên chân (khung 2) → vòng lốc khung 3 → lốc nhiều tầng khung 4 (mỗi khung tách lớp TRƯỚC người /
    /// SAU lưng, cùng điểm neo chân; hai bản lốc lệch pha thay nhau sáng cho lốc cuộn) → cung quét khung 5 bám lưỡi hái.
    /// </summary>
    [Serializable]
    public class BlueSoulWhirl : Skill
    {
        [Tooltip("Mỗi nhát của vòng lửa quanh thân")]
        public float tickDamage = 10f;
        public int ticks = 3;
        [Tooltip("Bán kính vòng lửa quanh thân (ở ×1, nhân Cỡ chiêu)")]
        public float radius = 2f;
        [Tooltip("Nhát quét mạnh tới trước")]
        public float finisherDamage = 32f;
        [Tooltip("Phần Cỡ chiêu áp vào bề ngang lốc (0..1): rộng 1 + (Cỡ chiêu − 1) × số này, cao chỉ thêm một nửa phần đó (lốc ôm người, không vùi người). Vùng đánh vẫn nhân đủ Cỡ chiêu")]
        public float fxScale = 0.85f;

        public BlueSoulWhirl()
        {
            name = "Dùng chiêu";
            description = "Xoay lưỡi hái thành vòng lửa xanh đánh quanh mình, rồi quét mạnh tới trước.";
            cooldown = 4f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float f = ctx.Facing;
            float size = ctx.Size;
            float wide = BlueKit.Visual(ctx, fxScale);
            float tall = 1f + (wide - 1f) * 0.5f;
            ctx.Hero?.Root(1.1f);
            ctx.Act(CombatPose.Spin, 0.78f);

            // Khung 2: khom người, lửa xanh bùng hai bên chân
            Vector2 c = ctx.Center;
            Vector2 feet = c + Vector2.down * 0.5f;
            FootFlame(ctx, BlueFx.WhirlFlameL, feet, tall, 0.75f);
            FootFlame(ctx, BlueFx.WhirlFlameR, feet, tall, 0.75f);
            ctx.Fx?.Flash(ctx.Chest, 0.9f * tall, BlueKit.Main, 0.25f, true);

            yield return Wait(0.16f);

            // Khung 3 → 4: vòng lốc (trước / sau người), rồi lốc nhiều tầng cuộn (2 bản lệch pha) tới hết lúc xoay
            feet = ctx.Center + Vector2.down * 0.5f;
            Ring(ctx, BlueFx.WhirlRing3Back, BlueFx.WhirlRing3Front, feet, wide, tall, 0f, 0.2f, 0.7f, 0f);
            Ring(ctx, BlueFx.WhirlRing4Back, BlueFx.WhirlRing4Front, feet, wide, tall, 0.12f, 0.42f, 0.55f, 0f);
            Ring(ctx, BlueFx.WhirlRing4Back, BlueFx.WhirlRing4Front, feet, wide * 0.94f, tall * 0.97f, 0.12f, 0.42f, 0.55f, 0.5f, true);
            int hits = 0;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (hits < ticks && t >= hits * 0.16f)
                {
                    hits++;
                    ctx.HitCircle(ctx.Center + Vector2.up * 0.3f, radius * size, tickDamage, new HitEffect { knockback = new Vector2(1.2f, 0.8f) },
                        BlueKit.Main);
                }

                yield return null;
            }

            // Khung 5: quét mạnh tới trước, cung lửa vút lên bám lưỡi hái
            c = ctx.Center;
            ctx.Act(CombatPose.WhipAlt, 0.42f, c + new Vector2(f * 2.5f, 0.3f));
            yield return Wait(0.15f);
            c = ctx.Center;
            BlueKit.SlashArc(ctx, BlueFx.WhirlFinish, 0.3f, fxScale, 0.4f, 6f);
            BlueKit.Spray(ctx, ctx.WeaponTip, new Vector2(f, 0.7f), BlueKit.Hot, 8, 6f, 30f, 0.12f, true, -1f);
            ctx.HitBox(BlueKit.Ahead(ctx, c, 1.35f, 0.5f), new Vector2(2.9f, 2.5f) * size, finisherDamage,
                new HitEffect { knockback = new Vector2(5f, 2.5f), heavy = true }, BlueKit.Main);
            yield return Wait(0.27f);
        }

        static void FootFlame(SkillContext ctx, string piece, Vector2 feet, float k, float life)
        {
            float f = ctx.Facing;
            BlueFx.Fx fl = BlueKit.Fx.Play(piece, BlueKit.PlaceAt(piece, "feet", feet, k, f), k, life, BlueFx.Behind);
            fl.Stretch = new Vector2(f, 1f);
            fl.RiseTime = 0.12f;
            fl.Flicker = 0.15f;
            fl.FadeOut = 0.3f;
        }

        // Một khung lốc: lớp sau lưng + lớp trước người, cùng điểm neo chân, bề ngang wide, cao tall; pulse = lệch pha (0..1)
        // cho hai bản thay nhau sáng; mirror = bản lật ngang (lốc cuộn ngược pha)
        static void Ring(SkillContext ctx, string back, string front, Vector2 feet, float wide, float tall, float delay, float life,
            float alpha, float phase, bool mirror = false)
        {
            float f = ctx.Facing * (mirror ? -1f : 1f);
            for (int i = 0; i < 2; i++)
            {
                BlueFx.Fx r = BlueKit.Fx.Play(i == 0 ? back : front, feet, 1f, life, i == 0 ? BlueFx.Behind : BlueFx.Front);
                r.Stretch = new Vector2(f * wide, tall);
                r.Delay = delay;
                r.GrowFrom = 0.8f;
                r.GrowTime = 0.08f;
                r.FadeIn = 0.05f;
                r.FadeOut = Mathf.Min(0.12f, life * 0.4f);
                r.Color = new Color(1f, 1f, 1f, alpha);
                r.Pulse = 0.55f;
                r.PulseRate = 7f;
                r.PulsePhase = phase;
            }
        }
    }

    /// <summary>
    /// Nút 2 · ↑ — Dùng chiêu tầm xa (tấm "Dùng chiêu tầm xa"): giơ lưỡi hái, cầu sáng + vòng phép tụ ở đầu lưỡi hái → vung
    /// xuống chĩa tới, phóng sóng lưỡi liềm lửa xanh bay xa → trúng kẻ địch (hoặc hết tầm / đụng vách) nổ tinh thể.
    /// Hình: vòng phép khung 2 (cỡ vừa đầu lưỡi hái) → cung quét khung 3 bám lưỡi hái → sóng khung 4 bay (lõi trắng riêng) →
    /// nổ khung 5 nhiều lớp (BlueKit.CrystalBurst).
    /// </summary>
    [Serializable]
    public class BlueCrescentWave : Skill
    {
        public float damage = 42f;
        [Tooltip("Tốc độ sóng (unit/giây, ở ×1, nhân Tầm chiêu)")]
        public float speed = 13f;
        [Tooltip("Tầm bay tối đa (unit, ở ×1, nhân Tầm chiêu)")]
        public float range = 10f;
        [Tooltip("Bán kính vụ nổ tinh thể (ở ×1, nhân Cỡ chiêu)")]
        public float blastRadius = 1.4f;
        [Tooltip("Phần Cỡ chiêu áp vào vòng phép tụ ở đầu lưỡi hái (0..1): cỡ 1 + (Cỡ chiêu − 1) × số này, không lấn đầu lưỡi hái. Sóng bay và vụ nổ vẫn nhân đủ Cỡ chiêu")]
        public float fxScale = 0.4f;

        public BlueCrescentWave()
        {
            name = "Dùng chiêu tầm xa";
            description = "Tụ vòng phép, phóng sóng lưỡi liềm lửa xanh bay xa, nổ tinh thể khi trúng.";
            cooldown = 3.5f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float f = ctx.Facing;
            ctx.Hero?.Root(0.95f);

            // Khung 2: giơ lưỡi hái lên, cầu sáng + vòng phép tụ ở đầu lưỡi hái
            ctx.Act(CombatPose.Rear, 0.42f, ctx.Center + Vector2.up * 3f);
            BlueFx.Fx circle = BlueKit.Fx.Play(BlueFx.Circle, ctx.WeaponTip, 1.15f * BlueKit.Visual(ctx, fxScale), 0.7f, BlueFx.Behind);
            circle.Pop = true;
            circle.GrowFrom = 0.15f;
            circle.GrowTime = 0.25f;
            circle.Spin = -50f * f;
            circle.Glow = 0.45f;
            circle.FadeOut = 0.15f;
            var held = new BlueFx.FxRef(circle);
            for (float t = 0f; t < 0.42f; t += Time.deltaTime)
            {
                BlueFx.Fx c1 = held.Get;
                if (c1 != null)
                    c1.Position = ctx.WeaponTip + new Vector2(f * 0.12f, 0.05f);
                yield return null;
            }

            // Khung 3–4: vung xuống chĩa tới (cung quét khung 3 bám lưỡi hái), phóng sóng lưỡi liềm
            Vector2 c = ctx.Center;
            ctx.Act(CombatPose.Reach, 0.5f, c + new Vector2(f * 5f, 0.4f));
            held.Stop(0.12f);
            yield return Wait(0.17f);
            c = ctx.Center;
            BlueKit.SlashArc(ctx, BlueFx.WaveSweep, 0.22f, 0f, 0f);
            // Sóng hiện ra cách 1.15 unit × tầm chiêu; lần quét đầu tính từ chỗ sóng hiện ra ở ×1, để kẻ địch đứng sát vẫn trúng
            ctx.Combat.Linger(Fly(ctx, BlueKit.Ahead(ctx, c, 1.15f, 0.45f), c.x + f * 1.15f, f));
            yield return Wait(0.33f);
        }

        IEnumerator Fly(SkillContext ctx, Vector2 from, float nearX, float f)
        {
            float size = ctx.Size;
            float v = speed * ctx.Range; // bay xa gấp tầm chiêu và nhanh gấp tầm chiêu: thời gian bay như cũ
            var box = new Vector2(0.9f, 1.9f) * size; // vùng chạm của sóng, mép sau ở giữa sóng
            BlueFx.Fx wave = BlueKit.Fx.Play(BlueFx.Wave, from, size, 10f);
            wave.Stretch = new Vector2(f, 1f);
            wave.Glow = 0.3f;
            wave.Pop = true;
            wave.GrowFrom = 0.4f;
            wave.GrowTime = 0.12f;
            wave.FadeOut = 0.1f;
            var held = new BlueFx.FxRef(wave);
            BlueFx.Fx hot = BlueKit.Fx.PlayAdd(BlueFx.CoreOf(BlueFx.Wave) ?? BlueFx.Wave, from, size, 10f);
            hot.Stretch = new Vector2(f, 1f);
            hot.Color = new Color(1f, 1f, 1f, 0.55f);
            hot.Flicker = 0.3f;
            hot.FadeOut = 0.08f;
            var heldHot = new BlueFx.FxRef(hot);
            Vector2 p = from;
            float limit = BlueKit.Clear(from, f, range * ctx.Range);
            float trail = 0f;
            bool first = true;
            for (float travelled = 0f; travelled < limit;)
            {
                float step = v * Time.deltaTime;
                p.x += f * step;
                travelled += step;
                BlueFx.Fx w = held.Get;
                if (w != null)
                    w.Position = p;
                BlueFx.Fx h = heldHot.Get;
                if (h != null)
                    h.Position = p;
                trail -= Time.deltaTime;
                if (trail <= 0f && ctx.Fx != null)
                {
                    trail = 0.03f;
                    ctx.Fx.SpeedLines(p - new Vector2(f * 0.5f * size, 0f), new Vector2(f * v, 0f), BlueKit.Main);
                }

                // Vùng chạm: từ giữa sóng tới mép trước; lần đầu kéo dài về sau tới nearX
                float rear = first ? Mathf.Max(0f, (p.x - nearX) * f) : 0f;
                first = false;
                var touch = new Vector2(box.x + rear, box.y);
                if (BlueKit.Touching(ctx, new Vector2(p.x + f * (box.x - rear) * 0.5f, p.y), touch).Count > 0)
                    break;
                yield return null;
            }

            // Khung 5: nổ tinh thể ở chỗ trúng / cuối tầm
            held.Stop(0.08f);
            heldHot.Stop(0.05f);
            Vector2 at = p + new Vector2(f * 0.5f * size, 0f);
            BlueKit.CrystalBurst(ctx, at, 0.95f);
            ctx.HitCircle(at, blastRadius * size, damage, new HitEffect { knockback = new Vector2(3.5f, 2f), heavy = true }, BlueKit.Main);
        }
    }

    /// <summary>
    /// Nút 2 · ↓ — Dùng chiêu thủ (tấm "Dùng chiêu thủ"): dựng lưỡi hái, vòng phép hiện dưới chân, lửa xanh và đầu lâu lửa bốc
    /// lên quanh người, vệt lửa cuộn quanh thân, tinh thể bay vòng quanh. Trong lúc đó chặn mọi đòn (khiên) và lửa đốt kẻ địch
    /// đứng sát bên. Đứng tại chỗ giữ thế thủ.
    /// Hình theo tấm, lớn dần: vòng phép mỏng dưới chân (khung 3, nửa sau lưng / nửa trước chân; nằm trên đất nên to đúng vùng
    /// đốt) → lưỡi lửa đứng hai bên + dải lửa xoắn ôm người (khung 3–4, trước / sau, đập nhịp) → đầu lâu lửa (5 biến thể) bốc
    /// lên, tinh thể bay vòng sát người (khung 5–6). Phần đứng quanh người chỉ to thêm một phần theo Cỡ chiêu (fxScale).
    /// </summary>
    [Serializable]
    public class BlueSoulGuard : Skill
    {
        [Tooltip("Giữ thế thủ (chặn đòn) bấy nhiêu giây")]
        public float duration = 2.2f;
        [Tooltip("Lửa đốt kẻ địch đứng sát bên: mỗi nhát")]
        public float burnDamage = 7f;
        public float burnInterval = 0.3f;
        [Tooltip("Bán kính lửa đốt quanh người (ở ×1, nhân Cỡ chiêu)")]
        public float burnRadius = 1.75f;
        [Tooltip("Phần Cỡ chiêu áp vào hào quang đứng quanh người (0..1): lửa xoắn, lưỡi lửa, đầu lâu, tinh thể to 1 + (Cỡ chiêu − 1) × số này (ôm sát người). Vòng phép dưới chân và vùng đốt vẫn nhân đủ Cỡ chiêu")]
        public float fxScale = 0.35f;

        const int Crystals = 4;
        static readonly string[] Skulls = { BlueFx.Skull, BlueFx.SkullA, BlueFx.SkullB, BlueFx.SkullC, BlueFx.SkullD };

        public BlueSoulGuard()
        {
            name = "Dùng chiêu thủ";
            description = "Vòng phép và lửa hồn bao quanh: chặn mọi đòn, đốt kẻ địch đứng sát bên.";
            cooldown = 6f;
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float f = ctx.Facing;
            float size = ctx.Size;
            float aura = BlueKit.Visual(ctx, fxScale); // hào quang đứng ôm người
            ctx.Hero?.Root(duration + 0.2f);
            ctx.Act(CombatPose.Squash, duration + 0.3f, ctx.Center + new Vector2(f * 2f, 0.5f));
            ctx.HeroVisual?.SetWeaponPower(0.7f);
            PlayerHealth.Block block = ctx.BlockFor(duration, hit =>
            {
                // Đòn bị chặn: chớp sáng ở phía bị đánh
                if (ctx.Fx == null)
                    return;
                Vector2 dir = hit.from - ctx.Chest;
                Vector2 at = ctx.Chest + (dir.sqrMagnitude > 1e-4f ? dir.normalized : ctx.Forward) * (0.8f * aura);
                ctx.Fx.Flash(at, 1.2f * aura, BlueKit.Main, 0.2f);
                BlueKit.Sparks(ctx, at, BlueKit.Hot, 8, 4.5f, 0.12f, 0.3f);
            });

            // Khung 3: vòng phép mỏng dưới chân (nửa sau lưng + nửa trước chân), to đúng vùng đốt
            Vector2 feet = ctx.Center + Vector2.down * 0.5f;
            float sealSize = burnRadius / 1.63f * size; // vòng phép trên tấm bán kính 1.63 unit
            for (int i = 0; i < 2; i++)
            {
                BlueFx.Fx seal = BlueKit.Fx.Play(i == 0 ? BlueFx.SealBack : BlueFx.SealFront, feet + Vector2.up * 0.03f, sealSize, duration + 0.35f,
                    i == 0 ? BlueFx.Behind : BlueFx.Front);
                seal.Pop = true;
                seal.GrowFrom = 0.2f;
                seal.GrowTime = 0.3f;
                seal.Glow = 0.35f;
                seal.Flicker = 0.1f;
                seal.FadeOut = 0.35f;
            }

            // Khung 3–4: lưỡi lửa đứng hai bên + dải lửa xoắn ôm người (trước / sau), hiện sau vòng phép, đập nhịp
            for (int i = 0; i < 2; i++)
            {
                string piece = i == 0 ? BlueFx.GuardFlameL : BlueFx.GuardFlameR;
                BlueFx.Fx fl = BlueKit.Fx.Play(piece, BlueKit.PlaceAt(piece, "feet", feet, aura, f), aura, duration + 0.05f, BlueFx.Behind);
                fl.Delay = 0.15f;
                fl.Stretch = new Vector2(f, 1f);
                fl.RiseTime = 0.2f;
                fl.Flicker = 0.18f;
                fl.FadeOut = 0.3f;
                BlueFx.Fx sw = BlueKit.Fx.Play(i == 0 ? BlueFx.GuardSwirlBack : BlueFx.GuardSwirlFront, feet, aura, duration - 0.2f,
                    i == 0 ? BlueFx.Behind : BlueFx.Front);
                sw.Delay = 0.3f;
                sw.Stretch = new Vector2(f, 1f);
                sw.GrowFrom = 0.7f;
                sw.GrowTime = 0.25f;
                sw.FadeIn = 0.2f;
                sw.FadeOut = 0.35f;
                sw.Color = new Color(1f, 1f, 1f, i == 0 ? 0.85f : 0.6f);
                sw.Pulse = 0.35f;
                sw.PulseRate = 2.2f;
                sw.PulsePhase = i * 0.5f;
            }

            var crystals = new BlueFx.FxRef[Crystals];
            float burn = burnInterval * 0.5f, skull = 0.35f;
            float spin = Random.Range(0f, 360f);
            float burnR = burnRadius * size;
            int skullIndex = Random.Range(0, Skulls.Length);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                Vector2 c = ctx.Center;

                // Lửa đốt kẻ địch chạm vào
                burn -= dt;
                if (burn <= 0f)
                {
                    burn = burnInterval;
                    if (ctx.HitCircle(c + Vector2.up * 0.3f, burnR, burnDamage, new HitEffect { knockback = new Vector2(0.8f, 0.4f) },
                            BlueKit.Main, false) > 0 && ctx.Fx != null)
                        ctx.Fx.Flames(c + new Vector2(f * burnR * 0.7f, -0.45f), 0.6f * aura, BlueKit.Flame, 0.3f);
                }

                // Đầu lâu lửa bốc lên sát quanh người (khung 3, 5, 6), lần lượt các biến thể
                skull -= dt;
                if (skull <= 0f && t < duration - 0.4f)
                {
                    skull = Random.Range(0.3f, 0.45f);
                    skullIndex = (skullIndex + 1 + Random.Range(0, 2)) % Skulls.Length;
                    float x = Random.Range(0.55f, 1f) * (Random.value < 0.5f ? -1f : 1f);
                    bool front = Random.value < 0.35f;
                    BlueFx.Fx s = BlueKit.Fx.Play(Skulls[skullIndex], c + new Vector2(x * aura, Random.Range(-0.2f, 0.8f)),
                        Random.Range(0.95f, 1.2f) * aura, 0.95f, front ? BlueFx.Front : BlueFx.Behind);
                    s.Velocity = new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0.8f, 1.2f)) * aura;
                    s.FadeIn = 0.22f;
                    s.FadeOut = 0.45f;
                    s.Glow = 0.3f;
                    s.Flicker = 0.2f;
                    s.Stretch = new Vector2(x < 0f ? -1f : 1f, 1f);
                }

                // Tinh thể bay vòng sát người (khung 5): nửa vòng trước vẽ trước mặt, nửa sau vẽ sau lưng
                if (t > 0.45f)
                {
                    spin += dt * 115f;
                    for (int i = 0; i < Crystals; i++)
                    {
                        BlueFx.Fx cr = crystals[i].Get;
                        if (cr == null)
                        {
                            cr = BlueKit.Fx.Play(i % 2 == 0 ? BlueFx.CrystalA : BlueFx.CrystalB, c, 1.2f * aura, duration - t + 0.3f);
                            cr.Pop = true;
                            cr.GrowFrom = 0.2f;
                            cr.GrowTime = 0.2f;
                            cr.Glow = 0.35f;
                            cr.FadeOut = 0.3f;
                            crystals[i] = new BlueFx.FxRef(cr);
                        }

                        float a = (spin + i * 360f / Crystals) * Mathf.Deg2Rad;
                        cr.Position = ctx.Chest + new Vector2(Mathf.Cos(a) * 1.15f, Mathf.Sin(a) * 0.3f + 0.25f * Mathf.Sin(t * 3f + i * 1.7f)) * aura;
                        cr.Order = Mathf.Sin(a) < 0f ? BlueFx.Front : BlueFx.Behind;
                        cr.Angle = Mathf.Cos(a) * 12f;
                    }
                }

                yield return null;
            }

            // Khung 6: thu thế thủ, lửa lắng xuống
            block?.Cancel();
            for (int i = 0; i < Crystals; i++)
                crystals[i].Stop(0.25f);
            ctx.HeroVisual?.SetWeaponPower(0f);
            yield return Wait(0.2f);
        }
    }

    // =====================================================================================================================
    // NÚT 3 — KĨ NĂNG ĐẶC BIỆT (cần nộ; ctx.Power theo mốc nộ)
    // =====================================================================================================================

    /// <summary>
    /// Nút 3 · thường — Kĩ năng đặc biệt (tấm "kĩ năng đặc biệt"): lửa xanh bùng lên, khom lấy đà → bật lên xoay chém thành vòng
    /// xoáy lửa (đánh quanh mình vài nhát) → giáng xuống nổ cột tinh thể lớn (đánh rộng, choáng). Mốc nộ càng cao càng đau, nổ
    /// càng rộng.
    /// Hình riêng của tấm này (không mượn hình chiêu khác): lửa chân (khung 2) → vành xoáy quay quanh người (khung 3) → dải
    /// xoáy + đá bay trước / sau người (khung 4) → giáng: tia giáng, cột tinh thể hai bên mọc lên, nứt đất + bậc đá, đá văng
    /// (khung 5). Lúc đáp không chớp trắng to (nhân vật vẫn thấy rõ giữa vụ nổ).
    /// </summary>
    [Serializable]
    public class BlueReaperStorm : Skill
    {
        [Tooltip("Mỗi nhát của vòng xoáy (× hệ số nộ)")]
        public float vortexDamage = 14f;
        [Tooltip("Nổ cột tinh thể lúc giáng xuống (× hệ số nộ)")]
        public float damage = 70f;
        [Tooltip("Bán kính nổ cột tinh thể (ở ×1, nhân Cỡ chiêu; mỗi mốc nộ thêm 0.3)")]
        public float radius = 3f;
        [Tooltip("Bật lên cao bấy nhiêu unit")]
        public float height = 3.2f;
        [Tooltip("Phần Cỡ chiêu áp vào hình (0..1): xoáy quanh người to 1 + (Cỡ chiêu − 1) × số này; cột tinh thể, nứt đất lúc giáng theo đủ. Vùng đánh vẫn nhân đủ Cỡ chiêu")]
        public float fxScale = 0.7f;

        public BlueReaperStorm()
        {
            name = "Kĩ năng đặc biệt";
            description = "Bật lên xoay chém thành vòng xoáy lửa, giáng xuống nổ cột tinh thể lớn.";
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float f = ctx.Facing;
            float power = ctx.Power;
            float size = ctx.Size; // vùng đánh × cỡ chiêu; độ cao bật lên giữ nguyên
            float vis = BlueKit.Visual(ctx, fxScale);
            ctx.HeroVisual?.SetWeaponPower(1f);
            Vector2 c = ctx.Center;
            Vector2 feet = c + Vector2.down * 0.5f;
            Transform body = BlueKit.BodyOf(ctx);

            if (ctx.Grounded)
            {
                // Khung 1–2: lửa xanh bùng lên hai bên chân, khom lấy đà, tinh thể đen bật lên quanh chân
                ctx.Hero?.Root(0.35f);
                ctx.Act(CombatPose.Rear, 0.36f, c + Vector2.up * 3f);
                for (int i = 0; i < 2; i++)
                {
                    string piece = i == 0 ? BlueFx.StormFlameL : BlueFx.StormFlameR;
                    BlueFx.Fx fl = BlueKit.Fx.Play(piece, BlueKit.PlaceAt(piece, "feet", feet, vis, f), vis, 0.6f, BlueFx.Behind);
                    fl.Stretch = new Vector2(f, 1f);
                    fl.RiseTime = 0.12f;
                    fl.Flicker = 0.15f;
                    fl.FadeOut = 0.25f;
                }

                if (ctx.Fx != null)
                {
                    ctx.Fx.Flash(ctx.Chest, 1.2f * vis, BlueKit.Main, 0.3f, true);
                    BlueKit.Shards(ctx, feet, BlueKit.Crystal, 6, 4.5f, 0.28f);
                    ctx.Fx.Cracks(feet, 0.9f * vis, BlueKit.Main, 0.6f);
                }

                yield return Wait(0.32f);
                if (ctx.Hero != null)
                    ctx.Hero.Launch(new Vector2(0f, ctx.Hero.Settings.LaunchSpeed(height)));
            }

            // Khung 3: vành xoáy quay quanh người đang bay lên; khung 4: dải xoáy + đá bay trước / sau người (bám người)
            ctx.Act(CombatPose.Spin, 0.72f);
            Vector2 offset = body != null ? ctx.Chest - (Vector2)body.position : Vector2.zero;
            BlueFx.Fx rim = BlueKit.Fx.Play(BlueFx.StormRim, ctx.Chest, vis, 0.26f);
            rim.Follow = body;
            rim.Offset = offset;
            rim.Stretch = new Vector2(f, 1f);
            rim.Spin = -620f * f;
            rim.GrowFrom = 0.6f;
            rim.GrowTime = 0.08f;
            rim.FadeOut = 0.1f;
            rim.Glow = 0.2f;
            for (int i = 0; i < 2; i++)
            {
                BlueFx.Fx sw = BlueKit.Fx.Play(i == 0 ? BlueFx.StormSwirlBack : BlueFx.StormSwirlFront, ctx.Chest, vis, 0.5f,
                    i == 0 ? BlueFx.Behind : BlueFx.Front);
                sw.Follow = body;
                sw.Offset = offset;
                sw.Delay = 0.16f;
                sw.Stretch = new Vector2(f, 1f);
                sw.GrowFrom = 0.75f;
                sw.GrowTime = 0.12f;
                sw.FadeIn = 0.06f;
                sw.FadeOut = 0.14f;
                sw.Color = new Color(1f, 1f, 1f, i == 0 ? 0.9f : 0.7f);
                sw.Pulse = 0.3f;
                sw.PulseRate = 8f;
                sw.PulsePhase = i * 0.5f;
            }

            int hits = 0;
            bool hover = false;
            for (float t = 0f; t < 0.62f; t += Time.deltaTime)
            {
                if (!hover && t > 0.36f && ctx.Hero != null)
                {
                    hover = true;
                    ctx.Hero.Hover(0.26f);
                }

                if (hits < 3 && t >= 0.08f + hits * 0.18f)
                {
                    hits++;
                    ctx.HitCircle(ctx.Center, 2.3f * size, vortexDamage * power, new HitEffect { knockback = new Vector2(-1f, 2.5f) }, BlueKit.Main);
                    BlueKit.Shards(ctx, ctx.Center, BlueKit.Crystal, 3, 5f, 0.22f, 8f);
                }

                yield return null;
            }

            // Khung 5: giáng xuống, nổ cột tinh thể
            ctx.Act(CombatPose.Slam, 0.55f, ctx.Center + new Vector2(f * 1f, -3f));
            yield return Wait(0.12f);
            ctx.Hero?.Launch(new Vector2(0f, -24f));
            for (float t = 0f; t < 0.7f && !ctx.Grounded; t += Time.deltaTime)
                yield return null;

            Crash(ctx, ctx.Center + Vector2.down * 0.5f, power);
            yield return Wait(0.42f);
            ctx.HeroVisual?.SetWeaponPower(0f);
        }

        // Nổ ở chỗ đáp (ground = mặt đất dưới chân): vùng nổ × cỡ chiêu, tâm cao 1 unit × cỡ chiêu (đáy sát đất). Hình khung 5:
        // tia giáng giữa, 2 cột tinh thể mọc lên hai bên, nứt đất + bậc đá, đá văng — to theo bán kính nổ (mốc nộ cao to hơn)
        void Crash(SkillContext ctx, Vector2 ground, float power)
        {
            float size = ctx.Size;
            float f = ctx.Facing;
            int tier = Mathf.Max(1, ctx.Tier);
            float r = (radius + 0.3f * (tier - 1)) * size;
            float k = (0.85f + 0.1f * (tier - 1)) * size; // nứt đất / cột tinh thể theo bán kính nổ
            float beamK = BlueKit.Visual(ctx, fxScale);

            BlueFx.Fx crack = BlueKit.Fx.Play(BlueFx.StormCrack, ground, k, 1.1f, BlueFx.Behind - 2);
            crack.Stretch = new Vector2(f, 1f);
            crack.GrowFrom = 0.4f;
            crack.GrowTime = 0.1f;
            crack.FadeOut = 0.5f;
            for (int i = 0; i < 2; i++)
            {
                string piece = i == 0 ? BlueFx.StormPillarL : BlueFx.StormPillarR;
                BlueFx.Fx p = BlueKit.Fx.Play(piece, BlueKit.PlaceAt(piece, "feet", ground, k, f), k, 0.85f, BlueFx.Behind);
                p.Delay = 0.03f * i;
                p.Stretch = new Vector2(f, 1f);
                p.RiseTime = 0.14f;
                p.FadeOut = 0.4f;
                p.Glow = 0.2f;
            }

            BlueFx.Fx beam = BlueKit.Fx.Play(BlueFx.StormBeam, ground, beamK, 0.45f, BlueFx.Behind);
            beam.Stretch = new Vector2(f, 1.2f);
            beam.RiseTime = 0.1f;
            beam.FadeOut = 0.25f;
            beam.Glow = 0.3f;
            BlueFx.Fx beamCore = BlueKit.Fx.PlayAdd(BlueFx.CoreOf(BlueFx.StormBeam) ?? BlueFx.StormBeam, ground, beamK, 0.2f, BlueFx.Behind + 1);
            beamCore.Stretch = new Vector2(f, 1.2f);
            beamCore.RiseTime = 0.08f;
            beamCore.Color = new Color(1f, 1f, 1f, 0.7f);

            if (ctx.Fx != null)
            {
                BlueKit.Shards(ctx, ground + Vector2.up * 0.2f, BlueKit.Crystal, 10 + 2 * tier, 7f, 0.3f, 18f, Vector2.up * 0.6f);
                BlueKit.Sparks(ctx, ground + Vector2.up * 0.3f, BlueKit.Hot, 10, 6f, 0.12f, 0.35f);
                ctx.Fx.Flash(ground + Vector2.up * 0.6f, 1.4f * beamK, BlueKit.Main, 0.18f, true);
            }

            ctx.HitCircle(ground + Vector2.up * size, r, damage * power,
                new HitEffect { knockback = new Vector2(6f, 7f), stun = 1f, heavy = true }, BlueKit.Main);
        }
    }

    /// <summary>
    /// Nút 3 · ↑ — Kĩ năng đặc biệt tầm xa (tấm "Kĩ năng đặc biệt tầm xa"): giơ lưỡi hái tụ cầu lửa xoáy → quét ra luồng lửa
    /// xanh khổng lồ chạy dọc mặt đất, đánh mọi kẻ địch trên đường (mỗi kẻ một lần) → tới cuối đường (gặp vách / mép bục thì
    /// dừng sớm) tinh thể đen trồi lên nổ tung.
    /// Luồng lửa (khung 3–4) ráp từ nhiều lớp: chỗ bắt đầu trước lưỡi hái, thân nối liền (3 biến thể xen nhau, hai đầu mờ chồng
    /// lên nhau nên không lộ mối nối, không lặp một mảng), đầu luồng nhọn chạy trước, khói đen trên, đá vụn dưới. Luồng dài ra
    /// bằng cách đặt thêm đoạn thân sau đầu luồng (không kéo giãn hình).
    /// </summary>
    [Serializable]
    public class BlueSoulTide : Skill
    {
        [Tooltip("Luồng lửa đi qua (× hệ số nộ)")]
        public float damage = 55f;
        [Tooltip("Nổ tinh thể ở cuối (× hệ số nộ)")]
        public float endDamage = 45f;
        [Tooltip("Tốc độ luồng lửa (unit/giây, ở ×1, nhân Tầm chiêu)")]
        public float speed = 16f;
        [Tooltip("Luồng lửa chạy xa bấy nhiêu unit (ở ×1, nhân Tầm chiêu; gặp vách / mép bục thì dừng sớm)")]
        public float range = 11f;
        [Tooltip("Phần Cỡ chiêu áp vào bề cao luồng lửa (0..1): cao 0.85 × (1 + (Cỡ chiêu − 1) × số này). Vùng đánh vẫn nhân đủ Cỡ chiêu")]
        public float fxScale = 0.85f;

        const int MaxPieces = 20;
        static readonly string[] Bodies = { BlueFx.GroundWave, BlueFx.GroundWaveB, BlueFx.GroundWaveC };

        public BlueSoulTide()
        {
            name = "Kĩ năng đặc biệt tầm xa";
            description = "Tụ cầu lửa xoáy, quét ra luồng lửa xanh khổng lồ chạy dọc mặt đất, nổ tinh thể ở cuối.";
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float f = ctx.Facing;
            float size = ctx.Size;
            ctx.Hero?.Root(1.2f);
            ctx.HeroVisual?.SetWeaponPower(1f);

            // Khung 2: giơ lưỡi hái, cầu lửa xoáy tụ lại ở đầu lưỡi hái
            float orbSize = 1.2f * BlueKit.Visual(ctx, 0.6f);
            ctx.Act(CombatPose.Rear, 0.55f, ctx.Center + Vector2.up * 3f);
            BlueFx.Fx orb = BlueKit.Fx.Play(BlueFx.Vortex, ctx.WeaponTip, orbSize, 0.7f, BlueFx.Behind);
            orb.Pop = true;
            orb.GrowFrom = 0.1f;
            orb.GrowTime = 0.4f;
            orb.Spin = 300f * f;
            orb.Glow = 0.6f;
            orb.FadeOut = 0.12f;
            var held = new BlueFx.FxRef(orb);
            float pulse = 0f;
            var orbAt = new Vector2(f * 0.3f, 0.1f) * orbSize; // tâm cầu lửa so với đầu lưỡi hái (cầu to thì lệch xa hơn)
            for (float t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                BlueFx.Fx o = held.Get;
                if (o != null)
                    o.Position = ctx.WeaponTip + orbAt;
                pulse -= Time.deltaTime;
                if (pulse <= 0f && ctx.Fx != null)
                {
                    pulse = 0.14f;
                    ctx.Fx.Flash(ctx.WeaponTip + orbAt, 0.7f * orbSize, BlueKit.Main, 0.16f);
                }

                yield return null;
            }

            // Khung 3: quét lưỡi hái, luồng lửa khổng lồ phóng ra dọc mặt đất
            Vector2 c = ctx.Center;
            ctx.Act(CombatPose.WhipAlt, 0.45f, c + new Vector2(f * 4f, 0f));
            yield return Wait(0.15f);
            held.Stop(0.1f);
            c = ctx.Center;
            float floor = ctx.Grounded ? c.y - 0.5f : BlueKit.FloorBelow(ctx, c, c.y - 0.5f);
            ctx.Combat.Linger(Tide(ctx, new Vector2(BlueKit.Ahead(ctx, c, 0.4f).x, floor), f, ctx.Power, Mathf.Max(1, ctx.Tier)));
            yield return Wait(0.35f);
            ctx.HeroVisual?.SetWeaponPower(0f);
        }

        // Luồng lửa: quãng chạy và tốc độ × tầm chiêu (cùng thời gian chạy), vẫn dừng ở vách / mép bục; bề cao luồng lửa theo
        // cỡ hình, vùng đánh và vụ nổ cuối × cỡ chiêu (đáy sát đất)
        IEnumerator Tide(SkillContext ctx, Vector2 ground, float f, float power, int tier)
        {
            float k = ctx.Size;
            float s = 0.85f * BlueKit.Visual(ctx, fxScale); // cỡ hình luồng lửa
            float v = speed * ctx.Range;
            float length = BlueKit.AlongGround(ctx, ground, f, range * ctx.Range);
            float segment = BlueFx.SizeOf(BlueFx.GroundWave).x * s;
            float ramp = 64f / BlueFx.PixelsPerUnit * s;     // đầu trái mỗi đoạn hiện dần chừng này, đè lên đuôi đoạn trước
            float step = segment - ramp;
            float headTip = BlueFx.PointOf(BlueFx.TideHead, "tip").x * s;   // điểm neo đầu luồng → mũi nhọn
            float headBody = BlueFx.PointOf(BlueFx.TideHead, "body").x * s; // điểm neo → hết phần thân đặc của đầu luồng
            var pieces = new BlueFx.FxRef[MaxPieces];
            var extras = new BlueFx.FxRef[MaxPieces];
            var hit = new HashSet<Health>();
            float head = 0f, shards = 0f;
            int placed = 0;

            // Chỗ bắt đầu (trước lưỡi hái): neo ở mép phải ảnh (mép đặc) = gốc luồng; đoạn thân đầu tiên đè lên mép đó
            BlueFx.Fx root = BlueKit.Fx.Play(BlueFx.TideStart, ground + Vector2.down * 0.05f, s, 30f);
            root.Stretch = new Vector2(f, 1f);
            root.GrowFrom = 0.5f;
            root.GrowTime = 0.1f;
            root.FadeIn = 0.04f;
            var rootRef = new BlueFx.FxRef(root);
            BlueFx.Fx tipFx = BlueKit.Fx.Play(BlueFx.TideHead, ground, s, 30f);
            tipFx.Stretch = new Vector2(f, 1f);
            tipFx.FadeIn = 0.05f;
            tipFx.Depth = -0.2f; // đè lên mọi đoạn thân
            var headRef = new BlueFx.FxRef(tipFx);

            while (head < length)
            {
                head = Mathf.Min(length, head + v * Time.deltaTime);
                // Đầu luồng nhọn chạy trước (phần thân đặc của nó che chỗ nối); đoạn thân mới đặt khi đã nằm gọn sau phần thân
                // đặc đó — luồng dài ra theo đầu luồng, không kéo giãn hình
                float pivot = Mathf.Max(head - headTip, -headBody * 0.6f);
                BlueFx.Fx hd = headRef.Get;
                if (hd != null)
                    hd.Position = new Vector2(ground.x + f * pivot, ground.y - 0.05f);
                while (placed < MaxPieces && placed * step - ramp + segment <= pivot + headBody)
                {
                    float from = placed * step - ramp;
                    BlueFx.Fx p = BlueKit.Fx.Play(Bodies[(placed * 2 + placed / 3) % Bodies.Length], new Vector2(ground.x + f * from, ground.y - 0.05f), s, 30f);
                    p.Stretch = new Vector2(f, 1f);
                    p.FadeIn = 0.05f;
                    p.Depth = -0.01f * (placed + 1); // đoạn sau đè lên đuôi đoạn trước
                    pieces[placed] = new BlueFx.FxRef(p);
                    // khói đen trên luồng (đoạn chẵn), đá vụn dưới chân luồng (đoạn lẻ)
                    float mid = from + segment * 0.5f;
                    BlueFx.Fx e = placed % 2 == 0
                        ? BlueKit.Fx.Play(placed % 4 == 0 ? BlueFx.TideSmokeA : BlueFx.TideSmokeB,
                            new Vector2(ground.x + f * mid, ground.y + BlueFx.SizeOf(BlueFx.GroundWave).y * s * 0.86f), s * 0.9f, 30f, BlueFx.Front + 2)
                        : BlueKit.Fx.Play(BlueFx.TideRocks, new Vector2(ground.x + f * mid, ground.y), s * 0.7f, 30f, BlueFx.Front + 2);
                    e.Stretch = new Vector2(f, 1f);
                    e.FadeIn = 0.1f;
                    e.Velocity = new Vector2(f * 0.4f, placed % 2 == 0 ? 0.35f : 0f);
                    e.Color = new Color(1f, 1f, 1f, placed % 2 == 0 ? 0.8f : 1f);
                    extras[placed] = new BlueFx.FxRef(e);
                    placed++;
                }

                // Đầu luồng đánh mọi kẻ địch nó chạm tới, mỗi kẻ một lần
                Vector2 tip = new Vector2(ground.x + f * head, ground.y);
                // (vùng đánh là đoạn ngay sau đầu luồng, không lùi quá chỗ bắt đầu: không đánh sau lưng)
                float back = Mathf.Max(0.3f, Mathf.Min(1.6f * k, head));
                List<Health> touching = BlueKit.Touching(ctx, tip + new Vector2(-f * back * 0.5f, 1.4f * k), new Vector2(back, 2.8f * k));
                for (int i = 0; i < touching.Count; i++)
                {
                    if (hit.Add(touching[i]))
                        BlueKit.HitOne(ctx, touching[i], damage * power,
                            new HitEffect { knockback = new Vector2(3f, 6f), stun = 0.3f, heavy = true }, BlueKit.Main);
                }

                shards -= Time.deltaTime;
                if (shards <= 0f)
                {
                    shards = 0.1f;
                    BlueKit.Shards(ctx, tip, BlueKit.Crystal, 2, 4f, 0.2f);
                }

                yield return null;
            }

            // Khung 5: tinh thể đen trồi lên nổ tung ở cuối luồng (lõi trắng loé trước)
            Vector2 end = new Vector2(ground.x + f * length, ground.y);
            float ek = (1.05f + 0.1f * (tier - 1)) * k;
            BlueFx.Fx erupt = BlueKit.Fx.Play(BlueFx.Eruption, end, ek, 1.1f);
            erupt.RiseTime = 0.15f;
            erupt.Glow = 0.3f;
            erupt.FadeOut = 0.45f;
            erupt.Stretch = new Vector2(f, 1f);
            BlueFx.Fx eCore = BlueKit.Fx.PlayAdd(BlueFx.CoreOf(BlueFx.Eruption) ?? BlueFx.Eruption, end, ek, 0.3f, BlueFx.Front + 1);
            eCore.RiseTime = 0.12f;
            eCore.Stretch = new Vector2(f, 1f);
            eCore.Color = new Color(1f, 1f, 1f, 0.75f);
            if (ctx.Fx != null)
            {
                BlueKit.Shards(ctx, end + Vector2.up * 0.3f, BlueKit.Crystal, 9, 6f, 0.28f, 18f, Vector2.up * 0.5f);
                ctx.Fx.Flash(end + Vector2.up * (1.2f * k), 2f * k, BlueKit.Main, 0.25f);
            }

            ctx.HitCircle(end + Vector2.up * (1.2f * k), 1.9f * k, endDamage * power, new HitEffect { knockback = new Vector2(2f, 9f), heavy = true },
                BlueKit.Main);
            yield return Wait(0.2f);
            for (int i = 0; i < placed; i++)
            {
                pieces[i].Stop(0.45f);
                extras[i].Stop(0.35f);
            }

            headRef.Stop(0.2f);
            rootRef.Stop(0.45f);
        }
    }

    /// <summary>
    /// Nút 3 · ↓ — Kĩ năng đặc biệt áp chế (tấm "Kĩ năng đặc biệt áp chế"): chĩa lưỡi hái phóng xích lửa xanh trói kẻ địch gần
    /// nhất phía trước → kẻ địch bị giam trong lồng xích có cột giáo, tinh thể và lửa xanh: choáng lâu, mất máu liên tục, hết
    /// giam thì lồng vỡ nổ tinh thể. Boss sư tử chỉ choáng một phần (da cứng: LionBoss tự rút ngắn) nên lồng giam boss cũng
    /// ngắn hơn. Không có ai trong tầm thì xích bay tới cuối tầm, lồng dựng ở đó giam ai đứng trong.
    /// Hình theo tấm: xích bay từ lưỡi hái tới (khung 2) → xích quấn quanh thân mục tiêu, vòng trước / sau mục tiêu, siết lại
    /// (khung 3) → lồng mọc lên: lớp sau (xích treo, ánh sáng) trước, rồi cột giáo + chóp + đế phía trước mục tiêu (khung 4–5).
    /// </summary>
    [Serializable]
    public class BlueSoulPrison : Skill
    {
        [Tooltip("Trói (× hệ số nộ)")]
        public float damage = 30f;
        [Tooltip("Lửa trong lồng: mỗi nhát (× hệ số nộ)")]
        public float tickDamage = 12f;
        public float tickInterval = 0.4f;
        [Tooltip("Lồng vỡ nổ tinh thể (× hệ số nộ)")]
        public float finisherDamage = 40f;
        [Tooltip("Giam (choáng) bấy nhiêu giây ở mốc nộ 1; mỗi mốc thêm 0.4 giây. Boss bị choáng ngắn hơn")]
        public float holdTime = 3.2f;
        [Tooltip("Tầm phóng xích (unit, ở ×1, nhân Tầm chiêu)")]
        public float range = 9f;

        const int ChainLinks = 14;

        public BlueSoulPrison()
        {
            name = "Kĩ năng đặc biệt áp chế";
            description = "Phóng xích trói kẻ địch gần nhất phía trước, giam trong lồng xích lửa xanh: choáng lâu, mất máu liên tục.";
        }

        public override IEnumerator Run(SkillContext ctx)
        {
            float f = ctx.Facing;
            float power = ctx.Power;
            float size = ctx.Size;
            int tier = Mathf.Max(1, ctx.Tier);
            ctx.Hero?.Root(0.8f);
            // Tầm phóng xích × tầm chiêu; mục tiêu tìm được thì xích, lồng đặt đúng trên người nó (không nhân tầm)
            Health target = BlueKit.NearestAhead(ctx, range * ctx.Range);
            Vector2 point;
            float floor;
            Bounds body;
            if (target != null)
            {
                body = BlueKit.BoundsOf(target);
                point = body.center;
                floor = BlueKit.FloorBelow(ctx, body.center, body.min.y);
                floor = Mathf.Min(floor, body.min.y + 0.05f);
            }
            else
            {
                point = BlueKit.Ahead(ctx, ctx.Center, range * 0.6f, 0.4f);
                floor = BlueKit.FloorBelow(ctx, point, ctx.Center.y - 0.5f);
                body = new Bounds(new Vector3(point.x, floor + 0.95f, 0f), new Vector3(1f, 1.9f, 0f));
            }

            // Khung 2: chĩa lưỡi hái tới, xích lửa xanh phóng ra từ lưỡi hái trói mục tiêu
            ctx.Act(CombatPose.Reach, 0.6f, point);
            ctx.HeroVisual?.SetWeaponPower(1f);
            yield return Wait(0.2f);
            BlueKit.Sparks(ctx, ctx.WeaponTip, BlueKit.Hot, 6, 3.5f, 0.1f, 0.25f, 0f, ctx.Forward);
            var chains = new[] { new BlueFx.FxRef[ChainLinks], new BlueFx.FxRef[ChainLinks] };
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float reach = Mathf.Clamp01(t / 0.14f);
                DrawChain(chains[0], ctx.WeaponTip, point + new Vector2(0f, 0.25f), reach, 0.6f * size, size);
                DrawChain(chains[1], ctx.WeaponTip, point + new Vector2(0f, -0.35f), reach, -0.5f * size, size);
                yield return null;
            }

            // Khung 3: trói, mục tiêu choáng; xích quấn quanh thân rồi lồng xích dựng lên quanh nó
            float hold = holdTime + 0.4f * (tier - 1);
            ctx.HitBox(body.center, (Vector2)body.size * 0.9f, damage * power, // đúng thân mục tiêu đang bị trói
                new HitEffect { stun = hold, heavy = true }, BlueKit.Main);
            LionBoss lion = target != null ? target.GetComponentInParent<LionBoss>() : null;
            if (lion != null)
                hold = Mathf.Max(1f, lion.StunRemaining); // boss da cứng: choáng ngắn, lồng giam cũng ngắn
            for (int k = 0; k < 2; k++)
                for (int i = 0; i < ChainLinks; i++)
                    chains[k][i].Stop(0.2f);
            ctx.Combat.Linger(Prison(ctx, body, floor, hold, power));
            yield return Wait(0.28f);
            ctx.HeroVisual?.SetWeaponPower(0f);
        }

        // Xích: các đoạn xích (ảnh xích của tấm) nối nhau từ a tới b, duỗi tới phần reach, võng nhẹ sag unit ở giữa. scale = cỡ
        // chiêu: đoạn xích to và dài hơn, nên cùng số đoạn vẫn với tới mục tiêu ở tầm xa hơn
        static void DrawChain(BlueFx.FxRef[] links, Vector2 a, Vector2 b, float reach, float sag, float scale)
        {
            Vector2 segment = BlueFx.SizeOf(BlueFx.Chain);
            float size = 1.1f * scale;
            float len = segment.x * size * 0.92f;
            Vector2 d = b - a;
            float total = d.magnitude * reach;
            int need = Mathf.Min(links.Length, Mathf.CeilToInt(total / len));
            for (int i = 0; i < links.Length; i++)
            {
                if (i >= need)
                {
                    links[i].Stop(0f);
                    continue;
                }

                float s0 = Mathf.Min(1f, i * len / Mathf.Max(0.01f, d.magnitude));
                float s1 = Mathf.Min(1f, (i + 1) * len / Mathf.Max(0.01f, d.magnitude));
                Vector2 p0 = a + d * s0 + Vector2.up * (Mathf.Sin(Mathf.PI * s0) * sag * reach * 0.3f);
                Vector2 p1 = a + d * s1 + Vector2.up * (Mathf.Sin(Mathf.PI * s1) * sag * reach * 0.3f);
                BlueFx.Fx fx = links[i].Get;
                if (fx == null)
                {
                    fx = BlueKit.Fx.Play(BlueFx.Chain, p0, size, 2f);
                    fx.Anchor = new Vector2(0f, 0.5f);
                    fx.Glow = 0.5f;
                    fx.FadeIn = 0f;
                    links[i] = new BlueFx.FxRef(fx);
                }

                fx.Position = p0;
                Vector2 dir = p1 - p0;
                fx.Angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
        }

        IEnumerator Prison(SkillContext ctx, Bounds body, float floor, float hold, float power)
        {
            float f = ctx.Facing;
            var foot = new Vector2(body.center.x, floor - 0.05f);
            // Khung 3: xích quấn quanh thân mục tiêu (vòng sau lưng nó + vòng trước người nó), siết lại; vừa thân mục tiêu
            float wrap = Mathf.Max(1f, (body.max.y - floor) / BlueFx.SizeOf(BlueFx.ChainWrapBack).y * 1.05f);
            for (int i = 0; i < 2; i++)
            {
                BlueFx.Fx w = BlueKit.Fx.Play(i == 0 ? BlueFx.ChainWrapBack : BlueFx.ChainWrapFront, foot, wrap, hold + 0.4f,
                    i == 0 ? BlueFx.Behind : BlueFx.Front);
                w.Stretch = new Vector2(f, 1f);
                w.GrowFrom = 1.3f;
                w.GrowTime = 0.18f;
                w.FadeIn = 0.06f;
                w.FadeOut = 0.25f;
                w.Glow = 0.25f;
                w.Color = new Color(1f, 1f, 1f, i == 0 ? 0.85f : 0.95f);
            }

            // Khung 4–5: lồng xích mọc lên từ đất quanh mục tiêu — lớp sau (xích treo, ánh sáng) trước, rồi cột giáo + chóp + đế
            // phía trước. Lồng đứng đúng chỗ mục tiêu và vừa với nó (không nhân cỡ chiêu — giống lồng của RED)
            Vector2 cage = BlueFx.SizeOf(BlueFx.CageFront);
            float height = Mathf.Max(3.1f, (body.max.y - floor) * 1.18f);
            float size = height / cage.y;
            float width = cage.x * size;
            BlueFx.Fx back = BlueKit.Fx.Play(BlueFx.CageBack, foot, size, hold + 0.5f, BlueFx.Behind);
            back.Delay = 0.12f;
            back.RiseTime = 0.25f;
            back.Glow = 0.2f;
            back.FadeOut = 0.25f;
            back.Flicker = 0.06f;
            BlueFx.Fx front = BlueKit.Fx.Play(BlueFx.CageFront, foot, size, hold + 0.4f, BlueFx.Front);
            front.Delay = 0.2f;
            front.RiseTime = 0.25f;
            front.Glow = 0.25f;
            front.FadeOut = 0.25f;
            front.Flicker = 0.05f;
            var heldBack = new BlueFx.FxRef(back);
            var heldFront = new BlueFx.FxRef(front);
            if (ctx.Fx != null)
            {
                ctx.Fx.Flames(foot, width * 0.6f, BlueKit.Flame, hold, true);
                BlueKit.Shards(ctx, foot + Vector2.up * 0.2f, BlueKit.Crystal, 6, 4f, 0.22f);
                ctx.Fx.Flash(foot + Vector2.up * height * 0.5f, 1.6f * ctx.Size, BlueKit.Main, 0.25f);
            }

            var area = new Vector2(width * 0.7f, height * 0.9f);
            Vector2 middle = foot + Vector2.up * (height * 0.48f);
            float elapsed = 0f;
            while (elapsed + tickInterval <= hold + 1e-3f)
            {
                yield return Wait(tickInterval);
                elapsed += tickInterval;
                // Lửa trong lồng đốt liên tục
                if (ctx.HitBox(middle, area, tickDamage * power, default, BlueKit.Main, false) > 0)
                    BlueKit.Sparks(ctx, middle + Random.insideUnitCircle * (0.5f * ctx.Size), BlueKit.Hot, 4, 3f, 0.1f, 0.3f, -2f);
            }

            if (hold - elapsed > 0.01f)
                yield return Wait(hold - elapsed);

            // Hết giam: lồng vỡ tung — lồng loé sáng nở ra rồi tắt (chính hình lồng, cộng sáng), tinh thể / đá văng, tia lửa
            heldBack.Stop(0.2f);
            heldFront.Stop(0.15f);
            BlueFx.Fx shatter = BlueKit.Fx.PlayAdd(BlueFx.CageFront, foot, size, 0.3f);
            shatter.GrowFrom = 0.95f;
            shatter.GrowTime = 0.05f;
            shatter.EndSize = 1.18f;
            shatter.Color = new Color(1f, 1f, 1f, 0.8f);
            shatter.FadeOut = 0.25f;
            BlueKit.Shards(ctx, middle, BlueKit.Crystal, 12, 7f, 0.28f, 14f, Vector2.up * 0.3f);
            BlueKit.Sparks(ctx, middle, BlueKit.Hot, 12, 6f, 0.13f, 0.35f);
            ctx.Fx?.Flash(middle, Mathf.Min(3f, height * 0.5f), BlueKit.Main, 0.2f);
            ctx.HitBox(middle, area, finisherDamage * power, new HitEffect { knockback = new Vector2(3f, 4f), heavy = true }, BlueKit.Main);
        }
    }
}
