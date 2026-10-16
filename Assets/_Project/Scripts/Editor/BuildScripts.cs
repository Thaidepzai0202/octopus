using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Build bằng một lệnh, dùng được từ menu lẫn terminal:
    /// Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod Octopus.EditorTools.BuildScripts.BuildIos
    /// Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Octopus.EditorTools.BuildScripts.BuildWeb
    /// </summary>
    public static class BuildScripts
    {
        const string IosPath = "Builds/iOS";
        const string AndroidPath = "Builds/Android/octopus.apk";
        const string WebPath = "Builds/Web";

        [MenuItem("Octopus/Build iOS (Xcode project)", priority = 40)]
        public static void BuildIos()
        {
            // Đã có project Xcode thì Append: build nhanh hơn và giữ cấu hình signing đã chỉnh trong Xcode
            BuildOptions options = Directory.Exists(IosPath) ? BuildOptions.AcceptExternalModificationsToPlayer : BuildOptions.None;
            Build(BuildTarget.iOS, IosPath, options);
        }

        [MenuItem("Octopus/Build Android (APK)", priority = 41)]
        public static void BuildAndroid()
        {
            Build(BuildTarget.Android, AndroidPath, BuildOptions.None);
        }

        /// <summary>
        /// Bản web cho mạng nội bộ: không nén, vì server http thường (python -m http.server) không gửi header nén
        /// và mạng LAN đủ nhanh. Trang dùng template Assets/WebGLTemplates/Octopus (chặn cử chỉ trình duyệt, nhắc xoay ngang).
        /// Chạy thử: python3 -m http.server 8000 --directory Builds/Web rồi mở http://&lt;IP máy&gt;:8000
        /// </summary>
        [MenuItem("Octopus/Build Web (mạng nội bộ)", priority = 42)]
        public static void BuildWeb()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "PROJECT:Octopus";
            Build(BuildTarget.WebGL, WebPath, BuildOptions.None);
        }

        static void Build(BuildTarget target, string path, BuildOptions options)
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = path,
                target = target,
                options = options,
            });

            BuildSummary summary = report.summary;
            Debug.Log($"[Octopus] Build {target}: {summary.result} sau {summary.totalTime.TotalSeconds:0}s → {path}");

            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
