using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Inspector của HeroRigAnimator: nút ▶ Chạy thử để xem đứng / chạy / nhảy… ngay trong Scene, không cần Play; chọn dáng
    /// đòn + thời gian rồi bấm Diễn (điểm nhắm: phía trước, cao / thấp tuỳ ô Nhắm), chớp trúng đòn, hoá ma, vũ khí rực.
    /// </summary>
    [CustomEditor(typeof(HeroRigAnimator))]
    class HeroRigAnimatorEditor : Editor
    {
        static HeroRigAnimator s_running;
        static double s_lastTime;
        static CombatPose s_pose = CombatPose.Whip;
        static float s_duration = 0.4f;
        static float s_aim;
        static float s_ghost, s_power;

        public override void OnInspectorGUI()
        {
            var rig = (HeroRigAnimator)target;
            EditorGUILayout.HelpBox(
                "• Chọn Preview = Idle / Run / Jump / Fall / Glide / Dash / Hurt rồi bấm ▶ Chạy thử để xem mà không cần Play.\n" +
                "• Đang chạy thử: chọn dáng đòn + thời gian (giây) rồi bấm Diễn; Nhắm = góc điểm nhắm (độ, + = cao).\n" +
                "• Kéo các ô bên dưới là thấy ngay. Ưng thì Cmd + S. Sửa ảnh / xương: chạy lại Tools/art/hero_rig.py rồi menu " +
                "Octopus → Dựng lại nhân vật người (giữ map).",
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
                s_pose = (CombatPose)EditorGUILayout.EnumPopup(s_pose);
                s_duration = EditorGUILayout.FloatField(s_duration, GUILayout.Width(50));
                if (GUILayout.Button("Diễn", GUILayout.Width(60)))
                {
                    float a = s_aim * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(a) * rig.Facing, Mathf.Sin(a));
                    rig.Act(s_pose, s_duration, (Vector2)rig.transform.position + Vector2.up * 1.1f + dir * 2.5f);
                }

                EditorGUILayout.EndHorizontal();
                s_aim = EditorGUILayout.Slider("Nhắm (độ)", s_aim, -60f, 60f);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Chớp trúng đòn"))
                    rig.Flash(1f);
                if (GUILayout.Button("Quay đầu"))
                    rig.Facing = -rig.Facing;
                EditorGUILayout.EndHorizontal();

                float ghost = EditorGUILayout.Slider("Hoá ma", s_ghost, 0f, 1f);
                if (!Mathf.Approximately(ghost, s_ghost))
                {
                    s_ghost = ghost;
                    rig.SetGhost(ghost);
                }

                float power = EditorGUILayout.Slider("Vũ khí rực", s_power, 0f, 1f);
                if (!Mathf.Approximately(power, s_power))
                {
                    s_power = power;
                    rig.SetWeaponPower(power);
                }
            }

            DrawDefaultInspector();
        }

        static void Run(HeroRigAnimator rig)
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
