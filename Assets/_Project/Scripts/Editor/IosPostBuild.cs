#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Octopus.EditorTools
{
    /// <summary>Sửa Info.plist sau mỗi lần build iOS để khỏi chỉnh tay trong Xcode.</summary>
    public static class IosPostBuild
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
                return;

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            // Game không dùng mã hoá đặc biệt → TestFlight không hỏi export compliance mỗi lần upload
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            // Cho phép chạy 120Hz trên iPhone ProMotion
            plist.root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);

            plist.WriteToFile(plistPath);
        }
    }
}
#endif
