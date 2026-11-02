using System;
using System.IO;
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
    }
}
