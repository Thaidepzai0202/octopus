using System.Collections.Generic;
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

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Đang Play: chỉnh gì ở đây cũng MẤT khi dừng Play. Muốn giữ, bấm nút 📌 bên dưới trước khi dừng.", MessageType.Warning);
                GUI.backgroundColor = s_keep ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.9f, 0.5f);
                if (GUILayout.Button(s_keep ? "✔ Sẽ giữ các thông số này khi dừng Play (bấm để huỷ)" : "📌 Giữ các thông số đã chỉnh khi dừng Play", GUILayout.Height(28)))
                    s_keep = !s_keep;
                GUI.backgroundColor = Color.white;
            }

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

            if (!serializedObject.FindProperty("driveBones").boolValue)
                EditorGUILayout.HelpBox("Drive Bones đang TẮT: code không điều khiển xương nữa (xúc tu đứng im). Tick lại Drive Bones ở dưới.", MessageType.Warning);

            EditorGUILayout.LabelField("Dáng bò", EditorStyles.boldLabel);
            if (!rig.HasCrawlClip)
            {
                EditorGUILayout.HelpBox("Dáng bò lấy thẳng từ các ô \"Bò bằng code\" bên dưới: chỉnh là thấy ngay (Preview = Crawl rồi bấm ▶ Chạy thử).", MessageType.None);
            }
            else if (!rig.CrawlCurves.handEdited)
            {
                EditorGUILayout.HelpBox(
                    "Dáng bò đang lấy thẳng từ các ô \"Bò bằng code\" bên dưới (Crawl Style, Snake Height / Length / Flow, Slither, Idle Crouch…): " +
                    "chỉnh là thấy ngay. Chọn Preview = Crawl (đi) hoặc Idle (đứng) rồi bấm ▶ Chạy thử để xem chuyển động.\n" +
                    "Muốn tự tay chỉnh từng frame: bảng ⚡ bên dưới (sửa tay xong thì clip sẽ điều khiển dáng bò).",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Dáng bò đang lấy từ clip bạn đã SỬA TAY, nên các ô Crawl Style / Slither / Gait trong \"Bò bằng code\" tạm " +
                    "không có tác dụng (các ô Snake Height / Length / Flow, Idle Crouch, Crawl Crouch, Wave…, Fin… vẫn có tác dụng). Sửa tiếp bằng bảng ⚡, " +
                    "hoặc bấm nút đỏ để bỏ phần sửa tay.",
                    MessageType.Warning);
                GUI.backgroundColor = new Color(1f, 0.6f, 0.55f);
                if (GUILayout.Button("Bỏ phần sửa tay, dùng lại các ô \"Bò bằng code\"…") &&
                    EditorUtility.DisplayDialog("Octopus", "Viết lại clip bò theo các ô \"Bò bằng code\"? Phần đã sửa tay sẽ mất (Cmd+Z để lấy lại).", "Viết lại", "Huỷ"))
                    OctopusCrawlClipTools.ResetFromGait(rig);
                GUI.backgroundColor = Color.white;
            }

            if (rig.HasCrawlClip)
            {
                GUI.backgroundColor = new Color(1f, 0.9f, 0.5f);
                if (GUILayout.Button("⚡ Bảng chỉnh dáng bò nhanh (từng frame)", GUILayout.Height(28)))
                    OctopusCrawlWindow.Open();
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Mở cửa sổ Animation để chỉnh dáng bò"))
                {
                    Selection.activeGameObject = rig.gameObject;
                    EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
                }

                string[] unsupported = rig.CrawlCurves.Unsupported(rig.transform);
                if (unsupported.Length > 0)
                    EditorGUILayout.HelpBox("Các thuộc tính sau trong clip game không dùng (chỉ tay cầm Foot L/M/R và Body " +
                                            "mới có tác dụng; xoay xương trực tiếp sẽ bị code ghi đè):\n" +
                                            string.Join("\n", unsupported), MessageType.Warning);
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
            if (DrawDefaultInspector() && !Application.isPlaying)
            {
                // Clip chưa sửa tay: viết lại theo thông số mới để luôn khớp (mở bảng ⚡ là thấy đúng dáng đang có)
                if (rig.HasCrawlClip && !rig.CrawlCurves.handEdited)
                    OctopusCrawlClipTools.Regenerate(rig, false);
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        // ---------- Giữ thông số chỉnh trong lúc Play ----------

        static bool s_keep;
        static List<(string path, SerializedPropertyType type, object value)> s_kept;

        // Không giữ phần tham chiếu / dữ liệu xương (do builder nối), chỉ giữ các con số, công tắc, lựa chọn
        static readonly string[] Skip =
        {
            "m_", "controller", "pose", "torso", "eye", "pupil", "glow", "speedTrail", "mantle", "limbs", "headFins",
            "headFinAngles", "crawlCurves", "crawlFeet", "crawlBody",
        };

        [InitializeOnLoadMethod]
        static void HookPlayMode() => EditorApplication.playModeStateChanged += KeepAcrossPlay;

        static void KeepAcrossPlay(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode && s_keep)
            {
                var rig = Object.FindAnyObjectByType<OctopusRigAnimator>();
                s_kept = rig != null ? Capture(rig) : null;
            }
            else if (change == PlayModeStateChange.EnteredEditMode && s_kept != null)
            {
                var rig = Object.FindAnyObjectByType<OctopusRigAnimator>();
                if (rig != null)
                {
                    Restore(rig, s_kept);
                    if (rig.HasCrawlClip && !rig.CrawlCurves.handEdited)
                        OctopusCrawlClipTools.Regenerate(rig, false);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
                    Debug.Log("[Octopus] Đã giữ các thông số chỉnh trong lúc Play. Bấm Cmd+S để lưu scene.");
                }

                s_kept = null;
                s_keep = false;
            }
        }

        static List<(string, SerializedPropertyType, object)> Capture(OctopusRigAnimator rig)
        {
            var values = new List<(string, SerializedPropertyType, object)>();
            var so = new SerializedObject(rig);
            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.Next(enter))
            {
                enter = true;
                if (System.Array.Exists(Skip, prefix => it.propertyPath == prefix || it.propertyPath.StartsWith(prefix + ".") ||
                                                        (prefix == "m_" && it.propertyPath.StartsWith(prefix))))
                {
                    enter = false;
                    continue;
                }

                switch (it.propertyType)
                {
                    case SerializedPropertyType.Float: values.Add((it.propertyPath, it.propertyType, it.floatValue)); break;
                    case SerializedPropertyType.Integer: values.Add((it.propertyPath, it.propertyType, it.intValue)); break;
                    case SerializedPropertyType.Boolean: values.Add((it.propertyPath, it.propertyType, it.boolValue)); break;
                    case SerializedPropertyType.Enum: values.Add((it.propertyPath, it.propertyType, it.enumValueIndex)); break;
                    case SerializedPropertyType.Vector2: values.Add((it.propertyPath, it.propertyType, it.vector2Value)); break;
                    case SerializedPropertyType.ArraySize: values.Add((it.propertyPath, SerializedPropertyType.Integer, it.intValue)); break;
                }
            }

            return values;
        }

        static void Restore(OctopusRigAnimator rig, List<(string path, SerializedPropertyType type, object value)> values)
        {
            var so = new SerializedObject(rig);
            foreach ((string path, SerializedPropertyType type, object value) in values)
            {
                SerializedProperty p = so.FindProperty(path);
                if (p == null)
                    continue;
                switch (type)
                {
                    case SerializedPropertyType.Float: p.floatValue = (float)value; break;
                    case SerializedPropertyType.Integer: p.intValue = (int)value; break;
                    case SerializedPropertyType.Boolean: p.boolValue = (bool)value; break;
                    case SerializedPropertyType.Enum: p.enumValueIndex = (int)value; break;
                    case SerializedPropertyType.Vector2: p.vector2Value = (Vector2)value; break;
                }
            }

            so.ApplyModifiedProperties(); // có Undo: Cmd+Z để bỏ
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
