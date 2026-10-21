using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>Tầm chiêu ×2: tia laze dài gấp đôi ô Range của Laser Skill (đấu trường boss đủ rộng để tia không chạm vách).</summary>
    public class ReachLaserTests
    {
        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        [UnityTest]
        public IEnumerator LaserBeam_LengthScalesWithRangeScale()
        {
            CharacterSelection.Choose(PlayableCharacter.Octopus);
            yield return SceneManager.LoadSceneAsync("BossArena");
            Object.FindAnyObjectByType<LionBoss>().SetAi(false);
            var controller = Object.FindAnyObjectByType<OctopusController>();
            controller.GetComponent<PlayerInputReader>().enabled = false;
            var laser = controller.GetComponent<LaserSkill>();
            float left = GameObject.Find("Level/Wall Left").GetComponent<Collider2D>().bounds.max.x;

            float[] lengths = new float[2];
            for (int i = 0; i < 2; i++)
            {
                float scale = i == 0 ? 1f : 2f;
                controller.ResetTo(new Vector2(left + 2f, 0.55f));
                yield return new WaitForSeconds(0.4f);
                controller.SetIntent(false, 1f);
                yield return new WaitForFixedUpdate();
                yield return null;
                controller.SetIntent(false, 0f);
                yield return new WaitForSeconds(0.3f);
                Assert.IsTrue(laser.TryCast(new LaserSkill.LaserShot { charge = 0.05f, fire = 0.3f, width = 1f, rangeScale = scale }));
                for (float t = 0f; t < 1f && laser.Phase != LaserPhase.Firing; t += Time.deltaTime)
                    yield return null;
                yield return null;
                lengths[i] = Vector2.Distance(laser.Origin, laser.BeamEnd);
                Assert.IsFalse(laser.BeamHit, "Tia bay thoáng, không chạm vật cản");
                for (float t = 0f; t < 2f && laser.Phase != LaserPhase.Ready; t += Time.deltaTime)
                    yield return null;
            }

            Assert.That(lengths[1], Is.EqualTo(lengths[0] * 2f).Within(0.5f), "Tầm ×2 thì tia dài gấp đôi");
        }
    }
}
