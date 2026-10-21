using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;
using static Octopus.EditorTools.RigArt;
using Object = UnityEngine.Object;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng phần hình boss sư tử (object con "Lion Visual" dưới parent, gốc ở giữa hai bàn chân sát sàn, cao height unit,
    /// quay sang phải) từ Art/Lion/Rig (do Tools/art/lion_rig.py cắt từ Docs/boss_su_tu/su_tu/lion_component.png):
    /// Lion Visual (LionRigAnimator) → Pose (lật khi quay đầu, tỉ lệ theo height) → Body → các lớp ảnh.
    /// Xương: Hips → Chest → Neck (đầu thường + đầu gầm, miệng, quầng mắt) / Mane (bờm, lọn lửa) / 2 chân trước;
    /// Hips → 2 chân sau, đuôi. Mỗi lớp có xương là SpriteRenderer + SpriteSkin (mở Skinning Editor là thấy, sửa được).
    /// Thứ tự vẽ: dải SortingBase … SortingBase + 17 (-39 … -22): trước nền / đá (≈ -90) và vết nứt trên sàn (-40), sau
    /// người chơi (-6 … 12) và hiệu ứng chiêu (lớp sau lưng -17 … -10, lớp trước 14+).
    /// Chưa có Art/Lion/Rig thì dựng khối màu tạm (LionPlaceholderVisual).
    /// </summary>
    public static class LionRigBuilder
    {
        public const string ArtFolder = "Assets/_Project/Art/Lion";
        public const string Folder = ArtFolder + "/Rig";
        public const int SortingBase = -39;
        const string VisualName = "Lion Visual";

        [Serializable]
        class Manifest
        {
            public float pixelsPerUnit = 150f;
            public float height = 3.89f;
            public PartInfo[] parts = Array.Empty<PartInfo>();
            public LegInfo[] legs = Array.Empty<LegInfo>();
            public FlameInfo[] flames = Array.Empty<FlameInfo>();
            public float[] headPivot = Array.Empty<float>();
            public float[] mouth = Array.Empty<float>();
            public float[] eye = Array.Empty<float>();
            public float[] maneRoot = Array.Empty<float>();
            public float[] maneSweep = Array.Empty<float>();
            public float[] bounds = Array.Empty<float>();
        }

        [Serializable]
        class LegInfo
        {
            public string part;
            public bool front;
            public bool near;
            [Tooltip("Gốc, khớp, cổ chân, đầu móng (unit tranh)")]
            public float[] joints = Array.Empty<float>();
            [Tooltip("Tâm bước khi đi (unit tranh)")]
            public float[] walk = Array.Empty<float>();
        }

        [Serializable]
        class FlameInfo
        {
            public string part;
            public bool front;
        }

        public static ILionVisual Build(Transform parent, float height)
        {
            string manifestPath = $"{Folder}/rig.json";
            if (!File.Exists(manifestPath))
                return BuildPlaceholder(parent, height);

            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            PartInfo Part(string name) => Array.Find(manifest.parts, p => p.name == name);
            float ppu = manifest.pixelsPerUnit;
            Material material = LionMaterial();
            Material glowMaterial = GlowMaterial();
            var renderers = new List<SpriteRenderer>();

            SpriteRenderer Add(Transform at, string name, PartInfo info, Vector3 localPosition)
            {
                SpriteRenderer r = NewSprite(name, at, Import(Folder, info, ppu), SortingBase + info.order);
                r.transform.localPosition = localPosition;
                r.sharedMaterial = material;
                renderers.Add(r);
                return r;
            }

            Transform root = NewChild(VisualName, parent);
            Transform pose = NewChild("Pose", root);
            Transform body = NewChild("Body", pose);

            // Thân: Hips → Chest
            PartInfo torsoInfo = Part("torso");
            SpriteRenderer torso = Add(body, "Torso", torsoInfo, Vec(torsoInfo.position));
            Transform[] spine = SkinBones(torso, body);
            Transform hips = spine[0], chest = spine[1];

            // Cổ + đầu (đầu thường, đầu gầm cùng gốc ở khớp cổ). Cổ quay lại cho trục trùng Body để toạ độ tranh dùng thẳng
            Transform neck = NewChild("Neck", chest);
            neck.position = body.TransformPoint(Vec(manifest.headPivot));
            neck.rotation = body.rotation;
            PartInfo headInfo = Part("head");
            SpriteRenderer headArt = Add(neck, "Head Art", headInfo, Vector3.zero);
            PartInfo roarInfo = Part("head_roar");
            SpriteRenderer roarArt = Add(neck, "Roar Art", roarInfo, Vector3.zero);
            roarArt.enabled = false;
            Transform mouth = NewChild("Mouth", neck);
            mouth.localPosition = Vec(manifest.mouth) - Vec(manifest.headPivot);
            Sprite glow = Import(Folder, Part("glow"), 128f);
            SpriteRenderer eyeGlow = NewSprite("Eye Glow", neck, glow, SortingBase + Part("glow").order);
            eyeGlow.sharedMaterial = glowMaterial;
            eyeGlow.transform.localPosition = Vec(manifest.eye) - Vec(manifest.headPivot);

            // Bờm: gốc theo ngực; lọn lửa rời gắn vào gốc bờm
            PartInfo maneInfo = Part("mane");
            SpriteRenderer maneArt = Add(body, "Mane", maneInfo, Vec(maneInfo.position));
            Transform[] maneBones = SkinBones(maneArt, body);
            maneBones[0].SetParent(chest, true);

            var flameChains = new List<Transform[]>();
            var flameFront = new List<bool>();
            foreach (FlameInfo flame in manifest.flames)
            {
                PartInfo info = Part(flame.part);
                SpriteRenderer r = Add(body, $"Flame {flame.part}", info, Vec(info.position));
                if (!flame.front)
                    r.color = new Color(0.82f, 0.78f, 0.8f); // lửa sau bờm tối hơn chút cho có chiều sâu
                Transform[] bones = SkinBones(r, body);
                bones[0].SetParent(maneBones[0], true);
                flameChains.Add(bones);
                flameFront.Add(flame.front);
            }

            // Đuôi theo hông
            PartInfo tailInfo = Part("tail");
            SpriteRenderer tailArt = Add(body, "Tail", tailInfo, Vec(tailInfo.position));
            Transform[] tailBones = SkinBones(tailArt, body);
            tailBones[0].SetParent(hips, true);
            SpriteBone[] tailSprite = tailArt.sprite.GetBones();

            // Hào quang nổi giận (sau cả con)
            SpriteRenderer aura = NewSprite("Aura", hips, glow, SortingBase);
            aura.sharedMaterial = glowMaterial;
            aura.transform.position = body.TransformPoint(new Vector3(0.2f, 1.9f, 0f));
            aura.enabled = false;

            // 4 chân: trước gần, trước xa, sau gần, sau xa
            var animator = root.gameObject.AddComponent<LionRigAnimator>();
            var so = new SerializedObject(animator);
            SerializedProperty legs = so.FindProperty("legs");
            string[] legOrder = { "leg_nf", "leg_ff", "leg_nh", "leg_fh" };
            legs.arraySize = legOrder.Length;
            for (int i = 0; i < legOrder.Length; i++)
            {
                LegInfo leg = Array.Find(manifest.legs, l => l.part == legOrder[i]);
                PartInfo info = Part(leg.part);
                string name = (leg.front ? "Front " : "Hind ") + (leg.near ? "Near" : "Far");
                SpriteRenderer r = Add(body, $"Leg {name}", info, Vec(info.position));
                if (!leg.near)
                    r.color = new Color(0.6f, 0.56f, 0.58f); // chân phía xa tối hơn
                Transform[] bones = SkinBones(r, body);
                bones[0].SetParent(leg.front ? chest : hips, true);
                SpriteBone[] spriteBones = r.sprite.GetBones();

                Vector2 joint = Joint(leg, 1), ankle = Joint(leg, 2), toe = Joint(leg, 3);
                float pawAngle = Angle(toe - ankle);
                SerializedProperty e = legs.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("upper").objectReferenceValue = bones[0];
                e.FindPropertyRelative("lower").objectReferenceValue = bones[1];
                e.FindPropertyRelative("paw").objectReferenceValue = bones[2];
                e.FindPropertyRelative("renderer").objectReferenceValue = r;
                e.FindPropertyRelative("upperLength").floatValue = bones[1].localPosition.x;
                e.FindPropertyRelative("lowerLength").floatValue = bones[2].localPosition.x;
                e.FindPropertyRelative("pawLength").floatValue = spriteBones[2].length;
                e.FindPropertyRelative("rest").vector2Value = ankle;
                e.FindPropertyRelative("walkCenter").vector2Value = leg.walk.Length >= 2 ? new Vector2(leg.walk[0], leg.walk[1]) : ankle;
                e.FindPropertyRelative("restPawAngle").floatValue = pawAngle;
                e.FindPropertyRelative("pawBend").floatValue = Mathf.DeltaAngle(Angle(ankle - joint), pawAngle);
                e.FindPropertyRelative("front").boolValue = leg.front;
                e.FindPropertyRelative("near").boolValue = leg.near;
                e.FindPropertyRelative("sortingOrder").intValue = r.sortingOrder;
            }

            // Điểm bao (VisualBounds, không tính đuôi): mông, bụng, ngực, đỉnh bờm, bờm sau, đầu (sừng, mũi, cằm)
            var boundPoints = new List<Transform>();
            void BoundPoint(string name, Transform parentBone, float x, float y)
            {
                Transform t = NewChild($"Bound {name}", parentBone);
                t.position = body.TransformPoint(new Vector3(x, y, 0f));
                boundPoints.Add(t);
            }

            float[] b = manifest.bounds;
            if (b.Length >= 16)
            {
                BoundPoint("Rump", hips, b[0], b[1]);
                BoundPoint("Belly", hips, b[2], b[3]);
                BoundPoint("Chest", chest, b[4], b[5]);
                BoundPoint("Mane Top", maneBones[0], b[6], b[7]);
                BoundPoint("Mane Back", maneBones[0], b[8], b[9]);
                BoundPoint("Horn", neck, b[10], b[11]);
                BoundPoint("Nose", neck, b[12], b[13]);
                BoundPoint("Chin", neck, b[14], b[15]);
            }

            // Thông số + tham chiếu cho LionRigAnimator
            so.FindProperty("size").floatValue = height / Mathf.Max(manifest.height, 0.01f);
            so.FindProperty("rigHeight").floatValue = manifest.height;
            so.FindProperty("pose").objectReferenceValue = pose;
            so.FindProperty("hips").objectReferenceValue = hips;
            so.FindProperty("chest").objectReferenceValue = chest;
            so.FindProperty("neck").objectReferenceValue = neck;
            so.FindProperty("headArt").objectReferenceValue = headArt;
            so.FindProperty("roarArt").objectReferenceValue = roarArt;
            so.FindProperty("mouth").objectReferenceValue = mouth;
            so.FindProperty("eyeGlow").objectReferenceValue = eyeGlow;
            so.FindProperty("aura").objectReferenceValue = aura;
            so.FindProperty("hipsAngle").floatValue = hips.localEulerAngles.z;
            so.FindProperty("chestAngle").floatValue = chest.localEulerAngles.z;
            so.FindProperty("hipsRest").vector2Value = hips.localPosition;
            so.FindProperty("chestRest").vector2Value = chest.localPosition;
            so.FindProperty("neckAngle").floatValue = neck.localEulerAngles.z;
            so.FindProperty("raisedFarLegOrder").intValue = SortingBase + 12;
            SetChain(so.FindProperty("mane"), maneBones, false);
            SetChain(so.FindProperty("tail"), tailBones, false);
            so.FindProperty("tuftLength").floatValue = tailSprite[tailSprite.Length - 1].length;
            SerializedProperty flames = so.FindProperty("flames");
            flames.arraySize = flameChains.Count;
            for (int i = 0; i < flameChains.Count; i++)
                SetChain(flames.GetArrayElementAtIndex(i), flameChains[i], flameFront[i]);
            SerializedProperty sweep = so.FindProperty("maneSweep");
            sweep.arraySize = manifest.maneSweep.Length;
            for (int i = 0; i < manifest.maneSweep.Length; i++)
                sweep.GetArrayElementAtIndex(i).floatValue = manifest.maneSweep[i];
            SerializedProperty list = so.FindProperty("renderers");
            list.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            SerializedProperty points = so.FindProperty("boundPoints");
            points.arraySize = boundPoints.Count;
            for (int i = 0; i < boundPoints.Count; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = boundPoints[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            pose.localScale = new Vector3(1f, 1f, 1f) * (height / Mathf.Max(manifest.height, 0.01f));
            return animator;
        }

        /// <summary>
        /// Menu: dựng lại hình sư tử trong scene đang mở (AI, map giữ nguyên), giữ chiều cao cũ, nối lại mọi ô tham chiếu
        /// đang trỏ vào hình cũ. Dùng sau khi chạy lại Tools/art/lion_rig.py hoặc sửa xương trong Skinning Editor.
        /// </summary>
        [MenuItem("Octopus/Dựng lại sư tử (giữ map)", priority = 4)]
        static void Rebuild()
        {
            var oldVisual = Object.FindAnyObjectByType<LionRigAnimator>() as MonoBehaviour;
            if (oldVisual == null)
                oldVisual = Object.FindAnyObjectByType<LionPlaceholderVisual>();
            if (oldVisual == null)
            {
                EditorUtility.DisplayDialog("Octopus", "Scene đang mở không có boss sư tử (Lion Visual).", "OK");
                return;
            }

            Transform parent = oldVisual.transform.parent;
            float height = VisualHeight(oldVisual);
            // Các ô tham chiếu (trên các component khác) đang trỏ vào hình cũ → nối sang hình mới
            var links = new List<(Object owner, string path, bool toGameObject)>();
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.transform.IsChildOf(oldVisual.transform))
                    continue;
                var so = new SerializedObject(behaviour);
                SerializedProperty it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    if (it.objectReferenceValue == oldVisual)
                        links.Add((behaviour, it.propertyPath, false));
                    else if (it.objectReferenceValue == oldVisual.gameObject)
                        links.Add((behaviour, it.propertyPath, true));
                }
            }

            int siblingIndex = oldVisual.transform.GetSiblingIndex();
            Undo.DestroyObjectImmediate(oldVisual.gameObject);
            var visual = (MonoBehaviour)Build(parent, height);
            visual.transform.SetSiblingIndex(siblingIndex);
            Undo.RegisterCreatedObjectUndo(visual.gameObject, "Dựng lại sư tử");
            foreach ((Object owner, string path, bool toGameObject) in links)
            {
                var so = new SerializedObject(owner);
                so.FindProperty(path).objectReferenceValue = toGameObject ? visual.gameObject : visual;
                so.ApplyModifiedProperties();
            }

            Selection.activeGameObject = visual.gameObject;
            EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
            Debug.Log($"[Octopus] Đã dựng lại sư tử (cao {height:0.##}), nối lại {links.Count} tham chiếu. Nhớ Cmd+S để lưu scene.");
        }

        static float VisualHeight(MonoBehaviour visual)
        {
            var so = new SerializedObject(visual);
            SerializedProperty size = so.FindProperty("size"), rigHeight = so.FindProperty("rigHeight");
            if (size != null && rigHeight != null)
                return size.floatValue * rigHeight.floatValue;
            SerializedProperty height = so.FindProperty("height");
            return height != null ? height.floatValue : 7f;
        }

        static ILionVisual BuildPlaceholder(Transform parent, float height)
        {
            var root = new GameObject(VisualName);
            root.transform.SetParent(parent, false);
            var art = new GameObject("Body");
            art.transform.SetParent(root.transform, false);
            art.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtFactory.Sprite("UiWhite", 8, 8, 8f, new Vector2(0.5f, 0.5f), (u, v) => Color.white, 1, false);
            renderer.sortingOrder = SortingBase + 8;
            var visual = root.AddComponent<LionPlaceholderVisual>();
            visual.Setup(height, renderer);
            return visual;
        }

        static void SetChain(SerializedProperty chain, Transform[] bones, bool front)
        {
            SerializedProperty list = chain.FindPropertyRelative("bones");
            SerializedProperty rest = chain.FindPropertyRelative("rest");
            list.arraySize = bones.Length;
            rest.arraySize = bones.Length;
            for (int i = 0; i < bones.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = bones[i];
                rest.GetArrayElementAtIndex(i).floatValue = bones[i].localEulerAngles.z;
            }

            chain.FindPropertyRelative("front").boolValue = front;
        }

        static Vector2 Joint(LegInfo leg, int index) => new Vector2(leg.joints[index * 2], leg.joints[index * 2 + 1]);

        static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        // Vật liệu chung cho mọi lớp sư tử: shader Octopus/Lion Sprite (chớp khi trúng đòn, rực lên khi nổi giận)
        static Material LionMaterial()
        {
            string path = $"{ArtFolder}/Lion.mat";
            Shader shader = Shader.Find("Octopus/Lion Sprite");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        // Quầng sáng mắt, hào quang: cộng sáng (shader hạt dựng sẵn, URP 2D vẽ được vì pass không gắn LightMode)
        static Material GlowMaterial()
        {
            string path = $"{ArtFolder}/LionGlow.mat";
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null)
                shader = Shader.Find("Mobile/Particles/Additive");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // shader nhân đôi: 0.5 = giữ nguyên màu
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
