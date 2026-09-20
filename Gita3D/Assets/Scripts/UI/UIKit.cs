using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Gita.UI
{
    /// <summary>
    /// Builds uGUI hierarchies from code. The whole interface is constructed at runtime,
    /// so these helpers stand in for what would otherwise be authored prefabs.
    /// </summary>
    public static class UIKit
    {
        // ---- sprite factory ---------------------------------------------
        // Generated once and reused; 9-sliced so one texture serves every size.
        static Sprite _rounded, _roundedSoft, _circle, _glow, _vGradient, _solid;

        public static Sprite Rounded     => _rounded     ??= RoundedRect(32);
        public static Sprite RoundedSoft => _roundedSoft ??= RoundedRect(56);
        public static Sprite Circle      => _circle      ??= CircleSprite(64);
        public static Sprite Glow        => _glow        ??= RadialGlow(128);
        public static Sprite VGradient   => _vGradient   ??= VerticalGradient(256);
        public static Sprite Solid       => _solid       ??= SolidSprite();

        /// <summary>A 9-sliced rounded rectangle with antialiased corners.</summary>
        public static Sprite RoundedRect(int radius)
        {
            int size = radius * 2 + 2;
            var tex = NewTexture(size, size);
            var px = new Color[size * size];
            float r = radius;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // clamp to the nearest corner centre, then measure distance from it
                float cx = x < r ? r : (x > size - 1 - r ? size - 1 - r : x);
                float cy = y < r ? r : (y > size - 1 - r ? size - 1 - r : y);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(r - d + 0.5f); // 1px feather
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        public static Sprite CircleSprite(int size)
        {
            var tex = NewTexture(size, size);
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Soft radial falloff, for halos behind sacred text and bloom in UI.</summary>
        public static Sprite RadialGlow(int size)
        {
            var tex = NewTexture(size, size);
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // smoothstep
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Opaque at the bottom, clear at the top - seats UI over the 3D world.</summary>
        public static Sprite VerticalGradient(int height)
        {
            var tex = NewTexture(1, height);
            var px = new Color[height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                float a = Mathf.Clamp01(1f - t);
                px[y] = new Color(1f, 1f, 1f, a * a); // quadratic, gentler top edge
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
        }

        public static Sprite SolidSprite()
        {
            var tex = NewTexture(4, 4);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        static Texture2D NewTexture(int w, int h) =>
            new(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

        // ---- hierarchy helpers -------------------------------------------

        /// <summary>An empty full-stretch RectTransform parented to <paramref name="parent"/>.</summary>
        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Raw(string name, Transform parent, Color color, Sprite sprite)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string content,
            TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = content ?? string.Empty;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>A tappable region. Transparent by default; pass a tint for a visible button.</summary>
        public static Button Tappable(string name, Transform parent, Action onClick, Color? tint = null)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = tint ?? new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor    = Color.white;
            colors.disabledColor    = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration     = 0.12f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // ---- layout shorthands -------------------------------------------

        /// <summary>Stretch horizontally, fixed height, measured down from the top edge.</summary>
        public static RectTransform TopBand(this RectTransform rt, float top, float height, float inset = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(inset, -top - height);
            rt.offsetMax = new Vector2(-inset, -top);
            return rt;
        }

        /// <summary>Stretch horizontally, fixed height, measured up from the bottom edge.</summary>
        public static RectTransform BottomBand(this RectTransform rt, float bottom, float height, float inset = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(inset, bottom);
            rt.offsetMax = new Vector2(-inset, bottom + height);
            return rt;
        }

        /// <summary>Inset from all four edges of the parent.</summary>
        public static RectTransform Inset(this RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Size(this RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static CanvasGroup Group(this Component c) =>
            c.GetComponent<CanvasGroup>() ?? c.gameObject.AddComponent<CanvasGroup>();
    }
}
