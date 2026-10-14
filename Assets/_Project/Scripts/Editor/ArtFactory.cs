using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Vẽ art bằng code: tính màu từng pixel rồi lưu PNG vào Assets/_Project/Art/Generated.
    /// Mỗi lần chạy Setup đều vẽ lại, nên sửa công thức ở đây là art cập nhật theo.
    /// </summary>
    static class ArtFactory
    {
        public const string Folder = "Assets/_Project/Art/Generated";

        /// <summary>pixel(u, v) nhận toạ độ 0..1 (gốc dưới-trái). supersample > 1 để khử răng cưa mép hình.</summary>
        public static Sprite Sprite(string name, int width, int height, float pixelsPerUnit, Vector2 pivot,
            Func<float, float, Color> pixel, int supersample = 2, bool tightMesh = true)
        {
            string path = Save(name, width, height, pixel, supersample);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
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
            settings.spriteMeshType = tightMesh ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Texture thường (không phải sprite), dùng cho LineRenderer.</summary>
        public static Texture2D Texture(string name, int width, int height, TextureWrapMode wrapU, Func<float, float, Color> pixel)
        {
            string path = Save(name, width, height, pixel, 1);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapModeU = wrapU;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material Material(string name, Texture texture)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Folder);
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        static string Save(string name, int width, int height, Func<float, float, Color> pixel, int supersample)
        {
            Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.png";
            var colors = new Color[width * height];
            int samples = Mathf.Max(1, supersample);
            float step = 1f / samples;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Trung bình có trọng số alpha để mép không bị viền đen
                Vector3 rgb = Vector3.zero;
                float alpha = 0f;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    Color c = pixel((x + (sx + 0.5f) * step) / width, (y + (sy + 0.5f) * step) / height);
                    rgb += new Vector3(c.r, c.g, c.b) * c.a;
                    alpha += c.a;
                }

                Color result = alpha > 0f ? new Color(rgb.x / alpha, rgb.y / alpha, rgb.z / alpha, alpha / (samples * samples)) : Color.clear;
                colors[y * width + x] = result;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(colors);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }
    }

    /// <summary>Value noise 2D có thể lặp lại theo seed, dùng tạo vân đá, mép gồ ghề.</summary>
    static class Noise
    {
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        public static float Value(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);
            float a = Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), u);
            float b = Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), u);
            return Mathf.Lerp(a, b, v);
        }

        /// <summary>Nhiều tầng noise chồng lên nhau, kết quả 0..1.</summary>
        public static float Fbm(float x, float y, int seed, int octaves = 4)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y, seed + i * 31) * amplitude;
                total += amplitude;
                x *= 2.03f;
                y *= 2.03f;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        /// <summary>Noise 1D quanh 0 (-0.5..0.5) cho mép gồ ghề.</summary>
        public static float Edge(float t, int seed) => Fbm(t, seed * 0.37f, seed, 3) - 0.5f;
    }

    /// <summary>Đa giác 2D: kiểm tra điểm trong / ngoài và khoảng cách tới mép.</summary>
    static class Polygon
    {
        public static bool Contains(IReadOnlyList<Vector2> points, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }

        public static float DistanceToEdge(IReadOnlyList<Vector2> points, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                Vector2 a = points[j];
                Vector2 ab = points[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, (a + ab * t - p).sqrMagnitude);
            }

            return Mathf.Sqrt(best);
        }

        public static Rect Bounds(IReadOnlyList<Vector2> points)
        {
            Vector2 min = points[0];
            Vector2 max = points[0];
            foreach (Vector2 p in points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
