#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Android build entry points.
    /// CI calls: Unity -batchmode -quit -projectPath . -executeMethod BuildTools.BuildScript.PerformBuild
    /// Editor:   Sim > Build > Android APK (debug / release)
    /// </summary>
    public static class BuildScript
    {
        const string DefaultOutputDir = "build/Android";

        [MenuItem("Sim/Build/Android APK (debug)", false, 40)]
        public static void BuildDebugMenu() => PerformBuild(true);

        [MenuItem("Sim/Build/Android APK (release)", false, 41)]
        public static void BuildReleaseMenu() => PerformBuild(false);

        /// <summary>Batch-mode entry point used by the GitHub Actions workflow.</summary>
        public static void PerformBuild()
        {
            bool debug = Environment.GetEnvironmentVariable("DARKNOTE_BUILD_DEBUG") != "0";
            PerformBuild(debug);
        }

        public static void PerformBuild(bool debug)
        {
            try
            {
                var catalog = ProjectSetup.EnsureRoster();
                ProjectSetup.EnsureScene(catalog);
                ProjectSetup.ApplyPlayerSettings();
                ApplySigning(debug);
                AssetDatabase.SaveAssets();

                string dir = Environment.GetEnvironmentVariable("DARKNOTE_OUTPUT_DIR");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(ProjectSetup.ProjectRoot, DefaultOutputDir);
                Directory.CreateDirectory(dir);

                string file = "DarkNote-" + ProjectSetup.VersionName + (debug ? "-debug" : "") + ".apk";
                string output = Path.Combine(dir, file);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = debug ? BuildOptions.Development : BuildOptions.None
                };

                Debug.Log($"DARK NOTE: building {(debug ? "debug" : "release")} APK -> {output}");
                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;

                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            Debug.LogError(msg.content);

                Debug.Log($"DARK NOTE: {summary.result}, {summary.totalErrors} errors, " +
                          $"{summary.totalWarnings} warnings, {summary.totalSize / (1024 * 1024)} MB -> {output}");

                Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("DARK NOTE build failed: " + e);
                Exit(1);
            }
        }

        static void ApplySigning(bool debug)
        {
            // Debug builds use Unity's generated debug keystore, which is what makes a sideloaded
            // test APK installable without any secrets.
            PlayerSettings.Android.useCustomKeystore = false;
            if (debug) return;

            string keystore = Environment.GetEnvironmentVariable("DARKNOTE_KEYSTORE_PATH");
            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
            {
                Debug.LogWarning("DARK NOTE: no release keystore supplied, falling back to debug signing.");
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("DARKNOTE_KEYSTORE_PASS") ?? "";
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("DARKNOTE_KEYALIAS") ?? "";
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("DARKNOTE_KEYALIAS_PASS") ?? "";
        }

        static void Exit(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
            else if (code != 0)
            {
                EditorUtility.DisplayDialog("DARK NOTE", "Build failed. See the Console for details.", "OK");
            }
        }
    }
}
#endif
