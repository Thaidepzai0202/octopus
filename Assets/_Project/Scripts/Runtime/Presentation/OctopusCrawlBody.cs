using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Tay cầm thân trong clip bò (Crawl.anim): vị trí = thân chồm tới / lùi lại, nhấp nhô lên xuống so với chỗ đứng;
    /// Mantle = đầu ngả ra sau bao nhiêu độ.
    /// </summary>
    public class OctopusCrawlBody : MonoBehaviour
    {
        [Tooltip("Đầu ngả ra sau bao nhiêu độ (âm = cúi về trước)")]
        public float mantle;

        void OnDrawGizmos()
        {
            float size = 0.05f * transform.lossyScale.y;
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size, size, 0f) * 2f);
            Vector3 up = Quaternion.Euler(0f, 0f, mantle) * Vector3.up;
            Gizmos.DrawLine(transform.position, transform.position + up * size * 4f);
        }
    }
}
