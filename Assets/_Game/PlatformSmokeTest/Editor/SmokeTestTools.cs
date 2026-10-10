using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WeChatWASM;

namespace ConvenienceStore.PlatformTest.Editor
{
    public static class SmokeTestTools
    {
        public const string Root = "Assets/_Game/PlatformSmokeTest";
        public const string ScenePath = Root + "/Scenes/PlatformSmokeTest.unity";
        public const string ConfigPath = Root + "/Data/SmokeTest.json";

        [MenuItem("Game/Platform Test/1 - Create or open test scene")]
        public static void Setup()
        {
            SyncConfig();
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();
            if (File.Exists(ScenePath))
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            else
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.07f, 0.10f);
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                var test = new GameObject("PlatformSmokeTest").AddComponent<PlatformSmokeTest>();
                test.font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/SmokeSansSC.ttf");
                test.configuration = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
                test.testTone = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/test-tone.wav");
                if (test.font == null || test.configuration == null || test.testTone == null)
                    throw new InvalidOperationException("Missing smoke test font, configuration or audio.");
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSettings.serializationMode = SerializationMode.ForceText;
            UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
            PlayerSettings.companyName = "ConvenienceStorePrototype";
            PlayerSettings.productName = "Convenience Store Platform Test";
            PlayerSettings.defaultScreenWidth = 720;
            PlayerSettings.defaultScreenHeight = 1280;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("[SmokeTest] Setup passed: " + ScenePath);
        }

        [MenuItem("Game/Platform Test/2 - Sync configuration")]
        public static void SyncConfig()
        {
            string json = File.ReadAllText("Configs/SmokeTest.json");
            PlatformSmokeTest.ParseConfig(json);
            Directory.CreateDirectory(Root + "/Data");
            if (!File.Exists(ConfigPath) || File.ReadAllText(ConfigPath) != json)
                File.WriteAllText(ConfigPath, json, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Game/Platform Test/3 - Validate")]
        public static void Validate()
        {
            var config = PlatformSmokeTest.ParseConfig(File.ReadAllText(ConfigPath));
            if (File.ReadAllText(ConfigPath) != File.ReadAllText("Configs/SmokeTest.json"))
                throw new InvalidOperationException("Configuration is stale. Run Sync configuration.");
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/SmokeSansSC.ttf");
            if (font == null) throw new InvalidOperationException("Missing bundled test font.");
            foreach (char c in config.title + config.message)
                if (!char.IsWhiteSpace(c) && !font.HasCharacter(c))
                    throw new InvalidOperationException("Test font lacks character: " + c);
            bool invalidRejected = false;
            try { PlatformSmokeTest.ParseConfig("{\"version\":2}"); }
            catch (FormatException) { invalidRejected = true; }
            if (!invalidRejected) throw new InvalidOperationException("Invalid config was accepted.");
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Test scene is missing.");
            Debug.Log("[SmokeTest] Validation passed: configuration, font coverage, scene.");
        }

        [MenuItem("Game/Platform Test/4 - Export WeChat test")]
        public static void ExportWeChat()
        {
            SyncConfig();
            Validate();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WeixinMiniGame)
                throw new InvalidOperationException("Switch to WeixinMiniGame first, then retry.");
            var cfg = WXConvertCore.config;
            cfg.ProjectConf.projectName = "convenience-store-platform-test";
            cfg.ProjectConf.Appid = Environment.GetEnvironmentVariable("WX_APPID") ?? "";
            if (string.IsNullOrEmpty(cfg.ProjectConf.Appid))
                Debug.LogWarning("[SmokeTest] No WX_APPID supplied. Export is for local build validation; enter your AppID before importing in DevTools.");
            cfg.ProjectConf.DST = Path.GetFullPath("Builds/WeChatSmoke");
            cfg.ProjectConf.assetLoadType = 1; // Small test data in a subpackage; no CDN needed.
            cfg.ProjectConf.CDN = "";
            cfg.ProjectConf.Orientation = 0;
            cfg.CompileOptions.DevelopBuild = true;
            cfg.CompileOptions.AutoProfile = false;
            cfg.CompileOptions.Webgl2 = true;
            cfg.CompileOptions.DeleteStreamingAssets = false;
            string node = Environment.GetEnvironmentVariable("WX_NODE_PATH");
            if (!string.IsNullOrEmpty(node)) cfg.CompileOptions.CustomNodePath = node;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WeixinMiniGame, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WeixinMiniGame, ManagedStrippingLevel.Low);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Directory.CreateDirectory(cfg.ProjectConf.DST);
            try
            {
                var result = WXConvertCore.DoExport();
                if (result != WXConvertCore.WXExportError.SUCCEED)
                    throw new InvalidOperationException("WeChat export failed: " + result);
            }
            finally { NormalizeLocalSettings(); }
            if (!File.Exists("Builds/WeChatSmoke/minigame/game.js"))
                throw new InvalidOperationException("Export reported success but game.js is missing.");
            Debug.Log("[SmokeTest] WeChat export passed. DevTools and device validation remain required.");
        }

        public static void NormalizeLocalSettings()
        {
            // Export injects resolved machine paths. Persist only portable defaults.
            var cfg = WXConvertCore.config;
            cfg.ProjectConf.DST = "Builds/WeChatSmoke";
            cfg.ProjectConf.Appid = "";
            cfg.CompileOptions.CustomNodePath = "";
            PlayerSettings.WeixinMiniGame.template = "APPLICATION:Default";
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
        }

        public static void ValidateAndNormalize()
        {
            NormalizeLocalSettings();
            Validate();
        }
    }
}
