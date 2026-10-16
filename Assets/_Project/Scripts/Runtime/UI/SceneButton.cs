using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Octopus
{
    /// <summary>
    /// Nút trên màn hình chơi để chuyển sang scene khác (vd quay về màn chọn nhân vật). Chạm vào nút không làm bạch tuộc
    /// nhảy / bắn xúc tu (nút tự đăng ký vùng chặn với PlayerInputReader).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SceneButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] string scene = CharacterSelection.SelectScene;
        [SerializeField] PlayerInputReader input;

        void OnEnable()
        {
            if (input != null)
                input.AddBlocker((RectTransform)transform);
        }

        void OnDisable()
        {
            if (input != null)
                input.RemoveBlocker((RectTransform)transform);
        }

        public void OnPointerClick(PointerEventData eventData) => SceneManager.LoadScene(scene);
    }
}
