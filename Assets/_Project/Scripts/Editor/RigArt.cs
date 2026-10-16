using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Phần chung của các nhân vật gắn xương (bạch tuộc, nhện): đọc mục bộ phận trong rig.json do Tools/art/*_rig.py
    /// xuất ra, import ảnh thành Sprite kèm xương / lưới / trọng số (đúng chỗ Skinning Editor lưu), dựng xương + SpriteSkin.
    /// </summary>
    static class RigArt
    {
        [Serializable]
        public class PartInfo
        {
            public string name;
            public string file;
            [Tooltip("Cỡ ảnh bộ phận (pixel)")]
            public float[] size = Array.Empty<float>();
            public float[] pivot = Array.Empty<float>();
            public float[] position = Array.Empty<float>();
            public int order;
            public BoneInfo[] bones = Array.Empty<BoneInfo>();
            public float[] contact = Array.Empty<float>();
            public float contactJoint;
            public float[] vertices = Array.Empty<float>();
            public int[] indices = Array.Empty<int>();
            public int[] edges = Array.Empty<int>();
            public float[] weights = Array.Empty<float>();
            [Tooltip("Số xương mỗi đỉnh lưới (2 bản cũ, 4 bản mới)")]
            public int influences = 2;
            [Tooltip("Số xương đầu chuỗi (phần còn lại là xương vây quanh đầu); -1 = cả chuỗi")]
            public int chainBones = -1;
            public int eyeBone = 1;
        }

        [Serializable]
        public class BoneInfo
        {
            public string name;
            public float[] position = Array.Empty<float>();
            public float rotation;
            public float length;
            public int parent = -2; // -2 = (bản cũ) nối tiếp xương trước
        }

        public static Sprite Import(string folder, PartInfo info, float pixelsPerUnit)
        {
            string path = $"{folder}/{info.file}";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                throw new FileNotFoundException("Thiếu ảnh bộ phận", path);

            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D; // .meta tạo tay chỉ có guid thì Unity mặc định Cubemap
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            // Ảnh xuất nét hơn màn hình cần; mipmap lọc Kaiser + lệch -0.3 để thu nhỏ trên điện thoại nhỏ vẫn sắc,
            // không lấm tấm răng cưa như khi không có mipmap
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.mipMapBias = -0.3f;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // ảnh nhỏ, giữ nét
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(info.pivot[0], info.pivot[1]);
            settings.spriteMeshType = info.bones.Length > 0 ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            // Chỉ ghi lại xương / lưới khi rig.json đổi (chạy lại script tách ảnh); sửa tay trong Skinning Editor được giữ
            string hash = "rig:" + Hash128.Compute(JsonUtility.ToJson(info));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            bool hasBones = sprite != null && sprite.GetBones().Length > 0;
            if (info.bones.Length > 0 && (importer.userData != hash || !hasBones))
            {
                WriteSkinning(importer, info);
                importer.userData = hash;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Ghi xương, lưới, trọng số vào dữ liệu Sprite Editor của ảnh (cùng chỗ Skinning Editor lưu)
        static void WriteSkinning(TextureImporter importer, PartInfo info)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider data = factory.GetSpriteEditorDataProviderFromObject(importer);
            data.InitSpriteEditorDataProvider();
            GUID id = data.GetSpriteRects()[0].spriteID;

            var bones = new List<SpriteBone>(info.bones.Length);
            for (int i = 0; i < info.bones.Length; i++)
            {
                BoneInfo b = info.bones[i];
                bones.Add(new SpriteBone
                {
                    name = b.name,
                    guid = GUID.Generate().ToString(),
                    position = new Vector3(b.position[0], b.position[1], 0f), // pixel; xương gốc tính từ góc dưới-trái ảnh
                    rotation = Quaternion.Euler(0f, 0f, b.rotation),
                    length = b.length,
                    parentId = b.parent == -2 ? i - 1 : b.parent,
                    color = Color.HSVToRGB((float)i / info.bones.Length, 0.7f, 1f),
                });
            }

            data.GetDataProvider<ISpriteBoneDataProvider>().SetBones(id, bones);

            int count = info.vertices.Length / 2;
            int stride = Mathf.Max(1, info.influences) * 2;
            var vertices = new Vertex2DMetaData[count];
            var pairs = new List<(int bone, float weight)>(4);
            for (int v = 0; v < count; v++)
            {
                pairs.Clear();
                for (int k = 0; k < stride / 2; k++)
                    pairs.Add(((int)info.weights[v * stride + k * 2], info.weights[v * stride + k * 2 + 1]));
                pairs.Sort((a, b) => b.weight.CompareTo(a.weight)); // Unity cần trọng số giảm dần
                while (pairs.Count < 4)
                    pairs.Add((0, 0f));

                vertices[v] = new Vertex2DMetaData
                {
                    position = new Vector2(info.vertices[v * 2], info.vertices[v * 2 + 1]),
                    boneWeight = new BoneWeight
                    {
                        boneIndex0 = pairs[0].bone, weight0 = pairs[0].weight,
                        boneIndex1 = pairs[1].bone, weight1 = pairs[1].weight,
                        boneIndex2 = pairs[2].bone, weight2 = pairs[2].weight,
                        boneIndex3 = pairs[3].bone, weight3 = pairs[3].weight,
                    },
                };
            }

            var edges = new Vector2Int[info.edges.Length / 2];
            for (int e = 0; e < edges.Length; e++)
                edges[e] = new Vector2Int(info.edges[e * 2], info.edges[e * 2 + 1]);

            var mesh = data.GetDataProvider<ISpriteMeshDataProvider>();
            mesh.SetVertices(id, vertices);
            mesh.SetIndices(id, info.indices);
            mesh.SetEdges(id, edges);
            data.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>Tạo transform cho mọi xương của sprite (xương gốc nằm trong root, xương con theo cha) và gắn SpriteSkin.</summary>
        public static Transform[] SkinBones(SpriteRenderer renderer, Transform root)
        {
            SpriteBone[] spriteBones = renderer.sprite.GetBones();
            var all = new Transform[spriteBones.Length];
            for (int b = 0; b < spriteBones.Length; b++)
            {
                SpriteBone bone = spriteBones[b];
                Transform t = NewChild(bone.name, bone.parentId < 0 ? root : all[bone.parentId]);
                t.localPosition = bone.parentId < 0 ? renderer.transform.localPosition + bone.position : bone.position;
                t.localRotation = bone.rotation;
                all[b] = t;
            }

            var skin = renderer.gameObject.AddComponent<SpriteSkin>();
            skin.SetRootBone(all[0]);
            skin.SetBoneTransforms(all);
            skin.alwaysUpdate = true; // bộ phận uốn ra ngoài khung ảnh gốc vẫn phải vẽ
            return all;
        }

        public static Vector3 Vec(float[] xy) => new Vector3(xy[0], xy[1], 0f);

        public static Transform NewChild(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        public static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var renderer = NewChild(name, parent).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
