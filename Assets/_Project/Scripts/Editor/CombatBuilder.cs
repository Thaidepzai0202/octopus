using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static Octopus.EditorTools.CharacterSelectBuilder;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng hệ thống chiêu: nộ + bộ 9 chiêu cho mỗi nhân vật (bạch tuộc, nhện, RED, PINK, BLUE) trên người chơi
    /// (CombatController), hiệu ứng chiêu (Combat FX), số sát thương, bia tập để thử chiêu, và HUD: cụm 3 nút chiêu góc dưới
    /// phải (nút 2 ở góc, 1 bên trái, 3 phía trên), thanh nộ góc trên trái; nhân vật người có thêm cần điều hướng góc trái và
    /// nút NHẢY (HeroBuilder.BuildHud). Biểu tượng nút: Art/UI/Skill_*.png (Tools/art/skill_icons.py, Tools/art/hero_ui.py).
    /// </summary>
    static class CombatBuilder
    {
        const string UiFolder = "Assets/_Project/Art/UI";

        // Bia tập: hai bên bệ xuất phát và trên đảo nghỉ giữa màn
        static readonly Vector2[] DummySpots = { new Vector2(3.7f, -1f), new Vector2(-3.9f, -1f), new Vector2(34.5f, -1f) };

        /// <summary>trainingDummies = false: không dựng bia tập (màn đánh boss), chỉ bộ chiêu + hiệu ứng + HUD.</summary>
        public static void Build(GameObject player, Sprite glow, int enemyLayer, bool trainingDummies = true)
        {
            var rage = player.AddComponent<RageMeter>();
            var combat = player.AddComponent<CombatController>();
            combat.SetDefaultSkills(DefaultSkills.Octopus(), DefaultSkills.Spider());
            combat.SetDefaultSkills(PlayableCharacter.Red, RedSkills.Default());
            combat.SetDefaultSkills(PlayableCharacter.Pink, PinkSkills.Default());
            combat.SetDefaultSkills(PlayableCharacter.Blue, BlueSkills.Default());
            var so = new SerializedObject(combat);
            so.FindProperty("targetMask").intValue = 1 << enemyLayer;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildFx(glow);
            BuildDamageNumbers();
            if (trainingDummies)
            {
                foreach (Vector2 spot in DummySpots)
                    BuildDummy(spot, enemyLayer);
            }
            BuildHud(combat, player.GetComponent<PlayerInputReader>(), rage, glow);
        }

        // ---------- Hiệu ứng, số sát thương ----------

        static void BuildFx(Sprite glow)
        {
            var fx = new GameObject("Combat FX").AddComponent<CombatFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("alphaMaterial").objectReferenceValue = FxMaterial("FxAlpha", false, null);
            so.FindProperty("additiveMaterial").objectReferenceValue = FxMaterial("FxAdditive", true, null);
            // Vệt chém / vết cào: ảnh vẽ tay (Tools/art/fx_sheet.py) cho thân vệt, ảnh vẽ bằng code (FxSlash) cho quầng mờ
            Texture slashPaint = FxTexture("FxSlashPaint");
            Texture clawPaint = FxTexture("FxClawPaint");
            so.FindProperty("slashMaterial").objectReferenceValue = FxMaterial("FxSlashPaint", false, slashPaint);
            so.FindProperty("slashCoreMaterial").objectReferenceValue = FxMaterial("FxSlashPaintAdd", true, slashPaint);
            so.FindProperty("thinSlashMaterial").objectReferenceValue = FxMaterial("FxSlashThin", false, FxTexture("FxSlashThin"));
            so.FindProperty("haloMaterial").objectReferenceValue = FxMaterial("FxSlash", false, FxTexture("FxSlash"));
            so.FindProperty("clawMaterial").objectReferenceValue = FxMaterial("FxClawPaint", false, clawPaint);
            so.FindProperty("clawCoreMaterial").objectReferenceValue = FxMaterial("FxClawPaintAdd", true, clawPaint);
            so.FindProperty("plainLine").objectReferenceValue = ArtFactory.Material("Line", null);
            so.FindProperty("glowLine").objectReferenceValue = FxMaterial("FxGlowLine", true, LaserSkillBuilder.BeamMaterial().mainTexture);
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("flare").objectReferenceValue = LaserSkillBuilder.FlareSprite();
            so.FindProperty("star").objectReferenceValue = FxSprite("FxBurst", 256f);
            so.FindProperty("spark").objectReferenceValue = FxSprite("FxSpark", 128f);
            so.FindProperty("ring").objectReferenceValue = FxRing();
            so.FindProperty("impact").objectReferenceValue = FxSprite("FxImpact", 256f);
            so.FindProperty("shock").objectReferenceValue = FxSprite("FxShock", 512f);
            so.FindProperty("crack").objectReferenceValue = FxSprite("FxCrack", 512f);
            so.FindProperty("crackBurst").objectReferenceValue = FxSprite("FxCrackBurst", 512f, new Vector2(0.5f, 0.436f)); // gốc = chỗ nổ trên mặt đất
            SerializedProperty rocks = so.FindProperty("rocks");
            rocks.arraySize = 3;
            for (int i = 0; i < 3; i++)
                rocks.GetArrayElementAtIndex(i).objectReferenceValue = FxSprite($"FxRock{i}", 64f);
            so.FindProperty("spikes").objectReferenceValue = FxSprite("FxSpikes", 256f, new Vector2(0.5f, 0.06f));
            so.FindProperty("flame").objectReferenceValue = FxSprite("FxFlame", 128f, new Vector2(0.5f, 0.02f));
            so.FindProperty("puff").objectReferenceValue = FxSprite("FxSmoke", 256f);
            so.FindProperty("bubble").objectReferenceValue = FxSprite("FxBubble", 64f);
            so.FindProperty("drop").objectReferenceValue = FxSprite("FxDrop", 64f);
            so.FindProperty("swirl").objectReferenceValue = FxSprite("FxVortex", 512f);
            so.FindProperty("splat").objectReferenceValue = FxSprite("FxSplat", 128f);
            so.FindProperty("web").objectReferenceValue = FxWeb();
            so.FindProperty("cocoon").objectReferenceValue = FxCocoon();
            so.FindProperty("tentacle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Octopus/Rig/tentacle_m.png");
            so.FindProperty("spiderling").objectReferenceValue = FxSprite("FxSpiderling", 236f); // nhện con nhìn ngang, 2 dáng bước
            so.FindProperty("spiderlingStep").objectReferenceValue = FxSprite("FxSpiderlingStep", 236f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildDamageNumbers()
        {
            var go = new GameObject("Damage Numbers", typeof(RectTransform), typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            ((RectTransform)go.transform).sizeDelta = new Vector2(100f, 100f);
            go.transform.localScale = Vector3.one * 0.01f;
            var numbers = go.AddComponent<DamageNumbers>();
            var so = new SerializedObject(numbers);
            so.FindProperty("font").objectReferenceValue = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Bia tập ----------

        static void BuildDummy(Vector2 at, int layer)
        {
            var go = new GameObject("Training Dummy") { layer = layer };
            go.transform.position = at;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true; // không cản đường người chơi
            box.size = new Vector2(1f, 1.9f);
            box.offset = new Vector2(0f, 0.95f);
            go.AddComponent<Health>();

            Sprite white = WhiteSprite();
            SpriteRenderer art = RigArt.NewSprite("Art", go.transform, DummySprite(false), 2);
            SpriteRenderer flash = RigArt.NewSprite("Flash", art.transform, DummySprite(true), 3);
            flash.color = new Color(1f, 1f, 1f, 0f);

            SpriteRenderer web = RigArt.NewSprite("Web", go.transform, FxWeb(), 4);
            web.transform.localPosition = new Vector3(0f, 1f, 0f);
            web.transform.localScale = Vector3.one * 0.65f;
            web.enabled = false;
            SpriteRenderer cocoon = RigArt.NewSprite("Cocoon", go.transform, FxCocoon(), 5);
            cocoon.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            cocoon.transform.localScale = Vector3.one * 1.15f;
            cocoon.enabled = false;

            // Sao xoay trên đầu khi choáng
            Transform stars = RigArt.NewChild("Stars", go.transform);
            stars.localPosition = new Vector3(0f, 2.15f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                SpriteRenderer star = RigArt.NewSprite($"Star {i}", stars, LaserSkillBuilder.FlareSprite(), 22);
                star.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.35f, Mathf.Sin(a) * 0.12f, 0f);
                star.transform.localScale = Vector3.one * 0.3f;
                star.color = new Color(1f, 0.9f, 0.4f);
            }

            stars.gameObject.SetActive(false);

            // Thanh máu
            Transform bar = RigArt.NewChild("Health Bar", go.transform);
            bar.localPosition = new Vector3(0f, 2.4f, 0f);
            SpriteRenderer back = RigArt.NewSprite("Back", bar, white, 20);
            back.transform.localScale = new Vector3(1.2f, 0.15f, 1f);
            back.color = new Color(0.08f, 0.04f, 0.12f, 0.85f);
            Transform pivot = RigArt.NewChild("Fill", bar);
            pivot.localPosition = new Vector3(-0.57f, 0f, 0f);
            SpriteRenderer fill = RigArt.NewSprite("Bar", pivot, white, 21);
            fill.transform.localPosition = new Vector3(0.57f, 0f, 0f);
            fill.transform.localScale = new Vector3(1.14f, 0.09f, 1f);
            fill.color = new Color(1f, 0.35f, 0.45f);

            var dummy = go.AddComponent<TrainingDummy>();
            var so = new SerializedObject(dummy);
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("barFill").objectReferenceValue = pivot;
            so.FindProperty("bar").objectReferenceValue = bar.gameObject;
            so.FindProperty("web").objectReferenceValue = web;
            so.FindProperty("cocoon").objectReferenceValue = cocoon;
            so.FindProperty("stars").objectReferenceValue = stars;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- HUD ----------

        static void BuildHud(CombatController combat, PlayerInputReader input, RageMeter rage, Sprite glow)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var hud = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // màn ngang: cỡ nút theo chiều cao màn hình

            RectTransform safe = Rect("Safe Area", hud.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildPad(safe, combat, input, glow);
            BuildRageBar(safe, rage, glow);
            HeroBuilder.BuildHud(safe, combat, input, glow);
        }

        static void BuildPad(RectTransform safe, CombatController combat, PlayerInputReader input, Sprite glow)
        {
            Sprite disc = LaserSkillBuilder.DiscSprite();
            Sprite ring = LaserSkillBuilder.RingSprite();
            Sprite[] octopusIcons = Icons("Octopus");
            Sprite[] spiderIcons = Icons("Spider");
            Sprite triangle = HeroBuilder.TriangleSprite();

            RectTransform pad = Rect("Skill Pad", safe);
            pad.anchorMin = pad.anchorMax = pad.pivot = new Vector2(1f, 0f);
            pad.anchoredPosition = new Vector2(-50f, 50f);
            pad.sizeDelta = new Vector2(560f, 560f);
            var skillPad = pad.gameObject.AddComponent<SkillPad>();
            var so = new SerializedObject(skillPad);

            // Tâm từng nút tính từ góc dưới phải cụm nút: nút 2 ở góc, nút 1 bên trái nút 2, nút 3 phía trên nút 2
            Vector2[] centers = { new Vector2(-390f, 115f), new Vector2(-125f, 125f), new Vector2(-115f, 390f) };
            float[] sizes = { 185f, 215f, 185f };
            SerializedProperty list = so.FindProperty("buttons");
            list.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                RectTransform root = Rect($"Button {i + 1}", pad);
                root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
                root.anchoredPosition = centers[i];
                root.sizeDelta = Vector2.one * sizes[i];
                var hit = root.gameObject.AddComponent<Image>();
                hit.sprite = disc;
                hit.color = new Color(1f, 1f, 1f, 0f); // vùng bấm, không vẽ
                var padButton = root.gameObject.AddComponent<SkillPadButton>();
                var buttonSettings = new SerializedObject(padButton);
                buttonSettings.FindProperty("pad").objectReferenceValue = skillPad;
                buttonSettings.FindProperty("slot").intValue = i;
                buttonSettings.ApplyModifiedPropertiesWithoutUndo();

                RectTransform face = Rect("Face", root);
                Stretch(face);
                Image halo = Img("Halo", face, glow, -55f, new Color(0.55f, 0.85f, 1f, 0f));
                Img("Base", face, disc, 0f, new Color(0.07f, 0.04f, 0.15f, 0.9f));
                Image icon = Img("Icon", face, octopusIcons[i], 10f, Color.white);
                Image shade = Img("Shade", face, disc, 10f, new Color(0.02f, 0.01f, 0.06f, 0.68f));
                shade.type = Image.Type.Filled;
                shade.fillMethod = Image.FillMethod.Radial360;
                shade.fillOrigin = (int)Image.Origin360.Top;
                shade.fillClockwise = false;
                shade.fillAmount = 0f;
                Image rim = Img("Ring", face, ring, 0f, new Color(0.55f, 0.85f, 1f));
                Text label = Label("Label", face, "", i == 2 ? 44 : 60, FontStyle.Bold, Color.white);
                Stretch(label.rectTransform);

                // Số nút ở góc trên trái (để biết nút nào là 1, 2, 3)
                Image tag = Img("Tag", root, disc, 0f, new Color(0.12f, 0.07f, 0.24f, 0.95f));
                Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(54f, 54f));
                Text number = Label("Number", tag.transform, (i + 1).ToString(), 34, FontStyle.Bold, new Color(1f, 0.9f, 0.6f));
                Stretch(number.rectTransform);

                // Nhân vật người kéo cần lên / xuống: huy hiệu ▲ / ▼ góc trên phải và tên chiêu kiểu đó dưới nút
                Image badge = Img("Variant Badge", root, disc, 0f, new Color(1f, 0.82f, 0.3f));
                Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(66f, 66f));
                Image arrow = Img("Arrow", badge.transform, triangle, 15f, new Color(0.1f, 0.05f, 0.2f));
                badge.gameObject.SetActive(false);
                Text variantName = Label("Variant Name", root, "", 28, FontStyle.Bold, new Color(1f, 0.82f, 0.3f));
                Place(variantName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -28f), new Vector2(250f, 56f));
                variantName.horizontalOverflow = HorizontalWrapMode.Wrap; // tên dài xuống 2 dòng / thu nhỏ, không đè nút bên cạnh
                variantName.resizeTextForBestFit = true;
                variantName.resizeTextMinSize = 16;
                variantName.resizeTextMaxSize = 28;
                variantName.gameObject.SetActive(false);

                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("rect").objectReferenceValue = root;
                element.FindPropertyRelative("icon").objectReferenceValue = icon;
                element.FindPropertyRelative("shade").objectReferenceValue = shade;
                element.FindPropertyRelative("ring").objectReferenceValue = rim;
                element.FindPropertyRelative("halo").objectReferenceValue = halo;
                element.FindPropertyRelative("label").objectReferenceValue = label;
                element.FindPropertyRelative("badge").objectReferenceValue = badge;
                element.FindPropertyRelative("badgeArrow").objectReferenceValue = arrow;
                element.FindPropertyRelative("variantName").objectReferenceValue = variantName;
            }

            // 3 chấm mốc nộ phía trên nút 3
            SerializedProperty pips = so.FindProperty("tierPips");
            pips.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                Image pip = Img($"Rage Pip {i + 1}", pad, disc, 0f, new Color(0.25f, 0.2f, 0.3f));
                Place(pip.rectTransform, new Vector2(1f, 0f), centers[2] + new Vector2((i - 1) * 46f, 112f - Mathf.Abs(i - 1) * 8f), new Vector2(26f, 26f));
                pips.GetArrayElementAtIndex(i).objectReferenceValue = pip;
            }

            Text skillName = Label("Skill Name", pad, "", 46, FontStyle.Bold, new Color(1f, 0.9f, 0.6f));
            Place(skillName.rectTransform, new Vector2(1f, 0f), new Vector2(-330f, 525f), new Vector2(560f, 64f));
            skillName.color = new Color(1f, 0.9f, 0.6f, 0f);

            so.FindProperty("combat").objectReferenceValue = combat;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("skillName").objectReferenceValue = skillName;
            SetSprites(so.FindProperty("octopusIcons"), octopusIcons);
            SetSprites(so.FindProperty("spiderIcons"), spiderIcons);
            SetSprites(so.FindProperty("redIcons"), Icons("Red"));
            SetSprites(so.FindProperty("pinkIcons"), Icons("Pink"));
            SetSprites(so.FindProperty("blueIcons"), Icons("Blue"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildRageBar(RectTransform safe, RageMeter rage, Sprite glow)
        {
            Sprite white = WhiteSprite();
            RectTransform root = Rect("Rage Bar", safe);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(40f, -36f);
            root.sizeDelta = new Vector2(600f, 76f);
            var bar = root.gameObject.AddComponent<RageBar>();

            Image halo = Img("Glow", root, glow, -45f, new Color(1f, 0.5f, 0.3f, 0f));
            Image back = Img("Back", root, PanelSprite(), 0f, new Color(0.08f, 0.04f, 0.12f, 0.88f));
            back.type = Image.Type.Sliced;
            Text label = Label("Label", root, "NỘ", 38, FontStyle.Bold, new Color(1f, 0.8f, 0.55f));
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(75f, 0f), new Vector2(130f, 60f));

            RectTransform track = Rect("Track", root);
            track.anchorMin = Vector2.zero;
            track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(145f, 20f);
            track.offsetMax = new Vector2(-20f, -20f);
            Img("Track Back", track, white, 0f, new Color(0.16f, 0.1f, 0.22f));
            Image fill = Img("Fill", track, white, 0f, new Color(1f, 0.62f, 0.25f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            var so = new SerializedObject(bar);
            SerializedProperty marks = so.FindProperty("marks");
            marks.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                RectTransform mark = Rect($"Mark {i + 1}", track);
                mark.anchorMin = mark.anchorMax = new Vector2((i + 1) / 3f, 0.5f);
                mark.sizeDelta = new Vector2(7f, 56f);
                var image = mark.gameObject.AddComponent<Image>();
                image.sprite = white;
                image.raycastTarget = false;
                marks.GetArrayElementAtIndex(i).objectReferenceValue = image;
            }

            so.FindProperty("rage").objectReferenceValue = rage;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("glow").objectReferenceValue = halo;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static Image Img(string name, Transform parent, Sprite sprite, float inset, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Stretch(rect, inset);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Sprite[] Icons(string character)
        {
            var icons = new Sprite[3];
            for (int i = 0; i < 3; i++)
                icons[i] = ImportSprite($"{UiFolder}/Skill_{character}_{i + 1}.png");
            return icons;
        }

        static void SetSprites(SerializedProperty property, Sprite[] sprites)
        {
            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        // ---------- Art vẽ bằng code ----------

        internal static Sprite WhiteSprite() => ArtFactory.Sprite("UiWhite", 8, 8, 8f, new Vector2(0.5f, 0.5f), (u, v) => Color.white, 1, false);

        // Ảnh hiệu ứng: vẽ bằng code (Tools/art/fx_textures.py) hoặc cắt từ bảng vẽ tay (Tools/art/fx_sheet.py); đều là ảnh
        // xám / trắng + alpha, CombatFx tô màu lúc chạy.
        // Cỡ (unit) giữ như bản cũ: vòng sóng, lưới tơ 2 unit; kén 1.28 x 2 unit
        static Sprite FxRing() => FxSprite("FxRing", 128f);
        static Sprite FxWeb() => FxSprite("FxWeb", 256f);
        static Sprite FxCocoon() => FxSprite("FxCocoon", 150f);

        static Sprite FxSprite(string name, float pixelsPerUnit) => FxSprite(name, pixelsPerUnit, new Vector2(0.5f, 0.5f));

        static Sprite FxSprite(string name, float pixelsPerUnit, Vector2 pivot)
        {
            string path = $"{ArtFactory.Folder}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Octopus] Chưa có {path} (chạy Tools/art/fx_textures.py)");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Ảnh thường (không phải sprite) cho mesh vệt chém
        static Texture2D FxTexture(string name)
        {
            string path = $"{ArtFactory.Folder}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Octopus] Chưa có {path} (chạy Tools/art/fx_textures.py)");
                return null;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Vật liệu hiệu ứng: additive = cộng sáng (shader hạt dựng sẵn của Unity, URP 2D vẽ được vì pass không gắn LightMode),
        // còn lại = sprite không chịu ánh sáng, hoà trộn thường
        internal static Material FxMaterial(string name, bool additive, Texture texture)
        {
            Shader shader = additive
                ? FindShader("Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive")
                : FindShader("Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default");
            string path = $"{ArtFactory.Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
                material.shader = shader;

            material.mainTexture = texture;
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // shader nhân đôi: 0.5 = giữ nguyên màu
            EditorUtility.SetDirty(material);
            return material;
        }

        static Shader FindShader(string name, string fallback)
        {
            Shader shader = Shader.Find(name);
            return shader != null ? shader : Shader.Find(fallback);
        }

        // Bia tập: tượng đá có vòng bia và lõi pha lê ở ngực. white = bản trắng để loé lên khi trúng đòn
        static Sprite DummySprite(bool white) => ArtFactory.Sprite(white ? "DummyFlash" : "Dummy", 160, 260, 130f, new Vector2(0.5f, 0f), (u, v) =>
        {
            float px = (u - 0.5f) * 160f, py = v * 260f;
            bool Inside(float x, float y)
            {
                bool slab = Mathf.Abs(x) < 70f && y < 24f;
                bool body = x * x / (50f * 50f) + (y - 122f) * (y - 122f) / (98f * 98f) < 1f;
                bool neck = Mathf.Abs(x) < 20f && y > 196f && y < 214f;
                bool head = x * x + (y - 228f) * (y - 228f) < 28f * 28f;
                return slab || body || neck || head;
            }

            if (!Inside(px, py))
                return Color.clear;
            if (white)
                return Color.white;

            bool rim = !Inside(px - 4f, py) || !Inside(px + 4f, py) || !Inside(px, py - 4f) || !Inside(px, py + 4f);
            float light = 0.85f + 0.25f * (-px / 60f) + 0.1f * Mathf.Sin(px * 0.21f + py * 0.13f);
            var stone = new Color(0.32f, 0.29f, 0.42f) * light;
            if (rim)
                stone = new Color(0.12f, 0.08f, 0.18f);

            // Vòng bia + lõi pha lê ở ngực
            float d = new Vector2(px, py - 128f).magnitude;
            if (Mathf.Abs(d - 34f) < 3f || Mathf.Abs(d - 20f) < 2.5f)
                stone = Color.Lerp(stone, new Color(0.45f, 0.85f, 1f), 0.85f);
            float diamond = Mathf.Abs(px) / 10f + Mathf.Abs(py - 128f) / 14f;
            if (diamond < 1f)
                stone = Color.Lerp(new Color(0.55f, 0.95f, 1f), Color.white, 1f - diamond);
            stone.a = 1f;
            return stone;
        }, 2, false);
    }
}
