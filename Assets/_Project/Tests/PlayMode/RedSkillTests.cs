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
    /// 9 chiêu thật của RED (Combat/Heroes/RedSkills.cs) theo tấm chiêu của người dùng: đúng tên / thứ tự, hiệu ứng cắt từ tấm nạp
    /// được (Resources/HeroFx/Red), và mỗi chiêu làm đúng việc của nó với bia tập (màn hang động: bia ở x = 3.7, mặt bệ y = -1)
    /// lẫn với sư tử (màn đánh boss: sàn y = 0): combo 3 đòn, hất tung, lướt chém trên đường, xoay đánh quanh mình, sóng bay xa
    /// rồi nổ, thế thủ chặn đòn + đánh kẻ đứng trong vòng, chuỗi chém X, sóng khổng lồ, xích trói kéo lại rồi giam (choáng lâu,
    /// mất máu liên tục; boss giam ngắn hơn).
    /// </summary>
    public class RedSkillTests
    {
        const float PlaygroundFloor = -1f;
        const float ArenaFloor = 0f;
        const float DummyX = 3.7f;

        static readonly string[] Names =
        {
            "Đánh thường", "Đánh thường hất tung", "Đánh thường áp sát",
            "Kĩ năng", "Kĩ năng tầm xa", "Kĩ năng thủ",
            "Kĩ năng đặc biệt", "Kĩ năng đặc biệt tầm xa", "Kĩ năng đặc biệt áp chế",
        };

        OctopusController _player;
        HeroMotor _hero;
        CombatController _combat;
        Rigidbody2D _body;
        LionBoss _boss;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        IEnumerator Load(string scene = "Playground")
        {
            CharacterSelection.Choose(PlayableCharacter.Red);
            yield return SceneManager.LoadSceneAsync(scene);
            _player = Object.FindAnyObjectByType<OctopusController>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _hero = _player.Hero;
            _combat = _player.GetComponent<CombatController>();
            _body = _player.GetComponent<Rigidbody2D>();
            _boss = Object.FindAnyObjectByType<LionBoss>();
            if (_boss != null)
                _boss.SetAi(false);
            yield return new WaitForFixedUpdate();
        }

        float Feet => _body.position.y - HeroMotor.FeetOffset;

        // Đứng yên trên sàn ở x, quay mặt về phía facing
        IEnumerator StandAt(float x, float floorY, float facing = 1f)
        {
            _player.ReleaseJump();
            _player.SetAim(0);
            _player.SetIntent(false, facing);
            yield return new WaitForFixedUpdate();
            yield return null;
            _player.SetIntent(false, 0f);
            _player.ResetTo(new Vector2(x, floorY + HeroMotor.FeetOffset + 0.05f));
            for (float t = 0f; t < 1.5f && !_hero.Grounded; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(_hero.Grounded, "Phải đứng trên sàn");
            Assert.That(_player.Facing, Is.EqualTo(Mathf.Sign(facing)));
        }

        static Health DummyAt(float x) =>
            Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Where(d => Mathf.Abs(d.transform.position.x - x) < 0.5f)
                .Select(d => d.GetComponent<Health>()).First();

        IEnumerator Use(int slot, int variant)
        {
            if (slot == 2)
                _combat.Rage.Add(RageMeter.PerTier);
            Assert.IsTrue(_combat.Use(slot, variant), $"Dùng được chiêu {Names[slot * 3 + variant]}");
            yield return null;
        }

        IEnumerator WaitIdle(float max = 6f)
        {
            for (float t = 0f; t < max && _combat.Busy; t += Time.deltaTime)
                yield return null;
        }

        // ---------- Bộ chiêu, ảnh hiệu ứng ----------

        [UnityTest]
        public IEnumerator SkillSet_NamesOrder_RealSkills_FxSpritesLoad()
        {
            yield return Load();
            Skill[] set = _combat.SkillsOf(PlayableCharacter.Red);
            Assert.That(set.Length, Is.EqualTo(9));
            for (int i = 0; i < 9; i++)
            {
                Assert.That(set[i].name, Is.EqualTo(Names[i]), $"Tên chiêu {i}");
                Assert.IsFalse(set[i] is HeroPlaceholderSkill, $"{Names[i]}: phải là chiêu thật");
            }

            Assert.That(set[0].cooldown, Is.EqualTo(0f), "Đánh thường không hồi chiêu");
            Assert.That(set[3].cooldown, Is.GreaterThan(0f));

            // mọi lớp hiệu ứng cắt từ tấm nạp được; vệt chém nào có lõi trắng riêng thì lõi khớp khung với thân
            foreach (string name in RedFx.SpriteNames)
                Assert.IsNotNull(RedFx.Instance.Sprite(name), $"Ảnh hiệu ứng {name} (Resources/HeroFx/Red)");

            // Pivot đúng chỗ: đầu sóng ở mép phải (đuôi cũng neo ở đầu sóng), nổ đất gốc ở đáy, cùng tỉ lệ với nhân vật (PPU 150)
            Sprite wave = RedFx.Instance.Sprite(RedFx.WaveSprite);
            Assert.That(wave.pivot.x / wave.rect.width, Is.GreaterThan(0.9f), "Sóng: pivot ở đầu sóng");
            Sprite tail = RedFx.Instance.Sprite(RedFx.BigWaveTailSprite);
            Assert.That(tail.pivot.x / tail.rect.width, Is.GreaterThan(0.9f), "Đuôi sóng neo ở đầu sóng (kéo dài ra sau)");
            Sprite burst = RedFx.Instance.Sprite(RedFx.WaveBurstSprite);
            Assert.That(burst.pivot.y / burst.rect.height, Is.LessThan(0.12f), "Nổ tinh thể: gốc ở mặt đất");
            Assert.That(wave.pixelsPerUnit, Is.EqualTo(150f).Within(0.01f));
            foreach (string name in RedFx.SpriteNames)
            {
                Sprite s = RedFx.Instance.Sprite(name);
                Assert.That(s.pivot.x, Is.InRange(0f, s.rect.width), $"{name}: pivot nằm trong ảnh");
                Assert.That(s.pivot.y, Is.InRange(0f, s.rect.height), $"{name}: pivot nằm trong ảnh");
                Assert.That(Mathf.Max(s.rect.width, s.rect.height), Is.LessThanOrEqualTo(700f), $"{name}: ảnh gọn (dễ xếp atlas)");
            }

            // lồng xích tách lớp: vòng phép dưới chân rộng hơn thân bia tập, vòng quấn hẹp hơn để siết vừa thân
            Assert.That(RedFx.Instance.Sprite(RedFx.CageBaseBack).bounds.size.x, Is.InRange(1.1f, 2f));
            Assert.That(RedFx.Instance.Sprite(RedFx.CageWrapSprite).bounds.size.x, Is.InRange(0.5f, 1.2f));

            // Ô cỡ hình (fxGrow) có giá trị mặc định cả khi Combat Controller nạp bộ chiêu cũ từ màn chơi
            Assert.That(((RedComboSlash)set[0]).fxGrow.x, Is.GreaterThan(0.1f), "fxGrow của Đánh thường có giá trị");
            Assert.That(((RedChainPrison)set[8]).chainWidth, Is.GreaterThan(0.05f), "Bề dày xích có giá trị");
        }

        // ---------- Hình hiệu ứng bám kiếm thật (cỡ ×2), lớp sau / trước, hình riêng của từng chiêu ----------

        IHeroVisual Visual => _player.GetComponent<PlayerCharacter>().HeroVisual;

        // Chờ tới khi lớp hiệu ứng sprite hiện; trả vị trí + thứ tự vẽ, ghi lại vị trí ngực / mũi kiếm thật lúc đó
        IEnumerator WaitFx(string sprite, float max, Vector2[] at, int[] order, Vector2[] chest, Vector2[] tip)
        {
            at[0] = new Vector2(float.NaN, float.NaN);
            for (float t = 0f; t < max; t += Time.deltaTime)
            {
                if (RedFx.Instance.TryGetActive(sprite, out Vector2 p, out int o))
                {
                    at[0] = p;
                    order[0] = o;
                    chest[0] = _hero.Chest;
                    tip[0] = Visual.WeaponTip;
                    yield break;
                }

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SlashFx_AnchoredToRealSwing_NotLiftedAtDoubleSize()
        {
            yield return Load();
            Assert.That(_combat.ReachOf(PlayableCharacter.Red).size, Is.EqualTo(2f), "Kiểm ở cỡ ×2 mặc định");
            var at = new Vector2[1];
            var order = new int[1];
            var chest = new Vector2[1];
            var tip = new Vector2[1];
            // (chiêu, ảnh vệt, cho phép lệch khỏi ngực thật)
            var cases = new (int slot, int variant, string sprite)[]
            {
                (0, 0, RedFx.ComboSlashSprite), (0, 2, RedFx.DashSlashSprite), (1, 0, RedFx.SpinRingFront),
                (2, 0, RedFx.BigSlashSprite),
            };
            foreach (var c in cases)
            {
                yield return StandAt(c.variant == 2 ? -2f : DummyX - 4f, PlaygroundFloor);
                yield return Use(c.slot, c.variant);
                yield return WaitFx(c.sprite, 1.5f, at, order, chest, tip);
                Assert.IsFalse(float.IsNaN(at[0].x), $"{c.sprite}: hiệu ứng hiện");
                Assert.That(Mathf.Abs(at[0].x - chest[0].x), Is.LessThan(0.6f), $"{c.sprite}: neo ngang ở người (không đẩy ra trước)");
                Assert.That(at[0].y - chest[0].y, Is.InRange(-0.7f, 0.35f),
                    $"{c.sprite}: neo ở chỗ vung kiếm thật, không bị nâng lên theo cỡ ×2 (lệch {at[0].y - chest[0].y:0.00})");
                Assert.That(order[0], Is.GreaterThan(10), $"{c.sprite}: vẽ trước người");
                yield return WaitIdle();
                yield return new WaitForSeconds(0.9f);
            }

            // sóng tầm xa bung ra từ mũi kiếm thật
            yield return StandAt(DummyX - 6f, PlaygroundFloor);
            yield return Use(1, 1);
            yield return WaitFx(RedFx.WaveLaunchSprite, 1.5f, at, order, chest, tip);
            Assert.That(Vector2.Distance(at[0], tip[0]), Is.LessThan(0.35f), "Cung phóng bung ở mũi kiếm");
            yield return WaitIdle();
        }

        [UnityTest]
        public IEnumerator EachSkill_UsesItsOwnSheetShapes_FrontBackLayers()
        {
            yield return Load();
            var at = new Vector2[1];
            var order = new int[1];
            var chest = new Vector2[1];
            var tip = new Vector2[1];

            // Áp sát: chém vòng NGANG của tấm áp sát, không dùng lưỡi liềm dọc của hất tung
            yield return StandAt(-2f, PlaygroundFloor);
            yield return Use(0, 2);
            bool vertical = false, horizontal = false;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                vertical |= RedFx.Instance.TryGetActive(RedFx.RisingSlashSprite, out _);
                horizontal |= RedFx.Instance.TryGetActive(RedFx.DashSlashSprite, out _);
                yield return null;
            }

            Assert.IsTrue(horizontal, "Áp sát: vệt ngang khung 4 của tấm áp sát");
            Assert.IsFalse(vertical, "Áp sát: không dùng lưỡi liềm dọc của hất tung");
            yield return WaitIdle();
            yield return new WaitForSeconds(0.5f);

            // Kĩ năng: một vòng → nhiều vòng → nổ, mỗi vòng có nửa sau (sau người) và nửa trước (trước người)
            yield return StandAt(DummyX - 2.5f, PlaygroundFloor);
            yield return Use(1, 0);
            string[] seq = { RedFx.SpinRingBack, RedFx.SpinRingsBack, RedFx.CrystalRingBack };
            string[] front = { RedFx.SpinRingFront, RedFx.SpinRingsFront, RedFx.CrystalRingFront };
            for (int i = 0; i < seq.Length; i++)
            {
                yield return WaitFx(seq[i], 1.5f, at, order, chest, tip);
                Assert.IsFalse(float.IsNaN(at[0].x), $"{seq[i]} hiện");
                Assert.That(order[0], Is.LessThan(-6), $"{seq[i]}: nửa sau vẽ sau người");
                Assert.IsTrue(RedFx.Instance.TryGetActive(front[i], out _, out int o), $"{front[i]} hiện cùng lúc");
                Assert.That(o, Is.GreaterThan(10), $"{front[i]}: nửa trước vẽ trước người");
            }

            yield return WaitIdle();
            yield return new WaitForSeconds(0.5f);

            // Kĩ năng đặc biệt: cung lớn → cung thứ hai → nhánh X "" rồi "/" + mảnh vỡ
            yield return StandAt(DummyX - 4f, PlaygroundFloor);
            yield return Use(2, 0);
            foreach (string name in new[] { RedFx.BigSlashSprite, RedFx.SecondArcSprite, RedFx.XArmASprite, RedFx.XArmBSprite,
                         RedFx.XDebrisSprite })
            {
                yield return WaitFx(name, 2.5f, at, order, chest, tip);
                Assert.IsFalse(float.IsNaN(at[0].x), $"Kĩ năng đặc biệt: {name} hiện theo thứ tự");
            }

            yield return WaitIdle();
        }

        [UnityTest]
        public IEnumerator AllSkills_KeepRendererCountReasonable()
        {
            yield return Load();
            int most = 0;
            for (int i = 0; i < 9; i++)
            {
                yield return StandAt(i == 2 ? -2f : DummyX - 4f, PlaygroundFloor);
                foreach (Health h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                    h.Restore();
                yield return Use(i / 3, i % 3);
                for (float t = 0f; t < 6f && (_combat.Busy || t < 1f); t += Time.deltaTime)
                {
                    most = Mathf.Max(most, RedFx.Instance.RendererCount);
                    yield return null;
                }

                yield return new WaitForSeconds(0.6f);
                var cd = (float[])typeof(CombatController).GetField("_cooldown",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_combat);
                System.Array.Clear(cd, 0, cd.Length);
            }

            Assert.That(most, Is.LessThanOrEqualTo(48), "Số SpriteRenderer hiệu ứng RED cùng lúc vừa phải cho điện thoại");
        }

        // ---------- Nút 1 ----------

        [UnityTest]
        public IEnumerator Combo_ThreeHits_FinisherSlamsGroundHeavier()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.5f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            var hits = new List<(float amount, HitEffect effect)>();
            dummy.Damaged += (amount, from, effect) => hits.Add((amount, effect));

            yield return Use(0, 0);
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(_combat.Use(0, 0), "Bấm tiếp trong lúc chém: xếp hàng đòn 2");
            Assert.IsTrue(_combat.Use(0, 0), "Xếp hàng đòn 3");
            yield return WaitIdle();

            Assert.That(hits.Count, Is.EqualTo(3), "3 đòn đều trúng");
            Assert.IsFalse(hits[0].effect.heavy);
            Assert.IsTrue(hits[2].effect.heavy, "Đòn 3 bổ xuống đất là đòn nặng");
            Assert.That(hits[2].amount, Is.GreaterThan(hits[0].amount * 1.5f), "Đòn 3 đau hơn hẳn");
            Assert.That(hits[2].effect.knockback.y, Is.GreaterThan(hits[0].effect.knockback.y), "Nổ đất hất lên");

            // Quá lâu không bấm: combo bắt đầu lại từ đòn 1
            yield return new WaitForSeconds(0.9f);
            hits.Clear();
            yield return Use(0, 0);
            yield return WaitIdle();
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.IsFalse(hits[0].effect.heavy, "Hết khoảng nối combo: lại là đòn 1");
        }

        [UnityTest]
        public IEnumerator RisingSlash_LaunchesTarget_RedHopsThenLands()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.4f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            HitEffect hit = default;
            dummy.Damaged += (amount, from, effect) => hit = effect;
            float start = Feet, top = Feet, lift = 0f;

            yield return Use(0, 1);
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                top = Mathf.Max(top, Feet);
                lift = Mathf.Max(lift, view.Displacement.y);
                if (t > 0.5f && !_combat.Busy && _hero.Grounded)
                    break;
                yield return null;
            }

            Assert.That(hit.knockback.y, Is.GreaterThan(6f), "Hất tung kẻ địch lên");
            Assert.That(lift, Is.GreaterThan(0.3f), "Bia tập bị hất lên khỏi chỗ đứng");
            Assert.That(top - start, Is.InRange(0.5f, 2.2f), "RED bật nhẹ lên theo");
            Assert.IsTrue(_hero.Grounded, "Rồi đáp xuống");
            Assert.That(_combat.CooldownRemaining(0, 1), Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator DashSlash_DashesForward_HitsTargetsAlongThePath()
        {
            yield return Load();
            // quãng lướt thật = quãng gốc × tầm chiêu của RED (ô Cỡ / tầm chiêu); đứng lùi lại cho lướt qua bia tập mà vẫn dừng
            // trên bệ xuất phát (bệ x -5..5)
            float dash = ((RedDashSlash)_combat.SkillsOf(PlayableCharacter.Red)[2]).distance * _combat.ReachOf(PlayableCharacter.Red).range;
            yield return StandAt(Mathf.Max(-4.5f, 4.3f - dash), PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            float dealt = 0f;
            dummy.Damaged += (amount, from, effect) => dealt += amount;
            float x0 = _body.position.x;

            yield return Use(0, 2);
            bool dashed = false;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                dashed |= _hero.IsDashing;
                yield return null;
            }

            yield return WaitIdle();
            Assert.IsTrue(dashed, "Lướt tới (HeroMotor.Dash)");
            Assert.That(_body.position.x - x0, Is.GreaterThan(dash * 0.78f), "Lướt thấp tới trước đủ quãng (qua cả bia tập)");
            Assert.That(dealt, Is.GreaterThan(0f), "Chém trúng bia tập trên đường lướt");
        }

        // ---------- Nút 2 ----------

        [UnityTest]
        public IEnumerator SpinBurst_HitsAllAround_IncludingBehind()
        {
            yield return Load();
            // bia tập ở SAU lưng
            yield return StandAt(DummyX + 1.3f, PlaygroundFloor, 1f);
            Health dummy = DummyAt(DummyX);
            var hits = new List<HitEffect>();
            dummy.Damaged += (amount, from, effect) => hits.Add(effect);
            yield return Use(1, 0);
            yield return WaitIdle();
            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(3), "Các vòng chém quanh thân + vụ nổ tinh thể đều trúng cả phía sau");
            Assert.IsTrue(hits.Last().heavy, "Nổ vòng tinh thể là đòn nặng");
            Assert.That(_combat.CooldownRemaining(1, 0), Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator CrescentWave_ProjectileTravelsFar_ExplodesOnTarget()
        {
            yield return Load();
            // xa 6 unit (bệ xuất phát chỉ rộng x -5..5; bia thứ hai ở x = -3.9 nằm sau lưng)
            yield return StandAt(DummyX - 6f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            float firstHit = -1f, dealt = 0f;
            float start = Time.time;
            dummy.Damaged += (amount, from, effect) =>
            {
                if (firstHit < 0f)
                    firstHit = Time.time - start;
                dealt += amount;
            };

            float x0 = _body.position.x;
            yield return Use(1, 1);
            for (float t = 0f; t < 2.5f && firstHit < 0f; t += Time.deltaTime)
                yield return null;
            // sóng rời mũi kiếm sau 0.28 giây giơ kiếm + lúc đâm tới; tầm × tầm chiêu nhưng tốc độ cũng × tầm nên thời gian bay hết
            // tầm không đổi
            var wave = (RedCrescentWave)_combat.SkillsOf(PlayableCharacter.Red)[4];
            float launch = 0.28f + HeroRigAnimator.StrikeAt(CombatPose.Reach) * 0.45f;
            Assert.That(firstHit, Is.GreaterThan(launch), "Sóng phải bay tới (bia đứng xa 6 unit): trúng sau lúc phóng");
            Assert.That(firstHit, Is.LessThan(launch + wave.range / wave.speed + 0.2f), "Trúng trong lúc sóng còn bay");
            Assert.That(Mathf.Abs(_body.position.x - x0), Is.LessThan(0.3f), "RED đứng tại chỗ phóng sóng (không lao tới)");
            yield return new WaitForSeconds(0.3f);
            Assert.That(dealt, Is.GreaterThan(30f), "Sóng trúng + nổ tinh thể");
        }

        [UnityTest]
        public IEnumerator GuardCircle_DamagesEnemiesInsideCircle_OverTime()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.1f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            int ticks = 0;
            dummy.Damaged += (amount, from, effect) => ticks++;
            float x0 = _body.position.x;
            yield return Use(1, 2);
            yield return new WaitForSeconds(0.9f);
            Assert.IsTrue(_combat.Busy, "Đang giữ thế thủ");
            Assert.That(Object.FindAnyObjectByType<RedFx>().ActiveCount, Is.GreaterThan(0), "Vòng phép đang hiện");
            yield return WaitIdle();
            Assert.That(ticks, Is.GreaterThanOrEqualTo(3), "Kẻ địch đứng trong vòng phép mất máu liên tục");
            Assert.That(Mathf.Abs(_body.position.x - x0), Is.LessThan(0.1f), "Cắm kiếm đứng yên");
        }

        [UnityTest]
        public IEnumerator GuardCircle_BlocksLionAttacks_WhileCircleLasts()
        {
            yield return Load("BossArena");
            var health = _player.GetComponent<PlayerHealth>();
            yield return StandAt(_boss.transform.position.x - 7.5f, ArenaFloor);
            int blocked = 0;
            health.Blocked += hit => blocked++;
            float hp = health.Current;
            _boss.ForceAttack(LionAttack.ClawSwipe);
            yield return Use(1, 2);
            Assert.IsTrue(health.IsBlocking, "Thế thủ chặn đòn ngay khi bấm");
            for (float t = 0f; t < 2f && blocked == 0; t += Time.deltaTime)
                yield return null;
            Assert.That(blocked, Is.GreaterThan(0), "Vả vuốt của boss trúng thế thủ");
            Assert.That(health.Current, Is.EqualTo(hp), "Không mất máu");
            yield return WaitIdle();
            Assert.IsFalse(health.IsBlocking, "Hết vòng phép thì hết chặn");
        }

        // ---------- Nút 3 ----------

        [UnityTest]
        public IEnumerator FuryCross_DashSlashesThenGiantX_ScalesWithRage()
        {
            yield return Load();
            float[] totals = new float[2];
            for (int run = 0; run < 2; run++)
            {
                yield return StandAt(DummyX - 4f, PlaygroundFloor);
                Health dummy = DummyAt(DummyX);
                dummy.Restore();
                int hits = 0;
                float dealt = 0f;
                void Count(float amount, Vector2 from, HitEffect effect)
                {
                    hits++;
                    dealt += amount;
                }

                dummy.Damaged += Count;
                float x0 = _body.position.x;
                _combat.Rage.Add(RageMeter.PerTier * (run == 0 ? 1f : 3f));
                Assert.IsTrue(_combat.Use(2, 0));
                yield return WaitIdle();
                dummy.Damaged -= Count;
                Assert.That(_body.position.x - x0, Is.GreaterThan(1f), "Lao tới chém");
                Assert.That(hits, Is.GreaterThanOrEqualTo(3), "Chém liên tiếp + chữ X + sóng xung kích");
                totals[run] = dealt;
                yield return new WaitForSeconds(0.5f);
            }

            Assert.That(totals[1], Is.GreaterThan(totals[0] * 1.2f), "Mốc nộ cao đau hơn");
        }

        [UnityTest]
        public IEnumerator GrandWave_FliesVeryFar_BigExplosionOnTarget()
        {
            yield return Load();
            yield return StandAt(DummyX - 6f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            float dealt = 0f;
            var effects = new List<HitEffect>();
            dummy.Damaged += (amount, from, effect) =>
            {
                dealt += amount;
                effects.Add(effect);
            };

            yield return Use(2, 1);
            yield return WaitIdle();
            Assert.That(dealt, Is.GreaterThan(80f), "Sóng khổng lồ bay tới bia + nổ lớn");
            Assert.IsTrue(effects.Any(e => e.stun > 0f), "Nổ lớn làm choáng");
        }

        [UnityTest]
        public IEnumerator ChainPrison_BindsPullsPunches_CagesWithLongStunAndDot()
        {
            yield return Load();
            yield return StandAt(DummyX - 5f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            int hits = 0;
            dummy.Damaged += (amount, from, effect) => hits++;
            float minX = 0f;

            yield return Use(2, 2);
            for (float t = 0f; t < 3f && _combat.Busy; t += Time.deltaTime)
            {
                minX = Mathf.Min(minX, view.Displacement.x);
                yield return null;
            }

            Assert.That(minX, Is.LessThan(-1.5f), "Xích kéo bia tập lại gần");
            Assert.IsTrue(view.IsStunned, "Bị giam: choáng");
            Assert.That(RedFx.Instance.CageCount, Is.EqualTo(1), "Lồng xích đang giam");
            int afterSkill = hits;
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(view.IsStunned, "Choáng lâu (vẫn còn sau 2 giây)");
            Assert.That(hits - afterSkill, Is.GreaterThanOrEqualTo(3), "Trong lồng mất máu liên tục");
            yield return new WaitForSeconds(2.2f);
            Assert.That(RedFx.Instance.CageCount, Is.EqualTo(0), "Hết giam thì lồng tắt");
            Assert.IsFalse(view.IsStunned);
        }

        [UnityTest]
        public IEnumerator ChainPrison_Lion_ShorterCage_StillStunsAndBleeds()
        {
            yield return Load("BossArena");
            yield return StandAt(_boss.Body.min.x - 4f, ArenaFloor);
            float hp = _boss.Health.Current;
            yield return Use(2, 2);
            bool stunned = false;
            float maxStun = 0f;
            for (float t = 0f; t < 3f && _combat.Busy; t += Time.deltaTime)
            {
                stunned |= _boss.IsStunned;
                maxStun = Mathf.Max(maxStun, _boss.StunRemaining);
                yield return null;
            }

            Assert.IsTrue(stunned, "Boss bị trói / giam choáng");
            Assert.That(maxStun, Is.LessThan(2f), "Boss chỉ bị giam ngắn (kháng khống chế)");
            Assert.That(RedFx.Instance.CageCount, Is.EqualTo(1), "Lồng xích quanh boss");
            float afterSkill = _boss.Health.Current;
            Assert.That(afterSkill, Is.LessThan(hp));
            yield return new WaitForSeconds(2.5f);
            Assert.That(RedFx.Instance.CageCount, Is.EqualTo(0), "Lồng của boss tắt sớm hơn bia tập");
            Assert.That(_boss.Health.Current, Is.LessThan(afterSkill), "Trong lồng boss vẫn mất máu");
        }

        // ---------- Cả 9 chiêu với sư tử ----------

        [UnityTest]
        public IEnumerator AllNineSkills_HitTheLion([Values(0, 1, 2)] int slot, [Values(0, 1, 2)] int variant)
        {
            yield return Load("BossArena");
            bool ranged = variant == 1 && slot > 0;
            yield return StandAt(_boss.Body.min.x - (ranged ? 6f : 1.2f), ArenaFloor);
            float hp = _boss.Health.Current;
            yield return Use(slot, variant);
            yield return WaitIdle();
            yield return new WaitForSeconds(0.6f);
            Assert.That(_boss.Health.Current, Is.LessThan(hp), $"{Names[slot * 3 + variant]}: đánh trúng sư tử");
        }
    }
}
