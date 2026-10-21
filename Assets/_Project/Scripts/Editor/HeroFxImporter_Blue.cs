using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Ảnh hiệu ứng chiêu của BLUE trong Resources/HeroFx/Blue (Tools/art/hero_fx_blue.py cắt từ tấm chiêu): tự nhập thành
    /// Sprite một ảnh, 134 px = 1 unit (cỡ như trên tấm chiêu so với BLUE cao 2 unit), không mipmap, mép kẹp. Điểm neo (pivot)
    /// lấy từ manifest.json cùng thư mục (script cắt ghi: chân, ngực, tâm vòng… của từng mảng); mảng không có trong manifest thì
    /// neo giữa ảnh. manifest.json đổi thì nhập lại mọi ảnh trong thư mục (pivot mới).
    /// Chỉ đụng tới thư mục này; BlueFx nạp lúc chạy bằng Resources.Load&lt;Sprite&gt;.
    /// </summary>
    public class HeroFxImporterBlue : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/HeroFx/Blue/";
        const string ManifestPath = Folder + "manifest.json";

        /// <summary>Số px ảnh cho 1 unit (BlueFx.PixelsPerUnit giữ cùng số).</summary>
        public const float PixelsPerUnit = 134f;

        [Serializable]
        class Piece
        {
            public string name;
            public float[] pivot;
        }

        [Serializable]
        class Manifest
        {
            public Piece[] pieces;
        }

        // Đổi số này khi đổi cách nhập: Unity nhập lại mọi ảnh
        public override uint GetVersion() => 2;

        static Dictionary<string, Vector2> ReadPivots()
        {
            var pivots = new Dictionary<string, Vector2>();
            if (!File.Exists(ManifestPath))
                return pivots;
            Manifest m = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (m?.pieces == null)
                return pivots;
            foreach (Piece p in m.pieces)
                if (p.pivot != null && p.pivot.Length == 2)
                    pivots[p.name] = new Vector2(p.pivot[0], p.pivot[1]);
            return pivots;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (ReadPivots().TryGetValue(name, out Vector2 pivot))
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
            }
            else
                settings.spriteAlignment = (int)SpriteAlignment.Center;

            settings.spriteMeshType = SpriteMeshType.Tight; // lược phần trong suốt: đỡ tô thừa trên điện thoại
            importer.SetTextureSettings(settings);
        }

        // manifest.json đổi (cắt lại) → nhập lại mọi ảnh trong thư mục để nhận pivot mới
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Array.IndexOf(imported, ManifestPath) < 0)
                return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png"))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
