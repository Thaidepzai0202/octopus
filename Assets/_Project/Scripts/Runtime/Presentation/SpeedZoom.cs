using Unity.Cinemachine;
using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Bay càng nhanh camera càng lùi ra xa, để người chơi kịp thấy chỗ bám tiếp theo.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class SpeedZoom : MonoBehaviour
    {
        [SerializeField] float baseSize = 6f;
        [SerializeField] float maxSize = 9f;
        [SerializeField] float speedForMaxZoom = 25f;
        [SerializeField] float smoothing = 2f;

        CinemachineCamera _camera;
        Rigidbody2D _target;

        void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
        }

        void Start()
        {
            if (_camera.Follow != null)
                _target = _camera.Follow.GetComponent<Rigidbody2D>();
        }

        void LateUpdate()
        {
            if (_target == null)
                return;

            float t = Mathf.Clamp01(_target.linearVelocity.magnitude / speedForMaxZoom);
            LensSettings lens = _camera.Lens;
            lens.OrthographicSize = Mathf.Lerp(
                lens.OrthographicSize, Mathf.Lerp(baseSize, maxSize, t), smoothing * Time.deltaTime);
            _camera.Lens = lens;
        }
    }
}
