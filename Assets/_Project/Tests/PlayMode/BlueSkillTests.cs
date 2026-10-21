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
    /// 9 chiêu của BLUE làm đúng việc của tấm chiêu tương ứng: combo 3 nhát, hất tung (mục tiêu và BLUE cùng bay lên), lướt
    /// chém trên đường, xoáy lửa đánh quanh mình rồi quét tới, sóng lưỡi liềm bay xa rồi nổ, thế thủ chặn đòn + đốt kẻ địch
    /// sát bên, bay lên xoáy rồi giáng xuống nổ, luồng lửa chạy dọc đất đánh mọi thứ trên đường, xích trói + lồng giam (choáng
    /// lâu, mất máu liên tục; boss sư tử bị giam ngắn hơn). Ảnh hiệu ứng cắt từ tấm chiêu nạp được. Màn đánh boss: cả 9 chiêu
    /// đánh trúng boss sư tử.
    /// Cỡ / tầm chiêu của BLUE (mặc định ×2) lấy từ CombatController.ReachOf: vị trí đứng / ngưỡng tính theo nó nên đúng với
    /// mọi hệ số (thử riêng ×1 / ×2: ReachBlueTests).
    /// Màn hang động: mặt bệ xuất phát y = -1 (x -5..5), bia tập ở x = 3.7 và -3.9. Màn đánh boss: sàn y = 0, boss ở x = 12
    /// (thân rộng ~10.5: mép trái ~6.8).
    /// </summary>
    public class BlueSkillTests
    {
        const float PlaygroundFloor = -1f;
        const float ArenaFloor = 0f;
        const float DummyX = 3.7f;
        const float OtherDummyX = -3.9f;

        static readonly string[] Names =
        {
            "Đánh thường", "Đánh thường hất tung", "Đánh thường áp sát",
            "Dùng chiêu", "Dùng chiêu tầm xa", "Dùng chiêu thủ",
            "Kĩ năng đặc biệt", "Kĩ năng đặc biệt tầm xa", "Kĩ năng đặc biệt áp chế",
        };

        // Mỗi chiêu có bộ hình cắt từ chính tấm của nó (không mượn hình chiêu khác), tách lớp trước / sau người
        static readonly string[][] FxSprites =
        {
            new[] { BlueFx.ReapArcA, BlueFx.ReapArcB, BlueFx.ReapTip, BlueFx.Ember1, BlueFx.Ember2, BlueFx.Ember3 },
            new[] { BlueFx.RiseLow, BlueFx.RiseArc1, BlueFx.Rise, BlueFx.Pillar, BlueFx.PillarBeam },
            new[] { BlueFx.DashKick, BlueFx.DashStreak, BlueFx.DashArc, BlueFx.DashDust },
            new[]
            {
                BlueFx.WhirlFlameL, BlueFx.WhirlFlameR, BlueFx.WhirlRing3Front, BlueFx.WhirlRing3Back, BlueFx.WhirlRing4Front,
                BlueFx.WhirlRing4Back, BlueFx.WhirlFinish,
            },
            new[] { BlueFx.Circle, BlueFx.WaveSweep, BlueFx.Wave, BlueFx.Burst },
            new[]
            {
                BlueFx.SealFront, BlueFx.SealBack, BlueFx.GuardSwirlFront, BlueFx.GuardSwirlBack, BlueFx.GuardFlameL, BlueFx.GuardFlameR,
                BlueFx.Skull, BlueFx.SkullA, BlueFx.SkullB, BlueFx.SkullC, BlueFx.SkullD, BlueFx.CrystalA, BlueFx.CrystalB,
            },
            new[]
            {
                BlueFx.StormFlameL, BlueFx.StormFlameR, BlueFx.StormRim, BlueFx.StormSwirlFront, BlueFx.StormSwirlBack, BlueFx.StormPillarL,
                BlueFx.StormPillarR, BlueFx.StormBeam, BlueFx.StormCrack,
            },
            new[]
            {
                BlueFx.Vortex, BlueFx.GroundWave, BlueFx.GroundWaveB, BlueFx.GroundWaveC, BlueFx.TideHead, BlueFx.TideStart, BlueFx.TideSmokeA,
                BlueFx.TideSmokeB, BlueFx.TideRocks, BlueFx.Eruption,
            },
            new[] { BlueFx.Chain, BlueFx.ChainWrapFront, BlueFx.ChainWrapBack, BlueFx.CageFront, BlueFx.CageBack },
        };

        OctopusController _player;
        HeroMotor _hero;
        CombatController _combat;
        Rigidbody2D _body;
        LionBoss _boss;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        IEnumerator Load(string scene = "Playground")
        {
            CharacterSelection.Choose(PlayableCharacter.Blue);
            yield return SceneManager.LoadSceneAsync(scene);
            _player = Object.FindAnyObjectByType<OctopusController>();
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _hero = _player.Hero;
            _combat = _player.GetComponent<CombatController>();
            _body = _player.GetComponent<Rigidbody2D>();
            _boss = Object.FindAnyObjectByType<LionBoss>();
            if (_boss != null)
                _boss.SetAi(false);
            yield return new WaitForFixedUpdate();
        }

        float Feet => _body.position.y - HeroMotor.FeetOffset;

        /// <summary>Cỡ / tầm chiêu của BLUE đang đặt trong Combat Controller.</summary>
        SkillReach Reach => _combat.ReachOf(PlayableCharacter.Blue);

        IEnumerator StandAt(float x, float floorY, float facing = 1f)
        {
            _player.ReleaseJump();
            _player.SetAim(0);
            _player.SetIntent(false, facing);
            yield return new WaitForFixedUpdate();
            yield return null;
            _player.SetIntent(false, 0f);
            _player.ResetTo(new Vector2(x, floorY + HeroMotor.FeetOffset + 0.05f));
            for (float t = 0f; t < 1.5f && !_hero.Grounded; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(_hero.Grounded, "Phải đứng trên sàn");
            Assert.That(_player.Facing, Is.EqualTo(Mathf.Sign(facing)));
        }

        static Health DummyAt(float x) =>
            Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Where(d => Mathf.Abs(d.transform.position.x - x) < 0.5f)
                .Select(d => d.GetComponent<Health>()).First();

        // Ghi mọi lần mất máu của mục tiêu: lượng, hiệu ứng, lúc nào
        class Hits
        {
            public readonly List<(float amount, HitEffect effect, float time)> List = new List<(float, HitEffect, float)>();
            public float Total => List.Sum(h => h.amount);
            public int Count => List.Count;

            public Hits(Health target) => target.Damaged += (amount, from, effect) => List.Add((amount, effect, Time.time));
        }

        IEnumerator WaitIdle(float max = 8f)
        {
            for (float t = 0f; t < max && _combat.Busy; t += Time.deltaTime)
                yield return null;
            Assert.IsFalse(_combat.Busy, "Chiêu chạy quá lâu");
        }

        void ClearCooldowns()
        {
            var field = typeof(CombatController).GetField("_cooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cooldowns = (float[])field.GetValue(_combat);
            System.Array.Clear(cooldowns, 0, cooldowns.Length);
        }

        // ---------- Bộ chiêu, ảnh hiệu ứng ----------

        [UnityTest]
        public IEnumerator Defaults_NineSkillsNamedLikeTheSheets_FxArtLoads()
        {
            yield return Load();
            Skill[] set = _combat.SkillsOf(PlayableCharacter.Blue);
            Assert.That(set.Length, Is.EqualTo(9));
            for (int i = 0; i < 9; i++)
            {
                Assert.That(set[i].name, Is.EqualTo(Names[i]), $"Chiêu {i}: tên theo file ảnh");
                Assert.IsFalse(set[i] is HeroPlaceholderSkill, $"{Names[i]}: chiêu thật, không phải chiêu tạm");
                Assert.That(set[i].GetType().Name, Does.StartWith("Blue"));
            }

            Skill[] code = BlueSkills.Default();
            for (int i = 0; i < 9; i++)
                Assert.That(set[i].GetType(), Is.EqualTo(code[i].GetType()), "Màn chơi dựng từ BlueSkills.Default()");
            Assert.That(set[0].cooldown, Is.EqualTo(0f), "Đánh thường không hồi chiêu");
            for (int i = 3; i < 6; i++)
                Assert.That(set[i].cooldown, Is.GreaterThan(1f), $"{Names[i]} có hồi chiêu");

            foreach (string name in FxSprites.SelectMany(x => x))
            {
                Sprite s = BlueFx.SpriteOf(name);
                Assert.IsNotNull(s, $"Thiếu ảnh hiệu ứng Resources/HeroFx/Blue/{name}");
                Assert.That(s.pixelsPerUnit, Is.EqualTo(BlueFx.PixelsPerUnit).Within(0.01f), $"{name}: 134 px = 1 unit");
                Assert.That(Mathf.Max(s.rect.width, s.rect.height), Is.LessThanOrEqualTo(1024f), $"{name}: vừa atlas");
            }

            // Vệt chém có điểm mốc ngực (= điểm neo) và mũi lưỡi hái để đặt theo lưỡi hái thật
            foreach (string arc in new[] { BlueFx.ReapArcA, BlueFx.ReapArcB, BlueFx.DashArc, BlueFx.WhirlFinish, BlueFx.WaveSweep })
            {
                Assert.That(BlueFx.PointOf(arc, "tip").magnitude, Is.GreaterThan(0.5f), $"{arc}: mũi lưỡi hái cách ngực");
                Assert.That(BlueFx.PointOf(arc, "chest").magnitude, Is.LessThan(0.01f), $"{arc}: điểm neo ở ngực");
            }

            // Lớp lõi trắng của các vệt / vụ nổ chính có sẵn (vẽ cộng sáng riêng)
            foreach (string main in new[] { BlueFx.ReapArcA, BlueFx.Rise, BlueFx.Wave, BlueFx.Burst, BlueFx.Eruption })
                Assert.IsNotNull(BlueFx.CoreOf(main), $"{main}: có lớp lõi trắng _Core");

            Assert.IsNotNull(Resources.Load<Material>("HeroFx/Blue/BlueFxAlpha"));
            Assert.IsNotNull(Resources.Load<Material>("HeroFx/Blue/BlueFxAdd"));
        }

        [UnityTest]
        public IEnumerator EachSkill_UsesOnlyPiecesCutFromItsOwnSheet()
        {
            yield return Load();
            for (int i = 0; i < 9; i++)
            {
                var allowed = new HashSet<string>(FxSprites[i]);
                foreach (string n in FxSprites[i])
                    allowed.Add(n + "_Core");
                ClearCooldowns();
                yield return StandAt(DummyX - 2.4f, PlaygroundFloor);
                DummyAt(DummyX).Restore();
                if (i >= 6)
                    _combat.Rage.Add(RageMeter.PerTier);
                var seen = new HashSet<string>();
                Assert.IsTrue(_combat.Use(i / 3, i % 3), Names[i]);
                for (float t = 0f; t < 5f; t += Time.deltaTime)
                {
                    int active = ActiveBlueFx(seen);
                    if (t > 1.2f && !_combat.Busy && active == 0)
                        break;
                    yield return null;
                }

                Assert.That(seen.Count, Is.GreaterThanOrEqualTo(3), $"{Names[i]}: hiệu ứng nhiều lớp ({string.Join(", ", seen)})");
                var foreign = seen.Where(n => !allowed.Contains(n)).ToList();
                Assert.That(foreign, Is.Empty, $"{Names[i]} chỉ dùng hình cắt từ tấm của nó, không mượn: {string.Join(", ", foreign)}");
                yield return new WaitForSeconds(0.3f);
            }
        }

        // Số ảnh hiệu ứng BLUE đang hiện; ghi tên ảnh vào seen
        static int ActiveBlueFx(HashSet<string> seen)
        {
            int n = 0;
            foreach (SpriteRenderer r in BlueFx.Instance.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!r.enabled || r.sprite == null)
                    continue;
                seen.Add(r.sprite.name);
                n++;
            }

            return n;
        }

        // ---------- Nút 1 ----------

        [UnityTest]
        public IEnumerator BasicAttack_ThreeSweepCombo_FinisherHeaviest()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.7f, PlaygroundFloor);
            var hits = new Hits(DummyAt(DummyX));
            Assert.IsTrue(_combat.Use(0, 0));
            yield return null;
            Assert.IsTrue(_combat.Use(0, 0), "Bấm trong lúc nhát trước chưa xong: xếp hàng nối combo");
            Assert.IsTrue(_combat.Use(0, 0));
            yield return WaitIdle();
            yield return new WaitForSeconds(0.2f);
            Assert.That(hits.Count, Is.EqualTo(3), "3 nhát quét đều trúng");
            Assert.That(hits.List[2].amount, Is.GreaterThan(hits.List[0].amount), "Nhát 3 nặng nhất");
            Assert.IsTrue(hits.List[2].effect.heavy);

            // Quá thời gian nối combo thì về lại nhát 1
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(_combat.Use(0, 0));
            yield return WaitIdle();
            Assert.That(hits.List[3].amount, Is.EqualTo(hits.List[0].amount).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator RisingReap_LaunchesTargetUp_AndBlueRisesWithIt()
        {
            yield return Load();
            yield return StandAt(DummyX - 1.5f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            var hits = new Hits(dummy);
            float floorFeet = Feet, top = Feet, lift = 0f;
            Assert.IsTrue(_combat.Use(0, 1));
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                top = Mathf.Max(top, Feet);
                lift = Mathf.Max(lift, view.Displacement.y);
                yield return null;
            }

            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(1), "Nhát hất trúng bia");
            Assert.That(hits.List[0].effect.knockback.y, Is.GreaterThan(8f), "Hất mục tiêu bay lên");
            Assert.That(lift, Is.GreaterThan(0.3f), "Bia bị hất lên");
            Assert.That(top - floorFeet, Is.GreaterThan(1.5f), "BLUE bật lên theo trong cột lửa");
            for (float t = 0f; t < 2f && !_hero.Grounded; t += Time.deltaTime)
                yield return null;
            Assert.IsTrue(_hero.Grounded, "Rồi đáp đất");
        }

        [UnityTest]
        public IEnumerator ReaperDash_DashesForward_HitsOnThePath()
        {
            yield return Load();
            float distance = ((BlueReaperDash)_combat.Get(0, 2)).distance * Reach.range;
            // Đứng sao cho cuối đường lướt ở ngay bia: bia vừa trên đường lướt vừa trong nhát quét cuối đường
            yield return StandAt(DummyX - distance, PlaygroundFloor);
            var hits = new Hits(DummyAt(DummyX));
            float x0 = _body.position.x;
            Assert.IsTrue(_combat.Use(0, 2));
            bool dashed = false;
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                dashed |= _hero.IsDashing;
                yield return null;
            }

            Assert.IsTrue(dashed, "Lướt tới");
            Assert.That(_body.position.x - x0, Is.GreaterThan(distance * 0.85f), $"Lướt xa ~{distance:0.0} unit (3.6 × tầm chiêu)");
            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(1), "Chém trúng bia trên đường lướt");
            Assert.IsTrue(hits.List.Any(h => h.effect.heavy), "Nhát quét lưỡi liềm ở cuối đường");
            Assert.That(_combat.CooldownRemaining(0, 2), Is.GreaterThan(0f));
        }

        // ---------- Nút 2 ----------

        [UnityTest]
        public IEnumerator SoulWhirl_HitsAllAround_ThenStrongForwardSweep()
        {
            yield return Load();
            // Bia ở SAU lưng: vòng lửa quanh thân vẫn trúng; bia trước mặt trúng cả nhát quét tới
            yield return StandAt(OtherDummyX + 1.3f, PlaygroundFloor);
            var behind = new Hits(DummyAt(OtherDummyX));
            Assert.IsTrue(_combat.Use(1, 0));
            yield return WaitIdle();
            Assert.That(behind.Count, Is.GreaterThanOrEqualTo(2), "Vòng lửa quanh thân đánh vài nhát cả phía sau");
            Assert.IsFalse(behind.List.Any(h => h.effect.heavy), "Nhát quét mạnh chỉ đánh phía trước");

            ClearCooldowns();
            yield return StandAt(DummyX - 1.5f, PlaygroundFloor);
            var front = new Hits(DummyAt(DummyX));
            Assert.IsTrue(_combat.Use(1, 0));
            yield return WaitIdle();
            Assert.IsTrue(front.List.Any(h => h.effect.heavy && h.amount >= 30f), "Quét mạnh tới trước");
            Assert.That(front.Count, Is.GreaterThanOrEqualTo(3));
        }

        [UnityTest]
        public IEnumerator CrescentWave_FliesFar_ExplodesOnTheTarget()
        {
            // Màn đánh boss (sàn rộng): boss trước mặt cách 11 unit, trong tầm bay ở cả ×1 (10 unit) lẫn ×2; sóng to ×2 hiện ra xa
            // hơn nên bia tập trên bệ xuất phát quá gần để thấy sóng bay
            yield return Load("BossArena");
            yield return new WaitForSeconds(1f); // khung đánh của boss bám theo dáng đứng: chờ nó vào chỗ rồi mới đo mép
            float edge = BlueKitTestBounds(_boss.Health).min.x;
            yield return StandAt(edge - 11f, ArenaFloor);
            float x0 = _body.position.x;
            var hits = new Hits(_boss.Health);
            float start = Time.time;
            Assert.IsTrue(_combat.Use(1, 1));
            for (float t = 0f; t < 3f && hits.Count == 0; t += Time.deltaTime)
                yield return null;
            Assert.That(hits.Count, Is.EqualTo(1), "Sóng lưỡi liềm bay tới trúng boss");
            float travel = hits.List[0].time - start;
            Assert.That(travel, Is.GreaterThan(0.75f), $"Phải bay một lúc mới tới (trúng sau {travel:0.00} s)");
            Assert.IsTrue(hits.List[0].effect.heavy, "Nổ tinh thể");
            Assert.That(_body.position.x, Is.EqualTo(x0).Within(0.3f), "Đứng tại chỗ phóng");
        }

        [UnityTest]
        public IEnumerator SoulGuard_BlocksBossHits_BurnsEnemiesAtItsSide()
        {
            yield return Load("BossArena");
            var health = _player.GetComponent<PlayerHealth>();
            float edge = BlueKitTestBounds(_boss.Health).min.x;
            yield return StandAt(edge - 1f, ArenaFloor);
            float lion = _boss.Health.Current;
            float x0 = _body.position.x;
            Assert.IsTrue(_combat.Use(1, 2));
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(health.IsBlocking, "Đang thế thủ: chặn đòn");
            Assert.IsTrue(health.Hit(60f, _body.position + Vector2.right * 2f, new Vector2(6f, 4f), true));
            Assert.That(health.Current, Is.EqualTo(health.Max), "Đòn bị chặn hết");
            yield return new WaitForSeconds(1f);
            Assert.That(_boss.Health.Current, Is.LessThan(lion), "Lửa đốt boss đứng sát bên");
            Assert.That(_body.position.x, Is.EqualTo(x0).Within(0.2f), "Đứng yên giữ thế thủ");
            yield return WaitIdle();
            yield return new WaitForSeconds(0.1f);
            Assert.IsFalse(health.IsBlocking, "Hết thế thủ thì thôi chặn");
            Assert.That(_combat.CooldownRemaining(1, 2), Is.GreaterThan(1f));
        }

        // ---------- Nút 3 (nộ) ----------

        [UnityTest]
        public IEnumerator ReaperStorm_RisesInVortex_CrashesWithBigAoE()
        {
            yield return Load();
            // Bia sau lưng phải ngoài tầm nổ (bán kính × cỡ chiêu): đứng cách mép bia đó hơn bán kính nổ, vẫn ngay trước bia kia
            float blast = ((BlueReaperStorm)_combat.Get(2, 0)).radius * Reach.size;
            float x = Mathf.Max(DummyX - 1.5f, OtherDummyX + 0.5f + blast + 0.5f);
            yield return StandAt(x, PlaygroundFloor);
            var hits = new Hits(DummyAt(DummyX));
            var other = new Hits(DummyAt(OtherDummyX));
            _combat.Rage.Add(RageMeter.PerTier);
            float floorFeet = Feet, top = Feet;
            Assert.IsTrue(_combat.Use(2, 0));
            Assert.That(_combat.Rage.Value, Is.LessThan(1f), "Tốn nộ");
            while (_combat.Busy)
            {
                top = Mathf.Max(top, Feet);
                yield return null;
            }

            Assert.That(top - floorFeet, Is.GreaterThan(2f), "Bay lên trong vòng xoáy");
            Assert.IsTrue(_hero.Grounded, "Giáng xuống đất");
            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(2), "Vòng xoáy + nổ đều trúng");
            Assert.IsTrue(hits.List.Any(h => h.effect.heavy && h.amount >= 70f && h.effect.stun > 0f), "Nổ cột tinh thể: nặng, choáng");
            Assert.That(other.Count, Is.EqualTo(0), $"Bia sau lưng ngoài tầm nổ ({blast:0.0} unit)");

            // Mốc nộ cao hơn đau hơn
            yield return StandAt(x, PlaygroundFloor);
            DummyAt(DummyX).Restore();
            int before = hits.Count;
            _combat.Rage.Add(RageMeter.PerTier * 3f);
            Assert.IsTrue(_combat.Use(2, 0));
            yield return WaitIdle();
            float tier1 = hits.List.Take(before).Max(h => h.amount);
            float tier3 = hits.List.Skip(before).Max(h => h.amount);
            Assert.That(tier3, Is.GreaterThan(tier1 * 2f), "Mốc nộ 3 mạnh hơn hẳn");
        }

        [UnityTest]
        public IEnumerator SoulTide_GroundWaveRunsAlongTheGround_HitsEachTargetOnce_EruptsAtTheLedge()
        {
            yield return Load();
            const float x0 = -2.6f;
            yield return StandAt(x0, PlaygroundFloor);
            var tide = (BlueSoulTide)_combat.Get(2, 1);
            var hits = new Hits(DummyAt(DummyX));
            var behind = new Hits(DummyAt(OtherDummyX));
            _combat.Rage.Add(RageMeter.PerTier);
            float start = Time.time;
            Assert.IsTrue(_combat.Use(2, 1));
            Sprite erupt = BlueFx.SpriteOf(BlueFx.Eruption);
            float eruptX = float.NaN;
            for (float t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                SpriteRenderer r = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).FirstOrDefault(x => x.enabled && x.sprite == erupt);
                if (r != null && float.IsNaN(eruptX))
                    eruptX = r.bounds.center.x;
                yield return null;
            }

            var wave = hits.List.Where(h => h.effect.stun > 0f).ToList();
            Assert.That(wave.Count, Is.EqualTo(1), "Luồng lửa đi qua bia: trúng đúng một lần");
            // Vận chiêu ~0.7 giây, rồi luồng lửa xuất phát trước mặt 0.4 unit × tầm, chạy speed × tầm unit/giây tới mép bia
            float run = (DummyX - 0.5f) - (x0 + 0.4f * Reach.range);
            float travel = run / (tide.speed * Reach.range);
            Assert.That(wave[0].time - start, Is.GreaterThan(0.7f + travel * 0.5f),
                $"Luồng lửa chạy dọc đất một lúc mới tới bia cách ~6 unit (~{travel:0.00} s)");
            Assert.That(wave[0].amount, Is.GreaterThanOrEqualTo(55f));
            Assert.That(behind.Count, Is.EqualTo(0), "Không đánh sau lưng");
            // Bệ xuất phát hết ở x = 5: luồng lửa chạy dọc mặt bệ, tới mép thì dừng, nổ tinh thể ở đó (không chạy giữa không trung)
            Assert.IsFalse(float.IsNaN(eruptX), "Nổ tinh thể ở cuối luồng lửa");
            Assert.That(eruptX, Is.InRange(4.2f, 5.6f), "Dừng ở mép bệ");
            Assert.That(GroundWaveRenderers(), Is.EqualTo(0), "Hết luồng lửa thì ảnh luồng lửa tắt");
        }

        [UnityTest]
        public IEnumerator SoulPrison_BindsNearestAhead_LongStun_DamageOverTime_Cage()
        {
            yield return Load();
            yield return StandAt(DummyX - 2.5f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            var hits = new Hits(dummy);
            _combat.Rage.Add(RageMeter.PerTier);
            Assert.IsTrue(_combat.Use(2, 2));
            for (float t = 0f; t < 1f && !view.IsStunned; t += Time.deltaTime)
                yield return null;
            Assert.IsTrue(view.IsStunned, "Xích trói: choáng");
            Assert.That(hits.List[0].effect.stun, Is.GreaterThanOrEqualTo(3f), "Giam lâu");
            // xích quấn quanh thân trước, rồi lồng mọc lên ngay sau đó
            for (float t = 0f; t < 0.5f && CageRenderers() == 0; t += Time.deltaTime)
                yield return null;
            Assert.That(CageRenderers(), Is.GreaterThan(0), "Lồng xích dựng quanh bia");
            yield return new WaitForSeconds(2.5f);
            Assert.IsTrue(view.IsStunned, "Vẫn bị giam sau 2.5 giây");
            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(5), "Mất máu liên tục trong lồng");
            Assert.IsFalse(_combat.Busy, "BLUE làm việc khác được trong lúc lồng giam");
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(hits.List.Last().effect.heavy, "Hết giam lồng vỡ nổ");
            yield return new WaitForSeconds(0.5f);
            Assert.That(CageRenderers(), Is.EqualTo(0), "Lồng đã tắt");
        }

        // ---------- Màn đánh boss ----------

        [UnityTest]
        public IEnumerator BossArena_AllNineSkills_HitTheLion_PrisonShorterOnBoss()
        {
            yield return Load("BossArena");
            float edge = BlueKitTestBounds(_boss.Health).min.x;
            for (int slot = 0; slot < 3; slot++)
            {
                for (int variant = 0; variant < 3; variant++)
                {
                    Skill skill = _combat.Get(slot, variant);
                    // Chiêu tầm xa đứng xa hơn cho thấy bay tới
                    float gap = slot > 0 && variant == 1 ? 4.5f : 1.3f;
                    yield return StandAt(edge - gap, ArenaFloor);
                    _player.GetComponent<PlayerHealth>().Restore();
                    ClearCooldowns();
                    if (slot == 2)
                        _combat.Rage.Add(RageMeter.PerTier);
                    float before = _boss.Health.Current;
                    Assert.IsTrue(_combat.Use(slot, variant), $"Dùng được {skill.name}");
                    if (slot == 2 && variant == 2)
                    {
                        for (float t = 0f; t < 1f && !_boss.IsStunned; t += Time.deltaTime)
                            yield return null;
                        Assert.IsTrue(_boss.IsStunned, "Xích trói boss: choáng");
                        yield return null;
                        yield return null;
                        Assert.That(_boss.StunRemaining, Is.LessThan(1.6f), "Boss da cứng: bị giam ngắn hơn bia tập (3.2 s)");
                        for (float t = 0f; t < 0.5f && CageRenderers() == 0; t += Time.deltaTime)
                            yield return null;
                        Assert.That(CageRenderers(), Is.GreaterThan(0), "Lồng xích dựng quanh boss");
                    }

                    yield return WaitIdle();
                    yield return new WaitForSeconds(slot > 0 && variant == 1 ? 1.2f : 0.3f);
                    Assert.That(_boss.Health.Current, Is.LessThan(before), $"{skill.name} phải trúng boss sư tử");
                }
            }
        }

        static Bounds BlueKitTestBounds(Health h)
        {
            Collider2D c = h.GetComponent<Collider2D>();
            return c != null ? c.bounds : new Bounds(h.transform.position, Vector3.one);
        }

        static int CageRenderers() => ActiveFx(BlueFx.CageFront) + ActiveFx(BlueFx.CageBack);

        static int GroundWaveRenderers() => ActiveFx(BlueFx.GroundWave);

        static int ActiveFx(string sprite)
        {
            Sprite s = BlueFx.SpriteOf(sprite);
            return Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Count(r => r.enabled && r.sprite == s);
        }
    }
}
