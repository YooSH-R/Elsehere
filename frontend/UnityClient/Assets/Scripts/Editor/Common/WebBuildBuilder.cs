using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace A305.EditorTools.Common
{
    public static class WebBuildBuilder
    {
        [MenuItem("Tools/A305/Web/Configure Responsive Template")]
        public static void Configure()
        {
            PlayerSettings.WebGL.template = "PROJECT:A305Responsive";
            AssetDatabase.SaveAssets();
            Debug.Log("반응형 Web 템플릿을 적용했습니다.");
        }

        [MenuItem("Tools/A305/Web/Build")]
        public static void Build()
        {
            A305.EditorTools.Player.TouchControlsBuilder.PrepareAssets();
            Configure();
            BuildReport report = BuildPipeline.BuildPlayer(
                EditorBuildSettings
                    .scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray(),
                "Builds/Web",
                BuildTarget.WebGL,
                BuildOptions.None
            );
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Web 빌드 실패: {report.summary.result}");
            }
        }
    }
}
