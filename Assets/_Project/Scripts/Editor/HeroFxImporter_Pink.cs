using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Ảnh hiệu ứng chiêu PINK (Assets/_Project/Resources/HeroFx/Pink, do Tools/art/hero_fx_pink.py cắt từ tấm dáng chiêu):
    /// import thành Sprite một ảnh, số px / unit và gốc (pivot) lấy từ fx_manifest.json cạnh ảnh (script ghi ra), không mipmap,
    /// mép trong suốt kéo màu ra (không viền tối khi thu nhỏ). Chỉ đụng tới thư mục này.
    /// </summary>
    class HeroFxImporter_Pink : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/HeroFx/Pink/";

        [Serializable]
        class Manifest
        {
            public Entry[] sprites = Array.Empty<Entry>();
        }

        [Serializable]
        class Entry
        {
            public string name;
            public float pixelsPerUnit = 100f;
            public float[] pivot = { 0.5f, 0.5f };
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            Entry entry = Find(Path.GetFileNameWithoutExtension(assetPath));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // ảnh co giãn x / y riêng, lưới chữ nhật là đủ (ít đỉnh nhất)
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            if (entry != null)
            {
                settings.spritePixelsPerUnit = entry.pixelsPerUnit;
                if (entry.pivot != null && entry.pivot.Length >= 2)
                    settings.spritePivot = new Vector2(entry.pivot[0], entry.pivot[1]);
            }
            else
                settings.spritePivot = new Vector2(0.5f, 0.5f);

            importer.SetTextureSettings(settings);
        }

        static Entry Find(string name)
        {
            string path = Path.Combine(Application.dataPath, "..", Folder, "fx_manifest.json");
            if (!File.Exists(path))
                return null;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            foreach (Entry entry in manifest.sprites)
            {
                if (entry.name == name)
                    return entry;
            }

            return null;
        }
    }
}
