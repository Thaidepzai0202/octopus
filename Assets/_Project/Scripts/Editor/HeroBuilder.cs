using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Octopus.EditorTools.CharacterSelectBuilder;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng 3 nhân vật người (RED, PINK, BLUE) lên người chơi: thân hình người (CapsuleCollider2D, đang tắt),
    /// HeroMotor (chạy / nhảy / bục một chiều), và hình mỗi con ("Hero Red" / "Hero Pink" / "Hero Blue" đặt ở bàn chân, bên
    /// trong là "Hero Visual" do HeroRigBuilder.Build dựng). PlayerCharacter bật hình + HeroMotor theo màn chọn nhân vật.
    /// HUD riêng (cần điều hướng góc trái, nút NHẢY góc phải) do BuildHud dựng, chỉ hiện khi chơi nhân vật người.
    /// </summary>
    static class HeroBuilder
    {
        /// <summary>
        /// Chiều cao hình nhân vật người (đỉnh tóc tới mặt sàn): ngang bạch tuộc đứng (thân + xúc tu ≈ 1.95 unit, xem
        /// BossArenaBuilder.BossHeight), nhện ≈ 1.4.
        /// </summary>
        public const float HeroHeight = 2f;

        static readonly PlayableCharacter[] Heroes = { PlayableCharacter.Red, PlayableCharacter.Pink, PlayableCharacter.Blue };

        public static void Build(GameObject player)
        {
            // Thân hình người: cao gần bằng hình, đáy trùng đáy vòng tròn của bạch tuộc (bàn chân = tâm thân − 0.5)
            var capsule = player.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.8f, 1.9f);
            capsule.offset = new Vector2(0f, capsule.size.y * 0.5f - HeroMotor.FeetOffset);
            capsule.enabled = false;

            var motor = player.AddComponent<HeroMotor>();
            var motorSettings = new SerializedObject(motor);
            motorSettings.FindProperty("heroCollider").objectReferenceValue = capsule;
            motorSettings.ApplyModifiedPropertiesWithoutUndo();

            var character = player.GetComponent<PlayerCharacter>();
            if (character == null)
                character = player.AddComponent<PlayerCharacter>();
            var settings = new SerializedObject(character);
            foreach (PlayableCharacter hero in Heroes)
            {
                Transform holder = RigArt.NewChild($"Hero {hero}", player.transform);
                holder.localPosition = new Vector3(0f, -HeroMotor.FeetOffset, 0f); // gốc hình = giữa hai bàn chân
                HeroRigBuilder.Build(holder, hero, HeroHeight);
                holder.gameObject.SetActive(false);
                settings.FindProperty(hero switch
                {
                    PlayableCharacter.Pink => "pinkVisual",
                    PlayableCharacter.Blue => "blueVisual",
                    _ => "redVisual",
                }).objectReferenceValue = holder.gameObject;
            }

            settings.FindProperty("heroMotor").objectReferenceValue = motor;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- HUD: cần điều hướng + nút NHẢY ----------

        /// <summary>Cần điều hướng góc dưới bên trái + nút NHẢY góc dưới bên phải (cụm nút chiêu dịch vào trong chừa chỗ).</summary>
        public static void BuildHud(RectTransform safe, CombatController combat, PlayerInputReader input, Sprite glow)
        {
            Sprite disc = LaserSkillBuilder.DiscSprite();
            Sprite ring = LaserSkillBuilder.RingSprite();
            Sprite triangle = TriangleSprite();

            RectTransform root = Rect("Hero Controls", safe);
            Stretch(root);
            root.SetAsFirstSibling(); // vẽ dưới các nút khác
            var controls = root.gameObject.AddComponent<HeroControls>();

            // Cần điều hướng: vùng chạm lớn góc dưới trái (đế cần hiện ra chỗ đặt ngón tay)
            RectTransform zone = Rect("Joystick", root);
            zone.anchorMin = zone.anchorMax = zone.pivot = Vector2.zero;
            zone.anchoredPosition = Vector2.zero;
            zone.sizeDelta = new Vector2(620f, 600f);
            var hit = zone.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            RectTransform stickBase = Rect("Base", zone);
            stickBase.anchorMin = stickBase.anchorMax = Vector2.zero;
            stickBase.anchoredPosition = new Vector2(250f, 230f);
            stickBase.sizeDelta = new Vector2(280f, 280f);
            CombatBuilder.Img("Shadow", stickBase, glow, -50f, new Color(0f, 0f, 0f, 0.35f));
            CombatBuilder.Img("Disc", stickBase, disc, 0f, new Color(0.07f, 0.04f, 0.15f, 0.55f));
            CombatBuilder.Img("Ring", stickBase, ring, 0f, new Color(0.55f, 0.85f, 1f, 0.6f));
            Image[] arrows = new Image[4];
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            string[] names = { "Up", "Down", "Left", "Right" };
            for (int i = 0; i < 4; i++)
            {
                RectTransform a = Rect($"Arrow {names[i]}", stickBase);
                a.anchorMin = a.anchorMax = new Vector2(0.5f, 0.5f);
                a.anchoredPosition = dirs[i] * 185f; // ngoài vòng đế: núm kéo tới đâu cũng không che mũi tên
                a.sizeDelta = new Vector2(42f, 42f);
                a.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, dirs[i]));
                var image = a.gameObject.AddComponent<Image>();
                image.sprite = triangle;
                image.color = new Color(1f, 1f, 1f, 0.35f);
                image.raycastTarget = false;
                arrows[i] = image;
            }

            RectTransform knob = Rect("Knob", stickBase);
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(130f, 130f);
            CombatBuilder.Img("Glow", knob, glow, -30f, new Color(0.55f, 0.85f, 1f, 0.35f));
            CombatBuilder.Img("Face", knob, disc, 0f, new Color(0.85f, 0.92f, 1f, 0.92f));
            CombatBuilder.Img("Rim", knob, ring, 0f, new Color(0.55f, 0.85f, 1f, 1f));
            foreach (Image arrow in arrows)
                arrow.transform.SetAsLastSibling(); // mũi tên vẽ trên núm

            var stick = zone.gameObject.AddComponent<VirtualJoystick>();
            var so = new SerializedObject(stick);
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("stickBase").objectReferenceValue = stickBase;
            so.FindProperty("knob").objectReferenceValue = knob;
            so.FindProperty("upArrow").objectReferenceValue = arrows[0];
            so.FindProperty("downArrow").objectReferenceValue = arrows[1];
            so.FindProperty("leftArrow").objectReferenceValue = arrows[2];
            so.FindProperty("rightArrow").objectReferenceValue = arrows[3];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Chữ nhắc trên đế cần: kéo lên / xuống để đổi kiểu chiêu
            Text tip = Label("Tip", zone, "kéo lên / xuống: đổi chiêu", 26, FontStyle.Bold, new Color(0.82f, 0.78f, 1f, 0.75f));
            Place(tip.rectTransform, Vector2.zero, new Vector2(250f, 470f), new Vector2(420f, 40f));

            // Nút NHẢY: góc dưới phải, to nhất cụm (nút bấm nhiều nhất)
            RectTransform jump = Rect("Jump Button", root);
            jump.anchorMin = jump.anchorMax = jump.pivot = new Vector2(1f, 0f);
            jump.anchoredPosition = new Vector2(-40f, 40f);
            jump.sizeDelta = new Vector2(215f, 215f);
            var jumpHit = jump.gameObject.AddComponent<Image>();
            jumpHit.sprite = disc;
            jumpHit.color = new Color(1f, 1f, 1f, 0f);
            RectTransform face = Rect("Face", jump);
            Stretch(face);
            Image halo = CombatBuilder.Img("Halo", face, glow, -55f, new Color(0.55f, 0.85f, 1f, 0.15f));
            CombatBuilder.Img("Base", face, disc, 0f, new Color(0.07f, 0.04f, 0.15f, 0.9f));
            Image chevron = CombatBuilder.Img("Icon", face, triangle, 58f, new Color(0.85f, 0.95f, 1f, 0.95f));
            chevron.rectTransform.anchoredPosition = new Vector2(0f, 22f);
            Image rim = CombatBuilder.Img("Ring", face, ring, 0f, new Color(0.55f, 0.85f, 1f));
            Text label = Label("Label", face, "NHẢY", 40, FontStyle.Bold, Color.white);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(200f, 50f));
            var jumpButton = jump.gameObject.AddComponent<HeroJumpButton>();
            var jumpSettings = new SerializedObject(jumpButton);
            jumpSettings.FindProperty("input").objectReferenceValue = input;
            jumpSettings.FindProperty("face").objectReferenceValue = face;
            jumpSettings.FindProperty("ring").objectReferenceValue = rim;
            jumpSettings.FindProperty("halo").objectReferenceValue = halo;
            jumpSettings.ApplyModifiedPropertiesWithoutUndo();

            zone.gameObject.SetActive(false);
            jump.gameObject.SetActive(false);
            var controlSettings = new SerializedObject(controls);
            controlSettings.FindProperty("combat").objectReferenceValue = combat;
            SerializedProperty list = controlSettings.FindProperty("heroOnly");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = zone.gameObject;
            list.GetArrayElementAtIndex(1).objectReferenceValue = jump.gameObject;
            controlSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Tam giác chỉ lên (mũi tên trên cần điều hướng, nút NHẢY, huy hiệu ▲ / ▼ trên nút chiêu; ▼ = xoay 180°).</summary>
        internal static Sprite TriangleSprite() => ArtFactory.Sprite("UiTriangle", 128, 128, 100f, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            // Tam giác bo góc: đỉnh trên, đáy dưới
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float d = Mathf.Max(-y - 0.62f, Mathf.Max(0.866f * x + 0.5f * y - 0.42f, -0.866f * x + 0.5f * y - 0.42f));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * 64f));
        }, 2, false);
    }
}
