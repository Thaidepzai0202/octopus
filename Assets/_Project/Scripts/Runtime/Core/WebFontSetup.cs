using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Octopus
{
    /// <summary>
    /// Font mặc định của Unity (LegacyRuntime) thiếu các chữ có dấu chồng (ọ, ậ, ộ, ắ…). Trên iOS / Android / Editor máy
    /// tự mượn font hệ thống bù vào, còn trên web không có font hệ thống nên chữ bị mất. Bản web thay mọi Text đang dùng
    /// font mặc định bằng Liberation Sans (Resources/Fonts, giấy phép OFL): cùng kích thước chữ nên bố cục không xô lệch.
    /// </summary>
    public static class WebFontSetup
    {
        const string BuiltinFontName = "LegacyRuntime";

        static Font _regular;
        static Font _bold;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                return;

            _regular = Resources.Load<Font>("Fonts/LiberationSans-Regular");
            _bold = Resources.Load<Font>("Fonts/LiberationSans-Bold");
            SceneManager.sceneLoaded += (scene, mode) => Apply(scene);
        }

        // sceneLoaded chạy sau Awake nên Text tạo lúc Awake (DamageNumbers) cũng được đổi
        static void Apply(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    if (text.font == null || text.font.name == BuiltinFontName)
                        Replace(text);
                }
            }
        }

        // Font đậm đã có sẵn nét đậm: bỏ kiểu Bold để Unity không làm đậm thêm lần nữa
        static void Replace(Text text)
        {
            switch (text.fontStyle)
            {
                case FontStyle.Bold:
                    text.font = _bold;
                    text.fontStyle = FontStyle.Normal;
                    break;
                case FontStyle.BoldAndItalic:
                    text.font = _bold;
                    text.fontStyle = FontStyle.Italic;
                    break;
                default:
                    text.font = _regular;
                    break;
            }
        }
    }
}
