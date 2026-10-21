using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Inspector của LionRigAnimator: nút ▶ Chạy thử để xem đứng / đi / phi ngay trong Scene, không cần Play; chọn tư thế
    /// rồi bấm Diễn để xem từng đòn (chồm, vả, gầm, vồ…), chớp trúng đòn và nổi giận.
    /// </summary>
    [CustomEditor(typeof(LionRigAnimator))]
    class LionRigAnimatorEditor : Editor
    {
        static LionRigAnimator s_running;
        static double s_lastTime;
        static LionPose s_pose = LionPose.RearUp;
        static float s_duration = 0.6f;
        static bool s_hold;
        static float s_enrage;

        public override void OnInspectorGUI()
        {
            var rig = (LionRigAnimator)target;
            EditorGUILayout.HelpBox(
                "• Chọn Preview = Idle / Walk / Run / Air rồi bấm ▶ Chạy thử để xem chuyển động mà không cần Play.\n" +
                "• Đang chạy thử: chọn tư thế + thời gian rồi bấm Diễn (đòn nối đòn được: RearUp → ClawSwipe → ClawSwipeAlt).\n" +
                "• Kéo các ô bên dưới là thấy ngay. Ưng thì Cmd + S. Sửa ảnh / xương: chạy lại Tools/art/lion_rig.py rồi menu " +
                "Octopus → Dựng lại sư tử (giữ map).",
                MessageType.Info);

            bool running = s_running == rig;
            if (!Application.isPlaying)
            {
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

            if (running || Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                s_pose = (LionPose)EditorGUILayout.EnumPopup(s_pose);
                s_duration = EditorGUILayout.FloatField(s_duration, GUILayout.Width(50));
                s_hold = GUILayout.Toggle(s_hold, "giữ", GUILayout.Width(44));
                if (GUILayout.Button("Diễn", GUILayout.Width(60)))
                    rig.Act(s_pose, s_duration, s_hold);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Chớp trúng đòn"))
                    rig.Flash(1f);
                if (GUILayout.Button("Quay đầu"))
                    rig.Facing = -rig.Facing;
                float enrage = EditorGUILayout.Slider("Nổi giận", s_enrage, 0f, 1f);
                if (!Mathf.Approximately(enrage, s_enrage))
                {
                    s_enrage = enrage;
                    rig.SetEnrage(enrage);
                }

                EditorGUILayout.EndHorizontal();
            }

            DrawDefaultInspector();
        }

        static void Run(LionRigAnimator rig)
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
    }
}
