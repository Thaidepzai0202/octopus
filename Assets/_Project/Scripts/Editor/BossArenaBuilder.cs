using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Octopus.EditorTools.CharacterSelectBuilder;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng scene BossArena "Hang Hắc Viêm" (Docs/boss_su_tu/PHAN_TICH_BOSS.md mục 6–7):
    ///  - Sàn đấu rộng 56 unit, vách đá hai đầu, trần hang; 13 tảng đá treo 3 tầng (chỗ bám cao 5 / 9 / 13 unit trên sàn), xếp
    ///    so le để đu nối từ tảng này sang tảng kia; tầng cao nối liền từ trái sang phải là lối đu vòng qua đầu boss.
    ///  - Không khí đỏ cam: dùng lại tranh hang động (Art/Drawn) nhuộm lại thành tông than hồng (thác nước thành thác dung nham),
    ///    sàn có khe dung nham phát sáng, tàn lửa bay lên, quầng đỏ + ánh đèn đỏ sau lưng boss.
    ///  - Người chơi như màn thường (bạch tuộc / nhện / 3 nhân vật người, bộ chiêu, nộ, HUD, nút Đổi nhân vật) + máu 300 (PlayerHealth).
    ///  - Boss sư tử (LionBoss) với hình do LionRigBuilder dựng, cao ≈ 5 lần nhân vật.
    ///  - Camera lùi xa hơn màn thường, bám giữa người chơi và boss (BossArenaCamera); rung camera vẫn chạy.
    ///  - Thanh máu boss giữa trên, thanh máu người chơi dưới thanh nộ, màn THẤT BẠI / CHIẾN THẮNG.
    /// Chưa có tranh riêng cho đấu trường (Docs/boss_su_tu/dau_truong/) nên dùng tranh hang động nhuộm lại.
    /// </summary>
    static class BossArenaBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/BossArena.unity";

        // ---- Bố cục ----
        public const float FloorY = 0f;
        public const float ArenaHalfWidth = 28f; // sàn 56 unit giữa hai vách
        const float CeilingY = 18.5f;            // mặt dưới trần (collider)
        const float WallThickness = 8f;

        /// <summary>
        /// Chiều cao boss. Bạch tuộc đứng cao ≈ 1.95 unit (thân + xúc tu), nhện ≈ 1.4 → boss 9.5 ≈ 5 lần bạch tuộc.
        /// Tầng đá giữa (9) vừa ngang đỉnh đầu boss, tầng thấp (5) ngang ngực: vuốt với tới tầng thấp, không tới tầng giữa.
        /// </summary>
        public const float BossHeight = 9.5f;

        public const float BossMaxHealth = 6000f;
        public const float PlayerMaxHealth = 300f;

        static readonly Vector2 PlayerSpawn = new Vector2(-19f, FloorY + 0.6f);
        static readonly Vector2 BossSpawn = new Vector2(12f, FloorY);

        /// <summary>Tầng đá: mặt dưới (chỗ bám) cao bấy nhiêu trên sàn.</summary>
        public static readonly float[] Tiers = { 5f, 9f, 13f };

        // 13 tảng đá: x, tầng, bề rộng, lệch cao. Tầng thấp cách nhau 11 unit, tầng giữa nằm giữa hai tảng thấp (đá thấp ↔ đá
        // giữa kề nhau theo đường chéo ~6 unit < tầm xúc tu 7, bò ra mép đá rồi bắn). Tầng cao cách nhau 6–7.5 unit, đu nối liền
        // từ trái sang phải trên đầu boss
        static readonly (float x, int tier, float width, float jitter)[] Rocks =
        {
            (-22f, 0, 2.6f, 0.15f), (-11f, 0, 2.8f, -0.2f), (0f, 0, 2.6f, 0.1f), (11f, 0, 2.8f, -0.1f), (22f, 0, 2.6f, 0.2f),
            (-16.5f, 1, 2.6f, -0.15f), (-5.5f, 1, 2.8f, 0.2f), (5.5f, 1, 2.6f, -0.1f), (16.5f, 1, 2.8f, 0.15f),
            (-10.5f, 2, 2.8f, 0.1f), (-3f, 2, 2.8f, -0.15f), (3f, 2, 2.8f, 0.2f), (10.5f, 2, 2.8f, -0.1f),
        };

        // ---- Màu: than hồng, dung nham ----
        static readonly Color CameraBackground = CaveBuilder.Hex("120404");
        static readonly Color Ember = new Color(1f, 0.45f, 0.12f);
        static readonly Color Lava = new Color(1f, 0.55f, 0.15f);
        static readonly Color RimIdle = new Color(1f, 0.45f, 0.15f, 0.6f);
        static readonly Color RimReach = new Color(1f, 0.85f, 0.45f, 1f);

        // Tranh hang động (Art/Drawn) nhuộm lại: tên file, hệ số trôi (parallax), cao (unit), lệch so với giữa khung hình,
        // độ sáng, thứ tự vẽ
        static readonly (string file, float factor, float height, float offset, float shade, int order)[] Layers =
        {
            ("deep", 0.92f, 21f, 0.5f, 0.62f, -950),
            ("far", 0.8f, 13f, -3.5f, 0.55f, -900),
            ("mid", 0.55f, 8.5f, -6.5f, 0.42f, -800),
            ("near", 0.3f, 7f, -9.2f, 0.32f, -700),
        };

        const float CameraMidY = 7.5f;

        [MenuItem("Octopus/Dựng lại màn đánh boss (ghi đè scene BossArena)", priority = 2)]
        static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            string previous = EditorSceneManager.GetActiveScene().path;
            Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFactory.Folder + "/Glow.png");
            var glowLine = AssetDatabase.LoadAssetAtPath<Material>(ArtFactory.Folder + "/GlowLine.mat");
            var plainLine = AssetDatabase.LoadAssetAtPath<Material>(ArtFactory.Folder + "/Line.mat");
            if (glow == null || glowLine == null || plainLine == null)
            {
                EditorUtility.DisplayDialog("Octopus", "Chưa có art dùng chung. Chạy menu Octopus → Dựng lại màn chơi trước.", "OK");
                return;
            }

            BuildScene(glow, glowLine, plainLine);
            AddToBuildSettings();
            if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
                EditorSceneManager.OpenScene(previous);
            Debug.Log($"[Octopus] Đã dựng lại {ScenePath}");
        }

        static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void BuildScene(Sprite glow, Material glowLine, Material plainLine)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int anchorable = LayerMask.NameToLayer(ProjectSetup.AnchorableLayer);
            int enemy = LayerMask.NameToLayer(ProjectSetup.EnemyLayer);
            int hazard = LayerMask.NameToLayer(ProjectSetup.HazardLayer);
            int playerLayer = LayerMask.NameToLayer(ProjectSetup.PlayerLayer);

            Camera camera = ProjectSetup.CreateCamera();
            camera.backgroundColor = CameraBackground;
            ProjectSetup.CreateGlobalLight();
            Material additive = CombatBuilder.FxMaterial("FxAdditive", true, null);

            BuildBackdrop(camera);
            BuildLayers();
            Transform level = new GameObject("Level").transform;
            BuildFloor(level, additive);
            BuildWalls(level);
            BuildCeiling(level);
            BuildRocks(level, glow, glowLine, anchorable);
            BuildKillZone(level, hazard);
            BuildEmbers(glow);

            // Người chơi: như màn thường, thêm máu
            GameObject player = OctopusBuilder.Build(plainLine, glow, ProjectSetup.CreateBodyMaterial(), playerLayer,
                LayerMask.GetMask(ProjectSetup.AnchorableLayer), LayerMask.GetMask("Default", ProjectSetup.AnchorableLayer));
            player.transform.position = PlayerSpawn;
            LaserSkillBuilder.Build(player, glow, LayerMask.GetMask("Default", ProjectSetup.AnchorableLayer));
            CombatBuilder.Build(player, glow, enemy, trainingDummies: false);
            SpiderBuilder.Build(player.GetComponent<OctopusController>(), glow);
            HeroBuilder.Build(player);
            var playerHealth = AddPlayerHealth(player);
            var safe = GameObject.Find("HUD/Safe Area").GetComponent<RectTransform>();
            AddBackButton(safe, player.GetComponent<PlayerInputReader>());

            LionBoss boss = BuildBoss(playerHealth, glow, additive, enemy, LayerMask.GetMask("Default", ProjectSetup.AnchorableLayer),
                LayerMask.GetMask(ProjectSetup.AnchorableLayer));
            BuildCamera(player, boss);
            GameFeelBuilder.Build(); // khựng hình, rung camera

            BuildPlayerHealthBar(safe, playerHealth, glow);
            BuildBossBar(safe, boss, glow);
            BuildResultOverlay(boss, playerHealth);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        }

        // =============================================================================================================
        // Nền
        // =============================================================================================================

        // Phông xa nhất dính theo camera: đỏ sẫm, quầng đỏ cam giữa (ánh lửa phía sau đấu trường)
        static void BuildBackdrop(Camera camera)
        {
            Color top = CaveBuilder.Hex("0b0203");
            Color middle = CaveBuilder.Hex("3a0b07");
            Color bottom = CaveBuilder.Hex("1c0504");
            Color light = CaveBuilder.Hex("b8360f");
            Sprite sprite = ArtFactory.Sprite("Arena_Backdrop", 256, 128, 128f, new Vector2(0.5f, 0.5f), (u, v) =>
            {
                Color c = v > 0.45f ? Color.Lerp(middle, top, (v - 0.45f) / 0.55f) : Color.Lerp(bottom, middle, v / 0.45f);
                float glowAmount = Blob(u, v, 0.5f, 0.4f, 0.3f, 0.3f) + 0.6f * Blob(u, v, 0.18f, 0.3f, 0.12f, 0.2f) +
                                   0.6f * Blob(u, v, 0.83f, 0.33f, 0.12f, 0.2f);
                glowAmount *= 0.75f + 0.5f * Noise.Fbm(u * 6f, v * 3f, 17);
                return Color.Lerp(c, light, Mathf.Clamp01(glowAmount) * 0.7f);
            }, 1, false);

            var go = new GameObject("Backdrop");
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 30f);
            go.transform.localScale = new Vector3(36f, 36f, 1f); // 72 x 36 unit: đủ cho khung lùi xa nhất
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -1000;
        }

        // Các lớp tranh hang động nhuộm lại, trôi theo camera (ParallaxLayer) như màn thường
        static void BuildLayers()
        {
            foreach ((string file, float factor, float height, float offset, float shade, int order) in Layers)
            {
                var go = new GameObject($"BG {char.ToUpper(file[0])}{file.Substring(1)}");
                var parallax = go.AddComponent<ParallaxLayer>();
                var settings = new SerializedObject(parallax);
                settings.FindProperty("factorX").floatValue = factor;
                settings.FindProperty("factorY").floatValue = factor;
                settings.ApplyModifiedPropertiesWithoutUndo();

                Sprite sprite = Emberize(file, new Vector2(0.5f, 0.5f));
                if (sprite == null)
                    continue;
                float scale = height / sprite.bounds.size.y;
                float tile = sprite.bounds.size.x * scale - 0.02f;
                float y = CameraMidY * (1f - factor) + offset;
                const float range = 48f;
                int index = 0;
                for (float x = -range - tile * 0.5f; x < range + tile; x += tile, index++)
                {
                    SpriteRenderer r = CaveBuilder.Place(go.transform, sprite, new Vector2(x, y), Vector2.one * scale, new Color(shade, shade, shade), order);
                    r.flipX = index % 2 == 1; // lặp soi gương cho mép nối liền
                }
            }
        }

        // =============================================================================================================
        // Sàn, vách, trần, đá
        // =============================================================================================================

        static void BuildFloor(Transform level, Material additive)
        {
            var go = new GameObject("Arena Floor");
            go.transform.SetParent(level, false);
            go.transform.position = new Vector2(0f, FloorY);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(ArenaHalfWidth * 2f + WallThickness * 2f, 6f);
            box.offset = new Vector2(0f, -3f);

            const float width = 72f, depth = 9f;
            Sprite rock = ArtFactory.Sprite("Arena_Floor", 1152, 144, 16f, new Vector2(0.5f, 1f), (u, v) =>
            {
                float x = (u - 0.5f) * width, y = (v - 1f) * depth;
                float top = FloorTop(x);
                if (y > top)
                    return Color.clear;
                float d = top - y;
                float shade = 0.7f + 0.35f * Noise.Fbm(x * 0.45f, y * 0.9f, 41) - 0.25f * Mathf.Clamp01(d / depth);
                float seam = Mathf.Abs(Noise.Fbm(x * 0.22f, y * 0.6f, 43) - 0.5f);
                if (seam < 0.014f)
                    shade *= 0.55f;
                Color c = new Color(0.2f, 0.075f, 0.06f) * shade;
                if (d < 0.14f)
                    c = Color.Lerp(c, new Color(0.85f, 0.35f, 0.13f), 1f - d / 0.14f); // mép sàn hắt lửa
                c.a = 1f;
                return c;
            }, 1, false);
            CaveBuilder.Place(go.transform, rock, Vector2.zero, Vector2.one, Color.white, -100).name = "Floor Art";

            // Khe dung nham: lớp cộng sáng phập phồng
            Sprite lava = ArtFactory.Sprite("Arena_FloorLava", 1152, 144, 16f, new Vector2(0.5f, 1f), (u, v) =>
            {
                float x = (u - 0.5f) * width, y = (v - 1f) * depth;
                float d = FloorTop(x) - y;
                if (d < 0.3f)
                    return Color.clear;
                float vein = Mathf.Abs(Noise.Fbm(x * 0.16f, y * 0.4f, 47) - 0.5f);
                float patch = Mathf.Clamp01((Noise.Fbm(x * 0.09f, 3.7f, 49) - 0.3f) * 5f); // khe chỉ có từng quãng
                float line = Mathf.Clamp01(1f - vein / 0.016f) * patch;
                float halo = Mathf.Exp(-vein * vein / 0.0015f) * 0.4f * patch;
                float depthFade = Mathf.Lerp(1f, 0.2f, Mathf.Clamp01((d - 0.3f) / 3.5f));
                float a = Mathf.Clamp01(Mathf.Max(line, halo)) * depthFade;
                Color c = Color.Lerp(new Color(0.95f, 0.25f, 0.06f), new Color(1f, 0.7f, 0.3f), line * 0.7f);
                c.a = a;
                return c;
            }, 1, false);
            SpriteRenderer glowCracks = CaveBuilder.Place(go.transform, lava, Vector2.zero, Vector2.one, Color.white, -99);
            glowCracks.name = "Lava Cracks";
            glowCracks.sharedMaterial = additive;
            glowCracks.gameObject.AddComponent<ArenaGlow>();
        }

        static float FloorTop(float x) => 0.04f * Noise.Edge(x * 0.9f, 31) - 0.01f;

        static void BuildWalls(Transform level)
        {
            const float bottom = -9f, top = CeilingY + 9f;
            float height = top - bottom;
            Sprite wall = ArtFactory.Sprite("Arena_Wall", 160, 576, 16f, new Vector2(0.25f, 0f), (u, v) =>
            {
                float x = (u - 0.25f) * WallThickness * 1.25f, y = bottom + v * height; // nhô vào trong sân tối đa ~2 unit
                float face = 2.4f * Noise.Edge(y * 0.22f, 61) + 0.8f * Noise.Edge(y * 0.9f, 62) - 0.1f;
                if (x < face)
                    return Color.clear;
                float d = x - face;
                float shade = 0.65f + 0.35f * Noise.Fbm(x * 0.6f, y * 0.35f, 63) - 0.2f * Mathf.Clamp01(d / WallThickness);
                if (Mathf.Abs(Noise.Fbm(x * 0.3f, y * 0.25f, 65) - 0.5f) < 0.016f)
                    shade *= 0.55f;
                Color c = new Color(0.16f, 0.06f, 0.05f) * shade;
                if (d < 0.18f)
                    c = Color.Lerp(c, new Color(0.75f, 0.28f, 0.1f), 1f - d / 0.18f);
                c.a = 1f;
                return c;
            }, 1, false);

            for (int side = -1; side <= 1; side += 2)
            {
                var go = new GameObject(side < 0 ? "Wall Left" : "Wall Right");
                go.transform.SetParent(level, false);
                go.transform.position = new Vector2(side * ArenaHalfWidth, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(WallThickness, height);
                box.offset = new Vector2(side * WallThickness * 0.5f, bottom + height * 0.5f);
                SpriteRenderer art = CaveBuilder.Place(go.transform, wall, new Vector2(0f, bottom), Vector2.one, Color.white, -98);
                art.name = "Wall Art";
                if (side < 0)
                    art.transform.localScale = new Vector3(-1f, 1f, 1f); // vách trái: lật gương, mặt vách hướng vào trong
            }
        }

        static void BuildCeiling(Transform level)
        {
            var go = new GameObject("Ceiling");
            go.transform.SetParent(level, false);
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2(0f, CeilingY + 2f);
            box.size = new Vector2(ArenaHalfWidth * 2f + WallThickness * 2f, 4f);

            Sprite sprite = Emberize("ceiling", new Vector2(0.5f, 1f));
            if (sprite == null)
                return;
            var art = new GameObject("Ceiling Art").transform;
            art.SetParent(go.transform, false);
            const float artHeight = 8.5f;
            float scale = artHeight / sprite.bounds.size.y;
            float tile = sprite.bounds.size.x * scale - 0.02f;
            int index = 0;
            for (float x = -50f; x < 50f + tile; x += tile, index++)
            {
                SpriteRenderer r = CaveBuilder.Place(art, sprite, new Vector2(x, CeilingY + 7f), Vector2.one * scale, new Color(0.6f, 0.6f, 0.6f), -105);
                r.flipX = index % 2 == 1;
            }
        }

        static void BuildRocks(Transform level, Sprite glow, Material glowLine, int anchorableLayer)
        {
            Sprite drawn = Emberize("rock", new Vector2(0.5f, 0f));
            for (int i = 0; i < Rocks.Length; i++)
            {
                (float x, int tier, float width, float jitter) = Rocks[i];
                float bottom = FloorY + Tiers[tier] + jitter;
                var go = new GameObject($"Rock {i + 1:00}") { layer = anchorableLayer };
                go.transform.SetParent(level, false);
                go.transform.position = new Vector2(x, bottom + 0.5f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(width, 1f);

                if (drawn != null)
                {
                    // Như DrawnArt.ApplyRocks: mép dưới ảnh = chỗ bám, rộng hơn collider một chút
                    float scale = (width + 0.3f) / drawn.bounds.size.x * 1.15f;
                    float surface = 0.06f * drawn.bounds.size.y * scale;
                    SpriteRenderer art = CaveBuilder.Place(go.transform, drawn, new Vector2(0f, -0.5f - 0.05f - surface), Vector2.one * scale, Color.white, -100);
                    art.name = "Rock Art";
                    art.flipX = i % 2 == 1;
                }
                else
                {
                    List<Vector2> outline = CaveBuilder.RockOutline(width, 900 + i * 17, out _);
                    Rect area = CaveBuilder.Expand(Polygon.Bounds(outline), 0.2f);
                    Sprite sprite = CaveBuilder.Silhouette($"Arena_Rock_{i + 1:00}", area, 40f, Vector2.zero, p => Polygon.Contains(outline, p),
                        new Vector2(0.05f, -0.2f), 900 + i * 17);
                    CaveBuilder.Place(go.transform, sprite, Vector2.zero, Vector2.one, new Color(0.45f, 0.2f, 0.16f), -100).name = "Rock Art";
                }

                CaveBuilder.Place(go.transform, glow, new Vector2(0f, -0.75f), new Vector2(width + 1.6f, 1.6f), new Color(1f, 0.4f, 0.12f, 0.22f), -102);
                var rim = CaveBuilder.Rim(go.transform, "Glow Rim", new List<Vector2> { new Vector2(-width / 2f, -0.5f), new Vector2(width / 2f, -0.5f) },
                    glowLine, RimIdle, 0.22f, -90);
                var component = go.AddComponent<Rock>();
                var settings = new SerializedObject(component);
                settings.FindProperty("glowRim").objectReferenceValue = rim;
                settings.FindProperty("idleColor").colorValue = RimIdle;
                settings.FindProperty("inReachColor").colorValue = RimReach;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void BuildKillZone(Transform level, int hazardLayer)
        {
            // Lọt qua sàn (lỗi vật lý hiếm gặp) thì về chỗ xuất phát
            var go = new GameObject("Kill Zone") { layer = hazardLayer };
            go.transform.SetParent(level, false);
            go.transform.position = new Vector2(0f, FloorY - 12f);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(120f, 2f);
            collider.isTrigger = true;
            go.AddComponent<KillZone>();
        }

        // Tàn lửa bay lên
        static void BuildEmbers(Sprite glow)
        {
            var motes = new GameObject("Embers").AddComponent<AmbientMotes>();
            var settings = new SerializedObject(motes);
            settings.FindProperty("sprite").objectReferenceValue = glow;
            settings.FindProperty("count").intValue = 80;
            settings.FindProperty("area").vector2Value = new Vector2(64f, 32f);
            settings.FindProperty("sizeRange").vector2Value = new Vector2(0.07f, 0.22f);
            settings.FindProperty("color").colorValue = new Color(1f, 0.5f, 0.15f, 0.85f);
            settings.FindProperty("riseSpeed").floatValue = 1.3f;
            settings.FindProperty("swayAmount").floatValue = 0.8f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // =============================================================================================================
        // Người chơi, boss, camera
        // =============================================================================================================

        static PlayerHealth AddPlayerHealth(GameObject player)
        {
            var health = player.AddComponent<Health>();
            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").floatValue = PlayerMaxHealth;
            so.ApplyModifiedPropertiesWithoutUndo();
            return player.AddComponent<PlayerHealth>();
        }

        static LionBoss BuildBoss(PlayerHealth player, Sprite glow, Material additive, int enemyLayer, LayerMask surfaces, LayerMask rocks)
        {
            var root = new GameObject("Lion Boss");
            root.transform.position = BossSpawn;
            ILionVisual visual = LionRigBuilder.Build(root.transform, BossHeight);

            // Hitbox thân: máu + collider (trigger, không cản đường), lớp Enemy để chiêu người chơi đánh trúng
            var hurt = new GameObject("Hurtbox") { layer = enemyLayer };
            hurt.transform.SetParent(root.transform, false);
            hurt.transform.localPosition = new Vector3(0f, BossHeight * 0.5f, 0f);
            var box = hurt.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(BossHeight * 1.1f, BossHeight * 0.9f);
            var health = hurt.AddComponent<Health>();
            var healthSettings = new SerializedObject(health);
            healthSettings.FindProperty("maxHealth").floatValue = BossMaxHealth;
            healthSettings.ApplyModifiedPropertiesWithoutUndo();

            // Quầng đỏ sau lưng (sau đá, trước các lớp nền) + đèn đỏ hắt lên sàn / đá quanh boss
            SpriteRenderer backLight = CaveBuilder.Place(root.transform, glow, new Vector2(0f, BossHeight * 0.45f),
                new Vector2(BossHeight * 3f, BossHeight * 2.2f), new Color(1f, 0.22f, 0.06f, 0.35f), -130);
            backLight.name = "Back Light";
            backLight.sharedMaterial = additive;
            var lamp = new GameObject("Ember Light").AddComponent<Light2D>();
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, BossHeight * 0.4f, 0f);
            lamp.lightType = Light2D.LightType.Point;
            lamp.color = new Color(1f, 0.32f, 0.12f);
            lamp.intensity = 0.85f;
            lamp.pointLightInnerRadius = 2f;
            lamp.pointLightOuterRadius = BossHeight * 1.7f;

            // Vòng sao choáng trên đầu
            Transform stars = RigArt.NewChild("Stun Stars", root.transform);
            Sprite star = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFactory.Folder + "/DizzyStar.png");
            if (star == null)
                star = LaserSkillBuilder.FlareSprite();
            for (int i = 0; i < 5; i++)
            {
                SpriteRenderer s = RigArt.NewSprite($"Star {i}", stars, star, 26);
                s.color = new Color(1f, 0.9f, 0.45f);
            }

            stars.gameObject.SetActive(false);

            var hazards = root.AddComponent<LionHazards>();
            var hazardSettings = new SerializedObject(hazards);
            hazardSettings.FindProperty("markerSprite").objectReferenceValue = MarkerSprite();
            hazardSettings.FindProperty("glowSprite").objectReferenceValue = glow;
            hazardSettings.FindProperty("fireballSprite").objectReferenceValue = FireballSprite();
            hazardSettings.FindProperty("additive").objectReferenceValue = additive;
            hazardSettings.FindProperty("rockMask").intValue = rocks;
            hazardSettings.ApplyModifiedPropertiesWithoutUndo();

            var boss = root.AddComponent<LionBoss>();
            var so = new SerializedObject(boss);
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("hurtbox").objectReferenceValue = box;
            so.FindProperty("target").objectReferenceValue = player;
            so.FindProperty("visual").objectReferenceValue = visual as MonoBehaviour;
            so.FindProperty("backLight").objectReferenceValue = backLight;
            so.FindProperty("stunStars").objectReferenceValue = stars;
            so.FindProperty("surfaceMask").intValue = surfaces;
            so.FindProperty("height").floatValue = BossHeight;
            so.FindProperty("floorY").floatValue = FloorY;
            so.FindProperty("arenaX").vector2Value = new Vector2(-ArenaHalfWidth, ArenaHalfWidth);
            so.FindProperty("ceilingY").floatValue = CeilingY;
            so.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        static void BuildCamera(GameObject player, LionBoss boss)
        {
            var target = new GameObject("Camera Target");
            target.transform.position = new Vector3(PlayerSpawn.x, CameraMidY, 0f);

            var go = new GameObject("Follow Camera");
            var camera = go.AddComponent<CinemachineCamera>();
            camera.Follow = target.transform;
            LensSettings lens = LensSettings.Default;
            lens.OrthographicSize = 10f;
            camera.Lens = lens;
            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(0.5f, 0.6f, 0f);
            composer.Lookahead = new LookaheadSettings { Enabled = false };

            var framing = target.AddComponent<BossArenaCamera>();
            var so = new SerializedObject(framing);
            so.FindProperty("player").objectReferenceValue = player.GetComponent<Rigidbody2D>();
            so.FindProperty("boss").objectReferenceValue = boss;
            so.FindProperty("followCamera").objectReferenceValue = camera;
            so.FindProperty("centerY").floatValue = CameraMidY;
            so.FindProperty("arenaX").vector2Value = new Vector2(-ArenaHalfWidth, ArenaHalfWidth);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =============================================================================================================
        // Giao diện
        // =============================================================================================================

        // Thanh máu người chơi: ngay dưới thanh nộ (thanh nộ ở (40, -36), cao 76)
        static void BuildPlayerHealthBar(RectTransform safe, PlayerHealth player, Sprite glow)
        {
            Sprite white = CombatBuilder.WhiteSprite();
            RectTransform root = Rect("Player Health Bar", safe);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(40f, -122f);
            root.sizeDelta = new Vector2(600f, 62f);
            var bar = root.gameObject.AddComponent<PlayerHealthBar>();

            Image halo = CombatBuilder.Img("Glow", root, glow, -40f, new Color(1f, 0.25f, 0.25f, 0f));
            Image back = CombatBuilder.Img("Back", root, PanelSprite(), 0f, new Color(0.08f, 0.04f, 0.12f, 0.88f));
            back.type = Image.Type.Sliced;
            Text title = Label("Label", root, "MÁU", 34, FontStyle.Bold, new Color(0.65f, 1f, 0.7f));
            Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(75f, 0f), new Vector2(130f, 50f));

            RectTransform track = Rect("Track", root);
            track.anchorMin = Vector2.zero;
            track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(145f, 16f);
            track.offsetMax = new Vector2(-110f, -16f);
            CombatBuilder.Img("Track Back", track, white, 0f, new Color(0.16f, 0.1f, 0.22f));
            Image trail = Filled(CombatBuilder.Img("Trail", track, white, 0f, new Color(1f, 0.95f, 0.85f)));
            Image fill = Filled(CombatBuilder.Img("Fill", track, white, 0f, new Color(0.35f, 0.95f, 0.55f)));
            Text number = Label("Number", root, "300", 36, FontStyle.Bold, Color.white);
            Place(number.rectTransform, new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(100f, 50f));

            var so = new SerializedObject(bar);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("trail").objectReferenceValue = trail;
            so.FindProperty("glow").objectReferenceValue = halo;
            so.FindProperty("label").objectReferenceValue = number;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Thanh máu boss giữa phía trên: tên, thanh máu + thanh máu vừa mất, vạch 60% / 30%, dòng trạng thái
        static void BuildBossBar(RectTransform safe, LionBoss boss, Sprite glow)
        {
            Sprite white = CombatBuilder.WhiteSprite();
            RectTransform root = Rect("Boss Bar", safe);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -34f);
            root.sizeDelta = new Vector2(900f, 120f);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var bar = root.gameObject.AddComponent<BossBar>();

            Text name = Label("Name", root, LionBoss.DisplayName, 40, FontStyle.Bold, new Color(1f, 0.85f, 0.6f));
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.anchoredPosition = Vector2.zero;
            name.rectTransform.sizeDelta = new Vector2(0f, 48f);

            RectTransform frame = Rect("Frame", root);
            frame.anchorMin = new Vector2(0f, 1f);
            frame.anchorMax = new Vector2(1f, 1f);
            frame.pivot = new Vector2(0.5f, 1f);
            frame.anchoredPosition = new Vector2(0f, -50f);
            frame.sizeDelta = new Vector2(0f, 42f);
            Image halo = CombatBuilder.Img("Glow", frame, glow, -36f, new Color(1f, 0.25f, 0.1f, 0.25f));
            Image back = CombatBuilder.Img("Back", frame, PanelSprite(), -6f, new Color(0.1f, 0.03f, 0.03f, 0.9f));
            back.type = Image.Type.Sliced;

            RectTransform track = Rect("Track", frame);
            CharacterSelectBuilder.Stretch(track, 6f);
            CombatBuilder.Img("Track Back", track, white, 0f, new Color(0.22f, 0.06f, 0.06f));
            Image trail = Filled(CombatBuilder.Img("Trail", track, white, 0f, new Color(1f, 0.85f, 0.45f)));
            Image fill = Filled(CombatBuilder.Img("Fill", track, white, 0f, new Color(0.95f, 0.22f, 0.12f)));

            var so = new SerializedObject(bar);
            SerializedProperty marks = so.FindProperty("marks");
            float[] at = { 0.6f, 0.3f };
            marks.arraySize = at.Length;
            for (int i = 0; i < at.Length; i++)
            {
                RectTransform mark = Rect($"Mark {Mathf.RoundToInt(at[i] * 100f)}%", track);
                mark.anchorMin = mark.anchorMax = new Vector2(at[i], 0.5f);
                mark.sizeDelta = new Vector2(6f, 44f);
                var image = mark.gameObject.AddComponent<Image>();
                image.sprite = white;
                image.raycastTarget = false;
                marks.GetArrayElementAtIndex(i).objectReferenceValue = image;
            }

            Text status = Label("Status", root, "", 30, FontStyle.Bold, new Color(1f, 0.9f, 0.55f, 0f));
            status.rectTransform.anchorMin = new Vector2(0f, 1f);
            status.rectTransform.anchorMax = new Vector2(1f, 1f);
            status.rectTransform.pivot = new Vector2(0.5f, 1f);
            status.rectTransform.anchoredPosition = new Vector2(0f, -94f);
            status.rectTransform.sizeDelta = new Vector2(0f, 36f);

            so.FindProperty("boss").objectReferenceValue = boss;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("trail").objectReferenceValue = trail;
            so.FindProperty("glow").objectReferenceValue = halo;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Màn THẤT BẠI / CHIẾN THẮNG: canvas riêng vẽ trên HUD, phủ kín màn hình
        static void BuildResultOverlay(LionBoss boss, PlayerHealth player)
        {
            var go = new GameObject("Result Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var group = go.AddComponent<CanvasGroup>();

            Image shade = CombatBuilder.Img("Shade", go.transform, null, 0f, new Color(0.08f, 0.01f, 0.02f, 0.78f));
            shade.raycastTarget = true; // chặn chạm xuống HUD bên dưới
            RectTransform safe = Rect("Safe Area", go.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            Text title = Label("Title", safe, "THẤT BẠI", 150, FontStyle.Bold, Color.white);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1600f, 200f));
            title.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            Text info = Label("Info", safe, "", 48, FontStyle.Normal, new Color(1f, 0.9f, 0.8f));
            Place(info.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1600f, 70f));

            Sprite panel = PanelSprite();
            (Button retry, Text retryText) = OverlayButton(safe, "Retry Button", "Thử lại", new Vector2(-300f, -170f), new Color(0.88f, 0.3f, 0.14f), panel);
            (Button menu, Text menuText) = OverlayButton(safe, "Menu Button", "Đổi nhân vật", new Vector2(300f, -170f), new Color(0.45f, 0.28f, 0.85f), panel);

            var overlay = go.AddComponent<ResultOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("boss").objectReferenceValue = boss;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("shade").objectReferenceValue = shade;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("info").objectReferenceValue = info;
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("retryLabel").objectReferenceValue = retryText;
            so.FindProperty("menuButton").objectReferenceValue = menu;
            so.FindProperty("menuLabel").objectReferenceValue = menuText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static (Button, Text) OverlayButton(Transform parent, string name, string text, Vector2 position, Color color, Sprite panel)
        {
            Image image = CombatBuilder.Img(name, parent, panel, 0f, color);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Place(image.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(520f, 140f));
            var button = image.gameObject.AddComponent<Button>();
            Text label = Label("Text", image.transform, text, 60, FontStyle.Bold, Color.white);
            Stretch(label.rectTransform);
            return (button, label);
        }

        static Image Filled(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
            return image;
        }

        // =============================================================================================================
        // Art
        // =============================================================================================================

        /// <summary>
        /// Nhuộm tranh hang động Art/Drawn/{file}.png sang tông than hồng: độ sáng mỗi điểm ảnh tra theo dải màu đen → nâu đỏ →
        /// đỏ cam → vàng (đá tối thành đá nung, tinh thể và thác nước sáng thành dung nham). Lưu Art/Generated/Arena_{file}.png.
        /// Chưa có tranh thì trả null.
        /// </summary>
        static Sprite Emberize(string file, Vector2 pivot)
        {
            string source = $"{DrawnArt.Folder}/{file}.png";
            if (!File.Exists(source))
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(source));
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (p.a == 0)
                    continue;
                float max = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) / 255f;
                float min = Mathf.Min(p.r, Mathf.Min(p.g, p.b)) / 255f;
                float saturation = max > 0f ? (max - min) / max : 0f;
                // Nén vùng tối / trung bình cho hang tối hẳn; chỗ sáng và rực màu (tinh thể, thác) giữ sáng thành dung nham
                float v = Mathf.Clamp01(Mathf.Pow(max, 1.7f) * (1f + 0.5f * saturation * max));
                Color c = EmberRamp(v);
                pixels[i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), p.a);
            }

            texture.SetPixels32(pixels);
            Directory.CreateDirectory(ArtFactory.Folder);
            string path = $"{ArtFactory.Folder}/Arena_{file}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
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

        static readonly (float at, Color color)[] Ramp =
        {
            (0f, new Color(0.03f, 0.01f, 0.01f)),
            (0.15f, new Color(0.12f, 0.035f, 0.03f)),
            (0.35f, new Color(0.3f, 0.08f, 0.055f)),
            (0.55f, new Color(0.6f, 0.18f, 0.07f)),
            (0.75f, new Color(0.95f, 0.42f, 0.12f)),
            (1f, new Color(1f, 0.85f, 0.5f)),
        };

        static Color EmberRamp(float v)
        {
            for (int i = 1; i < Ramp.Length; i++)
            {
                if (v <= Ramp[i].at)
                    return Color.Lerp(Ramp[i - 1].color, Ramp[i].color, (v - Ramp[i - 1].at) / (Ramp[i].at - Ramp[i - 1].at));
            }

            return Ramp[Ramp.Length - 1].color;
        }

        // Vòng báo trước: vành đỏ đậm + lòng mờ (nhuộm màu lúc chạy)
        static Sprite MarkerSprite() => ArtFactory.Sprite("Boss_Marker", 256, 256, 128f, new Vector2(0.5f, 0.5f), (u, v) =>
        {
            float r = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.06f);
            float inner = r < 0.9f ? 0.12f + 0.2f * Mathf.Pow(r / 0.9f, 3f) : 0f;
            float halo = r < 1f ? 0.35f * Mathf.Exp(-Mathf.Pow((r - 0.9f) / 0.05f, 2f)) : 0f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(ring, inner) + halo));
        }, 2, false);

        // Cầu lửa: lõi vàng trắng, thân cam, đuôi lửa loe về bên trái (bay sang phải), viền đỏ mờ
        static Sprite FireballSprite() => ArtFactory.Sprite("Boss_Fireball", 192, 128, 128f, new Vector2(0.6f, 0.5f), (u, v) =>
        {
            float x = (u - 0.6f) * 1.5f, y = (v - 0.5f);
            float tail = x < 0f ? 1f + (-x) * 0.9f : 1f; // đuôi kéo dài về phía sau
            float r = new Vector2(x / tail, y * 2f).magnitude;
            float n = Noise.Fbm(u * 6f, v * 6f, 71);
            float body = Mathf.Clamp01((0.95f - r - 0.15f * n) * 4f);
            if (body <= 0f)
                return Color.clear;
            float heat = Mathf.Clamp01(1f - r * 1.3f + 0.2f * n);
            Color c = heat > 0.6f ? Color.Lerp(new Color(1f, 0.6f, 0.15f), new Color(1f, 0.97f, 0.8f), (heat - 0.6f) / 0.4f)
                : Color.Lerp(new Color(0.85f, 0.15f, 0.05f), new Color(1f, 0.6f, 0.15f), heat / 0.6f);
            c.a = body;
            return c;
        }, 2, false);

        static float Blob(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx;
            float dy = (v - cy) / ry;
            return Mathf.Exp(-(dx * dx + dy * dy) * 1.6f);
        }
    }
}
