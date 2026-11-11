using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

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
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled)
                    continue;
                Scene scene = EditorSceneManager.OpenScene(entry.path);
                if (ConnectShadowQuality(scene))
                    EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        public static bool ConnectShadowQuality(Scene scene)
        {
            bool changed = false;
            foreach (Light light in BuilderTools.FindAllInScene<Light>(scene))
            {
                if (light.type != LightType.Directional || light.shadows == LightShadows.None)
                    continue;
                // URP는 이 연결이 없으면 파이프라인 설정과 관계없이 Low 필터를 쓴다.
                var data = light.GetComponent<UniversalAdditionalLightData>();
                if (data != null && data.softShadowQuality == SoftShadowQuality.UsePipelineSettings)
                    continue;
                if (data == null)
                    data = light.gameObject.AddComponent<UniversalAdditionalLightData>();
                data.softShadowQuality = SoftShadowQuality.UsePipelineSettings;
                EditorUtility.SetDirty(data);
                changed = true;
            }
            return changed;
        }
    }
}
