using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gita.EditorTools
{
    /// <summary>
    /// Authors the scene's materials as real assets under Resources.
    ///
    /// They used to be built at runtime with Shader.Find. That works in the editor and
    /// fails in a player: nothing references the shaders at build time, so variant
    /// stripping keeps only the opaque variant of each, and the additive dust motes
    /// came out as flat bright squares. An asset in Resources pulls its exact variants
    /// into the build.
    /// </summary>
    public static class MaterialSetup
    {
        const string Dir = "Assets/Resources/Materials";
        const string TexPath = "Assets/Resources/Materials/SoftDot.png";

        public static void Build()
        {
            Directory.CreateDirectory(Dir);
            var softDot = BuildSoftDot();

            Lit("Silhouette",     new Color(0.058f, 0.050f, 0.064f), 0.22f, cull: CullMode.Back);
            Lit("SilhouetteFlat", new Color(0.055f, 0.048f, 0.062f), 0.30f, cull: CullMode.Off);
            Lit("Ground",         new Color(0.42f, 0.31f, 0.21f),    0.05f, cull: CullMode.Back);
            Lit("Banner",         new Color(0.16f, 0.10f, 0.085f),   0.30f, cull: CullMode.Off);

            Additive("SunGlow", "Universal Render Pipeline/Unlit",
                new Color(1f, 0.72f, 0.38f, 1f), softDot);
            Additive("Dust", "Universal Render Pipeline/Particles/Unlit",
                Color.white, softDot);

            // Ridge cards: unlit, haze-tinted, opaque. Three depths.
            Unlit("Ridge0", new Color(0.30f, 0.22f, 0.18f));
            Unlit("Ridge1", new Color(0.22f, 0.15f, 0.13f));
            Unlit("Ridge2", new Color(0.13f, 0.09f, 0.10f));

            BuildSky();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Materials] Built material assets under " + Dir);
        }

        static Material Load(string name, string shaderName)
        {
            string path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Materials] Shader missing: {shaderName}");
                return null;
            }
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            return mat;
        }

        static void Lit(string name, Color baseColor, float smoothness, CullMode cull)
        {
            var m = Load(name, "Universal Render Pipeline/Lit");
            if (m == null) return;
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", (float)cull);
            m.doubleSidedGI = cull == CullMode.Off;
            EditorUtility.SetDirty(m);
        }

        static void Unlit(string name, Color baseColor)
        {
            var m = Load(name, "Universal Render Pipeline/Unlit");
            if (m == null) return;
            m.SetColor("_BaseColor", baseColor);
            EditorUtility.SetDirty(m);
        }

        static void Additive(string name, string shaderName, Color tint, Texture2D map)
        {
            var m = Load(name, shaderName);
            if (m == null) return;

            m.SetColor("_BaseColor", tint);
            if (map != null && m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", map);
                m.SetTextureScale("_BaseMap", Vector2.one);
                m.SetTextureOffset("_BaseMap", Vector2.zero);
            }

            // Transparent, additive.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;

            // The enabled keywords on a material asset are what the build collects
            // variants from, so _SURFACE_TYPE_TRANSPARENT above is the part that
            // actually matters - it is why this is an asset and not a runtime material.
            EditorUtility.SetDirty(m);
        }

        static void BuildSky()
        {
            var m = Load("DawnSky", "Skybox/Panoramic");
            if (m == null) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Resources/Sky/kurukshetra_dawn.hdr");
            if (tex == null)
            {
                Debug.LogWarning("[Materials] Dawn HDRI not found.");
                return;
            }
            m.SetTexture("_MainTex", tex);
            m.SetFloat("_Mapping", 1f);     // latitude-longitude
            m.SetFloat("_ImageType", 0f);   // 360 degrees
            m.SetFloat("_Exposure", 0.42f);
            m.SetFloat("_Rotation", 200f);
            m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(m);
        }

        /// <summary>Radial falloff sprite for glare and dust motes.</summary>
        static Texture2D BuildSoftDot()
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / r);
                float a = 1f - d;
                a = a * a * (3f - 2f * a); // smoothstep
                a *= a;                     // tighten the core
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();

            File.WriteAllBytes(TexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        }
    }
}
