using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// Màn đánh boss (scene BossArena): đấu trường + 3 tầng đá đu được, chiêu người chơi đánh boss, 3 pha, choáng gục, kháng
    /// khống chế, từng đòn của boss đánh trúng người chơi, bất tử sau khi bị đánh, màn thua / thắng, nút Đánh boss.
    /// Sàn y = 0, vách ở x = ±28; boss đứng ở x = 12 quay mặt sang trái, người chơi xuất phát ở x = -19.
    /// </summary>
    public class BossPlayModeTests
    {
        const float FloorY = 0f;
        const float StandY = 0.55f;

        LionBoss _boss;
        PlayerHealth _player;
        OctopusController _controller;
        CombatController _combat;
        Rigidbody2D _body;

        IEnumerator Load(PlayableCharacter character = PlayableCharacter.Octopus, bool quietBoss = true)
        {
            CharacterSelection.Choose(character);
            yield return SceneManager.LoadSceneAsync("BossArena");
            _boss = Object.FindAnyObjectByType<LionBoss>();
            _player = Object.FindAnyObjectByType<PlayerHealth>();
            _controller = _player.GetComponent<OctopusController>();
            _controller.GetComponent<PlayerInputReader>().enabled = false;
            _combat = _player.GetComponent<CombatController>();
            _body = _player.GetComponent<Rigidbody2D>();
            if (quietBoss)
                _boss.SetAi(false);
            yield return new WaitForFixedUpdate();
        }

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        // Đứng yên trên sàn ở x, quay mặt về phía boss
        IEnumerator StandAt(float x)
        {
            _controller.ResetTo(new Vector2(x, StandY));
            yield return new WaitForSeconds(0.4f);
            float toward = Mathf.Sign(_boss.transform.position.x - x);
            _controller.SetIntent(false, toward);
            yield return new WaitForFixedUpdate();
            yield return null;
            _controller.SetIntent(false, 0f);
            _controller.ResetTo(new Vector2(x, StandY));
            yield return new WaitForSeconds(0.35f);
        }

        static Rock[] Rocks() => Object.FindObjectsByType<Rock>(FindObjectsSortMode.None);

        static float Bottom(Rock rock) => rock.GetComponent<Collider2D>().bounds.min.y;

        // ---------- Đấu trường ----------

        [UnityTest]
        public IEnumerator Arena_WideFloorWallsAndRocksInThreeTiers()
        {
            yield return Load();
            var floor = GameObject.Find("Level/Arena Floor").GetComponent<Collider2D>();
            Assert.That(floor.bounds.max.y, Is.EqualTo(FloorY).Within(0.01f), "Mặt sàn ở y = 0");
            float left = GameObject.Find("Level/Wall Left").GetComponent<Collider2D>().bounds.max.x;
            float right = GameObject.Find("Level/Wall Right").GetComponent<Collider2D>().bounds.min.x;
            Assert.That(right - left, Is.EqualTo(56f).Within(1f), "Sàn đấu rộng ~56 unit giữa hai vách");
            Assert.That(floor.bounds.min.x, Is.LessThan(left), "Sàn chạy suốt tới chân vách");

            Rock[] rocks = Rocks();
            Assert.That(rocks.Length, Is.InRange(12, 14), "12–14 tảng đá treo");
            foreach (float tier in new[] { 5f, 9f, 13f })
            {
                int count = rocks.Count(r => Mathf.Abs(Bottom(r) - FloorY - tier) < 0.6f);
                Assert.That(count, Is.GreaterThanOrEqualTo(3), $"Tầng đá cao ~{tier} unit phải có ít nhất 3 tảng");
            }

            Assert.That(rocks.All(r => r.gameObject.layer == LayerMask.NameToLayer("Anchorable")), "Đá bám được");
            Assert.That(_boss.Health.Max, Is.EqualTo(6000f));
            Assert.That(_player.Max, Is.EqualTo(300f));
        }

        // Đồ thị tầm với: từ sàn với tới mọi tảng đá qua các lần đu (mỗi bước trong tầm xúc tu), và có đường đu toàn đá cao hơn
        // đầu boss đi từ nửa trái sang nửa phải đấu trường
        [UnityTest]
        public IEnumerator Arena_EveryRockReachableBySwinging_PathOverBossHead()
        {
            yield return Load();
            float reach = _controller.Swing.Reach * 0.95f;
            Rock[] rocks = Rocks();
            var colliders = rocks.Select(r => r.GetComponent<Collider2D>()).ToArray();

            bool Links(int a, int b)
            {
                Vector2 hang = new Vector2(colliders[a].bounds.center.x, colliders[a].bounds.min.y - 0.55f);
                return Vector2.Distance(hang, colliders[b].ClosestPoint(hang)) < reach;
            }

            var reached = new HashSet<int>();
            var queue = new Queue<int>();
            for (int i = 0; i < rocks.Length; i++)
            {
                Vector2 below = new Vector2(colliders[i].bounds.center.x, StandY);
                if (Vector2.Distance(below, colliders[i].ClosestPoint(below)) < reach && reached.Add(i))
                    queue.Enqueue(i);
            }

            Assert.That(reached.Count, Is.GreaterThan(0), "Từ sàn phải với tới tầng đá thấp");
            while (queue.Count > 0)
            {
                int a = queue.Dequeue();
                for (int b = 0; b < rocks.Length; b++)
                {
                    if (b != a && Links(a, b) && reached.Add(b))
                        queue.Enqueue(b);
                }
            }

            Assert.That(reached.Count, Is.EqualTo(rocks.Length), "Tảng đá nào cũng đu tới được");

            float headTop = FloorY + _boss.Height + 1f; // treo dưới đá vẫn cao hơn đỉnh đầu boss
            var high = Enumerable.Range(0, rocks.Length).Where(i => colliders[i].bounds.min.y > headTop).ToList();
            var over = new HashSet<int>(high.Where(i => colliders[i].bounds.center.x < -8f));
            var open = new Queue<int>(over);
            while (open.Count > 0)
            {
                int a = open.Dequeue();
                foreach (int b in high)
                {
                    if (!over.Contains(b) && Links(a, b))
                    {
                        over.Add(b);
                        open.Enqueue(b);
                    }
                }
            }

            Assert.IsTrue(over.Any(i => colliders[i].bounds.center.x > 8f), "Có đường đu vòng qua đầu boss (toàn đá cao hơn đầu boss)");
        }

        // Đu thật: từ sàn bắn xúc tu lên tầng thấp, rút lên bám; từ đó bắn lên tầng giữa, rồi tầng cao
        [UnityTest]
        public IEnumerator Player_SwingsUpThroughAllThreeTiers()
        {
            yield return Load();
            Rock[] rocks = Rocks();
            Rock low = rocks.OrderBy(r => Vector2.Distance(r.transform.position, new Vector2(-22f, 5.5f))).First();
            Rock mid = rocks.OrderBy(r => Vector2.Distance(r.transform.position, new Vector2(-16.5f, 9.5f))).First();
            Rock top = rocks.OrderBy(r => Vector2.Distance(r.transform.position, new Vector2(-10.5f, 13.5f))).First();
            yield return StandAt(-22f);

            foreach (Rock rock in new[] { low, mid, top })
            {
                var collider = rock.GetComponent<Collider2D>();
                if (_controller.State == OctopusState.Clinging)
                {
                    // Đang bám dưới đá: bò tới mép phía tảng kế tiếp cho tảng đang bám khỏi chắn đường xúc tu
                    _controller.SetIntent(holding: false, drag: Mathf.Sign(collider.bounds.center.x - _body.position.x));
                    yield return new WaitForSeconds(0.7f);
                    _controller.SetIntent(holding: false, drag: 0f);
                    yield return new WaitForFixedUpdate();
                }

                // Chạm vào phần đá gần người nhất (hơi lùi vào trong đá)
                Vector2 near = collider.ClosestPoint(_body.position);
                Vector2 point = near + ((Vector2)collider.bounds.center - near).normalized * 0.3f;
                Assert.IsTrue(_controller.Grab(point), $"Phải bắn xúc tu tới {rock.name}");
                _controller.SetIntent(holding: true, drag: 0f);
                for (float t = 0f; t < 4f && _controller.State != OctopusState.Clinging; t += Time.deltaTime)
                    yield return null;
                _controller.SetIntent(holding: false, drag: 0f);
                Assert.That(_controller.State, Is.EqualTo(OctopusState.Clinging), $"Giữ tay thì rút lên bám {rock.name}");
                Assert.That(_controller.Mover.ClingSurface, Is.EqualTo(collider));
                yield return new WaitForSeconds(0.2f);
            }

            Assert.That(_body.position.y, Is.GreaterThan(FloorY + 11f), "Đã lên tới tầng cao");
        }

        [UnityTest]
        public IEnumerator Camera_ZoomedOut_ShowsPlayerAndBoss_ShakeStillAttached()
        {
            yield return Load();
            Camera camera = Camera.main;
            camera.aspect = 16f / 9f; // màn điện thoại ngang (batchmode có tỉ lệ khác)
            yield return new WaitForSeconds(2f);
            Assert.That(camera.orthographicSize, Is.GreaterThanOrEqualTo(9.5f), "Camera lùi xa hơn màn thường (6)");
            Vector3 player = camera.WorldToViewportPoint(_body.position);
            Vector3 boss = camera.WorldToViewportPoint(_boss.Body.center);
            Assert.That(player.x, Is.InRange(0.02f, 0.98f), "Thấy người chơi");
            Assert.That(boss.x, Is.InRange(0.02f, 0.98f), "Thấy boss");
            Vector3 floor = camera.WorldToViewportPoint(new Vector3(0f, FloorY, 0f));
            Vector3 high = camera.WorldToViewportPoint(new Vector3(0f, FloorY + 14f, 0f));
            Assert.That(floor.y, Is.InRange(0f, 0.3f), "Thấy mặt sàn");
            Assert.That(high.y, Is.LessThanOrEqualTo(1f), "Thấy cả tầng đá cao");
            Assert.IsNotNull(GameObject.Find("Follow Camera").GetComponent("CameraShake"), "Rung camera vẫn gắn trên Follow Camera");
            camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator Boss_IsAboutFiveTimesThePlayer()
        {
            yield return Load();
            yield return StandAt(-19f);
            Bounds player = default;
            bool any = false;
            foreach (SpriteRenderer r in _player.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!r.enabled || r.sprite == null || r.GetComponentInParent<LaserBeamView>() != null || r.bounds.size.magnitude > 3f)
                    continue; // bỏ quầng sáng lớn quanh người
                if (!any)
                    player = r.bounds;
                else
                    player.Encapsulate(r.bounds);
                any = true;
            }

            float ratio = _boss.Visual.VisualBounds.size.y / player.size.y;
            Assert.That(ratio, Is.InRange(4f, 6f), $"Boss cao gấp ~5 lần bạch tuộc (đang {ratio:0.0})");
        }

        // ---------- Người chơi đánh boss ----------

        [UnityTest]
        public IEnumerator OctopusSkills_DamageBoss()
        {
            yield return Load(PlayableCharacter.Octopus);
            float edge = _boss.Body.min.x;
            yield return StandAt(edge - 1f);
            float start = _boss.Health.Current;
            for (int i = 0; i < 3; i++)
            {
                _combat.Use(0, 0);
                yield return new WaitForSeconds(0.45f);
            }

            Assert.That(_boss.Health.Current, Is.LessThan(start), "Combo quất xúc tu trúng boss");

            // Móc kéo từ xa: chỗ nhắm (gốc Health) dời theo người chơi nên vẫn móc trúng thân boss khổng lồ
            yield return new WaitForSeconds(0.6f);
            yield return StandAt(edge - 4f);
            float before = _boss.Health.Current;
            Assert.IsTrue(_combat.Use(0, 2), "Dùng được Móc kéo");
            yield return new WaitForSeconds(0.6f);
            Assert.That(_boss.Health.Current, Is.LessThan(before), "Móc kéo trúng boss");
            Assert.That(_boss.transform.position.x, Is.EqualTo(12f).Within(0.01f), "Boss không bị kéo");
        }

        [UnityTest]
        public IEnumerator SpiderSkills_DamageBoss()
        {
            yield return Load(PlayableCharacter.Spider);
            yield return StandAt(_boss.Body.min.x - 1f);
            float start = _boss.Health.Current;
            _combat.Use(0, 0);
            yield return new WaitForSeconds(0.5f);
            Assert.That(_boss.Health.Current, Is.LessThan(start), "Cào độc trúng boss");
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(_boss.IsPoisoned || _boss.Health.Current < start - 16f, "Độc ăn đủ trên boss");
        }

        // ---------- Pha, choáng gục, kháng khống chế ----------

        void DamageTo(float fraction)
        {
            Health h = _boss.Health;
            float amount = (h.Current - h.Max * fraction) / h.DamageMultiplier;
            h.TakeHit(amount, _body.position, default);
        }

        [UnityTest]
        public IEnumerator Boss_PhasesSwitchAt60And30Percent()
        {
            yield return Load();
            var phases = new List<int>();
            _boss.PhaseChanged += phases.Add;
            Assert.That(_boss.Phase, Is.EqualTo(1));
            DamageTo(0.61f);
            Assert.That(_boss.Phase, Is.EqualTo(1), "Trên 60% vẫn pha 1");
            DamageTo(0.59f);
            Assert.That(_boss.Phase, Is.EqualTo(2), "Dưới 60% sang pha 2");
            yield return new WaitForSeconds(0.2f);
            DamageTo(0.31f);
            Assert.That(_boss.Phase, Is.EqualTo(2));
            DamageTo(0.29f);
            Assert.That(_boss.Phase, Is.EqualTo(3), "Dưới 30% sang pha 3 (nổi giận)");
            CollectionAssert.AreEqual(new[] { 2, 3 }, phases);

            // Đổi pha thì gầm: bật AI là gầm ngay
            yield return new WaitForSeconds(2.5f); // hết choáng gục do dồn sát thương
            _boss.SetAi(true);
            for (float t = 0f; t < 2f && _boss.Current != LionAttack.Roar; t += Time.deltaTime)
                yield return null;
            Assert.That(_boss.Current, Is.EqualTo(LionAttack.Roar), "Đổi pha thì gầm");
        }

        [UnityTest]
        public IEnumerator Boss_Staggers_After600DamageIn6Seconds_TakesBonusDamage()
        {
            yield return Load();
            Health h = _boss.Health;
            h.TakeHit(300f, _body.position, default);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(_boss.IsStaggered);
            h.TakeHit(310f, _body.position, default);
            Assert.IsTrue(_boss.IsStaggered, "Nhận ≥ 600 trong 6 giây thì choáng gục");
            float dealt = h.TakeHit(100f, _body.position, default);
            Assert.That(dealt, Is.EqualTo(125f).Within(0.01f), "Đang gục nhận thêm 25% sát thương");
            yield return new WaitForSeconds(2.6f);
            Assert.IsFalse(_boss.IsStaggered, "Gục 2 giây rồi đứng dậy");
            Assert.That(h.TakeHit(100f, _body.position, default), Is.EqualTo(100f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Boss_ResistsCrowdControl_ButTakesFullPoison()
        {
            yield return Load();
            Health h = _boss.Health;
            Vector3 home = _boss.transform.position;
            h.TakeHit(1f, _body.position, new HitEffect { knockback = new Vector2(-30f, 15f), stun = 2f });
            Assert.That(_boss.StunRemaining, Is.EqualTo(0.6f).Within(0.02f), "Choáng còn 30%");
            h.TakeHit(1f, _body.position, new HitEffect { snare = 3f });
            Assert.That(_boss.HoldRemaining, Is.LessThanOrEqualTo(0.6f), "Trói tối đa 0.6 giây");
            h.TakeHit(1f, _body.position, new HitEffect { cocoon = true, stun = 1.6f });
            Assert.That(_boss.StunRemaining, Is.LessThanOrEqualTo(0.6f + 1e-3f), "Kén tối đa 0.6 giây");
            yield return new WaitForSeconds(0.3f);
            Assert.That(Vector3.Distance(_boss.transform.position, home), Is.LessThan(0.01f), "Không bị đẩy văng / kéo");

            float before = h.Current;
            h.TakeHit(1f, _body.position, new HitEffect { poisonDps = 20f, poisonTime = 2f });
            yield return new WaitForSeconds(1.2f);
            Assert.That(before - 1f - h.Current, Is.GreaterThanOrEqualTo(19f), "Độc ăn đủ (20/giây)");
        }

        // ---------- Đòn của boss ----------

        IEnumerator ExpectHit(LionAttack attack, float playerX, float within, string message)
        {
            yield return Load();
            yield return StandAt(playerX);
            float start = _player.Current;
            _boss.ForceAttack(attack);
            for (float t = 0f; t < within && _player.Current >= start; t += Time.deltaTime)
                yield return null;
            Assert.That(_player.Current, Is.LessThan(start), message);
        }

        [UnityTest]
        public IEnumerator Boss_ClawSwipe_HitsPlayerInFront()
        {
            yield return ExpectHit(LionAttack.ClawSwipe, 12f - 6.65f - 1.5f, 1.5f, "Vả vuốt trúng người chơi đứng trước mặt");
            Assert.That(_player.Current, Is.EqualTo(270f).Within(0.01f), "Mỗi nhát 30");
            Assert.That(_body.linearVelocity.x, Is.LessThan(0f), "Bị đẩy văng ra xa boss");
        }

        [UnityTest]
        public IEnumerator Boss_Pounce_TelegraphsLandingRing_ThenHits()
        {
            yield return Load();
            yield return StandAt(-8f);
            float start = _player.Current;
            _boss.ForceAttack(LionAttack.Pounce);
            yield return new WaitForSeconds(0.4f);
            Assert.That(_boss.Hazards.MarkerCount, Is.EqualTo(1), "Vòng đỏ dưới chỗ sắp đáp");
            Assert.That(_boss.Hazards.Markers[0].Position.x, Is.EqualTo(-8f).Within(0.5f));
            for (float t = 0f; t < 2f && _player.Current >= start; t += Time.deltaTime)
                yield return null;
            Assert.That(_player.Current, Is.LessThan(start), "Vồ trúng người chơi");
            yield return new WaitForSeconds(0.6f);
            Assert.That(_boss.Body.center.x, Is.EqualTo(-8f).Within(1f), "Thân boss đáp đè lên chỗ người chơi");
            Assert.That(_boss.transform.position.y, Is.EqualTo(FloorY).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Boss_Roar_HitsAndPushesAway()
        {
            yield return ExpectHit(LionAttack.Roar, 0f, 1.5f, "Gầm trúng người chơi trong tầm 14");
            Assert.That(_player.Current, Is.EqualTo(285f).Within(0.01f), "Gầm 15");
            Assert.That(_body.linearVelocity.x, Is.LessThan(0f), "Bị đẩy ra xa");
        }

        [UnityTest]
        public IEnumerator Boss_Roar_OutOfRange_Misses()
        {
            yield return Load();
            yield return StandAt(-24f);
            _boss.ForceAttack(LionAttack.Roar);
            yield return new WaitForSeconds(1.6f);
            Assert.That(_player.Current, Is.EqualTo(300f), "Ra khỏi tầm gầm thì không trúng");
        }

        [UnityTest]
        public IEnumerator Boss_Fireballs_HitPlayer() =>
            ExpectHit(LionAttack.Fireballs, -2f, 3f, "Cầu lửa trúng người chơi");

        [UnityTest]
        public IEnumerator Boss_Charge_LaunchesPlayer_StunnedOnWall()
        {
            yield return ExpectHit(LionAttack.Charge, -8f, 3f, "Húc lao trúng người chơi");
            Assert.That(_player.Current, Is.EqualTo(255f).Within(0.01f), "Húc 45");
            Assert.That(_body.linearVelocity.y, Is.GreaterThan(3f), "Bị hất tung");
            for (float t = 0f; t < 3f && !_boss.IsStunned; t += Time.deltaTime)
                yield return null;
            Assert.IsTrue(_boss.IsStunned, "Đâm vách thì choáng");
            Assert.That(_boss.Body.min.x, Is.LessThan(-26f), "Lao hết đấu trường tới vách trái");
        }

        [UnityTest]
        public IEnumerator Boss_FireRain_MarksEightSpots_MeteorHitsPlayer()
        {
            yield return Load();
            yield return StandAt(-8.5f); // không có đá phía trên che
            float start = _player.Current;
            _boss.ForceAttack(LionAttack.FireRain);
            for (float t = 0f; t < 1.5f && _boss.Hazards.MarkerCount < 8; t += Time.deltaTime)
                yield return null;
            Assert.That(_boss.Hazards.MarkerCount, Is.GreaterThanOrEqualTo(8), "8 vòng đỏ đánh dấu");
            Assert.IsTrue(_boss.Hazards.Markers.Any(m => Mathf.Abs(m.Position.x + 8.5f) < 0.3f), "Có vòng ngay dưới chân người chơi");
            for (float t = 0f; t < 2.5f && _player.Current >= start; t += Time.deltaTime)
                yield return null;
            Assert.That(_player.Current, Is.EqualTo(start - 30f).Within(0.01f), "Thiên thạch trúng 30");
        }

        // ---------- Máu người chơi ----------

        [UnityTest]
        public IEnumerator Player_Hit_InvulnerableBlinksKnockedBack_GainsRage()
        {
            yield return Load();
            yield return StandAt(-10f);
            RageMeter rage = _combat.Rage;
            float rageBefore = rage.Value;
            Vector2 from = _boss.transform.position;
            Assert.IsTrue(_player.Hit(30f, from, new Vector2(6f, 5f)));
            Assert.That(_player.Current, Is.EqualTo(270f));
            Assert.That(rage.Value - rageBefore, Is.EqualTo(15f).Within(0.01f), "Nộ +0.5 cho mỗi máu mất");
            Assert.That(_body.linearVelocity.x, Is.LessThan(0f), "Bị đẩy ra xa boss");
            Assert.IsTrue(_player.IsInvulnerable);
            Assert.IsFalse(_player.Hit(30f, from, new Vector2(6f, 5f)), "Đang bất tử thì không trúng");
            Assert.That(_player.Current, Is.EqualTo(270f));

            var renderers = _player.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.GetComponentInParent<LaserBeamView>() == null).ToList();
            bool hidden = false, shown = false;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                hidden |= renderers.Any(r => r.forceRenderingOff);
                shown |= renderers.All(r => !r.forceRenderingOff);
                yield return null;
            }

            Assert.IsTrue(hidden && shown, "Nhấp nháy trong lúc bất tử");
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(_player.IsInvulnerable, "Hết 0.8 giây thì hết bất tử");
            Assert.IsTrue(renderers.All(r => !r.forceRenderingOff), "Hết nhấp nháy");
            Assert.IsTrue(_player.Hit(30f, from, new Vector2(6f, 5f)));
            Assert.That(_player.Current, Is.EqualTo(240f));
        }

        [UnityTest]
        public IEnumerator PlayerDeath_ShowsDefeatOverlay_RetryReloadsArena()
        {
            yield return Load(quietBoss: false);
            var overlay = Object.FindAnyObjectByType<ResultOverlay>();
            Assert.IsFalse(overlay.IsShowing);
            _player.Hit(999f, _boss.transform.position, Vector2.zero);
            Assert.IsTrue(_player.IsDead);
            Assert.IsFalse(_combat.enabled, "Gục thì thôi dùng chiêu");
            for (float t = 0f; t < 3f && !overlay.IsShowing; t += Time.deltaTime)
                yield return null;
            Assert.IsTrue(overlay.IsShowing, "Hiện màn thua");
            Assert.IsFalse(overlay.IsVictory);
            Assert.That(overlay.Title, Is.EqualTo("THẤT BẠI"));
            Assert.That(overlay.RetryButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("Thử lại"));
            Assert.That(overlay.MenuButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("Đổi nhân vật"));
            Assert.That(_boss.Current, Is.EqualTo(LionAttack.None), "Boss thôi đánh");

            overlay.RetryButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BossArena"));
            Assert.That(Object.FindAnyObjectByType<PlayerHealth>().Current, Is.EqualTo(300f), "Thử lại: đầy máu");
            Object.FindAnyObjectByType<ResultOverlay>().ChangeCharacter();
            for (float t = 0f; t < 3f && SceneManager.GetActiveScene().name != "CharacterSelect"; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterSelect"), "Đổi nhân vật: về màn chọn");
        }

        [UnityTest]
        public IEnumerator BossDeath_ShowsVictoryOverlayWithFightTime()
        {
            yield return Load();
            for (float t = 0f; t < 4f && !_boss.Engaged; t += Time.deltaTime)
                yield return null;
            Assert.IsTrue(_boss.Engaged, "Boss gầm mở màn");
            var overlay = Object.FindAnyObjectByType<ResultOverlay>();
            yield return new WaitForSeconds(0.5f);
            _boss.Health.TakeHit(99999f, _body.position, default);
            Assert.IsTrue(_boss.IsDead);
            Assert.IsFalse(_boss.Health.GetComponent<Collider2D>().enabled, "Xác boss không nhận đòn");
            for (float t = 0f; t < 4f && !overlay.IsShowing; t += Time.deltaTime)
                yield return null;
            Assert.IsTrue(overlay.IsShowing, "Hiện màn thắng");
            Assert.IsTrue(overlay.IsVictory);
            Assert.That(overlay.Title, Is.EqualTo("CHIẾN THẮNG"));
            StringAssert.Contains("0:0", overlay.Info, "Kèm thời gian đánh");
            Assert.That(_boss.FightTime, Is.GreaterThan(0.3f));
            Assert.That(overlay.RetryButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("Đánh lại"));
            Assert.That(overlay.MenuButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("Về màn chọn"));
        }

        // ---------- Giao diện, vào màn ----------

        [UnityTest]
        public IEnumerator Hud_BossBarTopCenter_PlayerHealthUnderRageBar()
        {
            yield return Load();
            yield return null;
            Rect boss = ScreenRect(Object.FindAnyObjectByType<BossBar>().transform);
            Rect rage = ScreenRect(Object.FindAnyObjectByType<RageBar>().transform);
            Rect health = ScreenRect(Object.FindAnyObjectByType<PlayerHealthBar>().transform);
            Rect back = ScreenRect(Object.FindAnyObjectByType<SceneButton>().transform);

            Assert.That(boss.center.x, Is.EqualTo(Screen.width * 0.5f).Within(Screen.width * 0.03f), "Thanh máu boss ở giữa");
            Assert.That(boss.center.y, Is.GreaterThan(Screen.height * 0.6f), "Thanh máu boss phía trên");
            Assert.That(health.center.x, Is.LessThan(Screen.width * 0.4f), "Thanh máu người chơi bên trái");
            Assert.That(health.yMax, Is.LessThanOrEqualTo(rage.yMin + 1f), "Ngay dưới thanh nộ");
            Assert.That(rage.yMin - health.yMax, Is.LessThan(Screen.height * 0.05f), "Sát dưới thanh nộ");
            Assert.IsFalse(boss.Overlaps(rage) || boss.Overlaps(health) || boss.Overlaps(back), "Thanh máu boss không đè lên HUD khác");
            Assert.IsNotNull(Object.FindAnyObjectByType<SkillPad>(), "Có cụm nút chiêu");
        }

        static Rect ScreenRect(Transform t)
        {
            var corners = new Vector3[4];
            ((RectTransform)t).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator CharacterSelect_BossButton_OpensArenaWithChosenCharacter()
        {
            CharacterSelection.Clear();
            yield return SceneManager.LoadSceneAsync("CharacterSelect");
            yield return null;
            var menu = Object.FindAnyObjectByType<CharacterSelectMenu>();
            menu.Select(PlayableCharacter.Spider);
            menu.StartBoss();
            for (float t = 0f; t < 3f && SceneManager.GetActiveScene().name != "BossArena"; t += Time.unscaledDeltaTime)
                yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BossArena"), "Bấm Đánh boss thì vào màn boss");
            Assert.That(Object.FindAnyObjectByType<PlayerCharacter>().Current, Is.EqualTo(PlayableCharacter.Spider));
            Assert.IsNotNull(Object.FindAnyObjectByType<LionBoss>());
        }

        // Tầng đá cao ngoài tầm vồ / cầu lửa / vuốt: boss đi tới dưới chân, người chơi ở trên cao quá 6 giây thì gầm (với tới)
        [UnityTest]
        public IEnumerator Boss_PlayerCampsOnHighTier_OnlyRoarReaches()
        {
            yield return Load(quietBoss: false);
            Rock high = Rocks().OrderBy(r => Vector2.Distance(r.transform.position, new Vector2(-3f, 13.5f))).First();
            Bounds top = high.GetComponent<Collider2D>().bounds;
            _controller.ResetTo(new Vector2(top.center.x, top.max.y + 0.6f)); // đứng trên mặt tảng đá cao
            for (float t = 0f; t < 12f && _boss.AttackCount(LionAttack.Roar) == 0; t += Time.deltaTime)
                yield return null;
            Assert.That(_boss.AttackCount(LionAttack.Roar), Is.GreaterThanOrEqualTo(1), "Ở trên cao lâu thì boss gầm");
            Assert.That(_boss.AttackCount(LionAttack.Pounce) + _boss.AttackCount(LionAttack.Fireballs) + _boss.AttackCount(LionAttack.ClawSwipe),
                Is.EqualTo(0), "Vồ, cầu lửa, vuốt không với tới tầng cao");
            Assert.That(Mathf.Abs(_boss.transform.position.x - top.center.x), Is.LessThan(8f), "Boss đi tới dưới chân người chơi");
            for (float t = 0f; t < 2f && _player.Current >= 300f; t += Time.deltaTime)
                yield return null;
            Assert.That(_player.Current, Is.EqualTo(285f).Within(0.01f), "Gầm với tới tầng cao");
        }

        // AI tự chạy: sau khi gầm mở màn thì tự ra đòn đánh người chơi đứng dưới sàn
        [UnityTest]
        public IEnumerator Boss_Ai_AttacksOnItsOwn()
        {
            yield return Load(quietBoss: false);
            yield return StandAt(-2f);
            for (float t = 0f; t < 8f && _player.Current >= 300f; t += Time.deltaTime)
                yield return null;
            Assert.That(_player.Current, Is.LessThan(300f), "Boss tự đi tới và đánh");
            int attacks = System.Enum.GetValues(typeof(LionAttack)).Cast<LionAttack>().Sum(a => a == LionAttack.None ? 0 : _boss.AttackCount(a));
            Assert.That(attacks, Is.GreaterThanOrEqualTo(1));
        }
    }
}
