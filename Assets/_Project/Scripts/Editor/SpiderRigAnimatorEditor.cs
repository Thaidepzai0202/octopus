using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>Inspector của SpiderRigAnimator: nút ▶ Chạy thử để xem đứng / bò ngay trong Scene, không cần Play.</summary>
    [CustomEditor(typeof(SpiderRigAnimator))]
    class SpiderRigAnimatorEditor : Editor
    {
        static SpiderRigAnimator s_running;
        static double s_lastTime;

        public override void OnInspectorGUI()
        {
            var rig = (SpiderRigAnimator)target;
            EditorGUILayout.HelpBox(
                "• Chọn Preview = Idle (đứng) hoặc Walk (bò) rồi bấm ▶ Chạy thử để xem chuyển động mà không cần Play.\n" +
                "• Kéo các ô bên dưới là thấy ngay. Ưng thì Cmd + S.\n" +
                "• Trong game nhện tự đi qua lại quanh chỗ đặt nó (component Spider Walker trên object Spider: quãng đi, tốc độ, thời gian đứng).",
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

            DrawDefaultInspector();
        }

        static void Run(SpiderRigAnimator rig)
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
