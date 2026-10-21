using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Kiểu import cho ảnh hiệu ứng của RED (Assets/_Project/Resources/HeroFx/Red, Tools/art/hero_fx_red.py cắt từ các tấm
    /// chiêu): Sprite đơn, không mipmap, khung chữ nhật đầy đủ (xích lát nối tiếp được), pivot + PPU theo pivots.json cạnh ảnh
    /// (script cắt ghi ra). Chạy lại script cắt → pivots.json đổi → các ảnh trong thư mục tự import lại.
    /// </summary>
    sealed class HeroFxImporter_Red : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/HeroFx/Red/";
        const string PivotFile = Folder + "pivots.json";

        [Serializable]
        sealed class Entry
        {
            public string name;
            public float x = 0.5f, y = 0.5f, ppu = 150f;
        }

        [Serializable]
        sealed class Table
        {
            public Entry[] items = Array.Empty<Entry>();
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            Entry entry = Find(Path.GetFileNameWithoutExtension(assetPath));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(entry.x, entry.y);
            settings.spritePixelsPerUnit = entry.ppu;
            settings.spriteExtrude = 1;
            importer.SetTextureSettings(settings);
        }

        // pivots.json vừa đổi: import lại các ảnh trong thư mục cho nhận pivot mới
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Array.IndexOf(imported, PivotFile) < 0)
                return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;
                Entry entry = Find(Path.GetFileNameWithoutExtension(path));
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spritePivot != new Vector2(entry.x, entry.y) || !Mathf.Approximately(settings.spritePixelsPerUnit, entry.ppu))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        static Entry Find(string name)
        {
            string file = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", PivotFile);
            if (File.Exists(file))
            {
                Table table = JsonUtility.FromJson<Table>(File.ReadAllText(file));
                if (table?.items != null)
                    foreach (Entry e in table.items)
                        if (e != null && e.name == name)
                            return e;
            }

            return new Entry { name = name };
        }
    }
}
