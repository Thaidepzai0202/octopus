using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// Cỡ / tầm chiêu của BLUE (Combat Controller › Cỡ / tầm chiêu): ở (2, 2) chiêu đánh tới chỗ ngoài tầm ở (1, 1), vùng đánh và
    /// hiệu ứng to gấp đôi, lướt / sóng bay / xích xa gấp đôi; đặt lại (1, 1) thì về đúng tầm cũ. Hệ số đặt thẳng vào field
    /// riêng blueReach bằng reflection (không có hàm đặt công khai).
    /// Màn hang động: mặt bệ xuất phát y = -1 (x -5..5), bia tập ở x = 3.7 và -3.9. Màn đánh boss: sàn y = 0, boss ở x = 12.
    /// </summary>
    public class ReachBlueTests
    {
        const float PlaygroundFloor = -1f;
        const float ArenaFloor = 0f;
        const float DummyX = 3.7f;
        const float OtherDummyX = -3.9f;

        OctopusController _player;
        HeroMotor _hero;
        CombatController _combat;
        Rigidbody2D _body;
        LionBoss _boss;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        IEnumerator Load(string scene = "Playground")
        {
            CharacterSelection.Choose(PlayableCharacter.Blue);
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

        void SetReach(float size, float range)
        {
            FieldInfo field = typeof(CombatController).GetField("blueReach", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "CombatController.blueReach");
            field.SetValue(_combat, new SkillReach(size, range));
            SkillReach now = _combat.ReachOf(PlayableCharacter.Blue);
            Assert.That(now.size, Is.EqualTo(size));
            Assert.That(now.range, Is.EqualTo(range));
        }

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

        IEnumerator WaitIdle(float max = 8f)
        {
            for (float t = 0f; t < max && _combat.Busy; t += Time.deltaTime)
                yield return null;
            Assert.IsFalse(_combat.Busy, "Chiêu chạy quá lâu");
        }

        void ClearCooldowns()
        {
            var field = typeof(CombatController).GetField("_cooldown", BindingFlags.NonPublic | BindingFlags.Instance);
            var cooldowns = (float[])field.GetValue(_combat);
            System.Array.Clear(cooldowns, 0, cooldowns.Length);
        }

        static Health DummyAt(float x) =>
            Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Where(d => Mathf.Abs(d.transform.position.x - x) < 0.5f)
                .Select(d => d.GetComponent<Health>()).First();

        // Đếm số lần mất máu của mục tiêu
        sealed class Hits
        {
            public int Count;
            public Hits(Health target) => target.Damaged += (amount, from, effect) => Count++;
        }

        static Bounds BoundsOf(Health h)
        {
            Collider2D c = h.GetComponent<Collider2D>();
            return c != null ? c.bounds : new Bounds(h.transform.position, Vector3.one);
        }

        // Bề ngang lớn nhất (world) của ảnh hiệu ứng sprite đang hiện
        static float WidestFx(string sprite)
        {
            Sprite s = BlueFx.SpriteOf(sprite);
            float widest = 0f;
            foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (r.enabled && r.sprite == s)
                    widest = Mathf.Max(widest, r.bounds.size.x);
            return widest;
        }

        // ---------- Đánh tới chỗ ngoài tầm ×1 ----------

        [UnityTest]
        public IEnumerator DoubleReach_BasicAttack_HitsDummyThatIsOutOfReachAtX1()
        {
            yield return Load();
            var combo = (BlueReapCombo)_combat.Get(0, 0);
            // Nhát quét đầu: khung tâm reach × 0.5, rộng reach × 1.1 → mép xa cách tâm thân reach × 1.05 (×1: 2.4 unit, ×2: 4.8)
            float far1 = combo.reach * 1.05f;
            float gap = far1 + 1.2f; // mép bia cách tâm thân ~3.6 unit: ngoài tầm ×1, trong tầm ×2
            float x = DummyX - 0.5f - gap;
            var hits = new Hits(DummyAt(DummyX));

            SetReach(2f, 2f);
            yield return StandAt(x, PlaygroundFloor);
            Assert.IsTrue(_combat.Use(0, 0));
            yield return WaitIdle();
            Assert.That(hits.Count, Is.EqualTo(1), $"×2: nhát quét tới được bia cách {gap:0.0} unit");
            yield return new WaitForSeconds(1f); // hết thời gian nối combo, bia về chỗ

            SetReach(1f, 1f);
            yield return StandAt(x, PlaygroundFloor);
            Assert.IsTrue(_combat.Use(0, 0));
            yield return WaitIdle();
            yield return new WaitForSeconds(0.2f);
            Assert.That(hits.Count, Is.EqualTo(1), "×1: bia ở đó ngoài tầm");
        }

        [UnityTest]
        public IEnumerator DoubleReach_CrescentWaveAndChainPrison_ReachTwiceAsFar_WaveTwiceAsBig()
        {
            yield return Load("BossArena");
            // Khung đánh của boss bám theo hình (dáng đứng), lúc mới vào màn chưa đúng chỗ: chờ một lúc rồi mới đo mép
            yield return new WaitForSeconds(1f);
            float edge = BoundsOf(_boss.Health).min.x;
            var hits = new Hits(_boss.Health);

            // Sóng lưỡi liềm: tầm bay 10 unit ở ×1 (tới mép xa ~13 kể cả vụ nổ), 20 ở ×2; boss cách 16 unit
            float[] widest = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                float k = pass == 0 ? 1f : 2f;
                SetReach(k, k);
                ClearCooldowns();
                yield return StandAt(edge - 16f, ArenaFloor);
                int before = hits.Count;
                Assert.IsTrue(_combat.Use(1, 1));
                for (float t = 0f; t < 2.5f; t += Time.deltaTime)
                {
                    widest[pass] = Mathf.Max(widest[pass], WidestFx(BlueFx.Wave));
                    yield return null;
                }

                if (pass == 0)
                    Assert.That(hits.Count, Is.EqualTo(before), "×1: sóng hết tầm trước khi tới boss cách 16 unit");
                else
                    Assert.That(hits.Count, Is.GreaterThan(before), "×2: sóng bay tới boss cách 16 unit");
            }

            Assert.That(widest[1] / widest[0], Is.InRange(1.7f, 2.3f), $"Sóng ×2 to gấp đôi ({widest[0]:0.00} → {widest[1]:0.00} unit)");

            // Xích trói: tầm phóng 9 unit ở ×1, 18 ở ×2; boss cách 13 unit
            for (int pass = 0; pass < 2; pass++)
            {
                float k = pass == 0 ? 1f : 2f;
                SetReach(k, k);
                yield return StandAt(edge - 13f, ArenaFloor);
                _combat.Rage.Add(RageMeter.PerTier);
                Assert.IsTrue(_combat.Use(2, 2));
                bool stunned = false;
                for (float t = 0f; t < 1f && !stunned; t += Time.deltaTime)
                {
                    stunned = _boss.IsStunned;
                    yield return null;
                }

                if (pass == 0)
                    Assert.IsFalse(stunned, "×1: boss cách 13 unit ngoài tầm xích");
                else
                    Assert.IsTrue(stunned, "×2: xích với tới boss cách 13 unit, trói choáng");
                yield return WaitIdle();
            }
        }

        // ---------- Đặt lại (1, 1) thì về tầm cũ ----------

        [UnityTest]
        public IEnumerator ReachBackToOne_RestoresOldReach_DashAndWhirl()
        {
            yield return Load();
            var dash = (BlueReaperDash)_combat.Get(0, 2);
            var whirl = (BlueSoulWhirl)_combat.Get(1, 0);

            // Lướt áp sát: ×1 lướt ~3.6 unit như cũ, ×2 gấp đôi (cùng thời gian)
            float[] travel = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                float k = pass == 0 ? 1f : 2f;
                SetReach(k, k);
                ClearCooldowns();
                yield return StandAt(-3.6f, PlaygroundFloor);
                float x0 = _body.position.x;
                Assert.IsTrue(_combat.Use(0, 2));
                float dashed = 0f;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    if (_hero.IsDashing)
                        dashed += Time.deltaTime;
                    yield return null;
                }

                travel[pass] = _body.position.x - x0;
                Assert.That(dashed, Is.LessThan(dash.dashTime + 0.1f), "Lướt cùng thời gian");
                yield return WaitIdle();
            }

            Assert.That(travel[0], Is.EqualTo(dash.distance).Within(1f), $"×1: lướt ~{dash.distance} unit như cũ");
            Assert.That(travel[1], Is.GreaterThan(dash.distance * 1.7f), $"×2: lướt xa gấp đôi ({travel[0]:0.0} → {travel[1]:0.0})");

            // Vòng lửa quanh thân (chỉ theo cỡ chiêu): bia sau lưng cách 3 unit, ngoài bán kính ×1 (2), trong bán kính ×2 (4)
            float gap = whirl.radius * 1.5f;
            var behind = new Hits(DummyAt(OtherDummyX));
            for (int pass = 0; pass < 2; pass++)
            {
                float k = pass == 0 ? 1f : 2f;
                SetReach(k, k);
                ClearCooldowns();
                yield return StandAt(OtherDummyX + 0.5f + gap, PlaygroundFloor);
                int before = behind.Count;
                Assert.IsTrue(_combat.Use(1, 0));
                yield return WaitIdle();
                if (pass == 0)
                    Assert.That(behind.Count, Is.EqualTo(before), $"×1: vòng lửa bán kính {whirl.radius} không tới bia cách {gap} unit");
                else
                    Assert.That(behind.Count, Is.GreaterThan(before), "×2: vòng lửa to gấp đôi tới được bia");
            }

            // ×1: đứng chỗ cũ của bài thử combo (cách bia 1.7) vẫn trúng đủ 3 nhát như trước
            SetReach(1f, 1f);
            yield return new WaitForSeconds(0.8f);
            yield return StandAt(DummyX - 1.7f, PlaygroundFloor);
            var front = new Hits(DummyAt(DummyX));
            Assert.IsTrue(_combat.Use(0, 0));
            yield return null;
            Assert.IsTrue(_combat.Use(0, 0));
            Assert.IsTrue(_combat.Use(0, 0));
            yield return WaitIdle();
            Assert.That(front.Count, Is.EqualTo(3), "×1: combo 3 nhát trúng như cũ");
        }
    }
}
