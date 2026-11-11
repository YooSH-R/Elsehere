using System;
using System.IO;
using System.IO.Compression;
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

        /// <summary>템플릿(index.html)에서 휴대폰용 텍스처 데이터 파일 이름이 들어갈 자리. 빌드가 실제 이름으로 바꾼다.</summary>
        public const string MobileTextureDataToken = "A305_MOBILE_TEXTURE_DATA";

        /// <summary>
        /// 휴대폰 GPU는 PC용 DXT 텍스처를 읽지 못해 Unity가 불러올 때 CPU로 풀어 느리고 메모리를 많이 쓴다.
        /// 휴대폰용(ASTC)을 옆 임시 폴더에 빌드한 다음 PC용(프로젝트 설정 그대로)을 <paramref name="options"/> 자리에 빌드해,
        /// 휴대폰용 데이터 파일만 PC 빌드에 넣고 페이지가 고를 수 있게 그 이름을 적는다. 엔진 코드는 두 빌드가 같아야 한다.
        /// Unity는 다른 자리에 다시 빌드하면 앞선 빌드의 산출물을 지울 수 있어, 휴대폰용 결과는 Unity가 모르는 곳에 복사해 두고 PC용을 마지막에 빌드한다.
        /// </summary>
        /// <returns>PC용 빌드 결과</returns>
        public static BuildReport BuildBothTextureFormats(
            BuildPlayerOptions options,
            Func<BuildPlayerOptions, WebGLTextureSubtarget, BuildReport> buildPlayer
        )
        {
            string mobileLocation = options.locationPathName + "-astc";
            string keptMobileBuild = Path.Combine(
                Path.GetTempPath(),
                "A305WebGlAstc-" + Guid.NewGuid().ToString("N")
            );
            try
            {
                BuildPlayerOptions mobile = options;
                mobile.locationPathName = mobileLocation;
                buildPlayer(mobile, WebGLTextureSubtarget.ASTC);
                CopyFiles(Path.Combine(mobileLocation, "Build"), keptMobileBuild);
                BuildReport desktop = buildPlayer(options, WebGLTextureSubtarget.Generic);
                AddMobileTextureData(options.locationPathName, keptMobileBuild);
                return desktop;
            }
            finally
            {
                if (Directory.Exists(mobileLocation))
                    Directory.Delete(mobileLocation, true);
                if (Directory.Exists(keptMobileBuild))
                    Directory.Delete(keptMobileBuild, true);
            }
        }

        private static void CopyFiles(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        private static void AddMobileTextureData(string desktopLocation, string mobileBuild)
        {
            string desktopBuild = Path.Combine(desktopLocation, "Build");
            string mobileData = DataFile(mobileBuild);
            // 로더(loader.js)는 텍스처와 무관하고 빌드마다 wasm 크기 힌트가 달라질 수 있어, 엔진(wasm·framework)만 비교한다.
            foreach (string desktopFile in Directory.GetFiles(desktopBuild).Where(IsEngineFile))
            {
                string kind = FileKind(desktopFile);
                string mobileFile = Directory
                    .GetFiles(mobileBuild)
                    .SingleOrDefault(file => FileKind(file) == kind);
                if (
                    mobileFile == null
                    || !ReadContent(mobileFile).SequenceEqual(ReadContent(desktopFile))
                )
                {
                    throw new BuildFailedException(
                        $"The ASTC build's {kind} differs from the desktop build, so its data cannot share the desktop engine."
                    );
                }
            }

            string page = Path.Combine(desktopLocation, "index.html");
            string html = File.ReadAllText(page);
            if (Regex.Matches(html, MobileTextureDataToken).Count != 1)
            {
                throw new BuildFailedException(
                    $"index.html must have exactly one {MobileTextureDataToken} for the mobile texture data."
                );
            }

            string name = Path.GetFileName(mobileData);
            File.Copy(mobileData, Path.Combine(desktopBuild, name));
            File.WriteAllText(page, html.Replace(MobileTextureDataToken, name));
        }

        private static string DataFile(string build)
        {
            string[] data = Directory
                .GetFiles(build)
                .Where(file => FileKind(file).StartsWith("data", StringComparison.Ordinal))
                .ToArray();
            if (data.Length != 1)
                throw new BuildFailedException($"Expected one data file in {build}.");
            return data[0];
        }

        private static bool IsEngineFile(string file)
        {
            string kind = FileKind(file);
            return kind.StartsWith("wasm", StringComparison.Ordinal)
                || kind.StartsWith("framework", StringComparison.Ordinal);
        }

        // 해시 이름(abc123.wasm.gz)에서 해시를 뺀 종류(wasm.gz)
        private static string FileKind(string file)
        {
            string name = Path.GetFileName(file);
            return name.Substring(name.IndexOf('.') + 1);
        }

        // gzip 머리에는 압축한 시각 등이 들어갈 수 있어 푼 내용으로 비교한다.
        private static byte[] ReadContent(string file)
        {
            if (!file.EndsWith(".gz", StringComparison.Ordinal))
                return File.ReadAllBytes(file);
            using FileStream source = File.OpenRead(file);
            using var zip = new GZipStream(source, CompressionMode.Decompress);
            using var content = new MemoryStream();
            zip.CopyTo(content);
            return content.ToArray();
        }

        // 휴대폰용 빌드만 텍스처 형식을 바꾸고, 끝나면 원래 설정으로 돌린다.
        private static BuildReport BuildPlayerWithTextures(
            BuildPlayerOptions options,
            WebGLTextureSubtarget textures
        )
        {
            WebGLTextureSubtarget original = EditorUserBuildSettings.webGLBuildSubtarget;
            try
            {
                EditorUserBuildSettings.webGLBuildSubtarget = textures;
                options.subtarget = (int)textures;
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"WebGL build with {textures} textures failed. See the Unity build log."
                    );
                }
                return report;
            }
            finally
            {
                EditorUserBuildSettings.webGLBuildSubtarget = original;
            }
        }

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
                // 실패하면 BuildPlayerWithTextures가 예외로 멈춘다.
                BuildWithDesktopPipeline(() =>
                    BuildBothTextureFormats(options, BuildPlayerWithTextures)
                );

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
