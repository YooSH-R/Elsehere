using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>Blender 부품을 중첩 프리팹으로 조립한다. 이미 저장한 디자인은 덮어쓰지 않는다.</summary>
    public static class MainstreetCafeKitBuilder
    {
        private const string ArtPath = "Assets/Art/World/CafeBlenderKit";
        private const string PrefabPath = "Assets/Prefabs/World/CafeBlenderKit";
        private static readonly float[] Bays = { -3f, -1f, 1f, 3f };

        [Serializable]
        private sealed class PaletteEntry
        {
            public string name = "";
            public string hex = "";
        }

        [Serializable]
        private sealed class ModuleEntry
        {
            public string name = "";
            public float width_m = 0f;
            public float height_m = 0f;
            public float depth_m = 0f;
        }

        [Serializable]
        private sealed class Manifest
        {
            public PaletteEntry[] palette = Array.Empty<PaletteEntry>();
            public ModuleEntry[] modules = Array.Empty<ModuleEntry>();
        }

        internal static bool IsReady =>
            AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPath(2)) != null;

        [MenuItem("Tools/A305/World/Build And Verify Blender Cafe")]
        public static void BuildAndVerify()
        {
            if (!BuilderTools.CanBuild())
                return;
            PreparePrefabs();
            VerifyPrefabs();
            TownMainstreetVerification.BuildAndVerify();
        }

        private static void PreparePrefabs()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Manifest manifest = JsonUtility.FromJson<Manifest>(
                File.ReadAllText(ArtPath + "/Models/manifest.json")
            );
            foreach (PaletteEntry entry in manifest.palette)
                CreateMaterial(entry);
            foreach (ModuleEntry entry in manifest.modules)
                ImportPart(entry, manifest.palette);
            BuilderTools.CreatePrefabIfMissing(FloorPath("GroundFloor8m"), BuildGroundFloor);
            BuilderTools.CreatePrefabIfMissing(FloorPath("UpperFloor8m"), BuildUpperFloor);
            BuilderTools.CreatePrefabIfMissing(FloorPath("Roof8m"), BuildRoof);
            for (int floors = 1; floors <= 3; floors++)
            {
                int count = floors;
                BuilderTools.CreatePrefabIfMissing(BuildingPath(count), () => BuildBuilding(count));
            }
            AssetDatabase.SaveAssets();
        }

        private static void CreateMaterial(PaletteEntry entry)
        {
            string path = MaterialPath(entry.name);
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                return;
            if (!ColorUtility.TryParseHtmlString("#" + entry.hex, out UnityEngine.Color color))
                throw new InvalidOperationException("잘못된 색상: " + entry.name);
            Material material = BuilderTools.GetOrCreateMaterial(
                ArtPath + "/Materials",
                entry.name,
                color
            );
            if (entry.name == "Brick")
            {
                material.shader = Shader.Find("Elsehere/SoftMasonry");
                material.SetColor("_BaseColor", color);
                material.SetFloat("_BrickPattern", 1f);
                material.SetFloat("_Grain", .01f);
            }
            else
            {
                material.SetFloat("_Smoothness", entry.name == "Glass" ? .32f : .15f);
                material.SetFloat("_Metallic", 0f);
                if (entry.name == "Glow")
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 1.4f);
                }
            }
            EditorUtility.SetDirty(material);
        }

        private static void ImportPart(ModuleEntry entry, PaletteEntry[] palette)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PartPath(entry.name)) != null)
                return;
            string modelPath = ArtPath + "/Models/" + entry.name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null)
                throw new InvalidOperationException("FBX가 없습니다: " + modelPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.generateSecondaryUV = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (PaletteEntry color in palette)
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), color.name),
                    AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(color.name))
                );
            importer.SaveAndReimport();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            BuilderTools.CreatePrefabIfMissing(
                PartPath(entry.name),
                () =>
                {
                    var root = new GameObject(entry.name);
                    BuilderTools.InstantiatePrefab(
                        model,
                        root.transform,
                        Vector3.zero,
                        Quaternion.identity
                    );
                    AddPartColliders(root, entry);
                    return root;
                }
            );
        }

        private static void AddPartColliders(GameObject root, ModuleEntry entry)
        {
            if (entry.name.StartsWith("Wall"))
                ColliderBox(
                    root,
                    new Vector3(0, entry.height_m / 2, -.11f),
                    new Vector3(2, entry.height_m, .22f)
                );
            else if (entry.name == "Entrance4m")
            {
                foreach (int side in new[] { -1, 1 })
                    ColliderBox(root, new Vector3(side * 1.5f, 2, -.11f), new Vector3(1, 4, .22f));
                ColliderBox(root, new Vector3(0, 3.1f, -.11f), new Vector3(2, 1.8f, .22f));
            }
            else if (entry.name == "GardenTree4m")
                ColliderBox(root, new Vector3(0, 1, 0), new Vector3(.4f, 2, .4f));
            else if (entry.name == "FlowerPlanter1m")
                ColliderBox(root, new Vector3(0, .275f, 0), new Vector3(1.15f, .55f, .64f));
        }

        private static void ColliderBox(GameObject root, Vector3 center, Vector3 size)
        {
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private static GameObject BuildGroundFloor()
        {
            var root = new GameObject("GroundFloor8m");
            Transform parent = root.transform;
            Part("WallGround2m", parent, new Vector3(-3, 0, 0));
            Part("WallGround2m", parent, new Vector3(-1, 0, 0));
            Part("Entrance4m", parent, new Vector3(2, 0, 0));
            SideAndBackWalls(parent, "WallGround2m");
            Ring(parent, "Cornice2m", 3.7f);
            foreach (float x in new[] { -3f, -1f })
                Part("WindowShop2m", parent, new Vector3(x, 0, .02f));
            foreach (float x in Bays)
                Part("Awning2m", parent, new Vector3(x, 2.3f, .02f));
            foreach (float x in new[] { -3f, 0f, 3f })
                Part("WallLamp", parent, new Vector3(x, 3.48f, .08f));
            Block(
                "Floor",
                parent,
                new Vector3(0, -.05f, -4),
                new Vector3(8, .1f, 8),
                Material("Paver")
            );
            ColliderBox(root, new Vector3(0, -.05f, -4), new Vector3(8, .1f, 8));
            Block(
                "Counter",
                parent,
                new Vector3(1.4f, .5f, -2.4f),
                new Vector3(3, 1, .65f),
                Material("Wood")
            );
            var sign = Text("CafeSign", "CAFE", parent, new Vector3(0, 2.83f, .87f), .48f);
            sign.transform.localRotation = Quaternion.Euler(65, 180, 0);
            return root;
        }

        private static GameObject BuildUpperFloor()
        {
            var root = new GameObject("UpperFloor8m");
            Transform parent = root.transform;
            foreach (float x in Bays)
                Part("WallUpper2m", parent, new Vector3(x, 0, 0));
            SideAndBackWalls(parent, "WallUpper2m");
            Ring(parent, "Cornice2m", 2.7f);
            foreach (float x in new[] { -2f, 2f })
            {
                Part("WindowUpper2m", parent, new Vector3(x, 0, .02f));
                Part("WindowBox1p6m", parent, new Vector3(x, 0, .4f));
            }
            foreach (int side in new[] { -1, 1 })
            foreach (float z in new[] { -2f, -6f })
                Part("WindowUpper2m", parent, new Vector3(side * 4f, 0, z), side * 90f);
            return root;
        }

        private static GameObject BuildRoof()
        {
            var root = new GameObject("Roof8m");
            Ring(root.transform, "RoofEdge2m", 0);
            Block(
                "RoofSlab",
                root.transform,
                new Vector3(0, -.06f, -4),
                new Vector3(8, .12f, 8),
                Material("Trim")
            );
            return root;
        }

        private static void SideAndBackWalls(Transform parent, string part)
        {
            foreach (float bay in Bays)
            {
                Part(part, parent, new Vector3(bay, 0, -8), 180);
                foreach (int side in new[] { -1, 1 })
                    Part(part, parent, new Vector3(side * 4, 0, bay - 4), side * 90);
            }
        }

        private static void Ring(Transform parent, string part, float y)
        {
            foreach (float bay in Bays)
            {
                Part(part, parent, new Vector3(bay, y, 0));
                Part(part, parent, new Vector3(bay, y, -8), 180);
                foreach (int side in new[] { -1, 1 })
                    Part(part, parent, new Vector3(side * 4, y, bay - 4), side * 90);
            }
        }

        private static GameObject BuildBuilding(int floors)
        {
            var root = new GameObject("Cafe" + floors + "F");
            Instance(FloorPath("GroundFloor8m"), root.transform, Vector3.zero);
            for (int level = 1; level < floors; level++)
                Instance(
                    FloorPath("UpperFloor8m"),
                    root.transform,
                    new Vector3(0, 4 + (level - 1) * 3, 0)
                );
            Instance(FloorPath("Roof8m"), root.transform, new Vector3(0, 4 + (floors - 1) * 3, 0));
            return root;
        }

        internal static void Apply(Transform environment, TownBlockoutSettings settings)
        {
            if (!IsReady)
                return;
            if (
                !Mathf.Approximately(settings.ShopFootprintMeters, 8)
                || !Mathf.Approximately(settings.GroundFloorHeightMeters, 4)
                || !Mathf.Approximately(settings.UpperFloorHeightMeters, 3)
            )
            {
                Debug.LogWarning(
                    "Blender 카페는 폭 8m·1층 4m·상층 3m 규격입니다. 다른 규격은 기존 외관을 사용합니다."
                );
                return;
            }
            Transform cafe = environment.Find("BuildingFootprints/Cafe");
            foreach (Transform child in cafe)
                if (child.name != "DoorCenter")
                    child.gameObject.SetActive(false);
            Instance(BuildingPath(2), cafe, Vector3.zero);
            Transform props = Group("BlenderCafeSidewalk", cafe, Vector3.zero);
            Part("FlowerPlanter1m", props, new Vector3(-3.5f, 0, .65f));
            Part("FlowerPlanter1m", props, new Vector3(-.5f, 0, .65f));
            Part("CafeTable", props, new Vector3(-2, 0, 2.1f));
            Part("CafeChair", props, new Vector3(-2.65f, 0, 2.1f), 90);
            Part("CafeChair", props, new Vector3(-1.35f, 0, 2.1f), -90);
            Transform street = environment.Find("MainstreetArt");
            foreach (Transform tile in street)
            {
                Vector3 position = tile.localPosition;
                if (
                    tile.name == "SidewalkTile2m"
                    && position.x < -3
                    && position.z >= -13
                    && position.z <= -5
                )
                    tile.gameObject.SetActive(false);
            }
            Transform paving = Group("BlenderCafePaving", environment, Vector3.zero);
            foreach (float x in new[] { -4f, -6f })
                for (float z = -13; z <= -5; z += 2)
                    Part("Paving2m", paving, new Vector3(x, .115f, z));
            Transform gardens = environment.Find("StreetDepthAndGardens");
            foreach (Transform tree in gardens)
                if (Vector3.Distance(tree.localPosition, new Vector3(-6.4f, .1f, -15.5f)) < .01f)
                    tree.gameObject.SetActive(false);
            Part("GardenTree4m", paving, new Vector3(-6.4f, .1f, -15.5f));
        }

        private static void VerifyPrefabs()
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(
                File.ReadAllText(ArtPath + "/Models/manifest.json")
            );
            var results = new List<string>();
            foreach (ModuleEntry module in manifest.modules)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    PartPath(module.name)
                );
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    Bounds bounds = BoundsOf(instance);
                    Vector3 expected = new Vector3(module.width_m, module.height_m, module.depth_m);
                    if (Vector3.Distance(bounds.size, expected) > .015f)
                        throw new InvalidOperationException(
                            module.name + " FBX 축 또는 단위가 다릅니다: " + bounds.size
                        );
                    if (
                        instance
                            .GetComponentsInChildren<Renderer>()
                            .Any(r => r.sharedMaterials.Any(m => m == null))
                    )
                        throw new InvalidOperationException(module.name + " 머티리얼이 없습니다.");
                    results.Add(module.name + ": imported bounds " + bounds.size.ToString("F3"));
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            for (int floors = 1; floors <= 3; floors++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPath(floors));
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    float expectedTop = 4.4f + (floors - 1) * 3;
                    if (Mathf.Abs(BoundsOf(instance).max.y - expectedTop) > .015f)
                        throw new InvalidOperationException(prefab.name + " 높이가 맞지 않습니다.");
                    foreach (Transform child in prefab.transform)
                    {
                        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(
                            child.gameObject
                        );
                        if (
                            source == null
                            || !AssetDatabase
                                .GetAssetPath(source)
                                .StartsWith(PrefabPath + "/Floors/")
                        )
                            throw new InvalidOperationException(
                                prefab.name + "의 층 프리팹 연결이 끊겼습니다."
                            );
                    }
                    if (prefab.transform.childCount != floors + 1)
                        throw new InvalidOperationException(prefab.name + "의 층 수가 다릅니다.");
                    long triangles = instance
                        .GetComponentsInChildren<MeshFilter>()
                        .Sum(filter => filter.sharedMesh.triangles.LongLength / 3);
                    int renderers = instance.GetComponentsInChildren<Renderer>().Length;
                    results.Add(
                        prefab.name
                            + ": shared floors "
                            + floors
                            + ", top "
                            + expectedTop.ToString("F1")
                            + "m, triangles "
                            + triangles
                            + ", mesh renderers "
                            + renderers
                    );
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            Directory.CreateDirectory(TownMainstreetVerification.OutputFolder);
            File.WriteAllLines(
                TownMainstreetVerification.OutputFolder + "/cafe-prefabs.txt",
                results
            );
            Debug.Log("Cafe prefab import, height and nested references verified.");
        }

        internal static void PrepareFloorPreview(Scene scene, Camera camera)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Environment" || root.name == "Characters")
                    root.SetActive(false);
            Transform preview = new GameObject("CafeFloorPreview").transform;
            for (int floors = 1; floors <= 3; floors++)
            {
                float x = (2 - floors) * 12;
                Instance(BuildingPath(floors), preview, new Vector3(x, 0, -200));
                var label = Text(
                    "FloorLabel",
                    floors + "F / " + (4.4f + (floors - 1) * 3).ToString("F1") + "m",
                    preview,
                    new Vector3(x, .15f, -196.7f),
                    .72f,
                    Material("Metal")
                );
                label.transform.localRotation = Quaternion.Euler(70, 180, 0);
            }
            Block(
                "Ground",
                preview,
                new Vector3(0, -.15f, -201),
                new Vector3(50, .1f, 26),
                Material("Paver")
            );
            RenderSettings.fog = false;
            camera.orthographic = true;
            camera.orthographicSize = 11.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new UnityEngine.Color(.88f, .91f, .90f);
            camera.transform.position = new Vector3(13, 18, -158);
            camera.transform.LookAt(new Vector3(0, 4, -202));
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Material Material(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(name));

        private static string MaterialPath(string name) => ArtPath + "/Materials/" + name + ".mat";

        private static string PartPath(string name) => PrefabPath + "/Parts/" + name + ".prefab";

        private static string FloorPath(string name) => PrefabPath + "/Floors/" + name + ".prefab";

        private static string BuildingPath(int floors) =>
            PrefabPath + "/Buildings/Cafe" + floors + "F.prefab";

        private static GameObject Part(
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0
        ) => Instance(PartPath(name), parent, position, yaw);

        private static GameObject Instance(
            string path,
            Transform parent,
            Vector3 position,
            float yaw = 0
        ) =>
            BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(path),
                parent,
                position,
                Quaternion.Euler(0, yaw, 0)
            );
    }
}
