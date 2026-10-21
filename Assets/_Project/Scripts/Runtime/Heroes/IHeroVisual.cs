using UnityEngine;

namespace Octopus
{
    /// <summary>Dáng di chuyển của nhân vật người (Xích Ảnh, Tử Tinh, Lam Hồn). Bộ điều khiển báo mỗi frame, hình tự diễn.</summary>
    public enum HeroMove
    {
        Idle,
        Run,
        Jump,        // đang bay lên sau cú nhảy thường
        DoubleJump,  // cú nhảy thứ 2 (Xích Ảnh xoay kiếm, Lam Hồn bước hồn lướt ngang)
        Fall,
        Glide,       // Tử Tinh giữ nút nhảy: lơ lửng / lượn chậm
        Land,        // vừa đáp đất (nhún)
        Dash,        // đang lướt nhanh (chiêu lướt, bước hồn)
        Hurt,        // bị đánh văng
    }

    /// <summary>
    /// Phần hình của nhân vật người (hoạt ảnh gắn xương). Bộ điều khiển (HeroMotor) và các chiêu chỉ nói chuyện với hình
    /// qua giao diện này. Hình là object con "Hero Visual" do HeroRigBuilder.Build dựng; gốc hình đặt giữa hai bàn chân,
    /// sát mặt sàn; mặc định quay sang phải.
    ///
    /// Act(CombatPose, duration, point) — dáng vung vũ khí (point = điểm nhắm world, cho biết hướng chém / bắn):
    ///   Whip = chém ngang tới trước (point cao thì chém hất lên) · WhipAlt = chém ngược tay · Slam = bổ từ trên xuống ·
    ///   Spin = xoay một vòng vung vũ khí · Reach = đâm / ném / phóng phép về phía point · Rear = giơ vũ khí lên cao
    ///   (vận sức, niệm phép, chiêu cuối) · Lunge = lao tới chém · Squash = khom người (lấy đà, đáp đất mạnh, thế thủ).
    /// </summary>
    public interface IHeroVisual : ICombatAnimator
    {
        /// <summary>1 = quay sang phải, -1 = quay sang trái. Gán vào thì hình quay đầu mượt (không lật vụt, không bóp dẹt).</summary>
        float Facing { get; set; }

        /// <summary>Gọi mỗi frame: dáng di chuyển hiện tại và vận tốc thân (unit/giây) để chọn nhịp chạy, nghiêng người.</summary>
        void SetMove(HeroMove move, Vector2 velocity);

        /// <summary>Chớp sáng khi trúng đòn (0..1).</summary>
        void Flash(float strength);

        /// <summary>0 = bình thường, 1 = trong mờ như bóng ma (Lam Hồn hoá ma, nhấp nháy khi bất tử).</summary>
        void SetGhost(float amount);

        /// <summary>0..1: vũ khí rực lên (Xích Ảnh "Ma kiếm thức tỉnh": kiếm to ra, đỏ rực; con khác: vũ khí phát sáng).</summary>
        void SetWeaponPower(float amount);

        /// <summary>Mũi vũ khí (world): mũi kiếm / viên pha lê đầu trượng / lưỡi hái.</summary>
        Vector2 WeaponTip { get; }

        /// <summary>Điểm phóng phép (world): viên pha lê trượng của Tử Tinh, ngọn lửa xanh trên tay trái Lam Hồn, chuôi kiếm Xích Ảnh.</summary>
        Vector2 CastPoint { get; }

        /// <summary>Khung bao hình (world).</summary>
        Bounds VisualBounds { get; }
    }
}
