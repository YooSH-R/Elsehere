using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using A305.EditorTools.Deployment;
using NUnit.Framework;
using UnityEditor;
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
        public void WebTemplateProvidesAnExplicitAnalyticsConsentChoice()
        {
            string page = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/index.html");

            Assert.That(page, Does.Contain("id=\"analytics-consent\""));
            Assert.That(page, Does.Contain("id=\"analytics-accept\""));
            Assert.That(page, Does.Contain("id=\"analytics-reject\""));
        }

        [Test]
        public void AnalyticsNoticeLinksThePrivacyPolicyVersionTheServerServes()
        {
            // 웹 Nginx는 /privacy/버전 주소만 백엔드로 넘기므로 버전 없는 /privacy는 404다.
            string page = File.ReadAllText("Assets/WebGLTemplates/A305Responsive/index.html");
            string catalog = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "../../..",
                    "backend/src/main/java/com/elsehere/auth/service/ConsentCatalog.java"
                )
            );
            string privacy = Regex.Match(catalog, "\"(/privacy/[^\"]+)\"").Groups[1].Value;
            string[] links = Regex
                .Matches(page, "href=\"(/privacy[^\"]*)\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToArray();

            Assert.That(privacy, Is.Not.Empty, "처리방침의 지금 주소");
            Assert.That(links, Is.EqualTo(new[] { privacy }));
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
            Assert.That(style, Does.Contain("@media (orientation: portrait) and (pointer: coarse)"));
        }
    }
}
