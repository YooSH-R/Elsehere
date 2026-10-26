using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>벽등·쇼윈도의 발광과 근거리 조명을 공통 프리팹에 보관한다.</summary>
    public static class MainstreetFixtureLighting
    {
        internal const string FixtureLightName = "FixtureLight";
        private const string CafeParts = "Assets/Prefabs/World/CafeBlenderKit/Parts/";
        private const string WallLampPath = PrefabFolder + "/WallLampWarm.prefab";

        [MenuItem("Tools/A305/World/Apply And Verify All Fixture Lights")]
        public static void ApplyAndVerify()
        {
            if (!BuilderTools.CanBuild())
                return;
            PreparePrefabs(true);
            var scene = EditorSceneManager.OpenScene(TownMainstreetBuilder.ScenePath);
            ApplyToEnvironment(
                scene.GetRootGameObjects().Single(root => root.name == "Environment").transform
            );
            BuilderTools.SaveScene(scene, TownMainstreetBuilder.ScenePath);
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += MainstreetFixtureVerification.Verify;
        }

        internal static void PreparePrefabs(bool applyPreset = false)
        {
            ConfigureBulb(ArtFolder + "/LanternAmberV2.mat");
            ConfigureBulb(ArtFolder + "/PendantLight.mat");
            ConfigureBulb("Assets/Art/World/CafeBlenderKit/Materials/Glow.mat");
            Material window = AssetDatabase.LoadAssetAtPath<Material>(
                ArtFolder + "/WindowAmberGlow.mat"
            );
            MainstreetStreetlightBuilder.EnableBulbEmission(window);
            BuilderTools.CreatePrefabIfMissing(WallLampPath, BuildWallLamp);
            UpdateLight(WallLampPath, new Vector3(0, -.04f, .8f), .35f, 2.8f, applyPreset);
            UpdateLight(
                CafeParts + "WallLamp.prefab",
                new Vector3(0, -.12f, .85f),
                .35f,
                2.8f,
                applyPreset
            );
            UpdateLight(
                CafeParts + "WindowShop2m.prefab",
                new Vector3(-.3f, 1.4f, .65f),
                .1f,
                1.8f,
                applyPreset
            );
            UpdateLight(
                PrefabFolder + "/WarmShopWindowV2.prefab",
                new Vector3(-.36f, .25f, .65f),
                .1f,
                1.8f,
                applyPreset
            );
        }

        private static void ConfigureBulb(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader.name == "Elsehere/WarmLamp")
                return;
            material.shader = Shader.Find("Elsehere/WarmLamp");
            material.SetColor("_BaseColor", new UnityEngine.Color(1f, .73f, .3f));
            material.SetFloat("_CoreStrength", 1.2f);
            material.SetFloat("_EdgeStrength", .45f);
            EditorUtility.SetDirty(material);
        }

        private static void UpdateLight(
            string path,
            Vector3 position,
            float intensity,
            float range,
            bool applyPreset
        )
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || (!applyPreset && prefab.transform.Find(FixtureLightName) != null))
                return;
            BuilderTools.UpdatePrefabIfExists(
                path,
                root => AddLight(root.transform, position, intensity, range, applyPreset)
            );
        }

        internal static void AddLight(
            Transform parent,
            Vector3 position,
            float intensity,
            float range,
            bool applyPreset = false
        )
        {
            Transform existing = parent.Find(FixtureLightName);
            if (existing != null && !applyPreset)
                return;
            Transform source =
                existing != null ? existing : Group(FixtureLightName, parent, position);
            source.localPosition = position;
            Light light = BuilderTools.GetOrAddComponent<Light>(source.gameObject);
            light.type = LightType.Point;
            light.color = new UnityEngine.Color(1f, .88f, .65f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static GameObject BuildWallLamp()
        {
            var root = new GameObject("WallLampWarm");
            Transform lamp = root.transform;
            Block("Mount", lamp, Vector3.zero, new Vector3(.12f, .25f, .12f), Navy);
            Transform arm = Post("Arm", lamp, new Vector3(0, .1f, .15f), .025f, .35f, Navy);
            arm.localEulerAngles = new Vector3(65, 0, 0);
            SmoothBall(
                "Shade",
                lamp,
                new Vector3(0, .13f, .29f),
                new Vector3(.35f, .15f, .35f),
                Navy
            );
            SmoothBall(
                "Bulb",
                lamp,
                new Vector3(0, .04f, .29f),
                Vector3.one * .15f,
                AssetDatabase.LoadAssetAtPath<Material>(ArtFolder + "/PendantLight.mat")
            );
            AddLight(lamp, new Vector3(0, -.04f, .8f), .35f, 2.8f);
            return root;
        }

        internal static void PlaceWallLamp(Transform parent, float x, float y)
        {
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(WallLampPath, BuildWallLamp);
            BuilderTools.InstantiatePrefab(
                prefab,
                parent,
                new Vector3(x, y, .15f),
                Quaternion.identity
            );
        }

        internal static void ApplyToEnvironment(Transform environment)
        {
            // Older facades contain loose lamp meshes. Replace just those fixtures, preserving their pose.
            Transform[] oldLamps = environment
                .GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "WallLamp" && item.Find("Bulb") != null)
                .ToArray();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallLampPath);
            foreach (Transform old in oldLamps)
            {
                GameObject replacement = BuilderTools.InstantiatePrefab(
                    prefab,
                    old.parent,
                    old.localPosition,
                    old.localRotation
                );
                replacement.transform.localScale = old.localScale;
                Object.DestroyImmediate(old.gameObject);
            }
        }
    }
}
