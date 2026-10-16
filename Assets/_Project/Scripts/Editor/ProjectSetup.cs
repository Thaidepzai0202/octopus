using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Octopus.EditorTools
{
    /// <summary>
    /// Dựng base project: layer, cấu hình editor + mobile, art sinh bằng code và scene Playground.
    /// Chạy lại được nhiều lần (menu Octopus → Dựng lại màn chơi); ảnh vẽ tay trong Art/Drawn được dùng thay art vẽ bằng code. Chi tiết hang động ở CaveBuilder, bạch tuộc ở OctopusBuilder.
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/Playground.unity";
        const string MaterialPath = Root + "/Art/Materials";
        const string BundleId = "com.yourcompany.octopus";

        const string PlayerLayer = "Player";
        const string AnchorableLayer = "Anchorable";
        const string HazardLayer = "Hazard";
        const string EnemyLayer = "Enemy";

        [MenuItem("Octopus/Dựng lại màn chơi (ghi đè scene)", priority = 1)]
        public static void Run()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Octopus", "Scene Playground đã có. Dựng lại sẽ ghi đè mọi chỉnh sửa trong scene đó.", "Dựng lại", "Huỷ"))
                return;

            SetupLayers();
            SetupEditor();
            SetupPlayerSettings();
            RemoveOldArt();
            BuildPlaygroundScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Octopus] Setup xong. Mở {ScenePath} rồi bấm Play.");
        }

        static void SetupLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayer(layers, 6, PlayerLayer);
            SetLayer(layers, 7, AnchorableLayer);
            SetLayer(layers, 8, HazardLayer);
            SetLayer(layers, 9, EnemyLayer);
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
                layer.stringValue = name;
            else if (layer.stringValue != name)
                Debug.LogWarning($"[Octopus] Layer {index} đang là '{layer.stringValue}', không ghi đè thành '{name}'.");
        }

        static void SetupEditor()
        {
            // Bấm Play gần như tức thì: không reload domain. Code runtime không giữ state static nên an toàn.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            const string vscode = "/Applications/Visual Studio Code.app";
            if (Directory.Exists(vscode))
                Unity.CodeEditor.CodeEditor.SetExternalScriptEditor(vscode);
        }

        static void SetupPlayerSettings()
        {
            if (PlayerSettings.companyName == "DefaultCompany")
            {
                PlayerSettings.companyName = "YourCompany";
                PlayerSettings.productName = "Octopus Swing";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            }

            // Game đu ngang: chỉ cho xoay giữa 2 hướng nằm ngang
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        // Art tạm của bản đầu (hình tròn, hình vuông, mắt cũ) đã được thay bằng art sinh trong Art/Generated
        static void RemoveOldArt()
        {
            AssetDatabase.DeleteAsset(Root + "/Art/Placeholder");
            AssetDatabase.DeleteAsset(MaterialPath + "/Line.mat");
        }

        static void BuildPlaygroundScene()
        {
            Sprite glow = ArtFactory.Sprite("Glow", 64, 64, 64f, new Vector2(0.5f, 0.5f), (u, v) =>
            {
                float r = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
                return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2f));
            }, 1, false);

            // Vệt sáng dùng cho viền đá: lõi sáng mảnh ở giữa, quầng mờ toả ra hai bên
            Texture2D softLine = ArtFactory.Texture("SoftLine", 8, 32, TextureWrapMode.Clamp, (u, v) =>
            {
                float d = v - 0.5f;
                float core = Mathf.Exp(-d * d / (0.1f * 0.1f));
                float halo = 0.55f * Mathf.Exp(-d * d / (0.28f * 0.28f));
                return new Color(1f, 1f, 1f, Mathf.Max(core, halo));
            });
            Material glowLine = ArtFactory.Material("GlowLine", softLine);
            Material plainLine = ArtFactory.Material("Line", null);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            CreateGlobalLight();
            CaveBuilder.Build(camera, glow, glowLine, LayerMask.NameToLayer(AnchorableLayer), LayerMask.NameToLayer(HazardLayer));
            GameObject player = OctopusBuilder.Build(plainLine, glow, CreateBodyMaterial(),
                LayerMask.NameToLayer(PlayerLayer), LayerMask.GetMask(AnchorableLayer), LayerMask.GetMask("Default", AnchorableLayer));
            LaserSkillBuilder.Build(player, glow, LayerMask.GetMask("Default", AnchorableLayer));
            CombatBuilder.Build(player, glow, LayerMask.NameToLayer(EnemyLayer)); // bộ chiêu, nộ, bia tập, HUD
            SpiderBuilder.Build(player.GetComponent<OctopusController>(), glow); // hình nhện, bật khi chọn nhện
            CharacterSelectBuilder.AddBackButton(GameObject.Find("HUD/Safe Area").GetComponent<RectTransform>(),
                player.GetComponent<PlayerInputReader>());
            CreateFollowCamera(player);
            GameFeelBuilder.Build(); // khựng hình, rung camera, vòng sao choáng của bia tập

            // Ảnh vẽ tay (nếu có trong Art/Drawn) thay cho art vẽ bằng code
            List<string> drawn = DrawnArt.Apply();
            if (drawn.Count > 0)
                Debug.Log("[Octopus] Dùng ảnh vẽ tay: " + string.Join(", ", drawn));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Màn chọn nhân vật mở đầu tiên, rồi mới vào màn chơi
            CharacterSelectBuilder.BuildScene(glow);
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(CharacterSelectBuilder.ScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }

        static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CaveBuilder.CameraBackground;

            go.AddComponent<AudioListener>();
            go.AddComponent<CinemachineBrain>();
            return camera;
        }

        static void CreateGlobalLight()
        {
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
        }

        static void CreateFollowCamera(GameObject player)
        {
            var go = new GameObject("Follow Camera");

            var camera = go.AddComponent<CinemachineCamera>();
            camera.Follow = player.transform;
            LensSettings lens = LensSettings.Default;
            lens.OrthographicSize = 6f;
            camera.Lens = lens;

            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(0.6f, 0.8f, 0f);
            composer.Lookahead = new LookaheadSettings { Enabled = true, Time = 0.35f, Smoothing = 6f };

            go.AddComponent<SpeedZoom>();
        }

        static PhysicsMaterial2D CreateBodyMaterial()
        {
            string path = $"{MaterialPath}/OctopusBody.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material != null)
                return material;

            Directory.CreateDirectory(MaterialPath);
            material = new PhysicsMaterial2D("OctopusBody") { friction = 0.3f, bounciness = 0.15f };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
