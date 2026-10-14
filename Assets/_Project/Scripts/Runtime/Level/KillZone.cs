using UnityEngine;

namespace Octopus
{
    /// <summary>Vùng trigger: bạch tuộc chạm vào là respawn.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class KillZone : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            Rigidbody2D body = other.attachedRigidbody;
            if (body != null && body.TryGetComponent(out PlayerRespawn respawn))
                respawn.Respawn();
        }
    }
}
