using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng scene CharacterSelect (mở đầu tiên khi vào game): 2 thẻ Bạch tuộc / Nhện có chân dung (Art/UI/Portrait_*.png,
    /// tạo bằng Tools/art/portraits.py) và nút Bắt đầu. Thêm nút "Đổi nhân vật" vào HUD màn chơi để quay lại đây.
    /// </summary>
    static class CharacterSelectBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/CharacterSelect.unity";
        const string UiFolder = "Assets/_Project/Art/UI";

        static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static void BuildScene(Sprite glow)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CaveBuilder.CameraBackground;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = new GameObject("Character Select", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // Nền: tranh hang phủ kín màn hình, tối bớt cho chữ và thẻ nổi lên
            Sprite backdrop = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Drawn/far.png");
            if (backdrop != null)
            {
                Image back = AddImage("Backdrop", root.transform, backdrop, Color.white);
                Stretch(back.rectTransform);
                var fitter = back.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = backdrop.rect.width / backdrop.rect.height;
            }

            Stretch(AddImage("Shade", root.transform, null, new Color(0.04f, 0.02f, 0.1f, 0.55f)).rectTransform);

            RectTransform safe = Rect("Safe Area", root.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            Text title = Label("Title", safe, "CHỌN NHÂN VẬT", 96, FontStyle.Bold, Color.white);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1600f, 140f));
            Text hint = Label("Hint", safe, "Hai nhân vật điều khiển giống nhau: vuốt để bò · chạm nhanh để nhảy · chạm vào đá để đu",
                34, FontStyle.Normal, new Color(0.82f, 0.78f, 1f));
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1380f, 60f));

            Sprite panel = PanelSprite();
            var cards = new[]
            {
                Card(safe, PlayableCharacter.Octopus, "BẠCH TUỘC", "Đu bằng xúc tu · trườn hình rắn", "Portrait_Octopus", -360f, panel, glow),
                Card(safe, PlayableCharacter.Spider, "NHỆN", "Đu bằng tơ · bò bằng 8 chân", "Portrait_Spider", 360f, panel, glow),
            };

            // Nút Bắt đầu
            Image start = AddImage("Start Button", safe, panel, new Color(0.55f, 0.3f, 1f, 1f));
            start.type = UnityEngine.UI.Image.Type.Sliced;
            start.raycastTarget = true; // nhận cú bấm
            Place(start.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(560f, 140f));
            var startButton = start.gameObject.AddComponent<Button>();
            Text startText = Label("Text", start.transform, "BẮT ĐẦU", 64, FontStyle.Bold, Color.white);
            Stretch(startText.rectTransform);

            var menu = root.AddComponent<CharacterSelectMenu>();
            var so = new SerializedObject(menu);
            SerializedProperty list = so.FindProperty("cards");
            list.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("character").enumValueIndex = (int)cards[i].Character;
                element.FindPropertyRelative("button").objectReferenceValue = cards[i].Button;
                element.FindPropertyRelative("frame").objectReferenceValue = cards[i].Frame;
                element.FindPropertyRelative("glow").objectReferenceValue = cards[i].Glow;
                element.FindPropertyRelative("portrait").objectReferenceValue = cards[i].Portrait;
                element.FindPropertyRelative("group").objectReferenceValue = cards[i].Group;
            }

            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        }

        /// <summary>Nút "Đổi nhân vật" ở góc trên bên phải màn chơi (góc trái là thanh nộ): quay về màn chọn nhân vật.</summary>
        public static void AddBackButton(RectTransform safeArea, PlayerInputReader input)
        {
            Image back = AddImage("Change Character", safeArea, PanelSprite(), new Color(0.12f, 0.07f, 0.24f, 0.8f));
            back.type = UnityEngine.UI.Image.Type.Sliced;
            back.rectTransform.pivot = new Vector2(1f, 1f);
            Place(back.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(330f, 90f));
            back.raycastTarget = true;
            Text text = Label("Text", back.transform, "Đổi nhân vật", 36, FontStyle.Bold, Color.white);
            Stretch(text.rectTransform);
            var button = back.gameObject.AddComponent<SceneButton>();
            var so = new SerializedObject(button);
            so.FindProperty("input").objectReferenceValue = input;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Thẻ nhân vật ----------

        struct CardParts
        {
            public PlayableCharacter Character;
            public Button Button;
            public RectTransform Frame;
            public Image Glow;
            public RectTransform Portrait;
            public CanvasGroup Group;
        }

        static CardParts Card(Transform parent, PlayableCharacter character, string name, string description, string portrait,
            float x, Sprite panel, Sprite glow)
        {
            Image frame = AddImage($"Card {name}", parent, panel, new Color(0.11f, 0.07f, 0.22f, 0.92f));
            frame.type = UnityEngine.UI.Image.Type.Sliced;
            frame.raycastTarget = true;
            Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -35f), new Vector2(640f, 550f)); // vừa cả màn 4:3 (iPad)
            var button = frame.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; // thẻ tự to lên / mờ đi theo lựa chọn
            var group = frame.gameObject.AddComponent<CanvasGroup>();

            Image halo = AddImage("Glow", frame.transform, glow, new Color(0.55f, 0.85f, 1f, 0f));
            Stretch(halo.rectTransform, -90f);
            halo.transform.SetAsFirstSibling();

            Image art = AddImage("Portrait", frame.transform, ImportSprite($"{UiFolder}/{portrait}.png"), Color.white);
            art.preserveAspect = true;
            art.rectTransform.anchorMin = new Vector2(0f, 0f);
            art.rectTransform.anchorMax = new Vector2(1f, 1f);
            art.rectTransform.offsetMin = new Vector2(30f, 170f);
            art.rectTransform.offsetMax = new Vector2(-30f, -30f);

            Text title = Label("Name", frame.transform, name, 64, FontStyle.Bold, Color.white);
            Place(title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 105f), new Vector2(600f, 80f));
            Text info = Label("Description", frame.transform, description, 32, FontStyle.Normal, new Color(0.82f, 0.78f, 1f));
            Place(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(600f, 50f));

            return new CardParts
            {
                Character = character, Button = button, Frame = frame.rectTransform, Glow = halo,
                Portrait = art.rectTransform, Group = group,
            };
        }

        // ---------- Tiện ích ----------

        internal static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        internal static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        internal static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Image AddImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Text Label(string name, Transform parent, string text, int size, FontStyle style, Color color)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = UiFont;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.02f, 0.2f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        internal static Sprite ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Octopus] Chưa có {path} (chạy Tools/art/portraits.py)");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Khung bo góc kéo giãn được (9 mảnh)
        internal static Sprite PanelSprite()
        {
            const int size = 128;
            const float radius = 36f;
            Sprite sprite = ArtFactory.Sprite("UiPanel", size, size, 100f, new Vector2(0.5f, 0.5f), (u, v) =>
            {
                float x = Mathf.Abs(u * size - size / 2f) - (size / 2f - radius);
                float y = Mathf.Abs(v * size - size / 2f) - (size / 2f - radius);
                float d = new Vector2(Mathf.Max(x, 0f), Mathf.Max(y, 0f)).magnitude - radius;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }, 2, false);

            string path = AssetDatabase.GetAssetPath(sprite);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.spriteBorder = new Vector4(radius + 4f, radius + 4f, radius + 4f, radius + 4f);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
