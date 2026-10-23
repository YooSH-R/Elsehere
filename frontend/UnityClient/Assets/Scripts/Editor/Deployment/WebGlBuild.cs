using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace A305.EditorTools.Deployment
{
    public static class WebGlBuild
    {
        public static BuildPlayerOptions CreateOptions(
            string outputDirectory,
            EditorBuildSettingsScene[] scenes
        )
        {
            string outputPath = Path.GetFullPath(outputDirectory);
            string assetsPath = Path.GetFullPath(Application.dataPath);
            if (
                outputPath.Equals(assetsPath, StringComparison.OrdinalIgnoreCase)
                || outputPath.StartsWith(
                    assetsPath + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new ArgumentException(
                    "WebGL output must be outside Assets.",
                    nameof(outputDirectory)
                );
            }

            string[] enabledScenes = scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (enabledScenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "Enable the deployment scenes before building WebGL."
                );
            }

            return new BuildPlayerOptions
            {
                target = BuildTarget.WebGL,
                locationPathName = outputPath,
                scenes = enabledScenes,
                options = BuildOptions.None,
            };
        }

        public static void Build()
        {
            string outputDirectory = Environment.GetEnvironmentVariable("WEBGL_BUILD_DIRECTORY");
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException(
                    "Set WEBGL_BUILD_DIRECTORY to the build output directory."
                );
            }

            string commit = Environment.GetEnvironmentVariable("CI_COMMIT_SHA") ?? "local";
            if (commit != "local" && !Regex.IsMatch(commit, "^[0-9a-f]{40}$"))
            {
                throw new ArgumentException("CI_COMMIT_SHA must be the full Git commit SHA.");
            }

            BuildPlayerOptions options = CreateOptions(outputDirectory, EditorBuildSettings.scenes);
            WebGLCompressionFormat originalCompression = PlayerSettings.WebGL.compressionFormat;
            bool originalFallback = PlayerSettings.WebGL.decompressionFallback;
            try
            {
                // Match the gzip response headers in infra/nginx/web.conf.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = false;
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException("WebGL build failed. See the Unity build log.");
                }

                File.WriteAllText(
                    Path.Combine(options.locationPathName, "version.json"),
                    $"{{\"service\":\"web\",\"commit\":\"{commit}\",\"build_type\":\"unity-webgl\"}}\n"
                );
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = originalCompression;
                PlayerSettings.WebGL.decompressionFallback = originalFallback;
            }
        }
    }
}
