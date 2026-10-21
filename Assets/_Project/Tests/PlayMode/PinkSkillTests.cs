using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// 9 chiêu thật của PINK (Combat/Heroes/PinkSkills.cs), mỗi chiêu làm đúng việc tấm tranh của nó cho thấy: combo 3 đòn, hất
    /// tung, gai pha lê đẩy ra + chặn đòn, lướt đánh trên đường, rồng pha lê bay xa, vòng phép chặn đòn + đánh nhẹ, mái vòm nổ
    /// theo mốc nộ, tia pha lê bay xa nổ hình sao, xích trói + lồng giam choáng lâu (boss ngắn hơn). Hiệu ứng cắt từ tranh
    /// (PinkFx) phải nạp được và hiện ra. Màn hang động: mặt bệ xuất phát y = -1 (bệ từ x = -5 tới 5), bia tập ở x = 3.7.
    /// Màn đánh boss: sàn y = 0. Cỡ / tầm chiêu của PINK (Combat Controller, mặc định ×2) đọc từ ReachOf, đúng với mọi hệ số;
    /// kiểm thử riêng phần cỡ / tầm: ReachPinkTests.
    /// </summary>
    public class PinkSkillTests
    {
        const float Floor = -1f;
        const float DummyX = 3.7f;

        static readonly string[] Names =
        {
            "Đánh thường", "Đánh thường hất tung", "Đánh thường thủ",
            "Dùng chiêu áp sát", "Dùng chiêu tầm xa", "Dùng chiêu thủ",
            "Kĩ năng đặc biệt", "Kĩ năng đặc biệt tầm xa", "Kĩ năng đặc biệt áp chế",
        };

        // Mọi mảnh cắt từ tranh (mỗi chiêu từ tấm của nó, đã tách lớp): Tools/art/hero_fx_pink.py
        static readonly string[] FxSprites =
        {
            "crescent_small", "crescent_big", "crescent_core", "sparkle", "sparkle_shards", "shard",
            "rise_small", "rise_slash", "rise_core", "rise_splash", "rise_crystals", "rise_shard",
            "guard_ring", "guard_ray", "spikes_back", "spike_tall", "spikes_front", "spikes_low", "guard_crystal",
            "afterimage", "streak_long", "streak_short", "sweep", "sweep_band", "dust", "dust_b",
            "circle_front", "circle_side", "bolt_tip", "dragon", "dragon_core", "dragon_head", "dragon_ring", "dragon_blast", "dragon_rocks",
            "ward_circle", "ward_circle_back", "ward_swirl", "diamond", "diamond_b",
            "nova_runes", "nova_arc", "nova_dome_a", "nova_dome", "nova_front", "nova_rune_arc", "nova_rocks", "nova_shard",
            "orb", "circle_lance", "lance", "lance_core", "lance_tip", "lance_tail", "star_blast", "star_rays", "lance_shard",
            "chain", "chain_wrap", "cage", "cage_back", "seal_shard",
        };

        OctopusController _player;
        CombatController _combat;
        Rigidbody2D _body;
        LionBoss _boss;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        /// <summary>Cỡ / tầm chiêu đang đặt cho PINK.</summary>
        SkillReach Reach => _combat.ReachOf(PlayableCharacter.Pink);

        IEnumerator Load(string scene = "Playground")
        {
            CharacterSelection.Choose(PlayableCharacter.Pink);
            yield return SceneManager.LoadSceneAsync(scene);
            _player = Object.FindAnyObjectByType<OctopusController>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _combat = _player.GetComponent<CombatController>();
            _body = _player.GetComponent<Rigidbody2D>();
            _boss = Object.FindAnyObjectByType<LionBoss>();
            if (_boss != null)
                _boss.SetAi(false);
            yield return new WaitForFixedUpdate();
        }

        IEnumerator StandAt(float x, float floor, float facing = 1f)
        {
            _player.SetAim(0);
            _player.SetIntent(false, facing);
            yield return new WaitForFixedUpdate();
            yield return null;
            _player.SetIntent(false, 0f);
            _player.ResetTo(new Vector2(x, floor + HeroMotor.FeetOffset + 0.05f));
            for (float t = 0f; t < 1.5f && !_player.Hero.Grounded; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(_player.Hero.Grounded, "Phải đứng trên sàn");
        }

        static Health DummyAt(float x) =>
            Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Where(d => Mathf.Abs(d.transform.position.x - x) < 0.5f)
                .Select(d => d.GetComponent<Health>()).First();

        IEnumerator WaitIdle(float max = 6f)
        {
            for (float t = 0f; t < max && _combat.Busy; t += Time.deltaTime)
                yield return null;
            Assert.IsFalse(_combat.Busy, "Chiêu chạy quá lâu");
        }

        // ---------- Bộ chiêu, ảnh hiệu ứng ----------

        [UnityTest]
        public IEnumerator NineSkills_NamedAfterSheets_FxSpritesLoad()
        {
            yield return Load();
            Skill[] set = _combat.SkillsOf(PlayableCharacter.Pink);
            Assert.That(set.Length, Is.EqualTo(9));
            for (int i = 0; i < 9; i++)
            {
                Assert.That(set[i].name, Is.EqualTo(Names[i]), $"Chiêu {i}");
                Assert.IsFalse(set[i] is HeroPlaceholderSkill, $"{Names[i]} không còn là chiêu tạm");
                Assert.IsFalse(string.IsNullOrEmpty(set[i].description));
            }

            Assert.That(set[3].cooldown, Is.GreaterThan(0f), "Dùng chiêu có hồi chiêu");
            foreach (string name in FxSprites)
            {
                Sprite sprite = PinkFx.Sprite(name);
                Assert.IsNotNull(sprite, $"Thiếu ảnh hiệu ứng {name} (Resources/HeroFx/Pink)");
                Assert.That(sprite.bounds.size.x, Is.InRange(0.1f, 6f), $"{name}: cỡ hợp lý (px / unit theo tranh)");
            }

            Assert.IsNotNull(Resources.Load<Material>("HeroFx/Pink/PinkFxAlpha"));
        }

        // ---------- Nút 1 ----------

        // Bấm liên tiếp: 3 đòn (vệt trăng nhỏ, vệt trăng to, đâm trượng chớp sao — đòn nặng)
        [UnityTest]
        public IEnumerator Basic_ThreeHitCombo_ThirdIsHeavyThrust()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.5f, Floor);
            Health dummy = DummyAt(DummyX);
            var hits = new List<(float amount, bool heavy)>();
            dummy.Damaged += (amount, from, effect) => hits.Add((amount, effect.heavy));
            bool crescent = false, sparkle = false;
            float tipGap = -1f;
            Assert.IsTrue(_combat.Use(0, 0));
            for (float t = 0f; t < 3f && (hits.Count < 3 || _combat.Busy); t += Time.deltaTime)
            {
                if (t > 0.15f && hits.Count < 2)
                    _combat.Use(0, 0); // bấm liên tục: xếp hàng nối combo
                crescent |= PinkFx.Instance.IsShowing("crescent_small") || PinkFx.Instance.IsShowing("crescent_big");
                sparkle |= PinkFx.Instance.IsShowing("sparkle");
                // vệt trăng lớn vừa hiện: gốc ở đúng mũi trượng thật (không bị đẩy ra theo tầm ×2)
                if (tipGap < 0f && PinkFx.Instance.TryPosition("crescent_big", out Vector2 at))
                    tipGap = Vector2.Distance(at, _player.Hero.Visual.WeaponTip);
                yield return null;
            }

            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(3), "Đủ 3 đòn");
            Assert.That(hits[0].amount, Is.EqualTo(18f).Within(0.01f));
            Assert.That(hits[1].amount, Is.EqualTo(20f).Within(0.01f));
            Assert.That(hits[2].amount, Is.EqualTo(32f).Within(0.01f), "Đòn 3: đâm trượng");
            Assert.IsFalse(hits[0].heavy);
            Assert.IsTrue(hits[2].heavy, "Đòn 3 là đòn nặng");
            Assert.IsTrue(crescent, "Hiện vệt trăng tím cắt từ tranh");
            Assert.IsTrue(sparkle, "Đòn 3 hiện chớp sao");
            Assert.That(tipGap, Is.InRange(0f, 0.35f), "Vệt trăng bám mũi trượng");

            // Ngừng bấm quá lâu: combo bắt đầu lại từ đòn 1
            yield return WaitIdle();
            yield return new WaitForSeconds(0.9f);
            hits.Clear();
            Assert.IsTrue(_combat.Use(0, 0));
            for (float t = 0f; t < 1f && hits.Count == 0; t += Time.deltaTime)
                yield return null;
            Assert.That(hits[0].amount, Is.EqualTo(18f).Within(0.01f), "Combo bắt đầu lại");
        }

        // Hất tung: bia tập bị hất bay lên
        [UnityTest]
        public IEnumerator Launcher_LaunchesDummyUpward()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.5f, Floor);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            float dealt = 0f;
            Vector2 knock = Vector2.zero;
            dummy.Damaged += (amount, from, effect) =>
            {
                dealt += amount;
                knock = effect.knockback;
            };
            bool slash = false;
            float rise = 0f;
            Assert.IsTrue(_combat.Use(0, 1));
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                slash |= PinkFx.Instance.IsShowing("rise_slash");
                rise = Mathf.Max(rise, view.Displacement.y);
                yield return null;
            }

            Assert.That(dealt, Is.EqualTo(24f).Within(0.01f));
            Assert.That(knock.y, Is.GreaterThan(6f), "Hất lên mạnh");
            Assert.That(rise, Is.GreaterThan(0.3f), "Bia tập bay lên");
            Assert.IsTrue(slash, "Hiện vệt trăng dựng đứng");
        }

        // Thủ: kẻ địch đứng sát bị đánh + đẩy ra; trong lúc thủ thế thì chặn sát thương (màn đánh boss)
        [UnityTest]
        public IEnumerator Guard_HitsAndPushesAdjacentDummy_BlocksDuringPose()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.4f, Floor);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            float dealt = 0f;
            dummy.Damaged += (amount, from, effect) => dealt += amount;
            float push = 0f;
            bool spikes = false, ring = false;
            float x0 = _body.position.x;
            Assert.IsTrue(_combat.Use(0, 2));
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                push = Mathf.Max(push, view.Displacement.x);
                spikes |= PinkFx.Instance.IsShowing("spikes_front") && PinkFx.Instance.IsShowing("spikes_back");
                ring |= PinkFx.Instance.IsShowing("guard_ring");
                yield return null;
            }

            Assert.That(dealt, Is.EqualTo(20f).Within(0.01f));
            Assert.That(push, Is.GreaterThan(0.2f), "Bia tập bị đẩy ra xa");
            Assert.IsTrue(spikes, "Gai pha lê trồi lên (trước + sau lưng)");
            Assert.IsTrue(ring, "Vòng chữ xoắn quanh người");
            Assert.That(Mathf.Abs(_body.position.x - x0), Is.LessThan(0.1f), "Đứng yên thủ thế");

            yield return Load("BossArena");
            var health = _player.GetComponent<PlayerHealth>();
            yield return StandAt(-10f, 0f);
            Vector2 left = _body.position + Vector2.left * 2f;
            Assert.IsTrue(_combat.Use(0, 2));
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(health.IsBlocking, "Đang thủ thế thì chặn đòn");
            Assert.IsTrue(health.Hit(40f, left, new Vector2(5f, 3f), true));
            Assert.That(health.Current, Is.EqualTo(health.Max), "Bị chặn: không mất máu");
            yield return WaitIdle();
            yield return new WaitForSeconds(0.1f);
            Assert.IsFalse(health.IsBlocking, "Hết thế thủ thì thôi chặn");
        }

        // ---------- Nút 2 ----------

        // Áp sát: lướt tới (đi được quãng dài = quãng ×1 × tầm chiêu), kẻ địch trên đường bị đánh, nhát quét cuối đánh phía trước
        [UnityTest]
        public IEnumerator DashSweep_DashesForward_HitsOnPathAndSweeps()
        {
            yield return Load();
            float dist = ((PinkDashSweep)_combat.SkillsOf(PlayableCharacter.Pink)[3]).distance * Reach.range;
            // đứng lùi lại cho lướt hết quãng vẫn dừng trên bệ (mép bệ x = 5), đi xuyên qua bia tập
            yield return StandAt(Mathf.Max(-4.6f, 4.5f - dist), Floor);
            Health dummy = DummyAt(DummyX);
            var hits = new List<float>();
            dummy.Damaged += (amount, from, effect) => hits.Add(amount);
            float x0 = _body.position.x;
            bool ghost = false, sweep = false;
            Assert.IsTrue(_combat.Use(1, 0));
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                ghost |= PinkFx.Instance.IsShowing("afterimage");
                sweep |= PinkFx.Instance.IsShowing("sweep");
                yield return null;
            }

            Assert.That(_body.position.x - x0, Is.GreaterThan(dist * 0.8f), "Lướt tới hết quãng (× tầm chiêu)");
            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(1), "Đánh trúng bia trên đường / nhát quét");
            Assert.IsTrue(ghost, "Để lại bóng mờ");
            Assert.IsTrue(sweep, "Quét lưỡi liềm");
            Assert.That(_combat.CooldownRemaining(1, 0), Is.GreaterThan(1f), "Đang hồi chiêu");
        }

        // Tầm xa: rồng pha lê bay một quãng mới trúng bia tập ở xa, nổ đá vụn
        [UnityTest]
        public IEnumerator DragonBolt_TravelsFar_HitsDistantDummy()
        {
            yield return Load();
            yield return StandAt(DummyX - 6.2f, Floor);
            Health dummy = DummyAt(DummyX);
            float dealt = 0f, hitAt = -1f, launchAt = -1f;
            dummy.Damaged += (amount, from, effect) =>
            {
                dealt += amount;
                if (hitAt < 0f)
                    hitAt = Time.time;
            };
            bool circle = false, side = false, dragon = false, blast = false, headAtBlast = false;
            float start = Time.time;
            Assert.IsTrue(_combat.Use(1, 1));
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                circle |= PinkFx.Instance.IsShowing("circle_front");
                side |= PinkFx.Instance.IsShowing("circle_side") && PinkFx.Instance.IsShowing("bolt_tip") && launchAt < 0f;
                bool flying = PinkFx.Instance.IsShowing("dragon") && PinkFx.Instance.IsShowing("dragon_head") && hitAt < 0f;
                if (flying && launchAt < 0f)
                    launchAt = Time.time;
                dragon |= flying;
                bool blasting = PinkFx.Instance.IsShowing("dragon_blast");
                // nổ: đầu rồng còn đọng lại cùng vụ nổ (thấy rõ rồng kể cả khi trúng gần)
                headAtBlast |= blasting && PinkFx.Instance.IsShowing("dragon_head") && PinkFx.Instance.IsShowing("dragon_rocks");
                blast |= blasting;
                yield return null;
            }

            Assert.That(dealt, Is.EqualTo(34f).Within(0.01f), "Trúng một lần");
            Assert.That(launchAt - start, Is.GreaterThan(0.3f), "Vòng phép trước rồi rồng mới bay ra");
            Assert.That(hitAt - launchAt, Is.GreaterThan(0.05f), "Rồng bay một quãng mới trúng (không đánh tức thì)");
            Assert.IsTrue(circle, "Vòng phép trước trượng");
            Assert.IsTrue(side, "Pha K3: vòng nghiêng + mũi pha lê trước khi rồng bay");
            Assert.IsTrue(headAtBlast, "Đầu rồng đọng lại cùng vụ nổ + đá vụn");
            Assert.IsTrue(dragon, "Rồng pha lê bay");
            Assert.IsTrue(blast, "Nổ đá vụn chỗ trúng");
        }

        // Dùng chiêu thủ: bia tập chạm vòng phép mất máu nhiều nhịp; màn đánh boss: chặn mọi đòn trong lúc vòng phép còn
        [UnityTest]
        public IEnumerator Ward_TicksTouchingDummy_BlocksWhileUp()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.5f, Floor);
            Health dummy = DummyAt(DummyX);
            int ticks = 0;
            dummy.Damaged += (amount, from, effect) => ticks++;
            bool circle = false;
            Assert.IsTrue(_combat.Use(1, 2));
            for (float t = 0f; t < 1.7f; t += Time.deltaTime)
            {
                circle |= PinkFx.Instance.IsShowing("ward_circle") && PinkFx.Instance.IsShowing("ward_circle_back")
                    && PinkFx.Instance.IsShowing("diamond") && PinkFx.Instance.IsShowing("ward_swirl");
                yield return null;
            }

            Assert.That(ticks, Is.GreaterThanOrEqualTo(3), "Đánh nhẹ liên tục");
            Assert.IsTrue(circle, "Vòng phép dưới chân + pha lê bay quanh");

            yield return Load("BossArena");
            var health = _player.GetComponent<PlayerHealth>();
            yield return StandAt(-10f, 0f);
            Vector2 left = _body.position + Vector2.left * 2f;
            Assert.IsTrue(_combat.Use(1, 2));
            int blocked = 0;
            health.Blocked += hit => blocked++;
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.3f);
                Assert.IsTrue(health.IsBlocking, $"Vòng phép còn thì chặn ({i})");
                health.Hit(30f, left, new Vector2(5f, 3f), true);
            }

            Assert.That(health.Current, Is.EqualTo(health.Max), "Chặn hết sát thương");
            Assert.That(blocked, Is.EqualTo(4));
            yield return WaitIdle();
            yield return new WaitForSeconds(0.1f);
            Assert.IsFalse(health.IsBlocking, "Hết vòng phép thì thôi chặn");
            Assert.IsTrue(health.Hit(30f, left, Vector2.zero));
            Assert.That(health.Current, Is.LessThan(health.Max));
        }

        // ---------- Nút 3 ----------

        // Kĩ năng đặc biệt: mái vòm nổ quanh mình, đau theo mốc nộ
        [UnityTest]
        public IEnumerator Nova_DomeAroundSelf_ScalesWithRage()
        {
            yield return Load();
            Health dummy = DummyAt(DummyX);
            float dealt = 0f;
            dummy.Damaged += (amount, from, effect) => dealt += amount;
            var damage = new float[4];
            foreach (int tier in new[] { 1, 3 })
            {
                yield return StandAt(DummyX - 1.6f, Floor);
                dummy.Restore();
                dealt = 0f;
                _combat.Rage.Add(RageMeter.PerTier * tier);
                bool dome = false;
                Assert.IsTrue(_combat.Use(2, 0));
                for (float t = 0f; t < 2f; t += Time.deltaTime)
                {
                    dome |= PinkFx.Instance.IsShowing("nova_dome") && PinkFx.Instance.IsShowing("nova_front")
                        && PinkFx.Instance.IsShowing("nova_rocks");
                    yield return null;
                }

                Assert.IsTrue(dome, "Mái vòm pha lê (vòm sau lưng + chân vòm trước người + đá vụn)");
                damage[tier] = dealt;
                Assert.That(dealt, Is.EqualTo(60f * _combat.Rage.Multiplier(tier)).Within(0.5f), $"Mốc {tier}");
                yield return new WaitForSeconds(5f); // bia tập hồi máu
            }

            Assert.That(damage[3], Is.GreaterThan(damage[1]), "Nộ cao hơn thì đau hơn");
        }

        // Kĩ năng đặc biệt tầm xa: tia pha lê bay xa trúng bia tập, nổ hình sao
        [UnityTest]
        public IEnumerator StarLance_FiresFar_StarBlast()
        {
            yield return Load();
            yield return StandAt(DummyX - 6.2f, Floor);
            Health dummy = DummyAt(DummyX);
            float dealt = 0f;
            dummy.Damaged += (amount, from, effect) => dealt += amount;
            _combat.Rage.Add(RageMeter.PerTier);
            bool orb = false, lance = false, star = false;
            Assert.IsTrue(_combat.Use(2, 1));
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                orb |= PinkFx.Instance.IsShowing("orb");
                lance |= PinkFx.Instance.IsShowing("lance") && dealt <= 0f;
                star |= PinkFx.Instance.IsShowing("star_blast");
                yield return null;
            }

            Assert.That(dealt, Is.EqualTo(75f * _combat.Rage.Multiplier(1)).Within(0.5f));
            Assert.IsTrue(orb, "Cầu sáng đầu trượng");
            Assert.IsTrue(lance, "Tia pha lê bay");
            Assert.IsTrue(star, "Nổ hình sao");
        }

        // Áp chế: xích trói kẻ địch gần nhất phía trước, giam (choáng lâu) và mất máu liên tục
        [UnityTest]
        public IEnumerator ChainSeal_BindsNearestAhead_LongStunAndDamageOverTime()
        {
            yield return Load();
            yield return StandAt(DummyX - 4f, Floor);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            int hits = 0;
            dummy.Damaged += (amount, from, effect) => hits++;
            _combat.Rage.Add(RageMeter.PerTier);
            bool chains = false, cage = false;
            int renderers = 0;
            Assert.IsTrue(_combat.Use(2, 2));
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                chains |= PinkFx.Instance.ActiveChains > 0;
                cage |= PinkFx.Instance.IsShowing("cage") && PinkFx.Instance.IsShowing("cage_back");
                renderers = Mathf.Max(renderers, PinkFx.Instance.ActiveRenderers);
                yield return null;
            }

            Assert.IsTrue(chains, "Xích phóng ra");
            Assert.IsTrue(cage, "Lồng xích (cột trước + cột giữa sau mục tiêu)");
            Assert.That(renderers, Is.LessThan(40), "Số renderer hợp lý (xích vẽ Tiled, không 40 mắt mỗi sợi)");
            Assert.IsTrue(view.IsStunned, "Bị trói: choáng");
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(view.IsStunned, "Choáng lâu (2.5 giây sau vẫn còn)");
            yield return new WaitForSeconds(1f);
            Assert.That(hits, Is.GreaterThanOrEqualTo(5), "Mất máu liên tục khi bị giam");
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(view.IsStunned, "Hết giam");
        }

        // ---------- Màn đánh boss ----------

        // Cả 9 chiêu đánh trúng sư tử
        [UnityTest]
        public IEnumerator BossArena_AllNineSkills_HitLion()
        {
            yield return Load("BossArena");
            var failed = new List<string>();
            for (int i = 0; i < 9; i++)
            {
                int slot = i / 3, variant = i % 3;
                bool far = i == 4 || i == 7 || i == 8;
                yield return StandAt(_boss.Body.min.x - (far ? 5f : 1.2f), 0f);
                if (slot == 2)
                    _combat.Rage.Add(RageMeter.PerTier);
                float before = _boss.Health.Current;
                var cd = (float[])typeof(CombatController).GetField("_cooldown", System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance).GetValue(_combat);
                System.Array.Clear(cd, 0, cd.Length);
                Assert.IsTrue(_combat.Use(slot, variant), Names[i]);
                for (float t = 0f; t < 3f && (_combat.Busy || _boss.Health.Current >= before); t += Time.deltaTime)
                    yield return null;
                if (_boss.Health.Current >= before)
                    failed.Add(Names[i]);
                yield return WaitIdle();
                yield return new WaitForSeconds(0.4f);
            }

            Assert.That(failed, Is.Empty, "Không trúng sư tử: " + string.Join(", ", failed));
        }

        // Áp chế lên boss: bị trói choáng nhưng ngắn hơn hẳn bia tập (da cứng), lồng tan theo
        [UnityTest]
        public IEnumerator BossArena_ChainSeal_StunsLionShorter()
        {
            yield return Load("BossArena");
            yield return StandAt(_boss.Body.min.x - 5f, 0f);
            _combat.Rage.Add(RageMeter.PerTier);
            Assert.IsTrue(_combat.Use(2, 2));
            float maxStun = 0f;
            bool cage = false;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                maxStun = Mathf.Max(maxStun, _boss.StunRemaining);
                cage |= PinkFx.Instance.IsShowing("cage");
                yield return null;
            }

            Assert.That(maxStun, Is.GreaterThan(0.3f), "Boss bị trói choáng");
            Assert.That(maxStun, Is.LessThan(1.5f), "Boss choáng ngắn hơn bia tập (3 giây)");
            Assert.IsTrue(cage, "Lồng xích giam boss");
            yield return new WaitForSeconds(1.6f);
            Assert.IsFalse(_boss.IsStunned, "Boss hết choáng");
            Assert.IsFalse(PinkFx.Instance.IsShowing("cage"), "Lồng tan khi boss hết choáng");
        }
    }
}
