using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// Lỗi di chuyển đã sửa: giữ tay rút lên đá không văng vòng qua đỉnh đá; hướng mặt không lật qua lật lại khi đu / bay;
    /// nhện quay đầu, lật ngược không bị bóp dẹt thành sợi chỉ.
    /// Mặt sàn xuất phát y = -1, x -5..5. Tảng đá đầu tiên: x 6..8, mặt dưới y = 4, mặt trên y = 5.
    /// </summary>
    public class MovementFixTests
    {
        static readonly Vector2 FirstRock = new Vector2(7f, 4.5f);
        static readonly Vector2 InAirNearFirstRock = new Vector2(3f, 1f);
        const float RestingY = -0.5f;

        OctopusController _player;
        SwingController _swing;
        Rigidbody2D _body;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        IEnumerator Load(PlayableCharacter character)
        {
            CharacterSelection.Choose(character);
            yield return SceneManager.LoadSceneAsync("Playground");
            _player = Object.FindAnyObjectByType<OctopusController>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _swing = _player.Swing;
            _body = _player.GetComponent<Rigidbody2D>();
            yield return new WaitForFixedUpdate();
        }

        IEnumerator GrabFromAir()
        {
            _player.SetIntent(holding: false, drag: 0f);
            _player.ResetTo(InAirNearFirstRock);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(_player.Grab(FirstRock), "Phải bám được tảng đá đầu tiên");
        }

        // Giữ tay rút lên đá ở nhịp đu nào cũng vậy: người luôn ở dưới mặt dưới của đá (không văng lên đỉnh đá), quét quanh
        // chỗ bám chưa tới nửa vòng, xúc tu không xuyên qua đá, tới nơi thì đang ở ngay dưới chỗ bám và không lao vun vút
        [UnityTest]
        public IEnumerator Reel_FromAnySwingPhase_ClingsFromBelowWithoutLooping(
            [Values(0f, 0.4f, 0.8f, 1.2f, 1.4f, 1.8f)] float swingFirst)
        {
            yield return Load(PlayableCharacter.Octopus);
            yield return GrabFromAir();
            for (float t = 0f; t < swingFirst; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();

            Vector2 anchor = _swing.AnchorPoint;
            Collider2D rock = _swing.AnchorCollider;
            _player.SetIntent(holding: true, drag: 0f);

            Vector2 last = _body.position;
            float lastAngle = Angle(last - anchor);
            float highest = float.MinValue, sweep = 0f, fastestNear = 0f, time = 0f;
            int ropeThroughRock = 0;
            while (time < 2f)
            {
                yield return new WaitForFixedUpdate();
                time += Time.fixedDeltaTime;
                if (_player.State != OctopusState.Swinging)
                    break; // vừa bám: thân đã được đặt vào chỗ bám, vị trí cuối lúc còn đu là last

                Vector2 p = _body.position;
                highest = Mathf.Max(highest, p.y);
                float angle = Angle(p - anchor);
                sweep += Mathf.Abs(Mathf.DeltaAngle(lastAngle, angle));
                lastAngle = angle;
                if (Vector2.Distance(p, anchor) < 1.6f)
                    fastestNear = Mathf.Max(fastestNear, _body.linearVelocity.magnitude);

                // Dây từ thân tới chỗ bám không được cắt qua tảng đá đang bám
                Vector2 end = anchor + (p - anchor).normalized * 0.05f;
                RaycastHit2D hit = Physics2D.Linecast(p, end, _swing.AnchorMask);
                if (hit.collider == rock)
                    ropeThroughRock++;
                last = p;
            }

            _player.SetIntent(holding: false, drag: 0f);
            string stats = $"cao nhất {highest:0.00}, quét {sweep:0}°, lệch ngang lúc tới {last.x - anchor.x:0.00}, " +
                           $"nhanh nhất gần đá {fastestNear:0.0} u/s, {time:0.00} s";
            Debug.Log($"[MovementFix] đu trước {swingFirst:0.0} s rồi rút: {stats}");
            Assert.That(_player.State, Is.EqualTo(OctopusState.Clinging), $"Giữ tay phải rút lên bám đá ({stats})");
            Assert.That(highest, Is.LessThan(anchor.y - 0.4f), $"Không được văng lên cao ngang / qua mặt dưới đá ({stats})");
            Assert.That(sweep, Is.LessThan(180f), $"Không được quay vòng quanh chỗ bám ({stats})");
            Assert.That(ropeThroughRock, Is.EqualTo(0), $"Xúc tu không được xuyên qua đá ({stats})");
            Assert.That(Mathf.Abs(last.x - anchor.x), Is.LessThan(0.35f), $"Phải lên đá từ ngay bên dưới chỗ bám ({stats})");
            Assert.That(fastestNear, Is.LessThan(12f), $"Gần đá không được lao vun vút ({stats})");
            Assert.That(_body.position.y, Is.EqualTo(3.5f).Within(0.2f), "Bám sát mặt dưới đá");
        }

        // Đu qua lại rồi rút lên bám đá mà không vuốt: hướng mặt không đổi lần nào (trước đây lật theo dấu vận tốc ngang,
        // nhịp đu / văng quanh đá đổi chiều liên tục nên nhện lật mặt nhiều lần)
        [UnityTest]
        public IEnumerator SwingAndReel_WithoutDrag_KeepsFacing()
        {
            yield return Load(PlayableCharacter.Spider);
            var rig = _player.GetComponentInChildren<SpiderRigAnimator>();
            _player.ResetTo(InAirNearFirstRock);
            _player.SetIntent(holding: false, drag: -1f); // đang quay sang trái, đá ở bên phải
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(_player.Grab(FirstRock), "Phải bám được tảng đá đầu tiên");
            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_player.Facing, Is.GreaterThan(0f), "Bám đá hẳn về bên phải thì quay về phía đá");

            float facing = _player.Facing, rigFacing = 1f;
            int flips = 0, rigFlips = 0;
            float clock = 0f;
            bool reeling = false;
            while (clock < 5f && _player.State != OctopusState.Clinging)
            {
                yield return null;
                clock += Time.deltaTime;
                if (!reeling && clock > 2.6f)
                {
                    reeling = true; // đu qua lại hơn một nhịp rồi mới giữ tay rút lên
                    _player.SetIntent(holding: true, drag: 0f);
                }

                if (_player.Facing != facing)
                    flips++;
                facing = _player.Facing;

                // Hình nhện theo kịp hướng mặt sau một frame; đếm từ lúc đã đu ổn định
                float shown = Mathf.Sign(rig.Facing);
                if (clock > 0.3f && shown != rigFacing)
                    rigFlips++;
                rigFacing = shown;
            }

            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Clinging), "Phải rút lên bám được đá");
            Assert.That(flips, Is.EqualTo(0), "Đu / rút không vuốt thì không được đổi hướng mặt");
            Assert.That(rigFlips, Is.EqualTo(0), "Nhện không được lật mặt khi đu / rút lên đá");
        }

        // Trên không: bị hất ngược thoáng qua thì không quay; trôi ngược hẳn một lúc thì mới quay theo hướng bay
        [UnityTest]
        public IEnumerator Airborne_TurnsOnlyWhenClearlyDriftingBackward()
        {
            yield return Load(PlayableCharacter.Octopus);
            _player.ResetTo(new Vector2(0f, 9f));
            _player.SetIntent(holding: false, drag: 1f);
            yield return new WaitForFixedUpdate();
            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_player.Facing, Is.GreaterThan(0f), "Vuốt thì quay ngay");

            yield return Drift(-4f, 0.1f);
            Assert.That(_player.Facing, Is.GreaterThan(0f), "Bị hất ngược thoáng qua thì không quay");
            yield return Drift(0f, 0.1f);
            yield return Drift(-4f, 0.45f);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Airborne));
            Assert.That(_player.Facing, Is.LessThan(0f), "Bay ngược hẳn một lúc thì quay theo hướng bay");
        }

        // Chiêu giữ hướng (HoldFacing): bị đẩy lùi lâu vẫn nhìn về phía trước; vuốt thì vẫn quay được
        [UnityTest]
        public IEnumerator HoldFacing_KeepsFacingWhilePushedBack()
        {
            yield return Load(PlayableCharacter.Octopus);
            _player.ResetTo(new Vector2(0f, 9f));
            _player.SetIntent(holding: false, drag: 1f);
            yield return new WaitForFixedUpdate();
            _player.SetIntent(holding: false, drag: 0f);
            _player.HoldFacing(1f);

            yield return Drift(-4f, 0.5f);
            Assert.That(_player.Facing, Is.GreaterThan(0f), "Đang giữ hướng thì bị đẩy lùi vẫn nhìn về phía trước");

            _player.SetIntent(holding: false, drag: -1f);
            yield return new WaitForFixedUpdate();
            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_player.Facing, Is.LessThan(0f), "Vuốt thì vẫn quay ngay");
        }

        // Nhện quay đầu: đổi chiều ngay giữa lượt, thân không bao giờ bị bóp dẹt thành sợi chỉ
        [UnityTest]
        public IEnumerator Spider_Turn_NeverSquashesToSliver()
        {
            yield return Load(PlayableCharacter.Spider);
            var rig = _player.GetComponentInChildren<SpiderRigAnimator>();
            _player.ResetTo(new Vector2(0f, RestingY + 0.05f));
            _player.SetIntent(holding: false, drag: 1f);
            yield return new WaitForSeconds(0.3f);
            _player.SetIntent(holding: false, drag: 0f);
            yield return new WaitForSeconds(0.4f);
            float width = Mathf.Abs(rig.PoseTransform.localScale.x);

            float narrowest = width;
            _player.SetIntent(holding: false, drag: -1f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                yield return null;
                narrowest = Mathf.Min(narrowest, Mathf.Abs(rig.PoseTransform.localScale.x));
            }

            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(rig.PoseTransform.localScale.x, Is.LessThan(0f), "Đã quay sang trái");
            Assert.That(narrowest, Is.GreaterThan(width * 0.6f), $"Quay đầu không được bóp dẹt (hẹp nhất {narrowest / width:0.00} lần)");
        }

        // Nhện rút tơ lên đá rồi lật ngược bám: lúc lật cũng không bị bóp dẹt thành sợi chỉ
        [UnityTest]
        public IEnumerator Spider_ReelToCling_FlipsUpsideDownWithoutSliver()
        {
            yield return Load(PlayableCharacter.Spider);
            var rig = _player.GetComponentInChildren<SpiderRigAnimator>();
            yield return GrabFromAir();
            yield return new WaitForSeconds(0.3f);
            float height = Mathf.Abs(rig.PoseTransform.localScale.y);

            float flattest = height;
            _player.SetIntent(holding: true, drag: 0f);
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                yield return null;
                if (_player.State == OctopusState.Clinging)
                    _player.SetIntent(holding: false, drag: 0f);
                flattest = Mathf.Min(flattest, Mathf.Abs(rig.PoseTransform.localScale.y));
            }

            Assert.That(_player.State, Is.EqualTo(OctopusState.Clinging));
            Assert.That(rig.PoseTransform.lossyScale.y, Is.LessThan(0f), "Bám đá thì lật ngược người");
            Assert.That(flattest, Is.GreaterThan(height * 0.6f), $"Lật ngược không được bóp dẹt (dẹt nhất {flattest / height:0.00} lần)");
        }

        // Đẩy thân trôi ngang với vận tốc vx trong seconds giây (vận tốc dọc để trọng lực lo)
        IEnumerator Drift(float vx, float seconds)
        {
            for (float t = 0f; t < seconds - 0.001f; t += Time.fixedDeltaTime)
            {
                _body.linearVelocity = new Vector2(vx, _body.linearVelocity.y);
                yield return new WaitForFixedUpdate();
            }
        }

        static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    }
}
