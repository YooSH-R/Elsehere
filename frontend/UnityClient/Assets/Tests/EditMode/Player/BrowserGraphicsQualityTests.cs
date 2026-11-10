using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace A305.Player.Tests
{
    public class BrowserGraphicsQualityTests
    {
        private int originalQualityLevel;

        [SetUp]
        public void SetUp()
        {
            originalQualityLevel = QualitySettings.GetQualityLevel();
        }

        [TearDown]
        public void TearDown()
        {
            QualitySettings.SetQualityLevel(originalQualityLevel);
        }

        [Test]
        public void DesktopBrowserSwitchesFromMobileToPcQuality()
        {
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "Mobile"));

            BrowserGraphicsQuality.Apply(isMobileBrowser: false, isPrimaryPointerCoarse: false);

            Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()], Is.EqualTo("PC"));
            Assert.That(
                QualitySettings.renderPipeline,
                Is.SameAs(
                    AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                        "Assets/Settings/PC_RPAsset.asset"
                    )
                )
            );
        }

        [TestCase(true, true, TestName = "MobileBrowserKeepsWebDefaultQuality")]
        [TestCase(true, false, TestName = "MobileBrowserWithMouseKeepsWebDefaultQuality")]
        [TestCase(false, true, TestName = "DesktopUserAgentWithTouchPrimaryKeepsWebDefaultQuality")]
        public void MobileBrowserKeepsWebDefaultQuality(
            bool isMobileBrowser,
            bool isPrimaryPointerCoarse
        )
        {
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "Mobile"));
            RenderPipelineAsset mobilePipeline = QualitySettings.renderPipeline;

            BrowserGraphicsQuality.Apply(isMobileBrowser, isPrimaryPointerCoarse);

            Assert.That(
                QualitySettings.names[QualitySettings.GetQualityLevel()],
                Is.EqualTo("Mobile")
            );
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(mobilePipeline));
        }
    }
}
