using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>공통 가로등의 외형을 보존하고 주변을 비추는 광원을 추가한다.</summary>
    public static class MainstreetStreetlightBuilder
    {
        internal const string LightName = "LanternLight";
        internal const string LampPath = PrefabFolder + "/StreetLampV2.prefab";

        [MenuItem("Tools/A305/World/Apply And Verify Streetlights")]
        public static void ApplyAndVerify()
        {
            if (!BuilderTools.CanBuild())
                return;
            PreparePrefab();
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += MainstreetStreetlightVerification.Verify;
        }

        internal static void PreparePrefab()
        {
            EnableBulbEmission(
                AssetDatabase.LoadAssetAtPath<Material>(ArtFolder + "/LanternAmberV2.mat")
            );
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LampPath);
            if (prefab == null || prefab.transform.Find(LightName) != null)
                return;
            BuilderTools.UpdatePrefabIfExists(LampPath, AddLightIfMissing);
        }

        internal static void EnableBulbEmission(Material material)
        {
            if (
                material == null
                || material.shader.name == "Elsehere/WarmLamp"
                || material.GetColor("_EmissionColor").maxColorComponent <= 0f
            )
                return;
            // URP requires an AnyEmissive flag to preserve the emission keyword on import.
            material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }

        internal static void AddLightIfMissing(GameObject root)
        {
            if (root.transform.Find(LightName) != null)
                return;
            Transform source = Group(LightName, root.transform, new Vector3(0f, 2.88f, 0f));
            Light light = source.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            ColorUtility.TryParseHtmlString("#FFE1B3", out UnityEngine.Color color);
            light.color = color;
            light.intensity = 4f;
            light.range = 5f;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            // No shadow maps per lamp: the opaque bulb would also block a light inside it.
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }
    }
}
