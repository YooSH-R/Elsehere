using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using A305.EditorTools.Deployment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace A305.Deployment.Tests
{
    public class WebGlBuildTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void WebBuildUsesDesktopPipelineOnlyDuringBuild(bool buildFails)
        {
            int originalQualityLevel = QualitySettings.GetQualityLevel();
            int desktopLevel = Array.IndexOf(QualitySettings.names, "PC");
            int mobileLevel = Array.IndexOf(QualitySettings.names, "Mobile");
            RenderPipelineAsset editorDesktopPipeline = QualitySettings.GetRenderPipelineAssetAt(
                desktopLevel
            );
            RenderPipelineAsset mobilePipeline = QualitySettings.GetRenderPipelineAssetAt(
                mobileLevel
            );
            RenderPipelineAsset webDesktopPipeline =
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                    "Assets/Data/Player/WebDesktop_RPAsset.asset"
                );
            Assert.That(webDesktopPipeline, Is.Not.Null);

            var buildError = new InvalidOperationException("Build failed");
            BuildReport BuildPlayer()
            {
                Assert.That(
                    QualitySettings.GetRenderPipelineAssetAt(desktopLevel),
                    Is.SameAs(webDesktopPipeline)
                );
                Assert.That(
                    QualitySettings.GetRenderPipelineAssetAt(mobileLevel),
                    Is.SameAs(mobilePipeline)
                );
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(originalQualityLevel));
                if (buildFails)
                    throw buildError;
                return null;
            }

            if (buildFails)
                Assert.That(
                    Assert.Throws<InvalidOperationException>(() =>
                        WebGlBuild.BuildWithDesktopPipeline(BuildPlayer)
                    ),
                    Is.SameAs(buildError)
                );
            else
                WebGlBuild.BuildWithDesktopPipeline(BuildPlayer);

            Assert.That(
                QualitySettings.GetRenderPipelineAssetAt(desktopLevel),
                Is.SameAs(editorDesktopPipeline)
            );
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(originalQualityLevel));
        }

        // 휴대폰 GPU는 PC용 DXT 텍스처를 읽지 못해 Unity가 불러올 때 CPU로 풀어 느리고 메모리를 많이 쓴다.
        // 휴대폰용(ASTC)을 옆 임시 폴더에 빌드한 다음 PC용(지금 설정)을 빌드해, 휴대폰용 데이터만 PC 빌드에 넣고 페이지에 그 이름을 적는다.
        [TestCase(false)]
        [TestCase(true)]
        public void BuildsMobileThenDesktopTextures_AndAddsOnlyTheMobileData(bool mobileBuildFails)
        {
            string root = CreateTemporaryDirectory();
            string output = Path.Combine(root, "webgl");
            var calls = new List<(string Location, WebGLTextureSubtarget Textures)>();
            BuildReport BuildPlayer(BuildPlayerOptions options, WebGLTextureSubtarget textures)
            {
                calls.Add((options.locationPathName, textures));
                bool mobile = textures == WebGLTextureSubtarget.ASTC;
                if (mobile && mobileBuildFails)
                    throw new BuildFailedException("ASTC build failed");
                WriteFakeBuild(
                    options.locationPathName,
                    mobile ? "mobile123" : "desktop456",
                    mobile ? "astc textures" : "dxt textures",
                    "same engine"
                );
                return null;
            }

            try
            {
                var options = new BuildPlayerOptions { locationPathName = output };
                if (mobileBuildFails)
                    Assert.Throws<BuildFailedException>(() =>
                        WebGlBuild.BuildBothTextureFormats(options, BuildPlayer)
                    );
                else
                    WebGlBuild.BuildBothTextureFormats(options, BuildPlayer);

                Assert.That(calls[0].Textures, Is.EqualTo(WebGLTextureSubtarget.ASTC));
                Assert.That(calls[0].Location, Is.Not.EqualTo(output));
                Assert.That(
                    Directory.Exists(calls[0].Location),
                    Is.False,
                    "휴대폰용 임시 빌드는 지운다."
                );
                if (mobileBuildFails)
                {
                    Assert.That(calls, Has.Count.EqualTo(1), "휴대폰용이 실패하면 멈춘다.");
                    return;
                }

                Assert.That(calls[1].Textures, Is.EqualTo(WebGLTextureSubtarget.Generic));
                Assert.That(calls[1].Location, Is.EqualTo(output));

                Assert.That(
                    File.ReadAllText(Path.Combine(output, "Build", "mobile123.data.gz")),
                    Is.EqualTo("astc textures")
                );
                Assert.That(
                    Directory.GetFiles(Path.Combine(output, "Build"), "*.wasm.gz"),
                    Has.Length.EqualTo(1),
                    "엔진 코드는 PC 빌드 것 하나만 둔다."
                );
                string page = File.ReadAllText(Path.Combine(output, "index.html"));
                Assert.That(page, Does.Contain("\"mobile123.data.gz\""));
                Assert.That(page, Does.Not.Contain(WebGlBuild.MobileTextureDataToken));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        // 실제 Unity 빌드는 다른 자리에 다시 빌드하면 앞선 빌드의 Build 폴더를 지웠다(증분 빌드의 이전 산출물 정리).
        // 앞선 빌드 결과가 사라져도 PC 빌드에 휴대폰용 데이터가 들어가야 한다.
        [Test]
        public void MobileTextureData_SurvivesTheNextBuildCleaningEarlierOutput()
        {
            string root = CreateTemporaryDirectory();
            string output = Path.Combine(root, "webgl");
            var built = new List<string>();
            BuildReport BuildPlayer(BuildPlayerOptions options, WebGLTextureSubtarget textures)
            {
                foreach (string earlier in built)
                    Directory.Delete(Path.Combine(earlier, "Build"), true);
                built.Add(options.locationPathName);
                bool mobile = textures == WebGLTextureSubtarget.ASTC;
                WriteFakeBuild(
                    options.locationPathName,
                    mobile ? "mobile123" : "desktop456",
                    mobile ? "astc textures" : "dxt textures",
                    "same engine"
                );
                return null;
            }

            try
            {
                WebGlBuild.BuildBothTextureFormats(
                    new BuildPlayerOptions { locationPathName = output },
                    BuildPlayer
                );

                Assert.That(
                    File.ReadAllText(Path.Combine(output, "Build", "mobile123.data.gz")),
                    Is.EqualTo("astc textures")
                );
                Assert.That(
                    File.ReadAllText(Path.Combine(output, "Build", "desktop456.data.gz")),
                    Is.EqualTo("dxt textures")
                );
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        // 데이터만 바꿔 쓰므로 두 빌드의 엔진 코드가 다르면 섞지 않고 빌드를 멈춘다.
        [Test]
        public void MobileTextureData_IsRejectedWhenItsEngineCodeDiffers()
        {
            string root = CreateTemporaryDirectory();
            string output = Path.Combine(root, "webgl");
            BuildReport BuildPlayer(BuildPlayerOptions options, WebGLTextureSubtarget textures)
            {
                bool mobile = textures == WebGLTextureSubtarget.ASTC;
                WriteFakeBuild(
                    options.locationPathName,
                    mobile ? "mobile123" : "desktop456",
                    "textures",
                    mobile ? "mobile engine" : "desktop engine"
                );
                return null;
            }

            try
            {
                Assert.Throws<BuildFailedException>(() =>
                    WebGlBuild.BuildBothTextureFormats(
                        new BuildPlayerOptions { locationPathName = output },
                        BuildPlayer
                    )
                );
                Assert.That(
                    File.Exists(Path.Combine(output, "Build", "mobile123.data.gz")),
                    Is.False
                );
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        // 실제 두 빌드는 로더(loader.js)의 wasm 크기 힌트만 달랐다. 로더는 텍스처와 무관하므로 엔진(wasm·framework)만 같으면 된다.
        [Test]
        public void MobileTextureData_IsAddedWhenOnlyTheLoaderDiffers()
        {
            string root = CreateTemporaryDirectory();
            string output = Path.Combine(root, "webgl");
            BuildReport BuildPlayer(BuildPlayerOptions options, WebGLTextureSubtarget textures)
            {
                bool mobile = textures == WebGLTextureSubtarget.ASTC;
                WriteFakeBuild(
                    options.locationPathName,
                    mobile ? "mobile123" : "desktop456",
                    "textures",
                    "same engine"
                );
                File.WriteAllText(
                    Path.Combine(
                        options.locationPathName,
                        "Build",
                        (mobile ? "mobile123" : "desktop456") + ".loader.js"
                    ),
                    mobile ? "wasmFileSize:62983135" : "wasmFileSize:0"
                );
                return null;
            }

            try
            {
                WebGlBuild.BuildBothTextureFormats(
                    new BuildPlayerOptions { locationPathName = output },
                    BuildPlayer
                );
                Assert.That(
                    File.Exists(Path.Combine(output, "Build", "mobile123.data.gz")),
                    Is.True
                );
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void WebTemplateChoosesTheDataFileTheBuildNames()
        {
            // 빌드가 바꿀 자리는 한 곳뿐이고, 로더 설정의 데이터 주소가 그 이름과 PC용 이름 중에서 고른다.
            string page = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/index.html");

            Assert.That(
                Regex.Matches(page, WebGlBuild.MobileTextureDataToken).Count,
                Is.EqualTo(1)
            );
            Assert.That(
                page,
                Does.Contain(
                    "chooseTextureData?.(\"{{{ DATA_FILENAME }}}\", \""
                        + WebGlBuild.MobileTextureDataToken
                        + "\")"
                )
            );
        }

        private static string CreateTemporaryDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "A305WebGlBuildTests-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(directory);
            return directory;
        }

        // Unity WebGL 빌드처럼 index.html과 Build 폴더(해시 이름의 로더·엔진 코드·데이터)를 만든다. .gz는 실제 gzip이다.
        private static void WriteFakeBuild(string location, string name, string data, string engine)
        {
            string build = Path.Combine(location, "Build");
            Directory.CreateDirectory(build);
            File.WriteAllText(
                Path.Combine(location, "index.html"),
                "dataUrl: buildUrl + \"/\" + chooseTextureData(\""
                    + name
                    + ".data.gz\", \""
                    + WebGlBuild.MobileTextureDataToken
                    + "\"),"
            );
            File.WriteAllText(Path.Combine(build, name + ".data.gz"), data);
            File.WriteAllText(Path.Combine(build, name + ".loader.js"), "loader");
            WriteGzip(Path.Combine(build, name + ".wasm.gz"), engine + " wasm");
            WriteGzip(Path.Combine(build, name + ".framework.js.gz"), engine + " framework");
        }

        // 같은 내용이어도 압축할 때마다 머리 부분(시각 등)이 달라질 수 있어 이름·바이트가 아니라 푼 내용으로 비교하는지 본다.
        private static void WriteGzip(string path, string text)
        {
            using FileStream file = File.Create(path);
            using var zip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            zip.Write(bytes, 0, bytes.Length);
        }

        [Test]
        public void WebDesktopBuildKeepsTheOriginalWebRenderingBudget()
        {
            int selectedLevel = QualitySettings.GetQualityLevel();
            int desktopLevel = Array.IndexOf(QualitySettings.names, "PC");
            int mobileLevel = Array.IndexOf(QualitySettings.names, "Mobile");
            try
            {
                QualitySettings.SetQualityLevel(mobileLevel);
                var webBudget = ReadRenderingBudget();
                QualitySettings.SetQualityLevel(desktopLevel);
                var editorBudget = ReadRenderingBudget();

                WebGlBuild.BuildWithDesktopPipeline(() =>
                {
                    Assert.That(ReadRenderingBudget(), Is.EqualTo(webBudget));
                    return null;
                });

                Assert.That(ReadRenderingBudget(), Is.EqualTo(editorBudget));
            }
            finally
            {
                QualitySettings.SetQualityLevel(selectedLevel);
            }
        }

        private static (
            float LodBias,
            SkinWeights SkinWeights,
            AnisotropicFiltering AnisotropicFiltering,
            ShadowmaskMode ShadowmaskMode
        ) ReadRenderingBudget() =>
            (
                QualitySettings.lodBias,
                QualitySettings.skinWeights,
                QualitySettings.anisotropicFiltering,
                QualitySettings.shadowmaskMode
            );

        [Test]
        public void BuildUsesEnabledScenesInTheConfiguredOrder()
        {
            EditorBuildSettingsScene[] scenes =
            {
                new EditorBuildSettingsScene("Assets/Scenes/Production/HomeScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false),
                new EditorBuildSettingsScene("Assets/Scenes/Production/TownScene.unity", true),
            };

            BuildPlayerOptions options = WebGlBuild.CreateOptions(
                Path.Combine(Path.GetTempPath(), "a305-webgl-build-test"),
                scenes
            );

            Assert.That(options.target, Is.EqualTo(BuildTarget.WebGL));
            Assert.That(
                options.scenes,
                Is.EqualTo(
                    new[]
                    {
                        "Assets/Scenes/Production/HomeScene.unity",
                        "Assets/Scenes/Production/TownScene.unity",
                    }
                )
            );
        }

        [Test]
        public void BuildRejectsAnEmptySceneList()
        {
            Assert.Throws<InvalidOperationException>(() =>
                WebGlBuild.CreateOptions(
                    Path.Combine(Path.GetTempPath(), "a305-webgl-build-test"),
                    Array.Empty<EditorBuildSettingsScene>()
                )
            );
        }

        [Test]
        public void BuildRejectsOutputInsideProjectAssets()
        {
            Assert.Throws<ArgumentException>(() =>
                WebGlBuild.CreateOptions(
                    "Assets/BuildOutput",
                    new[]
                    {
                        new EditorBuildSettingsScene(
                            "Assets/Scenes/Production/HomeScene.unity",
                            true
                        ),
                    }
                )
            );
        }

        [Test]
        public void WebTemplateUsesServiceNameAsTabTitle()
        {
            // Product Name(UnityClient)이 브라우저 탭과 화면 읽기 프로그램에 보이지 않게 서비스 이름을 직접 쓴다.
            // 로더 설정의 productName은 브라우저 저장소 이름에 쓰이므로 그대로 둔다.
            string page = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/index.html");

            Assert.That(page, Does.Contain("<title>Elsehere</title>"));
            Assert.That(page, Does.Contain("aria-label=\"Elsehere\""));
        }

        [Test]
        public void TypingOnAPhone_DoesNotZoomThePage()
        {
            // 휴대폰에서는 입력 플러그인(WebGLInput)이 글자 크기 5vh인 HTML 입력칸을 띄워, 가로 휴대폰에서는 16px보다 작아진다.
            // 아이폰 사파리는 16px보다 작은 입력칸에 포커스가 가면 페이지를 확대하고, 게임 화면은 손가락 제스처를 게임이 받으므로
            // (touch-action: none) 다시 줄일 수 없다. 페이지 확대를 막고 입력칸 글자를 16px 이상으로 둔다.
            string page = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/index.html");
            string style = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/style.css");

            Assert.That(page, Does.Contain("maximum-scale=1"));
            Assert.That(style, Does.Contain("font-size: max(16px, 5vh) !important"));
        }

        [Test]
        public void UnitySplashScreenIsHidden()
        {
            // 게임 파일을 받는 동안 웹 로딩 화면이 소개 장면을 보여 주므로, 다 받은 뒤 Unity 시작 로고로 로그인을 더 늦추지 않는다.
            Assert.That(PlayerSettings.SplashScreen.show, Is.False);
        }

        [Test]
        public void IntroSlidesUseImagesInsideTheTemplate()
        {
            // 소개 장면 이미지가 빠지면 로딩 화면에 빈 장면이 보이므로 템플릿 폴더에 실제로 있는지 확인한다.
            const string templateFolder = "Assets/WebGLTemplates/A305Responsive/";
            string page = File.ReadAllText(templateFolder + "index.html");
            string[] images = Regex
                .Matches(page, "\"(intro/[^\"]+\\.jpg)\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToArray();

            Assert.That(images, Is.Not.Empty);
            Assert.That(images.Where(image => !File.Exists(templateFolder + image)), Is.Empty);
        }

        [Test]
        public void IntroCaptionsDoNotTieTheServiceToEnglish()
        {
            // 여러 언어로 넓힐 서비스이므로 로딩 화면 소개 문구에 한 언어를 적지 않는다.
            string page = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/index.html");
            string[] captions = Regex
                .Matches(page, "caption: \"([^\"]+)\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToArray();

            Assert.That(captions, Is.Not.Empty);
            Assert.That(captions.Where(caption => caption.Contains("영어")), Is.Empty);
        }

        [Test]
        public void PhonesHeldUpright_SeeALandscapeHintTheyCanDismiss()
        {
            // UI가 가로 화면에 맞춰져 있어, 휴대폰·태블릿(손가락 입력)을 세로로 들면 로딩 중에도 가로로 돌리라고 권한다.
            // PC 창이 세로로 길어도 띄우지 않고, 가로로 돌리면 저절로 사라진다.
            string page = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/index.html");
            string style = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/style.css");

            Assert.That(page, Does.Contain("id=\"orientation-hint\""));
            Assert.That(page, Does.Contain("가로 화면을 권장해요"));
            Assert.That(page, Does.Contain("id=\"orientation-hint-continue\""));
            Assert.That(
                style,
                Does.Contain("@media (orientation: portrait) and (pointer: coarse)")
            );
        }
    }
}
