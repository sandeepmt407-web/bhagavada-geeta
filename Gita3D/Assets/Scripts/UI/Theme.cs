using UnityEngine;
using TMPro;

namespace Gita.UI
{
    /// <summary>
    /// Design tokens for the whole app: one dawn-at-Kurukshetra palette, one type scale.
    /// Everything visual reads from here so the look stays consistent.
    /// </summary>
    public static class Theme
    {
        // ---- palette ---------------------------------------------------
        public static readonly Color NightDeep   = Hex("0B1026"); // deepest sky
        public static readonly Color Twilight    = Hex("16203F"); // panel base
        public static readonly Color TwilightLit = Hex("222E55"); // raised panel
        public static readonly Color Saffron     = Hex("E8A33D"); // sacred accent
        public static readonly Color SaffronLit  = Hex("F5C563"); // highlight
        public static readonly Color Terracotta  = Hex("C8553D"); // secondary accent
        public static readonly Color Cream       = Hex("F5EDE0"); // primary text
        public static readonly Color Parchment   = Hex("D9C9A8"); // secondary text
        public static readonly Color Muted       = Hex("8C94AE"); // tertiary text
        public static readonly Color Scrim       = new(0.04f, 0.06f, 0.15f, 0.82f);

        // ---- type scale (reference resolution 1080 x 1920) --------------
        public const float SizeDisplay = 64f;
        public const float SizeTitle   = 44f;
        public const float SizeHeading = 34f;
        public const float SizeVerseSa = 40f; // Devanagari needs more height
        public const float SizeBody    = 30f;
        public const float SizeLabel   = 25f;
        public const float SizeCaption = 22f;

        // ---- spacing ----------------------------------------------------
        public const float Gutter = 48f;
        public const float GapS   = 12f;
        public const float GapM   = 24f;
        public const float GapL   = 40f;
        public const float Radius = 28f;

        // ---- fonts (populated at boot from Resources/Fonts) -------------
        public static TMP_FontAsset Devanagari { get; private set; } // Noto Serif Devanagari
        public static TMP_FontAsset DevanagariUI { get; private set; }
        public static TMP_FontAsset Serif { get; private set; }      // EB Garamond
        public static TMP_FontAsset Sans { get; private set; }       // Inter
        public static TMP_FontAsset SansBold { get; private set; }

        public static bool FontsReady { get; private set; }

        public static void LoadFonts()
        {
            if (FontsReady) return;

            Devanagari   = Load("Fonts/NotoSerifDevanagari-Regular SDF");
            DevanagariUI = Load("Fonts/NotoSansDevanagari-Regular SDF");
            Serif        = Load("Fonts/EBGaramond-Regular SDF");
            Sans         = Load("Fonts/Inter-Regular SDF");
            SansBold     = Load("Fonts/Inter-SemiBold SDF");

            // Latin fonts fall back to Devanagari glyphs and vice versa, so a
            // mixed-script string never renders as boxes.
            if (Sans != null && Devanagari != null)
            {
                AddFallback(Sans, DevanagariUI);
                AddFallback(SansBold, DevanagariUI);
                AddFallback(Serif, Devanagari);
                AddFallback(Devanagari, Serif);
                AddFallback(DevanagariUI, Sans);
            }

            FontsReady = Sans != null;
            if (!FontsReady)
                Debug.LogWarning("[Gita] Font assets missing - TMP will fall back to its default face.");
        }

        static void AddFallback(TMP_FontAsset target, TMP_FontAsset fallback)
        {
            if (target == null || fallback == null || target == fallback) return;
            target.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (!target.fallbackFontAssetTable.Contains(fallback))
                target.fallbackFontAssetTable.Add(fallback);
        }

        static TMP_FontAsset Load(string path)
        {
            var f = Resources.Load<TMP_FontAsset>(path);
            if (f == null) Debug.LogWarning($"[Gita] Missing font asset: Resources/{path}");
            return f;
        }

        public static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
