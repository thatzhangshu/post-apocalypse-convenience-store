using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WeChatWASM;

namespace ConvenienceStore.Prototype.Editor
{
    public static class PrototypeTools
    {
        public const string Root = "Assets/_Game/Prototype";
        public const string ScenePath = Root + "/Scenes/Prototype.unity";
        public const string ConfigPath = Root + "/Data/Prototype.json";
        public const string FontPath = Root + "/Fonts/PrototypeSansSC.ttf";
        [MenuItem("Game/Prototype/1 - Create or open prototype")]
        public static void Setup()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Sync(); Directory.CreateDirectory(Root + "/Scenes"); AssetDatabase.Refresh();
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = PrototypeUI.Night;
                camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
                var app = new GameObject("PrototypeApp").AddComponent<PrototypeApp>();
                app.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                app.configuration = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var currentApp = UnityEngine.Object.FindObjectOfType<PrototypeApp>();
            if (currentApp == null) throw new InvalidOperationException("Prototype scene is missing its app component.");
            currentApp.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            currentApp.configuration = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            EditorSceneManager.MarkSceneDirty(currentApp.gameObject.scene);
            EditorSceneManager.SaveScene(currentApp.gameObject.scene, ScenePath);
            PlayerSettings.defaultScreenWidth = 720; PlayerSettings.defaultScreenHeight = 1280;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets(); Validate();
            Debug.Log("[Prototype] Setup passed: " + ScenePath);
        }
        [MenuItem("Game/Prototype/2 - Sync configuration")]
        public static void Sync()
        {
            string json = File.ReadAllText("Configs/Prototype.json"); GameConfig.Parse(json);
            Directory.CreateDirectory(Root + "/Data");
            if (!File.Exists(ConfigPath) || File.ReadAllText(ConfigPath) != json) File.WriteAllText(ConfigPath, json, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceSynchronousImport);
        }
        [MenuItem("Game/Prototype/3 - Validate and run logic checks")]
        public static void Validate()
        {
            var config = GameConfig.Parse(File.ReadAllText(ConfigPath));
            if (File.ReadAllText(ConfigPath) != File.ReadAllText("Configs/Prototype.json")) throw new InvalidOperationException("Run Sync configuration first.");
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) throw new InvalidOperationException("Missing prototype font. Run Tools/Generate-PrototypeFont.py.");
            string text = File.ReadAllText(ConfigPath) + string.Join("", Directory.GetFiles(Root + "/Runtime", "*.cs").Select(File.ReadAllText));
            foreach (char c in text.Distinct()) if (!char.IsWhiteSpace(c) && !font.HasCharacter(c)) throw new InvalidOperationException("Prototype font lacks character: " + c);
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Missing prototype scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var app = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PrototypeApp>()).Single();
            if (app.font == null || app.configuration == null) throw new InvalidOperationException("Missing scene references.");
            PrototypeChecks.Run(config);
            Debug.Log("[Prototype] Validation passed: content references, font coverage, scene, gameplay checks.");
        }
        [MenuItem("Game/Prototype/4 - Build Windows preview")]
        public static void BuildWindows()
        {
            Sync(); Validate(); Directory.CreateDirectory("Builds/PrototypeWindows");
            PlayerSettings.productName = "末日便利店 · 三日原型";
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/PrototypeWindows/ConvenienceStore.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Windows preview build failed.");
            Debug.Log("[Prototype] Windows preview built successfully.");
        }
        [MenuItem("Game/Prototype/5 - Export WeChat prototype")]
        public static void ExportWeChat()
        {
            Sync(); Validate();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WeixinMiniGame) throw new InvalidOperationException("Switch to WeixinMiniGame before exporting.");
            var originalScenes = EditorBuildSettings.scenes;
            var cfg = WXConvertCore.config;
            string originalDst = cfg.ProjectConf.DST, originalName = cfg.ProjectConf.projectName, originalApp = cfg.ProjectConf.Appid;
            try
            {
                cfg.ProjectConf.DST = Path.GetFullPath("Builds/WeChatPrototype"); cfg.ProjectConf.projectName = "convenience-store-prototype";
                cfg.ProjectConf.Appid = Environment.GetEnvironmentVariable("WX_APPID") ?? "";
                if (string.IsNullOrEmpty(cfg.ProjectConf.Appid)) Debug.LogWarning("[Prototype] AppID absent: this validates export only; DevTools/device checks remain required.");
                cfg.ProjectConf.assetLoadType = 1; cfg.ProjectConf.CDN = ""; cfg.ProjectConf.Orientation = 0;
                cfg.CompileOptions.DevelopBuild = true; cfg.CompileOptions.AutoProfile = false; cfg.CompileOptions.Webgl2 = true;
                cfg.CompileOptions.DeleteStreamingAssets = false;
                string node = Environment.GetEnvironmentVariable("WX_NODE_PATH"); if (!string.IsNullOrEmpty(node)) cfg.CompileOptions.CustomNodePath = node;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.WeixinMiniGame, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WeixinMiniGame, ManagedStrippingLevel.Low);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                Directory.CreateDirectory(cfg.ProjectConf.DST);
                var result = WXConvertCore.DoExport();
                if (result != WXConvertCore.WXExportError.SUCCEED) throw new InvalidOperationException("WeChat export failed: " + result);
            }
            finally
            {
                EditorBuildSettings.scenes = originalScenes;
                ConvenienceStore.PlatformTest.Editor.SmokeTestTools.NormalizeLocalSettings();
                cfg.ProjectConf.DST = originalDst; cfg.ProjectConf.projectName = originalName; cfg.ProjectConf.Appid = originalApp;
                EditorUtility.SetDirty(cfg); AssetDatabase.SaveAssets();
            }
            Debug.Log("[Prototype] WeChat prototype export passed. Device checks remain required.");
        }
    }
}
