using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Nối clip bò (Crawl.anim) với bạch tuộc gắn xương:
    ///  - Cửa sổ Animation đang xem / ghi clip → cho rig cập nhật liên tục để xúc tu bám theo tay cầm khi kéo thanh thời gian.
    ///  - Mỗi lần clip bị sửa (thêm / kéo keyframe, sửa đường cong) → chép đường cong sang OctopusCrawlCurves cho game dùng.
    ///  - Trước khi build → chép lại lần nữa cho chắc.
    ///  - Tạo / tạo lại clip từ dáng bò bằng code (mục Gait).
    /// </summary>
    [InitializeOnLoad]
    static class OctopusCrawlClipTools
    {
        static List<OctopusCrawlCurves> s_assets;
        static bool s_writing; // đang tự ghi clip từ thông số: không tính là sửa tay
        static bool s_saveQueued;

        static OctopusCrawlClipTools()
        {
            OctopusRigAnimator.EditorAnimationModeActive = AnimationMode.InAnimationMode;
            AnimationUtility.onCurveWasModified += OnCurveModified;
            EditorApplication.update += FollowAnimationWindow;
            EditorApplication.projectChanged += () => s_assets = null;
            Undo.undoRedoPerformed += BakeAll; // Cmd+Z trên clip: bản game dùng cũng hoàn tác theo
        }

        static void BakeAll()
        {
            foreach (OctopusCrawlCurves asset in Assets())
            {
                if (asset != null && asset.source != null)
                    Bake(asset);
            }

            if (!Application.isPlaying)
                EditorApplication.QueuePlayerLoopUpdate();
        }

        static void FollowAnimationWindow()
        {
            if (!Application.isPlaying && AnimationMode.InAnimationMode())
                EditorApplication.QueuePlayerLoopUpdate();
        }

        static void OnCurveModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type)
        {
            foreach (OctopusCrawlCurves asset in Assets())
            {
                if (asset == null || asset.source != clip)
                    continue;
                // Unity báo sự kiện này cả lúc nạp / import clip, nên chỉ tính là sửa tay khi đang chỉnh trong cửa sổ
                // Animation (đang Preview / Record hoặc đang chọn cửa sổ đó). Bảng ⚡ tự đánh dấu qua MarkHandEdited
                if (!s_writing && (AnimationMode.InAnimationMode() || AnimationWindowFocused()))
                    MarkHandEdited(asset);

                Bake(asset);
            }
        }

        static bool AnimationWindowFocused() =>
            EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.GetType().Name == "AnimationWindow";

        /// <summary>Clip bò vừa được sửa tay: từ giờ dáng bò lấy theo clip (các ô "Bò bằng code" tạm không có tác dụng).</summary>
        public static void MarkHandEdited(OctopusCrawlCurves asset)
        {
            if (asset == null || asset.handEdited || s_writing)
                return;
            asset.handEdited = true;
            EditorUtility.SetDirty(asset);
            Debug.Log("[Octopus] Clip bò đã được sửa tay: từ giờ dáng bò lấy theo clip. Muốn quay về các ô \"Bò bằng code\": " +
                      "Inspector của Rig Visual → Bỏ phần sửa tay.");
        }

        static IEnumerable<OctopusCrawlCurves> Assets()
        {
            if (s_assets == null)
            {
                s_assets = new List<OctopusCrawlCurves>();
                foreach (string guid in AssetDatabase.FindAssets("t:OctopusCrawlCurves"))
                    s_assets.Add(AssetDatabase.LoadAssetAtPath<OctopusCrawlCurves>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            return s_assets;
        }

        /// <summary>Chép mọi đường cong của clip gốc sang bản game dùng.</summary>
        public static void Bake(OctopusCrawlCurves asset)
        {
            AnimationClip clip = asset.source;
            if (clip == null)
                return;

            var channels = new List<OctopusCrawlCurves.Channel>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                channels.Add(new OctopusCrawlCurves.Channel
                {
                    path = binding.path,
                    property = binding.propertyName,
                    curve = AnimationUtility.GetEditorCurve(clip, binding),
                });
            }

            asset.channels = channels.ToArray();
            asset.length = clip.length > 0.01f ? clip.length : 1f;
            EditorUtility.SetDirty(asset);
        }

        /// <summary>Ghi đè clip bằng dáng bò bằng code hiện tại (mục Gait…) rồi chép sang bản game dùng.</summary>
        public static void ResetFromGait(OctopusRigAnimator rig) => Regenerate(rig, true);

        /// <summary>
        /// Viết lại clip theo các ô "Bò bằng code". undoable: ghi Undo + lưu ngay (bấm nút); false: lưu sau (đang kéo thanh trượt).
        /// Clip coi như chưa sửa tay.
        /// </summary>
        public static void Regenerate(OctopusRigAnimator rig, bool undoable)
        {
            OctopusCrawlCurves asset = rig.CrawlCurves;
            if (asset == null || asset.source == null)
            {
                Debug.LogWarning("[Octopus] Chưa có clip bò. Chạy Octopus → Dựng lại bạch tuộc (giữ map).");
                return;
            }

            if (undoable)
                Undo.RegisterCompleteObjectUndo(new Object[] { asset.source, asset }, "Tạo lại clip bò");
            s_writing = true;
            try
            {
                WriteClip(asset.source, rig.CrawlCurvesFromGait());
            }
            finally
            {
                s_writing = false;
            }

            asset.handEdited = false;
            EditorUtility.SetDirty(asset);
            Bake(asset);
            if (undoable)
                AssetDatabase.SaveAssets();
            else
                QueueSave();
            rig.SampleCrawlClip(0f);
        }

        static void QueueSave()
        {
            if (s_saveQueued)
                return;
            s_saveQueued = true;
            EditorApplication.delayCall += () =>
            {
                s_saveQueued = false;
                AssetDatabase.SaveAssets();
            };
        }

        public static void WriteClip(AnimationClip clip, List<OctopusRigAnimator.CrawlCurve> curves)
        {
            clip.ClearCurves();
            clip.frameRate = 60f;
            foreach (OctopusRigAnimator.CrawlCurve c in curves)
            {
                AnimationCurve curve = c.Curve;
                for (int i = 0; i < curve.length; i++)
                {
                    Keyframe key = curve.keys[i];
                    if (float.IsInfinity(key.inTangent) || float.IsInfinity(key.outTangent))
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                        continue;
                    }

                    // Giữ nguyên tiếp tuyến đã tính (Free); chỗ gấp khúc (bám ↔ nhấc) thì tách hai bên (Broken)
                    AnimationUtility.SetKeyBroken(curve, i, !Mathf.Approximately(key.inTangent, key.outTangent));
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(c.Path, c.Type, c.Property), curve);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        // Trước khi build lên điện thoại: chép lại đường cong mới nhất của mọi clip bò
        class BeforeBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                s_assets = null;
                foreach (OctopusCrawlCurves asset in Assets())
                {
                    if (asset != null)
                        Bake(asset);
                }

                AssetDatabase.SaveAssets();
            }
        }
    }
}
