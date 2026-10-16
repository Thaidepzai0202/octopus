using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Octopus.Tests
{
    /// <summary>Màn chọn nhân vật (scene CharacterSelect) và nút quay lại từ màn chơi.</summary>
    public class CharacterSelectPlayModeTests
    {
        [TearDown]
        public void ClearChoice()
        {
            CharacterSelection.Clear();
            SetStartupScene("");
        }

        // Như bấm Play ngay trong scene Playground (test chạy từ scene riêng của Test Runner nên phải giả lập)
        static void SetStartupScene(string scene) =>
            typeof(CharacterSelection).GetProperty(nameof(CharacterSelection.StartupScene), BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, scene);

        [UnityTest]
        public IEnumerator PlayInPlayground_WithoutChoosing_OpensCharacterSelectFirst()
        {
            CharacterSelection.Clear();
            SetStartupScene("Playground");
            yield return SceneManager.LoadSceneAsync("Playground");
            for (float t = 0f; t < 3f && SceneManager.GetActiveScene().name != "CharacterSelect"; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterSelect"),
                "Bấm Play trong màn chơi mà chưa chọn nhân vật thì phải mở màn chọn nhân vật trước");
        }

        [UnityTest]
        public IEnumerator CharacterSelect_ChooseSpider_StartsGameAsSpider()
        {
            CharacterSelection.Clear();
            yield return SceneManager.LoadSceneAsync("CharacterSelect");
            yield return null;
            var menu = Object.FindAnyObjectByType<CharacterSelectMenu>();
            Assert.IsNotNull(menu, "Phải có màn chọn nhân vật");
            Assert.That(menu.Selected, Is.EqualTo(PlayableCharacter.Octopus), "Mặc định chọn bạch tuộc");

            menu.Select(PlayableCharacter.Spider);
            menu.StartGame();
            for (float t = 0f; t < 3f && SceneManager.GetActiveScene().name != "Playground"; t += Time.unscaledDeltaTime)
                yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Playground"), "Bấm Bắt đầu thì vào màn chơi");
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(player.Current, Is.EqualTo(PlayableCharacter.Spider));
            Assert.IsNotNull(player.GetComponentInChildren<SpiderRigAnimator>(), "Vào màn chơi bằng con nhện");
            Assert.IsNull(player.GetComponentInChildren<OctopusRigAnimator>());
        }

        // Bấm thật: dò giao diện tại giữa mỗi nút phải trúng chính nút đó (không bị tắt nhận chạm, không bị hình khác che)
        [UnityTest]
        public IEnumerator CharacterSelect_EveryButtonReceivesClicks()
        {
            yield return SceneManager.LoadSceneAsync("CharacterSelect");
            yield return null;
            UnityEngine.UI.Button[] buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
            Assert.That(buttons.Length, Is.EqualTo(3), "2 thẻ nhân vật + nút Bắt đầu");
            foreach (UnityEngine.UI.Button button in buttons)
                AssertClickable(button.transform, button.name);
        }

        [UnityTest]
        public IEnumerator Playground_HudButtonsReceiveClicks()
        {
            yield return SceneManager.LoadSceneAsync("Playground");
            yield return null;
            var pad = Object.FindAnyObjectByType<SkillPad>();
            for (int i = 0; i < 3; i++)
                AssertClickable(pad.ButtonRect(i), $"Nút chiêu {i + 1}");
            AssertClickable(Object.FindAnyObjectByType<SceneButton>().transform, "Nút Đổi nhân vật");
        }

        static void AssertClickable(Transform target, string name)
        {
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners); // canvas Overlay: toạ độ world = pixel màn hình
            var data = new PointerEventData(EventSystem.current) { position = (corners[0] + corners[2]) / 2f };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), $"{name}: bấm vào không trúng gì");
            Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(target), $"{name}: bấm vào lại trúng {hits[0].gameObject.name}");
        }

        [UnityTest]
        public IEnumerator CharacterSelect_CardsAndStartButtonFitOnScreen()
        {
            yield return SceneManager.LoadSceneAsync("CharacterSelect");
            yield return null;
            foreach (UnityEngine.UI.Button button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(-1f), $"{button.name} lọt ra ngoài mép trái");
                Assert.That(corners[2].x, Is.LessThanOrEqualTo(Screen.width + 1f), $"{button.name} lọt ra ngoài mép phải");
                Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(-1f), $"{button.name} lọt ra ngoài mép dưới");
                Assert.That(corners[2].y, Is.LessThanOrEqualTo(Screen.height + 1f), $"{button.name} lọt ra ngoài mép trên");
            }
        }

        // Nút "Đổi nhân vật" ở góc trên bên phải màn chơi: chạm không làm nhảy, bấm thì về màn chọn nhân vật
        [UnityTest]
        public IEnumerator Playground_ChangeCharacterButton_ReturnsToMenu()
        {
            yield return SceneManager.LoadSceneAsync("Playground");
            yield return null;
            var button = Object.FindAnyObjectByType<SceneButton>();
            Assert.IsNotNull(button, "Phải có nút Đổi nhân vật");
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            Vector2 center = (corners[0] + corners[2]) / 2f;
            Assert.That(center.x, Is.GreaterThan(Screen.width * 0.7f), "Nút nằm bên phải (bên trái là thanh nộ)");
            Assert.That(center.y, Is.GreaterThan(Screen.height * 0.7f), "Nút nằm phía trên");
            var input = Object.FindAnyObjectByType<PlayerInputReader>();
            Assert.IsTrue(input.IsOverBlocker(center), "Chạm vào nút không được tính là chạm điều khiển");

            button.OnPointerClick(null);
            for (float t = 0f; t < 3f && SceneManager.GetActiveScene().name != "CharacterSelect"; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterSelect"));
        }
    }
}
