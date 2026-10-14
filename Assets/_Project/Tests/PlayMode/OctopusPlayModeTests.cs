using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// Chạy vật lý thật trên scene Playground. Input thật bị tắt; test điều khiển qua
    /// OctopusController.Grab / Tap / SetIntent giống như người chơi chạm, giữ, vuốt.
    /// </summary>
    public class OctopusPlayModeTests
    {
        // Tảng đá đầu tiên: x 6..8, mặt dưới y = 4. Mặt sàn xuất phát y = -1, x -5..5
        static readonly Vector2 FirstRock = new Vector2(7f, 4.5f);
        static readonly Vector2 InAirNearFirstRock = new Vector2(3f, 1f);
        const float RestingY = -0.5f;

        OctopusController _octopus;
        SwingController _swing;
        Rigidbody2D _body;

        [UnitySetUp]
        public IEnumerator LoadPlayground()
        {
            yield return SceneManager.LoadSceneAsync("Playground");
            _octopus = Object.FindAnyObjectByType<OctopusController>();
            _octopus.GetComponent<PlayerInputReader>().enabled = false;
            _swing = _octopus.Swing;
            _body = _octopus.GetComponent<Rigidbody2D>();
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Octopus_LandsOnStartPlatform()
        {
            yield return new WaitForSeconds(1f);

            Assert.That(_body.position.y, Is.EqualTo(RestingY).Within(0.1f));
            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Grounded));
        }

        [UnityTest]
        public IEnumerator Drag_OnGround_CrawlsRight()
        {
            yield return StandAt(0f);
            _octopus.SetIntent(holding: false, drag: 1f);
            yield return new WaitForSeconds(1f);

            Assert.That(_body.position.x, Is.GreaterThan(2.5f));
            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Grounded));
        }

        [UnityTest]
        public IEnumerator Tap_OnGround_Jumps()
        {
            yield return StandAt(0f);
            _octopus.Tap();

            float maxY = _body.position.y;
            for (float t = 0f; t < 1.5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                maxY = Mathf.Max(maxY, _body.position.y);
            }

            Assert.That(maxY, Is.GreaterThan(1.5f), "Phải nhảy lên cao");
            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Grounded), "Phải rơi lại xuống sàn");
        }

        [UnityTest]
        public IEnumerator Grab_OutOfReach_Fails()
        {
            yield return StandAt(-4f);

            Assert.IsFalse(_octopus.Grab(FirstRock), "Đá ngoài bán kính xúc tu thì không bám được");
            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Grounded));
        }

        [UnityTest]
        public IEnumerator Grab_EmptySpace_Fails()
        {
            yield return StandAt(2f);

            Assert.IsFalse(_octopus.Grab(new Vector2(2f, 2f)));
            Assert.IsFalse(_swing.IsAttached);
        }

        [UnityTest]
        public IEnumerator Grab_FromPlatform_LaunchesIntoSwing()
        {
            yield return StandAt(2f);
            Assert.IsTrue(_octopus.Grab(FirstRock), "Phải bám được tảng đá đầu tiên");

            float maxY = _body.position.y;
            float maxX = _body.position.x;
            for (float t = 0f; t < 1.5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                maxY = Mathf.Max(maxY, _body.position.y);
                maxX = Mathf.Max(maxX, _body.position.x);
            }

            Assert.That(maxY, Is.GreaterThan(0f), "Phải bị giật lên khỏi sàn");
            Assert.That(maxX, Is.GreaterThan(5.5f), "Phải văng qua mép sàn (x = 5) để bắt đầu đu");
        }

        [UnityTest]
        public IEnumerator Swing_StaysAttachedWithoutHolding()
        {
            yield return GrabFromAir();
            Vector2 anchor = _swing.AnchorPoint;
            float ropeLength = _swing.RopeLength;

            for (float t = 0f; t < 2f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(Vector2.Distance(_body.position, anchor), Is.LessThanOrEqualTo(ropeLength + 0.1f));
            }

            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Swinging), "Nhả tay vẫn phải bám");
        }

        [UnityTest]
        public IEnumerator Tap_WhileSwinging_ReleasesWithBoost()
        {
            yield return GrabFromAir();
            yield return new WaitForSeconds(0.5f);

            float speedBefore = _body.linearVelocity.magnitude;
            _octopus.Tap();

            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Airborne));
            Assert.IsFalse(_swing.IsAttached);
            Assert.That(_body.linearVelocity.magnitude, Is.GreaterThan(speedBefore));
        }

        [UnityTest]
        public IEnumerator Hold_WhileSwinging_ReelsUpAndClingsToRock()
        {
            yield return GrabFromAir();
            _octopus.SetIntent(holding: true, drag: 0f);

            for (float t = 0f; t < 3f && _octopus.State != OctopusState.Clinging; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();

            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Clinging), "Giữ tay phải rút lên tới đá");
            Assert.That(_body.position.y, Is.EqualTo(3.5f).Within(0.2f), "Phải bám sát mặt dưới đá");
        }

        [UnityTest]
        public IEnumerator Hold_ReleasedMidway_StopsReeling()
        {
            yield return GrabFromAir();
            _octopus.SetIntent(holding: true, drag: 0f);
            yield return new WaitForSeconds(0.2f);
            _octopus.SetIntent(holding: false, drag: 0f);

            float lengthAfterRelease = _swing.RopeLength;
            yield return new WaitForSeconds(0.5f);

            Assert.That(_swing.RopeLength, Is.EqualTo(lengthAfterRelease).Within(0.001f), "Nhả tay thì ngừng rút");
            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Swinging));
        }

        [UnityTest]
        public IEnumerator Clinging_DragCrawlsAlongRock_TapDrops()
        {
            yield return GrabFromAir();
            _octopus.SetIntent(holding: true, drag: 0f);
            for (float t = 0f; t < 3f && _octopus.State != OctopusState.Clinging; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Clinging));

            _octopus.SetIntent(holding: false, drag: 1f);
            yield return new WaitForSeconds(1.5f);
            Assert.That(_body.position.x, Is.EqualTo(8f).Within(0.1f), "Bò tới mép phải của đá thì dừng");

            _octopus.SetIntent(holding: false, drag: 0f);
            _octopus.Tap();
            yield return new WaitForSeconds(0.3f);
            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Airborne));
            Assert.That(_body.position.y, Is.LessThan(3f), "Thả ra thì rơi xuống");
        }

        // Rút xúc tu khi đang đu phải làm vận tốc tăng (bảo toàn mô-men động lượng → cộng hưởng)
        [UnityTest]
        public IEnumerator Reeling_WhileSwinging_SpeedsUp()
        {
            float withoutReel = 0f;
            float withReel = 0f;
            yield return SpeedAfterSwing(reel: false, speed => withoutReel = speed);
            yield return SpeedAfterSwing(reel: true, speed => withReel = speed);

            Assert.That(withReel, Is.GreaterThan(withoutReel * 1.1f),
                $"Có rút: {withReel:0.0} u/s, không rút: {withoutReel:0.0} u/s");
        }

        [UnityTest]
        public IEnumerator Rig_TentaclesAreSkinnedToBones()
        {
            yield return StandAt(0f);
            var skins = Object.FindObjectsByType<SpriteSkin>(FindObjectsSortMode.None);
            Assert.That(skins.Length, Is.EqualTo(4), "Phải có đầu + 3 xúc tu gắn xương (chạy Octopus → Dựng lại màn chơi)");
            foreach (SpriteSkin skin in skins)
            {
                Sprite sprite = skin.GetComponent<SpriteRenderer>().sprite;
                Assert.That(sprite.GetBones().Length, Is.GreaterThan(2), $"{skin.name}: ảnh chưa có xương");
                Assert.That(skin.boneTransforms.Length, Is.EqualTo(sprite.GetBones().Length), $"{skin.name}: thiếu xương");
                Assert.IsNotNull(skin.rootBone);
                Assert.IsTrue(skin.HasCurrentDeformedVertices(), $"{skin.name}: SpriteSkin chưa uốn ảnh");
            }
        }

        // Bò kiểu trườn: chóp xúc tu luôn sát đất (không nhấc lên bước như đi bộ), xúc tu uốn sóng, đầu thấp hơn lúc đứng
        [UnityTest]
        public IEnumerator Rig_Crawl_SlithersLowWithoutStepping()
        {
            var rig = Object.FindAnyObjectByType<OctopusRigAnimator>();
            yield return StandAt(-3f);
            yield return new WaitForSeconds(0.3f);
            float ground = _body.position.y - 0.5f;
            float standingHead = rig.HeadTipPosition.y;

            _octopus.SetIntent(holding: false, drag: 1f);
            yield return new WaitForSeconds(0.4f);
            float highestFoot = float.MinValue, crawlingHead = 0f;
            float tipMin = float.MaxValue, tipMax = float.MinValue;
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForSeconds(0.03f);
                for (int f = 0; f < 3; f++)
                    highestFoot = Mathf.Max(highestFoot, rig.FootPosition(f).y - ground);
                crawlingHead += rig.HeadTipPosition.y / 30f;
                float tip = rig.TipPosition(1).y - _body.position.y;
                tipMin = Mathf.Min(tipMin, tip);
                tipMax = Mathf.Max(tipMax, tip);
            }

            _octopus.SetIntent(holding: false, drag: 0f);
            Assert.That(highestFoot, Is.LessThan(0.12f), $"Trườn thì chóp xúc tu không nhấc khỏi đất (cao nhất {highestFoot:0.00})");
            Assert.That(crawlingHead, Is.LessThan(standingHead - 0.1f), $"Bò thì đầu thấp hơn lúc đứng ({crawlingHead:0.00} so với {standingHead:0.00})");
            Assert.That(tipMax - tipMin, Is.GreaterThan(0.02f), "Ngọn xúc tu phải uốn lượn");
        }

        // Đầu mềm như áo bạch tuộc: bò sang phải thì đỉnh đầu ngả về phía sau so với lúc đứng yên
        [UnityTest]
        public IEnumerator Rig_Crawl_MantleTrailsBehind()
        {
            var rig = Object.FindAnyObjectByType<OctopusRigAnimator>();
            yield return StandAt(-3f);
            yield return new WaitForSeconds(0.3f);
            float idle = rig.HeadTipPosition.x - _body.position.x;

            _octopus.SetIntent(holding: false, drag: 1f);
            float crawling = 0f;
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSeconds(0.03f);
                crawling += (rig.HeadTipPosition.x - _body.position.x) / 20f;
            }

            _octopus.SetIntent(holding: false, drag: 0f);
            Assert.That(crawling, Is.LessThan(idle - 0.03f), $"Đỉnh đầu khi bò ({crawling:0.00}) phải ngả ra sau so với khi đứng ({idle:0.00})");
        }

        [UnityTest]
        public IEnumerator Rig_Swing_TentacleReachesRock()
        {
            var rig = Object.FindAnyObjectByType<OctopusRigAnimator>();
            yield return GrabFromAir();
            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(rig.IsGrabbing, "Đang đu thì xúc tu giữa phải đang nắm đá");
            Assert.That(Vector2.Distance(rig.GrabPoint, _swing.AnchorPoint), Is.LessThan(0.15f),
                "Xúc tu phải vươn tới đúng chỗ bám trên đá");
        }

        [UnityTest]
        public IEnumerator Rig_FallingFast_TentaclesTrailUpward()
        {
            var rig = Object.FindAnyObjectByType<OctopusRigAnimator>();
            _octopus.ResetTo(new Vector2(20f, 8f)); // trên vực, không có gì đỡ
            yield return new WaitForSeconds(0.8f);

            for (int i = 0; i < 3; i++)
                Assert.That(rig.TipPosition(i).y, Is.GreaterThan(_body.position.y), $"Rơi nhanh thì chóp xúc tu {i} phải bay lên trên");
        }

        [UnityTest]
        public IEnumerator Rig_Clinging_SideTentaclesGripRock()
        {
            var rig = Object.FindAnyObjectByType<OctopusRigAnimator>();
            yield return GrabFromAir();
            _octopus.SetIntent(holding: true, drag: 0f);
            for (float t = 0f; t < 3f && _octopus.State != OctopusState.Clinging; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            _octopus.SetIntent(holding: false, drag: 0f);
            yield return new WaitForSeconds(0.4f);

            // Mặt dưới đá ở y = 4
            Assert.That(rig.TipPosition(0).y, Is.GreaterThan(3.85f), "Xúc tu trái phải nắm lên mặt đá");
            Assert.That(rig.TipPosition(2).y, Is.GreaterThan(3.85f), "Xúc tu phải phải nắm lên mặt đá");
        }

        [UnityTest]
        public IEnumerator Rig_EyeBlinks()
        {
            var rig = Object.FindAnyObjectByType<OctopusRigAnimator>();
            yield return StandAt(0f);
            bool blinked = false;
            for (float t = 0f; t < 6f && !blinked; t += Time.deltaTime)
            {
                yield return null;
                blinked = rig.EyeClosed;
            }

            Assert.IsTrue(blinked, "Đứng yên vài giây phải chớp mắt");
        }

        // Bật nhảy thì thân dãn dài, chạm đất thì thân bẹp xuống (squash & stretch)
        [UnityTest]
        public IEnumerator Jump_StretchesBody_LandingSquashesIt()
        {
            yield return StandAt(0f);
            var animator = Object.FindAnyObjectByType<OctopusRigAnimator>();
            _octopus.Tap();

            float maxStretch = 0f;
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                yield return null;
                maxStretch = Mathf.Max(maxStretch, animator.Squash);
            }

            for (float t = 0f; t < 2f && _octopus.State != OctopusState.Grounded; t += Time.deltaTime)
                yield return null;

            float minSquash = 0f;
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                yield return null;
                minSquash = Mathf.Min(minSquash, animator.Squash);
            }

            Assert.That(maxStretch, Is.GreaterThan(0.12f), "Bật nhảy phải dãn thân");
            Assert.That(minSquash, Is.LessThan(-0.08f), "Chạm đất phải bẹp thân");
        }

        [UnityTest]
        public IEnumerator FallingIntoKillZone_RespawnsAtStart()
        {
            _octopus.ResetTo(new Vector2(20f, -8f));
            yield return new WaitForSeconds(1f);

            Assert.That(_body.position.x, Is.EqualTo(0f).Within(0.5f));
            Assert.That(_body.position.y, Is.GreaterThan(-1f));
        }

        IEnumerator StandAt(float x)
        {
            _octopus.ResetTo(new Vector2(x, RestingY + 0.05f));
            yield return new WaitForSeconds(0.5f);
            Assert.That(_octopus.State, Is.EqualTo(OctopusState.Grounded), "Chưa đứng được trên sàn");
        }

        IEnumerator GrabFromAir()
        {
            _octopus.ResetTo(InAirNearFirstRock);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(_octopus.Grab(FirstRock), "Phải bám được tảng đá đầu tiên");
        }

        IEnumerator SpeedAfterSwing(bool reel, System.Action<float> result)
        {
            _octopus.SetIntent(holding: false, drag: 0f);
            yield return GrabFromAir();
            yield return new WaitForSeconds(0.3f);

            _octopus.SetIntent(holding: reel, drag: 0f);
            for (float t = 0f; t < 0.3f; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();

            result(_body.linearVelocity.magnitude);
            _octopus.SetIntent(holding: false, drag: 0f);
        }
    }
}
