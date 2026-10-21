using UnityEngine;

namespace Octopus
{
    /// <summary>Các tư thế boss sư tử diễn khi tung đòn (AI gọi, hình tự diễn theo kiểu riêng).</summary>
    public enum LionPose
    {
        None,
        Crouch,       // thu mình lấy đà (trước khi vồ / húc)
        Leap,         // bay vồ, chân trước vươn ra
        Land,         // đáp đất nặng, thân nhún
        RearUp,       // chồm đứng bằng 2 chân sau (báo trước vả vuốt)
        ClawSwipe,    // vả vuốt nhát 1 (chân trước gần)
        ClawSwipeAlt, // vả vuốt nhát 2 (chân trước kia)
        Roar,         // ngửa đầu há miệng gầm, bờm bùng
        ManeShake,    // lắc bờm phóng cầu lửa
        ChargeWindup, // cào chân, cúi đầu
        Charge,       // cúi đầu lao
        Hurt,         // giật nhẹ khi trúng đòn nặng
        Stagger,      // choáng gục nằm nghiêng
        Die,          // gục chết, lửa tắt dần
    }

    /// <summary>
    /// Phần hình của boss sư tử (hoạt ảnh gắn xương). AI boss (LionBoss) chỉ nói chuyện với hình qua giao diện này.
    /// Hình là object con "Lion Visual" do LionRigBuilder.Build dựng; gốc hình đặt ở giữa hai bàn chân, sát mặt sàn.
    /// </summary>
    public interface ILionVisual
    {
        /// <summary>1 = quay sang phải, -1 = quay sang trái. Gán vào thì hình quay đầu mượt (không lật vụt).</summary>
        float Facing { get; set; }

        /// <summary>Gọi mỗi frame: tốc độ ngang (unit/giây, có dấu) và có đang đứng trên sàn không, để chọn đi / chạy / đứng thở.</summary>
        void SetLocomotion(float velocityX, bool grounded);

        /// <summary>Diễn tư thế pose trong duration giây. hold = giữ tư thế cuối tới khi có Act khác (vd Stagger, Die).</summary>
        void Act(LionPose pose, float duration, bool hold = false);

        /// <summary>Chớp sáng khi trúng đòn (0..1).</summary>
        void Flash(float strength);

        /// <summary>Mức nổi giận 0..1 (pha 3): hào quang, bờm cháy mạnh hơn.</summary>
        void SetEnrage(float amount);

        /// <summary>Điểm miệng (world) — gầm, phun lửa.</summary>
        Vector2 MouthPoint { get; }

        /// <summary>Đầu móng chân trước đang vả (world) — vệt cào.</summary>
        Vector2 PawPoint { get; }

        /// <summary>Khung bao hình (world) — để dựng hitbox thân và đặt thanh máu.</summary>
        Bounds VisualBounds { get; }
    }
}
