using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// Cỡ / tầm chiêu (CombatController › Cỡ / tầm chiêu) của bạch tuộc và nhện: ở ×2 chiêu đánh tới mục tiêu mà ở ×1 không
    /// với tới; đặt lại (1, 1) thì tầm cũ trở lại; laze to theo cỡ chiêu.
    /// Bia tập gần nhất ở x = 3.7 trên bệ xuất phát (mặt sàn y = -1, x -5..5), khung trúng rộng 1 unit.
    /// </summary>
    public class ReachOctopusSpiderTests
    {
        const float RestingY = -0.5f;
        const float DummyX = 3.7f;

        OctopusController _player;
        CombatController _combat;

        IEnumerator Load(PlayableCharacter character)
        {
            CharacterSelection.Choose(character);
            yield return SceneManager.LoadSceneAsync("Playground");
            _player = Object.FindAnyObjectByType<OctopusController>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _combat = _player.GetComponent<CombatController>();
            yield return new WaitForFixedUpdate();
        }

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        // Đứng ở x, quay mặt sang phải (về phía bia tập x = 3.7)
        IEnumerator StandAt(float x)
        {
            _player.ResetTo(new Vector2(x, RestingY + 0.05f));
            yield return new WaitForSeconds(0.5f);
            _player.SetIntent(holding: false, drag: 1f);
            yield return new WaitForFixedUpdate(); // phải qua ít nhất một bước vật lý thì hướng mặt mới cập nhật
            yield return null;
            _player.SetIntent(holding: false, drag: 0f);
            _player.ResetTo(new Vector2(x, RestingY + 0.05f));
            yield return new WaitForSeconds(0.4f);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Grounded));
            Assert.That(_player.Facing, Is.GreaterThan(0f));
        }

        // Đặt cỡ / tầm chiêu của nhân vật (ô private trong Inspector) qua reflection
        void SetReach(PlayableCharacter character, float size, float range)
        {
            string name = character == PlayableCharacter.Spider ? "spiderReach" : "octopusReach";
            FieldInfo field = typeof(CombatController).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Thiếu ô {name} trong CombatController");
            field.SetValue(_combat, new SkillReach(size, range));
            SkillReach now = _combat.ReachOf(character);
            Assert.That(now.size, Is.EqualTo(size));
            Assert.That(now.range, Is.EqualTo(range));
        }

        void ClearCooldowns()
        {
            var cooldown = (float[])typeof(CombatController).GetField("_cooldown", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_combat);
            System.Array.Clear(cooldown, 0, cooldown.Length);
        }

        static Health DummyAt(float x)
        {
            foreach (TrainingDummy dummy in Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None))
            {
                if (Mathf.Abs(dummy.transform.position.x - x) < 0.5f)
                    return dummy.GetComponent<Health>();
            }

            return null;
        }

        // Khoảng cách từ tâm người chơi tới mép gần nhất khung trúng của bia
        float GapTo(Health dummy) =>
            Vector2.Distance(_player.GetComponent<Rigidbody2D>().position, dummy.GetComponent<Collider2D>().ClosestPoint(_player.GetComponent<Rigidbody2D>().position));

        // Dùng chiêu rồi chờ chiêu xong; trả về sát thương bia nhận được
        IEnumerator Cast(Health dummy, int slot, int variant, float[] dealt)
        {
            dealt[0] = 0f;
            System.Action<float, Vector2, HitEffect> onDamaged = (amount, from, effect) => dealt[0] += amount;
            dummy.Damaged += onDamaged;
            Assert.IsTrue(_combat.Use(slot, variant), $"Không dùng được chiêu {_combat.Get(slot, variant).name}");
            for (float t = 0f; t < 3f && _combat.Busy; t += Time.deltaTime)
                yield return null;
            yield return new WaitForSeconds(0.2f);
            dummy.Damaged -= onDamaged;
        }

        // Móc kéo: bia đứng ngoài tầm móc ×1 (6 unit) nhưng trong tầm ×2 → ×1 trượt, ×2 móc trúng
        [UnityTest]
        public IEnumerator Octopus_TentacleHook_ReachesDummyOnlyAtDoubleRange()
        {
            yield return Load(PlayableCharacter.Octopus);
            var hook = (TentacleHook)_combat.Get(0, 2);
            Health dummy = DummyAt(DummyX);
            var dealt = new float[1];

            SetReach(PlayableCharacter.Octopus, 1f, 1f);
            yield return StandAt(-3f);
            float gap = GapTo(dummy);
            Assert.That(gap, Is.GreaterThan(hook.range).And.LessThan(hook.range * 2f), "Bia phải ở ngoài tầm ×1, trong tầm ×2");
            yield return Cast(dummy, 0, 2, dealt);
            Assert.That(dealt[0], Is.EqualTo(0f), "Tầm ×1: bia ngoài tầm móc, không trúng");

            SetReach(PlayableCharacter.Octopus, 2f, 2f);
            ClearCooldowns();
            yield return StandAt(-3f);
            yield return Cast(dummy, 0, 2, dealt);
            Assert.That(dealt[0], Is.GreaterThan(0f), "Tầm ×2: móc trúng bia ở xa gấp đôi");
        }

        // Quất xúc tu (đòn 1 combo): bia ở ngoài khung quất ×1 nhưng trong khung ×2
        [UnityTest]
        public IEnumerator Octopus_TentacleWhip_DoubleReach_HitsDummyBeyondOldReach()
        {
            yield return Load(PlayableCharacter.Octopus);
            var combo = (TentacleCombo)_combat.Get(0, 0);
            Health dummy = DummyAt(DummyX);
            var dealt = new float[1];
            SetReach(PlayableCharacter.Octopus, 2f, 2f);
            yield return StandAt(0f);

            // Mép xa khung quất: tâm ở reach × 0.55 × Range, nửa bề ngang reach × 1.15 / 2 × Size
            float oldFar = combo.reach * (0.55f + 0.575f), newFar = combo.reach * (0.55f * 2f + 0.575f * 2f);
            float gap = GapTo(dummy);
            Assert.That(gap, Is.GreaterThan(oldFar).And.LessThan(newFar), "Bia phải ở ngoài khung quất ×1, trong khung ×2");
            yield return Cast(dummy, 0, 0, dealt);
            Assert.That(dealt[0], Is.GreaterThan(0f), "Cỡ / tầm ×2: quất trúng bia ở xa gấp đôi");
        }

        // Cào độc của nhện: đặt lại (1, 1) thì tầm cũ trở lại (đứng xa trượt, đứng gần như cũ thì trúng); ×2 thì đứng xa cũng trúng
        [UnityTest]
        public IEnumerator Spider_ClawCombo_ReachBackToOne_RestoresOldReach()
        {
            yield return Load(PlayableCharacter.Spider);
            var claw = (ClawCombo)_combat.Get(0, 0);
            Health dummy = DummyAt(DummyX);
            var dealt = new float[1];
            float oldFar = claw.reach * (0.55f + 0.55f);

            SetReach(PlayableCharacter.Spider, 1f, 1f);
            yield return StandAt(0f);
            Assert.That(GapTo(dummy), Is.GreaterThan(oldFar).And.LessThan(oldFar * 2f), "Bia phải ở ngoài khung cào ×1, trong khung ×2");
            yield return Cast(dummy, 0, 0, dealt);
            Assert.That(dealt[0], Is.EqualTo(0f), "Tầm ×1: bia ngoài khung cào, không trúng");

            yield return new WaitForSeconds(0.8f); // hết cửa sổ combo: lại ra đòn 1
            yield return StandAt(DummyX - 1.5f);
            yield return Cast(dummy, 0, 0, dealt);
            Assert.That(dealt[0], Is.GreaterThan(0f), "Tầm ×1: đứng gần như cũ thì vẫn trúng");

            SetReach(PlayableCharacter.Spider, 2f, 2f);
            yield return new WaitForSeconds(0.8f);
            yield return StandAt(0f);
            yield return Cast(dummy, 0, 0, dealt);
            Assert.That(dealt[0], Is.GreaterThan(0f), "Tầm ×2: bia ở xa gấp đôi vẫn trúng");
        }

        // Laze (mắt bạch tuộc, tám mắt nhện): tia to gấp Cỡ chiêu lần (hình tia và bề dày vùng trúng đọc cùng WidthScale)
        [UnityTest]
        public IEnumerator Laser_BeamWidthScalesWithSkillSize([Values(PlayableCharacter.Octopus, PlayableCharacter.Spider)] PlayableCharacter character)
        {
            yield return Load(character);
            int variant = character == PlayableCharacter.Spider ? 2 : 0;
            var beam = (LaserBeamSkill)_combat.Get(1, variant);
            var laser = _player.GetComponent<LaserSkill>();
            float original = beam.shot.width;
            SetReach(character, 2f, 2f);
            yield return StandAt(0f);
            Assert.IsTrue(_combat.Use(1, variant));
            yield return null;
            Assert.That(laser.WidthScale, Is.EqualTo(original * 2f).Within(1e-4f), "Cỡ ×2: tia to gấp đôi");
            for (float t = 0f; t < 4f && _combat.Busy; t += Time.deltaTime)
                yield return null;
            Assert.That(beam.shot.width, Is.EqualTo(original), "Thông số gốc của chiêu (Inspector, ở ×1) không bị nhân dồn");

            SetReach(character, 1f, 1f);
            ClearCooldowns();
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(_combat.Use(1, variant));
            yield return null;
            Assert.That(laser.WidthScale, Is.EqualTo(original).Within(1e-4f), "Cỡ ×1: tia như cũ");
        }
    }
}
