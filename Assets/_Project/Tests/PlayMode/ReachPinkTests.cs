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
    /// Cỡ / tầm chiêu của PINK (Combat Controller › "Cỡ / tầm chiêu", mặc định ×2): chiêu đánh trúng mục tiêu ở chỗ mà bản ×1
    /// không với tới (khung đánh thường, vòng phép thủ, rồng pha lê, tầm tìm xích áp chế), lướt áp sát đi gấp đôi quãng trong
    /// cùng thời gian; đặt lại (1, 1) (sửa ô private pinkReach bằng reflection) thì về đúng tầm cũ. Màn hang động: mặt bệ xuất
    /// phát y = -1 (bệ từ x = -5 tới 5), bia tập ở x = 3.7 (bia khác ở -3.9). Màn đánh boss: sàn y = 0.
    /// </summary>
    public class ReachPinkTests
    {
        const float Floor = -1f;
        const float DummyX = 3.7f;

        static readonly FieldInfo PinkReach = typeof(CombatController).GetField("pinkReach", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo Cooldowns = typeof(CombatController).GetField("_cooldown", BindingFlags.NonPublic | BindingFlags.Instance);

        OctopusController _player;
        CombatController _combat;
        Rigidbody2D _body;
        LionBoss _boss;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

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

        void SetReach(float size, float range) => PinkReach.SetValue(_combat, new SkillReach(size, range));

        void ClearCooldowns()
        {
            var cd = (float[])Cooldowns.GetValue(_combat);
            System.Array.Clear(cd, 0, cd.Length);
        }

        IEnumerator WaitIdle(float max = 6f)
        {
            for (float t = 0f; t < max && _combat.Busy; t += Time.deltaTime)
                yield return null;
            Assert.IsFalse(_combat.Busy, "Chiêu chạy quá lâu");
        }

        // Dùng chiêu (slot, variant) rồi đợi tới khi xong + thêm extra giây; trả về tổng sát thương vào target qua out
        IEnumerator UseAndCollect(Health target, int slot, int variant, float extra, float[] dealt)
        {
            dealt[0] = 0f;
            System.Action<float, Vector2, HitEffect> onHit = (amount, from, effect) => dealt[0] += amount;
            target.Damaged += onHit;
            ClearCooldowns();
            if (slot == 2)
                _combat.Rage.Add(RageMeter.PerTier);
            Assert.IsTrue(_combat.Use(slot, variant), $"Dùng chiêu {slot}/{variant}");
            yield return WaitIdle();
            yield return new WaitForSeconds(extra);
            target.Damaged -= onHit;
        }

        // ---------- Mặc định ×2: trúng chỗ bản ×1 không với tới ----------

        // Đánh thường: bia tập cách 4.2 unit — ngoài khung đâm trượng ×1 (tới 2.64 + nửa bia 0.5), trong khung ×2 (tới 5.28)
        [UnityTest]
        public IEnumerator Default_Double_BasicHitsDummyBeyondOldReach()
        {
            yield return Load();
            SkillReach reach = _combat.ReachOf(PlayableCharacter.Pink);
            Assert.That(reach.size, Is.EqualTo(2f), "Cỡ chiêu PINK mặc định ×2");
            Assert.That(reach.range, Is.EqualTo(2f), "Tầm chiêu PINK mặc định ×2");

            yield return StandAt(DummyX - 4.2f, Floor);
            Health dummy = DummyAt(DummyX);
            var dealt = new float[1];
            yield return UseAndCollect(dummy, 0, 0, 0.1f, dealt);
            Assert.That(dealt[0], Is.EqualTo(18f).Within(0.01f), "Đòn 1 combo trúng bia ở xa 4.2");
        }

        // Dùng chiêu thủ: bia tập cách 3 unit — ngoài vòng phép ×1 (bán kính 1.7), trong vòng phép ×2 (3.4)
        [UnityTest]
        public IEnumerator Ward_DoubleSize_HitsDummyOutsideOldCircle()
        {
            yield return Load();
            yield return StandAt(DummyX - 3f, Floor);
            Health dummy = DummyAt(DummyX);
            var dealt = new float[1];

            SetReach(1f, 1f);
            yield return UseAndCollect(dummy, 1, 2, 0.1f, dealt);
            Assert.That(dealt[0], Is.EqualTo(0f), "×1: vòng phép không chạm bia ở xa 3");

            SetReach(2f, 2f);
            yield return UseAndCollect(dummy, 1, 2, 0.1f, dealt);
            Assert.That(dealt[0], Is.GreaterThanOrEqualTo(6f * 3f - 0.01f), "×2: vòng phép to gấp đôi chạm bia, đánh nhiều nhịp");
        }

        // Màn đánh boss, đứng cách thân sư tử 15 unit: rồng pha lê (bay 9 unit từ mũi trượng ở ×1) và xích áp chế (tầm tìm 8 unit
        // ở ×1) không tới; ở ×2 (bay 18, tìm 16) thì trúng / trói được
        [UnityTest]
        public IEnumerator BossArena_DragonAndChainSeal_ReachLionOnlyAtDouble()
        {
            yield return Load("BossArena");
            // đứng bên nào của sư tử còn đủ chỗ (sàn từ x = -28 tới 28), quay mặt về phía nó
            Bounds lion = _boss.Body;
            bool fromRight = 28f - lion.max.x > lion.min.x + 28f;
            yield return StandAt(fromRight ? lion.max.x + 15f : lion.min.x - 15f, 0f, fromRight ? -1f : 1f);
            float gap = fromRight ? _body.position.x - lion.max.x : lion.min.x - _body.position.x;
            Assert.That(gap, Is.GreaterThan(14f), "Đứng đủ xa sư tử");
            Assert.That(_player.Facing * (lion.center.x - _body.position.x), Is.GreaterThan(0f), "Quay mặt về phía sư tử");
            var dealt = new float[1];

            SetReach(1f, 1f);
            yield return UseAndCollect(_boss.Health, 1, 1, 1.2f, dealt);
            Assert.That(dealt[0], Is.EqualTo(0f), "×1: rồng bay hết quãng 9 chưa tới sư tử");
            yield return UseAndCollect(_boss.Health, 2, 2, 0.3f, dealt);
            Assert.That(dealt[0], Is.EqualTo(0f), "×1: sư tử ngoài tầm tìm của xích");
            Assert.IsFalse(_boss.IsStunned);

            SetReach(2f, 2f);
            yield return UseAndCollect(_boss.Health, 1, 1, 1.2f, dealt);
            Assert.That(dealt[0], Is.GreaterThan(0f), "×2: rồng bay quãng 18 trúng sư tử");
            yield return new WaitForSeconds(0.5f);
            ClearCooldowns();
            _combat.Rage.Add(RageMeter.PerTier);
            Assert.IsTrue(_combat.Use(2, 2));
            float maxStun = 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                maxStun = Mathf.Max(maxStun, _boss.StunRemaining);
                yield return null;
            }

            Assert.That(maxStun, Is.GreaterThan(0.3f), "×2: xích tìm tới sư tử, trói choáng");
        }

        // ---------- Đặt lại (1, 1): về đúng tầm cũ ----------

        // Đánh thường hụt bia ở xa 4.2 như bản cũ, chỉ trúng khi đứng gần (1.5); lướt áp sát ×1 đi ~3.6 unit, ×2 đi gấp đôi
        // trong cùng thời gian lướt
        [UnityTest]
        public IEnumerator ReachOne_RestoresOldReach_DashDistanceDoublesAtTwo()
        {
            yield return Load();
            SetReach(1f, 1f);
            Assert.That(_combat.ReachOf(PlayableCharacter.Pink).range, Is.EqualTo(1f));
            Health dummy = DummyAt(DummyX);
            var dealt = new float[1];

            yield return StandAt(DummyX - 4.2f, Floor);
            yield return UseAndCollect(dummy, 0, 0, 0.9f, dealt); // chờ quá cửa sổ combo: lần sau lại từ đòn 1
            Assert.That(dealt[0], Is.EqualTo(0f), "×1: đánh thường không với tới bia ở xa 4.2");
            yield return StandAt(DummyX - 1.5f, Floor);
            yield return UseAndCollect(dummy, 0, 0, 0.1f, dealt);
            Assert.That(dealt[0], Is.EqualTo(18f).Within(0.01f), "×1: đứng gần thì trúng như cũ");

            float distance = ((PinkDashSweep)_combat.SkillsOf(PlayableCharacter.Pink)[3]).distance;
            var moved = new float[2];
            var took = new float[2];
            for (int k = 0; k < 2; k++)
            {
                float range = k + 1f;
                SetReach(range, range);
                yield return StandAt(4.5f - distance * range, Floor); // lướt xong vẫn còn trên bệ
                float x0 = _body.position.x;
                ClearCooldowns();
                Assert.IsTrue(_combat.Use(1, 0));
                float dashFrom = -1f, dashTo = -1f;
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    if (_player.Hero.IsDashing && dashFrom < 0f)
                        dashFrom = t;
                    if (dashFrom >= 0f && !_player.Hero.IsDashing && dashTo < 0f)
                        dashTo = t;
                    yield return null;
                }

                moved[k] = _body.position.x - x0;
                took[k] = dashTo - dashFrom;
                Assert.That(dashFrom, Is.GreaterThanOrEqualTo(0f), $"×{range}: có lướt");
                Assert.That(dashTo, Is.GreaterThan(dashFrom), $"×{range}: lướt xong");
                yield return WaitIdle();
            }

            Assert.That(moved[0], Is.InRange(distance * 0.8f, distance * 1.2f), "×1: lướt đúng quãng cũ");
            Assert.That(moved[1] / moved[0], Is.InRange(1.75f, 2.25f), "×2: lướt xa gấp đôi");
            Assert.That(took[1], Is.EqualTo(took[0]).Within(0.06f), "Thời gian lướt giữ nguyên");
        }
    }
}
