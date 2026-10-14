using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Thay art vẽ bằng code bằng ảnh vẽ tay. Thả PNG vào Assets/_Project/Art/Drawn/ theo đúng tên file rồi chạy
    /// menu Octopus → Áp dụng ảnh vẽ tay. File nào chưa có thì giữ nguyên art cũ, nên vẽ dần từng lớp được.
    ///
    ///  sky.png     phông xa nhất, dính theo camera (không cần trong suốt)
    ///  deep/far/mid/near.png   các lớp nền từ xa tới gần, lặp ngang
    ///  front.png   tiền cảnh tối sát mép dưới, lặp ngang
    ///  ceiling.png trần hang + nhũ đá, lặp ngang, mép trên ảnh là trần
    ///  ledge.png   một gờ đá (sàn), mép trên ảnh là mặt sàn
    ///  rock.png    một tảng đá treo để bám, mép dưới ảnh là chỗ xúc tu bám
    ///  column.png  cột đá nối tảng đá lên trần, bị kéo giãn theo chiều cao
    ///
    /// Menu Octopus → Tạo khung mẫu để vẽ xuất các ảnh mẫu có kẻ vạch (khung camera, mặt sàn, vùng đá treo)
    /// vào thư mục Docs/art-templates để mở làm lớp tham chiếu trong phần mềm vẽ.
    /// </summary>
    public static class DrawnArt
    {
        public const string Folder = "Assets/_Project/Art/Drawn";
        const string ScenePath = "Assets/_Project/Scenes/Playground.unity";
        const string TemplateFolder = "Docs/art-templates";

        [MenuItem("Octopus/Áp dụng ảnh vẽ tay", priority = 20)]
        public static void ApplyFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);

            List<string> applied = Apply();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            string message = applied.Count == 0
                ? $"Chưa có ảnh nào trong {Folder}. Xem README phần \"Vẽ ảnh tay\" để biết cần vẽ file nào."
                : "Đã áp dụng: " + string.Join(", ", applied);
            Debug.Log("[Octopus] " + message);
            EditorUtility.DisplayDialog("Octopus", message, "OK");
        }

        [MenuItem("Octopus/Bảng chỉnh ảnh vẽ tay", priority = 22)]
        public static void OpenSettings()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);

            DrawnArtSettings settings = DrawnArtSettings.Load(createIfMissing: true);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        /// <summary>Dùng cho batchmode: mở scene Playground, áp dụng ảnh vẽ tay, lưu lại.</summary>
        public static void ApplyFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath);
            List<string> applied = Apply();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[Octopus] Đã áp dụng ảnh vẽ tay: " + string.Join(", ", applied));
        }

        static bool _applyQueued;

        /// <summary>
        /// Áp dụng lại ở nhịp Editor kế tiếp (gộp nhiều lần gọi liền nhau thành một), chỉ khi đang mở Playground và không Play.
        /// Dùng khi lưu ảnh mới hoặc sửa số trong bảng chỉnh.
        /// </summary>
        public static void QueueApply()
        {
            if (_applyQueued || Application.isBatchMode)
                return;

            _applyQueued = true;
            EditorApplication.delayCall += () =>
            {
                _applyQueued = false;
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path != ScenePath)
                    return;
                if (Apply().Count > 0)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            };
        }

        /// <summary>Áp dụng mọi ảnh vẽ tay đang có vào scene đang mở. Trả về tên các file đã dùng.</summary>
        public static List<string> Apply()
        {
            Directory.CreateDirectory(Folder);
            DrawnArtSettings settings = DrawnArtSettings.Load(createIfMissing: true);
            var applied = new List<string>();

            if (ApplySky(settings.sky))
                applied.Add("sky");
            foreach (CaveLayout.Layer layer in CaveLayout.Backgrounds)
                if (ApplyLayer(layer, settings.LayerFor(layer.File)))
                    applied.Add(layer.File);
            if (ApplyCeiling(settings.ceiling))
                applied.Add("ceiling");
            if (ApplyLedges(settings.ledge))
                applied.Add("ledge");
            if (ApplyRocks(settings.rock))
                applied.Add("rock");
            if (ApplyColumns(settings.column))
                applied.Add("column");
            foreach (Transform column in FindByName("Column Art"))
                column.GetComponent<Renderer>().enabled = settings.showColumns;
            foreach (Transform ledge in FindByName("Ledge Art"))
            {
                Transform rim = ledge.parent.Find("Rim");
                if (rim != null)
                    rim.GetComponent<Renderer>().enabled = settings.showLedgeRim;
            }
            return applied;
        }

        // ---------- Từng loại ảnh ----------

        static bool ApplySky(DrawnArtSettings.Layer look)
        {
            Sprite sprite = Load("sky", new Vector2(0.5f, 0.5f), tight: false);
            Camera camera = Camera.main;
            Transform backdrop = camera != null ? camera.transform.Find("Backdrop") : null;
            if (sprite == null || backdrop == null)
                return false;

            // Phủ kín khung hình kể cả khi zoom xa nhất (cao 20 unit, rộng tới khoảng 46 unit trên màn dài)
            Vector2 size = sprite.bounds.size;
            float scale = Mathf.Max(30f / size.y, 52f / size.x) * look.scale;
            SetSprite(backdrop, sprite, new Vector2(look.offsetX, look.offsetY), Vector2.one * scale, look.tint, look.flipX);
            return true;
        }

        static bool ApplyLayer(CaveLayout.Layer layer, DrawnArtSettings.Layer look)
        {
            Sprite sprite = Load(layer.File, new Vector2(0.5f, 0.5f));
            GameObject root = GameObject.Find(layer.ObjectName);
            if (sprite == null || root == null)
                return false;

            ClearChildren(root.transform);
            (float min, float max, float midY) = CaveLayout.Range(layer);
            float scale = layer.Height / sprite.bounds.size.y * look.scale;
            float tileWidth = sprite.bounds.size.x * scale - 0.02f; // chồng mép một chút để không hở đường nối

            int index = 0;
            for (float x = min - tileWidth + Mathf.Repeat(look.offsetX, tileWidth); x < max; x += tileWidth, index++)
                NewSprite($"{layer.File} {index + 1}", root.transform, sprite, layer.SortingOrder,
                    new Vector2(x + tileWidth / 2f, midY + look.offsetY), scale, look.tint, Flip(look, index));
            return true;
        }

        static bool ApplyCeiling(DrawnArtSettings.Layer look)
        {
            Sprite sprite = Load("ceiling", new Vector2(0.5f, 1f));
            GameObject ceiling = GameObject.Find("Ceiling");
            Transform art = ceiling != null ? ceiling.transform.Find("Ceiling Art") : null;
            if (sprite == null || art == null)
                return false;

            ClearChildren(art);
            float scale = CaveLayout.CeilingArtHeight / sprite.bounds.size.y * look.scale;
            float tileWidth = sprite.bounds.size.x * scale - 0.02f;
            float min = CaveLayout.CameraMinX - CaveLayout.ViewHalfWidth - 6f;
            float max = CaveLayout.CameraMaxX + CaveLayout.ViewHalfWidth + 6f;

            int index = 0;
            for (float x = min - tileWidth + Mathf.Repeat(look.offsetX, tileWidth); x < max; x += tileWidth, index++)
                NewSprite($"ceiling {index + 1}", art, sprite, -105,
                    new Vector2(x + tileWidth / 2f, CaveLayout.CeilingArtTop + look.offsetY), scale, look.tint, Flip(look, index));
            return true;
        }

        // Mép trên ảnh = mặt sàn; co giãn đều theo bề rộng gờ đá
        static bool ApplyLedges(DrawnArtSettings.Piece look)
        {
            Sprite sprite = Load("ledge", new Vector2(0.5f, 1f));
            if (sprite == null)
                return false;

            foreach (Transform art in FindByName("Ledge Art"))
            {
                var box = art.parent.GetComponent<BoxCollider2D>();
                float top = box.offset.y + box.size.y / 2f;
                float scale = (box.size.x + 0.4f) / sprite.bounds.size.x * look.scale;
                float surface = look.surface * sprite.bounds.size.y * scale; // mặt đứng nằm thấp hơn mép trên ảnh
                SetSprite(art, sprite, new Vector2(0f, top + 0.05f + surface) + look.offset, Vector2.one * scale, look.tint, look.flipX);
                StraightRim(art.parent.Find("Rim"), box.size.x / 2f + 0.2f, top);
            }

            return true;
        }

        // Mép dưới ảnh = chỗ xúc tu bám (mặt dưới collider)
        static bool ApplyRocks(DrawnArtSettings.Piece look)
        {
            Sprite sprite = Load("rock", new Vector2(0.5f, 0f));
            if (sprite == null)
                return false;

            foreach (Transform art in FindByName("Rock Art"))
            {
                var box = art.parent.GetComponent<BoxCollider2D>();
                float bottom = box.offset.y - box.size.y / 2f;
                float scale = (box.size.x + 0.3f) / sprite.bounds.size.x * look.scale;
                float surface = look.surface * sprite.bounds.size.y * scale; // chỗ bám nằm cao hơn mép dưới ảnh
                SetSprite(art, sprite, new Vector2(0f, bottom - 0.05f - surface) + look.offset, Vector2.one * scale, look.tint, look.flipX);
                StraightRim(art.parent.Find("Glow Rim"), box.size.x / 2f, bottom);
            }

            return true;
        }

        // Cột đá giữ nguyên chiều cao (từ đỉnh tảng đá lên trần), chỉ thay hình; scale chỉnh bề ngang
        static bool ApplyColumns(DrawnArtSettings.Piece look)
        {
            Sprite sprite = Load("column", new Vector2(0.5f, 0f));
            if (sprite == null)
                return false;

            foreach (Transform art in FindByName("Column Art"))
            {
                float height = art.GetComponent<SpriteRenderer>().bounds.size.y;
                float width = art.parent.GetComponent<BoxCollider2D>().size.x * 0.7f * look.scale;
                Vector2 size = sprite.bounds.size;
                SetSprite(art, sprite, new Vector2(0f, CaveLayout.ColumnBottom) + look.offset,
                    new Vector2(width / size.x, height / size.y), look.tint, look.flipX);
            }

            return true;
        }

        // ---------- Khung mẫu để vẽ ----------

        [MenuItem("Octopus/Tạo khung mẫu để vẽ", priority = 21)]
        public static void CreateTemplatesFromMenu()
        {
            string folder = WriteTemplates();
            EditorUtility.RevealInFinder(folder);
        }

        /// <summary>
        /// Ảnh mẫu đúng kích thước khuyên dùng, nền trong suốt, kẻ vạch:
        /// xanh lá liền = khung camera bình thường, xanh lá đứt = phần có thể lộ ra khi camera lên cao / zoom xa,
        /// đỏ = mặt sàn gameplay, vàng = dải độ cao các tảng đá treo, xám = khung collider.
        /// </summary>
        public static string WriteTemplates()
        {
            string folder = Path.GetFullPath(TemplateFolder);
            Directory.CreateDirectory(folder);

            var green = new Color(0.2f, 1f, 0.4f, 0.9f);
            var red = new Color(1f, 0.25f, 0.25f, 0.9f);
            var yellow = new Color(1f, 0.85f, 0.2f, 0.5f);
            var gray = new Color(0.7f, 0.7f, 0.7f, 0.8f);

            // Lớp nền: ảnh cao Height unit, giữa ảnh ứng với midY của lớp
            foreach (CaveLayout.Layer layer in CaveLayout.Backgrounds)
            {
                float midY = CaveLayout.Range(layer).midY;
                float bottom = midY - layer.Height / 2f;
                float V(float localY) => (localY - bottom) / layer.Height;
                float camera = CaveLayout.CameraMidY;
                float drift = (CaveLayout.CameraMaxY - CaveLayout.CameraMidY) * (1f - layer.FactorY);
                float floor = CaveLayout.FloorY - layer.FactorY * camera;

                var lines = new List<(float v, Color color, bool dashed)>
                {
                    (V(midY + CaveLayout.NormalViewHalfHeight), green, false),
                    (V(midY - CaveLayout.NormalViewHalfHeight), green, false),
                    (V(midY + CaveLayout.MaxViewHalfHeight + drift), green, true),
                    (V(midY - CaveLayout.MaxViewHalfHeight - drift), green, true),
                    (V(floor), red, false),
                };
                float rockLow = V(CaveLayout.RockBandBottom - layer.FactorY * camera);
                float rockHigh = V(CaveLayout.RockBandTop - layer.FactorY * camera);
                WriteTemplate(folder, layer.File, 3840, 1080, lines, (u, v) => v > rockLow && v < rockHigh ? yellow * new Color(1f, 1f, 1f, 0.35f) : Color.clear);
            }

            // Phông: dính theo camera, giữa ảnh = giữa màn hình
            WriteTemplate(folder, "sky", 1920, 1080, new List<(float, Color, bool)>
            {
                (0.5f + CaveLayout.NormalViewHalfHeight / 30f, green, false),
                (0.5f - CaveLayout.NormalViewHalfHeight / 30f, green, false),
            }, (u, v) => Color.clear);

            // Trần: ảnh cao 8 unit, mép trên ở y = 18
            float CeilingV(float y) => 1f - (CaveLayout.CeilingArtTop - y) / CaveLayout.CeilingArtHeight;
            WriteTemplate(folder, "ceiling", 3840, 1080, new List<(float, Color, bool)>
            {
                (CeilingV(CaveLayout.CeilingColliderBottom), red, false),
                (CeilingV(CaveLayout.CameraMidY + CaveLayout.NormalViewHalfHeight), green, false),
            }, (u, v) => Color.clear);

            // Gờ đá: ảnh vuông, bề rộng = gờ rộng nhất (10 unit) + 0.4; mép trên = mặt sàn, khung xám = collider cao 1 unit
            const float ledgeWidth = 10.4f;
            WriteTemplate(folder, "ledge", 2048, 2048, new List<(float, Color, bool)> { (0.995f, red, false) },
                (u, v) => Frame(u, v, 0.2f / ledgeWidth, 1f - 0.2f / ledgeWidth, 1f - 1f / ledgeWidth, 1f, 0.002f) ? gray : Color.clear);

            // Tảng đá: tỉ lệ 2:1, bề rộng = đá 2.5 unit + 0.3; mép dưới = chỗ bám, khung xám = collider cao 1 unit
            const float rockWidth = 2.8f;
            WriteTemplate(folder, "rock", 1024, 512, new List<(float, Color, bool)> { (0.008f, red, false) },
                (u, v) => Frame(u, v, 0.15f / rockWidth, 1f - 0.15f / rockWidth, 0f, 2f / rockWidth, 0.004f) ? gray : Color.clear);

            WriteTemplate(folder, "column", 512, 2048, new List<(float, Color, bool)>(),
                (u, v) => Mathf.Abs(u - 0.5f) < 0.003f ? gray : Color.clear);

            return folder;
        }

        static void WriteTemplate(string folder, string name, int width, int height,
            List<(float v, Color color, bool dashed)> lines, Func<float, float, Color> fill)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;
                Color color = fill(u, v);
                foreach ((float lineV, Color lineColor, bool dashed) in lines)
                    if (Mathf.Abs(v - lineV) * height < 2.5f && (!dashed || x % 48 < 28))
                        color = lineColor;
                pixels[y * width + x] = color;
            }

            texture.SetPixels(pixels);
            File.WriteAllBytes(Path.Combine(folder, $"{name}.png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        static bool Frame(float u, float v, float left, float right, float bottom, float top, float thickness)
        {
            bool insideX = u >= left - thickness && u <= right + thickness;
            bool insideY = v >= bottom - thickness && v <= top + thickness;
            bool nearX = Mathf.Abs(u - left) < thickness || Mathf.Abs(u - right) < thickness;
            bool nearY = Mathf.Abs(v - bottom) < thickness || Mathf.Abs(v - top) < thickness;
            return (nearX && insideY) || (nearY && insideX);
        }

        // ---------- Tiện ích ----------

        /// <summary>
        /// Nạp ảnh vẽ tay (nếu có) và chỉnh cài đặt import: sprite, pivot, khổ tối đa 4096, nén chất lượng cao.
        /// Chỉ import lại khi cài đặt khác đi, để việc tự áp dụng sau mỗi lần import không lặp vô tận.
        /// </summary>
        static Sprite Load(string name, Vector2 pivot, bool tight = true)
        {
            string path = $"{Folder}/{name}.png";
            if (!File.Exists(path))
                return null;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    return null;
            }

            bool changed = false;
            void Set<T>(T current, T wanted, Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted))
                    return;
                apply(wanted);
                changed = true;
            }

            Set(importer.textureType, TextureImporterType.Sprite, v => importer.textureType = v);
            Set(importer.spriteImportMode, SpriteImportMode.Single, v => importer.spriteImportMode = v);
            Set(importer.mipmapEnabled, false, v => importer.mipmapEnabled = v);
            Set(importer.alphaIsTransparency, true, v => importer.alphaIsTransparency = v);
            Set(importer.maxTextureSize, 4096, v => importer.maxTextureSize = v);
            Set(importer.textureCompression, TextureImporterCompression.CompressedHQ, v => importer.textureCompression = v);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            SpriteMeshType meshType = tight ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom || settings.spritePivot != pivot || settings.spriteMeshType != meshType)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = meshType;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Lặp soi gương: ô lẻ lật ngược để mép ảnh luôn nối liền
        static bool Flip(DrawnArtSettings.Layer look, int index) => look.flipX ^ (look.mirrorTiles && index % 2 == 1);

        static void SetSprite(Transform art, Sprite sprite, Vector2 localPosition, Vector2 scale, Color tint, bool flipX)
        {
            var renderer = art.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.flipX = flipX;
            art.localPosition = new Vector3(localPosition.x, localPosition.y, art.localPosition.z);
            art.localScale = new Vector3(scale.x, scale.y, 1f);
        }

        static void NewSprite(string name, Transform parent, Sprite sprite, int sortingOrder, Vector2 localPosition, float scale, Color tint, bool flipX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = tint;
            renderer.flipX = flipX;
        }

        // Viền sáng thành đường thẳng theo mép collider (ảnh vẽ tay không có đường viền để bám theo)
        static void StraightRim(Transform rim, float halfWidth, float y)
        {
            if (rim == null || !rim.TryGetComponent(out LineRenderer line))
                return;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-halfWidth, y, 0f));
            line.SetPosition(1, new Vector3(halfWidth, y, 0f));
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        static IEnumerable<Transform> FindByName(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == name);
    }

    /// <summary>Lưu ảnh vào Art/Drawn (thêm mới hoặc sửa) là scene Playground tự cập nhật, nếu bảng chỉnh đang bật autoApply.</summary>
    class DrawnArtWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Application.isBatchMode)
                return;
            bool touched = imported.Concat(moved).Any(path => path.StartsWith(DrawnArt.Folder + "/") && path.EndsWith(".png"));
            if (!touched)
                return;

            DrawnArtSettings settings = DrawnArtSettings.Load(createIfMissing: false);
            if (settings == null || settings.autoApply)
                DrawnArt.QueueApply();
        }
    }
}
