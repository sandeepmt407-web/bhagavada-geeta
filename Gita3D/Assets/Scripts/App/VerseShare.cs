using System;
using UnityEngine;
using Gita.Data;
using Gita.UI;

namespace Gita.App
{
    /// <summary>
    /// Builds a share card for a verse - the live dawn scene with the Sanskrit and the
    /// translation set over it - and hands it to the system share sheet.
    ///
    /// Text is drawn through the same platform shaper the reader uses, so the Devanagari
    /// on a shared image is correct rather than the mis-shaped TextMeshPro fallback.
    /// If anything is unavailable the verse still goes out as plain text.
    /// </summary>
    public static class VerseShare
    {
        const int Width = 1080;
        const int Height = 1350;   // 4:5, the portrait that survives most feeds

        public static void Share(Verse verse, int edition)
        {
            if (verse == null) return;

            string translation = GitaDatabase.TextOf(verse, edition, out int actual);
            var record = GitaDatabase.EditionAt(actual);
            string caption = $"{verse.sa}\n\n{translation}\n\n" +
                             $"Bhagavad Gita {verse.c}.{verse.v}" +
                             (record != null ? $" — {record.translator}" : "");

            var png = TryCompose(verse, translation, record);
            if (png != null && ShareImage(png, $"gita-{verse.c}-{verse.v}.png", caption)) return;

            ShareText(caption);
        }

        // ------------------------------------------------------------------

        /// <summary>Renders the card, or null if the pieces for it are not available.</summary>
        static byte[] TryCompose(Verse verse, string translation, Edition record)
        {
            if (!NativeText.Available) return null;

            var cam = Camera.main;
            if (cam == null) return null;

            RenderTexture rt = null;
            Texture2D card = null;
            var previousTarget = cam.targetTexture;

            try
            {
                rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.DefaultHDR);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = previousTarget;

                var previousActive = RenderTexture.active;
                RenderTexture.active = rt;
                card = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                card.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                card.Apply();
                RenderTexture.active = previousActive;

                var pixels = card.GetPixels32();
                Darken(pixels);
                DrawRule(pixels, Height - 300, 3, Theme.Saffron, 0.85f, 360);

                // Sanskrit, centred in the upper middle.
                var sanskrit = NativeText.Render(FirstLines(verse.sa, 2),
                    NativeText.SerifDevanagari, 60f, Width - 220, NativeText.AlignCenter, 1.30f);
                int y = 360;
                if (sanskrit != null)
                {
                    Blit(pixels, sanskrit, 110, y, Theme.Cream);
                    y += sanskrit.height + 70;
                }

                // Translation beneath it.
                var body = NativeText.Render(Trim(translation, 320),
                    NativeText.SerifLatin, 40f, Width - 260, NativeText.AlignCenter, 1.36f);
                if (body != null) Blit(pixels, body, 130, y, Theme.Parchment);

                // Footer: the reference and the translator.
                var footer = NativeText.Render(
                    $"BHAGAVAD GITA  {verse.c}.{verse.v}" +
                    (record != null ? $"   ·   {record.translator.ToUpperInvariant()}" : ""),
                    NativeText.SansLatin, 27f, Width - 160, NativeText.AlignCenter, 1.2f);
                if (footer != null)
                    Blit(pixels, footer, 80, Height - 230, Theme.Saffron);

                card.SetPixels32(pixels);
                card.Apply(false, false);
                return card.EncodeToPNG();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Could not compose the share card: {e.Message}");
                cam.targetTexture = previousTarget;
                return null;
            }
            finally
            {
                if (card != null) UnityEngine.Object.Destroy(card);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Pulls the scene down so set text stays readable over it.</summary>
        static void Darken(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].r = (byte)(pixels[i].r * 0.42f);
                pixels[i].g = (byte)(pixels[i].g * 0.42f);
                pixels[i].b = (byte)(pixels[i].b * 0.46f);
                pixels[i].a = 255;
            }
        }

        /// <summary>
        /// Alpha-blends a coverage bitmap onto the card.
        /// The source rows run top-down; the destination runs bottom-up.
        /// </summary>
        static void Blit(Color32[] dest, Texture2D coverage, int x, int yFromTop, Color tint)
        {
            var src = coverage.GetPixels32();
            int cw = coverage.width, ch = coverage.height;

            for (int row = 0; row < ch; row++)
            {
                int destY = Height - 1 - (yFromTop + row);
                if (destY < 0 || destY >= Height) continue;

                // The coverage texture was written bottom-up by NativeText.
                int srcRow = ch - 1 - row;

                for (int col = 0; col < cw; col++)
                {
                    int destX = x + col;
                    if (destX < 0 || destX >= Width) continue;

                    float a = src[srcRow * cw + col].a / 255f;
                    if (a <= 0.004f) continue;

                    int d = destY * Width + destX;
                    dest[d].r = (byte)(tint.r * 255f * a + dest[d].r * (1f - a));
                    dest[d].g = (byte)(tint.g * 255f * a + dest[d].g * (1f - a));
                    dest[d].b = (byte)(tint.b * 255f * a + dest[d].b * (1f - a));
                }
            }
        }

        static void DrawRule(Color32[] pixels, int yFromTop, int thickness, Color colour,
            float alpha, int width)
        {
            int x0 = (Width - width) / 2;
            for (int row = 0; row < thickness; row++)
            {
                int y = Height - 1 - (yFromTop + row);
                if (y < 0 || y >= Height) continue;
                for (int x = x0; x < x0 + width && x < Width; x++)
                {
                    int d = y * Width + x;
                    pixels[d].r = (byte)(colour.r * 255f * alpha + pixels[d].r * (1f - alpha));
                    pixels[d].g = (byte)(colour.g * 255f * alpha + pixels[d].g * (1f - alpha));
                    pixels[d].b = (byte)(colour.b * 255f * alpha + pixels[d].b * (1f - alpha));
                }
            }
        }

        static string FirstLines(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var kept = new System.Text.StringBuilder();
            int taken = 0;
            foreach (var line in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (taken++ >= count) break;
                if (kept.Length > 0) kept.Append('\n');
                kept.Append(line.Trim());
            }
            return kept.ToString();
        }

        static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            int cut = text.LastIndexOf(' ', Mathf.Min(max, text.Length - 1));
            if (cut < max / 2) cut = max;
            return text[..cut].TrimEnd(',', ';', '.', ' ') + "…";
        }

        // ------------------------------------------------------------------

        static bool ShareImage(byte[] png, string fileName, string caption)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var sharer = new AndroidJavaClass("com.gita.share.Sharer");
                if (!sharer.CallStatic<bool>("canShareImage")) return false;
                return sharer.CallStatic<bool>("shareImage", png, fileName, caption);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Image share unavailable: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        static void ShareText(string caption)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var sharer = new AndroidJavaClass("com.gita.share.Sharer");
                sharer.CallStatic<bool>("shareText", caption);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Text share unavailable: {e.Message}");
            }
#else
            Debug.Log($"[Gita] Share (no-op off Android):\n{caption}");
#endif
        }
    }
}
