using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.U2D.Animation;

namespace Octopus.Tests
{
    /// <summary>
    /// Người chơi chọn nhện (như chọn ở màn chọn nhân vật) rồi vào Playground: cùng cách điều khiển với bạch tuộc.
    /// Mặt sàn xuất phát y = -1, x -5..5. Tảng đá đầu tiên: x 6..8, mặt dưới y = 4.
    /// </summary>
    public class SpiderPlayModeTests
    {
        const float RestingY = -0.5f;
        const float GroundY = -1f;

        OctopusController _player;
        Rigidbody2D _body;
        SpiderRigAnimator _rig;

        [UnitySetUp]
        public IEnumerator LoadAsSpider()
        {
            CharacterSelection.Choose(PlayableCharacter.Spider);
            yield return SceneManager.LoadSceneAsync("Playground");
            _player = Object.FindAnyObjectByType<OctopusController>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _body = _player.GetComponent<Rigidbody2D>();
            _rig = _player.GetComponentInChildren<SpiderRigAnimator>();
            yield return new WaitForFixedUpdate();
        }

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        [UnityTest]
        public IEnumerator Spider_Chosen_ReplacesOctopus()
        {
            yield return StandAt(0f);
            Assert.IsNotNull(_rig, "Chọn nhện thì phải thấy nhện");
            Assert.That(_player.GetComponent<PlayerCharacter>().Current, Is.EqualTo(PlayableCharacter.Spider));
            Assert.IsNull(_player.GetComponentInChildren<OctopusRigAnimator>(), "Chọn nhện thì ẩn bạch tuộc");
            var laser = _player.GetComponent<LaserSkill>();
            Assert.That(Vector2.Distance(laser.Origin, _rig.EyePosition), Is.LessThan(0.01f), "Laze bắn ra từ mắt nhện");
        }

        [UnityTest]
        public IEnumerator Spider_EightLegsSkinnedWithTwoBones()
        {
            yield return null;
            Assert.That(_rig.LegCount, Is.EqualTo(8), "Nhện có 8 chân");
            int legs = 0;
            foreach (SpriteSkin skin in _rig.GetComponentsInChildren<SpriteSkin>())
            {
                if (!skin.name.StartsWith("Leg"))
                    continue;
                legs++;
                Assert.That(skin.boneTransforms.Length, Is.EqualTo(2), $"{skin.name}: đùi + cẳng");
            }

            Assert.That(legs, Is.EqualTo(8));
        }

        // Đứng: móng nằm trên đất, không trượt (trừ lúc nhấc chân gõ), IK đặt móng đúng chỗ, thân phập phồng
        [UnityTest]
        public IEnumerator Spider_Idle_FeetStayPlanted()
        {
            yield return StandAt(-3f);
            var start = new Vector2[8];
            for (int i = 0; i < 8; i++)
                start[i] = _rig.FootPosition(i);
            Transform body = _rig.PoseTransform.Find("Body");
            Vector3 bodyStart = body.localPosition;
            float bodyMoved = 0f;

            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                yield return null;
                bodyMoved = Mathf.Max(bodyMoved, Vector3.Distance(body.localPosition, bodyStart));
                for (int i = 0; i < 8; i++)
                {
                    Vector2 foot = _rig.FootPosition(i);
                    Assert.That(Vector2.Distance(_rig.TipPosition(i), foot), Is.LessThan(0.03f), $"Chân {i}: móng phải tới đúng chỗ");
                    if (_rig.FootLift(i) < 0.001f)
                        Assert.That(Vector2.Distance(foot, start[i]), Is.LessThan(0.01f), $"Chân {i} đang bám không được trượt");
                    Assert.That(foot.y, Is.GreaterThan(GroundY - 0.01f), $"Chân {i} không được lún xuống đất");
                }
            }

            Assert.That(bodyMoved, Is.GreaterThan(0.005f), "Đứng yên thân vẫn phải thở / dồn trọng tâm");
        }

