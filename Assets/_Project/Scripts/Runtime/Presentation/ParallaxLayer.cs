using UnityEngine;
using UnityEngine.Rendering;

namespace Octopus
{
    /// <summary>
    /// Lớp cảnh trôi theo camera với tỉ lệ khác nhau để tạo chiều sâu.
    /// 1 = dính theo camera (xa vô tận), 0 = đứng yên như màn chơi, âm = tiền cảnh trôi nhanh hơn màn chơi.
    /// Cập nhật ngay trước khi camera vẽ nên không giật dù Cinemachine di chuyển camera trong LateUpdate.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField, Range(-1f, 1f)] float factorX = 0.5f;
        [SerializeField, Range(-1f, 1f)] float factorY = 0.5f;

        Vector3 _origin;

        void Awake()
        {
            _origin = transform.position;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game)
                return;

            Vector3 cameraPosition = camera.transform.position;
            transform.position = _origin + new Vector3(cameraPosition.x * factorX, cameraPosition.y * factorY, 0f);
        }
    }
}
