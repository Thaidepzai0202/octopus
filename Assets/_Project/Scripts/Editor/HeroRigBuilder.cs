using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Octopus.EditorTools.RigArt;
using Object = UnityEngine.Object;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng phần hình nhân vật người (object con "Hero Visual" dưới parent, gốc giữa hai bàn chân sát sàn, quay phải, đỉnh
    /// tóc cao height unit) từ Art/Heroes/&lt;Red|Pink|Blue&gt; (do Tools/art/hero_rig.py cắt từ Docs/nhan_vat_moi):
    /// Hero Visual (HeroRigAnimator) → Pose (lật khi quay đầu, bẹp khi đáp, tỉ lệ theo height) → Body (xoay khi lộn) →
    ///   Torso (SpriteSkin: Hips → Chest + xương vải) · Neck → Head (SpriteSkin: Head + xương tóc; Tử Tinh thêm tóc dài)
    ///   Chest → Arm Upper → Arm Lower → Hand (tay gần: + Weapon, mũi, điểm phóng phép) · Hips → Leg Upper → Leg Lower → Foot
    /// Tay, chân, vũ khí là ảnh cứng xoay quanh khớp; thân, đầu, tóc dài, lửa hồn uốn bằng xương (mở Skinning Editor là thấy).
    /// Thứ tự vẽ: dải SortingBase … SortingBase + 16 (-6 … 10), cùng dải với bạch tuộc / nhện (mỗi lúc chỉ một con): trước
    /// sư tử (-39 … -22) và hiệu ứng sau lưng (-17 … -10), sau hiệu ứng chiêu phía trước (14+). Tóc dài -6, tay xa -5 / -4,
    /// (vũ khí khi xoay người ra sau lưng -3), chân xa -2 / -1, chân gần 0 / 1, thân 2, (vũ khí giơ ra sau đầu 3), đầu 4,
    /// lửa hồn 5, vũ khí 6, quầng vũ khí 7, tay gần 8 / 9, quầng phép 10.
    /// Chưa có Art/Heroes/&lt;tên&gt;/rig.json thì dựng khối màu tạm (HeroPlaceholderVisual).
    /// </summary>
    public static class HeroRigBuilder
    {
        public const string ArtFolder = "Assets/_Project/Art/Heroes";
        public const int SortingBase = -6;
        const string VisualName = "Hero Visual";
        static readonly Color FarTint = new Color(0.72f, 0.7f, 0.8f); // tay / chân phía xa tối hơn, hơi lạnh

        [Serializable]
        class Manifest
        {
            public float pixelsPerUnit = 256f;
            public float height = 2f;
            public PartInfo[] parts = Array.Empty<PartInfo>();
            public PointInfo[] points = Array.Empty<PointInfo>();
        }

        [Serializable]
        class PointInfo
        {
            public string name;
            public float[] at = Array.Empty<float>();
        }

        public static string Folder(PlayableCharacter character)
        {
            switch (character)
            {
                case PlayableCharacter.Red: return $"{ArtFolder}/Red";
                case PlayableCharacter.Pink: return $"{ArtFolder}/Pink";
                case PlayableCharacter.Blue: return $"{ArtFolder}/Blue";
                default: return null;
            }
        }

        public static IHeroVisual Build(Transform parent, PlayableCharacter character, float height)
        {
            string folder = Folder(character);
            string manifestPath = folder != null ? $"{folder}/rig.json" : null;
            if (manifestPath == null || !File.Exists(manifestPath))
                return BuildPlaceholder(parent, character, height);

            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            PartInfo Part(string name) => Array.Find(manifest.parts, p => p.name == name);
            Vector2 P(string name)
            {
                PointInfo p = Array.Find(manifest.points, q => q.name == name);
                return p != null && p.at.Length >= 2 ? new Vector2(p.at[0], p.at[1]) : Vector2.zero;
            }

            bool HasPoint(string name) => Array.Exists(manifest.points, q => q.name == name);
            float ppu = manifest.pixelsPerUnit;
            Material material = HeroMaterial();
            Material glowMaterial = GlowMaterial();
            var renderers = new List<SpriteRenderer>();
            HeroRigAnimator.Style style = character == PlayableCharacter.Pink ? HeroRigAnimator.Style.Staff
                : character == PlayableCharacter.Blue ? HeroRigAnimator.Style.Scythe : HeroRigAnimator.Style.Sword;

            // Ảnh vừa xuất (chưa được Unity nhận) thì import ngay rồi mới đặt kiểu Sprite + xương
            Sprite Load(PartInfo info, float pixelsPerUnit)
            {
                string path = $"{folder}/{info.file}";
                if (AssetImporter.GetAtPath(path) == null)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return Import(folder, info, pixelsPerUnit);
            }

            SpriteRenderer Add(Transform at, string name, PartInfo info, Vector3 localPosition)
            {
                SpriteRenderer r = NewSprite(name, at, Load(info, ppu), SortingBase + info.order);
                r.transform.localPosition = localPosition;
                r.sharedMaterial = material;
                renderers.Add(r);
                return r;
            }

            Transform root = NewChild(VisualName, parent);
            Transform pose = NewChild("Pose", root);
            Transform body = NewChild("Body", pose);
            var rig = new HeroRigAnimator.Rig { pose = pose, body = body };

            // Thân: Hips → Chest + xương vải (vạt áo, khăn, áo choàng)
            PartInfo torsoInfo = Part("torso");
            SpriteRenderer torso = Add(body, "Torso", torsoInfo, Vec(torsoInfo.position));
            Transform[] torsoBones = SkinBones(torso, body);
            Transform hips = torsoBones[0], chest = torsoBones[1];
            rig.hips = hips;
            rig.chest = chest;
            rig.cloth = Sways(torsoBones, 2, 30f, 2.5f, 1.3f);

            // Cổ + đầu (trục cổ trùng Body khi đứng, để góc cúi / ngẩng tính thẳng)
            Transform neck = NewChild("Neck", chest);
            neck.position = body.TransformPoint(P("neck"));
            neck.rotation = body.rotation;
            rig.neck = neck;
            PartInfo headInfo = Part("head");
            SpriteRenderer head = Add(body, "Head", headInfo, Vec(headInfo.position));
            Transform[] headBones = SkinBones(head, body);
            headBones[0].SetParent(neck, true);
            rig.hair = Sways(headBones, 1, 16f, 2f, 1.7f);

            // Tóc dài sau lưng (Tử Tinh): chuỗi từ gáy xuống, theo đầu
            PartInfo longInfo = Part("hair_back");
            if (longInfo != null)
            {
                SpriteRenderer hair = Add(body, "Hair Back", longInfo, Vec(longInfo.position));
                Transform[] bones = SkinBones(hair, body);
                bones[0].SetParent(headBones[0], true);
                rig.longHair = Sways(bones, 0, 14f, 3.5f, 0.7f);
            }

            // Tay: gần (cầm vũ khí, trước thân), xa (sau thân, tối hơn)
            Vector2 grip = P("weapon_grip");
            float weaponAngle = Angle(P("weapon_axis") - grip);
            rig.armNear = BuildLimb("Arm", "n", chest, body, Part, P, Add, weaponAngle, true);
            rig.armFar = BuildLimb("Arm", "f", chest, body, Part, P, Add, 0f, false);
            rig.legNear = BuildLimb("Leg", "n", hips, body, Part, P, Add, 0f, false);
            rig.legFar = BuildLimb("Leg", "f", hips, body, Part, P, Add, 0f, false);
            rig.legNear.rest = P("foot_n");
            rig.legFar.rest = P("foot_f");

            // Vũ khí trong tay gần: gốc ở chỗ cầm, trục +x của bàn tay = trục vũ khí
            Transform hand = rig.armNear.end;
            Transform weapon = NewChild("Weapon", hand);
            PartInfo weaponInfo = Part("weapon");
            SpriteRenderer weaponArt = Add(weapon, "Art", weaponInfo, Vector3.zero);
            weaponArt.transform.localRotation = Quaternion.Euler(0f, 0f, -weaponAngle);
            Vector3 InWeapon(Vector2 p) => Quaternion.Euler(0f, 0f, -weaponAngle) * (Vector3)(p - grip);
            Transform tip = NewChild("Weapon Tip", weapon);
            tip.localPosition = InWeapon(P("weapon_tip"));
            rig.weapon = weapon;
            rig.weaponTip = tip;
            rig.weaponArt = weaponArt;
            if (HasPoint("weapon_off"))
            {
                Transform off = NewChild("Off Grip", weapon); // chỗ tay xa nắm khi cầm 2 tay
                off.localPosition = InWeapon(P("weapon_off"));
                rig.offGrip = off;
            }

            // Vũ khí đổi lớp khi diễn: sau đầu (giơ ra sau đầu), sau thân (xoay người, ở phía xa)
            rig.weaponOrder = SortingBase + weaponInfo.order;
            rig.weaponOrderBehindHead = SortingBase + Part("head").order - 1;
            rig.weaponOrderBehindBody = SortingBase + Part("leg_upper_f").order - 1;

            PartInfo glowInfo = Part("glow");
            Sprite glow = Load(glowInfo, glowInfo.size[0]); // 1 unit tranh ở tỉ lệ 1
            Vector2 glowA = P("weapon_glow_a"), glowB = P("weapon_glow_b");
            SpriteRenderer weaponGlow = NewSprite("Weapon Glow", weapon, glow, rig.weaponOrder + 1);
            weaponGlow.sharedMaterial = glowMaterial;
            weaponGlow.transform.localPosition = InWeapon((glowA + glowB) * 0.5f);
            weaponGlow.transform.localRotation = Quaternion.Euler(0f, 0f, Angle(glowB - glowA) - weaponAngle);
            float glowWidth = style == HeroRigAnimator.Style.Scythe ? 0.75f : style == HeroRigAnimator.Style.Staff ? 0.45f : 0.42f;
            weaponGlow.transform.localScale = new Vector3((glowB - glowA).magnitude * 1.35f + 0.2f, glowWidth, 1f);
            weaponGlow.enabled = false;
            rig.weaponGlow = weaponGlow;
            var weaponRenderers = new List<SpriteRenderer> { weaponArt };

            // Điểm phóng phép: chuôi kiếm (Xích Ảnh), pha lê đầu trượng (Tử Tinh), lửa hồn trên tay xa (Lam Hồn)
            Transform cast;
            PartInfo flameInfo = Part("flame");
            if (flameInfo != null)
            {
                SpriteRenderer flame = Add(body, "Flame", flameInfo, Vec(flameInfo.position));
                Transform[] bones = SkinBones(flame, body);
                bones[0].SetParent(rig.armFar.end, true);
                rig.flame = Sways(bones, 0, 0f, 0f, 1f);
                weaponRenderers.Add(flame);
                cast = NewChild("Cast Point", bones[0]);
                cast.position = body.TransformPoint(HasPoint("flame_center") ? P("flame_center") : P("hand_f"));
            }
            else
            {
                cast = NewChild("Cast Point", weapon);
                cast.localPosition = InWeapon(P("weapon_cast"));
            }

            rig.castPoint = cast;
            if (style != HeroRigAnimator.Style.Sword)
            {
                SpriteRenderer castGlow = NewSprite("Cast Glow", cast, glow, SortingBase + glowInfo.order);
                castGlow.sharedMaterial = glowMaterial;
                rig.castGlow = castGlow;
            }

            rig.weaponRenderers = weaponRenderers.ToArray();
            rig.renderers = renderers.ToArray();
            rig.hipsAngle = hips.localEulerAngles.z;
            rig.chestAngle = chest.localEulerAngles.z;
            rig.neckAngle = neck.localEulerAngles.z;
            rig.hipsRest = hips.localPosition;

            // Điểm bao (VisualBounds, không tính vũ khí): đỉnh tóc, 2 bên đầu, 2 bên thân, gấu áo, khuỷu, bàn tay, gối, đế giày
            var bounds = new List<Transform>();
            void BoundPoint(string name, Transform at)
            {
                Transform t = NewChild($"Bound {name}", at);
                t.position = body.TransformPoint(P(name));
                bounds.Add(t);
            }

            BoundPoint("head_top", headBones[0]);
            BoundPoint("head_l", headBones[0]);
            BoundPoint("head_r", headBones[0]);
            BoundPoint("torso_l", chest);
            BoundPoint("torso_r", chest);
            BoundPoint("torso_b", hips);
            foreach (HeroRigAnimator.Limb limb in new[] { rig.armNear, rig.armFar, rig.legNear, rig.legFar })
            {
                bounds.Add(limb.lower);
                bounds.Add(limb.end);
            }

            rig.boundPoints = bounds.ToArray();

            float scale = height / Mathf.Max(manifest.height, 0.01f);
            var animator = root.gameObject.AddComponent<HeroRigAnimator>();
            animator.Setup(style, rig, scale, manifest.height);
            pose.localScale = new Vector3(scale, scale, 1f);
            EditorUtility.SetDirty(animator);
            return animator;
        }

        // Một tay / chân: đoạn trên (gốc ở vai / háng), đoạn dưới (gốc ở khuỷu / gối), đầu chi (bàn tay / đế giày).
        // Trục +x mỗi đoạn dọc theo đoạn; ảnh con xoay ngược lại cho đúng dáng tranh
        static HeroRigAnimator.Limb BuildLimb(string kind, string side, Transform parent, Transform body,
            Func<string, PartInfo> part, Func<string, Vector2> point, Func<Transform, string, PartInfo, Vector3, SpriteRenderer> add,
            float endAngle, bool weaponHand)
        {
            bool arm = kind == "Arm";
            Vector2 a = point(arm ? $"shoulder_{side}" : $"hip_{side}");
            Vector2 b = point(arm ? $"elbow_{side}" : $"knee_{side}");
            Vector2 c = point(arm ? $"wrist_{side}" : $"shin_{side}");
            Vector2 d = point(arm ? $"hand_{side}" : $"foot_{side}");
            string label = side == "n" ? "Near" : "Far";
            float upperAngle = Angle(b - a), lowerAngle = Angle(d - c);

            // Mỗi đoạn: khớp (trục +x dọc đoạn) → Stretch (kéo dài dọc trục khi tay vươn xa) → ảnh (xoay về dáng tranh)
            Transform upper = NewChild($"{kind} Upper {label}", parent);
            upper.position = body.TransformPoint(a);
            upper.rotation = body.rotation * Quaternion.Euler(0f, 0f, upperAngle);
            Transform upperHolder = NewChild("Stretch", upper);
            SpriteRenderer upperArt = add(upperHolder, "Art", part($"{kind.ToLowerInvariant()}_upper_{side}"), Vector3.zero);
            upperArt.transform.localRotation = Quaternion.Euler(0f, 0f, -upperAngle);

            Transform lower = NewChild($"{kind} Lower {label}", upper);
            lower.localPosition = new Vector3((b - a).magnitude, 0f, 0f);
            lower.rotation = body.rotation * Quaternion.Euler(0f, 0f, lowerAngle);
            Transform lowerHolder = NewChild("Stretch", lower);
            SpriteRenderer lowerArt = add(lowerHolder, "Art", part($"{kind.ToLowerInvariant()}_lower_{side}"), Vector3.zero);
            lowerArt.transform.localRotation = Quaternion.Euler(0f, 0f, -lowerAngle);

            Transform end = NewChild(arm ? $"Hand {label}" : $"Foot {label}", lower);
            end.localPosition = new Vector3((d - c).magnitude, 0f, 0f);
            end.rotation = body.rotation * Quaternion.Euler(0f, 0f, weaponHand ? endAngle : lowerAngle);

            if (side == "f")
            {
                upperArt.color = FarTint;
                lowerArt.color = FarTint;
            }

            return new HeroRigAnimator.Limb
            {
                upper = upper, lower = lower, end = end, upperArt = upperHolder, lowerArt = lowerHolder,
                upperLength = (b - a).magnitude, lowerLength = (d - c).magnitude,
            };
        }

        // Xương phụ lay theo gió từ vị trí first trở đi; xương xa gốc lệch pha
        static HeroRigAnimator.Sway[] Sways(Transform[] bones, int first, float wind, float sway, float phaseStep)
        {
            var list = new List<HeroRigAnimator.Sway>();
            for (int i = first; i < bones.Length; i++)
            {
                list.Add(new HeroRigAnimator.Sway
                {
                    bone = bones[i], rest = Mathf.DeltaAngle(0f, bones[i].localEulerAngles.z), wind = wind, sway = sway,
                    phase = (i - first) * phaseStep,
                });
            }

            return list.ToArray();
        }

        static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        static IHeroVisual BuildPlaceholder(Transform parent, PlayableCharacter character, float height)
        {
            var root = new GameObject(VisualName);
            root.transform.SetParent(parent, false);
            var art = new GameObject("Body");
            art.transform.SetParent(root.transform, false);
            art.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtFactory.Sprite("UiWhite", 8, 8, 8f, new Vector2(0.5f, 0.5f), (u, v) => Color.white, 1, false);
            renderer.sortingOrder = SortingBase + 7;
            Color tint = character switch
            {
                PlayableCharacter.Red => new Color(0.85f, 0.15f, 0.15f),
                PlayableCharacter.Pink => new Color(0.65f, 0.3f, 0.95f),
                PlayableCharacter.Blue => new Color(0.2f, 0.6f, 1f),
                _ => Color.white,
            };
            var visual = root.AddComponent<HeroPlaceholderVisual>();
            visual.Setup(height, renderer, tint);
            return visual;
        }

        /// <summary>
        /// Menu: dựng lại mọi hình nhân vật người trong scene đang mở (giữ chỗ, chiều cao), nối lại các ô tham chiếu đang trỏ
        /// vào hình cũ. Dùng sau khi chạy lại Tools/art/hero_rig.py hoặc sửa xương trong Skinning Editor.
        /// </summary>
        [MenuItem("Octopus/Dựng lại nhân vật người (giữ map)", priority = 5)]
        static void Rebuild()
        {
            HeroRigAnimator[] heroes = Object.FindObjectsByType<HeroRigAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (heroes.Length == 0)
            {
                EditorUtility.DisplayDialog("Octopus", "Scene đang mở không có nhân vật người (Hero Visual dùng HeroRigAnimator).", "OK");
                return;
            }

            int linked = 0;
            foreach (HeroRigAnimator old in heroes)
            {
                Transform parent = old.transform.parent;
                PlayableCharacter character = old.WeaponStyle == HeroRigAnimator.Style.Staff ? PlayableCharacter.Pink
                    : old.WeaponStyle == HeroRigAnimator.Style.Scythe ? PlayableCharacter.Blue : PlayableCharacter.Red;
                var so = new SerializedObject(old);
                float height = so.FindProperty("size").floatValue * so.FindProperty("rigHeight").floatValue;
                bool active = old.gameObject.activeSelf;
                var links = new List<(Object owner, string path, bool toGameObject)>();
                foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (behaviour == null || behaviour.transform.IsChildOf(old.transform))
                        continue;
                    var other = new SerializedObject(behaviour);
                    SerializedProperty it = other.GetIterator();
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference)
                            continue;
                        if (it.objectReferenceValue == old)
                            links.Add((behaviour, it.propertyPath, false));
                        else if (it.objectReferenceValue == old.gameObject)
                            links.Add((behaviour, it.propertyPath, true));
                    }
                }

                int sibling = old.transform.GetSiblingIndex();
                Undo.DestroyObjectImmediate(old.gameObject);
                var visual = (MonoBehaviour)Build(parent, character, height);
                visual.transform.SetSiblingIndex(sibling);
                visual.gameObject.SetActive(active);
                Undo.RegisterCreatedObjectUndo(visual.gameObject, "Dựng lại nhân vật người");
                foreach ((Object owner, string path, bool toGameObject) in links)
                {
                    var other = new SerializedObject(owner);
                    other.FindProperty(path).objectReferenceValue = toGameObject ? visual.gameObject : visual;
                    other.ApplyModifiedProperties();
                    linked++;
                }

                EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
            }

            Debug.Log($"[Octopus] Đã dựng lại {heroes.Length} nhân vật người, nối lại {linked} tham chiếu. Nhớ Cmd+S để lưu scene.");
        }

        // Vật liệu chung: shader Octopus/Hero Sprite (chớp khi trúng đòn, hoá ma, vũ khí rực); mỗi nhân vật tự tạo bản riêng
        static Material HeroMaterial()
        {
            string path = $"{ArtFolder}/HeroSprite.mat";
            Shader shader = Shader.Find("Octopus/Hero Sprite");
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

        // Quầng sáng vũ khí / pha lê / lửa: cùng shader Octopus/Hero Sprite nhưng cộng sáng (SrcAlpha + One), để cũng
        // không vẽ phần dưới mặt sàn như vũ khí
        static Material GlowMaterial()
        {
            string path = $"{ArtFolder}/HeroGlow.mat";
            Shader shader = Shader.Find("Octopus/Hero Sprite");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
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

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
