using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Tay cầm một xúc tu trong clip bò (Crawl.anim). Kiểu sâu đo (xúc tu trái / phải): tay cầm = chóp xúc tu, đoạn giữa
    /// tự uốn thành vòm (kéo chóp lại gần gốc là vòm cao lên). Kiểu khác / xúc tu giữa: tay cầm = chỗ chạm đất, IK uốn theo.
    /// </summary>
    public class OctopusCrawlFoot : MonoBehaviour
    {
        [Tooltip("Chóp cuộn bao nhiêu: 1 = như tranh, nhỏ hơn = duỗi thẳng ra, lớn hơn = cuộn chặt")]
        public float curl = 1f;

        [Tooltip("1 = chóp đang bám đất (đứng yên trên đất khi thân trôi qua), 0 = đang nhấc lên đưa tới")]
        [Range(0f, 1f)] public float contact = 1f;

        [Tooltip("Kiểu sâu đo: đỉnh vòm nằm ở đâu giữa gốc và chóp (0.5 = giữa, nhỏ = gần gốc)")]
        [Range(0.1f, 0.9f)] public float peak = 0.5f;

        void OnDrawGizmos()
        {
            float size = 0.035f * transform.lossyScale.y;
            Gizmos.color = contact >= 0.5f ? new Color(0.2f, 1f, 0.35f, 0.9f) : new Color(1f, 0.6f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, size * 2f);
            Gizmos.DrawSphere(transform.position, size * 0.8f);
        }
    }
}
