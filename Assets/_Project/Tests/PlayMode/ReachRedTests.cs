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
    /// Cỡ / tầm chiêu của RED (Combat Controller › "Cỡ / tầm chiêu", mặc định ×2 cả cỡ lẫn tầm): chiêu chém tới được xa gấp
    /// đôi (bia đứng ngoài tầm cũ vẫn trúng), vòng xoay quanh thân to gấp đôi (trúng cả bia sau lưng ở xa), lướt xa gấp đôi
    /// trong cùng thời gian. Đặt lại ×1 (sửa ô redReach bằng reflection) thì mọi thứ về đúng tầm cũ.
    /// Màn hang động: bia tập ở x = 3.7 và x = -3.9, mặt bệ y = -1 (bệ xuất phát x -5..5).
    /// </summary>
    public class ReachRedTests
    {
        const float PlaygroundFloor = -1f;
        const float DummyX = 3.7f;

        static readonly FieldInfo RedReach = typeof(CombatController).GetField("redReach", BindingFlags.NonPublic | BindingFlags.Instance);

        OctopusController _player;
        HeroMotor _hero;
        CombatController _combat;
        Rigidbody2D _body;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        IEnumerator Load()
        {
            CharacterSelection.Choose(PlayableCharacter.Red);
            yield return SceneManager.LoadSceneAsync("Playground");
            _player = Object.FindAnyObjectByType<OctopusController>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _hero = _player.Hero;
            _combat = _player.GetComponent<CombatController>();
            _body = _player.GetComponent<Rigidbody2D>();
            yield return new WaitForFixedUpdate();
        }

        void SetReach(float size, float range)
        {
            Assert.IsNotNull(RedReach, "CombatController phải có ô redReach (Cỡ / tầm chiêu của RED)");
            RedReach.SetValue(_combat, new SkillReach(size, range));
        }

        // Đứng yên trên sàn ở x, quay mặt về phía facing
        IEnumerator StandAt(float x, float facing = 1f)
        {
            _player.ReleaseJump();
            _player.SetAim(0);
            _player.SetIntent(false, facing);
            yield return new WaitForFixedUpdate();
            yield return null;
            _player.SetIntent(false, 0f);
            _player.ResetTo(new Vector2(x, PlaygroundFloor + HeroMotor.FeetOffset + 0.05f));
            for (float t = 0f; t < 1.5f && !_hero.Grounded; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(_hero.Grounded, "Phải đứng trên sàn");
            Assert.That(_player.Facing, Is.EqualTo(Mathf.Sign(facing)));
        }

        static Health DummyAt(float x) =>
            Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Where(d => Mathf.Abs(d.transform.position.x - x) < 0.5f)
                .Select(d => d.GetComponent<Health>()).First();

        IEnumerator WaitIdle(float max = 6f)
        {
            for (float t = 0f; t < max && _combat.Busy; t += Time.deltaTime)
                yield return null;
        }

        // Dùng chiêu (slot, variant) đứng ở x quay về facing; trả số lần bia ở DummyX trúng đòn qua hits[0]
        IEnumerator HitsOn(float x, float facing, int slot, int variant, int[] hits)
        {
            yield return StandAt(x, facing);
            Health dummy = DummyAt(DummyX);
            dummy.Restore();
            int n = 0;
            void Count(float amount, Vector2 from, HitEffect effect) => n++;
            dummy.Damaged += Count;
            Assert.IsTrue(_combat.Use(slot, variant), $"Dùng được chiêu {slot}/{variant}");
            yield return null;
            yield return WaitIdle();
            yield return new WaitForSeconds(0.1f);
            dummy.Damaged -= Count;
            hits[0] = n;
            yield return new WaitForSeconds(0.8f); // bia về chỗ, hết khoảng nối combo
        }

        // Lướt chém (nút 1 ↓) dừng ở x = 4.3 trên bệ; trả quãng đã lướt qua moved[0]
        IEnumerator DashFrom(float expected, float[] moved)
        {
            yield return StandAt(Mathf.Max(-4.5f, 4.3f - expected));
            float x0 = _body.position.x;
            Assert.IsTrue(_combat.Use(0, 2));
            yield return null;
            yield return WaitIdle();
            moved[0] = _body.position.x - x0;
        }

        [UnityTest]
        public IEnumerator DefaultReach_IsDouble_SkillsHitBeyondTheOldReach()
        {
            yield return Load();
            SkillReach reach = _combat.ReachOf(PlayableCharacter.Red);
            Assert.That(reach.size, Is.EqualTo(2f), "Cỡ chiêu RED mặc định ×2");
            Assert.That(reach.range, Is.EqualTo(2f), "Tầm chiêu RED mặc định ×2");
            var hits = new int[1];

            // Đánh thường đòn 1: ×1 vùng chém tới ~2.4 unit trước tâm thân (+ nửa bề ngang bia 0.5) → bia cách 4 unit nằm ngoài
            // tầm cũ; ×2 vùng chém tới ~4.7 unit → trúng
            yield return HitsOn(DummyX - 4f, 1f, 0, 0, hits);
            Assert.That(hits[0], Is.EqualTo(1), "×2: chém trúng bia cách 4 unit (ngoài tầm cũ)");

            // Kĩ năng (xoay + nổ vòng tinh thể): ×1 bán kính nổ 2.4 → bia sau lưng cách 3.5 unit ngoài vòng; ×2 bán kính 4.8 → trúng
            yield return HitsOn(DummyX - 3.5f, -1f, 1, 0, hits);
            Assert.That(hits[0], Is.GreaterThanOrEqualTo(2), "×2: vòng xoay + nổ quanh thân trúng cả bia sau lưng cách 3.5 unit");

            // Đánh thường áp sát: lướt xa gấp đôi (3.2 → 6.4 unit) trong cùng thời gian
            float dash = ((RedDashSlash)_combat.SkillsOf(PlayableCharacter.Red)[2]).distance;
            var moved = new float[1];
            yield return DashFrom(dash * 2f, moved);
            Assert.That(moved[0], Is.GreaterThan(dash * 2f * 0.78f), "×2: lướt xa gấp đôi");
        }

        [UnityTest]
        public IEnumerator ReachBackToOne_RestoresOldReach()
        {
            yield return Load();
            SetReach(1f, 1f);
            SkillReach reach = _combat.ReachOf(PlayableCharacter.Red);
            Assert.That(reach.size, Is.EqualTo(1f));
            Assert.That(reach.range, Is.EqualTo(1f));
            var hits = new int[1];

            yield return HitsOn(DummyX - 4f, 1f, 0, 0, hits);
            Assert.That(hits[0], Is.EqualTo(0), "×1: bia cách 4 unit ngoài tầm chém như cũ");
            yield return HitsOn(DummyX - 1.8f, 1f, 0, 0, hits);
            Assert.That(hits[0], Is.EqualTo(1), "×1: trong tầm cũ vẫn chém trúng");

            yield return HitsOn(DummyX - 3.5f, -1f, 1, 0, hits);
            Assert.That(hits[0], Is.EqualTo(0), "×1: vòng xoay nhỏ như cũ, không với tới bia sau lưng cách 3.5 unit");

            float dash = ((RedDashSlash)_combat.SkillsOf(PlayableCharacter.Red)[2]).distance;
            var moved = new float[1];
            yield return DashFrom(dash, moved);
            Assert.That(moved[0], Is.InRange(dash * 0.78f, dash * 1.3f), "×1: lướt đúng quãng cũ");
        }
    }
}
