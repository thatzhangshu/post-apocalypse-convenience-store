#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ConvenienceStore.Prototype
{
    // Opt-in development QA. Never reads/writes the player's save slot.
    public sealed class PrototypeCaptureDriver : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-prototypeCaptureDir");
            string directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : "Logs/PrototypePreview";
            Directory.CreateDirectory(directory);
            var app = GetComponent<PrototypeApp>();
            yield return new WaitForSecondsRealtime(1);
            app.PreviewForCapture("store"); yield return Capture(directory, "01-store");
            Click("小林"); yield return new WaitForSecondsRealtime(3);
            Click("继续 →"); yield return new WaitForSecondsRealtime(3);
            Click("记下医院的位置"); yield return new WaitForSecondsRealtime(3);
            Click("阅读完毕 → 返回");
            app.PreviewForCapture("map"); yield return Capture(directory, "02-map");
            app.PreviewForCapture("dialogue"); yield return Capture(directory, "03-dialogue");
            Click("邀请医务志愿者协助店铺"); yield return new WaitForSecondsRealtime(3);
            Click("阅读完毕 → 返回");
            app.PreviewForCapture("journal"); yield return Capture(directory, "04-journal");
            Click("便利店"); Click("撤下货架"); Click("药品 ×2"); Click("上架商品");
            Click("营业 1 时段"); yield return Capture(directory, "05-day-end");
            Click("开始第 2 天");
            app.PreviewForCapture("ending"); yield return Capture(directory, "06-ending");
            Click("回顾日志"); Click("查看三日结果");
            Debug.Log("[Prototype] UI click smoke checks and six rendered previews passed.");
            Application.Quit(0);
        }
        private static void Click(string caption)
        {
            var button = FindObjectsOfType<Button>().FirstOrDefault(x => x.isActiveAndEnabled && x.IsInteractable() && x.GetComponentInChildren<Text>().text == caption);
            if (button == null) { Debug.LogError("[Prototype] UI button missing/disabled: " + caption); Application.Quit(1); throw new InvalidOperationException(caption); }
            button.onClick.Invoke();
        }
        private static IEnumerator Capture(string directory, string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.GetFullPath(Path.Combine(directory, name + ".png"));
            // Hidden Windows players can skip their swapchain. Render the canvas to a texture explicitly.
            var camera = Camera.main; var canvas = FindObjectOfType<Canvas>();
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera; var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(720, 1280, 24); var texture = new Texture2D(720, 1280, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0); texture.Apply();
                if (texture.GetPixels32().Select(x => (int)x.r << 16 | (int)x.g << 8 | x.b).Distinct().Take(32).Count() < 32)
                    throw new InvalidOperationException("Preview rendering produced a blank image.");
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = previousCamera; camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                target.Release(); Destroy(target); Destroy(texture); Canvas.ForceUpdateCanvases();
            }
            Debug.Log("[Prototype] Rendered " + name);
        }
    }
}
#endif
