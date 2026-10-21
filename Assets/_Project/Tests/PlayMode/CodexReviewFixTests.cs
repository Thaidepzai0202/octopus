using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// Các lỗi Codex review tìm ra (2026-10-20): người chơi đã gục vẫn ra chiêu / chiêu vẫn chạy; phần kéo dài của chiêu cuối
    /// bị chiêu dùng sau đổi cờ cộng nộ; boss đang húc lao bị choáng vẫn lao tiếp.
    /// </summary>
    public class CodexReviewFixTests
    {
        const float RestingY = -0.5f;
        const float DummyX = 3.7f;
        const float StandY = 0.55f;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        static IEnumerator Wait(System.Func<bool> until, float max)
        {
            for (float t = 0f; t < max && !until(); t += Time.deltaTime)
                yield return null;
        }

        // Gục giữa lúc đang ra chiêu: chiêu đang chạy bị huỷ, bấm chiêu nữa không được
        [UnityTest]
        public IEnumerator PlayerDeath_CancelsRunningSkill_AndRejectsNewCasts()
        {
            CharacterSelection.Choose(PlayableCharacter.Octopus);
            yield return SceneManager.LoadSceneAsync("BossArena");
            var boss = Object.FindAnyObjectByType<LionBoss>();
            boss.SetAi(false);
            var player = Object.FindAnyObjectByType<PlayerHealth>();
            var controller = player.GetComponent<OctopusController>();
            controller.GetComponent<PlayerInputReader>().enabled = false;
            var combat = player.GetComponent<CombatController>();
            controller.ResetTo(new Vector2(-8f, StandY));
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(combat.Use(1, 0), "Bắn laze (chiêu dài 3 giây)");
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(combat.Busy);

            player.GetComponent<Health>().TakeHit(99999f, boss.transform.position, default);
            yield return null;
            Assert.IsTrue(player.IsDead, "Gục");
            Assert.IsFalse(combat.Busy, "Chiêu đang chạy bị huỷ khi gục");
            Assert.IsFalse(combat.Use(0, 0), "Gục rồi thì không ra chiêu được (bấm nút trên màn hình vẫn gọi Use)");
            Assert.That(player.GetComponent<LaserSkill>().Phase, Is.EqualTo(LaserPhase.Ready), "Tia laze đang dở cũng tắt");
            yield return new WaitForSeconds(1.5f);
            Assert.That(player.GetComponent<LaserSkill>().Phase, Is.EqualTo(LaserPhase.Ready), "Không tự bắn tiếp từ cái xác");
        }

        // BLUE · Kĩ năng đặc biệt áp chế: lồng xích còn gây sát thương sau khi chiêu xong; dùng đánh thường (trượt) lúc đó
        // không được làm các nhát của lồng xích cộng nộ (chiêu cuối không cộng nộ)
        [UnityTest]
        public IEnumerator LingeringUltimateDamage_DoesNotGainRage_AfterAnotherCast()
        {
            CharacterSelection.Choose(PlayableCharacter.Blue);
            yield return SceneManager.LoadSceneAsync("Playground");
            var controller = Object.FindAnyObjectByType<OctopusController>();
            controller.GetComponent<PlayerInputReader>().enabled = false;
            var combat = controller.GetComponent<CombatController>();
            RageMeter rage = combat.Rage;
            Health dummy = null;
            foreach (TrainingDummy d in Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None))
                if (Mathf.Abs(d.transform.position.x - DummyX) < 0.5f)
                    dummy = d.GetComponent<Health>();
            Assert.IsNotNull(dummy);

            controller.ResetTo(new Vector2(DummyX - 1.5f, RestingY + 0.05f));
            yield return new WaitForSeconds(0.5f);
            controller.SetIntent(holding: false, drag: 1f);
            yield return new WaitForFixedUpdate();
            yield return null;
            controller.SetIntent(holding: false, drag: 0f);
            yield return new WaitForSeconds(0.3f);

            rage.Add(RageMeter.PerTier * 3f);
            Assert.IsTrue(combat.Use(2, 2), "Kĩ năng đặc biệt áp chế");
            yield return Wait(() => !combat.Busy, 5f);

            // Ra xa rồi đánh thường vào khoảng không (không trúng gì nên tự nó không cộng nộ)
            controller.ResetTo(new Vector2(DummyX - 9f, RestingY + 0.05f));
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(combat.Use(0, 0), "Đánh thường");
            yield return Wait(() => !combat.Busy, 2f);
            yield return new WaitForSeconds(0.2f);

            float dealt = 0f;
            dummy.Damaged += (amount, from, effect) => dealt += amount;
            float before = rage.Value;
            yield return new WaitForSeconds(1f);
            Assert.That(dealt, Is.GreaterThan(0f), "Lồng xích vẫn đang gây sát thương");
            Assert.That(rage.Value - before, Is.LessThan(0.5f), "Sát thương của chiêu cuối không cộng nộ");
        }

        // RED · Kĩ năng tầm xa: sóng lưỡi liềm đã bay ra mà người chơi gục thì sóng không còn gây sát thương
        // (đối chứng: không gục thì sóng trúng boss)
        [UnityTest]
        public IEnumerator ProjectileInFlight_DealsNoDamage_AfterCasterDies([Values(false, true)] bool dies)
        {
            CharacterSelection.Choose(PlayableCharacter.Red);
            yield return SceneManager.LoadSceneAsync("BossArena");
            var boss = Object.FindAnyObjectByType<LionBoss>();
            boss.SetAi(false);
            var player = Object.FindAnyObjectByType<PlayerHealth>();
            var controller = player.GetComponent<OctopusController>();
            controller.GetComponent<PlayerInputReader>().enabled = false;
            var combat = player.GetComponent<CombatController>();

            Bounds body = boss.GetComponentInChildren<ILionVisual>().VisualBounds;
            float side = Mathf.Sign(body.center.x) > 0f ? -1f : 1f; // đứng phía giữa đấu trường
            float x = (side < 0f ? body.min.x : body.max.x) + side * 7.5f;
            controller.ResetTo(new Vector2(x, StandY));
            yield return new WaitForSeconds(0.4f);
            controller.SetIntent(false, -side);
            yield return new WaitForFixedUpdate();
            yield return null;
            controller.SetIntent(false, 0f);
            controller.ResetTo(new Vector2(x, StandY));
            yield return new WaitForSeconds(0.4f);

            Assert.IsTrue(combat.Use(1, 1), "Kĩ năng tầm xa");
            yield return new WaitForSeconds(0.55f); // sóng đã phóng (~0.45 s), chưa tới boss
            float dealt = 0f;
            boss.Health.Damaged += (amount, from, effect) => dealt += amount;
            if (dies)
                player.GetComponent<Health>().TakeHit(99999f, boss.transform.position, default);
            yield return new WaitForSeconds(1.5f);
            if (dies)
                Assert.That(dealt, Is.EqualTo(0f), "Người chơi gục rồi thì sóng đang bay không gây sát thương");
            else
                Assert.That(dealt, Is.GreaterThan(0f), "Đối chứng: không gục thì sóng trúng boss");
        }

        // Boss đang vồ (giữa không trung) mà bị choáng thì đứng hình tới hết choáng, không đè trúng ai
        [UnityTest]
        public IEnumerator BossPounce_PausesWhileStunned()
        {
            CharacterSelection.Choose(PlayableCharacter.Octopus);
            yield return SceneManager.LoadSceneAsync("BossArena");
            var boss = Object.FindAnyObjectByType<LionBoss>();
            boss.SetAi(false);
            var player = Object.FindAnyObjectByType<PlayerHealth>();
            var controller = player.GetComponent<OctopusController>();
            controller.GetComponent<PlayerInputReader>().enabled = false;
            controller.ResetTo(new Vector2(-10f, StandY));
            yield return new WaitForSeconds(0.5f);

            float floor = boss.transform.position.y;
            boss.ForceAttack(LionAttack.Pounce);
            yield return Wait(() => boss.transform.position.y > floor + 0.5f, 3f);
            Assert.That(boss.transform.position.y, Is.GreaterThan(floor + 0.5f), "Đang bay vồ");

            boss.Health.TakeHit(1f, controller.transform.position, new HitEffect { stun = 3f });
            yield return null;
            Assert.IsTrue(boss.IsStunned, "Bị choáng");
            Vector3 at = boss.transform.position;
            yield return new WaitForSeconds(0.3f);
            Assert.That(Vector2.Distance(boss.transform.position, at), Is.LessThan(0.05f), "Choáng thì dừng giữa cú vồ");

            yield return Wait(() => !boss.IsStunned, 3f);
            yield return new WaitForSeconds(1.5f);
            Assert.That(boss.transform.position.y, Is.EqualTo(floor).Within(0.05f), "Hết choáng thì vồ nốt và đáp xuống");
        }

        // Đang đu (dây căng, không vuốt, không rút) mà bị đánh văng: cú hất phải cộng vào nhịp đu, không bị cơ chế giữ cơ năng
        // của dây kéo về mức cũ
        [UnityTest]
        public IEnumerator Knockback_WhileSwinging_AddsEnergy()
        {
            CharacterSelection.Choose(PlayableCharacter.Octopus);
            yield return SceneManager.LoadSceneAsync("Playground");
            var controller = Object.FindAnyObjectByType<OctopusController>();
            controller.GetComponent<PlayerInputReader>().enabled = false;
            var body = controller.GetComponent<Rigidbody2D>();
            controller.ResetTo(new Vector2(3f, 1f));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(controller.Grab(new Vector2(7f, 4.5f)), "Bám tảng đá đầu tiên");
            yield return new WaitForSeconds(0.6f);
            Assert.That(controller.State, Is.EqualTo(OctopusState.Swinging));

            float Energy() => 0.5f * body.linearVelocity.sqrMagnitude - Vector2.Dot(Physics2D.gravity * body.gravityScale, body.position);
            float before = Energy();
            // Hất dọc theo hướng đang đu (tiếp tuyến với dây) nên chắc chắn cộng thêm động năng
            Vector2 along = body.linearVelocity.sqrMagnitude > 0.01f
                ? body.linearVelocity.normalized
                : Vector2.Perpendicular(body.position - controller.Swing.AnchorPoint).normalized;
            controller.Knockback(along * 8f);
            float kicked = Energy();
            Assert.That(kicked, Is.GreaterThan(before + 5f), "Cú hất làm nhịp đu mạnh lên");
            for (int i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(controller.State, Is.EqualTo(OctopusState.Swinging), "Vẫn đu");
            Assert.That(Energy(), Is.GreaterThan(before + (kicked - before) * 0.6f), "Bước vật lý sau không triệt mất cú hất");
        }

        // Boss đang húc lao mà bị choáng (chưa tới mức choáng gục) thì đứng yên tới hết choáng, rồi mới lao tiếp
        [UnityTest]
        public IEnumerator BossCharge_StopsWhileStunned()
        {
            CharacterSelection.Choose(PlayableCharacter.Octopus);
            yield return SceneManager.LoadSceneAsync("BossArena");
            var boss = Object.FindAnyObjectByType<LionBoss>();
            boss.SetAi(false);
            var player = Object.FindAnyObjectByType<PlayerHealth>();
            var controller = player.GetComponent<OctopusController>();
            controller.GetComponent<PlayerInputReader>().enabled = false;
            controller.ResetTo(new Vector2(-20f, StandY));
            yield return new WaitForSeconds(0.5f);

            float start = boss.transform.position.x;
            boss.ForceAttack(LionAttack.Charge);
            yield return Wait(() => Mathf.Abs(boss.transform.position.x - start) > 0.5f, 3f);
            Assert.That(Mathf.Abs(boss.transform.position.x - start), Is.GreaterThan(0.5f), "Đã bắt đầu lao");

            boss.Health.TakeHit(1f, controller.transform.position, new HitEffect { stun = 3f });
            yield return null;
            Assert.IsTrue(boss.IsStunned, "Bị choáng");
            float x = boss.transform.position.x;
            yield return new WaitForSeconds(0.3f);
            Assert.That(boss.transform.position.x, Is.EqualTo(x).Within(0.05f), "Choáng thì đứng yên");

            yield return Wait(() => !boss.IsStunned, 3f);
            yield return new WaitForSeconds(0.2f);
            Assert.That(Mathf.Abs(boss.transform.position.x - x), Is.GreaterThan(0.3f), "Hết choáng thì lao tiếp");
        }
    }
}
