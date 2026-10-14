namespace Octopus.EditorTools
{
    /// <summary>
    /// Bố cục chung của hang động: vùng camera đi qua và các lớp nền parallax.
    /// CaveBuilder (art vẽ bằng code) và DrawnArt (ảnh vẽ tay) cùng dùng để hai bên luôn khớp nhau.
    /// </summary>
    static class CaveLayout
    {
        // Camera đi qua vùng này trong suốt màn chơi
        public const float CameraMinX = -3f;
        public const float CameraMaxX = 72f;
        public const float CameraMinY = -1f;
        public const float CameraMaxY = 8f;
        public const float CameraMidY = 3f;
        public const float ViewHalfWidth = 24f;

        // Nửa chiều cao khung hình: bình thường (camera size 7) và khi bay nhanh zoom ra xa nhất (size 10)
        public const float NormalViewHalfHeight = 7f;
        public const float MaxViewHalfHeight = 10f;

        // Mốc gameplay để vẽ cho khớp
        public const float FloorY = -1f;
        public const float RockBandBottom = 4f;
        public const float RockBandTop = 6.7f;
        public const float CeilingArtTop = 18f;
        public const float CeilingArtHeight = 10f;
        public const float CeilingColliderBottom = 11f;
        public const float ColumnBottom = 0.55f; // chân cột đá, tính từ tâm tảng đá

        public readonly struct Layer
        {
            public readonly string ObjectName;
            public readonly string File;
            public readonly float FactorX;
            public readonly float FactorY;
            public readonly float Height; // ảnh vẽ tay được co giãn để cao đúng bấy nhiêu unit
            public readonly int SortingOrder;

            public Layer(string objectName, string file, float factorX, float factorY, float height, int sortingOrder)
            {
                ObjectName = objectName;
                File = file;
                FactorX = factorX;
                FactorY = factorY;
                Height = height;
                SortingOrder = sortingOrder;
            }
        }

        public static readonly Layer Deep = new Layer("BG Deep", "deep", 0.92f, 0.92f, 24f, -950);
        public static readonly Layer Far = new Layer("BG Far", "far", 0.8f, 0.8f, 24f, -900);
        public static readonly Layer Mid = new Layer("BG Mid", "mid", 0.55f, 0.55f, 26f, -800);
        public static readonly Layer Near = new Layer("BG Near", "near", 0.3f, 0.3f, 28f, -700);
        public static readonly Layer Front = new Layer("Foreground", "front", -0.25f, 0.6f, 26f, 100);
        public static readonly Layer[] Backgrounds = { Deep, Far, Mid, Near, Front };

        /// <summary>
        /// Lớp trôi theo camera với tỉ lệ factor, nên vật thể đặt (theo toạ độ trong lớp) trong khoảng này
        /// sẽ hiện quanh camera suốt màn chơi. midY là độ cao giữa khung hình khi camera ở độ cao thường gặp.
        /// </summary>
        public static (float min, float max, float midY) Range(Layer layer)
        {
            float min = CameraMinX * (1f - layer.FactorX) - ViewHalfWidth;
            float max = CameraMaxX * (1f - layer.FactorX) + ViewHalfWidth;
            return (min, max, CameraMidY * (1f - layer.FactorY));
        }
    }
}
