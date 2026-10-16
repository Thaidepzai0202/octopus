using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// Hệ thống chiêu: cụm 3 nút + vuốt, thanh nộ 3 mốc, 9 chiêu mỗi nhân vật đánh trúng bia tập.
    /// Bia tập gần nhất ở x = 3.7 trên bệ xuất phát (mặt sàn y = -1).
    /// </summary>
    public class CombatPlayModeTests
    {
        const float RestingY = -0.5f;
        const float DummyX = 3.7f;

        OctopusController _player;
        CombatController _combat;
        Rigidbody2D _body;

        IEnumerator Load(PlayableCharacter character)
        {
            CharacterSelection.Choose(character);
            yield return SceneManager.LoadSceneAsync("Playground");
            _player = Object.FindAnyObjectByType<OctopusController>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _combat = _player.GetComponent<CombatController>();
            _body = _player.GetComponent<Rigidbody2D>();
            yield return new WaitForFixedUpdate();
        }

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        // Đứng cách bia tập 1.5 unit, quay mặt về phía bia
        IEnumerator StandBeforeDummy()
        {
            _player.ResetTo(new Vector2(DummyX - 1.5f, RestingY + 0.05f));
            yield return new WaitForSeconds(0.5f);
            _player.SetIntent(holding: false, drag: 1f);
            yield return new WaitForFixedUpdate(); // phải qua ít nhất một bước vật lý thì hướng mặt mới cập nhật
            yield return null;
            _player.SetIntent(holding: false, drag: 0f);
            _player.ResetTo(new Vector2(DummyX - 1.5f, RestingY + 0.05f));
            yield return new WaitForSeconds(0.4f);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Grounded));
            Assert.That(_player.Facing, Is.GreaterThan(0f));
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

        [UnityTest]
        public IEnumerator SkillPad_Button2BottomRight_Button1Left_Button3Above_RageBarTopLeft()
        {
            yield return Load(PlayableCharacter.Octopus);
            var pad = Object.FindAnyObjectByType<SkillPad>();
            Vector2 one = SkillPad.ScreenCenter(pad.ButtonRect(0));
            Vector2 two = SkillPad.ScreenCenter(pad.ButtonRect(1));
            Vector2 three = SkillPad.ScreenCenter(pad.ButtonRect(2));
            Assert.That(two.x, Is.GreaterThan(Screen.width * 0.75f), "Nút 2 ở góc phải");
            Assert.That(two.y, Is.LessThan(Screen.height * 0.3f), "Nút 2 ở góc dưới");
            Assert.That(one.x, Is.LessThan(two.x - 50f), "Nút 1 bên trái nút 2");
            Assert.That(Mathf.Abs(one.y - two.y), Is.LessThan(Screen.height * 0.08f), "Nút 1 ngang hàng nút 2");
            Assert.That(three.y, Is.GreaterThan(two.y + 50f), "Nút 3 phía trên nút 2");
            Assert.That(Mathf.Abs(three.x - two.x), Is.LessThan(Screen.width * 0.05f), "Nút 3 thẳng trên nút 2");

            var rage = Object.FindAnyObjectByType<RageBar>();
            Vector2 bar = SkillPad.ScreenCenter((RectTransform)rage.transform);
            Assert.That(bar.x, Is.LessThan(Screen.width * 0.4f), "Thanh nộ bên trái");
            Assert.That(bar.y, Is.GreaterThan(Screen.height * 0.75f), "Thanh nộ phía trên");
        }

        // Vuốt từ nút này về phía nút kia ra đúng kiểu chiêu
        [UnityTest]
        public IEnumerator SkillPad_SwipeTowardOtherButtons_PicksVariant()
        {
            yield return Load(PlayableCharacter.Octopus);
            var pad = Object.FindAnyObjectByType<SkillPad>();
            var c = new Vector2[3];
            for (int i = 0; i < 3; i++)
                c[i] = SkillPad.ScreenCenter(pad.ButtonRect(i));
            float far = Screen.height * 0.12f;
            Vector2 Toward(int from, int to) => (c[to] - c[from]).normalized * far;

            Assert.That(SkillPad.Classify(0, Vector2.one * 3f, c, far * 0.5f), Is.EqualTo(0), "Chạm (không vuốt) = chiêu chính");
            Assert.That(SkillPad.Classify(0, Toward(0, 1), c, far * 0.5f), Is.EqualTo(1), "1 vuốt sang 2");
            Assert.That(SkillPad.Classify(0, Toward(0, 2), c, far * 0.5f), Is.EqualTo(2), "1 vuốt sang 3");
            Assert.That(SkillPad.Classify(1, Toward(1, 0), c, far * 0.5f), Is.EqualTo(1), "2 vuốt sang 1");
            Assert.That(SkillPad.Classify(1, Toward(1, 2), c, far * 0.5f), Is.EqualTo(2), "2 vuốt lên 3");
            Assert.That(SkillPad.Classify(2, Toward(2, 0), c, far * 0.5f), Is.EqualTo(1), "3 vuốt về 1");
            Assert.That(SkillPad.Classify(2, Toward(2, 1), c, far * 0.5f), Is.EqualTo(2), "3 vuốt về 2");
        }

        // Nộ đầy dần khi di chuyển và khi đánh trúng; chiêu cuối khoá khi chưa qua mốc 1, dùng thì xả hết nộ
        [UnityTest]
        public IEnumerator Rage_FillsByMovingAndHitting_UltimateNeedsTier()
        {
            yield return Load(PlayableCharacter.Octopus);
            RageMeter rage = _combat.Rage;
            yield return StandBeforeDummy();
            float start = rage.Value;
            Assert.IsFalse(_combat.Use(2, 0), "Chưa đủ nộ thì không dùng được chiêu cuối");

            _player.SetIntent(holding: false, drag: -1f);
            yield return new WaitForSeconds(0.6f);
            _player.SetIntent(holding: false, drag: 0f);
            float moved = rage.Value;
            Assert.That(moved, Is.GreaterThan(start + 1f), "Di chuyển thì nộ tăng");

            yield return StandBeforeDummy();
            float beforeHit = rage.Value;
            Assert.IsTrue(_combat.Use(0, 0));
            yield return new WaitForSeconds(0.5f);
            Assert.That(rage.Value, Is.GreaterThan(beforeHit + 1f), "Đánh trúng thì nộ tăng");

            rage.Add(250f);
            Assert.That(rage.Tier, Is.GreaterThanOrEqualTo(2), "Qua mốc 2");
            Assert.That(rage.Multiplier(3), Is.GreaterThan(rage.Multiplier(2)), "Mốc càng cao chiêu cuối càng đau");
            Assert.That(rage.Multiplier(2), Is.GreaterThan(rage.Multiplier(1)));
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(_combat.Use(2, 0), "Đủ nộ thì dùng được chiêu cuối");
            Assert.That(rage.Value, Is.EqualTo(0f).Within(0.01f), "Dùng chiêu cuối là xả hết nộ");
        }

        // Từng chiêu (9 của bạch tuộc, 9 của nhện) phải đánh trúng bia tập trước mặt
        [UnityTest]
        public IEnumerator Skill_HitsTrainingDummy(
            [Values(PlayableCharacter.Octopus, PlayableCharacter.Spider)] PlayableCharacter character,
            [Values(0, 1, 2)] int slot,
            [Values(0, 1, 2)] int variant)
        {
            yield return Load(character);
            yield return StandBeforeDummy();
            Health dummy = DummyAt(DummyX);
            Assert.IsNotNull(dummy, "Phải có bia tập trên bệ xuất phát");
            float dealt = 0f;
            dummy.Damaged += (amount, from, effect) => dealt += amount;
            if (slot == 2)
                _combat.Rage.Add(RageMeter.PerTier * 2f);

            Skill skill = _combat.Get(slot, variant);
            Assert.IsNotNull(skill, "Thiếu chiêu");
            Assert.IsTrue(_combat.Use(slot, variant), $"Không dùng được chiêu {skill.name}");
            for (float t = 0f; t < 6f && (_combat.Busy || dealt <= 0f); t += Time.deltaTime)
                yield return null;
            yield return new WaitForSeconds(0.3f);

            Assert.That(dealt, Is.GreaterThan(0f), $"{character} · {skill.name}: phải đánh trúng bia tập");
        }

        // Bấm đánh thường 3 lần liền: ra đủ combo 3 đòn, đòn cuối là đòn nặng
        [UnityTest]
        public IEnumerator BasicAttack_ThreeTaps_ChainCombo([Values(PlayableCharacter.Octopus, PlayableCharacter.Spider)] PlayableCharacter character)
        {
            yield return Load(character);
            yield return StandBeforeDummy();
            Health dummy = DummyAt(DummyX);
            int hits = 0;
            bool heavy = false;
            dummy.Damaged += (amount, from, effect) =>
            {
                if (effect.knockback != Vector2.zero || effect.heavy)
                    hits++;
                heavy |= effect.heavy;
            };

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(_combat.Use(0, 0));
                yield return new WaitForSeconds(0.12f);
            }

            for (float t = 0f; t < 2f && _combat.Busy; t += Time.deltaTime)
                yield return null;
            Assert.That(hits, Is.GreaterThanOrEqualTo(3), "Combo phải ra đủ 3 đòn");
            Assert.IsTrue(heavy, "Đòn cuối combo là đòn nặng");
        }

        // Mỗi chiêu hồi riêng: laze đang hồi vẫn phun mực được; nhện vừa vồ mồi (đang hồi) vẫn đánh thường được
        [UnityTest]
        public IEnumerator Cooldown_IsPerSkill_NotPerButton()
        {
            yield return Load(PlayableCharacter.Octopus);
            yield return StandBeforeDummy();
            Assert.IsTrue(_combat.Use(1, 0), "Laze mắt");
            for (float t = 0f; t < 5f && _combat.Busy; t += Time.deltaTime)
                yield return null;
            Assert.That(_combat.CooldownRemaining(1, 0), Is.GreaterThan(0f), "Laze đang hồi");
            Assert.IsFalse(_combat.Use(1, 0), "Laze đang hồi thì chưa bắn lại được");
            Assert.IsTrue(_combat.Use(1, 1), "Laze đang hồi vẫn phun mực được");

            yield return Load(PlayableCharacter.Spider);
            yield return StandBeforeDummy();
            Assert.IsTrue(_combat.Use(0, 1), "Vồ mồi");
            for (float t = 0f; t < 3f && _combat.Busy; t += Time.deltaTime)
                yield return null;
            Assert.That(_combat.CooldownRemaining(0, 1), Is.GreaterThan(0f), "Vồ mồi đang hồi");
            Assert.IsTrue(_combat.Use(0, 0), "Vồ mồi đang hồi vẫn đánh thường được");
        }

        [UnityTest]
        public IEnumerator TrainingDummy_BreaksAndRespawns()
        {
            yield return Load(PlayableCharacter.Octopus);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            dummy.TakeHit(99999f, Vector2.zero, default);
            yield return null;
            Assert.IsTrue(view.IsBroken, "Hết máu thì vỡ");
            yield return new WaitForSeconds(3.3f);
            Assert.IsFalse(view.IsBroken, "Vài giây sau dựng lại");
            Assert.That(dummy.Current, Is.EqualTo(dummy.Max), "Dựng lại đầy máu");
        }
    }
}
