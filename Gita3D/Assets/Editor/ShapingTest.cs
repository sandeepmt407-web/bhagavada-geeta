using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Gita.EditorTools
{
    /// <summary>
    /// Renders a sheet of Devanagari test cases to a PNG so the shaping can actually
    /// be looked at. Devanagari needs matra reordering and conjunct formation; if the
    /// text engine does not do them, the Sanskrit will be subtly wrong everywhere,
    /// and that has to be caught before anything is built on top of it.
    /// </summary>
    public static class ShapingTest
    {
        // Each case is (label, text, what correct output looks like).
        static readonly (string id, string text, string expect)[] Cases =
        {
            ("plain",     "क ख ग",                    "three separate consonants"),
            ("i-matra",   "कि कि कि",                  "the i-hook sits BEFORE each ka"),
            ("ii-matra",  "की",                        "the ii-hook sits after ka"),
            ("conjunct",  "क्ष",                        "one ksha ligature, no visible halant"),
            ("reph",      "कर्म",                       "r rides as a hook above the ma"),
            ("halant",    "द्ध",                        "stacked ddha conjunct"),
            ("title",     "श्रीमद्भगवद्गीता",              "Shrimad Bhagavad Gita"),
            ("verse247",  "कर्मण्येवाधिकारस्ते",            "opening of 2.47"),
            ("verse11",   "धर्मक्षेत्रे कुरुक्षेत्रे",          "opening of 1.1"),
        };

        public static void Render()
        {
            const int width = 1100;
            const int rowHeight = 150;
            int height = rowHeight * (Cases.Length + 1);

            var camGo = new GameObject("ShapeCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.12f);
            cam.orthographic = true;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            cam.targetTexture = rt;

            var canvasGo = new GameObject("ShapeCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Resources/Fonts/NotoSerifDevanagari-Regular SDF.asset");
            var latin = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Resources/Fonts/Inter-Regular SDF.asset");

            if (font == null)
            {
                Debug.LogError("[ShapingTest] Devanagari font asset not found - run setup first.");
                return;
            }

            for (int i = 0; i < Cases.Length; i++)
            {
                var (id, text, expect) = Cases[i];
                float y = -(i + 0.5f) * rowHeight + height * 0.5f;

                Label(canvasGo.transform, latin, id, 22, new Color(0.55f, 0.6f, 0.75f),
                      new Vector2(20f, y + 42f), new Vector2(380f, 34f), TextAlignmentOptions.Left);

                Label(canvasGo.transform, latin, expect, 17, new Color(0.42f, 0.46f, 0.58f),
                      new Vector2(20f, y - 44f), new Vector2(520f, 30f), TextAlignmentOptions.Left);

                Label(canvasGo.transform, font, text, 62, Color.white,
                      new Vector2(430f, y), new Vector2(640f, 110f), TextAlignmentOptions.Left);
            }

            Label(canvasGo.transform, latin,
                  "Devanagari shaping check - TextMeshPro dynamic SDF", 26,
                  new Color(0.9f, 0.7f, 0.35f),
                  new Vector2(20f, height * 0.5f - rowHeight * 0.5f),
                  new Vector2(900f, 40f), TextAlignmentOptions.Left);

            Canvas.ForceUpdateCanvases();
            foreach (var t in canvasGo.GetComponentsInChildren<TextMeshProUGUI>())
                t.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();

            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            shot.Apply();
            RenderTexture.active = prev;

            Directory.CreateDirectory("../build");
            string outPath = Path.GetFullPath("../build/shaping-test.png");
            File.WriteAllBytes(outPath, shot.EncodeToPNG());
            Debug.Log($"[ShapingTest] Wrote {outPath}");

            cam.targetTexture = null;
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(shot);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        static void Label(Transform parent, TMP_FontAsset font, string text, float size,
            Color color, Vector2 pos, Vector2 boxSize, TextAlignmentOptions align)
        {
            var go = new GameObject("L", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = boxSize;
            rt.anchoredPosition = pos;

            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
