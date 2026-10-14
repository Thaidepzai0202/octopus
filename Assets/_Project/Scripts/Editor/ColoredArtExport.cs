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
    /// Xuất art hiện tại ra ảnh đã có màu (như trong game) vào Docs/colored-art để xem và chỉnh:
    ///  - sky / far / mid / near / front / ceiling / ledge / rock / column.png: đúng định dạng ảnh vẽ tay,
    ///    chỉnh xong chép vào Assets/_Project/Art/Drawn là game dùng ngay.
    ///  - pieces/: từng mảnh riêng lẻ đã tô màu (nhũ đá, cột đá, tảng đá, thân bạch tuộc…) để xem.
    /// </summary>
    public static class ColoredArtExport
    {
        const string Folder = "Docs/colored-art";
        const string ScenePath = "Assets/_Project/Scenes/Playground.unity";

        [MenuItem("Octopus/Xuất ảnh màu hiện tại", priority = 23)]
        public static void ExportFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            EditorUtility.RevealInFinder(Export());
        }

        public static void ExportFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[Octopus] Đã xuất ảnh màu vào " + Export());
        }

        public static string Export()
        {
            string folder = Path.GetFullPath(Folder);
            Directory.CreateDirectory(Path.Combine(folder, "pieces"));
            ExportPieces(Path.Combine(folder, "pieces"));
            ExportLayers(folder);
            File.WriteAllText(Path.Combine(folder, "_DOC_TOI.txt"),
                "Ảnh art hiện tại, đã có màu như trong game.\n\n" +
                "sky, far, mid, near, front, ceiling, ledge, rock, column.png: đúng định dạng ảnh vẽ tay.\n" +
                "  Mở ra chỉnh, rồi lưu (cùng tên) vào Assets/_Project/Art/Drawn là game dùng ảnh của bạn.\n\n" +
                "pieces/: từng mảnh riêng lẻ đã tô màu, để xem và làm mẫu.\n" +
                "Thư mục này được ghi đè mỗi lần chạy menu Octopus → Xuất ảnh màu hiện tại.\n");
            return folder;
        }

        // ---------- Từng mảnh, nhân màu nhuộm vào ảnh xám ----------

        static void ExportPieces(string folder)
        {
            var written = new HashSet<string>();
            foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                Sprite sprite = renderer.sprite;
                if (sprite == null || !AssetDatabase.GetAssetPath(sprite).StartsWith(ArtFactory.Folder))
                    continue;

                // Cùng một hình nhuộm màu khác ở lớp khác thì ghi kèm tên lớp
                string name = sprite.name;
                string key = name + ColorUtility.ToHtmlStringRGBA(renderer.color);
                if (!written.Add(key))
                    continue;
                if (written.Any(k => k != key && k.StartsWith(name) && k.Length == key.Length))
                    name += " - " + renderer.transform.root.name;
                WriteTinted(sprite.texture, renderer.color, Path.Combine(folder, name + ".png"));
            }

            // Bạch tuộc: thân và xúc tu được nhuộm bằng màu trong OctopusAnimator
            var animator = Object.FindAnyObjectByType<OctopusAnimator>();
            if (animator == null)
                return;
            var settings = new SerializedObject(animator);
            Color body = settings.FindProperty("bodyColor").colorValue;
            Color ink = settings.FindProperty("inkColor").colorValue;
            Color tip = Color.Lerp(body, ink, settings.FindProperty("tipInk").floatValue);
            WriteTinted(Load("OctopusBody"), body, Path.Combine(folder, "OctopusBody.png"));
            WriteTinted(Load("OctopusTentacle"), body, Path.Combine(folder, "OctopusTentacle.png"));
            WriteTinted(Load("TentaclePad"), tip, Path.Combine(folder, "TentaclePad.png"));
        }

        static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFactory.Folder}/{name}.png");

        static void WriteTinted(Texture2D source, Color tint, string path)
        {
            if (source == null)
                return;
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] *= tint;
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        // ---------- Cả lớp, chụp bằng camera tạm ----------

        static void ExportLayers(string folder)
        {
            // Khung chụp khớp đúng cách DrawnArt đặt ảnh, nên chép thẳng vào Art/Drawn là ra y như cũ
            Camera main = Camera.main;
            Transform backdrop = main != null ? main.transform.Find("Backdrop") : null;
            if (backdrop != null)
            {
                Bounds b = backdrop.GetComponent<Renderer>().bounds;
                Capture(Path.Combine(folder, "sky.png"), Renderers(backdrop), new Rect(b.min, b.size), 1920, 1080);
            }

            foreach (CaveLayout.Layer layer in CaveLayout.Backgrounds)
            {
                GameObject root = GameObject.Find(layer.ObjectName);
                if (root == null)
                    continue;
                (float min, float max, float midY) = CaveLayout.Range(layer);
                Capture(Path.Combine(folder, layer.File + ".png"), Renderers(root.transform),
                    new Rect(min, midY - layer.Height / 2f, max - min, layer.Height), 4096, 1080);
            }

            Transform ceilingArt = GameObject.Find("Ceiling")?.transform.Find("Ceiling Art");
            if (ceilingArt != null)
            {
                float min = CaveLayout.CameraMinX - CaveLayout.ViewHalfWidth - 6f;
                float max = CaveLayout.CameraMaxX + CaveLayout.ViewHalfWidth + 6f;
                Capture(Path.Combine(folder, "ceiling.png"), Renderers(ceilingArt),
                    new Rect(min, CaveLayout.CeilingArtTop - CaveLayout.CeilingArtHeight, max - min, CaveLayout.CeilingArtHeight), 4096, 1080);
            }

            Transform ledge = FindByName("Ledge Art").OrderByDescending(t => t.parent.GetComponent<BoxCollider2D>().size.x).FirstOrDefault();
            if (ledge != null)
            {
                var box = ledge.parent.GetComponent<BoxCollider2D>();
                float width = box.size.x + 0.4f;
                Vector2 top = (Vector2)ledge.parent.position + new Vector2(0f, box.offset.y + box.size.y / 2f + 0.05f);
                Capture(Path.Combine(folder, "ledge.png"), Renderers(ledge), new Rect(top.x - width / 2f, top.y - width, width, width), 2048, 2048);
            }

            Transform rock = FindByName("Rock Art").FirstOrDefault(t => Mathf.Approximately(t.parent.GetComponent<BoxCollider2D>().size.x, 2.5f))
                             ?? FindByName("Rock Art").FirstOrDefault();
            if (rock != null)
            {
                var box = rock.parent.GetComponent<BoxCollider2D>();
                float width = box.size.x + 0.3f;
                float bottom = rock.parent.position.y + box.offset.y - box.size.y / 2f - 0.05f;
                float height = rock.GetComponent<Renderer>().bounds.max.y + 0.05f - bottom;
                Capture(Path.Combine(folder, "rock.png"), Renderers(rock), new Rect(rock.parent.position.x - width / 2f, bottom, width, height), 1024, 1024);

                Transform column = rock.parent.Find("Column Art");
                if (column != null)
                {
                    Bounds b = column.GetComponent<Renderer>().bounds;
                    float columnWidth = box.size.x * 0.7f;
                    Capture(Path.Combine(folder, "column.png"), Renderers(column),
                        new Rect(rock.parent.position.x - columnWidth / 2f, b.min.y, columnWidth, b.size.y), 512, 2048);
                }
            }
        }

        static IEnumerable<Renderer> Renderers(Transform root) => root.GetComponentsInChildren<Renderer>();

        /// <summary>Chụp riêng các renderer cho trước trong vùng world, nền trong suốt, khổ ảnh không vượt quá maxWidth × maxHeight.</summary>
        static void Capture(string path, IEnumerable<Renderer> targets, Rect region, int maxWidth, int maxHeight)
        {
            float aspect = region.width / region.height;
            int height = Mathf.Min(maxHeight, Mathf.RoundToInt(maxWidth / aspect));
            int width = Mathf.Max(1, Mathf.RoundToInt(height * aspect));

            // Tạm tắt mọi renderer khác để chỉ chụp đúng lớp cần xuất
            var keep = new HashSet<Renderer>(targets);
            var hidden = new List<Renderer>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.enabled && !keep.Contains(renderer))
                {
                    renderer.enabled = false;
                    hidden.Add(renderer);
                }
            }

            var cameraObject = new GameObject("Export Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = region.height / 2f;
            camera.aspect = aspect;
            camera.transform.position = new Vector3(region.center.x, region.center.y, -50f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = null;

            // Ảnh chụp trên nền trong suốt bị nhân sẵn alpha: chia lại để mép mờ không bị sạm
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a > 0.004f)
                    pixels[i] = new Color(Mathf.Clamp01(c.r / c.a), Mathf.Clamp01(c.g / c.a), Mathf.Clamp01(c.b / c.a), c.a);
            }

            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            foreach (Renderer renderer in hidden)
                renderer.enabled = true;
        }

        static IEnumerable<Transform> FindByName(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == name);
    }
}
