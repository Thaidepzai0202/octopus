using System;
using UnityEditor;
using UnityEngine;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Bảng chỉnh ảnh vẽ tay: màu, độ trong suốt, vị trí, kích thước của từng ảnh trong Art/Drawn.
    /// Sửa số ở đây là scene Playground cập nhật ngay (khi đang mở scene đó, không Play); Cmd+S để lưu scene.
    /// Mở bằng menu Octopus → Bảng chỉnh ảnh vẽ tay.
    /// </summary>
    public class DrawnArtSettings : ScriptableObject
    {
        public const string AssetPath = DrawnArt.Folder + "/DrawnArtSettings.asset";

        [Serializable]
        public class Layer
        {
            [Tooltip("Nhân màu cho ảnh. Kênh A (Alpha) là độ đậm: kéo xuống để ảnh mờ đi")]
            public Color tint = Color.white;
            [Tooltip("Dịch lên (+) / xuống (-), đơn vị unit")]
            public float offsetY;
            [Tooltip("Dịch chỗ bắt đầu lặp sang phải (+) / trái (-), đơn vị unit")]
            public float offsetX;
            [Tooltip("Phóng to / thu nhỏ so với mặc định")]
            [Range(0.3f, 3f)] public float scale = 1f;
            [Tooltip("Lật ngang ảnh")]
            public bool flipX;
            [Tooltip("Các ô lặp lật xen kẽ như soi gương: chỗ nối luôn liền mạch dù ảnh không vẽ để lặp")]
            public bool mirrorTiles = true;
        }

        [Serializable]
        public class Piece
        {
            [Tooltip("Nhân màu cho ảnh. Kênh A (Alpha) là độ đậm")]
            public Color tint = Color.white;
            [Tooltip("Dịch ảnh so với chỗ mặc định (unit). Collider không đổi, chỉ hình dịch")]
            public Vector2 offset;
            [Tooltip("Phóng to / thu nhỏ so với mặc định")]
            [Range(0.3f, 3f)] public float scale = 1f;
            [Tooltip("Lật ngang ảnh")]
            public bool flipX;
            [Tooltip("Sàn: mặt đứng nằm cách mép trên ảnh bao nhiêu (tỉ lệ chiều cao ảnh). Đá: chỗ bám cách mép dưới bao nhiêu")]
            [Range(0f, 1f)] public float surface;
        }

        [Header("Lớp nền (từ xa tới gần)")]
        public Layer sky = new Layer();
        public Layer deep = new Layer();
        public Layer far = new Layer();
        public Layer mid = new Layer();
        public Layer near = new Layer();
        public Layer front = new Layer();
        public Layer ceiling = new Layer();

        [Header("Sàn, đá, cột")]
        public Piece ledge = new Piece();
        public Piece rock = new Piece();
        public Piece column = new Piece();

        [Header("Art vẽ bằng code")]
        [Tooltip("Hiện cột đá nối tảng đá lên trần (tắt khi đá là đảo lơ lửng)")]
        public bool showColumns = true;
        [Tooltip("Hiện vạch sáng xanh dọc mặt sàn (tắt khi sàn là tranh vẽ đã có mép rõ)")]
        public bool showLedgeRim = true;

        [Header("Tự cập nhật")]
        [Tooltip("Lưu ảnh trong Art/Drawn hoặc sửa số ở bảng này là scene Playground cập nhật ngay")]
        public bool autoApply = true;

        public Layer LayerFor(string file)
        {
            switch (file)
            {
                case "sky": return sky;
                case "deep": return deep;
                case "far": return far;
                case "mid": return mid;
                case "near": return near;
                case "front": return front;
                default: return ceiling;
            }
        }

        public static DrawnArtSettings Load(bool createIfMissing)
        {
            var settings = AssetDatabase.LoadAssetAtPath<DrawnArtSettings>(AssetPath);
            if (settings == null && createIfMissing)
            {
                settings = CreateInstance<DrawnArtSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        void OnValidate()
        {
            // Chỉ asset thật (không phải bản tạm) mới tự áp dụng
            if (autoApply && AssetDatabase.Contains(this))
                DrawnArt.QueueApply();
        }
    }
}
