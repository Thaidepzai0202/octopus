using UnityEngine;

namespace Octopus
{
    /// <summary>
    /// Mobile mặc định khoá 30fps. Game đu dây cần mượt nên chạy theo tần số quét của màn hình (60–120Hz).
    /// </summary>
    public static class FrameRateSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            // Trên web trình duyệt tự vẽ theo tần số quét màn hình; đặt targetFrameRate làm Unity bỏ nhịp đó → giật
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                return;

            int refreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = Mathf.Clamp(refreshRate, 60, 120);
        }
    }
}
