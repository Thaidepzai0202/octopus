using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Co RectTransform này vào vùng an toàn của màn hình (tránh tai thỏ, góc bo, thanh Home trên iPhone),
    /// để nút ở góc màn hình không bị che hay khó bấm.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect _applied;
        Vector2Int _screen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != _applied || Screen.width != _screen.x || Screen.height != _screen.y)
                Apply();
        }

        void Apply()
        {
            Rect area = Screen.safeArea;
            _applied = area;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
