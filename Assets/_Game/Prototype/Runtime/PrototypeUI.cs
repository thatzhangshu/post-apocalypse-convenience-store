using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace ConvenienceStore.Prototype
{
    // Small uGUI primitives. Artwork is replaceable without touching game rules.
    public sealed class PrototypeUI
    {
        public readonly Font Font;
        public static readonly Color Ink = Hex(0xEDE8D8), Muted = Hex(0x9BA9A3), Night = Hex(0x101C21), Panel = Hex(0x1D3036), Mint = Hex(0x89CEAE), Gold = Hex(0xEEC47F);
        private static Sprite rounded;
        public PrototypeUI(Font font) { Font = font; }
        public static Color Hex(int rgb, float alpha = 1)
        { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha); }
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        private static Sprite RoundSprite()
        {
            if (rounded != null) return rounded;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "Prototype rounded panel"; texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float cx = Mathf.Clamp(x, 8, 23), cy = Mathf.Clamp(y, 8, 23);
                float alpha = Mathf.Clamp01(8.5f - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)));
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            rounded = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(9, 9, 9, 9));
            return rounded;
        }
        public RectTransform Box(Transform p, string name, float x, float y, float w, float h, Color color, bool round = true, bool hit = false)
        {
            var r = Rect(p, name, x, y, w, h); var image = r.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = hit;
            if (round) { image.sprite = RoundSprite(); image.type = Image.Type.Sliced; }
            return r;
        }
        public Text Label(Transform p, string text, float x, float y, float w, float h, int size = 24, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            // Noto SC's line metrics exceed 1 em; short label boxes otherwise drop the entire line.
            var r = Rect(p, "Text", x, y, w, Mathf.Max(h, size * 1.6f)); var t = r.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color ?? Ink;
            t.raycastTarget = false; t.alignment = align; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
        public Button Button(Transform p, string text, float x, float y, float w, float h, UnityAction action, bool enabled = true, bool primary = false, int size = 24)
        {
            var r = Box(p, text, x, y, w, h, primary ? Mint : Hex(0x30464D), true, true);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>();
            var colors = b.colors; colors.highlightedColor = Hex(0xC7E3D4); colors.pressedColor = Hex(0x91A89D); colors.disabledColor = Hex(0x58645F, .55f); b.colors = colors;
            b.interactable = enabled; b.onClick.AddListener(action);
            Label(r, text, 12, 6, w - 24, h - 12, size, primary ? Night : Ink, TextAnchor.MiddleCenter);
            return b;
        }
        public Text ScrollText(Transform p, string text, float x, float y, float w, float h, int size = 25, Color? color = null)
        {
            var r = Box(p, "Scrollable text", x, y, w, h, Color.clear, false, true);
            var scroll = r.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            var viewport = Rect(r, "Viewport", 0, 0, w, h); viewport.gameObject.AddComponent<RectMask2D>();
            var label = Label(viewport, text, 0, 0, w - 12, h, size, color);
            var content = label.rectTransform; content.sizeDelta = new Vector2(w - 12, Mathf.Max(h, label.preferredHeight + 12));
            label.verticalOverflow = VerticalWrapMode.Overflow;
            scroll.viewport = viewport; scroll.content = content;
            return label;
        }
        public static void Clear(Transform p)
        { for (int i = p.childCount - 1; i >= 0; i--) { var child = p.GetChild(i).gameObject; child.SetActive(false); Object.Destroy(child); } }
    }
}