        // Vuốt để bò: móng đang bám đứng yên trên đất, chân nào cũng có lúc bám lúc nhấc, luôn còn ít nhất 3 chân chạm đất
        [UnityTest]
        public IEnumerator Spider_Crawl_PlantedFeetHoldAndLegsAlternate()
        {
            yield return StandAt(-4f);
            _player.SetIntent(holding: false, drag: 1f);
            var lastFoot = new Vector2[8];
            var wasPlanted = new bool[8];
            var maxLift = new float[8];
            var planted = new int[8];
            float worstSlide = 0f;
            int fewestDown = 8;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                yield return null;
                if (t < 0.4f)
                    continue; // đợi chuyển hẳn sang dáng bò

                int down = 0;
                for (int i = 0; i < 8; i++)
                {
                    Vector2 foot = _rig.FootPosition(i);
                    float lift = _rig.FootLift(i);
                    bool onGround = lift < 0.001f;
                    maxLift[i] = Mathf.Max(maxLift[i], lift);
                    Assert.That(foot.y, Is.GreaterThan(GroundY - 0.01f), $"Chân {i} không được lún xuống đất");
                    if (onGround)
                    {
                        down++;
                        planted[i]++;
                        if (wasPlanted[i])
                            worstSlide = Mathf.Max(worstSlide, Mathf.Abs(foot.x - lastFoot[i].x));
                    }

                    wasPlanted[i] = onGround;
                    lastFoot[i] = foot;
                }

                fewestDown = Mathf.Min(fewestDown, down);
            }

            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_body.position.x, Is.GreaterThan(-1f), "Vuốt sang phải thì nhện bò sang phải");
            Assert.That(_rig.Facing, Is.GreaterThan(0f));
            Assert.That(worstSlide, Is.LessThan(0.04f), $"Móng đang bám không được trượt trên đất ({worstSlide:0.000})");
            Assert.That(fewestDown, Is.GreaterThanOrEqualTo(3), "Lúc nào cũng phải còn ít nhất 3 chân chạm đất");
            for (int i = 0; i < 8; i++)
            {
                Assert.That(maxLift[i], Is.GreaterThan(0.05f), $"Chân {i} phải nhấc lên bước");
                Assert.That(planted[i], Is.GreaterThan(10), $"Chân {i} phải có lúc bám đất");
            }
        }

        [UnityTest]
        public IEnumerator Spider_CrawlingLeft_TurnsAround()
        {
            yield return StandAt(2f);
            _player.SetIntent(holding: false, drag: -1f);
            yield return new WaitForSeconds(0.6f);
            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_rig.Facing, Is.LessThan(0f));
            Assert.That(_rig.PoseTransform.localScale.x, Is.LessThan(0f), "Bò sang trái thì quay cả con sang trái");
        }

        // Nhảy: rời đất thì co chân lên khỏi mặt đất
        [UnityTest]
        public IEnumerator Spider_Jump_TucksLegs()
        {
            yield return StandAt(0f);
            _player.Tap();
            yield return new WaitForSeconds(0.25f);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Airborne));
            float ground = _body.position.y - 0.5f;
            for (int i = 0; i < 8; i++)
                Assert.That(_rig.FootLift(i), Is.GreaterThan(0.5f), $"Chân {i} phải rời khỏi dáng bám đất");
            Assert.That(ground, Is.GreaterThan(GroundY + 0.5f), "Đang bay lên");
        }

        // Chạm vào đá: nhả tơ từ bụng tới đá rồi đu
        [UnityTest]
        public IEnumerator Spider_Swing_ShootsWebToRock()
        {
            _player.ResetTo(new Vector2(3f, 1f));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(_player.Grab(new Vector2(7f, 4.5f)), "Phải bám được tảng đá đầu tiên");
            yield return new WaitForSeconds(0.3f);

            Assert.That(_player.State, Is.EqualTo(OctopusState.Swinging));
            LineRenderer web = _rig.Web;
            Assert.IsTrue(web.enabled, "Đang đu phải thấy sợi tơ");
            Assert.That(Vector2.Distance(web.GetPosition(1), _player.Swing.AnchorPoint), Is.LessThan(0.05f), "Tơ dính vào đá");
            Assert.That(Vector2.Distance(web.GetPosition(0), _body.position), Is.LessThan(1.5f), "Tơ nhả ra từ người nhện");
        }

        // Giữ tay rút tơ tới đá: lật ngược người bám mặt dưới đá; vuốt thì bò ngược dọc đá
        [UnityTest]
        public IEnumerator Spider_Cling_HangsUpsideDownAndCrawls()
        {
            _player.ResetTo(new Vector2(3f, 1f));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(_player.Grab(new Vector2(7f, 4.5f)));
            _player.SetIntent(holding: true, drag: 0f);
            for (float t = 0f; t < 3f && _player.State != OctopusState.Clinging; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Clinging), "Chưa bám được dưới đá");
            yield return new WaitForSeconds(0.5f);

            Assert.That(_rig.PoseTransform.lossyScale.y, Is.LessThan(0f), "Bám đá thì lật ngược người");
            for (int i = 0; i < 8; i++)
                Assert.That(_rig.FootPosition(i).y, Is.GreaterThan(3.8f), $"Chân {i} phải bám lên mặt dưới đá");

            float startX = _body.position.x;
            _player.SetIntent(holding: false, drag: -1f);
            yield return new WaitForSeconds(0.25f);
            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_body.position.x, Is.LessThan(startX - 0.3f), "Vuốt sang trái thì bò ngược sang trái dọc đá");
            Assert.That(_rig.PoseTransform.lossyScale.y, Is.LessThan(0f), "Bò ngược vẫn lộn ngược");
        }

        IEnumerator StandAt(float x)
        {
            _player.ResetTo(new Vector2(x, RestingY + 0.05f));
            yield return new WaitForSeconds(0.5f);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Grounded), "Chưa đứng được trên sàn");
        }
    }
}
