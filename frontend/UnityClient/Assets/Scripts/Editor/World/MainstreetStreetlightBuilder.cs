using System.Linq;
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
        private const string TownProduction = "Assets/Prefabs/World/TownProduction/";

        // 가로수 잎에 파묻힌 가로등과 옮길 자리(같은 보도, 가로수 중심에서 3m 넘게). 두 프리팹은 손으로 다듬은 배치라 이 가로등만 옮긴다.
        private static readonly (string prefab, float x, float fromZ, float toZ)[] BuriedLamps =
        {
            (TownProduction + "StreetFurnishing.prefab", -3.45f, -27f, -24f),
            (TownProduction + "StreetFurnishing.prefab", 3.45f, -27f, -24f),
            (TownProduction + "NorthStreetDressing.prefab", -3.45f, 35f, 37.5f),
            (TownProduction + "NorthStreetDressing.prefab", 3.45f, 35f, 31.5f),
        };

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

        [MenuItem("Tools/A305/World/Move Streetlights Out Of Trees")]
        public static void MoveOutOfTrees()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (var prefab in BuriedLamps.GroupBy(lamp => lamp.prefab))
                BuilderTools.UpdatePrefabIfExists(
                    prefab.Key,
                    root =>
                    {
                        foreach (var lamp in prefab)
                            Move(root.transform, lamp.x, lamp.fromZ, lamp.toZ);
                    }
                );
            AssetDatabase.SaveAssets();
        }

        // 이미 옮겼으면 그 자리에 가로등이 없어 그대로 둔다.
        private static void Move(Transform root, float x, float fromZ, float toZ)
        {
            Transform lamp = root.Cast<Transform>()
                .FirstOrDefault(child =>
                    child.name == "StreetLampV2"
                    && Mathf.Abs(child.localPosition.x - x) < .05f
                    && Mathf.Abs(child.localPosition.z - fromZ) < .05f
                );
            if (lamp != null)
                lamp.localPosition = new Vector3(x, lamp.localPosition.y, toZ);
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
