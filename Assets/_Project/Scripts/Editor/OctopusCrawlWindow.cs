using System;
using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Ghi / đọc keyframe của clip bò theo số frame. Dùng chung cho bảng "Chỉnh dáng bò" (và test).
    /// </summary>
    static class OctopusCrawlEditing
    {
        /// <summary>Một thuộc tính có thể chỉnh của clip bò.</summary>
        public struct Channel
        {
            public int Handle;        // 0 trái, 1 giữa, 2 phải, 3 thân
            public string Label;
            public Type Type;
            public string Property;
            public float Min, Max;
            public bool Stepped;      // bậc thang (Contact): giữ nguyên tới keyframe sau
        }

        public const int Body = 3;

        public static readonly Channel[] Channels =
        {
            Foot(0, "X (trước / sau)", typeof(Transform), "m_LocalPosition.x", -1.6f, 1.6f),
            Foot(0, "Y (cao)", typeof(Transform), "m_LocalPosition.y", -0.1f, 0.6f),
            Foot(0, "Cuộn", typeof(OctopusCrawlFoot), "curl", 0f, 2.2f),
            Foot(0, "Bám chặt (khoá chân)", typeof(OctopusCrawlFoot), "contact", 0f, 1f, true),
            Foot(1, "X (trước / sau)", typeof(Transform), "m_LocalPosition.x", -1.6f, 1.6f),
            Foot(1, "Y (cao)", typeof(Transform), "m_LocalPosition.y", -0.1f, 0.6f),
            Foot(1, "Cuộn", typeof(OctopusCrawlFoot), "curl", 0f, 2.2f),
            Foot(1, "Bám chặt (khoá chân)", typeof(OctopusCrawlFoot), "contact", 0f, 1f, true),
            Foot(2, "X (trước / sau)", typeof(Transform), "m_LocalPosition.x", -1.6f, 1.6f),
            Foot(2, "Y (cao)", typeof(Transform), "m_LocalPosition.y", -0.1f, 0.6f),
            Foot(2, "Cuộn", typeof(OctopusCrawlFoot), "curl", 0f, 2.2f),
            Foot(2, "Bám chặt (khoá chân)", typeof(OctopusCrawlFoot), "contact", 0f, 1f, true),
            Foot(Body, "Chồm tới / lùi (X)", typeof(Transform), "m_LocalPosition.x", -0.3f, 0.3f),
            Foot(Body, "Nhấp nhô (Y)", typeof(Transform), "m_LocalPosition.y", -0.3f, 0.3f),
            Foot(Body, "Đầu ngả sau (độ)", typeof(OctopusCrawlBody), "mantle", -30f, 40f),
        };

        static Channel Foot(int handle, string label, Type type, string property, float min, float max, bool stepped = false) =>
            new Channel { Handle = handle, Label = label, Type = type, Property = property, Min = min, Max = max, Stepped = stepped };

        public static AnimationClip Clip(OctopusRigAnimator rig) => rig != null && rig.CrawlCurves != null ? rig.CrawlCurves.source : null;

        public static Transform HandleTransform(OctopusRigAnimator rig, int handle) =>
            handle == Body ? rig.CrawlBody != null ? rig.CrawlBody.transform : null
                : rig.CrawlFoot(handle) != null ? rig.CrawlFoot(handle).transform : null;

        public static EditorCurveBinding Binding(OctopusRigAnimator rig, Channel channel) =>
            EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(HandleTransform(rig, channel.Handle), rig.transform),
                channel.Type, channel.Property);

        /// <summary>Số frame của một vòng (frame cuối = frame 0 của vòng sau).</summary>
        public static int LastFrame(AnimationClip clip) => Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(clip.length, 1f / 60f) * Rate(clip)));

        static float Rate(AnimationClip clip) => clip.frameRate > 0f ? clip.frameRate : 60f;

        public static float Time(AnimationClip clip, int frame) => frame / Rate(clip);

        public static float Value(OctopusRigAnimator rig, AnimationClip clip, Channel channel, int frame)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, Binding(rig, channel));
            return curve != null && curve.length > 0 ? curve.Evaluate(Time(clip, frame)) : 0f;
        }

        public static bool HasKey(OctopusRigAnimator rig, AnimationClip clip, Channel channel, int frame)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, Binding(rig, channel));
            return curve != null && FindKey(curve, Time(clip, frame), Rate(clip)) >= 0;
        }

        static int FindKey(AnimationCurve curve, float time, float rate)
        {
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (Mathf.Abs(keys[i].time - time) < 0.5f / rate)
                    return i;
            }

            return -1;
        }

        /// <summary>Đặt giá trị tại frame (tạo keyframe nếu chưa có). loopSync: frame 0 và frame cuối luôn bằng nhau.</summary>
        public static void SetKey(OctopusRigAnimator rig, Channel channel, int frame, float value, bool loopSync)
        {
            AnimationClip clip = Clip(rig);
            if (clip == null)
                return;

            Undo.RecordObject(clip, "Chỉnh dáng bò");
            EditorCurveBinding binding = Binding(rig, channel);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding) ?? new AnimationCurve();
            int last = LastFrame(clip);
            WriteKey(curve, Time(clip, frame), value, channel.Stepped, Rate(clip));
            if (loopSync && (frame == 0 || frame == last))
                WriteKey(curve, Time(clip, frame == 0 ? last : 0), value, channel.Stepped, Rate(clip));
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        static void WriteKey(AnimationCurve curve, float time, float value, bool stepped, float rate)
        {
            int index = FindKey(curve, time, rate);
            if (index >= 0)
            {
                Keyframe key = curve.keys[index];
                key.value = value;
                curve.MoveKey(index, key);
                return;
            }

            index = curve.AddKey(new Keyframe(time, value));
            AnimationUtility.TangentMode mode = stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.ClampedAuto;
            AnimationUtility.SetKeyLeftTangentMode(curve, index, mode);
            AnimationUtility.SetKeyRightTangentMode(curve, index, mode);
        }

        /// <summary>Xoá keyframe ở frame (trừ frame 0 và frame cuối, giữ để vòng nối liền). handle -1 = mọi tay cầm.</summary>
        public static void RemoveKeys(OctopusRigAnimator rig, int frame, int handle = -1)
        {
            AnimationClip clip = Clip(rig);
            if (clip == null || frame <= 0 || frame >= LastFrame(clip))
                return;

            Undo.RecordObject(clip, "Xoá keyframe");
            foreach (Channel channel in Channels)
            {
                if (handle >= 0 && channel.Handle != handle)
                    continue;
                EditorCurveBinding binding = Binding(rig, channel);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    continue;
                int index = FindKey(curve, Time(clip, frame), Rate(clip));
                if (index < 0)
                    continue;
                curve.RemoveKey(index);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        /// <summary>Đặt keyframe cho mọi thuộc tính (của một tay cầm hoặc tất cả) đúng bằng giá trị đang thấy ở frame.</summary>
        public static void KeyAll(OctopusRigAnimator rig, int frame, int handle = -1)
        {
            AnimationClip clip = Clip(rig);
            if (clip == null)
                return;
            foreach (Channel channel in Channels)
            {
                if (handle < 0 || channel.Handle == handle)
                    SetKey(rig, channel, frame, Value(rig, clip, channel, frame), true);
            }
        }

        public static float[] CopyPose(OctopusRigAnimator rig, int frame)
        {
            AnimationClip clip = Clip(rig);
            var values = new float[Channels.Length];
            for (int i = 0; i < Channels.Length; i++)
                values[i] = Value(rig, clip, Channels[i], frame);
            return values;
        }

        public static void PastePose(OctopusRigAnimator rig, int frame, float[] values)
        {
            for (int i = 0; i < Channels.Length && i < values.Length; i++)
                SetKey(rig, Channels[i], frame, values[i], true);
        }

        public static int ChannelIndex(int handle, string property) =>
            Array.FindIndex(Channels, c => c.Handle == handle && c.Property == property);
    }

    /// <summary>
    /// Bảng "Chỉnh dáng bò": chỉnh nhanh từng frame của clip bò (Crawl.anim) mà không cần bật Record hay chọn tay cầm.
    ///  - Kéo vòng tròn của chóp xúc tu / thân ngay trong Scene, xoay vòng quanh thân để chỉnh đầu ngả → ghi keyframe tại frame đang xem
    ///  - Thanh trượt cho mọi thuộc tính; dải thời gian thấy lúc bám / nhấc của từng xúc tu, bấm để nhảy tới frame
    ///  - Đường đi của chóp cả vòng (mỗi frame một chấm: thưa = nhanh, dày = chậm), xanh = bám, cam = nhấc
    ///  - Tự giữ luật: sửa frame 0 thì frame cuối theo, đang bám thì chóp dính đất
    /// </summary>
    class OctopusCrawlWindow : EditorWindow
    {
        static readonly string[] HandleNames = { "Xúc tu TRÁI (sau, đẩy)", "Xúc tu GIỮA (lăn dưới thân)", "Xúc tu PHẢI (trước, vươn kéo)", "THÂN" };
        static readonly string[] ShortNames = { "Trái", "Giữa", "Phải", "Thân" };
        static readonly Color[] HandleColors =
        {
            new Color(1f, 0.85f, 0.3f), new Color(0.75f, 0.6f, 1f), new Color(0.35f, 0.95f, 1f), new Color(0.4f, 0.7f, 1f),
        };

        static readonly Color GripColor = new Color(0.25f, 0.95f, 0.4f);
        static readonly Color SwingColor = new Color(1f, 0.6f, 0.2f);

        OctopusRigAnimator _rig;
        int _frame;
        bool _playing;
        float _playTime;
        double _lastTime;
        float _playSpeed = 1f;
        bool _loopSync = true;
        bool _groundSnap = true;
        bool _showPaths = true;
        float[] _clipboard;
        Vector2 _scroll;

        [MenuItem("Octopus/Chỉnh dáng bò từng frame", priority = 3)]
        public static void Open()
        {
            var window = GetWindow<OctopusCrawlWindow>("Chỉnh dáng bò");
            window.minSize = new Vector2(360f, 420f);
            window.FindRig();
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += Refresh;
            FindRig();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= Refresh;
            if (_rig != null)
            {
                _rig.SetEditorCrawlPhase(-1f);
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        void FindRig()
        {
            _rig = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<OctopusRigAnimator>() : null;
            if (_rig == null)
                _rig = FindAnyObjectByType<OctopusRigAnimator>();
            SetFrame(_frame);
        }

        AnimationClip Clip => OctopusCrawlEditing.Clip(_rig);

        void SetFrame(int frame)
        {
            AnimationClip clip = Clip;
            if (clip == null)
                return;
            int last = OctopusCrawlEditing.LastFrame(clip);
            _frame = Mathf.Clamp(frame, 0, last);
            _rig.SetEditorCrawlPhase(_frame / (float)last);
            Refresh();
        }

        void Step(int delta)
        {
            AnimationClip clip = Clip;
            if (clip == null)
                return;
            int last = OctopusCrawlEditing.LastFrame(clip);
            int frame = _frame + delta;
            if (frame > last)
                frame = 1;            // frame cuối = frame 0, đi tiếp là frame 1 của vòng sau
            else if (frame < 0)
                frame = last - 1;
            SetFrame(frame);
        }

        void Refresh()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            Repaint();
        }

        void OnEditorUpdate()
        {
            if (!_playing || Clip == null || Application.isPlaying)
                return;
            double now = EditorApplication.timeSinceStartup;
            _playTime += (float)(now - _lastTime) * _playSpeed;
            _lastTime = now;
            int last = OctopusCrawlEditing.LastFrame(Clip);
            int frame = Mathf.FloorToInt(Mathf.Repeat(_playTime, Clip.length) * last / Mathf.Max(Clip.length, 0.01f));
            if (frame != _frame)
                SetFrame(frame);
        }

        // ---------- Bảng ----------

        void OnGUI()
        {
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Đang Play. Dừng Play để chỉnh dáng bò.", MessageType.Info);
                return;
            }

            if (_rig == null || Clip == null)
            {
                EditorGUILayout.HelpBox("Chưa tìm thấy bạch tuộc có clip bò. Mở scene Playground (và chạy Octopus → Dựng lại bạch tuộc nếu cần).", MessageType.Warning);
                if (GUILayout.Button("Tìm lại"))
                    FindRig();
                return;
            }

            if (!_rig.UsesCrawlClip)
            {
                EditorGUILayout.HelpBox("Rig Visual đang tắt Use Crawl Clip (bò bằng code). Tick lại để dùng bảng này.", MessageType.Warning);
                return;
            }

            if (AnimationMode.InAnimationMode())
                EditorGUILayout.HelpBox("Cửa sổ Animation đang bật Preview / Record. Tắt nó đi (bấm nút Preview) để bảng này điều khiển bạch tuộc.", MessageType.Warning);

            HandleKeys();
            AnimationClip clip = Clip;
            int last = OctopusCrawlEditing.LastFrame(clip);

            // Thanh điều khiển frame
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("|◀", GUILayout.Width(32)))
                    SetFrame(0);
                if (GUILayout.Button("◀", GUILayout.Width(32)))
                    Step(-1);
                GUILayout.Label("Frame", GUILayout.Width(42));
                int frame = EditorGUILayout.IntField(_frame, GUILayout.Width(40));
                if (frame != _frame)
                    SetFrame(frame);
                GUILayout.Label($"/ {last}", GUILayout.Width(36));
                if (GUILayout.Button("▶", GUILayout.Width(32)))
                    Step(1);
                if (GUILayout.Button("▶|", GUILayout.Width(32)))
                    SetFrame(last);
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = _playing ? new Color(1f, 0.6f, 0.5f) : new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button(_playing ? "■ Dừng" : "▶ Chạy vòng", GUILayout.Width(90)))
                {
                    _playing = !_playing;
                    _lastTime = EditorApplication.timeSinceStartup;
                    _playTime = _frame / (float)last * clip.length;
                }

                GUI.backgroundColor = Color.white;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Tốc độ chạy vòng", GUILayout.Width(110));
                _playSpeed = GUILayout.HorizontalSlider(_playSpeed, 0.1f, 1f);
                GUILayout.Label($"{_playSpeed:0.0}×", GUILayout.Width(34));
            }

            DrawTimeline(clip, last);
            EditorGUILayout.LabelField("Phím , và . (dấu phẩy, chấm) để lùi / tới 1 frame. Kéo vòng tròn trong Scene để chỉnh nhanh.", EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int handle = 2; handle >= 0; handle--)
                DrawHandleSection(clip, handle);
            DrawHandleSection(clip, OctopusCrawlEditing.Body);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("◆ Key cả tư thế", "Đặt keyframe cho mọi thuộc tính ở frame này, giữ chặt dáng đang thấy")))
                    OctopusCrawlEditing.KeyAll(_rig, _frame);
                if (GUILayout.Button(new GUIContent("✕ Xoá key ở frame này", "Frame 0 và frame cuối luôn được giữ để vòng nối liền")))
                    OctopusCrawlEditing.RemoveKeys(_rig, _frame);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sao chép tư thế"))
                    _clipboard = OctopusCrawlEditing.CopyPose(_rig, _frame);
                using (new EditorGUI.DisabledScope(_clipboard == null))
                {
                    if (GUILayout.Button("Dán tư thế vào frame này"))
                        OctopusCrawlEditing.PastePose(_rig, _frame, _clipboard);
                }
            }

            EditorGUILayout.Space(4);
            _loopSync = EditorGUILayout.ToggleLeft(new GUIContent("Frame 0 và frame cuối luôn giống nhau", "Sửa frame 0 thì frame cuối tự sửa theo (vòng nối liền, không giật)"), _loopSync);
            _groundSnap = EditorGUILayout.ToggleLeft(new GUIContent("Đang bám đất thì chóp dính đất", "Bật Bám đất hoặc kéo chóp đang bám: độ cao tự về mặt đất"), _groundSnap);
            bool paths = EditorGUILayout.ToggleLeft("Hiện đường đi của chóp trong Scene", _showPaths);
            if (paths != _showPaths)
            {
                _showPaths = paths;
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lưu (Cmd+S)", GUILayout.Height(24)))
                    AssetDatabase.SaveAssets();
                if (GUILayout.Button("Mở cửa sổ Animation", GUILayout.Height(24)))
                {
                    Selection.activeGameObject = _rig.gameObject;
                    EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
                }
            }
        }

        void HandleKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;
            if (e.keyCode == KeyCode.Comma)
            {
                Step(-1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Period)
            {
                Step(1);
                e.Use();
            }
        }

        // Dải thời gian: mỗi xúc tu một hàng (xanh = bám, cam = nhấc), ◆ = keyframe, vạch trắng = frame đang xem
        void DrawTimeline(AnimationClip clip, int last)
        {
            const float rowHeight = 16f;
            Rect area = GUILayoutUtility.GetRect(10f, rowHeight * 4f + 16f, GUILayout.ExpandWidth(true));
            const float labelWidth = 42f;
            Rect bars = new Rect(area.x + labelWidth, area.y + 14f, area.width - labelWidth - 4f, rowHeight * 4f);
            EditorGUI.DrawRect(bars, new Color(0.12f, 0.12f, 0.14f));

            for (int f = 0; f <= last; f += 10)
            {
                float x = bars.x + bars.width * f / last;
                EditorGUI.DrawRect(new Rect(x, bars.y, 1f, bars.height), new Color(1f, 1f, 1f, 0.12f));
                GUI.Label(new Rect(x - 10f, area.y - 1f, 30f, 14f), f.ToString(), EditorStyles.miniLabel);
            }

            int[] order = { 2, 1, 0, OctopusCrawlEditing.Body };
            for (int row = 0; row < 4; row++)
            {
                int handle = order[row];
                Rect r = new Rect(bars.x, bars.y + row * rowHeight + 2f, bars.width, rowHeight - 4f);
                GUI.Label(new Rect(area.x, r.y - 2f, labelWidth, rowHeight), ShortNames[handle], EditorStyles.miniBoldLabel);
                if (handle != OctopusCrawlEditing.Body)
                {
                    int contact = OctopusCrawlEditing.ChannelIndex(handle, "contact");
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, OctopusCrawlEditing.Binding(_rig, OctopusCrawlEditing.Channels[contact]));
                    for (int f = 0; f < last; f++)
                    {
                        bool grip = curve != null && curve.Evaluate(OctopusCrawlEditing.Time(clip, f)) >= 0.5f;
                        EditorGUI.DrawRect(new Rect(r.x + r.width * f / last, r.y, r.width / last + 0.5f, r.height),
                            grip ? GripColor * new Color(1f, 1f, 1f, 0.75f) : SwingColor * new Color(1f, 1f, 1f, 0.45f));
                    }
                }
                else
                {
                    EditorGUI.DrawRect(r, new Color(0.4f, 0.7f, 1f, 0.25f));
                }

                // Keyframe của tay cầm này
                foreach (OctopusCrawlEditing.Channel channel in OctopusCrawlEditing.Channels)
                {
                    if (channel.Handle != handle)
                        continue;
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, OctopusCrawlEditing.Binding(_rig, channel));
                    if (curve == null)
                        continue;
                    foreach (Keyframe key in curve.keys)
                    {
                        float x = r.x + r.width * Mathf.Clamp01(key.time / Mathf.Max(clip.length, 0.01f));
                        EditorGUI.DrawRect(new Rect(x - 2f, r.y + r.height * 0.5f - 2f, 4f, 4f), Color.white);
                    }
                }
            }

            float cursor = bars.x + bars.width * _frame / last;
            EditorGUI.DrawRect(new Rect(cursor - 1f, bars.y - 2f, 2f, bars.height + 4f), Color.white);

            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && bars.Contains(e.mousePosition))
            {
                SetFrame(Mathf.RoundToInt((e.mousePosition.x - bars.x) / bars.width * last));
                e.Use();
            }
        }

        void DrawHandleSection(AnimationClip clip, int handle)
        {
            EditorGUILayout.Space(3);
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = HandleColors[handle] } };
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(HandleNames[handle], style);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("◆", "Đặt keyframe cho tay cầm này ở frame này"), GUILayout.Width(26)))
                    OctopusCrawlEditing.KeyAll(_rig, _frame, handle);
                if (GUILayout.Button(new GUIContent("✕", "Xoá keyframe của tay cầm này ở frame này"), GUILayout.Width(26)))
                    OctopusCrawlEditing.RemoveKeys(_rig, _frame, handle);
            }

            for (int i = 0; i < OctopusCrawlEditing.Channels.Length; i++)
            {
                OctopusCrawlEditing.Channel channel = OctopusCrawlEditing.Channels[i];
                if (channel.Handle != handle)
                    continue;

                float value = OctopusCrawlEditing.Value(_rig, clip, channel, _frame);
                bool keyed = OctopusCrawlEditing.HasKey(_rig, clip, channel, _frame);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent(keyed ? "◆" : "◇", keyed ? "Có keyframe ở frame này" : "Chưa có keyframe: giá trị do Unity tính chuyển tiếp"), GUILayout.Width(14));
                    EditorGUI.BeginChangeCheck();
                    float changed;
                    if (channel.Stepped)
                        changed = EditorGUILayout.Toggle(channel.Label, value >= 0.5f) ? 1f : 0f;
                    else
                        changed = EditorGUILayout.Slider(channel.Label, value, channel.Min, channel.Max);
                    if (EditorGUI.EndChangeCheck())
                        Apply(channel, changed);
                }
            }
        }

        void Apply(OctopusCrawlEditing.Channel channel, float value)
        {
            OctopusCrawlEditing.SetKey(_rig, channel, _frame, value, _loopSync);
            if (_groundSnap && channel.Property == "contact" && value >= 0.5f)
                OctopusCrawlEditing.SetKey(_rig, OctopusCrawlEditing.Channels[OctopusCrawlEditing.ChannelIndex(channel.Handle, "m_LocalPosition.y")],
                    _frame, _rig.CrawlGroundY(channel.Handle), _loopSync);
            Refresh();
        }

        // ---------- Scene: đường đi + tay cầm kéo được ----------

        void OnSceneGUI(SceneView view)
        {
            if (Application.isPlaying || _rig == null || Clip == null || !_rig.UsesCrawlClip || _rig.PoseTransform == null)
                return;

            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Comma || e.keyCode == KeyCode.Period))
            {
                Step(e.keyCode == KeyCode.Comma ? -1 : 1);
                e.Use();
            }

            AnimationClip clip = Clip;
            Transform pose = _rig.PoseTransform;
            int last = OctopusCrawlEditing.LastFrame(clip);

            // Mặt đất
            Handles.color = new Color(1f, 1f, 1f, 0.25f);
            Handles.DrawLine(pose.TransformPoint(new Vector3(-1.6f, 0f)), pose.TransformPoint(new Vector3(1.6f, 0f)));

            for (int handle = 0; handle < 3; handle++)
                DrawFoot(clip, pose, last, handle);
            DrawBody(clip, pose);
        }

        void DrawFoot(AnimationClip clip, Transform pose, int last, int handle)
        {
            OctopusCrawlEditing.Channel cx = OctopusCrawlEditing.Channels[OctopusCrawlEditing.ChannelIndex(handle, "m_LocalPosition.x")];
            OctopusCrawlEditing.Channel cy = OctopusCrawlEditing.Channels[OctopusCrawlEditing.ChannelIndex(handle, "m_LocalPosition.y")];
            OctopusCrawlEditing.Channel cc = OctopusCrawlEditing.Channels[OctopusCrawlEditing.ChannelIndex(handle, "contact")];
            AnimationCurve x = AnimationUtility.GetEditorCurve(clip, OctopusCrawlEditing.Binding(_rig, cx));
            AnimationCurve y = AnimationUtility.GetEditorCurve(clip, OctopusCrawlEditing.Binding(_rig, cy));
            AnimationCurve c = AnimationUtility.GetEditorCurve(clip, OctopusCrawlEditing.Binding(_rig, cc));
            if (x == null || y == null)
                return;

            Vector3 Point(int f)
            {
                float t = OctopusCrawlEditing.Time(clip, f);
                return pose.TransformPoint(new Vector3(x.Evaluate(t), y.Evaluate(t), 0f));
            }

            bool Grip(int f) => c != null && c.Evaluate(OctopusCrawlEditing.Time(clip, f)) >= 0.5f;

            if (_showPaths)
            {
                for (int f = 0; f < last; f++)
                {
                    Vector3 a = Point(f), b = Point(f + 1);
                    Handles.color = Grip(f) ? GripColor : SwingColor;
                    Handles.DrawAAPolyLine(3f, a, b);
                    float dot = HandleUtility.GetHandleSize(a) * 0.012f;
                    Handles.DrawSolidDisc(a, Vector3.forward, dot);
                }
            }

            Vector3 position = Point(_frame);
            float size = HandleUtility.GetHandleSize(position) * 0.09f;
            bool grip = Grip(_frame);
            Handles.color = HandleColors[handle];
            Handles.Label(position + Vector3.up * size * 1.8f, $"{ShortNames[handle]}{(grip ? " · bám" : " · trượt")}", EditorStyles.whiteBoldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(position, size, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 local = pose.InverseTransformPoint(moved);
                OctopusCrawlEditing.SetKey(_rig, cx, _frame, local.x, _loopSync);
                float height = grip && _groundSnap ? _rig.CrawlGroundY(handle) : local.y;
                OctopusCrawlEditing.SetKey(_rig, cy, _frame, height, _loopSync);
                Refresh();
            }

            Handles.color = grip ? GripColor : SwingColor;
            Handles.DrawSolidDisc(position, Vector3.forward, size * 0.35f);
        }

        void DrawBody(AnimationClip clip, Transform pose)
        {
            int body = OctopusCrawlEditing.Body;
            OctopusCrawlEditing.Channel cx = OctopusCrawlEditing.Channels[OctopusCrawlEditing.ChannelIndex(body, "m_LocalPosition.x")];
            OctopusCrawlEditing.Channel cy = OctopusCrawlEditing.Channels[OctopusCrawlEditing.ChannelIndex(body, "m_LocalPosition.y")];
            OctopusCrawlEditing.Channel cm = OctopusCrawlEditing.Channels[OctopusCrawlEditing.ChannelIndex(body, "mantle")];
            var offset = new Vector2(OctopusCrawlEditing.Value(_rig, clip, cx, _frame), OctopusCrawlEditing.Value(_rig, clip, cy, _frame));
            float mantle = OctopusCrawlEditing.Value(_rig, clip, cm, _frame);
            var center = new Vector2(0f, _rig.BodyCenter);
            Vector3 position = pose.TransformPoint(offset + center);
            float size = HandleUtility.GetHandleSize(position) * 0.1f;

            Handles.color = HandleColors[body];
            Handles.Label(position + Vector3.right * size * 4.2f, "Thân (kéo) · vòng = đầu ngả", EditorStyles.whiteBoldLabel);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(position, size, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector2 local = (Vector2)pose.InverseTransformPoint(moved) - center;
                OctopusCrawlEditing.SetKey(_rig, cx, _frame, local.x, _loopSync);
                OctopusCrawlEditing.SetKey(_rig, cy, _frame, local.y, _loopSync);
                Refresh();
            }

            // Vòng xoay: đầu ngả sau (dương = ngả về phía sau, tức xoay ngược chiều kim đồng hồ khi quay mặt sang phải)
            EditorGUI.BeginChangeCheck();
            Quaternion rotation = Handles.Disc(pose.rotation * Quaternion.Euler(0f, 0f, mantle), position, Vector3.forward, size * 3.5f, false, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                float angle = Mathf.DeltaAngle(0f, (Quaternion.Inverse(pose.rotation) * rotation).eulerAngles.z);
                OctopusCrawlEditing.SetKey(_rig, cm, _frame, angle, _loopSync);
                Refresh();
            }

            Vector3 up = pose.rotation * Quaternion.Euler(0f, 0f, mantle) * Vector3.up;
            Handles.DrawLine(position, position + up * size * 3.5f);
        }
    }
}
