using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sim.Controls
{
    // Tiny runtime UI toolkit so the game needs no UI prefabs. Replace with real UI later.
    public static class UiKit
    {
        static Sprite circle, ring;
        static Font font;

        public static Font Font => font ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>()) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#else
            Debug.LogWarning("No EventSystem in the scene and the legacy input module is disabled. " +
                             "Add one with GameObject > UI > Event System so touch controls work.");
#endif
        }

        // Pivot equals anchor, so `pos` offsets the element from that corner or edge.
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite = null)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
            string text, int fontSize, TextAnchor align, Color color)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = fontSize; t.alignment = align; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
            string text, int fontSize, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var img = Panel(parent, name, anchor, pos, size, color, Circle());
            img.type = Image.Type.Simple;
            var b = img.gameObject.AddComponent<Button>();
            if (onClick != null) b.onClick.AddListener(onClick);
            var l = Label(img.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, size, text, fontSize, TextAnchor.MiddleCenter, Color.white);
            return b;
        }

        public static Sprite Circle() => circle ? circle : (circle = MakeSprite(false));
        public static Sprite Ring() => ring ? ring : (ring = MakeSprite(true));

        static Sprite MakeSprite(bool hollow)
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;       // 0 centre, 1 edge
                float a = Mathf.Clamp01((1f - d) * c * 0.5f);                          // soft outer edge
                if (hollow) a *= Mathf.Clamp01((d - 0.78f) * c * 0.5f);                // cut out the centre
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
