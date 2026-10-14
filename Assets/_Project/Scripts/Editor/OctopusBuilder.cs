using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng bạch tuộc: vật lý + điều khiển + phần vẽ (thân, mắt + con ngươi, 3 xúc tu có giác hút, quầng sáng, vệt tốc độ).
    /// Màu sắc, độ dày… do OctopusAnimator quyết định; ở đây chỉ tạo bộ phận và nối tham chiếu.
    /// </summary>
    static class OctopusBuilder
    {
        struct Art
        {
            public Material Body;
            public Material Tentacle;
            public Material Line;
            public Sprite EyeWhite;
            public Sprite Pupil;
            public Sprite Pad;
            public Sprite Glow;
        }

        public static GameObject Build(Material plainLine, Sprite glow, PhysicsMaterial2D physics,
            int playerLayer, LayerMask anchorMask, LayerMask groundMask)
        {
            Art art = CreateArt(plainLine, glow);

            var go = new GameObject("Octopus") { layer = playerLayer };

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.sharedMaterial = physics;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            go.AddComponent<DistanceJoint2D>().enabled = false;
            go.AddComponent<PlayerInputReader>();
            var swing = go.AddComponent<SwingController>();
            go.AddComponent<SurfaceMover>();
            var controller = go.AddComponent<OctopusController>();
            go.AddComponent<PlayerRespawn>();

            var swingSettings = new SerializedObject(swing);
            swingSettings.FindProperty("anchorMask").intValue = anchorMask;
            swingSettings.ApplyModifiedPropertiesWithoutUndo();

            Transform visual = CreateVisual(go.transform, controller, art, groundMask);

            // Ưu tiên bạch tuộc gắn xương (Art/Octopus/Rig), rồi tới khung hình vẽ tay (Art/Octopus), cuối cùng là bản
            // vẽ bằng code. Các bản không dùng vẫn giữ trong cây nhưng tắt đi, bật lại là đổi được
            GameObject sprites = CreateSpriteVisual(go.transform, controller, glow);
            GameObject rig = OctopusRigBuilder.Create(go.transform, controller, glow);
            if (sprites != null || rig != null)
                visual.gameObject.SetActive(false);
            if (sprites != null && rig != null)
                sprites.SetActive(false);
            CreateReachIndicator(go.transform, swing, plainLine);
            return go;
        }

        static Transform CreateVisual(Transform octopus, OctopusController controller, Art art, LayerMask groundMask)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(octopus, false);

            SpriteRenderer glow = Sprite("Glow", visual, art.Glow, -5);
            LineRenderer outline = Line("Body Outline", visual, art.Line, 8);
            LineRenderer body = Line("Body", visual, art.Body, 10);
            body.textureMode = LineTextureMode.Stretch;
            Transform eye = Sprite("Eye", visual, art.EyeWhite, 13).transform;
            Transform pupil = Sprite("Pupil", eye, art.Pupil, 14).transform;

            // Xúc tu 0 vẽ trước thân (thường là xúc tu nắm đá), hai xúc tu còn lại sau thân
            (string name, int order)[] tentacleSetup = { ("Tentacle Front", 12), ("Tentacle Middle", 6), ("Tentacle Back", 5) };
            var lines = new LineRenderer[3];
            var tips = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                lines[i] = Line(tentacleSetup[i].name, visual, art.Tentacle, tentacleSetup[i].order);
                tips[i] = Sprite("Tip", lines[i].transform, art.Pad, tentacleSetup[i].order).transform;
            }

            var trail = new GameObject("Speed Trail").AddComponent<TrailRenderer>();
            trail.transform.SetParent(visual, false);
            trail.sharedMaterial = art.Line;
            trail.time = 0.18f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 0f));
            trail.sortingOrder = 4;
            trail.emitting = false;

            var animator = visual.gameObject.AddComponent<OctopusAnimator>();
            var settings = new SerializedObject(animator);
            settings.FindProperty("controller").objectReferenceValue = controller;
            settings.FindProperty("body").objectReferenceValue = body;
            settings.FindProperty("bodyOutline").objectReferenceValue = outline;
            settings.FindProperty("eye").objectReferenceValue = eye;
            settings.FindProperty("pupil").objectReferenceValue = pupil;
            settings.FindProperty("glow").objectReferenceValue = glow;
            settings.FindProperty("speedTrail").objectReferenceValue = trail;
            settings.FindProperty("groundMask").intValue = groundMask;

            SerializedProperty tentacles = settings.FindProperty("tentacles");
            tentacles.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty tentacle = tentacles.GetArrayElementAtIndex(i);
                tentacle.FindPropertyRelative("line").objectReferenceValue = lines[i];
                tentacle.FindPropertyRelative("tip").objectReferenceValue = tips[i];
                tentacle.FindPropertyRelative("segments").intValue = 14;
            }

            settings.ApplyModifiedPropertiesWithoutUndo();
            return visual;
        }

        // ---------- Bạch tuộc vẽ tay (khung hình) ----------

        const string FramesFolder = "Assets/_Project/Art/Octopus";

        [Serializable]
        class FrameManifest
        {
            public float pixelsPerUnit = 100f;
            public FrameInfo[] frames = Array.Empty<FrameInfo>();
        }

        [Serializable]
        class FrameInfo
        {
            public string name;
            public float[] pivot = Array.Empty<float>();
            public bool ground;
            public float[] attach = Array.Empty<float>();
        }

        /// <summary>
        /// Đọc Art/Octopus/frames.json (do Tools/art/octopus_frames.py tạo), chỉnh cài đặt import từng khung
        /// (pivot, số pixel mỗi unit) rồi dựng "Sprite Visual" với OctopusSpriteAnimator. Không có khung thì trả về null.
        /// </summary>
        static GameObject CreateSpriteVisual(Transform octopus, OctopusController controller, Sprite glow)
        {
            string manifestPath = $"{FramesFolder}/frames.json";
            if (!File.Exists(manifestPath))
                return null;

            var manifest = JsonUtility.FromJson<FrameManifest>(File.ReadAllText(manifestPath));
            OctopusSpriteAnimator.Frame Load(string name)
            {
                FrameInfo info = Array.Find(manifest.frames, f => f.name == name);
                if (info == null)
                    return new OctopusSpriteAnimator.Frame();

                string path = $"{FramesFolder}/{name}.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = manifest.pixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(info.pivot[0], info.pivot[1]);
                settings.spriteMeshType = SpriteMeshType.Tight;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var frame = new OctopusSpriteAnimator.Frame { sprite = sprite, ground = info.ground };
                if (info.attach != null && info.attach.Length == 2)
                {
                    // Từ toạ độ chuẩn hoá trong ảnh → unit tính từ pivot
                    Vector2 size = sprite.rect.size / sprite.pixelsPerUnit;
                    frame.attach = new Vector2((info.attach[0] - info.pivot[0]) * size.x, (info.attach[1] - info.pivot[1]) * size.y);
                }

                return frame;
            }

            OctopusSpriteAnimator.Frame[] LoadAll(string prefix, int count)
            {
                var frames = new OctopusSpriteAnimator.Frame[count];
                for (int i = 0; i < count; i++)
                    frames[i] = Load($"{prefix}_{i + 1}");
                return frames;
            }

            var root = new GameObject("Sprite Visual").transform;
            root.SetParent(octopus, false);

            SpriteRenderer halo = Sprite("Glow", root, glow, -5);
            halo.color = new Color(0.6f, 0.4f, 1f, 0.45f); // quầng sáng tách thân đen khỏi nền hang tối
            halo.transform.localScale = Vector3.one * 3.8f;
            SpriteRenderer body = Sprite("Body", root, null, 10);

            // Xúc tu nắm đá: cùng kiểu với tranh — đen, viền tím, giác hút xanh ngọc phát sáng
            Texture2D skin = ArtFactory.Texture("OctopusTentacleDark", 32, 32, TextureWrapMode.Repeat, (u, v) =>
            {
                var color = new Color(0.1f, 0.08f, 0.16f);
                float edge = Mathf.Abs(v - 0.5f) * 2f;
                if (edge > 0.72f)
                    color = Color.Lerp(color, new Color(0.55f, 0.32f, 1f), (edge - 0.72f) / 0.28f);
                float du = (u - 0.5f) / 0.3f, dv = (v - 0.32f) / 0.16f;
                float sucker = du * du + dv * dv;
                if (sucker < 1f)
                    color = sucker < 0.35f ? new Color(0.08f, 0.2f, 0.42f) : new Color(0.35f, 0.88f, 1f);
                return color;
            });
            LineRenderer line = Line("Grab Tentacle", root, ArtFactory.Material("OctopusTentacleDark", skin), 9);
            line.textureMode = LineTextureMode.Tile;
            line.enabled = false;
            Transform tip = Sprite("Tip", line.transform, glow, 9).transform;
            tip.gameObject.SetActive(false);

            var trail = new GameObject("Speed Trail").AddComponent<TrailRenderer>();
            trail.transform.SetParent(root, false);
            trail.sharedMaterial = ArtFactory.Material("Line", null);
            trail.time = 0.2f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0f));
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(new Color(0.6f, 0.4f, 1f), 0f), new GradientColorKey(new Color(0.6f, 0.4f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = fade;
            trail.sortingOrder = 4;
            trail.emitting = false;

            var animator = root.gameObject.AddComponent<OctopusSpriteAnimator>();
            var so = new SerializedObject(animator);
            void SetFrame(SerializedProperty property, OctopusSpriteAnimator.Frame frame)
            {
                property.FindPropertyRelative("sprite").objectReferenceValue = frame.sprite;
                property.FindPropertyRelative("ground").boolValue = frame.ground;
                property.FindPropertyRelative("attach").vector2Value = frame.attach;
            }

            void SetFrames(string field, OctopusSpriteAnimator.Frame[] frames)
            {
                SerializedProperty array = so.FindProperty(field);
                array.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    SetFrame(array.GetArrayElementAtIndex(i), frames[i]);
            }

            SetFrame(so.FindProperty("idle"), Load("idle"));
            SetFrames("walk", LoadAll("walk", 6));
            SetFrames("jump", LoadAll("jump", 6));
            SetFrames("fall", LoadAll("fall", 6));
            SetFrames("swing", LoadAll("swing", 2));
            SetFrame(so.FindProperty("release"), Load("release"));
            so.FindProperty("controller").objectReferenceValue = controller;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("glow").objectReferenceValue = halo;
            so.FindProperty("speedTrail").objectReferenceValue = trail;
            so.FindProperty("grab.line").objectReferenceValue = line;
            so.FindProperty("grab.tip").objectReferenceValue = tip;
            so.FindProperty("grab.segments").intValue = 16;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root.gameObject;
        }

        static void CreateReachIndicator(Transform octopus, SwingController swing, Material material)
        {
            LineRenderer line = Line("Reach", octopus, material, -50);
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 0.045f);
            line.startColor = line.endColor = new Color(0.45f, 0.95f, 1f, 0.16f);
            line.numCapVertices = 0;
            line.loop = true;

            var indicator = line.gameObject.AddComponent<ReachIndicator>();
            var settings = new SerializedObject(indicator);
            settings.FindProperty("swing").objectReferenceValue = swing;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        static LineRenderer Line(string name, Transform parent, Material material, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.sortingOrder = sortingOrder;
            return line;
        }

        static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        // ---------- Art của bạch tuộc ----------

        static Art CreateArt(Material plainLine, Sprite glow)
        {
            // Thân: U chạy từ đầu (0) tới đuôi (1), V cắt ngang thân. Sáng ở giữa, tối dần ra mép cho có khối,
            // vài đốm trên lưng và nét gạch chéo như bản phác bút chì
            Texture2D bodyTexture = ArtFactory.Texture("OctopusBody", 128, 64, TextureWrapMode.Clamp, (u, v) =>
            {
                float across = 1f - Mathf.Abs(v - 0.5f) * 2f;
                float shade = 0.66f + 0.34f * Mathf.Pow(across, 0.6f);
                for (int i = 0; i < 7; i++)
                {
                    float su = 0.14f + 0.08f * i + 0.03f * Mathf.Sin(i * 2.3f);
                    float sv = 0.5f + 0.22f * Mathf.Sin(i * 1.7f + 0.4f);
                    float du = (u - su) / 0.035f;
                    float dv = (v - sv) / 0.09f;
                    if (du * du + dv * dv < 1f)
                        shade *= 0.8f;
                }

                bool hatch = v < 0.38f && u > 0.12f && u < 0.75f && Mathf.Repeat(u * 22f + v * 9f, 1f) < 0.12f;
                if (hatch)
                    shade *= 0.88f;
                return new Color(shade, shade, shade, 1f);
            });

            // Xúc tu: lặp theo chiều dài; hai mép tối làm viền, một hàng giác hút sáng có lỗ tối ở giữa
            Texture2D tentacleTexture = ArtFactory.Texture("OctopusTentacle", 32, 32, TextureWrapMode.Repeat, (u, v) =>
            {
                float shade = 0.86f;
                if (v < 0.14f || v > 0.86f)
                    shade = 0.42f;
                float du = (u - 0.5f) / 0.3f;
                float dv = (v - 0.34f) / 0.16f;
                float sucker = du * du + dv * dv;
                if (sucker < 1f)
                    shade = sucker < 0.3f ? 0.72f : 1f;
                return new Color(shade, shade, shade, 1f);
            });

            return new Art
            {
                Body = ArtFactory.Material("OctopusBody", bodyTexture),
                Tentacle = ArtFactory.Material("OctopusTentacle", tentacleTexture),
                Line = plainLine,
                Glow = glow,
                EyeWhite = ArtFactory.Sprite("EyeWhite", 64, 48, 160f, new Vector2(0.5f, 0.5f), (u, v) =>
                {
                    float x = u * 2f - 1f;
                    float y = v * 2f - 1f;
                    float ellipse = Mathf.Sqrt(x * x / 0.92f + y * y / 0.88f);
                    if (ellipse > 1f)
                        return Color.clear;
                    return ellipse > 0.82f ? new Color(0.06f, 0.03f, 0.1f) : new Color(1f, 0.98f, 0.96f);
                }),
                Pupil = ArtFactory.Sprite("Pupil", 32, 32, 220f, new Vector2(0.5f, 0.5f), (u, v) =>
                {
                    var p = new Vector2(u * 2f - 1f, v * 2f - 1f);
                    if (p.magnitude > 1f)
                        return Color.clear;
                    return (p - new Vector2(0.35f, 0.35f)).magnitude < 0.28f ? Color.white : new Color(0.05f, 0.02f, 0.1f);
                }),
                Pad = ArtFactory.Sprite("TentaclePad", 32, 32, 32f, new Vector2(0.5f, 0.5f), (u, v) =>
                {
                    float r = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
                    if (r > 1f)
                        return Color.clear;
                    float shade = r < 0.45f ? 0.55f : r < 0.62f ? 1f : 0.85f;
                    return new Color(shade, shade, shade, 1f);
                }),
            };
        }
    }
}
