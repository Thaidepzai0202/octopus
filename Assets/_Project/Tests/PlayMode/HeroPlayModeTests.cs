using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>
    /// 3 nhân vật người (RED, PINK, BLUE): chọn nhân vật ra đúng hình, cần điều hướng chạy / đổi kiểu chiêu, nút nhảy, nhảy đúp
    /// (cả 3 con cùng một bộ thông số mặc định; lượn / nhảy đúp kiểu xoay / lướt là tuỳ chọn tắt sẵn, test bật lên để thử),
    /// tảng đá là bục một chiều (bạch tuộc vẫn bám dưới đá), 9 chiêu đánh trúng bia tập, đi hết màn hang động và lên các tầng
    /// đá màn đánh boss, đóng băng boss / bia tập, hồi máu, khiên chặn đòn.
    /// Màn hang động: mặt bệ xuất phát y = -1 (x -5..5), bia tập ở x = 3.7. Màn đánh boss: sàn y = 0, boss ở x = 12.
    /// </summary>
    public class HeroPlayModeTests
    {
        const float PlaygroundFloor = -1f;
        const float ArenaFloor = 0f;
        const float DummyX = 3.7f;

        static readonly PlayableCharacter[] Heroes = { PlayableCharacter.Red, PlayableCharacter.Pink, PlayableCharacter.Blue };

        OctopusController _player;
        HeroMotor _hero;
        CombatController _combat;
        Rigidbody2D _body;
        PlayerInputReader _input;
        LionBoss _boss;

        [TearDown]
        public void ClearChoice() => CharacterSelection.Clear();

        IEnumerator Load(PlayableCharacter character, string scene = "Playground", bool realInput = false)
        {
            CharacterSelection.Choose(character);
            yield return SceneManager.LoadSceneAsync(scene);
            _player = Object.FindAnyObjectByType<OctopusController>();
            _input = _player.GetComponent<PlayerInputReader>();
            _input.enabled = realInput;
            _hero = _player.Hero;
            _combat = _player.GetComponent<CombatController>();
            _body = _player.GetComponent<Rigidbody2D>();
            _boss = Object.FindAnyObjectByType<LionBoss>();
            if (_boss != null)
                _boss.SetAi(false);
            yield return new WaitForFixedUpdate();
        }

        float Feet => _body.position.y - HeroMotor.FeetOffset;

        IEnumerator WaitGrounded(float max)
        {
            for (float t = 0f; t < max && !_hero.Grounded; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
        }

        // Đứng yên trên sàn ở x, quay mặt về phía facing
        IEnumerator StandAt(float x, float floorY, float facing = 1f)
        {
            _player.ReleaseJump();
            _player.SetAim(0);
            _player.SetIntent(false, facing);
            yield return new WaitForFixedUpdate();
            yield return null;
            _player.SetIntent(false, 0f);
            _player.ResetTo(new Vector2(x, floorY + HeroMotor.FeetOffset + 0.05f));
            yield return WaitGrounded(1.5f);
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(_hero.Grounded, "Phải đứng trên sàn");
            Assert.That(_player.Facing, Is.EqualTo(Mathf.Sign(facing)));
        }

        // Nhảy (giữ nút) tới khi chạm đất lại; airJump: tới đỉnh thì bấm nhảy lần nữa. Nhả nút ở đỉnh để PINK khỏi lượn.
        // Ghi độ cao bàn chân lên được (so với lúc cất nhảy) và quãng ngang
        float _rise, _riseBeforeAir, _dx;

        IEnumerator Jump(bool airJump)
        {
            float start = Feet, startX = _body.position.x, top = Feet;
            _riseBeforeAir = 0f;
            _player.PressJump();
            bool used = !airJump;
            for (float t = 0f; t < 4f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                top = Mathf.Max(top, Feet);
                bool falling = _body.linearVelocity.y <= 0f && !_hero.IsDashing;
                if (falling && _hero.JumpHeld)
                {
                    _player.ReleaseJump();
                    if (!used)
                    {
                        used = true;
                        _riseBeforeAir = top - start;
                        _player.PressJump();
                    }
                }

                if (t > 0.2f && _hero.Grounded)
                    break;
            }

            _player.ReleaseJump();
            _rise = top - start;
            _dx = _body.position.x - startX;
        }

        static Health DummyAt(float x) =>
            Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Where(d => Mathf.Abs(d.transform.position.x - x) < 0.5f)
                .Select(d => d.GetComponent<Health>()).FirstOrDefault();

        static Collider2D Level(string name) => GameObject.Find($"Level/{name}").GetComponent<Collider2D>();

        static Collider2D RockNear(float x, float bottom) =>
            Object.FindObjectsByType<Rock>(FindObjectsSortMode.None).Select(r => r.GetComponent<Collider2D>())
                .OrderBy(c => Mathf.Abs(c.bounds.center.x - x) + Mathf.Abs(c.bounds.min.y - bottom)).First();

        // ---------- Chọn nhân vật ----------

        [UnityTest]
        public IEnumerator Selection_SpawnsHeroVisualAndHeroBody([ValueSource(nameof(Heroes))] PlayableCharacter character)
        {
            yield return Load(character);
            var pc = _player.GetComponent<PlayerCharacter>();
            Assert.That(pc.Current, Is.EqualTo(character));
            Assert.IsTrue(_player.IsHero, "Chọn nhân vật người thì bật HeroMotor");
            Assert.That(_hero.Character, Is.EqualTo(character));
            foreach (PlayableCharacter other in new[] { PlayableCharacter.Octopus, PlayableCharacter.Spider }.Concat(Heroes))
            {
                GameObject visual = pc.VisualOf(other);
                Assert.IsNotNull(visual, $"Phải có hình {other}");
                Assert.That(visual.activeInHierarchy, Is.EqualTo(other == character), $"Chỉ bật hình của {character} ({other})");
            }

            IHeroVisual heroVisual = pc.HeroVisual;
            Assert.IsNotNull(heroVisual, "Hình nhân vật người (IHeroVisual) trong \"Hero Visual\"");
            Assert.That(((Component)heroVisual).name, Is.EqualTo("Hero Visual"));
            Assert.That(_hero.Visual, Is.SameAs(heroVisual));
            Assert.IsNull(_player.GetComponentInChildren<OctopusRigAnimator>(), "Không hiện bạch tuộc");
            Assert.IsNull(_player.GetComponentInChildren<SpiderRigAnimator>(), "Không hiện nhện");
            Assert.IsFalse(_player.Swing.enabled, "Nhân vật người không đu dây");
            Assert.IsFalse(_player.GetComponent<CircleCollider2D>().enabled, "Thân tròn của bạch tuộc tắt");
            Assert.IsTrue(_hero.Collider.enabled, "Thân hình người bật");
            Assert.That(_body.gravityScale, Is.EqualTo(0f), "Trọng lực do HeroMotor tự tính");

            yield return StandAt(0f, PlaygroundFloor);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Grounded));
            Bounds b = heroVisual.VisualBounds;
            Assert.That(b.min.y, Is.EqualTo(PlaygroundFloor).Within(0.12f), "Chân hình chạm mặt sàn");
            Assert.That(b.size.y, Is.InRange(1.5f, 2.3f), "Cao cỡ bạch tuộc (~2 unit; RED đứng thủ thế khom thấp theo ảnh nên thấp hơn)");
            Assert.That(_hero.Move, Is.EqualTo(HeroMove.Idle));

            var skill = _combat.Get(0, 0);
            Assert.IsNotNull(skill, "Có bộ 9 chiêu");
            Assert.That(_combat.SkillsOf(character).Length, Is.EqualTo(9));
        }

        [UnityTest]
        public IEnumerator Octopus_KeepsSwingControls_HeroControlsHidden()
        {
            yield return Load(PlayableCharacter.Octopus);
            Assert.IsFalse(_player.IsHero);
            Assert.IsTrue(_player.Swing.enabled);
            Assert.IsTrue(_player.GetComponent<CircleCollider2D>().enabled);
            Assert.IsFalse(_player.GetComponent<CapsuleCollider2D>().enabled);
            Assert.IsNull(Object.FindAnyObjectByType<VirtualJoystick>(), "Bạch tuộc không có cần điều hướng");
            Assert.IsNull(Object.FindAnyObjectByType<HeroJumpButton>(), "Bạch tuộc không có nút nhảy");
        }

        // ---------- Cần điều hướng, nút nhảy, nút chiêu ----------

        [UnityTest]
        public IEnumerator Hud_JoystickLeft_JumpAndSkillButtonsRight_NoOverlap()
        {
            yield return Load(PlayableCharacter.Red, realInput: true);
            yield return null;
            var stick = Object.FindAnyObjectByType<VirtualJoystick>();
            var jump = Object.FindAnyObjectByType<HeroJumpButton>();
            var pad = Object.FindAnyObjectByType<SkillPad>();
            Assert.IsNotNull(stick, "Có cần điều hướng");
            Assert.IsNotNull(jump, "Có nút nhảy");

            Rect zone = ScreenRect(stick.Zone);
            Assert.That(zone.center.x, Is.LessThan(Screen.width * 0.35f), "Cần điều hướng bên trái");
            Assert.That(zone.center.y, Is.LessThan(Screen.height * 0.4f), "Cần điều hướng phía dưới");
            var buttons = new List<Rect> { ScreenRect((RectTransform)jump.transform) };
            for (int i = 0; i < 3; i++)
                buttons.Add(ScreenRect(pad.ButtonRect(i)));
            Rect jumpRect = buttons[0];
            Assert.That(jumpRect.center.x, Is.GreaterThan(Screen.width * 0.8f), "Nút nhảy ở góc phải");
            Assert.That(jumpRect.center.y, Is.LessThan(Screen.height * 0.25f), "Nút nhảy ở góc dưới");
            Assert.That(buttons[2].center.x, Is.LessThan(jumpRect.center.x), "Nút 2 bên trái nút nhảy");
            Assert.That(buttons[1].center.x, Is.LessThan(buttons[2].center.x), "Nút 1 bên trái nút 2");
            Assert.That(buttons[3].center.y, Is.GreaterThan(buttons[2].center.y), "Nút 3 phía trên nút 2");
            for (int a = 0; a < buttons.Count; a++)
            {
                Rect r = buttons[a];
                Assert.That(r.xMin >= -1f && r.xMax <= Screen.width + 1f && r.yMin >= -1f && r.yMax <= Screen.height + 1f, $"Nút {a} nằm trong màn hình");
                Assert.IsFalse(r.Overlaps(zone), $"Nút {a} không chồng lên vùng cần điều hướng");
                for (int b = a + 1; b < buttons.Count; b++)
                {
                    float gap = Vector2.Distance(r.center, buttons[b].center) - (r.width + buttons[b].width) * 0.5f;
                    Assert.That(gap, Is.GreaterThan(0f), $"Nút {a} và nút {b} không chồng nhau");
                }
            }

            Assert.IsTrue(_input.IsOverBlocker(zone.center), "Chạm vào cần điều hướng không tính là cử chỉ khác");
            Assert.IsTrue(_input.IsOverBlocker(jumpRect.center), "Chạm vào nút nhảy không tính là cử chỉ khác");
        }

        static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners); // canvas Overlay: world = pixel màn hình
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator Joystick_RunsLeftAndRight_DeadZone_JumpButtonJumps()
        {
            yield return Load(PlayableCharacter.Red, realInput: true);
            yield return StandAt(0f, PlaygroundFloor);
            var stick = Object.FindAnyObjectByType<VirtualJoystick>();
            float x0 = _body.position.x;

            stick.Set(new Vector2(0.1f, 0f)); // trong vùng chết
            yield return new WaitForSeconds(0.3f);
            Assert.That(Mathf.Abs(_body.position.x - x0), Is.LessThan(0.05f), "Kéo nhẹ trong vùng chết thì không đi");

            stick.Set(new Vector2(1f, 0f));
            yield return new WaitForSeconds(0.4f);
            Assert.That(_body.position.x, Is.GreaterThan(x0 + 1.5f), "Kéo phải thì chạy sang phải");
            Assert.That(_player.Facing, Is.EqualTo(1f));
            Assert.That(_hero.Move, Is.EqualTo(HeroMove.Run));
            float vx = _body.linearVelocity.x;
            Assert.That(vx, Is.EqualTo(_hero.Settings.runSpeed).Within(0.3f), "Kéo hết cỡ: chạy nhanh nhất");

            stick.Set(new Vector2(-0.6f, 0f));
            yield return new WaitForSeconds(0.4f);
            Assert.That(_body.linearVelocity.x, Is.LessThan(0f), "Kéo trái thì chạy sang trái");
            Assert.That(-_body.linearVelocity.x, Is.LessThan(_hero.Settings.runSpeed * 0.7f), "Kéo vừa phải: chạy chậm hơn");
            Assert.That(_player.Facing, Is.EqualTo(-1f), "Quay mặt theo hướng chạy");

            stick.Release();
            yield return new WaitForSeconds(0.35f);
            Assert.That(Mathf.Abs(_body.linearVelocity.x), Is.LessThan(0.1f), "Thả cần thì dừng lại");

            // Nút NHẢY trên màn hình
            var jump = Object.FindAnyObjectByType<HeroJumpButton>();
            var data = new PointerEventData(EventSystem.current) { pointerId = 7 };
            float feet = Feet;
            jump.OnPointerDown(data);
            yield return new WaitForSeconds(0.25f);
            Assert.That(Feet, Is.GreaterThan(feet + 1.5f), "Bấm nút nhảy thì nhảy lên");
            Assert.That(_player.State, Is.EqualTo(OctopusState.Airborne));
            jump.OnPointerUp(data);
            yield return WaitGrounded(2f);
            Assert.IsTrue(_hero.Grounded);
        }

        // Kéo lên / xuống: không chạy, nút chiêu hiện ▲ / ▼ + tên chiêu, bấm nút ra chiêu ↑ / ↓
        [UnityTest]
        public IEnumerator Joystick_UpDown_SelectsVariant_ShownOnButtons()
        {
            yield return Load(PlayableCharacter.Pink, realInput: true);
            yield return StandAt(0f, PlaygroundFloor);
            var stick = Object.FindAnyObjectByType<VirtualJoystick>();
            var pad = Object.FindAnyObjectByType<SkillPad>();
            var used = new List<(int slot, int variant)>();
            _combat.Used += (slot, variant, skill) => used.Add((slot, variant));
            var data = new PointerEventData(EventSystem.current) { pointerId = 3 };
            float x0 = _body.position.x;

            Assert.That(pad.ShownIcon(0), Is.SameAs(pad.IconOf(PlayableCharacter.Pink, 0)), "Nút hiện icon của PINK");
            Assert.IsNotNull(pad.ShownIcon(0));
            for (int i = 0; i < 3; i++)
                Assert.IsFalse(pad.BadgeShown(i), "Chưa kéo thì không có huy hiệu ▲ / ▼");

            stick.Set(new Vector2(0.15f, 1f)); // kéo lên (hơi lệch)
            yield return new WaitForSeconds(0.3f);
            Assert.That(_input.Aim, Is.EqualTo(1));
            Assert.That(Mathf.Abs(_body.position.x - x0), Is.LessThan(0.05f), "Kéo lên thì không đi");
            Assert.That(_combat.HeroVariant, Is.EqualTo(1));
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(pad.BadgeShown(i), $"Nút {i + 1} hiện ▲");
                Assert.That(pad.ShownVariantName(i), Is.EqualTo(_combat.Get(i, 1).name), $"Nút {i + 1} hiện tên chiêu ↑");
            }

            pad.Press(0, data);
            pad.Release(0, data);
            Assert.That(used.Last(), Is.EqualTo((0, 1)), "Kéo lên + nút 1 = chiêu ↑ của nút 1");
            yield return new WaitUntil(() => !_combat.Busy);

            stick.Set(new Vector2(0f, -1f));
            yield return null;
            yield return null;
            Assert.That(pad.ShownVariantName(1), Is.EqualTo(_combat.Get(1, 2).name), "Kéo xuống: nút hiện tên chiêu ↓");
            pad.Press(1, data);
            pad.Release(1, data);
            Assert.That(used.Last(), Is.EqualTo((1, 2)), "Kéo xuống + nút 2 = chiêu ↓ của nút 2");
            yield return new WaitUntil(() => !_combat.Busy);

            stick.Set(new Vector2(1f, 0.3f)); // kéo ngang (hơi chếch lên) = chạy + chiêu thường
            yield return new WaitForSeconds(0.25f);
            Assert.That(_input.Aim, Is.EqualTo(0));
            Assert.That(_body.position.x, Is.GreaterThan(x0 + 0.3f), "Kéo ngang thì chạy");
            for (int i = 0; i < 3; i++)
                Assert.IsFalse(pad.BadgeShown(i), "Kéo ngang: không có huy hiệu");
            stick.Release();
            yield return new WaitForSeconds(0.3f);
            pad.Press(0, data);
            Assert.That(used.Last(), Is.EqualTo((0, 0)), "Không kéo + nút 1 = chiêu thường");
        }

        // ---------- Chạy, nhảy, nhảy đúp ----------

        // Cả 3 con mặc định di chuyển giống hệt nhau; các nét riêng (lượn, nhảy đúp xoay / lướt) tắt sẵn
        [UnityTest]
        public IEnumerator Defaults_AllHeroesMoveTheSame_OptionalTraitsOff()
        {
            yield return Load(PlayableCharacter.Red);
            HeroMoveSettings red = _hero.SettingsOf(PlayableCharacter.Red);
            foreach (PlayableCharacter other in new[] { PlayableCharacter.Pink, PlayableCharacter.Blue })
            {
                HeroMoveSettings s = _hero.SettingsOf(other);
                Assert.That(s, Is.Not.SameAs(red), "Mỗi con một bộ thông số riêng để chỉnh");
                Assert.That(JsonUtility.ToJson(s), Is.EqualTo(JsonUtility.ToJson(red)), $"{other} mặc định giống RED");
            }

            foreach (PlayableCharacter hero in Heroes)
            {
                HeroMoveSettings s = _hero.SettingsOf(hero);
                Assert.That(s.airJump, Is.EqualTo(HeroAirJump.Plain), "Nhảy đúp thường");
                Assert.That(s.airJumps, Is.EqualTo(1), "Có nhảy đúp");
                Assert.IsFalse(s.canGlide, "Lượn tắt sẵn");
            }
        }

        [UnityTest]
        public IEnumerator Jump_HoldHigh_TapLow_DoubleJumpHigher([ValueSource(nameof(Heroes))] PlayableCharacter character)
        {
            yield return Load(character);
            HeroMoveSettings s = _hero.Settings;
            yield return StandAt(-1f, PlaygroundFloor);

            yield return Jump(airJump: false);
            float full = _rise;
            Assert.That(full, Is.EqualTo(s.jumpHeight).Within(0.3f), $"Giữ nút: nhảy cao ~{s.jumpHeight} unit");
            Assert.IsTrue(_hero.Grounded, "Đáp xuống sàn");

            // Chạm nhanh (bấm rồi nhả ngay): nhảy thấp
            yield return StandAt(-1f, PlaygroundFloor);
            float start = Feet, top = Feet;
            _player.Tap();
            for (float t = 0f; t < 2f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                top = Mathf.Max(top, Feet);
                if (t > 0.2f && _hero.Grounded)
                    break;
            }

            Assert.That(top - start, Is.LessThan(full * 0.6f), "Nhả nút sớm thì nhảy thấp hơn");
            Assert.That(top - start, Is.GreaterThan(0.4f), "Chạm nhanh vẫn nhảy lên");

            // Nhảy đúp ở đỉnh
            yield return StandAt(-1f, PlaygroundFloor);
            yield return Jump(airJump: true);
            Assert.That(_riseBeforeAir, Is.EqualTo(s.jumpHeight).Within(0.3f));
            float expected = s.airJump == HeroAirJump.SoulStep
                ? s.jumpHeight + s.stepHop * s.stepHop / (2f * s.Gravity)
                : s.jumpHeight + s.airJumpHeight;
            Assert.That(_rise, Is.EqualTo(expected).Within(0.45f), $"Nhảy đúp lên tổng ~{expected:0.0} unit");
            Assert.That(_rise, Is.GreaterThan(6.9f), "Nhảy + nhảy đúp đủ cao để lên mặt tảng đá (đá cao ~6–6.7 unit trên sàn)");
            Assert.IsTrue(_hero.Grounded);
        }

        // Nhảy đúp không được quá số lần; chạm đất là hồi; rời mép sàn (coyote) vẫn nhảy được; bấm sớm trước khi chạm đất vẫn nhảy
        [UnityTest]
        public IEnumerator Jump_CoyoteTime_JumpBuffer_AirJumpsRefill()
        {
            yield return Load(PlayableCharacter.Red);
            HeroMoveSettings s = _hero.Settings;

            // Bấm nhảy khi còn cách sàn một chút (rơi xuống): chạm sàn là nhảy luôn
            _player.ResetTo(new Vector2(0f, PlaygroundFloor + HeroMotor.FeetOffset + 1.2f));
            yield return WaitGrounded(1f);
            yield return new WaitForSeconds(0.2f);
            _player.ResetTo(new Vector2(0f, PlaygroundFloor + HeroMotor.FeetOffset + 0.6f));
            for (int i = 0; i < 10 && _body.linearVelocity.y > -4f; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(_hero.Grounded);
            _player.PressJump();
            bool jumped = false;
            for (float t = 0f; t < 0.5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                if (_body.linearVelocity.y > s.LaunchSpeed(s.jumpHeight) * 0.8f)
                {
                    jumped = true;
                    break;
                }
            }

            Assert.IsTrue(jumped, "Bấm nhảy ngay trước khi chạm sàn: chạm sàn là nhảy (jump buffer)");
            Assert.That(_hero.AirJumpsLeft, Is.EqualTo(s.airJumps), "Nhảy từ sàn vẫn còn lượt nhảy đúp");
            _player.ReleaseJump();
            yield return WaitGrounded(3f);

            // Chạy khỏi mép bệ xuất phát (x = 5) rồi bấm nhảy ngay: vẫn tính là nhảy từ sàn (coyote time)
            _player.ResetTo(new Vector2(3.5f, PlaygroundFloor + HeroMotor.FeetOffset + 0.05f));
            yield return WaitGrounded(1f);
            _player.SetIntent(false, 1f);
            for (int i = 0; i < 100 && (_hero.Grounded || Feet > PlaygroundFloor - 0.05f); i++)
                yield return new WaitForFixedUpdate();
            _player.SetIntent(false, 0f);
            Assert.IsFalse(_hero.Grounded, "Đã rời mép sàn");
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_body.linearVelocity.y, Is.GreaterThan(s.LaunchSpeed(s.jumpHeight) * 0.8f), "Coyote time: rời mép một chút vẫn nhảy");
            Assert.That(_hero.AirJumpsLeft, Is.EqualTo(s.airJumps), "Nhảy kiểu coyote không tốn lượt nhảy đúp");

            // Hết lượt nhảy trên không thì bấm nữa không nhảy (thả từ trên cao xuống bệ xuất phát)
            _player.ReleaseJump();
            _player.ResetTo(new Vector2(0f, PlaygroundFloor + HeroMotor.FeetOffset + 6f));
            yield return new WaitForFixedUpdate();
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_hero.AirJumpsLeft, Is.EqualTo(s.airJumps - 1));
            _player.ReleaseJump();
            while (_body.linearVelocity.y > 0f)
                yield return new WaitForFixedUpdate();
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_body.linearVelocity.y, Is.LessThanOrEqualTo(0f), "Hết lượt nhảy đúp thì không nhảy nữa");
            _player.ReleaseJump();
            _player.ResetTo(new Vector2(0f, PlaygroundFloor + HeroMotor.FeetOffset + 0.05f));
            yield return WaitGrounded(1f);
            Assert.That(_hero.AirJumpsLeft, Is.EqualTo(s.airJumps), "Chạm đất là hồi lượt nhảy đúp");
        }

        [UnityTest]
        public IEnumerator GlideOption_HoldJumpInAir_GlidesSlowly_RefillsOnLanding()
        {
            yield return Load(PlayableCharacter.Pink);
            HeroMoveSettings s = _hero.Settings;
            s.canGlide = true; // tuỳ chọn, mặc định tắt
            yield return StandAt(-2f, PlaygroundFloor);

            // Nhảy, giữ nút qua đỉnh: lượn (nhấc nhẹ lên rồi rơi rất chậm)
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            while (_body.linearVelocity.y > 0f)
                yield return new WaitForFixedUpdate();
            float apex = Feet;
            float peak = apex, slowest = 0f;
            bool glided = false;
            for (float t = 0f; t < 1.2f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, Feet);
                slowest = Mathf.Min(slowest, _body.linearVelocity.y);
                glided |= _hero.IsGliding && _hero.Move == HeroMove.Glide;
            }

            Assert.IsTrue(glided, "Giữ nút nhảy trên không thì lượn (dáng Glide)");
            Assert.That(peak, Is.GreaterThan(apex + 0.4f), "Bắt đầu lượn được nhấc nhẹ lên");
            Assert.That(slowest, Is.GreaterThanOrEqualTo(-s.glideFallSpeed - 0.05f), "Lượn thì rơi chậm");
            Assert.That(Feet, Is.GreaterThan(PlaygroundFloor + 2f), "Sau hơn 1 giây vẫn lơ lửng trên cao");
            Assert.That(_hero.GlideLeft, Is.LessThan(s.glideTime - 1f), "Đồng hồ lượn đang chạy");

            // Lượn tối đa glideTime giây rồi rơi bình thường
            for (float t = 0f; t < s.glideTime && _hero.IsGliding; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(_hero.IsGliding, $"Lượn tối đa {s.glideTime} giây");
            float before = _body.linearVelocity.y;
            yield return new WaitForSeconds(0.15f);
            if (!_hero.Grounded)
                Assert.That(_body.linearVelocity.y, Is.LessThan(before - 1f), "Hết lượn thì rơi nhanh dần");

            _player.ReleaseJump();
            yield return WaitGrounded(3f);
            Assert.That(_hero.GlideLeft, Is.EqualTo(s.glideTime).Within(0.01f), "Chạm đất là hồi đầy lượn");

            // So sánh: không giữ nút thì rơi nhanh
            yield return StandAt(-2f, PlaygroundFloor);
            yield return Jump(airJump: false);
            Assert.IsFalse(_hero.IsGliding);
        }

        [UnityTest]
        public IEnumerator SoulStepOption_DoubleJumpDashesSideways()
        {
            yield return Load(PlayableCharacter.Blue);
            HeroMoveSettings s = _hero.Settings;
            s.airJump = HeroAirJump.SoulStep; // tuỳ chọn, mặc định nhảy đúp thường
            yield return StandAt(-4f, PlaygroundFloor, facing: 1f);
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            while (_body.linearVelocity.y > 0f)
                yield return new WaitForFixedUpdate();
            _player.ReleaseJump();
            float x0 = _body.position.x, y0 = Feet;
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(_hero.IsDashing, "Nhảy đúp kiểu SoulStep là lướt ngang");
            Assert.That(_hero.Move, Is.EqualTo(HeroMove.Dash));
            float lowest = Feet;
            while (_hero.IsDashing)
            {
                yield return new WaitForFixedUpdate();
                lowest = Mathf.Min(lowest, Feet);
            }

            float dx = _body.position.x - x0;
            Assert.That(dx, Is.EqualTo(s.stepDistance).Within(0.6f), "Lướt ngang một đoạn ngắn về phía trước");
            Assert.That(y0 - lowest, Is.LessThan(0.3f), "Lướt ngang, không rơi");
            yield return new WaitForFixedUpdate();
            Assert.That(_body.linearVelocity.y, Is.GreaterThan(s.stepHop * 0.7f), "Lướt xong bật nhẹ lên");
            Assert.That(_hero.Move, Is.EqualTo(HeroMove.DoubleJump));
            _player.ReleaseJump();
            yield return WaitGrounded(3f);

            // Kéo cần về sau rồi nhảy đúp kiểu lướt: lướt về phía kéo
            yield return StandAt(0f, PlaygroundFloor, facing: 1f);
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            while (_body.linearVelocity.y > 0f)
                yield return new WaitForFixedUpdate();
            _player.ReleaseJump();
            _player.SetIntent(false, -1f);
            x0 = _body.position.x;
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            while (_hero.IsDashing)
                yield return new WaitForFixedUpdate();
            _player.SetIntent(false, 0f);
            Assert.That(_body.position.x - x0, Is.LessThan(-s.stepDistance * 0.7f), "Kéo cần sang trái thì lướt sang trái");
            _player.ReleaseJump();
        }

        [UnityTest]
        public IEnumerator SpinOption_DoubleJumpSlashesAdjacentDummy()
        {
            yield return Load(PlayableCharacter.Red);
            _hero.Settings.airJump = HeroAirJump.Spin; // tuỳ chọn, mặc định nhảy đúp thường
            Health dummy = DummyAt(DummyX);
            float dealt = 0f;
            dummy.Damaged += (amount, from, effect) => dealt += amount;
            yield return StandAt(DummyX - 1.1f, PlaygroundFloor);
            _player.PressJump();
            yield return new WaitForSeconds(0.12f);
            _player.ReleaseJump();
            Assert.That(dealt, Is.EqualTo(0f));
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(_hero.Move, Is.EqualTo(HeroMove.DoubleJump), "Nhảy đúp: dáng DoubleJump");
            Assert.That(dealt, Is.GreaterThan(0f), "Xoay vũ khí chém trúng bia tập ngay cạnh");
            _player.ReleaseJump();
            yield return WaitGrounded(3f);
        }

        // ---------- Tảng đá là bục một chiều ----------

        // Màn đánh boss: đứng dưới tảng đá tầng thấp (x = -22, mặt dưới ~5.15, mặt trên ~6.15), nhảy + nhảy đúp xuyên lên, đứng
        // trên mặt đá, kéo xuống + nhảy thì rơi qua đá xuống sàn
        [UnityTest]
        public IEnumerator Rocks_OneWay_JumpThroughFromBelow_StandOnTop_DropThrough([ValueSource(nameof(Heroes))] PlayableCharacter character)
        {
            yield return Load(character, "BossArena");
            Collider2D rock = RockNear(-22f, 5f);
            float top = rock.bounds.max.y;
            yield return StandAt(-24f, ArenaFloor);
            yield return Hop(rock);
            Assert.IsTrue(_hero.Grounded, "Đứng trên mặt đá");
            Assert.IsTrue(_hero.OnRock);
            Assert.That(_hero.Ground, Is.EqualTo(rock));
            Assert.That(Feet, Is.EqualTo(top).Within(0.1f), "Bàn chân trên mặt đá");
            Assert.That(_player.State, Is.EqualTo(OctopusState.Grounded));
            yield return new WaitForSeconds(0.5f);
            Assert.That(Feet, Is.EqualTo(top).Within(0.1f), "Đứng yên trên đá, không lọt xuống");

            // Kéo xuống + nhảy: thả xuống qua đá
            _player.SetAim(-1);
            _player.PressJump();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            _player.ReleaseJump();
            _player.SetAim(0);
            Assert.That(_body.linearVelocity.y, Is.LessThan(0f), "Kéo xuống + nhảy trên đá: rơi xuống chứ không nhảy lên");
            yield return WaitGrounded(3f);
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(_hero.Grounded);
            Assert.IsFalse(_hero.OnRock, "Đã rơi qua đá xuống sàn");
            Assert.That(Feet, Is.EqualTo(ArenaFloor).Within(0.1f));

            // Sàn vẫn cứng: kéo xuống + nhảy trên sàn thì nhảy bình thường
            _player.SetAim(-1);
            _player.PressJump();
            yield return new WaitForSeconds(0.15f);
            _player.ReleaseJump();
            _player.SetAim(0);
            Assert.That(Feet, Is.GreaterThan(ArenaFloor + 0.5f), "Trên sàn thì kéo xuống + nhảy vẫn nhảy");
            yield return WaitGrounded(3f);
            Assert.That(Feet, Is.EqualTo(ArenaFloor).Within(0.1f), "Không lọt qua sàn");
        }

        // Bạch tuộc vẫn bắn xúc tu, rút lên bám dưới tảng đá như cũ (đá không thành bục một chiều với bạch tuộc)
        [UnityTest]
        public IEnumerator Rocks_OctopusStillClingsUnderRock()
        {
            yield return Load(PlayableCharacter.Octopus, "BossArena");
            Collider2D rock = RockNear(-22f, 5f);
            _player.ResetTo(new Vector2(-22f, ArenaFloor + 0.55f));
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(Physics2D.GetIgnoreCollision(_player.GetComponent<CircleCollider2D>(), rock), "Bạch tuộc vẫn va chạm với đá");
            Assert.IsTrue(_player.Grab(new Vector2(-22f, rock.bounds.min.y + 0.2f)), "Bắn xúc tu tới đá");
            _player.SetIntent(holding: true, drag: 0f);
            for (float t = 0f; t < 4f && _player.State != OctopusState.Clinging; t += Time.deltaTime)
                yield return null;
            _player.SetIntent(holding: false, drag: 0f);
            Assert.That(_player.State, Is.EqualTo(OctopusState.Clinging), "Rút lên bám dưới đá");
            Assert.That(_player.Mover.ClingSurface, Is.EqualTo(rock));
            Assert.That(_body.position.y, Is.LessThan(rock.bounds.min.y), "Bám mặt dưới đá");
        }

        // Nhảy từ chỗ đang đứng lên / sang mặt target như người chơi: chạy tới chỗ cất nhảy, nhảy (giữ nút), ở đỉnh nhảy đúp,
        // lái về giữa target cho tới khi đáp. Bật tuỳ chọn lượn thì giữ nút suốt. Trả về qua _hero.Ground
        IEnumerator Hop(Collider2D target, float maxTime = 6f)
        {
            Bounds b = target.bounds;
            HeroMoveSettings s = _hero.Settings;
            bool glide = s.canGlide;
            bool step = s.airJump == HeroAirJump.SoulStep;
            Collider2D from = _hero.Ground;
            bool climb = b.max.y > Feet + 0.5f; // lên cao hơn: cần nhảy đúp

            // Chỗ cất nhảy: đá nằm ngay trên đầu thì đứng dưới giữa đá (nhảy đúp kiểu lướt thì lùi lại một quãng lướt); không thì
            // ra mép phía target
            if (from != null && from != target)
            {
                Bounds f = from.bounds;
                float dir = Mathf.Sign(b.center.x - _body.position.x + 1e-3f);
                bool above = b.max.x > f.min.x && b.min.x < f.max.x;
                float takeoff = above ? b.center.x - (step ? dir * s.stepDistance : 0f) : dir > 0f ? f.max.x - 0.3f : f.min.x + 0.3f;
                takeoff = Mathf.Clamp(takeoff, f.min.x + 0.3f, f.max.x - 0.3f);
                for (float t = 0f; t < 5f; t += Time.fixedDeltaTime)
                {
                    float dx = takeoff - _body.position.x;
                    if (Mathf.Abs(dx) < 0.15f && Mathf.Abs(_body.linearVelocity.x) < 1f)
                        break;
                    _player.SetIntent(false, Mathf.Clamp(dx * 2f, -1f, 1f));
                    yield return new WaitForFixedUpdate();
                }
            }

            _player.PressJump();
            bool airUsed = false;
            float side = Mathf.Sign(b.center.x - _body.position.x + 1e-3f);
            for (float t = 0f; t < maxTime; t += Time.fixedDeltaTime)
            {
                // Nhảy đúp kiểu lướt mà lên cao: trước khi lướt đứng lùi một quãng lướt; sau đó lái về giữa target
                float aimX = step && climb && !airUsed ? b.center.x - side * s.stepDistance : b.center.x;
                float dx = aimX - _body.position.x;
                float run = Mathf.Clamp(dx * 1.3f, -1f, 1f);
                _player.SetIntent(false, run);
                yield return new WaitForFixedUpdate();
                bool falling = _body.linearVelocity.y <= 0f && !_hero.IsDashing;
                if (falling && !airUsed && Feet < b.max.y + 1.2f && _hero.AirJumpsLeft > 0)
                {
                    airUsed = true;
                    _player.ReleaseJump();
                    _player.PressJump();
                    continue;
                }

                if (falling && !glide && _hero.JumpHeld)
                    _player.ReleaseJump();
                if (t > 0.2f && _hero.Grounded)
                    break;
                if (Feet < b.min.y - 14f)
                    break; // rơi xuống vực
            }

            _player.ReleaseJump();
            _player.SetIntent(false, 0f);
            yield return new WaitForSeconds(0.1f);
        }

        // ---------- Đi hết màn ----------

        // Màn hang động: từ bệ xuất phát nhảy qua 5 tảng đá, xuống đảo nghỉ, qua 5 tảng nữa, xuống bệ đích
        [UnityTest]
        public IEnumerator Playground_HeroTraversesAllRocksToGoal([ValueSource(nameof(Heroes))] PlayableCharacter character)
        {
            yield return Load(character);
            yield return StandAt(0f, PlaygroundFloor);
            var route = new List<Collider2D>();
            for (int i = 1; i <= 5; i++)
                route.Add(Level($"Rock {i:00}"));
            route.Add(Level("Rest Island"));
            for (int i = 6; i <= 10; i++)
                route.Add(Level($"Rock {i:00}"));
            route.Add(Level("Goal Platform"));

            var failed = new List<string>();
            foreach (Collider2D target in route)
            {
                yield return Hop(target);
                if (_hero.Ground != target)
                {
                    failed.Add(target.name);
                    // Không lên được thì đặt lên đó rồi đi tiếp, để biết hết các chặng hỏng
                    _player.ResetTo(new Vector2(target.bounds.center.x, target.bounds.max.y + HeroMotor.FeetOffset + 0.05f));
                    yield return WaitGrounded(1.5f);
                }
            }

            Assert.That(failed, Is.Empty, $"{character} không nhảy lên được: {string.Join(", ", failed)}");
        }

        // Màn đánh boss: từ sàn lên tầng đá thấp (~5), tầng giữa (~9) rồi tầng cao (~13); dùng chiêu đánh trúng boss
        [UnityTest]
        public IEnumerator BossArena_HeroPlayable_ReachesRockTiers_HitsBoss([ValueSource(nameof(Heroes))] PlayableCharacter character)
        {
            yield return Load(character, "BossArena");
            var pc = _player.GetComponent<PlayerCharacter>();
            Assert.That(pc.Current, Is.EqualTo(character));
            Assert.IsTrue(pc.VisualOf(character).activeInHierarchy, "Hiện hình nhân vật người");
            Assert.IsNotNull(Object.FindAnyObjectByType<VirtualJoystick>(), "Có cần điều hướng");
            var health = _player.GetComponent<PlayerHealth>();
            Assert.That(health.Max, Is.EqualTo(300f));
            yield return StandAt(-19f, ArenaFloor);
            Assert.That(health.Center.y, Is.EqualTo(_body.position.y + HeroMotor.ChestOffset).Within(0.01f), "Boss nhắm vào ngực nhân vật người");

            Collider2D low = RockNear(-22f, 5f), mid = RockNear(-16.5f, 9f), high = RockNear(-10.5f, 13f);
            int reached = 0;
            foreach (Collider2D rock in new[] { low, mid, high })
            {
                yield return Hop(rock);
                if (_hero.Ground != rock)
                    break;
                reached++;
            }

            Debug.Log($"[HeroTest] {character}: lên được {reached}/3 tầng đá màn đánh boss");
            Assert.That(reached, Is.GreaterThanOrEqualTo(2), $"{character} phải lên được tầng đá thấp và tầng giữa (lên được {reached})");

            // Xuống sàn, chạy tới trước mặt boss, đánh
            _player.ResetTo(new Vector2(4f, ArenaFloor + HeroMotor.FeetOffset + 0.05f));
            yield return WaitGrounded(2f);
            _player.SetIntent(false, 1f);
            yield return new WaitForFixedUpdate();
            yield return null;
            _player.SetIntent(false, 0f);
            float before = _boss.Health.Current;
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(_combat.Use(0, 0), "Dùng đánh thường");
                for (float t = 0f; t < 3f && _combat.Busy; t += Time.deltaTime)
                    yield return null;
            }

            Assert.That(_boss.Health.Current, Is.LessThan(before), "Đánh trúng boss");
        }

        // ---------- 9 chiêu ----------

        // Mỗi chiêu (bản tạm HeroPlaceholderSkill) đánh trúng bia tập trước mặt. Chiêu thật không gây sát thương trực tiếp (bật
        // trạng thái, khiên, phản kích…) thì đánh thường ngay sau đó phải trúng.
        [UnityTest]
        public IEnumerator Skill_HitsTrainingDummy(
            [ValueSource(nameof(Heroes))] PlayableCharacter character,
            [Values(0, 1, 2)] int slot,
            [Values(0, 1, 2)] int variant)
        {
            yield return Load(character);
            yield return StandAt(DummyX - 1.5f, PlaygroundFloor);
            Health dummy = DummyAt(DummyX);
            float dealt = 0f;
            dummy.Damaged += (amount, from, effect) => dealt += amount;
            if (slot == 2)
                _combat.Rage.Add(RageMeter.PerTier * 2f);

            Skill skill = _combat.Get(slot, variant);
            Assert.IsNotNull(skill, "Thiếu chiêu");
            Assert.IsFalse(string.IsNullOrEmpty(skill.name), "Chiêu phải có tên");
            Assert.IsTrue(_combat.Use(slot, variant), $"Không dùng được chiêu {skill.name}");
            for (float t = 0f; t < 8f && (_combat.Busy || dealt <= 0f); t += Time.deltaTime)
                yield return null;
            yield return new WaitForSeconds(0.3f);

            if (dealt <= 0f && !(skill is HeroPlaceholderSkill))
            {
                // Chiêu bật trạng thái / phòng thủ: đánh thường sau đó vẫn phải ra đòn trúng
                Assert.IsFalse(_combat.Busy, $"{skill.name} chạy quá lâu");
                yield return StandAt(DummyX - 1.5f, PlaygroundFloor);
                Assert.IsTrue(_combat.Use(0, 0));
                for (float t = 0f; t < 4f && (_combat.Busy || dealt <= 0f); t += Time.deltaTime)
                    yield return null;
            }

            Assert.That(dealt, Is.GreaterThan(0f), $"{character} · {skill.name}: phải đánh trúng bia tập");
        }

        // ---------- Đóng băng, hồi máu, khiên ----------

        [UnityTest]
        public IEnumerator Freeze_TrainingDummy_StandsStill_ThenReacts()
        {
            yield return Load(PlayableCharacter.Pink);
            Health dummy = DummyAt(DummyX);
            var view = dummy.GetComponent<TrainingDummy>();
            SkillContext ctx = _combat.CreateContext();
            List<Health> frozen = ctx.FreezeAll(1f);
            Assert.That(frozen.Count, Is.EqualTo(Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Length), "Đóng băng mọi bia tập");
            Assert.IsTrue(dummy.IsFrozen);
            dummy.TakeHit(15f, new Vector2(DummyX - 2f, 0f), new HitEffect { knockback = new Vector2(8f, 0f) });
            yield return new WaitForSeconds(0.5f);
            Assert.That(view.Displacement.magnitude, Is.LessThan(0.01f), "Đang đóng băng: đứng im dù bị đánh văng");
            Assert.IsTrue(dummy.IsFrozen);
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(dummy.IsFrozen, "Hết đóng băng");
            yield return new WaitForSeconds(0.15f);
            Assert.That(view.Displacement.x, Is.GreaterThan(0.05f), "Hết đóng băng thì đòn dồn lại mới văng ra");
        }

        // Boss bị đóng băng giữa cú húc: đứng hình, không đánh trúng người chơi; hết đóng băng thì húc tiếp
        [UnityTest]
        public IEnumerator Freeze_StopsLionAttack_ThenResumes()
        {
            yield return Load(PlayableCharacter.Pink, "BossArena");
            var health = _player.GetComponent<PlayerHealth>();
            yield return StandAt(_boss.transform.position.x - 14f, ArenaFloor);
            _boss.ForceAttack(LionAttack.Charge);
            float x0 = _boss.transform.position.x;
            for (float t = 0f; t < 3f && Mathf.Abs(_boss.transform.position.x - x0) < 0.5f; t += Time.deltaTime)
                yield return null;
            Assert.That(Mathf.Abs(_boss.transform.position.x - x0), Is.GreaterThan(0.5f), "Boss đã lao đi");

            _combat.CreateContext().Freeze(_boss.Health, 1.5f);
            Assert.IsTrue(_boss.IsFrozen);
            yield return null;
            float x1 = _boss.transform.position.x;
            float hp = health.Current;
            yield return new WaitForSeconds(1.2f);
            Assert.That(_boss.transform.position.x, Is.EqualTo(x1).Within(0.01f), "Đang đóng băng: boss đứng hình");
            Assert.That(_boss.Current, Is.EqualTo(LionAttack.Charge), "Đòn đang ra chỉ dừng lại, chưa bị huỷ");
            Assert.That(health.Current, Is.EqualTo(hp), "Boss đóng băng không đánh trúng người chơi");
            Assert.IsFalse(((Behaviour)_boss.Visual).enabled, "Hình boss đứng hình");
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(_boss.IsFrozen);
            Assert.IsTrue(((Behaviour)_boss.Visual).enabled, "Hết đóng băng hình diễn tiếp");
            Assert.That(Mathf.Abs(_boss.transform.position.x - x1), Is.GreaterThan(0.5f), "Hết đóng băng thì húc tiếp");
        }

        // Cầu lửa đang bay đứng yên giữa không trung khi boss bị đóng băng (đạn cũng đứng hình)
        [UnityTest]
        public IEnumerator Freeze_StopsLionProjectiles()
        {
            yield return Load(PlayableCharacter.Pink, "BossArena");
            var health = _player.GetComponent<PlayerHealth>();
            yield return StandAt(_boss.transform.position.x - 12f, ArenaFloor);
            _boss.ForceAttack(LionAttack.Fireballs);
            GameObject holder = null;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                holder = GameObject.Find("Lion Hazards FX");
                if (holder != null && t > 0.95f) // cầu lửa phóng lúc ~0.55 giây, bay 1 giây
                    break;
                yield return null;
            }

            Assert.IsNotNull(holder, "Boss đã phóng cầu lửa");
            _combat.CreateContext().FreezeAll(1f);
            yield return null;
            var active = holder.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            Assert.That(active.Length, Is.GreaterThan(0), "Có cầu lửa / vòng báo đang hiện");
            Vector3[] at = active.Select(r => r.transform.position).ToArray();
            float hp = health.Current;
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < active.Length; i++)
                Assert.That(Vector3.Distance(active[i].transform.position, at[i]), Is.LessThan(0.01f), "Đạn của boss đứng yên khi bị đóng băng");
            Assert.That(health.Current, Is.EqualTo(hp), "Đạn đứng hình không gây sát thương");
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(active.Any(r => !r.gameObject.activeInHierarchy || Vector3.Distance(r.transform.position, at[System.Array.IndexOf(active, r)]) > 0.05f),
                "Hết đóng băng thì đạn bay tiếp");
        }

        [UnityTest]
        public IEnumerator Heal_And_BlockFor_Hooks()
        {
            // Màn hang động không có máu người chơi: hồi máu / khiên không làm gì, không lỗi
            yield return Load(PlayableCharacter.Blue);
            SkillContext ctx = _combat.CreateContext();
            Assert.IsNull(ctx.Health);
            Assert.That(ctx.Heal(50f), Is.EqualTo(0f));
            Assert.IsNull(ctx.BlockFor(1f, hit => { }));

            yield return Load(PlayableCharacter.Blue, "BossArena");
            var health = _player.GetComponent<PlayerHealth>();
            ctx = _combat.CreateContext();
            Assert.That(ctx.Health, Is.SameAs(health));
            yield return StandAt(-10f, ArenaFloor);
            Vector2 left = _body.position + Vector2.left * 2f;

            Assert.IsTrue(health.Hit(50f, left, Vector2.zero));
            Assert.That(health.Current, Is.EqualTo(250f));
            yield return new WaitForSeconds(0.9f); // hết bất tử sau khi bị đánh
            float healed = 0f;
            health.Healed += amount => healed += amount;
            Assert.That(ctx.Heal(30f), Is.EqualTo(30f).Within(0.01f));
            Assert.That(health.Current, Is.EqualTo(280f).Within(0.01f));
            Assert.That(ctx.Heal(500f), Is.EqualTo(20f).Within(0.01f), "Không hồi quá máu tối đa");
            Assert.That(health.Current, Is.EqualTo(300f).Within(0.01f));
            Assert.That(healed, Is.EqualTo(50f).Within(0.01f));

            // Khiên: chặn đòn, báo cho chiêu
            var blocked = new List<HitInfo>();
            PlayerHealth.Block block = ctx.BlockFor(1f, hit => blocked.Add(hit));
            Assert.IsTrue(health.IsBlocking);
            Assert.IsTrue(health.Hit(40f, left, new Vector2(6f, 5f), heavy: true), "Đòn chạm người (bị chặn) thì báo đã trúng để boss thôi đánh lại nhát đó");
            Assert.That(health.Current, Is.EqualTo(300f), "Bị chặn: không mất máu");
            Assert.That(blocked.Count, Is.EqualTo(1));
            Assert.That(blocked[0].damage, Is.EqualTo(40f));
            Assert.IsTrue(blocked[0].heavy);
            Assert.That(blocked[0].from, Is.EqualTo(left));
            Assert.That(block.Count, Is.EqualTo(1));
            Assert.That(health.Burn(10f, left), Is.EqualTo(0f), "Khiên chặn cả lửa cháy");
            Assert.That(_body.linearVelocity.magnitude, Is.LessThan(1.5f), "Bị chặn thì không bị đẩy văng");

            block.Cancel();
            Assert.IsFalse(health.IsBlocking, "Tắt khiên sớm");
            Assert.IsTrue(health.Hit(10f, left, Vector2.zero));
            Assert.That(health.Current, Is.EqualTo(290f));
            yield return new WaitForSeconds(0.9f);

            // Đòn thật của boss bị chặn: phản kích biết mình vừa bị đánh
            blocked.Clear();
            yield return StandAt(_boss.transform.position.x - 7.5f, ArenaFloor);
            ctx.BlockFor(3f, hit => blocked.Add(hit));
            float hp = health.Current;
            _boss.ForceAttack(LionAttack.ClawSwipe);
            for (float t = 0f; t < 2.8f && blocked.Count == 0; t += Time.deltaTime)
                yield return null;
            Assert.That(blocked.Count, Is.GreaterThan(0), "Vả vuốt của boss trúng khiên");
            Assert.That(health.Current, Is.EqualTo(hp), "Khiên chặn hết sát thương của boss");
        }
    }
}
