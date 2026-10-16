using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Bản sao các đường cong của clip bò (Crawl.anim) để game dùng khi chạy, kể cả trên điện thoại. Editor tự chép lại
    /// mỗi khi clip được sửa trong cửa sổ Animation (OctopusCrawlClipTools), không cần làm gì bằng tay.
    /// </summary>
    [CreateAssetMenu(menuName = "Octopus/Crawl Curves")]
    public class OctopusCrawlCurves : ScriptableObject
    {
        [Serializable]
        public class Channel
        {
            [Tooltip("Đường dẫn tới tay cầm, tính từ Rig Visual (vd Pose/Crawl/Foot R)")]
            public string path;
            [Tooltip("Thuộc tính: m_LocalPosition.x / .y, curl, contact, peak, mantle")]
            public string property;
            public AnimationCurve curve = new AnimationCurve();
        }

        [Tooltip("Clip gốc để chỉnh trong cửa sổ Animation")]
        public AnimationClip source;
        [Tooltip("Độ dài clip (giây) = một vòng bò")]
        public float length = 1f;
        [Tooltip("Clip đã được sửa tay (bảng ⚡ / cửa sổ Animation). Chưa sửa thì game dùng thẳng các ô \"Bò bằng code\"")]
        public bool handEdited;
        public Channel[] channels = Array.Empty<Channel>();

        struct Target
        {
            public Transform Transform;
            public int Axis;                 // 0, 1, 2 = vị trí x, y, z; -1 = trường của tay cầm
            public OctopusCrawlFoot Foot;
            public OctopusCrawlBody Body;
            public string Field;
        }

        [NonSerialized] Target[] _targets;
        [NonSerialized] Transform _root;
        [NonSerialized] Channel[] _resolvedFor;

        /// <summary>Đặt các tay cầm dưới root theo clip tại thời điểm time (giây).</summary>
        public void Sample(Transform root, float time)
        {
            if (_root != root || _resolvedFor != channels || _targets == null)
                Resolve(root);

            for (int i = 0; i < channels.Length; i++)
            {
                Target target = _targets[i];
                if (target.Transform == null || channels[i].curve == null)
                    continue;

                float value = channels[i].curve.Evaluate(time);
                if (target.Axis >= 0)
                {
                    Vector3 position = target.Transform.localPosition;
                    position[target.Axis] = value;
                    target.Transform.localPosition = position;
                }
                else if (target.Foot != null)
                {
                    if (target.Field == "curl")
                        target.Foot.curl = value;
                    else if (target.Field == "contact")
                        target.Foot.contact = value;
                    else if (target.Field == "peak")
                        target.Foot.peak = value;
                }
                else if (target.Body != null && target.Field == "mantle")
                {
                    target.Body.mantle = value;
                }
            }
        }

        /// <summary>Các thuộc tính trong clip mà game không dùng được (chỉ tay cầm chân / thân mới có tác dụng).</summary>
        public string[] Unsupported(Transform root)
        {
            Resolve(root);
            var list = new System.Collections.Generic.List<string>();
            for (int i = 0; i < channels.Length; i++)
            {
                if (_targets[i].Transform == null)
                    list.Add($"{channels[i].path} › {channels[i].property}");
            }

            return list.ToArray();
        }

        void Resolve(Transform root)
        {
            _root = root;
            _resolvedFor = channels;
            _targets = new Target[channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                Transform t = root.Find(channels[i].path);
                if (t == null)
                    continue;

                string property = channels[i].property;
                var target = new Target { Axis = -1, Field = property };
                switch (property)
                {
                    case "m_LocalPosition.x": target.Axis = 0; break;
                    case "m_LocalPosition.y": target.Axis = 1; break;
                    case "m_LocalPosition.z": target.Axis = 2; break;
                    case "curl":
                    case "contact":
                    case "peak":
                        target.Foot = t.GetComponent<OctopusCrawlFoot>();
                        break;
                    case "mantle":
                        target.Body = t.GetComponent<OctopusCrawlBody>();
                        break;
                }

                if (target.Axis >= 0 || target.Foot != null || target.Body != null)
                    target.Transform = t;
                _targets[i] = target;
            }
        }
    }
}
