using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
                if (asset != null && asset.source == clip)
                    Bake(asset);
            }
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
        public static void ResetFromGait(OctopusRigAnimator rig)
        {
            OctopusCrawlCurves asset = rig.CrawlCurves;
            if (asset == null || asset.source == null)
            {
                Debug.LogWarning("[Octopus] Chưa có clip bò. Chạy Octopus → Dựng lại bạch tuộc (giữ map).");
                return;
            }

            Undo.RegisterCompleteObjectUndo(asset.source, "Tạo lại clip bò");
            WriteClip(asset.source, rig.CrawlCurvesFromGait());
            Bake(asset);
            AssetDatabase.SaveAssets();
            rig.SampleCrawlClip(0f);
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
