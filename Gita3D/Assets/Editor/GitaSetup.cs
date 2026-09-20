using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace Gita.EditorTools
{
    /// <summary>
    /// One-shot project configuration, driven from the command line. Everything the
    /// project needs that cannot live in a plain source file is created here.
    /// </summary>
    public static class GitaSetup
    {
        const string FontSrcDir = "Assets/Art/Fonts";
        const string FontOutDir = "Assets/Resources/Fonts";

        [MenuItem("Gita/Setup/1 - Import TMP Essentials")]
        public static void ImportTMPEssentials()
        {
            if (Directory.Exists("Assets/TextMesh Pro"))
            {
                Debug.Log("[Setup] TMP essentials already present.");
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            Debug.Log("[Setup] Imported TMP essential resources.");
        }

        [MenuItem("Gita/Setup/2 - Build Font Assets")]
        public static void BuildFontAssets()
        {
            Directory.CreateDirectory(FontOutDir);

            // Devanagari needs a large atlas: the script forms thousands of distinct
            // conjuncts, and they are rasterised on demand into this sheet.
            CreateFontAsset("NotoSerifDevanagari-Regular", 2048, 2048, 96);
            CreateFontAsset("NotoSansDevanagari-Regular", 2048, 2048, 96);
            CreateFontAsset("EBGaramond-Regular", 1024, 1024, 90);
            CreateFontAsset("Inter-Regular", 1024, 1024, 90);
            CreateFontAsset("Inter-SemiBold", 1024, 1024, 90);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] Font assets built.");
        }

        static void CreateFontAsset(string name, int atlasW, int atlasH, int pointSize)
        {
            string src = $"{FontSrcDir}/{name}.ttf";
            string dst = $"{FontOutDir}/{name} SDF.asset";

            if (File.Exists(dst))
            {
                Debug.Log($"[Setup] {name} SDF already exists - skipping.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(src);
            if (font == null)
            {
                Debug.LogError($"[Setup] Source font not found: {src}");
                return;
            }

            // Dynamic population: glyphs are added to the atlas as they are first used,
            // so the shipped asset stays small but any codepoint in the font can render.
            var asset = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: pointSize,
                atlasPadding: 8,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: atlasW,
                atlasHeight: atlasH,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (asset == null)
            {
                Debug.LogError($"[Setup] Failed to create font asset for {name}");
                return;
            }

            asset.name = $"{name} SDF";
            AssetDatabase.CreateAsset(asset, dst);

            // The atlas texture and material must live inside the asset, or they are
            // lost when the project is reopened.
            if (asset.atlasTextures != null)
            {
                foreach (var tex in asset.atlasTextures)
                {
                    if (tex == null) continue;
                    tex.name = $"{name} Atlas";
                    AssetDatabase.AddObjectToAsset(tex, asset);
                }
            }
            if (asset.material != null)
            {
                asset.material.name = $"{name} Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            EditorUtility.SetDirty(asset);
            Debug.Log($"[Setup] Created font asset: {dst}");
        }

        /// <summary>Entry point for -executeMethod: does the ordered setup in one pass.</summary>
        public static void All()
        {
            ImportTMPEssentials();
            BuildFontAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] Complete.");
        }
    }
}
