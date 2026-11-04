using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using A305.EditorTools.Deployment;
using NUnit.Framework;
using UnityEditor;

namespace A305.Deployment.Tests
{
    public class WebGlBuildTests
    {
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
    }
}
