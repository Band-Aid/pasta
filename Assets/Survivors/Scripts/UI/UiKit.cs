using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PastaSurvivors
{
    public static class UiKit
    {
        public static Font Font;
        public static Sprite Round, White, Pill;
        public static readonly Color Ink = new Color(0.08f, 0.06f, 0.07f, 0.88f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.36f);
        public static readonly Color Cream = new Color(1f, 0.96f, 0.86f);
        public static readonly Color Basil = new Color(0.35f, 0.75f, 0.35f);
        public static readonly Color Tomato = new Color(0.9f, 0.25f, 0.2f);

        public static void Init()
        {
            if (Font != null) return;
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Round = RoundedSprite(48, 14);
            Pill = RoundedSprite(48, 24);
            var tex = Texture2D.whiteTexture;
            White = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private static Sprite RoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        public static Canvas MakeCanvas(string name, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * inset;
            rt.offsetMax = -Vector2.one * inset;
            return rt;
        }

        public static Image Img(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite = null, Vector2? pivot = null)
        {
            var rt = Rect(parent, name, anchor, pos, size, pivot);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ?? Round;
            img.type = img.sprite == Round || img.sprite == Pill ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Fill(Image img)
        {
            img.sprite = White;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = 1f;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align,
            Vector2 anchor, Vector2 pos, Vector2 box, FontStyle style = FontStyle.Bold, bool outline = true, Vector2? pivot = null)
        {
            var rt = Rect(parent, name, anchor, pos, box, pivot);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.75f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }
    }

    /// <summary>Keyboard / gamepad / mouse navigation for a list or grid of buttons.</summary>
    public class MenuNav
    {
        public class Item
        {
            public RectTransform rt;
            public Image bg;
            public System.Action act;
            public System.Action<int> adjust;
            public bool enabled = true;
            public Color normal;
        }

        public readonly List<Item> items = new List<Item>();
        public int index, columns = 1;
        public Color highlight = new Color(1f, 0.82f, 0.36f, 1f);
        public System.Action onCancel;
        private float armedAt;

        public void Clear() { items.Clear(); index = 0; }

        public Item Add(RectTransform rt, Image bg, System.Action act, bool enabled = true)
        {
            var it = new Item { rt = rt, bg = bg, act = act, enabled = enabled, normal = bg != null ? bg.color : Color.white };
            items.Add(it);
            return it;
        }

        /// <summary>Ignore input for a moment after opening so held buttons don't instantly confirm.</summary>
        public void Arm(float delay = 0.25f) => armedAt = UnityEngine.Time.unscaledTime + delay;

        public void Update()
        {
            if (items.Count == 0) return;
            bool armed = UnityEngine.Time.unscaledTime >= armedAt;
            var nav = Controls.Nav();
            int prev = index;
            if (nav.x != 0 && columns > 1) index = Mathf.Clamp(index + nav.x, 0, items.Count - 1);
            if (nav.y != 0) index = Mathf.Clamp(index - nav.y * columns, 0, items.Count - 1);
            if (nav.y != 0 && columns == 1 && prev == index)
                index = (index - nav.y + items.Count) % items.Count;
            if (armed && nav.x != 0 && columns == 1) items[index].adjust?.Invoke(nav.x);
            var mouse = Controls.MousePosition;
            bool hovering = false;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].rt == null || !items[i].rt.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(items[i].rt, mouse, null))
                {
                    if (Controls.MouseDelta.sqrMagnitude > 0.5f || Controls.MouseClicked) index = i;
                    hovering = i == index;
                }
            }
            if (index != prev) G.Sfx?.Play(SfxId.Select, 0.5f);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.bg == null) continue;
                var baseColor = it.enabled ? it.normal : new Color(it.normal.r * 0.5f, it.normal.g * 0.5f, it.normal.b * 0.5f, it.normal.a);
                it.bg.color = i == index ? Color.Lerp(baseColor, highlight, 0.55f) : baseColor;
                float s = i == index ? 1.04f : 1f;
                it.rt.localScale = Vector3.Lerp(it.rt.localScale, Vector3.one * s, 0.35f);
            }
            if (!armed) return;
            int number = Controls.NumberKey();
            if (number >= 0 && number < items.Count) { index = number; Submit(); return; }
            if (Controls.Submit() || (Controls.MouseClicked && hovering)) { Submit(); return; }
            if (Controls.Cancel() && onCancel != null) { G.Sfx?.Play(SfxId.Select, 0.5f, 0.8f); onCancel(); }
        }

        private void Submit()
        {
            var it = items[index];
            if (!it.enabled) { G.Sfx?.Play(SfxId.Hurt, 0.3f, 1.5f); return; }
            G.Sfx?.Play(SfxId.Confirm, 0.6f);
            it.act?.Invoke();
        }
    }
}
