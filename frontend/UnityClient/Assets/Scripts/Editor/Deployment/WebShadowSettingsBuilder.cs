using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace A305.EditorTools.Deployment
{
    public static class WebShadowSettingsBuilder
    {
        [MenuItem("Tools/A305/Deployment/Soften Desktop Shadow Edges")]
        public static void Apply()
        {
            BuilderTools.ThrowIfPlaying();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Data/Player/WebDesktop_RPAsset.asset"
            );
            var settings = new SerializedObject(pipeline);
            // 움직이는 태양에서 경계가 튀는 현상을 줄이되 해상도·거리·캐스케이드 예산은 유지한다.
            settings.FindProperty("m_SoftShadowQuality").intValue = (int)SoftShadowQuality.Medium;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
