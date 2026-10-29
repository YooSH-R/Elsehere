using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>선별한 CC0 원본을 수정하지 않고 URP 재질과 바닥 피벗을 가진 소품 프리팹을 만든다.</summary>
    public static class TinyTreatsPropsBuilder
    {
        private const string Source = "Assets/ThirdParty/TinyTreats/";
        private const string Art = "Assets/Art/World/TinyTreats/";
        internal const string Prefabs = "Assets/Prefabs/World/TinyTreats/";
        private static readonly List<string> Report = new List<string>();
        private static readonly (
            string pack,
            string model,
            string name,
            float meters,
            bool width
        )[] Selection =
        {
            ("BakeryInterior", "coffee_machine", "CoffeeMachine", .5f, false),
            ("BakeryInterior", "cash_register", "CashRegister", .25f, false),
            ("BakeryInterior", "coffee_cup_takeaway", "TakeawayCup", .18f, false),
            ("BakeryInterior", "mug_B", "Mug", .16f, false),
            ("BakeryInterior", "counter_table", "Counter", .9f, false),
            ("BakeryInterior", "display_case_short", "DisplayCase", .65f, false),
            ("BakeryInterior", "table_round_A", "CafeTable", .65f, false),
            ("BakeryInterior", "chair", "CafeChair", .8f, false),
            ("BakeryInterior", "serving_tray", "ServingTray", .45f, true),
            ("BakeryInterior", "plate", "Plate", .24f, true),
            ("BakeryInterior", "pastry_stand_A", "PastryStand", .35f, false),
            ("BakeryInterior", "pricing_card", "PriceCard", .12f, false),
            ("BakedGoods", "bread", "Bread", .3f, true),
            ("BakedGoods", "croissant", "Croissant", .18f, true),
            ("BakedGoods", "muffin", "Muffin", .12f, false),
            ("BakedGoods", "cake_chocolate", "ChocolateCake", .35f, true),
            ("BakedGoods", "donut", "Donut", .12f, true),
            ("HousePlants", "monstera_plant_medium_potted", "Monstera", .8f, false),
            ("HousePlants", "sansevieria_plant_medium_potted", "SnakePlant", .7f, false),
            ("HousePlants", "succulent_plant_pot_small", "Succulent", .22f, false),
            ("PrettyPark", "fountain", "Fountain", 2.6f, true),
            ("PrettyPark", "bench", "ParkBench", 1.6f, true),
            ("PrettyPark", "hedge_straight", "HedgeStraight", .9f, false),
            ("PrettyPark", "hedge_corner", "HedgeCorner", .9f, false),
            ("PrettyPark", "trashcan", "TrashCan", .85f, false),
        };

        [MenuItem("Tools/A305/World/Build And Preview Tiny Treats Props")]
        public static void BuildAndPreview()
        {
            if (!BuilderTools.CanBuild())
                return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Report.Clear();
            foreach (var item in Selection)
                Prepare(item.pack, item.model, item.name, item.meters, item.width);
            PrepareCounterDisplay();
            AssetDatabase.SaveAssets();
            BuildGallery();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            int frames = 0;
            EditorApplication.CallbackFunction capture = null;
            capture = () =>
            {
                if (++frames < 30)
                    return;
                EditorApplication.update -= capture;
                try
                {
                    CaptureGallery();
                    if (Application.isBatchMode)
                        EditorApplication.Exit(0);
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    if (Application.isBatchMode)
                        EditorApplication.Exit(1);
                }
            };
            EditorApplication.update += capture;
        }

        private static Material Palette(string pack, bool glass)
        {
            string path = Art + pack + (glass ? "Glass.mat" : "Palette.mat");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            BuilderTools.EnsureFolder(Art.TrimEnd('/'));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture(
                "_BaseMap",
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    Source + pack + "/tiny_treats_texture_1.png"
                )
            );
            material.SetColor("_BaseColor", new Color(1, 1, 1, glass ? .3f : 1));
            material.SetFloat("_Smoothness", glass ? .4f : .18f);
            material.SetFloat("_Metallic", 0);
            material.enableInstancing = true;
            if (glass)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Prepare(
            string pack,
            string modelName,
            string name,
            float meters,
            bool width
        )
        {
            string prefabPath = Prefabs + pack + "/" + name + ".prefab";
            string sourcePath = Source + pack + "/Models/" + modelName + ".fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (model == null)
                throw new InvalidOperationException("Missing source: " + sourcePath);
            Material palette = Palette(pack, false);
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(
                prefabPath,
                () =>
                {
                    var root = new GameObject(name);
                    GameObject visual = BuilderTools.InstantiatePrefab(
                        model,
                        root.transform,
                        Vector3.zero,
                        Quaternion.identity
                    );
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials = renderer
                            .sharedMaterials.Select(material =>
                                material != null
                                && material.name.ToLowerInvariant().Contains("glass")
                                    ? Palette(pack, true)
                                    : palette
                            )
                            .ToArray();
                    Bounds bounds = BoundsOf(root);
                    float originalSize = width ? bounds.size.x : bounds.size.y;
                    if (originalSize < .0001f)
                        throw new InvalidOperationException("Invalid model bounds: " + name);
                    visual.transform.localScale *= meters / originalSize;
                    bounds = BoundsOf(root);
                    visual.transform.localPosition -= new Vector3(
                        bounds.center.x,
                        bounds.min.y,
                        bounds.center.z
                    );
                    bounds = BoundsOf(root);
                    BoxCollider collider = root.AddComponent<BoxCollider>();
                    collider.center = bounds.center;
                    collider.size = bounds.size;
                    return root;
                }
            );
            GameObject check = Object.Instantiate(prefab);
            try
            {
                Bounds bounds = BoundsOf(check);
                foreach (Renderer renderer in check.GetComponentsInChildren<Renderer>())
                    if (
                        renderer.sharedMaterials.Any(material =>
                            material == null
                            || material.shader.name != "Universal Render Pipeline/Lit"
                        )
                    )
                        throw new InvalidOperationException("Missing URP material: " + name);
                if (Mathf.Abs(bounds.min.y) > .005f)
                    throw new InvalidOperationException("Pivot is not on the ground: " + name);
                int triangles = check
                    .GetComponentsInChildren<MeshFilter>()
                    .Sum(filter => filter.sharedMesh.triangles.Length / 3);
                Report.Add(
                    $"{pack}/{name}: {bounds.size.x:F3} x {bounds.size.y:F3} x {bounds.size.z:F3} m, {triangles} triangles; URP, ground pivot, BoxCollider OK."
                );
            }
            finally
            {
                Object.DestroyImmediate(check);
            }
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                throw new InvalidOperationException("No mesh renderers: " + root.name);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void PrepareCounterDisplay()
        {
            BuilderTools.CreatePrefabIfMissing(
                Prefabs + "CafeCounterDisplay.prefab",
                () =>
                {
                    var root = new GameObject("CafeCounterDisplay");
                    PlaceProp("BakeryInterior/Counter", root.transform, Vector3.zero);
                    PlaceProp(
                        "BakeryInterior/CoffeeMachine",
                        root.transform,
                        new Vector3(-.55f, .9f, -.2f)
                    );
                    PlaceProp(
                        "BakeryInterior/CashRegister",
                        root.transform,
                        new Vector3(.12f, .9f, -.25f)
                    );
                    PlaceProp(
                        "BakeryInterior/DisplayCase",
                        root.transform,
                        new Vector3(.55f, .9f, .15f)
                    );
                    PlaceProp(
                        "BakeryInterior/TakeawayCup",
                        root.transform,
                        new Vector3(-.14f, .9f, .29f)
                    );
                    PlaceProp(
                        "BakeryInterior/Plate",
                        root.transform,
                        new Vector3(-.53f, .9f, .35f)
                    );
                    PlaceProp(
                        "BakedGoods/Croissant",
                        root.transform,
                        new Vector3(-.53f, .932f, .35f)
                    );
                    PlaceProp(
                        "BakeryInterior/PriceCard",
                        root.transform,
                        new Vector3(.56f, .9f, -.41f)
                    );
                    return root;
                }
            );
        }

        private static void PlaceProp(string path, Transform parent, Vector3 position)
        {
            BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + path + ".prefab"),
                parent,
                position,
                Quaternion.identity
            );
        }

        private static void BuildGallery()
        {
            SceneGroups groups = BuilderTools.NewSceneWithGroups();
            int packIndex = 0;
            foreach (var pack in Selection.GroupBy(item => item.pack))
            {
                Transform group = BuilderTools.CreateEmpty(
                    pack.Key,
                    groups.Environment,
                    new Vector3(packIndex++ * 20, 0, 0),
                    Quaternion.identity
                );
                int index = 0;
                float spacing =
                    pack.Key == "PrettyPark" ? 3.2f
                    : pack.Key == "BakeryInterior" ? 1.8f
                    : .9f;
                foreach (var item in pack)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        Prefabs + item.pack + "/" + item.name + ".prefab"
                    );
                    BuilderTools.InstantiatePrefab(
                        prefab,
                        group,
                        new Vector3(index % 4 * spacing, 0, index / 4 * spacing),
                        Quaternion.identity
                    );
                    index++;
                }
            }
            Transform counter = BuilderTools.CreateEmpty(
                "CafeCounterDisplay",
                groups.Environment,
                new Vector3(80, 0, 0),
                Quaternion.identity
            );
            PlaceProp("CafeCounterDisplay", counter, Vector3.zero);
            Material floor = BuilderTools.GetOrCreateMaterial(
                Art.TrimEnd('/'),
                "GalleryFloor",
                new Color(.8f, .74f, .62f)
            );
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "GalleryFloor",
                groups.Environment,
                new Vector3(42, -.06f, 2),
                new Vector3(100, .1f, 14),
                floor
            );
            Light sun = groups.Lighting.GetComponentInChildren<Light>();
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            sun.color = new Color(1, .94f, .84f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.6f, .72f, .8f);
            RenderSettings.ambientEquatorColor = new Color(.6f, .55f, .45f);
            RenderSettings.ambientGroundColor = new Color(.3f, .27f, .23f);
            Camera camera = new GameObject("PropsCamera").AddComponent<Camera>();
            camera.transform.SetParent(groups.Systems, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.86f, .83f, .75f);
            camera.orthographic = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.transform.position = new Vector3(6, 6, -8);
            camera.transform.LookAt(new Vector3(2.5f, 0, 2));
            camera.orthographicSize = 3.4f;
            BuilderTools.SaveScene(groups.Scene, "Assets/Scenes/Sandbox/WorldPropsSandbox.unity");
        }

        private static void CaptureGallery()
        {
            string output = Path.GetFullPath(
                Application.dataPath + "/../../../artifacts/unity/free-props"
            );
            Directory.CreateDirectory(output);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Camera camera = BuilderTools.FindSingleInScene<Camera>(scene);
            camera.aspect = 16f / 9f;
            Transform environment = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform;
            foreach (
                string pack in Selection
                    .Select(item => item.pack)
                    .Distinct()
                    .Append("CafeCounterDisplay")
            )
            {
                Bounds bounds = BoundsOf(environment.Find(pack).gameObject);
                camera.transform.position =
                    bounds.center + new Vector3(-5, 5.5f, 8).normalized * 12;
                camera.transform.LookAt(bounds.center);
                camera.orthographicSize =
                    Mathf.Max(bounds.size.x / 2.5f, bounds.size.z / 1.8f, bounds.size.y) + .25f;
                var render = new RenderTexture(1600, 900, 24);
                var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    camera.targetTexture = render;
                    camera.Render();
                    RenderTexture.active = render;
                    texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, pack + ".png"), texture.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    render.Release();
                    Object.DestroyImmediate(render);
                    Object.DestroyImmediate(texture);
                }
            }
            File.WriteAllLines(Path.Combine(output, "verification.txt"), Report);
            Debug.Log("Tiny Treats: verified " + Report.Count + " reusable prefabs.");
        }
    }
}
