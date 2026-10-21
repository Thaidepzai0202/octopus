using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using static Octopus.EditorTools.RigArt;
using Object = UnityEngine.Object;
using PartInfo = Octopus.EditorTools.RigArt.PartInfo;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng bạch tuộc gắn xương từ Art/Octopus/Rig (do Tools/art/octopus_rig.py tách từ octopus.png):
    ///  1. Chỉnh cài đặt import từng ảnh bộ phận; với 3 xúc tu thì ghi luôn xương + lưới tam giác + trọng số vào ảnh
    ///     (giống như tự làm trong Skinning Editor, nên mở Sprite Editor → Skinning Editor là thấy và sửa được).
    ///  2. Tạo cây "Rig Visual": Pose → Torso → Head (thân, mắt, con ngươi) và 3 chuỗi xương; mỗi xúc tu là một
    ///     SpriteRenderer + SpriteSkin bám theo chuỗi xương của nó.
    ///  3. Gắn OctopusRigAnimator và nối tham chiếu.
    /// </summary>
    static class OctopusRigBuilder
    {
        public const string Folder = "Assets/_Project/Art/Octopus/Rig";

        [Serializable]
        class RigManifest
        {
            public float pixelsPerUnit = 100f;
            public float heightUnits = 1.5f;
            public PartInfo[] parts = Array.Empty<PartInfo>();
        }

        /// <summary>
        /// Menu: dựng lại riêng bạch tuộc gắn xương trong scene đang mở (map giữ nguyên), giữ các thông số đã chỉnh
        /// trên OctopusRigAnimator. Dùng sau khi chạy lại Tools/art/octopus_rig.py hoặc thêm / bớt xương trong Skinning Editor.
        /// </summary>
        [MenuItem("Octopus/Dựng lại bạch tuộc (giữ map)", priority = 2)]
        static void Rebuild()
        {
            var controller = Object.FindAnyObjectByType<OctopusController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Octopus", "Scene đang mở không có bạch tuộc. Mở Assets/_Project/Scenes/Playground.unity trước.", "OK");
                return;
            }

            Transform old = controller.transform.Find("Rig Visual");
            string settings = null;
            Sprite glow = null;
            if (old != null)
            {
                var animator = old.GetComponent<OctopusRigAnimator>();
                if (animator != null)
                    settings = EditorJsonUtility.ToJson(animator);
                Transform halo = old.Find("Glow");
                if (halo != null)
                    glow = halo.GetComponent<SpriteRenderer>().sprite;
                int index = old.GetSiblingIndex();
                Undo.DestroyObjectImmediate(old.gameObject);
                GameObject rig = Create(controller.transform, controller, glow, settings);
                if (rig != null)
                {
                    rig.transform.SetSiblingIndex(index);
                    Undo.RegisterCreatedObjectUndo(rig, "Dựng lại bạch tuộc");
                    Selection.activeGameObject = rig;
                    LinkCharacter(controller, rig);
                }
            }
            else
            {
                GameObject rig = Create(controller.transform, controller, null);
                if (rig != null)
                {
                    Undo.RegisterCreatedObjectUndo(rig, "Dựng bạch tuộc");
                    LinkCharacter(controller, rig);
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log("[Octopus] Đã dựng lại bạch tuộc gắn xương. Nhớ Cmd+S để lưu scene.");
        }

        // Màn chọn nhân vật bật / tắt hình bạch tuộc qua PlayerCharacter: nối lại vào hình vừa dựng
        static void LinkCharacter(OctopusController controller, GameObject rig)
        {
            var character = controller.GetComponent<PlayerCharacter>();
            if (character == null)
                return;
            var so = new SerializedObject(character);
            so.FindProperty("octopusVisual").objectReferenceValue = rig;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Dựng "Rig Visual" dưới bạch tuộc. Chưa có Art/Octopus/Rig thì trả về null.
        /// keepSettings: JSON thông số của OctopusRigAnimator cũ để giữ lại khi dựng lại.
        /// </summary>
        public static GameObject Create(Transform octopus, OctopusController controller, Sprite glow, string keepSettings = null)
        {
            string manifestPath = $"{Folder}/rig.json";
            if (!File.Exists(manifestPath))
                return null;

            var manifest = JsonUtility.FromJson<RigManifest>(File.ReadAllText(manifestPath));
            PartInfo Part(string name) => Array.Find(manifest.parts, p => p.name == name);
            PartInfo bodyInfo = Part("body"), eyeInfo = Part("eye"), pupilInfo = Part("pupil");
            PartInfo[] tentacleInfo = { Part("tentacle_l"), Part("tentacle_m"), Part("tentacle_r") };
            if (bodyInfo == null || eyeInfo == null || pupilInfo == null || Array.Exists(tentacleInfo, t => t == null) ||
                bodyInfo.bones.Length == 0)
            {
                Debug.LogWarning("[Octopus] rig.json thiếu bộ phận hoặc thiếu xương đầu (chạy lại Tools/art/octopus_rig.py), bỏ qua bạch tuộc gắn xương");
                return null;
            }

            var root = new GameObject("Rig Visual").transform;
            root.SetParent(octopus, false);

            // Quầng sáng nhạt, gọn: đủ tách thân đen khỏi nền hang tối mà không làm nhoè viền
            SpriteRenderer halo = NewSprite("Glow", root, glow, -5);
            halo.color = new Color(0.6f, 0.4f, 1f, 0.22f);
            halo.transform.localScale = Vector3.one * 3.2f;

            // Pose: xoay, lật, co giãn, phóng to cả con. Toạ độ bên trong Pose = toạ độ trong tranh, gốc ở chân
            Transform pose = NewChild("Pose", root);
            pose.localPosition = new Vector3(0f, -0.5f, 0f);
            Transform torso = NewChild("Torso", pose);

            // Đầu: ảnh thân có 3 xương (MANTLE_00…02) nên mềm, ngả theo quán tính; mắt gắn vào xương giữa
            SpriteRenderer body = NewSprite("Body", pose, Import(bodyInfo, manifest.pixelsPerUnit), bodyInfo.order);
            body.transform.localPosition = Vec(bodyInfo.position);
            LimbData mantle = Skin(body, torso, bodyInfo);

            Transform eyeBone = mantle.Bones[Mathf.Clamp(bodyInfo.eyeBone, 0, mantle.Bones.Length - 1)];
            SpriteRenderer eye = NewSprite("Eye", eyeBone, Import(eyeInfo, manifest.pixelsPerUnit), eyeInfo.order);
            eye.transform.position = pose.TransformPoint(Vec(eyeInfo.position));
            eye.transform.rotation = pose.rotation;

            // Con ngươi chỉ hiện trong tròng mắt (liếc sang bên hay chớp mắt đều không lòi ra ngoài)
            var mask = eye.gameObject.AddComponent<SpriteMask>();
            mask.sprite = eye.sprite;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = eyeInfo.order;
            mask.frontSortingOrder = pupilInfo.order;
            SpriteRenderer pupil = NewSprite("Pupil", eye.transform, Import(pupilInfo, manifest.pixelsPerUnit), pupilInfo.order);
            pupil.transform.localPosition = Vec(pupilInfo.position) - Vec(eyeInfo.position);
            pupil.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            string[] names = { "Tentacle L", "Tentacle M", "Tentacle R" };
            float[] sides = { 1f, 0f, -1f };
            var limbs = new LimbData[3];
            for (int i = 0; i < 3; i++)
            {
                PartInfo info = tentacleInfo[i];
                Sprite sprite = Import(info, manifest.pixelsPerUnit);
                SpriteRenderer renderer = NewSprite(names[i], pose, sprite, info.order);
                renderer.transform.localPosition = Vec(info.position);
                limbs[i] = Skin(renderer, torso, info);
            }

            // Tay cầm của clip bò: kéo trong Scene (khi Record ở cửa sổ Animation) để đặt chỗ chóp xúc tu / thân
            Transform crawl = NewChild("Crawl", pose);
            var feet = new OctopusCrawlFoot[3];
            string[] footNames = { "Foot L", "Foot M", "Foot R" };
            for (int i = 0; i < 3; i++)
            {
                feet[i] = NewChild(footNames[i], crawl).gameObject.AddComponent<OctopusCrawlFoot>();
                SetIcon(feet[i].gameObject, "sv_icon_dot3_pix16_gizmo");
            }

            var crawlBody = NewChild("Body", crawl).gameObject.AddComponent<OctopusCrawlBody>();
            SetIcon(crawlBody.gameObject, "sv_icon_dot1_pix16_gizmo");
            (AnimationClip clip, OctopusCrawlCurves curves, bool freshClip) = CrawlAssets(root.gameObject);

            // Vệt tốc độ: nhả từ tâm thân, nằm sau thân và xúc tu. Mặt cắt mềm (đậm giữa, mờ dần ra hai mép), đầu vệt hiện dần
            // (khuất sau đầu nên không lộ mép cắt), đuôi nhỏ lại và tan. OctopusRigAnimator làm mờ cả vệt theo tốc độ / hướng bay
            var trail = new GameObject("Speed Trail").AddComponent<TrailRenderer>();
            trail.transform.SetParent(root, false);
            trail.sharedMaterial = ArtFactory.Material("SpeedTrail", SpeedTrailTexture());
            trail.textureMode = LineTextureMode.Stretch; // ảnh chỉ đổi theo bề ngang (V), U kéo dài cả vệt
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 0;
            trail.time = 0.2f;
            trail.minVertexDistance = 0.04f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f, 0f, -0.6f), new Keyframe(0.35f, 0.6f), new Keyframe(1f, 0f, -0.9f, 0f));
            var fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.88f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.7f, 0.62f, 1f), 0.4f),
                    new GradientColorKey(new Color(0.55f, 0.42f, 1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.65f, 0.14f), new GradientAlphaKey(0.42f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                });
            trail.colorGradient = fade;
            trail.sortingOrder = 4;
            trail.emitting = false;

            var animator = root.gameObject.AddComponent<OctopusRigAnimator>();
            if (!string.IsNullOrEmpty(keepSettings))
                EditorJsonUtility.FromJsonOverwrite(keepSettings, animator); // giữ thông số đã chỉnh, tham chiếu nối lại bên dưới

            var so = new SerializedObject(animator);
            so.FindProperty("controller").objectReferenceValue = controller;
            so.FindProperty("pose").objectReferenceValue = pose;
            so.FindProperty("torso").objectReferenceValue = torso;
            so.FindProperty("eye").objectReferenceValue = eye.transform;
            so.FindProperty("pupil").objectReferenceValue = pupil.transform;
            so.FindProperty("glow").objectReferenceValue = halo;
            so.FindProperty("speedTrail").objectReferenceValue = trail;
            so.FindProperty("bodyCenter").floatValue = manifest.heightUnits * 0.5f;

            WriteLimb(so.FindProperty("mantle"), "Mantle", mantle, 0f);
            SetObjects(so.FindProperty("headFins"), mantle.Extra);
            SetFloats(so.FindProperty("headFinAngles"), mantle.ExtraAngles);
            SerializedProperty array = so.FindProperty("limbs");
            array.arraySize = limbs.Length;
            for (int i = 0; i < limbs.Length; i++)
                WriteLimb(array.GetArrayElementAtIndex(i), names[i], limbs[i], sides[i]);

            so.FindProperty("crawlCurves").objectReferenceValue = curves;
            so.FindProperty("crawlBody").objectReferenceValue = crawlBody;
            SetObjects(so.FindProperty("crawlFeet"), feet);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Clip bò mới tạo thì lấy dáng từ bò bằng code; clip đã có (có thể đã sửa tay) thì giữ nguyên
            if (freshClip || AnimationUtility.GetCurveBindings(clip).Length == 0)
                OctopusCrawlClipTools.WriteClip(clip, animator.CrawlCurvesFromGait());
            OctopusCrawlClipTools.Bake(curves);
            AssetDatabase.SaveAssets();
            animator.SampleCrawlClip(0f);
            return root.gameObject;
        }

        // Mặt cắt ngang của vệt tốc độ (V = bề ngang): mép trong suốt hẳn, mờ dần vào giữa, giữa sáng hơn một chút
        static Texture2D SpeedTrailTexture() => ArtFactory.Texture("SpeedTrail", 4, 64, TextureWrapMode.Clamp, (u, v) =>
        {
            float d = v * 2f - 1f;
            float body = Mathf.Pow(Mathf.Max(0f, 1f - d * d), 2f);
            return new Color(1f, 1f, 1f, body * (0.6f + 0.4f * Mathf.Exp(-d * d / 0.06f)));
        });

        // Clip bò Crawl.anim + Animator (để cửa sổ Animation mở được clip) + bản sao đường cong cho game dùng
        static (AnimationClip clip, OctopusCrawlCurves curves, bool fresh) CrawlAssets(GameObject root)
        {
            string clipPath = $"{Folder}/Crawl.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool fresh = clip == null;
            if (fresh)
            {
                clip = new AnimationClip { frameRate = 60f };
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            string controllerPath = $"{Folder}/Octopus.controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (controller == null)
                controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            var host = root.AddComponent<Animator>();
            host.runtimeAnimatorController = controller;
            host.applyRootMotion = false;

            string curvesPath = $"{Folder}/CrawlCurves.asset";
            var curves = AssetDatabase.LoadAssetAtPath<OctopusCrawlCurves>(curvesPath);
            if (curves == null)
            {
                curves = ScriptableObject.CreateInstance<OctopusCrawlCurves>();
                AssetDatabase.CreateAsset(curves, curvesPath);
            }

            curves.source = clip;
            EditorUtility.SetDirty(curves);
            return (clip, curves, fresh);
        }

        // Biểu tượng chấm màu: hiện trong Scene và bấm vào được để chọn tay cầm
        static void SetIcon(GameObject go, string icon)
        {
            if (EditorGUIUtility.IconContent(icon)?.image is Texture2D texture)
                EditorGUIUtility.SetIconForObject(go, texture);
        }

        static void WriteLimb(SerializedProperty element, string name, LimbData limb, float side)
        {
            element.FindPropertyRelative("name").stringValue = name;
            element.FindPropertyRelative("renderer").objectReferenceValue = limb.Renderer;
            SetObjects(element.FindPropertyRelative("bones"), limb.Bones);
            SetFloats(element.FindPropertyRelative("bindAngles"), limb.Angles);
            SetFloats(element.FindPropertyRelative("bindLengths"), limb.Lengths);
            element.FindPropertyRelative("root").vector2Value = limb.Root;
            element.FindPropertyRelative("footBone").intValue = limb.FootBone;
            element.FindPropertyRelative("footOffset").vector2Value = limb.FootOffset;
            element.FindPropertyRelative("side").floatValue = side;
        }

        struct LimbData
        {
            public SpriteRenderer Renderer;
            public Transform[] Bones;          // chuỗi xương (code điều khiển như một xúc tu)
            public Transform[] Extra;          // xương ngoài chuỗi (vây quanh đầu)
            public float[] ExtraAngles;
            public float[] Angles;
            public float[] Lengths;
            public Vector2 Root;
            public int FootBone;
            public Vector2 FootOffset;
        }

        // Tạo xương theo xương trong ảnh (xương gốc nằm trong Torso, xương con theo cha) và gắn SpriteSkin.
        // chainBones xương đầu là chuỗi do code điều khiển; xương còn lại (vây) trả về riêng trong Extra
        static LimbData Skin(SpriteRenderer renderer, Transform torso, PartInfo info)
        {
            SpriteBone[] spriteBones = renderer.sprite.GetBones();
            Transform[] all = SkinBones(renderer, torso);
            int total = all.Length;
            int n = info.chainBones > 0 ? Mathf.Min(info.chainBones, total) : total;

            var bones = new Transform[n];
            var angles = new float[n];
            var lengths = new float[n];
            for (int b = 0; b < n; b++)
            {
                bones[b] = all[b];
                angles[b] = Mathf.DeltaAngle(0f, spriteBones[b].rotation.eulerAngles.z);
                lengths[b] = b + 1 < n ? spriteBones[b + 1].position.x : spriteBones[b].length;
            }

            var extra = new Transform[total - n];
            var extraAngles = new float[total - n];
            for (int b = n; b < total; b++)
            {
                extra[b - n] = all[b];
                extraAngles[b - n] = Mathf.DeltaAngle(0f, spriteBones[b].rotation.eulerAngles.z);
            }

            if (info.contact.Length < 2)
                return new LimbData
                {
                    Renderer = renderer, Bones = bones, Angles = angles, Lengths = lengths, Root = bones[0].localPosition,
                    Extra = extra, ExtraAngles = extraAngles,
                };

            // Điểm chạm đất: đổi sang hệ toạ độ của xương chứa nó
            int foot = Mathf.Clamp(Mathf.FloorToInt(info.contactJoint), 0, n - 1);
            Vector2 joint = bones[0].localPosition;
            float angle = 0f;
            for (int b = 0; b < foot; b++)
            {
                angle += angles[b];
                joint += new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * lengths[b];
            }

            angle += angles[foot];
            Vector2 offset = Quaternion.Euler(0f, 0f, -angle) * ((Vector2)Vec(info.contact) - joint);

            return new LimbData
            {
                Renderer = renderer, Bones = bones, Angles = angles, Lengths = lengths, Root = bones[0].localPosition,
                FootBone = foot, FootOffset = offset, Extra = extra, ExtraAngles = extraAngles,
            };
        }

        // ---------- Import ảnh bộ phận ----------

        static Sprite Import(PartInfo info, float pixelsPerUnit) => RigArt.Import(Folder, info, pixelsPerUnit);

        // ---------- Tiện ích ----------

        static void SetFloats(SerializedProperty property, float[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        static void SetObjects(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
