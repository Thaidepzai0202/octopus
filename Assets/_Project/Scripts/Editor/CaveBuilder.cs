using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng màn chơi hang động theo ảnh mẫu: nền tím nhiều lớp trôi theo chiều sâu, nhũ đá trên trần,
    /// cột đá nối trần với các tảng đá treo, gờ đá viền xanh ngọc phát sáng, sương tím dưới vực, bụi sáng lơ lửng.
    /// Collider giữ nguyên bố cục cũ nên cảm giác chơi và test không đổi; chỉ phần hình là mới.
    /// </summary>
    static class CaveBuilder
    {
        // ---- Bảng màu lấy từ ảnh mẫu ----
        public static readonly Color CameraBackground = Hex("140b2b");
        static readonly Color FarTint = Hex("3d3b94");
        static readonly Color MidTint = Hex("261d63");
        static readonly Color NearTint = Hex("1a1346");
        static readonly Color ForegroundTint = Hex("0c0719");
        static readonly Color LedgeTint = Hex("4d3e9e");
        static readonly Color RockTint = Hex("7a69d8");
        static readonly Color ColumnTint = Hex("1c1446");
        static readonly Color CeilingTint = Hex("1f1647");
        static readonly Color RimCyan = new Color(0.42f, 0.95f, 1f, 0.95f);
        static readonly Color LightBlob = new Color(0.45f, 0.5f, 1f, 0.45f);
        static readonly Color CrystalGlow = new Color(0.35f, 0.9f, 1f, 0.35f);
        static readonly Color MistColor = new Color(0.55f, 0.35f, 1f, 0.85f);

        // ---- Bố cục (giữ nguyên collider cũ) ----
        static readonly (string name, float x, float width)[] Ledges =
        {
            ("Start Platform", 0f, 10f), ("Rest Island", 34.5f, 6f), ("Goal Platform", 70f, 8f),
        };

        const float LedgeY = -1.5f;
        const float LedgeDepth = 11f;

        static readonly (float x, float y, float width)[] Rocks =
        {
            (7f, 4.5f, 2f), (12.5f, 5.5f, 2.5f), (18f, 4.8f, 2f), (23.5f, 6f, 3f), (29f, 5f, 2f),
            (40f, 5.2f, 2.5f), (45.5f, 6.2f, 2f), (51f, 5f, 2.5f), (56.5f, 5.8f, 2f), (62f, 4.8f, 2.5f),
        };

        const float CeilingBase = 10.4f;
        const float CeilingTop = 18f;
        const float WorldMinX = -30f;
        const float WorldMaxX = 100f;

        struct Shapes
        {
            public Sprite[] Stalactites;
            public Sprite[] Mounds;
            public Sprite[] Pillars;
            public Sprite[] Boulders;
            public Sprite Crystal;
            public Sprite Mist;
        }

        public static void Build(Camera camera, Sprite glow, Material glowLine, int anchorableLayer, int hazardLayer)
        {
            var rng = new Random(20261014);
            Shapes shapes = CreateShapes();

            BuildBackdrop(camera);
            BuildBackground(shapes, glow, rng);

            Transform level = new GameObject("Level").transform;
            Func<float, float> ceiling = BuildCeiling(level, rng);
            foreach ((string name, float x, float width) in Ledges)
                BuildLedge(level, name, x, width, shapes, glow, glowLine, rng);
            for (int i = 0; i < Rocks.Length; i++)
                BuildRock(level, i, Rocks[i], ceiling, glow, glowLine, anchorableLayer);

            BuildKillZone(level, hazardLayer, shapes.Mist);
            BuildGoal(level, shapes, glow);
            BuildMotes(glow);
        }

        // ---------- Nền ----------

        static void BuildBackdrop(Camera camera)
        {
            Color top = Hex("110925");
            Color middle = Hex("2a2172");
            Color bottom = Hex("1a1242");
            Color light = Hex("6f7ae8");

            Sprite sprite = ArtFactory.Sprite("Backdrop", 256, 128, 128f, new Vector2(0.5f, 0.5f), (u, v) =>
            {
                Color c = v > 0.55f ? Color.Lerp(middle, top, (v - 0.55f) / 0.45f) : Color.Lerp(bottom, middle, v / 0.55f);
                // Những khoảng sáng như cửa hang phía xa
                float glowAmount = Blob(u, v, 0.2f, 0.6f, 0.12f, 0.2f) + Blob(u, v, 0.53f, 0.55f, 0.16f, 0.26f) +
                                   Blob(u, v, 0.84f, 0.64f, 0.1f, 0.18f) + 0.6f * Blob(u, v, 0.38f, 0.78f, 0.07f, 0.1f);
                glowAmount *= 0.7f + 0.6f * Noise.Fbm(u * 7f, v * 3.5f, 7);
                return Color.Lerp(c, light, Mathf.Clamp01(glowAmount) * 0.85f);
            }, 1, false);

            var go = new GameObject("Backdrop");
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 30f);
            go.transform.localScale = new Vector3(30f, 30f, 1f); // sprite 2 x 1 unit → phủ 60 x 30, đủ cho mọi mức zoom
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -1000;
        }

        static void BuildBackground(Shapes shapes, Sprite glow, Random rng)
        {
            // Lớp xa nhất: để trống cho ảnh vẽ tay (deep.png), art vẽ bằng code không dùng
            Layer(CaveLayout.Deep);

            // Lớp xa: cột đá và nhũ đá nhạt màu che bớt những khoảng sáng
            (Transform far, float min, float max, float mid) = Layer(CaveLayout.Far);
            for (float x = min; x < max; x += Next(rng, 9f, 15f))
                Place(far, glow, new Vector2(x, mid + Next(rng, -1f, 5f)), Vector2.one * Next(rng, 12f, 20f),
                    WithAlpha(LightBlob, Next(rng, 0.35f, 0.6f)), -950);
            for (float x = min; x < max; x += Next(rng, 10f, 16f))
                Place(far, Pick(shapes.Pillars, rng), new Vector2(x, mid + Next(rng, -1f, 1f)), new Vector2(Next(rng, 1.1f, 1.8f), 1.3f), FarTint, -900, rng);
            for (float x = min; x < max; x += Next(rng, 4f, 8f))
                Place(far, Pick(shapes.Stalactites, rng), new Vector2(x, mid + 9.5f + Next(rng, 0f, 1.5f)), new Vector2(Next(rng, 0.7f, 2f), Next(rng, 0.7f, 1.6f)), FarTint, -900, rng);
            for (float x = min; x < max; x += Next(rng, 4f, 7f))
                Place(far, Pick(shapes.Mounds, rng), new Vector2(x, mid - 9.5f), new Vector2(Next(rng, 1f, 1.8f), Next(rng, 0.9f, 1.5f)), FarTint, -900, rng);

            // Lớp giữa
            (Transform middle, float min2, float max2, float mid2) = Layer(CaveLayout.Mid);
            for (float x = min2; x < max2; x += Next(rng, 14f, 22f))
                Place(middle, Pick(shapes.Pillars, rng), new Vector2(x, mid2 + Next(rng, -1f, 1f)), new Vector2(Next(rng, 1.2f, 2f), 1.4f), MidTint, -800, rng);
            for (float x = min2; x < max2; x += Next(rng, 5f, 9f))
                Place(middle, Pick(shapes.Stalactites, rng), new Vector2(x, mid2 + 9f + Next(rng, 0f, 1.2f)), new Vector2(Next(rng, 0.8f, 2.2f), Next(rng, 0.7f, 1.5f)), MidTint, -800, rng);
            for (float x = min2; x < max2; x += Next(rng, 4f, 7f))
                Place(middle, Pick(shapes.Mounds, rng), new Vector2(x, mid2 - 8.7f), new Vector2(Next(rng, 1.1f, 1.9f), Next(rng, 0.9f, 1.4f)), MidTint, -800, rng);

            // Lớp gần: tảng đá lớn và tinh thể phát sáng
            (Transform near, float min3, float max3, float mid3) = Layer(CaveLayout.Near);
            for (float x = min3; x < max3; x += Next(rng, 6f, 10f))
                Place(near, Pick(shapes.Stalactites, rng), new Vector2(x, mid3 + 8.6f + Next(rng, 0f, 1f)), new Vector2(Next(rng, 1.2f, 2f), Next(rng, 0.7f, 1.2f)), NearTint, -700, rng);
            for (float x = min3; x < max3; x += Next(rng, 4f, 7f))
            {
                float y = mid3 - 8.8f;
                Place(near, Pick(shapes.Boulders, rng), new Vector2(x, y), new Vector2(Next(rng, 1f, 1.8f), Next(rng, 0.8f, 1.3f)), NearTint, -700, rng);
                if (rng.NextDouble() < 0.2)
                {
                    float scale = Next(rng, 0.6f, 1.1f);
                    Place(near, glow, new Vector2(x + 0.6f, y + 1.6f), Vector2.one * 3.2f * scale, CrystalGlow, -705);
                    Place(near, shapes.Crystal, new Vector2(x + 0.6f, y + 1.2f), Vector2.one * scale, Color.white, -690, rng);
                }
            }

            // Tiền cảnh: tảng đá rất tối ở mép dưới, trôi nhanh hơn màn chơi
            (Transform front, float min4, float max4, float mid4) = Layer(CaveLayout.Front);
            for (float x = min4; x < max4; x += Next(rng, 7f, 12f))
                Place(front, Pick(shapes.Boulders, rng), new Vector2(x, mid4 - 9.4f), new Vector2(Next(rng, 1.6f, 2.6f), Next(rng, 0.7f, 1.1f)), ForegroundTint, 100, rng);
        }

        static (Transform layer, float min, float max, float midY) Layer(CaveLayout.Layer layer)
        {
            var go = new GameObject(layer.ObjectName);
            var parallax = go.AddComponent<ParallaxLayer>();
            var settings = new SerializedObject(parallax);
            settings.FindProperty("factorX").floatValue = layer.FactorX;
            settings.FindProperty("factorY").floatValue = layer.FactorY;
            settings.ApplyModifiedPropertiesWithoutUndo();

            (float min, float max, float midY) = CaveLayout.Range(layer);
            return (go.transform, min, max, midY);
        }

        // ---------- Trần hang ----------

        static Func<float, float> BuildCeiling(Transform level, Random rng)
        {
            var drips = new List<(float x, float length, float halfWidth)>();
            for (float x = WorldMinX; x < WorldMaxX; x += Next(rng, 1.4f, 3.6f))
                drips.Add((x, Next(rng, 0.5f, 2.1f), Next(rng, 0.35f, 0.9f)));

            // Mép dưới của trần: gợn sóng + nhũ đá nhọn rủ xuống. Tính sẵn theo từng 1/96 unit cho nhanh
            const float resolution = 96f;
            var profile = new float[Mathf.CeilToInt((WorldMaxX - WorldMinX) * resolution) + 2];
            for (int i = 0; i < profile.Length; i++)
            {
                float x = WorldMinX + i / resolution;
                float y = CeilingBase + 0.6f * (Noise.Fbm(x * 0.18f, 3.1f, 11) - 0.5f) * 2f;
                foreach ((float dx, float length, float halfWidth) in drips)
                {
                    float k = 1f - Mathf.Abs(x - dx) / halfWidth;
                    if (k > 0f)
                        y = Mathf.Min(y, CeilingBase + 0.2f - length * Mathf.Pow(k, 1.6f));
                }

                profile[i] = y;
            }

            float Profile(float x)
            {
                float f = Mathf.Clamp((x - WorldMinX) * resolution, 0f, profile.Length - 2);
                int i = (int)f;
                return Mathf.Lerp(profile[i], profile[i + 1], f - i);
            }

            var ceiling = new GameObject("Ceiling");
            ceiling.transform.SetParent(level, false);
            var collider = ceiling.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2((WorldMinX + WorldMaxX) / 2f, 13f);
            collider.size = new Vector2(WorldMaxX - WorldMinX, 4f);

            var art = new GameObject("Ceiling Art").transform;
            art.SetParent(ceiling.transform, false);

            const float chunk = 16f;
            int index = 0;
            for (float x0 = WorldMinX; x0 < WorldMaxX; x0 += chunk, index++)
            {
                var area = new Rect(x0, CeilingBase - 2.6f, chunk + 0.1f, CeilingTop - CeilingBase + 2.6f);
                Sprite sprite = Silhouette($"Ceiling_{index}", area, 24f, area.min, p => p.y >= Profile(p.x),
                    new Vector2(0.12f, -0.22f), 40 + index);
                Place(art, sprite, area.min, Vector2.one, CeilingTint, -105);
            }

            return Profile;
        }

        // ---------- Gờ đá (sàn) ----------

        static void BuildLedge(Transform level, string name, float x, float width, Shapes shapes, Sprite glow, Material glowLine, Random rng)
        {
            var go = new GameObject(name);
            go.transform.SetParent(level, false);
            go.transform.position = new Vector2(x, LedgeY);
            go.AddComponent<BoxCollider2D>().size = new Vector2(width, 1f);

            int seed = 100 + (int)(x * 7f);
            List<Vector2> outline = LedgeOutline(width, LedgeDepth, seed, out List<Vector2> top);
            Rect area = Expand(Polygon.Bounds(outline), 0.3f);
            Sprite sprite = Silhouette($"Ledge_{name.Replace(" ", "")}", area, 28f, Vector2.zero,
                p => Polygon.Contains(outline, p), new Vector2(0f, 0.2f), seed);
            Place(go.transform, sprite, Vector2.zero, Vector2.one, LedgeTint, -100).name = "Ledge Art";
            Rim(go.transform, "Rim", top, glowLine, RimCyan, 0.2f, -90);

            // Vài cụm tinh thể phát sáng trên mặt đá
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float localX = side * (width / 2f - Next(rng, 0.6f, 1.6f));
                float scale = Next(rng, 0.45f, 0.75f);
                Place(go.transform, glow, new Vector2(localX, 0.9f), Vector2.one * 2.6f * scale, CrystalGlow, -97);
                Place(go.transform, shapes.Crystal, new Vector2(localX, 0.45f), Vector2.one * scale, Color.white, -95, rng);
            }
        }

        // Mặt trên gần như phẳng (trùng mặt trên collider, y = 0.5), hai vách lượn xuống rồi thu hẹp thành cột
        static List<Vector2> LedgeOutline(float width, float depth, int seed, out List<Vector2> top)
        {
            float half = width / 2f + 0.18f;
            var points = new List<Vector2>();
            top = new List<Vector2>();

            int n = Mathf.CeilToInt(width * 3f);
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(-half, half, i / (float)n);
                var p = new Vector2(x, 0.5f + 0.035f * Noise.Edge(x * 1.5f, seed));
                points.Add(p);
                top.Add(p);
            }

            const int m = 16;
            for (int i = 1; i <= m; i++)
                points.Add(new Vector2(CliffX(i / (float)m, half, seed + 7), Mathf.Lerp(0.5f, -depth, i / (float)m)));
            for (int i = m; i >= 1; i--)
                points.Add(new Vector2(-CliffX(i / (float)m, half, seed + 9), Mathf.Lerp(0.5f, -depth, i / (float)m)));
            return points;
        }

        static float CliffX(float t, float half, int seed)
        {
            if (t < 0.08f)
                return half + 0.08f; // mép môi đá nhô ra
            return half * Mathf.Lerp(1f, 0.55f, Mathf.Pow(t, 0.7f)) + 0.5f * Noise.Edge(t * 4f, seed);
        }

        // ---------- Tảng đá treo (bám được) ----------

        static void BuildRock(Transform level, int index, (float x, float y, float width) rock, Func<float, float> ceiling,
            Sprite glow, Material glowLine, int anchorableLayer)
        {
            var go = new GameObject($"Rock {index + 1:00}") { layer = anchorableLayer };
            go.transform.SetParent(level, false);
            go.transform.position = new Vector2(rock.x, rock.y);
            go.AddComponent<BoxCollider2D>().size = new Vector2(rock.width, 1f);

            int seed = 300 + index * 17;

            // Cột đá nối từ đỉnh tảng lên trần
            float columnBottom = CaveLayout.ColumnBottom;
            float columnHeight = ceiling(rock.x) - rock.y - columnBottom + 0.8f;
            Sprite column = ColumnSprite($"Column_{index + 1:00}", rock.width, columnHeight, seed);
            Place(go.transform, column, new Vector2(0f, columnBottom), Vector2.one, ColumnTint, -110).name = "Column Art";

            List<Vector2> outline = RockOutline(rock.width, seed, out List<Vector2> bottom);
            Rect area = Expand(Polygon.Bounds(outline), 0.2f);
            Sprite sprite = Silhouette($"Rock_{index + 1:00}", area, 40f, Vector2.zero,
                p => Polygon.Contains(outline, p), new Vector2(0.05f, -0.2f), seed);
            Place(go.transform, sprite, Vector2.zero, Vector2.one, RockTint, -100).name = "Rock Art";

            Place(go.transform, glow, new Vector2(0f, -0.75f), new Vector2(rock.width + 1.6f, 1.6f), new Color(0.4f, 0.9f, 1f, 0.2f), -102);
            LineRenderer rim = Rim(go.transform, "Glow Rim", bottom, glowLine, RimCyan, 0.22f, -90);
            var component = go.AddComponent<Rock>();
            var settings = new SerializedObject(component);
            settings.FindProperty("glowRim").objectReferenceValue = rim;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // Đáy gần như phẳng trùng mặt dưới collider (chỗ xúc tu bám); đỉnh gồ ghề, nhô lên giữa để nối cột đá
        static List<Vector2> RockOutline(float width, int seed, out List<Vector2> bottom)
        {
            float half = width / 2f + 0.12f;
            var points = new List<Vector2>();
            bottom = new List<Vector2>();

            int n = Mathf.Max(4, Mathf.CeilToInt(width * 4f));
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(-half + 0.15f, half - 0.15f, i / (float)n);
                var p = new Vector2(x, -0.5f - 0.05f * Noise.Fbm(x * 2f, 0.3f, seed));
                points.Add(p);
                bottom.Add(p);
            }

            points.Add(new Vector2(half, -0.32f));
            points.Add(new Vector2(half + 0.06f + 0.1f * Noise.Edge(1.3f, seed), 0f));
            points.Add(new Vector2(half - 0.05f, 0.4f));
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float x = Mathf.Lerp(half - 0.2f, -half + 0.2f, t);
                points.Add(new Vector2(x, 0.55f + 0.3f * Mathf.Sin(t * Mathf.PI) + 0.18f * Noise.Edge(x * 1.7f, seed + 3)));
            }

            points.Add(new Vector2(-half + 0.05f, 0.4f));
            points.Add(new Vector2(-half - 0.06f - 0.1f * Noise.Edge(2.1f, seed), 0f));
            points.Add(new Vector2(-half, -0.32f));
            return points;
        }

        // Cột đá: dưới hẹp, càng lên càng loe ra hoà vào trần, mép lượn sóng
        static Sprite ColumnSprite(string name, float rockWidth, float height, int seed)
        {
            float baseHalf = rockWidth * 0.32f;
            float topHalf = rockWidth * 0.6f;
            var area = new Rect(-topHalf - 0.8f, 0f, (topHalf + 0.8f) * 2f, height);
            return Silhouette(name, area, 20f, Vector2.zero, p =>
            {
                float t = Mathf.Clamp01(p.y / height);
                float halfWidth = Mathf.Lerp(baseHalf, topHalf, t * t) * (0.85f + 0.5f * Noise.Edge(t * 3f, seed)) +
                                  0.12f * Mathf.Sin(t * 9f + seed);
                float center = 0.3f * Noise.Edge(t * 1.5f, seed + 4);
                return Mathf.Abs(p.x - center) < halfWidth;
            }, new Vector2(-0.15f, 0f), seed);
        }

        // ---------- Vực, đích, bụi sáng ----------

        static void BuildKillZone(Transform level, int hazardLayer, Sprite mist)
        {
            var go = new GameObject("Kill Zone") { layer = hazardLayer };
            go.transform.SetParent(level, false);
            go.transform.position = new Vector2(32f, -12f);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(120f, 2f);
            collider.isTrigger = true;
            go.AddComponent<KillZone>();

            // Sương tím dâng lên từ đáy vực: chân các cột đá và bạch tuộc rơi xuống đều chìm dần vào sương
            Place(level, mist, new Vector2(35f, -11f), new Vector2(150f, 1.25f), MistColor, 40);
        }

        static void BuildGoal(Transform level, Shapes shapes, Sprite glow)
        {
            var goal = new GameObject("Goal Crystal").transform;
            goal.SetParent(level, false);
            goal.position = new Vector2(72.2f, -1.05f);
            Place(goal, glow, new Vector2(0f, 1.4f), Vector2.one * 7f, new Color(0.4f, 0.95f, 1f, 0.45f), -98);
            Place(goal, shapes.Crystal, Vector2.zero, Vector2.one * 1.7f, Color.white, -95);
        }

        static void BuildMotes(Sprite glow)
        {
            var motes = new GameObject("Ambient Motes").AddComponent<AmbientMotes>();
            var settings = new SerializedObject(motes);
            settings.FindProperty("sprite").objectReferenceValue = glow;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Hình mẫu dùng lại nhiều lần cho các lớp nền ----------

        static Shapes CreateShapes()
        {
            var shapes = new Shapes
            {
                Stalactites = new Sprite[3],
                Mounds = new Sprite[3],
                Pillars = new Sprite[2],
                Boulders = new Sprite[2],
            };

            for (int i = 0; i < 3; i++)
            {
                int seed = 500 + i * 13;
                const float w = 2.4f, h = 7f;
                shapes.Stalactites[i] = Silhouette($"Stalactite_{i}", new Rect(0f, 0f, w, h), 24f, new Vector2(w / 2f, h), p =>
                {
                    float t = p.y / h; // 0 = mũi nhọn ở dưới, 1 = gốc ở trên
                    float halfWidth = w / 2f * Mathf.Pow(t, 0.75f) * (0.82f + 0.35f * Noise.Edge(t * 4f, seed));
                    float center = w / 2f + 0.3f * w * (1f - t) * Noise.Edge(t * 2f, seed + 1);
                    return Mathf.Abs(p.x - center) < halfWidth;
                }, new Vector2(-0.15f, 0.1f), seed);
            }

            for (int i = 0; i < 3; i++)
            {
                int seed = 600 + i * 13;
                const float w = 7f, h = 3.5f;
                shapes.Mounds[i] = Silhouette($"Mound_{i}", new Rect(0f, 0f, w, h), 24f, new Vector2(w / 2f, 0f), p =>
                {
                    float d = Mathf.Abs(p.x / w * 2f - 1f);
                    float top = h * (1f - Mathf.Pow(d, 1.6f)) * (0.65f + 0.45f * Noise.Fbm(p.x * 0.7f, 0.5f, seed));
                    return p.y < top;
                }, new Vector2(-0.15f, 0.18f), seed);
            }

            for (int i = 0; i < 2; i++)
            {
                int seed = 700 + i * 13;
                const float w = 3f, h = 16f;
                shapes.Pillars[i] = Silhouette($"Pillar_{i}", new Rect(0f, 0f, w, h), 24f, new Vector2(w / 2f, h / 2f), p =>
                {
                    float t = p.y / h;
                    float halfWidth = w / 2f * (0.5f + 0.45f * Mathf.Pow(Mathf.Abs(2f * t - 1f), 2f)) * (0.88f + 0.3f * Noise.Edge(t * 5f, seed));
                    float center = w / 2f + 0.25f * w * Noise.Edge(t * 2f, seed + 2);
                    return Mathf.Abs(p.x - center) < halfWidth;
                }, new Vector2(-0.2f, 0f), seed);
            }

            for (int i = 0; i < 2; i++)
            {
                int seed = 800 + i * 13;
                const float w = 4f, h = 3f;
                shapes.Boulders[i] = Silhouette($"Boulder_{i}", new Rect(0f, 0f, w, h), 24f, new Vector2(w / 2f, 0f), p =>
                {
                    float d = p.x / w * 2f - 1f;
                    float top = h * Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)) * (0.8f + 0.35f * Noise.Fbm(p.x * 0.9f, 1.5f, seed));
                    return p.y < top;
                }, new Vector2(-0.15f, 0.18f), seed);
            }

            shapes.Crystal = CrystalSprite();
            shapes.Mist = ArtFactory.Sprite("Mist", 8, 64, 8f, new Vector2(0.5f, 0f),
                (u, v) => new Color(1f, 1f, 1f, Mathf.Pow(1f - v, 1.2f)), 1, false);
            return shapes;
        }

        // Cụm 3 tinh thể nhọn, mặt trái sáng, mặt phải tối, viền mực
        static Sprite CrystalSprite()
        {
            (float x, float angle, float width, float height)[] shards =
            {
                (0f, 4f, 0.28f, 1.8f), (-0.35f, 24f, 0.2f, 1.1f), (0.38f, -20f, 0.22f, 1.25f),
            };
            Color light = new Color(0.75f, 1f, 1f);
            Color dark = new Color(0.22f, 0.62f, 0.9f);
            Color ink = new Color(0.08f, 0.1f, 0.25f);
            const float w = 1.6f, h = 2f;

            return ArtFactory.Sprite("Crystal", 80, 100, 50f, new Vector2(0.5f, 0f), (u, v) =>
            {
                var p = new Vector2((u - 0.5f) * w, v * h);
                foreach ((float x, float angle, float width, float height) in shards)
                {
                    Vector2 local = Quaternion.Euler(0f, 0f, -angle) * (p - new Vector2(x, 0f));
                    if (local.y < 0f || local.y > height)
                        continue;
                    float taper = local.y > height * 0.72f ? 1f - (local.y - height * 0.72f) / (height * 0.28f) : 1f;
                    float halfWidth = width * taper;
                    if (Mathf.Abs(local.x) > halfWidth)
                        continue;
                    if (Mathf.Abs(local.x) > halfWidth - 0.035f || local.y < 0.03f)
                        return ink;
                    Color face = local.x < 0f ? light : dark;
                    return Color.Lerp(face, Color.white, Mathf.Clamp01(local.y / height - 0.6f) * 0.6f);
                }

                return Color.clear;
            });
        }

        // ---------- Tiện ích vẽ ----------

        /// <summary>
        /// Vẽ hình bóng xám (sẽ được nhuộm màu bằng SpriteRenderer.color): vân đá theo noise, vết nứt,
        /// viền mực ở mép và một dải sáng ở phía có ánh sáng (rimOffset chỉ hướng ngược ánh sáng).
        /// </summary>
        static Sprite Silhouette(string name, Rect area, float pixelsPerUnit, Vector2 pivotPoint, Func<Vector2, bool> inside,
            Vector2 rimOffset, int seed)
        {
            int width = Mathf.CeilToInt(area.width * pixelsPerUnit);
            int height = Mathf.CeilToInt(area.height * pixelsPerUnit);
            var pivot = new Vector2((pivotPoint.x - area.xMin) / (width / pixelsPerUnit), (pivotPoint.y - area.yMin) / (height / pixelsPerUnit));
            float edge = 1.5f / pixelsPerUnit;

            return ArtFactory.Sprite(name, width, height, pixelsPerUnit, pivot, (u, v) =>
            {
                var p = new Vector2(area.xMin + u * width / pixelsPerUnit, area.yMin + v * height / pixelsPerUnit);
                if (!inside(p))
                    return Color.clear;

                bool outline = !inside(p + new Vector2(edge, 0f)) || !inside(p - new Vector2(edge, 0f)) ||
                               !inside(p + new Vector2(0f, edge)) || !inside(p - new Vector2(0f, edge));
                bool rim = rimOffset != Vector2.zero && !inside(p + rimOffset);

                float shade = 0.7f + 0.22f * Noise.Fbm(p.x * 0.9f, p.y * 0.9f, seed) + 0.08f * v;
                float crack = Mathf.Abs(Noise.Fbm(p.x * 0.6f, p.y * 1.4f, seed + 5) - 0.5f);
                if (crack < 0.012f)
                    shade *= 0.72f;
                if (rim)
                    shade = Mathf.Max(shade, 1.05f);
                if (outline)
                    shade *= 0.45f;
                shade = Mathf.Min(shade, 1f);
                return new Color(shade, shade, shade, 1f);
            });
        }

        static LineRenderer Rim(Transform parent, string name, List<Vector2> localPoints, Material material, Color color, float width, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = localPoints.Count;
            for (int i = 0; i < localPoints.Count; i++)
                line.SetPosition(i, localPoints[i]);
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.sortingOrder = sortingOrder;
            return line;
        }

        static SpriteRenderer Place(Transform parent, Sprite sprite, Vector2 localPosition, Vector2 scale, Color tint, int sortingOrder, Random flip = null)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
            renderer.flipX = flip != null && flip.NextDouble() < 0.5;
            return renderer;
        }

        static float Blob(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx;
            float dy = (v - cy) / ry;
            return Mathf.Exp(-(dx * dx + dy * dy) * 1.6f);
        }

        static Rect Expand(Rect rect, float margin) =>
            Rect.MinMaxRect(rect.xMin - margin, rect.yMin - margin, rect.xMax + margin, rect.yMax + margin);

        static float Next(Random rng, float min, float max) => (float)(min + rng.NextDouble() * (max - min));

        static T Pick<T>(T[] items, Random rng) => items[rng.Next(items.Length)];

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }
}
