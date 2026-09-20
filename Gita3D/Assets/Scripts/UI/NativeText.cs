using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gita.UI
{
    /// <summary>
    /// Bridge to the Android text rasteriser. Unity's text engine does not shape
    /// Indic scripts - no matra reordering, no conjuncts - so Sanskrit goes through
    /// Android's own stack instead and comes back as a coverage bitmap.
    ///
    /// Every entry point is defensive: if anything at all fails, <see cref="Available"/>
    /// latches false and callers fall back to TextMeshPro for the rest of the session.
    /// </summary>
    public static class NativeText
    {
        public const int AlignStart = 0;
        public const int AlignCenter = 1;
        public const int AlignEnd = 2;

        /// <summary>Font paths are relative to StreamingAssets, which lands in APK assets/.</summary>
        public const string SerifDevanagari = "Fonts/NotoSerifDevanagari-Regular.ttf";
        public const string SansDevanagari = "Fonts/NotoSansDevanagari-Regular.ttf";

        static bool _probed;
        static bool _available;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass _rasterizer;
#endif

        /// <summary>True when native shaping is usable on this device.</summary>
        public static bool Available
        {
            get
            {
                if (!_probed) Probe();
                return _available;
            }
        }

        static void Probe()
        {
            _probed = true;
            _available = false;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _rasterizer = new AndroidJavaClass("com.gita.text.TextRasterizer");
                // Round-trip a known string so a missing class or a signature mismatch
                // is discovered here rather than mid-render.
                var probe = _rasterizer.CallStatic<byte[]>("rasterize",
                    "कि", "", 24f, 64, AlignStart, 1f, 128);
                _available = probe != null && probe.Length > 8;
                if (!_available)
                    Debug.LogWarning("[Gita] Native text probe returned nothing - using TextMeshPro.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Native text unavailable ({e.GetType().Name}) - using TextMeshPro.");
                _available = false;
            }
#endif
        }

        // ---- render cache -------------------------------------------------
        // Verses are revisited constantly (paging back and forth), and a raster is
        // far more expensive than a dictionary lookup.

        const int CacheLimit = 24;
        static readonly Dictionary<string, Texture2D> Cache = new();
        static readonly LinkedList<string> Recent = new();

        /// <summary>
        /// Renders shaped text to a texture. Returns null when native shaping is not
        /// available or the render failed - the caller must handle that.
        /// </summary>
        public static Texture2D Render(string text, string fontAsset, float textSizePx,
            int maxWidthPx, int align, float lineSpacing)
        {
            if (string.IsNullOrEmpty(text) || !Available) return null;

            string key = $"{fontAsset}|{textSizePx:F1}|{maxWidthPx}|{align}|{lineSpacing:F2}|{text}";
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                Touch(key);
                return cached;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var data = _rasterizer.CallStatic<byte[]>("rasterize",
                    text, fontAsset, textSizePx, maxWidthPx, align, lineSpacing, 4096);

                var tex = Decode(data);
                if (tex == null) return null;

                Insert(key, tex);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Native render failed: {e.Message}");
                _available = false;
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>Header (w,h as little-endian int32) followed by one alpha byte per pixel.</summary>
        static Texture2D Decode(byte[] data)
        {
            if (data == null || data.Length <= 8) return null;

            int w = BitConverter.ToInt32(data, 0);
            int h = BitConverter.ToInt32(data, 4);
            if (w <= 0 || h <= 0 || w > 4096 || h > 4096) return null;
            if (data.Length < 8 + w * h) return null;

            var rgba = new byte[w * h * 4];
            // Android lays the bitmap out top-down; Unity textures are bottom-up.
            for (int y = 0; y < h; y++)
            {
                int src = 8 + (h - 1 - y) * w;
                int dst = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int o = dst + x * 4;
                    rgba[o] = 255;      // white, tinted by the UI colour
                    rgba[o + 1] = 255;
                    rgba[o + 2] = 255;
                    rgba[o + 3] = data[src + x];
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            tex.LoadRawTextureData(rgba);
            tex.Apply(false, true);
            return tex;
        }

        static void Touch(string key)
        {
            Recent.Remove(key);
            Recent.AddFirst(key);
        }

        static void Insert(string key, Texture2D tex)
        {
            Cache[key] = tex;
            Recent.AddFirst(key);

            while (Recent.Count > CacheLimit)
            {
                var oldest = Recent.Last.Value;
                Recent.RemoveLast();
                if (Cache.Remove(oldest, out var stale) && stale != null)
                    UnityEngine.Object.Destroy(stale);
            }
        }

        public static void ClearCache()
        {
            foreach (var tex in Cache.Values)
                if (tex != null) UnityEngine.Object.Destroy(tex);
            Cache.Clear();
            Recent.Clear();
        }
    }
}
