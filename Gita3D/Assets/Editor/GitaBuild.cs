using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Gita.EditorTools
{
    /// <summary>
    /// Command-line builds. Both targets share one configuration path so the APK and
    /// the AAB are the same binary in different containers.
    /// </summary>
    public static class GitaBuild
    {
        const string OutDir = "../build";
        const string ApkName = "BhagavadGita.apk";
        const string AabName = "BhagavadGita.aab";

        [MenuItem("Gita/Build/APK")]
        public static void BuildAPK() => Build(appBundle: false);

        [MenuItem("Gita/Build/AAB")]
        public static void BuildAAB() => Build(appBundle: true);

        /// <summary>
        /// A Windows desktop build, for looking at the app on this machine without a
        /// handset. Note that Devanagari falls back to TextMeshPro here - the platform
        /// shaper only exists on Android - so Sanskrit will be mis-shaped in this build.
        /// Everything else (scene, grade, camera, layout, navigation) is representative.
        /// </summary>
        [MenuItem("Gita/Build/Windows preview")]
        public static void BuildWindows()
        {
            const string outDir = "../build/windows";
            Directory.CreateDirectory(outDir);
            string output = Path.GetFullPath(Path.Combine(outDir, "BhagavadGita.exe"));

            // A tall window, so the portrait layout is laid out as it is on a phone.
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled).Select(s => s.path).ToArray();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                LogErrors(report);
                Debug.LogError($"[Build] Windows FAILED ({report.summary.result}).");
                return;
            }
            Debug.Log($"[Build] Windows OK -> {output} in {report.summary.totalTime.TotalSeconds:F0}s");
        }

        /// <summary>Builds both artifacts in one editor session.</summary>
        public static void BuildBoth()
        {
            Build(appBundle: false, exitOnFinish: false);
            Build(appBundle: true, exitOnFinish: false);
            Debug.Log("[Build] Both artifacts complete.");
        }

        static void Build(bool appBundle, bool exitOnFinish = false)
        {
            string label = appBundle ? "AAB" : "APK";
            Debug.Log($"[Build] Starting {label}...");

            ApplySigning();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[Build] Switching active build target to Android...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android);
            }

            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.development = false;
#if UNITY_ANDROID
            // Release builds ship no debug symbols; the old
            // EditorUserBuildSettings.androidCreateSymbols is deprecated.
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level =
                Unity.Android.Types.DebugSymbolLevel.None;
#endif

            Directory.CreateDirectory(OutDir);
            string output = Path.GetFullPath(Path.Combine(OutDir, appBundle ? AabName : ApkName));

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("[Build] No scenes are enabled in build settings.", exitOnFinish);
                return;
            }
            Debug.Log($"[Build] Scenes: {string.Join(", ", scenes)}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception e)
            {
                Fail($"[Build] {label} threw: {e}", exitOnFinish);
                return;
            }

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                LogErrors(report);
                Fail($"[Build] {label} FAILED ({summary.result}) - " +
                     $"{summary.totalErrors} errors, {summary.totalWarnings} warnings.", exitOnFinish);
                return;
            }

            long bytes = File.Exists(output) ? new FileInfo(output).Length : 0;
            Debug.Log($"[Build] {label} OK -> {output} ({bytes / 1024f / 1024f:F1} MB) " +
                      $"in {summary.totalTime.TotalSeconds:F0}s");

            if (exitOnFinish) EditorApplication.Exit(0);
        }

        static void ApplySigning()
        {
            string keystore = Environment.GetEnvironmentVariable("GITA_KEYSTORE")
                              ?? Path.GetFullPath("../keystore/gita.keystore");
            string pass = Environment.GetEnvironmentVariable("GITA_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("GITA_KEY_ALIAS") ?? "gita";

            if (string.IsNullOrEmpty(pass) || !File.Exists(keystore))
            {
                // Unsigned debug-key build still produces a valid APK for local testing;
                // an AAB for Play upload must be signed.
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[Build] No keystore or password supplied - using the debug key.");
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = pass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = pass;
            Debug.Log($"[Build] Signing with {keystore} (alias {alias}).");
        }

        static void LogErrors(BuildReport report)
        {
            foreach (var step in report.steps)
            foreach (var msg in step.messages)
            {
                if (msg.type is LogType.Error or LogType.Exception or LogType.Assert)
                    Debug.LogError($"[Build] {step.name}: {msg.content}");
            }
        }

        static void Fail(string message, bool exitOnFinish)
        {
            Debug.LogError(message);
            if (exitOnFinish) EditorApplication.Exit(1);
            else throw new BuildFailedExceptionMarker(message);
        }

        /// <summary>Lets BuildBoth stop at the first failure without killing the editor.</summary>
        class BuildFailedExceptionMarker : Exception
        {
            public BuildFailedExceptionMarker(string m) : base(m) { }
        }
    }
}
