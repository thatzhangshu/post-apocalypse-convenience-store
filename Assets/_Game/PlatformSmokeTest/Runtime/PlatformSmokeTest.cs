using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace ConvenienceStore.PlatformTest
{
    [Serializable]
    public sealed class SmokeConfig
    {
        public int version;
        public string title;
        public string message;
    }

    public sealed class PlatformSmokeTest : MonoBehaviour
    {
        public const string StorageKey = "convenience-store.smoke.v1.count";
        public Font font;
        public TextAsset configuration;
        public AudioClip testTone;

        private Text counterText;
        private Text statusText;
        private Text lifecycleText;
        private RectTransform safeArea;
        private RectTransform content;
        private RectTransform indicator;
        private AudioSource speaker;
        private Button[] storageButtons;
        private int count;
        private int showCount;
        private int hideCount;
        private bool storageReady;
        private Rect lastSafeArea;

        private void Start()
        {
            Application.targetFrameRate = 30;
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.clip = testTone;
            BuildInterface();
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
            WX.InitSDK(code =>
            {
                if (this == null) return;
                if (code != 0) { Report("WX SDK init failed: " + code); return; }
                storageReady = true;
                SetStorageButtons(true);
                WX.OnShow(OnWxShow);
                WX.OnHide(OnWxHide);
                var info = WX.GetSystemInfoSync();
                Report("WX SDK ready / " + info.platform);
                LoadCount();
            });
#else
            storageReady = true;
            SetStorageButtons(true);
            Report("Editor / desktop storage; WX SDK requires WeChat runtime.");
            LoadCount();
#endif
        }

        private void Update()
        {
            if (indicator != null) indicator.Rotate(0, 0, -45 * Time.unscaledDeltaTime);
            Rect area = Screen.safeArea;
            if (safeArea != null && area != lastSafeArea && Screen.width > 0 && Screen.height > 0)
            {
                safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
                safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
                lastSafeArea = area;
                Canvas.ForceUpdateCanvases();
                float scale = Mathf.Min(safeArea.rect.width / 720f, safeArea.rect.height / 1280f);
                content.localScale = Vector3.one * scale;
            }
        }

        public static SmokeConfig ParseConfig(string json)
        {
            var value = JsonUtility.FromJson<SmokeConfig>(json);
            if (value == null || value.version != 1 || string.IsNullOrWhiteSpace(value.title) || string.IsNullOrWhiteSpace(value.message))
                throw new FormatException("Smoke config requires version=1, title and message.");
            return value;
        }

        private void BuildInterface()
        {
            var canvasObject = new GameObject("SmokeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            safeArea = NewRect("SafeArea", canvasObject.transform);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            lastSafeArea = new Rect(-1, -1, -1, -1);
            content = NewRect("Content", safeArea);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(720, 1280);

            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);

            TextAt("WECHAT PLATFORM TEST", 38, 55, 620, 55);
            string configText;
            try
            {
                var data = ParseConfig(configuration.text);
                configText = data.title + "\n" + data.message;
            }
            catch (Exception e)
            {
                configText = "CONFIG ERROR: " + e.Message;
                Debug.LogException(e);
            }
            TextAt(configText, 28, 125, 620, 150);
            counterText = TextAt("Count: 0", 46, 290, 520, 80);
            indicator = NewRect("RenderingIndicator", content);
            Position(indicator, 590, 310, 40, 40);
            indicator.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.85f, 0.62f);

            ButtonAt("Tap +1", 390, () => { count++; RefreshCounter(); Report("Touch received."); });
            storageButtons = new[]
            {
                ButtonAt("Save", 490, SaveCount),
                ButtonAt("Load", 590, LoadCount),
                ButtonAt("Reset test save", 690, ResetCount)
            };
            SetStorageButtons(false);
            ButtonAt("Play sound", 790, () => { speaker.Play(); Report("Sound requested. Confirm it is audible."); });
            lifecycleText = TextAt("Show: 0 / Hide: 0", 25, 895, 620, 55);
            statusText = TextAt("Initializing...", 24, 960, 620, 110);
            TextAt("Save, fully close, reopen, then Load.\nSwitch apps and return to test lifecycle.", 23, 1095, 620, 100);
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Position(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private Text TextAt(string value, int size, float top, float width, float height)
        {
            var rect = NewRect("Label", content);
            Position(rect, 50, top, width, height);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.text = value;
            label.color = new Color(0.9f, 0.95f, 0.97f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button ButtonAt(string caption, float top, UnityEngine.Events.UnityAction action)
        {
            var rect = NewRect(caption, content);
            Position(rect, 50, top, 620, 80);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.32f, 0.36f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(action);
            var labelRect = NewRect("Caption", rect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelRect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 30;
            label.text = caption;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return button;
        }

        private void SetStorageButtons(bool enabled)
        {
            foreach (var button in storageButtons) button.interactable = enabled;
        }

        private void SaveCount()
        {
            if (!storageReady) return;
            try
            {
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
                WX.StorageSetIntSync(StorageKey, count);
#else
                PlayerPrefs.SetInt(StorageKey, count);
                PlayerPrefs.Save();
#endif
                Report("Saved count: " + count);
            }
            catch (Exception e) { Report("SAVE ERROR: " + e.Message); }
        }

        private void LoadCount()
        {
            if (!storageReady) return;
            try
            {
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
                count = WX.StorageGetIntSync(StorageKey, 0);
#else
                count = PlayerPrefs.GetInt(StorageKey, 0);
#endif
                RefreshCounter();
                Report("Loaded count: " + count);
            }
            catch (Exception e) { Report("LOAD ERROR: " + e.Message); }
        }

        private void ResetCount() { count = 0; RefreshCounter(); SaveCount(); }
        private void RefreshCounter() { counterText.text = "Count: " + count; }
        private void Report(string value) { statusText.text = value; Debug.Log("[SmokeTest] " + value); }
        private void Lifecycle(bool showing)
        {
            if (showing) showCount++; else hideCount++;
            if (lifecycleText != null) lifecycleText.text = "Show: " + showCount + " / Hide: " + hideCount;
            Debug.Log("[SmokeTest] " + (showing ? "show" : "hide"));
        }
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
        private void OnWxShow(OnShowListenerResult result) { Lifecycle(true); }
        private void OnWxHide(GeneralCallbackResult result) { Lifecycle(false); }
        private void OnDestroy()
        {
            if (!storageReady) return;
            WX.OffShow(OnWxShow);
            WX.OffHide(OnWxHide);
        }
#else
        private void OnApplicationFocus(bool focused) { Lifecycle(focused); }
#endif
    }
}
