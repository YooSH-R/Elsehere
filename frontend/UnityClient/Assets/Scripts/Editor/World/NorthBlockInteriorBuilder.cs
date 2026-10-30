using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.ObjectHover;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>기존 북쪽 배경 건물의 외관을 참조하는 변형에 실내와 계단을 넣는다.</summary>
    public static class NorthBlockInteriorBuilder
    {
        public const string SandboxPath = "Assets/Scenes/Sandbox/NorthBlockInteriorSandbox.unity";
        private static readonly (
            string name,
            string source,
            string sign,
            Vector3 position
        )[] Buildings =
        {
            ("MapleBakery", "BrickResidence2F", "MAPLE BAKERY", new Vector3(-7, .1f, 21)),
            ("LittleArcade", "SageResidence2F", "LITTLE ARCADE", new Vector3(7, .1f, 30)),
            ("HomeLiving", "SageResidence2F", "HOME & LIVING", new Vector3(-7, .1f, 39)),
        };

        [MenuItem("Tools/A305/World/Build North Block Interior Sandbox")]
        public static void BuildSandbox()
        {
            if (!BuilderTools.CanBuild())
                return;
            NorthBlockAssets.Prepare();
            foreach (var building in Buildings)
                BuilderTools.CreatePrefabIfMissing(
                    NorthBlockAssets.Folder + "/" + building.name + ".prefab",
                    () => Build(building.name, building.source, building.sign)
                );
            AssetDatabase.SaveAssets();
            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Install(scene);
            BuilderTools.SaveScene(scene, SandboxPath);
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }

        [MenuItem("Tools/A305/World/Apply North Block Interiors")]
        public static void ApplyToTown()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Install(scene);
            BuilderTools.SaveScene(scene, TownProductionBuilder.ScenePath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/World/Build And Apply North Block Interiors")]
        public static void BuildAndApply()
        {
            BuildSandbox();
            ApplyToTown();
            ObjectWordRegistration.Build();
            ObjectWordAuthoring.CheckExports();
            NorthBlockDesignPreview.Capture();
            NorthBlockDesignPreview.CaptureInteriors();
        }

        [MenuItem("Tools/A305/World/Set Up North Block Tenant Entrances")]
        public static void SetUpTenantEntrances()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (var item in Buildings)
                BuilderTools.UpdatePrefabIfExists(
                    NorthBlockAssets.Folder + "/" + item.name + ".prefab",
                    root => NorthBlockTenantLayout.Apply(root, item.name)
                );
            BuildSandbox();
            NorthBlockDesignPreview.CaptureInteriors();
        }

        private static void Install(Scene scene)
        {
            Transform background = TownProductionBuilder
                .Root(scene, "Environment")
                .Find("TownMainstreet/BackgroundBuildings");
            if (background == null)
                throw new InvalidOperationException("Existing north block is missing.");
            // Position plus source prefab identifies the intended building. The store opposite the hotel is never selected.
            foreach (var item in Buildings)
            {
                if (background.Find(item.name) != null)
                    continue;
                Transform old = background
                    .Cast<Transform>()
                    .SingleOrDefault(child =>
                        Vector3.Distance(child.position, item.position) < .05f
                        && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)
                            == TownBackgroundKit.Folder + "/" + item.source + ".prefab"
                    );
                if (old == null)
                    throw new InvalidOperationException(
                        "Expected existing building moved or changed: " + item.name
                    );
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    NorthBlockAssets.Folder + "/" + item.name + ".prefab"
                );
                if (prefab == null)
                    throw new InvalidOperationException(
                        "Build the north-block sandbox first: " + item.name
                    );
                GameObject replacement = BuilderTools.InstantiatePrefab(
                    prefab,
                    background,
                    old.localPosition,
                    old.localRotation
                );
                replacement.name = item.name;
                replacement.transform.localScale = old.localScale;
                old.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
            }
            Transform gardens = background.parent.Find("StreetGardens");
            foreach (
                Transform tree in gardens
                    .Cast<Transform>()
                    .Where(child => child.name == "GardenTreeV2")
            )
            {
                foreach (var item in Buildings)
                {
                    Transform building = background.Find(item.name);
                    Vector3 local = building.InverseTransformPoint(tree.position);
                    if (Mathf.Abs(local.x) >= 4 || local.z >= 0 || local.z <= -8)
                        continue;
                    // These legacy garden trees were hidden inside the formerly solid shells.
                    tree.position = new Vector3(
                        Mathf.Sign(tree.position.x) * 17.5f,
                        tree.position.y,
                        tree.position.z
                    );
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tree);
                }
            }
            // Clear the independent entrances and their directory signs without changing the street layout.
            foreach (string name in new[] { "LittleArcade", "HomeLiving" })
            {
                Transform building = background.Find(name);
                foreach (Transform item in background.parent.GetComponentsInChildren<Transform>())
                {
                    if (item.name != "GardenTreeV2" && item.name != "TreeBed")
                        continue;
                    Vector3 local = building.InverseTransformPoint(item.position);
                    if (Mathf.Abs(local.x) < 1.5f && local.z > 2.5f && local.z < 4.2f)
                    {
                        item.position += building.TransformDirection(Vector3.right) * 4.7f;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                    }
                }
            }
        }

        private static GameObject Build(string name, string source, string sign)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TownBackgroundKit.Folder + "/" + source + ".prefab"
            );
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.name = name;
            foreach (
                Transform floor in root
                    .transform.Cast<Transform>()
                    .Where(child => child.name.Contains("Floor8m"))
                    .ToArray()
            )
                OpenFloor(floor, floor.localPosition.y < .1f);
            NorthBlockAssets.Bind(root, "shop");
            Transform interior = Group("Interior", root.transform, Vector3.zero);
            Solid(
                "Floor",
                interior,
                new Vector3(0, -.08f, -4),
                new Vector3(8, .16f, 8),
                Cream,
                "floor"
            );
            Solid(
                "UpperFloor",
                interior,
                new Vector3(-1.95f, 3.9f, -4),
                new Vector3(4.1f, .2f, 8),
                Wood,
                "floor"
            );
            Solid(
                "UpperLanding",
                interior,
                new Vector3(2.05f, 3.9f, -.8f),
                new Vector3(3.9f, .2f, 1.6f),
                Wood,
                "floor"
            );
            Solid(
                "Ceiling",
                interior,
                new Vector3(0, 7.08f, -4),
                new Vector3(8, .16f, 8),
                Cream,
                "ceiling"
            );
            Stairs(interior);
            for (int floor = 0; floor < 2; floor++)
            {
                float y = floor * 4;
                InteriorFinish(
                    Group("InteriorFinish" + floor, interior, Vector3.zero),
                    y,
                    ground: floor == 0
                );
                NorthBlockAssets.Place("Plant", interior, new Vector3(-3.3f, y, -.9f));
                var lamp = new GameObject("InteriorLight").AddComponent<Light>();
                lamp.transform.SetParent(interior, false);
                lamp.transform.localPosition = new Vector3(-1.8f, y + 2.2f, -4);
                lamp.type = LightType.Point;
                lamp.range = 8;
                lamp.intensity = .7f;
                lamp.color = new UnityEngine.Color(1, .88f, .69f);
                lamp.shadows = LightShadows.None;
            }
            Solid(
                "ShopSign",
                root.transform,
                new Vector3(0, 3.2f, .25f),
                new Vector3(6.1f, .6f, .14f),
                Green,
                "sign",
                false
            );
            Text("ShopName", sign, root.transform, new Vector3(0, 3.2f, .34f), .32f);
            Text(
                "OpeningNotice",
                "OPEN  /  2 FLOORS",
                root.transform,
                new Vector3(0, 2.72f, .34f),
                .13f
            );
            if (name == "MapleBakery")
                Bakery(interior);
            else if (name == "LittleArcade")
                Arcade(interior);
            else
                HomeGoods(interior);
            NorthBlockTenantLayout.Apply(root, name);
            return root;
        }

        private static void InteriorFinish(Transform parent, float y, bool ground)
        {
            float height = ground ? 4 : 3;
            Material plaster = Color("NorthBlockPlaster", "#ECE3CE");
            Solid(
                "InteriorWall",
                parent,
                new Vector3(-3.785f, y + height / 2, -4),
                new Vector3(.025f, height, 7.6f),
                plaster,
                "wall",
                false
            );
            Solid(
                "InteriorWall",
                parent,
                new Vector3(-1.85f, y + height / 2, -7.785f),
                new Vector3(3.9f, height, .025f),
                plaster,
                "wall",
                false
            );
            Solid(
                "Skirting",
                parent,
                new Vector3(-3.75f, y + .28f, -4),
                new Vector3(.08f, .55f, 7.6f),
                Green,
                "wall",
                false
            );
            Solid(
                "Skirting",
                parent,
                new Vector3(-1.85f, y + .28f, -7.75f),
                new Vector3(3.9f, .55f, .08f),
                Green,
                "wall",
                false
            );
            foreach (float z in new[] { -2f, -6f })
            {
                GameObject window = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TownBackgroundKit.Folder + "/ClosedWindow.prefab"
                );
                BuilderTools.InstantiatePrefab(
                    window,
                    parent,
                    new Vector3(-3.7f, y + (ground ? 2 : 1.5f), z),
                    Quaternion.Euler(0, 90, 0)
                );
            }
        }

        private static void OpenFloor(Transform floor, bool ground)
        {
            Transform filled = floor.Find("Walls");
            Material wall = filled.GetComponent<Renderer>().sharedMaterial;
            filled.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filled.gameObject);
            BoxCollider solid = floor.GetComponent<BoxCollider>();
            solid.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(solid);
            if (ground)
                foreach (
                    string part in new[]
                    {
                        "DoorFrame",
                        "ClosedResidentialDoor",
                        "DoorPanel",
                        "Handle",
                        "Skirting",
                    }
                )
                {
                    Transform old = floor.Find(part);
                    if (old == null)
                        continue;
                    old.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
                }
            Transform shell = Group("HollowWalls", floor, Vector3.zero);
            float height = ground ? 4 : 3;
            foreach (int side in new[] { -1, 1 })
                Solid(
                    "SideWall",
                    shell,
                    new Vector3(side * 3.9f, height / 2, -4),
                    new Vector3(.2f, height, 8),
                    wall,
                    "wall"
                );
            Solid(
                "BackWall",
                shell,
                new Vector3(0, height / 2, -7.9f),
                new Vector3(8, height, .2f),
                wall,
                "wall"
            );
            if (ground)
            {
                foreach (int side in new[] { -1, 1 })
                    Solid(
                        "FrontWall",
                        shell,
                        new Vector3(side * 2.6f, 2, -.1f),
                        new Vector3(2.8f, 4, .2f),
                        wall,
                        "wall"
                    );
                Solid(
                    "DoorHeader",
                    shell,
                    new Vector3(0, 3.3f, -.1f),
                    new Vector3(2.4f, 1.4f, .2f),
                    wall,
                    "wall"
                );
                foreach (int side in new[] { -1, 1 })
                    Solid(
                        "DoorTrim",
                        shell,
                        new Vector3(side * 1.25f, 1.3f, .08f),
                        new Vector3(.12f, 2.6f, .18f),
                        Cream,
                        "door"
                    );
                Solid(
                    "DoorLintel",
                    shell,
                    new Vector3(0, 2.64f, .08f),
                    new Vector3(2.62f, .12f, .18f),
                    Cream,
                    "door"
                );
            }
            else
                Solid(
                    "FrontWall",
                    shell,
                    new Vector3(0, 1.5f, -.1f),
                    new Vector3(8, 3, .2f),
                    wall,
                    "wall"
                );
        }

        private static void Stairs(Transform parent)
        {
            Transform stairs = Group("Stairs", parent, Vector3.zero);
            NorthBlockAssets.Bind(stairs.gameObject, "stairs");
            const int steps = 15;
            const float run = 4;
            for (int index = 0; index < steps; index++)
            {
                float rise = (index + 1) * 2f / steps;
                float z = -1.6f - (index + .5f) * run / steps;
                Solid(
                    "Step",
                    stairs,
                    new Vector3(1.05f, rise / 2, z),
                    new Vector3(1.8f, rise, run / steps),
                    Wood,
                    "stairs"
                );
                float returnZ = -5.6f + (index + .5f) * run / steps;
                Solid(
                    "Step",
                    stairs,
                    new Vector3(2.95f, 2 + rise / 2, returnZ),
                    new Vector3(1.8f, rise, run / steps),
                    Wood,
                    "stairs"
                );
            }
            Solid(
                "MidLanding",
                stairs,
                new Vector3(2, 1.9f, -6.65f),
                new Vector3(3.7f, .2f, 2.1f),
                Wood,
                "floor"
            );
            Rail(stairs, new Vector3(.12f, .95f, -1.6f), new Vector3(.12f, 2.95f, -5.6f));
            Rail(stairs, new Vector3(2, .95f, -1.6f), new Vector3(2, 2.95f, -5.6f));
            Rail(stairs, new Vector3(2, 2.95f, -5.6f), new Vector3(2, 4.95f, -1.6f));
            // Guard the upper-floor opening; leave both stair mouths and the turning landing clear.
            Solid(
                "UpperRailing",
                stairs,
                new Vector3(.12f, 4.5f, -4.65f),
                new Vector3(.08f, 1, 6.1f),
                Green,
                "railing"
            );
            Text("StairDirection", "UPSTAIRS", parent, new Vector3(1.05f, 2.5f, -1.3f), .16f);
        }

        private static void Rail(Transform parent, Vector3 from, Vector3 to)
        {
            Transform rail = Solid(
                "Railing",
                parent,
                (from + to) / 2,
                new Vector3(.075f, .09f, Vector3.Distance(from, to)),
                Green,
                "railing"
            );
            rail.localRotation = Quaternion.LookRotation(to - from);
            for (int index = 0; index <= 4; index++)
            {
                Vector3 top = Vector3.Lerp(from, to, index / 4f);
                Solid(
                    "Baluster",
                    parent,
                    top - Vector3.up * .45f,
                    new Vector3(.065f, .9f, .065f),
                    Green,
                    "railing"
                );
            }
        }

        private static Transform Solid(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            string word,
            bool collision = true
        )
        {
            Transform part = Block(name, parent, position, size, material, .015f);
            if (collision)
                part.gameObject.AddComponent<BoxCollider>().size = size;
            NorthBlockAssets.Bind(part.gameObject, word);
            return part;
        }

        private static Transform Treat(
            string category,
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0
        )
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TinyTreatsPropsBuilder.Prefabs + category + "/" + name + ".prefab"
            );
            return BuilderTools
                .InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0, yaw, 0))
                .transform;
        }

        private static void Bakery(Transform interior)
        {
            for (int index = 0; index < 3; index++)
            {
                float z = -3 - index * 1.25f;
                Treat("BakeryInterior", "Counter", interior, new Vector3(-3, 0, z), 90);
                Treat("BakeryInterior", "DisplayCase", interior, new Vector3(-3, .9f, z), 90);
                Treat(
                    "BakedGoods",
                    new[] { "Bread", "Croissant", "ChocolateCake" }[index],
                    interior,
                    new Vector3(-3, 1.05f, z)
                );
            }
            Treat("BakeryInterior", "CashRegister", interior, new Vector3(-3, .9f, -2.1f), 90);
            Treat("BakeryInterior", "CoffeeMachine", interior, new Vector3(-3, .9f, -6.5f), 90);
            for (int index = 0; index < 2; index++)
            {
                float z = -3 - index * 2.5f;
                NorthBlockAssets.Place("Table", interior, new Vector3(-2, 4, z));
                NorthBlockAssets.Place("Chair", interior, new Vector3(-3, 4, z), 90);
                Treat("BakeryInterior", "Mug", interior, new Vector3(-2, 4.75f, z));
                NorthBlockAssets.Place("Book", interior, new Vector3(-2.3f, 4.75f, z));
            }
        }

        private static void Arcade(Transform interior)
        {
            for (int index = 0; index < 3; index++)
            {
                float z = -2.6f - index * 1.8f;
                NorthBlockAssets.Place("ClawMachine", interior, new Vector3(-3, 0, z), 90);
                NorthBlockAssets.Place(
                    index % 2 == 0 ? "Bear" : "Rabbit",
                    interior,
                    new Vector3(-3, .8f, z)
                );
            }
            NorthBlockAssets.Place("Sofa", interior, new Vector3(-2.7f, 4, -5.8f), 90);
            NorthBlockAssets.Place("CoffeeTable", interior, new Vector3(-1.4f, 4, -5.8f));
            NorthBlockAssets.Place("GameConsole", interior, new Vector3(-1.4f, 4.5f, -5.8f));
            NorthBlockAssets.Place("Table", interior, new Vector3(-2.2f, 4, -2.8f));
            NorthBlockAssets.Place("Chair", interior, new Vector3(-3.2f, 4, -2.8f), 90);
            NorthBlockAssets.Place("Toy", interior, new Vector3(-2.2f, 4.75f, -2.8f));
        }

        private static void HomeGoods(Transform interior)
        {
            NorthBlockAssets.Place("Cabinet", interior, new Vector3(-3.2f, 0, -6.7f), 90);
            NorthBlockAssets.Place("Armchair", interior, new Vector3(-3, 0, -3), 90);
            NorthBlockAssets.Place("CoffeeTable", interior, new Vector3(-1.5f, 0, -3));
            NorthBlockAssets.Place("Lamp", interior, new Vector3(-1.5f, .5f, -3));
            NorthBlockAssets.Place("Nightstand", interior, new Vector3(-3.2f, 0, -4.8f), 90);
            NorthBlockAssets.Place("Toy", interior, new Vector3(-3.2f, .6f, -4.8f));
            NorthBlockAssets.Place("Bed", interior, new Vector3(-2.5f, 4, -5.6f), 90);
            NorthBlockAssets.Place("Nightstand", interior, new Vector3(-3.1f, 4, -3.8f));
            NorthBlockAssets.Place("Lamp", interior, new Vector3(-3.1f, 4.6f, -3.8f));
            NorthBlockAssets.Place("Armchair", interior, new Vector3(-2.8f, 4, -2.2f), 90);
            NorthBlockAssets.Place("Book", interior, new Vector3(-3.1f, 4.6f, -4));
        }
    }
}
