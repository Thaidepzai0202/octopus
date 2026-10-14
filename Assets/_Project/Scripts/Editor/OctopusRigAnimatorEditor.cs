using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Inspector của OctopusRigAnimator: nút ▶ Chạy thử để xem chuyển động ngay trong cửa sổ Scene (không cần Play),
    /// nút mở Skinning Editor cho từng bộ phận để xem / sửa xương, lưới và trọng số.
    /// </summary>
    [CustomEditor(typeof(OctopusRigAnimator))]
    class OctopusRigAnimatorEditor : Editor
    {
        static OctopusRigAnimator s_running;
        static double s_lastTime;

        static readonly (string label, string path)[] Parts =
        {
            ("Đầu", "Body"),
            ("Xúc tu trái", "Tentacle L"),
            ("Xúc tu giữa", "Tentacle M"),
            ("Xúc tu phải", "Tentacle R"),
        };

        public override void OnInspectorGUI()
        {
            var rig = (OctopusRigAnimator)target;

            EditorGUILayout.HelpBox(
                "Khung xương: đầu 3 xương (MANTLE_00…02), mỗi xúc tu 14 xương (L_, M_, R_ 00…13), nằm trong Pose → Torso.\n" +
                "• Bật Show Bones: cửa sổ Scene vẽ xương (vàng → đỏ từ gốc ra chóp), chân đang bám đất (chấm xanh lá), đích IK (dấu +).\n" +
                "• Chọn dáng ở Preview rồi bấm ▶ Chạy thử để xem chuyển động mà không cần Play; chỉnh thông số bên dưới là thấy ngay.\n" +
                "• Tắt Drive Bones để tự xoay từng xương bằng tay trong Scene.\n" +
                "• Sửa vị trí xương / trọng số: nút Skinning Editor bên dưới → góc trên trái cửa sổ chọn Skinning Editor → Apply.",
                MessageType.Info);

            if (!Application.isPlaying)
            {
                bool running = s_running == rig;
                GUI.backgroundColor = running ? new Color(1f, 0.6f, 0.5f) : new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button(running ? "■  Dừng chạy thử" : "▶  Chạy thử trong Scene", GUILayout.Height(30)))
                {
                    if (running)
                        Stop();
                    else
                        Run(rig);
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.LabelField("Dáng bò (clip Crawl.anim)", EditorStyles.boldLabel);
            if (rig.UsesCrawlClip)
            {
                EditorGUILayout.HelpBox(
                    "Dáng bò lấy từ clip Crawl.anim. Mở cửa sổ Animation → kéo thanh thời gian để xem; bấm nút ● (Record) rồi " +
                    "kéo tay cầm Foot L / M / R (chấm xanh lá) hoặc Body (chấm xanh dương) trong Scene để đặt keyframe.",
                    MessageType.None);
                GUI.backgroundColor = new Color(1f, 0.9f, 0.5f);
                if (GUILayout.Button("⚡ Bảng chỉnh dáng bò nhanh (từng frame)", GUILayout.Height(28)))
                    OctopusCrawlWindow.Open();
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Mở cửa sổ Animation để chỉnh dáng bò", GUILayout.Height(24)))
                {
                    Selection.activeGameObject = rig.gameObject;
                    EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
                }

                if (GUILayout.Button("Tạo lại clip bò từ thông số \"Bò bằng code\"…") &&
                    EditorUtility.DisplayDialog("Octopus", "Ghi đè clip Crawl.anim bằng dáng bò tính từ mục Gait, Surge, Mantle…?\n" +
                                                           "Mọi keyframe đã sửa tay sẽ mất (Cmd+Z để lấy lại).", "Ghi đè", "Huỷ"))
                    OctopusCrawlClipTools.ResetFromGait(rig);

                if (rig.CrawlCurves != null)
                {
                    string[] unsupported = rig.CrawlCurves.Unsupported(rig.transform);
                    if (unsupported.Length > 0)
                        EditorGUILayout.HelpBox("Các thuộc tính sau trong clip game không dùng (chỉ tay cầm Foot L/M/R và Body " +
                                                "mới có tác dụng; xoay xương trực tiếp sẽ bị code ghi đè):\n" +
                                                string.Join("\n", unsupported), MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Đang bò bằng code (mục Gait). Tick Use Crawl Clip để dùng clip chỉnh tay.", MessageType.None);
            }

            EditorGUILayout.LabelField("Skinning Editor (xem / sửa xương, lưới, trọng số)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach ((string label, string path) in Parts)
                {
                    if (GUILayout.Button(label))
                        OpenSkinning(rig, path);
                }
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        static void Run(OctopusRigAnimator rig)
        {
            Stop();
            s_running = rig;
            s_lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void Stop()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            s_running = null;
        }

        static void OnPlayMode(PlayModeStateChange change) => Stop();

        static void Tick()
        {
            if (s_running == null || Application.isPlaying)
            {
                Stop();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            s_running.TickPreview((float)(now - s_lastTime));
            s_lastTime = now;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        // Chọn ảnh của bộ phận rồi mở Sprite Editor; trong đó đổi sang Skinning Editor (menu góc trên trái)
        static void OpenSkinning(OctopusRigAnimator rig, string child)
        {
            SpriteRenderer renderer = null;
            foreach (SpriteRenderer r in rig.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.name == child)
                    renderer = r;
            }

            if (renderer == null || renderer.sprite == null)
            {
                EditorUtility.DisplayDialog("Octopus", $"Không tìm thấy bộ phận {child}.", "OK");
                return;
            }

            Selection.activeObject = renderer.sprite.texture;
            EditorGUIUtility.PingObject(renderer.sprite.texture);
            if (!EditorApplication.ExecuteMenuItem("Window/2D/Sprite Editor"))
                Debug.Log("[Octopus] Mở Window → 2D → Sprite Editor, rồi chọn Skinning Editor ở góc trên trái.");
        }
    }
}
