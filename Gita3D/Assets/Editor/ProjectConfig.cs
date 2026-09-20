using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
#if UNITY_ANDROID
using UnityEditor.Android;   // AndroidPlatformIconKind lives in the platform extension
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Gita.EditorTools
{
    /// <summary>
    /// Configures the render pipeline, the Android player and the single scene.
    /// Idempotent: safe to run again on an already-configured project.
    /// </summary>
    public static class ProjectConfig
    {
        const string SettingsDir = "Assets/Settings";
        const string RendererPath = SettingsDir + "/URP-Renderer.asset";
        const string PipelinePath = SettingsDir + "/URP-Pipeline.asset";
        const string ScenePath = "Assets/Scenes/Main.unity";

        public static void All()
        {
            ConfigureRenderPipeline();
            IncludeRuntimeShaders();
            ConfigurePlayer();
            ConfigureIconsAndSplash();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Config] Project configured.");
        }

        // ------------------------------------------------------------------
        // render pipeline
        // ------------------------------------------------------------------

        public static void ConfigureRenderPipeline()
        {
            Directory.CreateDirectory(SettingsDir);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            // Several of these have no public setter, so they are driven through
            // SerializedObject by property path instead.
            var so = new SerializedObject(pipeline);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_MSAA", 2);                  // 2x - a reasonable mobile default
            Set(so, "m_RenderScale", 1.0f);
            Set(so, "m_ShadowDistance", 85f);
            Set(so, "m_ShadowCascadeCount", 2);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_MainLightShadowmapResolution", 2048);
            Set(so, "m_SupportsCameraDepthTexture", true);   // depth of field needs it
            Set(so, "m_SupportsCameraOpaqueTexture", false);
            so.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            // Point every quality level at the same asset so the build cannot pick
            // up a level that still has the built-in pipeline.
            int original = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(original, false);

            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(renderer);
            Debug.Log("[Config] URP pipeline configured.");
        }

        static void Set(SerializedObject so, string path, bool value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.boolValue = value;
        }

        static void Set(SerializedObject so, string path, int value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.intValue = value;
        }

        static void Set(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.floatValue = value;
        }

        /// <summary>
        /// Materials are created at runtime via Shader.Find, so nothing references
        /// these shaders at build time and they would otherwise be stripped out.
        /// </summary>
        public static void IncludeRuntimeShaders()
        {
            string[] wanted =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Particles/Unlit",
                "Skybox/Panoramic",
                "Sprites/Default",
                "UI/Default",
            };

            var graphics = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (graphics == null)
            {
                Debug.LogWarning("[Config] GraphicsSettings.asset not found - shaders not pinned.");
                return;
            }

            var so = new SerializedObject(graphics);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                Debug.LogWarning("[Config] m_AlwaysIncludedShaders not found.");
                return;
            }

            var already = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var s = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (s != null) already.Add(s.name);
            }

            foreach (var name in wanted)
            {
                if (already.Contains(name)) continue;
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning($"[Config] Shader not found, cannot pin: {name}");
                    continue;
                }
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                Debug.Log($"[Config] Pinned shader: {name}");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // player
        // ------------------------------------------------------------------

        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Sanatan Studios";
            PlayerSettings.productName = "Bhagavad Gita 3D";
            PlayerSettings.applicationIdentifier = "com.sanatan.bhagavadgita3d";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // Linear colour is what makes the dawn grade read correctly.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);

            // Play requires a 64-bit binary; ARMv7 keeps older handsets installable.
            // x86-64 is not an option: Unity 6.6 dropped it, which also rules out
            // testing on a standard x86_64 Android emulator.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64
                                                       | AndroidArchitecture.ARMv7;

            // 26 is Unity's floor now; 24 is deprecated and becomes a build error soon.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
            });

            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.renderOutsideSafeArea = true; // we inset the UI ourselves
            PlayerSettings.Android.optimizedFramePacing = true;

            // Lets narration keep going while the app is unfocused. It does not survive
            // the screen turning off - that needs a foreground service.
            PlayerSettings.runInBackground = true;

            PlayerSettings.gpuSkinning = true;
            PlayerSettings.MTRendering = true;
            PlayerSettings.stripEngineCode = true;

            ForceLegacyInput();
            Debug.Log("[Config] Player settings applied.");
        }

        /// <summary>
        /// The Input System package is not used here, and StandaloneInputModule only
        /// works under the old handler. Pin it so a stray setting cannot leave the UI
        /// completely unresponsive on device.
        /// </summary>
        static void ForceLegacyInput()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null || settings.Length == 0) return;

            var so = new SerializedObject(settings[0]);
            var prop = so.FindProperty("activeInputHandler"); // 0 = old, 1 = new, 2 = both
            if (prop == null) return;

            if (prop.intValue != 0)
            {
                prop.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Config] Active input handler set to the legacy Input Manager.");
            }
        }

        // ------------------------------------------------------------------
        // launcher art and splash
        // ------------------------------------------------------------------

        public static void ConfigureIconsAndSplash()
        {
            var legacy = LoadIcon("icon_legacy");
            var back = LoadIcon("icon_adaptive_back");
            var fore = LoadIcon("icon_adaptive_fore");

            if (legacy == null || back == null || fore == null)
            {
                Debug.LogWarning("[Config] Icon art missing - keeping the default icon.");
            }
            else
            {
                // The plain icon covers older launchers and the store listing.
                PlayerSettings.SetIcons(NamedBuildTarget.Android,
                    new[] { legacy }, IconKind.Application);
#if UNITY_ANDROID
                // Adaptive is the only kind still supported; Round and Legacy are
                // obsolete and Unity derives them from this pair.
                ApplyIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, back, fore);
#endif
                Debug.Log("[Config] Launcher icons applied.");
            }

            // Keep the boot sequence dark so it runs into the dawn scene rather than
            // flashing white. On Personal the Unity logo itself cannot be removed.
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.043f, 0.063f, 0.149f);
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
        }

#if UNITY_ANDROID
        // AndroidPlatformIconKind.Adaptive is typed as the base PlatformIconKind,
        // and the icon API is keyed by BuildTargetGroup rather than NamedBuildTarget.
        static void ApplyIcons(NamedBuildTarget target, PlatformIconKind kind, params Texture2D[] textures)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            if (icons == null || icons.Length == 0) return;
            foreach (var icon in icons) icon.SetTextures(textures);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }
#endif

        static Texture2D LoadIcon(string name)
        {
            string path = $"Assets/Art/Icons/{name}.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) Debug.LogWarning($"[Config] Missing icon: {path}");
            return tex;
        }

        // ------------------------------------------------------------------
        // scene
        // ------------------------------------------------------------------

        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");

            // The scene is intentionally almost empty - AppRoot constructs the world,
            // the camera and the whole interface at runtime.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("AppRoot");
            root.AddComponent<Gita.App.AppRoot>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[Config] Scene written: {ScenePath}");
        }
    }
}
