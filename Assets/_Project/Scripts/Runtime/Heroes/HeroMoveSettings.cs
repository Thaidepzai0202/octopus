using System;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Cú nhảy thứ 2 (bấm nhảy lần nữa trên không) của nhân vật người. Mặc định cả 3 con (RED, PINK, BLUE) đều Plain; hai kiểu
    /// còn lại là tuỳ chọn thử trong Inspector, chưa dùng.
    /// </summary>
    public enum HeroAirJump
    {
        Plain,    // nhảy đúp thường (mặc định)
        Spin,     // tuỳ chọn: nhảy đúp kèm xoay vũ khí, chém trúng ai ở sát bên
        SoulStep, // tuỳ chọn: lướt ngang một đoạn ngắn rồi bật nhẹ lên, để lại vệt sáng
    }

    /// <summary>
    /// Thông số di chuyển của một nhân vật người (RED / PINK / BLUE). Cả 3 con mặc định giống hệt nhau (Default): cần trái
    /// chạy, nút NHẢY nhảy + nhảy đúp; chỉnh riêng từng con trong Inspector của Hero Motor trên người chơi. Lượn (canGlide),
    /// nhảy đúp kiểu Spin / SoulStep là tuỳ chọn, mặc định tắt. Độ cao nhảy tính bằng unit từ mặt sàn tới bàn chân lúc lên cao
    /// nhất (nhân vật cao 2 unit); nhảy + nhảy đúp mặc định lên ~7.3 unit, đủ tới mặt các tảng đá (cao ~6–6.7 unit trên sàn).
    /// </summary>
    [Serializable]
    public class HeroMoveSettings
    {
        [Header("Chạy")]
        [Tooltip("Tốc độ chạy khi đẩy cần hết cỡ (unit/giây)")]
        [Min(0f)] public float runSpeed = 8f;
        [Tooltip("Tăng tốc trên sàn (unit/giây²)")]
        [Min(0f)] public float groundAcceleration = 80f;
        [Tooltip("Thả cần trên sàn: hãm lại (unit/giây²)")]
        [Min(0f)] public float groundDeceleration = 70f;
        [Tooltip("Đẩy cần ngược chiều đang chạy: hãm + tăng tốc ngược lại (unit/giây²)")]
        [Min(0f)] public float turnAcceleration = 130f;
        [Tooltip("Tăng tốc trên không (unit/giây²)")]
        [Min(0f)] public float airAcceleration = 50f;
        [Tooltip("Thả cần trên không: hãm lại (unit/giây²)")]
        [Min(0f)] public float airDeceleration = 16f;
        [Tooltip("Đang ra chiêu: cần điều hướng chỉ đẩy được bấy nhiêu phần tốc độ (0 = đứng im khi ra chiêu)")]
        [Range(0f, 1f)] public float attackMoveScale = 0.25f;

        [Header("Nhảy")]
        [Tooltip("Giữ nút nhảy: bay lên cao bấy nhiêu unit")]
        [Min(0.1f)] public float jumpHeight = 3.7f;
        [Tooltip("Mất bấy nhiêu giây để lên tới đỉnh (nhỏ = nhảy nhanh, gọn; lớn = bồng bềnh)")]
        [Min(0.05f)] public float timeToApex = 0.4f;
        [Tooltip("Đang rơi: trọng lực nhân thêm (lớn hơn 1 = rơi nhanh, chắc tay)")]
        [Min(0.1f)] public float fallGravityScale = 1.5f;
        [Tooltip("Nhả nút nhảy sớm khi đang bay lên: trọng lực nhân thêm (nhả càng sớm nhảy càng thấp)")]
        [Min(1f)] public float jumpCutGravityScale = 3f;
        [Tooltip("Rơi nhanh nhất (unit/giây)")]
        [Min(1f)] public float maxFallSpeed = 22f;
        [Tooltip("Rời mép sàn bấy nhiêu giây vẫn nhảy được như đang đứng (coyote time)")]
        [Min(0f)] public float coyoteTime = 0.1f;
        [Tooltip("Bấm nhảy sớm bấy nhiêu giây trước khi chạm sàn vẫn nhảy ngay khi chạm (jump buffer)")]
        [Min(0f)] public float jumpBuffer = 0.13f;
        [Tooltip("Rơi xuống chạm sàn nhanh hơn mức này (unit/giây) thì diễn dáng đáp (nhún)")]
        [Min(0f)] public float landSpeed = 7f;

        [Header("Nhảy đúp")]
        [Tooltip("Số lần nhảy trên không")]
        [Min(0)] public int airJumps = 1;
        public HeroAirJump airJump = HeroAirJump.Plain;
        [Tooltip("Nhảy đúp (thường / xoay): bay lên thêm bấy nhiêu unit")]
        [Min(0f)] public float airJumpHeight = 3.6f;
        [Tooltip("Dáng nhảy đúp (HeroMove.DoubleJump) kéo dài bấy nhiêu giây")]
        [Min(0f)] public float airJumpPoseTime = 0.35f;

        [Header("Tuỳ chọn (mặc định tắt): xoay vũ khí khi nhảy đúp (Air Jump = Spin)")]
        [Min(0f)] public float spinDamage = 14f;
        [Tooltip("Bán kính vòng chém quanh người (unit)")]
        [Min(0f)] public float spinRadius = 1.6f;
        [Tooltip("Màu vệt chém")]
        public Color spinColor = new Color(1f, 0.2f, 0.18f);

        [Header("Tuỳ chọn (mặc định tắt): lượn khi giữ nút nhảy trên không")]
        [Tooltip("Bật: giữ nút nhảy khi đang rơi thì lơ lửng, lượn chậm")]
        public bool canGlide;
        [Tooltip("Lượn tối đa bấy nhiêu giây, chạm đất là hồi đầy")]
        [Min(0f)] public float glideTime = 2f;
        [Tooltip("Bắt đầu lượn: được nhấc lên với vận tốc này (unit/giây)")]
        [Min(0f)] public float glideLift = 3f;
        [Tooltip("Trọng lực khi lượn (unit/giây²)")]
        [Min(0f)] public float glideGravity = 5f;
        [Tooltip("Lượn thì rơi chậm nhất bấy nhiêu (unit/giây)")]
        [Min(0f)] public float glideFallSpeed = 1.4f;
        [Tooltip("Tốc độ ngang khi lượn (unit/giây)")]
        [Min(0f)] public float glideSpeed = 6f;

        [Header("Tuỳ chọn (mặc định tắt): lướt khi nhảy đúp (Air Jump = SoulStep)")]
        [Tooltip("Lướt ngang bấy nhiêu unit")]
        [Min(0f)] public float stepDistance = 3.6f;
        [Tooltip("Lướt mất bấy nhiêu giây (không rơi trong lúc lướt)")]
        [Min(0.02f)] public float stepTime = 0.18f;
        [Tooltip("Lướt xong bật lên với vận tốc này (unit/giây)")]
        [Min(0f)] public float stepHop = 12f;
        [Tooltip("Màu vệt sáng khi lướt / lượn")]
        public Color stepColor = new Color(0.3f, 0.75f, 1f);

        /// <summary>Trọng lực khi bay lên (unit/giây²), tính từ độ cao và thời gian lên đỉnh.</summary>
        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);

        /// <summary>
        /// Vận tốc bật lên để lên cao đúng height unit với trọng lực khi bay lên (bù phần hụt do vật lý tính theo từng bước
        /// Time.fixedDeltaTime, mỗi cú nhảy hụt ~v·dt/2).
        /// </summary>
        public float LaunchSpeed(float height)
        {
            float half = Gravity * Time.fixedDeltaTime * 0.5f;
            return half + Mathf.Sqrt(half * half + 2f * Gravity * Mathf.Max(0f, height));
        }

        /// <summary>Thông số mặc định, giống nhau cho cả 3 nhân vật người.</summary>
        public static HeroMoveSettings Default() => new HeroMoveSettings();
    }
}
