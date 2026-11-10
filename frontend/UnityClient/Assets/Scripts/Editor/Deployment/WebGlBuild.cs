using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace A305.EditorTools.Deployment
{
    public static class WebGlBuild
    {
        private const string DesktopPipelinePath = "Assets/Data/Player/WebDesktop_RPAsset.asset";

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
            bool originalHashedNames = PlayerSettings.WebGL.nameFilesAsHashes;
            try
            {
                // Match the gzip response headers in infra/nginx/web.conf.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = false;
                // A cached file from another release must not share the same URL.
                PlayerSettings.WebGL.nameFilesAsHashes = true;
                BuildReport report = BuildWithDesktopPipeline(() =>
                    BuildPipeline.BuildPlayer(options)
                );
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
                PlayerSettings.WebGL.nameFilesAsHashes = originalHashedNames;
            }
        }

        public static BuildReport BuildWithDesktopPipeline(Func<BuildReport> buildPlayer)
        {
            int desktopLevel = Array.IndexOf(QualitySettings.names, "PC");
            int mobileLevel = Array.IndexOf(QualitySettings.names, "Mobile");
            RenderPipelineAsset desktopPipeline =
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(DesktopPipelinePath);
            if (desktopLevel < 0 || mobileLevel < 0 || desktopPipeline == null)
            {
                throw new BuildFailedException(
                    $"WebGL requires the PC and Mobile quality levels and the pipeline at {DesktopPipelinePath}."
                );
            }

            int selectedLevel = QualitySettings.GetQualityLevel();
            RenderPipelineAsset originalPipeline = QualitySettings.GetRenderPipelineAssetAt(
                desktopLevel
            );
            RenderingBudget originalBudget = RenderingBudget.Read(desktopLevel);
            RenderingBudget webBudget = RenderingBudget.Read(mobileLevel);
            try
            {
                // The Web profile improves shadows while keeping the original Web mesh and texture costs.
                SetDesktopGraphics(desktopLevel, desktopPipeline, webBudget, selectedLevel);
                return buildPlayer();
            }
            finally
            {
                SetDesktopGraphics(desktopLevel, originalPipeline, originalBudget, selectedLevel);
            }
        }

        private static void SetDesktopGraphics(
            int desktopLevel,
            RenderPipelineAsset pipeline,
            RenderingBudget budget,
            int selectedLevel
        )
        {
            QualitySettings.SetQualityLevel(desktopLevel, applyExpensiveChanges: false);
            try
            {
                QualitySettings.renderPipeline = pipeline;
                budget.Apply();
            }
            finally
            {
                QualitySettings.SetQualityLevel(selectedLevel, applyExpensiveChanges: false);
            }
        }

        private struct RenderingBudget
        {
            private float lodBias;
            private SkinWeights skinWeights;
            private AnisotropicFiltering anisotropicFiltering;
            private ShadowmaskMode shadowmaskMode;

            public static RenderingBudget Read(int qualityLevel)
            {
                int selectedLevel = QualitySettings.GetQualityLevel();
                QualitySettings.SetQualityLevel(qualityLevel, applyExpensiveChanges: false);
                try
                {
                    return new RenderingBudget
                    {
                        lodBias = QualitySettings.lodBias,
                        skinWeights = QualitySettings.skinWeights,
                        anisotropicFiltering = QualitySettings.anisotropicFiltering,
                        shadowmaskMode = QualitySettings.shadowmaskMode,
                    };
                }
                finally
                {
                    QualitySettings.SetQualityLevel(selectedLevel, applyExpensiveChanges: false);
                }
            }

            public void Apply()
            {
                QualitySettings.lodBias = lodBias;
                QualitySettings.skinWeights = skinWeights;
                QualitySettings.anisotropicFiltering = anisotropicFiltering;
                QualitySettings.shadowmaskMode = shadowmaskMode;
            }
        }
    }
}
