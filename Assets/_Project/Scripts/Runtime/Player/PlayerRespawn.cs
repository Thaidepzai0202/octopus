using UnityEngine;

namespace Octopus
{
    /// <summary>Đưa bạch tuộc về điểm xuất phát khi rơi vào vùng chết.</summary>
    [RequireComponent(typeof(OctopusController))]
    public class PlayerRespawn : MonoBehaviour
    {
        OctopusController _controller;
        Vector2 _spawnPoint;

        void Awake()
        {
            _controller = GetComponent<OctopusController>();
            _spawnPoint = transform.position;
        }

        public void Respawn()
        {
            _controller.ResetTo(_spawnPoint);
        }
    }
}
