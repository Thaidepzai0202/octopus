using System;
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
    /// Dựng hình con nhện cho người chơi từ Art/Spider/Rig (do Tools/art/spider_rig.py tách từ Docs/spider/Spider_pink.png):
    /// người chơi → "Spider Visual" (SpiderRigAnimator) → Pose → Body → bụng (chỗ nhả tơ), đầu (mắt, nanh), 8 chân; mỗi chân
    /// là một SpriteRenderer + SpriteSkin với 2 xương đùi / cẳng (mở Skinning Editor là thấy, sửa được). Thêm sợi tơ khi đu
    /// và PlayerCharacter để bật bạch tuộc hay nhện theo màn chọn nhân vật.
    /// </summary>
    static class SpiderBuilder
    {
        public const string Folder = "Assets/_Project/Art/Spider/Rig";

        // Thứ tự vẽ cùng tầm với bạch tuộc (mỗi lúc chỉ hiện một con), trước sàn và đá (≈ -100).
        // Thứ tự trong rig.json nhân đôi để còn chỗ chen quầng sáng mắt vào giữa đầu và mắt
        const int SortingBase = -6;
        const string VisualName = "Spider Visual";

        static int Order(PartInfo info) => SortingBase + info.order * 2;

        [Serializable]
        class Manifest
        {
            public float pixelsPerUnit = 256f;
            public PartInfo[] parts = Array.Empty<PartInfo>();
            public LegInfo[] legs = Array.Empty<LegInfo>();
        }

        [Serializable]
        class LegInfo
        {
            public string part;
            public int side;
            public int slot;
            public float[] root = Array.Empty<float>();
            public float[] rest = Array.Empty<float>();
        }

        /// <summary>
        /// Menu: dựng lại riêng hình con nhện của người chơi trong scene đang mở (map giữ nguyên), giữ các thông số đã chỉnh.
        /// Dùng sau khi chạy lại Tools/art/spider_rig.py hoặc sửa xương trong Skinning Editor.
        /// </summary>
        [MenuItem("Octopus/Dựng lại nhện (giữ map)", priority = 3)]
        static void Rebuild()
        {
            var controller = Object.FindAnyObjectByType<OctopusController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Octopus", "Scene đang mở không có người chơi. Mở Assets/_Project/Scenes/Playground.unity trước.", "OK");
                return;
            }

            Transform old = controller.transform.Find(VisualName);
            string rigSettings = null;
            if (old != null)
            {
                var oldRig = old.GetComponent<SpiderRigAnimator>();
                if (oldRig != null)
                    rigSettings = EditorJsonUtility.ToJson(oldRig);
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFactory.Folder + "/Glow.png");
            GameObject visual = Build(controller, glow, rigSettings);
            if (visual == null)
                return;
            Undo.RegisterCreatedObjectUndo(visual, "Dựng lại nhện");
            Selection.activeGameObject = visual;
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log("[Octopus] Đã dựng lại nhện. Nhớ Cmd+S để lưu scene. Muốn thấy nhện trong Scene: chọn người chơi → Player Character → Start With = Spider.");
        }

        /// <summary>
        /// Dựng "Spider Visual" dưới người chơi (đang tắt; PlayerCharacter bật khi chọn nhện). Chưa có Art/Spider/Rig thì
        /// trả về null. rigSettings: JSON thông số của SpiderRigAnimator cũ để giữ lại khi dựng lại.
        /// </summary>
        public static GameObject Build(OctopusController controller, Sprite glow, string rigSettings = null)
        {
            string manifestPath = $"{Folder}/rig.json";
            if (!File.Exists(manifestPath))
                return null;

            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            PartInfo Part(string name) => Array.Find(manifest.parts, p => p.name == name);
            float ppu = manifest.pixelsPerUnit;

            Transform visual = NewChild(VisualName, controller.transform);
            Transform pose = NewChild("Pose", visual);
            Transform body = NewChild("Body", pose);

            // Đầu ngực; mắt, nanh gắn vào đầu
            PartInfo headInfo = Part("head");
            Vector3 headAt = Vec(headInfo.position);
            Transform head = NewChild("Head", body);
            head.localPosition = headAt;
            Sprite(head, "Head Art", headInfo, ppu, Vector3.zero);

            PartInfo eyesInfo = Part("eyes");
            SpriteRenderer eyes = Sprite(head, "Eyes", eyesInfo, ppu, Vec(eyesInfo.position) - headAt);
            SpriteRenderer eyeGlow = NewSprite("Eye Glow", eyes.transform, glow, Order(eyesInfo) - 1); // sau mắt, trước đầu
            eyeGlow.transform.localScale = Vector3.one * 0.42f;

            // Nanh xoay quanh chân nanh (mép trên ảnh)
            string[] fangNames = { "fang_l", "fang_r", "fang_inner_l", "fang_inner_r" };
            var fangs = new Transform[fangNames.Length];
            for (int i = 0; i < fangNames.Length; i++)
            {
                PartInfo info = Part(fangNames[i]);
                float half = info.size[1] / ppu * 0.5f;
                Transform pivot = NewChild($"Fang {i}", head);
                pivot.localPosition = Vec(info.position) - headAt + new Vector3(0f, half * 0.8f, 0f);
                Sprite(pivot, "Art", info, ppu, new Vector3(0f, -half * 0.8f, 0f));
                fangs[i] = pivot;
            }

            // Bụng xoay quanh chỗ nối với đầu; tơ nhả ra ở đầu bụng phía xa đầu
            PartInfo abdomenInfo = Part("abdomen");
            Vector3 joint = Vector3.Lerp(headAt, Vec(abdomenInfo.position), 0.45f);
            Transform abdomen = NewChild("Abdomen", body);
            abdomen.localPosition = joint;
            Sprite(abdomen, "Abdomen Art", abdomenInfo, ppu, Vec(abdomenInfo.position) - joint);
            Transform spinneret = NewChild("Spinneret", abdomen);
            spinneret.localPosition = (Vec(abdomenInfo.position) - joint) * 1.8f;

            var web = NewChild("Web", visual).gameObject.AddComponent<LineRenderer>();
            web.sharedMaterial = ArtFactory.Material("Line", null);
            web.useWorldSpace = true;
            web.positionCount = 2;
            web.widthMultiplier = 0.035f;
            web.numCapVertices = 2;
            web.startColor = new Color(0.95f, 0.95f, 1f, 0.95f);
            web.endColor = new Color(0.85f, 0.8f, 1f, 0.8f);
            web.sortingOrder = SortingBase - 2;
            web.enabled = false;

            // 8 chân: ảnh chân nằm trong Body, xương cũng trong Body (gốc đùi = chỗ gắn trên thân)
            var animator = visual.gameObject.AddComponent<SpiderRigAnimator>();
            if (!string.IsNullOrEmpty(rigSettings))
                EditorJsonUtility.FromJsonOverwrite(rigSettings, animator);
            var so = new SerializedObject(animator);
            SerializedProperty legs = so.FindProperty("legs");
            legs.arraySize = manifest.legs.Length;
            for (int i = 0; i < manifest.legs.Length; i++)
            {
                LegInfo leg = manifest.legs[i];
                PartInfo info = Part(leg.part);
                string name = $"Leg {(leg.side > 0 ? "L" : "R")}{leg.slot + 1}";
                SpriteRenderer renderer = Sprite(body, name, info, ppu, Vec(info.position));
                Transform[] bones = SkinBones(renderer, body);
                SpriteBone[] spriteBones = renderer.sprite.GetBones();

                SerializedProperty element = legs.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("femur").objectReferenceValue = bones[0];
                element.FindPropertyRelative("tibia").objectReferenceValue = bones[1];
                element.FindPropertyRelative("femurLength").floatValue = bones[1].localPosition.x;
                element.FindPropertyRelative("tibiaLength").floatValue = spriteBones[1].length;
                element.FindPropertyRelative("root").vector2Value = bones[0].localPosition;
                element.FindPropertyRelative("rest").vector2Value = Vec(leg.rest);
                element.FindPropertyRelative("side").intValue = leg.side;
                element.FindPropertyRelative("slot").intValue = leg.slot;
            }

            so.FindProperty("controller").objectReferenceValue = controller;
            so.FindProperty("groundOffset").floatValue = controller.GetComponent<CircleCollider2D>().radius;
            so.FindProperty("web").objectReferenceValue = web;
            so.FindProperty("spinneret").objectReferenceValue = spinneret;
            so.FindProperty("pose").objectReferenceValue = pose;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("abdomen").objectReferenceValue = abdomen;
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("eyes").objectReferenceValue = eyes;
            so.FindProperty("eyeGlow").objectReferenceValue = eyeGlow;
            SerializedProperty fangList = so.FindProperty("fangs");
            fangList.arraySize = fangs.Length;
            for (int i = 0; i < fangs.Length; i++)
                fangList.GetArrayElementAtIndex(i).objectReferenceValue = fangs[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Người chơi: bật bạch tuộc hay nhện theo màn chọn nhân vật
            var character = controller.GetComponent<PlayerCharacter>();
            if (character == null)
                character = controller.gameObject.AddComponent<PlayerCharacter>();
            var characterSettings = new SerializedObject(character);
            Transform octopus = controller.transform.Find("Rig Visual");
            characterSettings.FindProperty("octopusVisual").objectReferenceValue = octopus != null ? octopus.gameObject : null;
            characterSettings.FindProperty("spiderVisual").objectReferenceValue = visual.gameObject;
            characterSettings.ApplyModifiedPropertiesWithoutUndo();
            visual.gameObject.SetActive(character.Current == PlayableCharacter.Spider);
            return visual.gameObject;
        }

        static SpriteRenderer Sprite(Transform parent, string name, PartInfo info, float ppu, Vector3 localPosition)
        {
            SpriteRenderer renderer = NewSprite(name, parent, Import(Folder, info, ppu), Order(info));
            renderer.transform.localPosition = localPosition;
            return renderer;
        }
    }
}
