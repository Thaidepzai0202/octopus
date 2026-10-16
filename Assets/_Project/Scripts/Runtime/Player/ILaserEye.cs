using UnityEngine;

namespace Octopus
{
    /// <summary>Hình nhân vật có mắt để chiêu laze bắn ra (bạch tuộc, nhện).</summary>
    public interface ILaserEye
    {
        /// <summary>Tâm mắt (world): chỗ tia laze bắn ra.</summary>
        Vector2 EyePosition { get; }
    }
}
