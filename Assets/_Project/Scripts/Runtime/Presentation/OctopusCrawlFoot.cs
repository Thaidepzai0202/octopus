using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Tay cầm chóp một xúc tu trong clip bò (Crawl.anim). Vị trí tay cầm = chỗ chóp xúc tu chạm tới; IK tự uốn cả
    /// xúc tu theo. Bấm Record trong cửa sổ Animation rồi kéo tay cầm trong Scene để đặt keyframe.
    /// </summary>
    public class OctopusCrawlFoot : MonoBehaviour
    {
        [Tooltip("Chóp cuộn bao nhiêu: 1 = như tranh, nhỏ hơn = duỗi thẳng ra, lớn hơn = cuộn chặt")]
        public float curl = 1f;

        [Tooltip("1 = chóp đang bám đất (đứng yên trên đất khi thân trôi qua), 0 = đang nhấc lên đưa tới")]
        [Range(0f, 1f)] public float contact = 1f;

        void OnDrawGizmos()
        {
            float size = 0.035f * transform.lossyScale.y;
            Gizmos.color = contact >= 0.5f ? new Color(0.2f, 1f, 0.35f, 0.9f) : new Color(1f, 0.6f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, size * 2f);
            Gizmos.DrawSphere(transform.position, size * 0.8f);
        }
    }
}
