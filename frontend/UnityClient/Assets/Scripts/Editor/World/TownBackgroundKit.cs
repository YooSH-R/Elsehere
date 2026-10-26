using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>불이 꺼진 창·닫힌 문으로 이용 장소와 구분하는 배경용 층 모듈.</summary>
    internal static class TownBackgroundKit
    {
        internal const string Folder = "Assets/Prefabs/World/TownProduction";

        internal static void Prepare()
        {
            BuilderTools.CreatePrefabIfMissing(
                Folder + "/ClosedWindow.prefab",
                () =>
                {
                    GameObject window = Window();
                    CombineParts(window, "TownClosedWindow");
                    return window;
                }
            );
            foreach (string style in new[] { "Butter", "Sage", "Brick" })
            {
                string palette = style;
                BuilderTools.CreatePrefabIfMissing(
                    Folder + $"/{palette}GroundFloor8m.prefab",
                    () => Floor(palette, true)
                );
                BuilderTools.CreatePrefabIfMissing(
                    Folder + $"/{palette}UpperFloor8m.prefab",
                    () => Floor(palette, false)
                );
                for (int count = 1; count <= 3; count++)
                {
                    int floors = count;
                    BuilderTools.CreatePrefabIfMissing(
                        Folder + $"/{palette}Residence{floors}F.prefab",
                        () => Building(palette, floors)
                    );
                }
            }
            BuilderTools.CreatePrefabIfMissing(Folder + "/VacantBuilding2F.prefab", VacantBuilding);
            BuilderTools.CreatePrefabIfMissing(Folder + "/PocketPark.prefab", PocketPark);
            BuilderTools.CreatePrefabIfMissing(
                Folder + "/ExtensionEnd.prefab",
                () => ExtensionEnd(40)
            );
            BuilderTools.CreatePrefabIfMissing(
                Folder + "/CrossStreetEnd14m.prefab",
                () => ExtensionEnd(14)
            );
            BuilderTools.CreatePrefabIfMissing(
                Folder + "/NorthStreetDressing.prefab",
                NorthStreetDressing
            );
        }

        internal static Transform Instance(
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0f
        )
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/" + name + ".prefab"
            );
            if (prefab == null)
                throw new System.InvalidOperationException("Town prefab missing: " + name);
            return BuilderTools
                .InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0, yaw, 0))
                .transform;
        }

        private static Material Wall(string style)
        {
            return style == "Brick"
                ? MainstreetReferenceKit.Brick
                : MainstreetReferenceKit.Surface(
                    "Town" + style,
                    style == "Butter" ? "#E7C694" : "#A2B8A4",
                    false
                );
        }

        private static GameObject Floor(string style, bool ground)
        {
            var root = new GameObject(style + (ground ? "GroundFloor8m" : "UpperFloor8m"));
            Transform p = root.transform;
            float height = ground ? 4f : 3f;
            Block(
                "Walls",
                p,
                new Vector3(0, height / 2, -4),
                new Vector3(8, height, 8),
                Wall(style),
                .08f
            );
            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0, height / 2, -4);
            solid.size = new Vector3(8, height, 8);
            Block(
                "FrontCornice",
                p,
                new Vector3(0, height - .14f, .08f),
                new Vector3(8.25f, .28f, .4f),
                Cream
            );
            foreach (int side in new[] { -1, 1 })
            {
                Block(
                    "SideCornice",
                    p,
                    new Vector3(side * 4, height - .14f, -4),
                    new Vector3(.4f, .28f, 8.25f),
                    Cream
                );
                foreach (float z in new[] { -2f, -6f })
                    Instance(
                        "ClosedWindow",
                        p,
                        new Vector3(side * 4.04f, ground ? 2f : 1.5f, z),
                        side * 90f
                    );
            }
            foreach (float x in new[] { -2.25f, 2.25f })
                Instance("ClosedWindow", p, new Vector3(x, ground ? 2f : 1.5f, .04f));
            if (ground)
            {
                Block(
                    "DoorFrame",
                    p,
                    new Vector3(0, 1.2f, .08f),
                    new Vector3(1.4f, 2.4f, .16f),
                    Cream
                );
                Block(
                    "ClosedResidentialDoor",
                    p,
                    new Vector3(0, 1.12f, .18f),
                    new Vector3(1.15f, 2.23f, .08f),
                    Color("TownDoorSage", "#506D65")
                );
                Block(
                    "DoorPanel",
                    p,
                    new Vector3(0, 1.12f, .23f),
                    new Vector3(.8f, 1.6f, .03f),
                    Color("TownDoorInset", "#698378")
                );
                SmoothBall("Handle", p, new Vector3(.4f, 1.05f, .29f), Vector3.one * .09f, Wood);
                Block(
                    "Skirting",
                    p,
                    new Vector3(0, .18f, .02f),
                    new Vector3(8.12f, .36f, .22f),
                    Cream
                );
            }
            return root;
        }

        private static GameObject Building(string style, int floors)
        {
            var root = new GameObject(style + "Residence" + floors + "F");
            Instance(style + "GroundFloor8m", root.transform, Vector3.zero);
            for (int index = 1; index < floors; index++)
                Instance(
                    style + "UpperFloor8m",
                    root.transform,
                    Vector3.up * (4 + (index - 1) * 3)
                );
            float roof = 4f + (floors - 1) * 3f;
            Block(
                "RoofCap",
                root.transform,
                new Vector3(0, roof + .2f, -4),
                new Vector3(8.35f, .4f, 8.35f),
                Cream,
                .08f
            );
            Block(
                "RoofInset",
                root.transform,
                new Vector3(0, roof + .41f, -4),
                new Vector3(7.5f, .03f, 7.5f),
                Color("TownRoofSlate", "#657776")
            );
            return root;
        }

        private static GameObject VacantBuilding()
        {
            var root = new GameObject("VacantBuilding2F");
            Instance("ButterResidence2F", root.transform, Vector3.zero);
            Transform p = root.transform;
            // A shutter replaces the invitation to enter; there is no shop name, light or merchandise.
            Block(
                "ClosedShutter",
                p,
                new Vector3(0, 1.24f, .35f),
                new Vector3(1.55f, 2.48f, .15f),
                Color("TownShutter", "#7D9492")
            );
            for (float y = .25f; y < 2.45f; y += .22f)
                Block(
                    "ShutterSlat",
                    p,
                    new Vector3(0, y, .44f),
                    new Vector3(1.5f, .035f, .03f),
                    Color("TownShutterLine", "#697F7E"),
                    .01f
                );
            Block(
                "StatusPlate",
                p,
                new Vector3(0, 1.55f, .49f),
                new Vector3(1.05f, .43f, .04f),
                Cream
            );
            Text("Status", "CLOSED", p, new Vector3(0, 1.55f, .52f), .14f, Cream);
            return root;
        }

        private static GameObject PocketPark()
        {
            var root = new GameObject("PocketPark");
            Transform p = root.transform;
            Block("Paving", p, new Vector3(0, -.05f, 0), new Vector3(8, .1f, 8), Cream);
            var ground = root.AddComponent<BoxCollider>();
            ground.center = new Vector3(0, -.05f, 0);
            ground.size = new Vector3(8, .1f, 8);
            FreeProp("Fountain", p, new Vector3(0, 0, 0));
            FreeProp("ParkBench", p, new Vector3(-2.4f, 0, 2.4f), 180);
            FreeProp("ParkBench", p, new Vector3(2.4f, 0, -2.4f));
            FreeProp("TrashCan", p, new Vector3(3, 0, 2.8f));
            foreach (float x in new[] { -3.2f, 3.2f })
            foreach (float z in new[] { -3.2f, 3.2f })
                Place("FlowerPlanter", p, new Vector3(x, 0, z), () => Planter());
            return root;
        }

        private static void FreeProp(string name, Transform parent, Vector3 position, float yaw = 0)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/World/TinyTreats/PrettyPark/" + name + ".prefab"
            );
            BuilderTools.InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0, yaw, 0));
        }

        private static GameObject NorthStreetDressing()
        {
            var root = new GameObject("NorthStreetDressing");
            foreach (int side in new[] { -1, 1 })
            {
                for (float z = 31; z < 69; z += 2)
                {
                    foreach (float x in new[] { 4f, 6f })
                    {
                        GameObject tile = AssetDatabase.LoadAssetAtPath<GameObject>(
                            PrefabFolder + "/SidewalkTile2m.prefab"
                        );
                        BuilderTools.InstantiatePrefab(
                            tile,
                            root.transform,
                            new Vector3(side * x, .105f, z),
                            Quaternion.identity
                        );
                    }
                    Block(
                        "Curb",
                        root.transform,
                        new Vector3(side * 3.04f, .06f, z),
                        new Vector3(.16f, .12f, 1.96f),
                        Cream
                    );
                }
                foreach (float z in new[] { 39f, 51f, 63f })
                    Place(
                        "GardenTreeV2",
                        root.transform,
                        new Vector3(side * 3.65f, .1f, z),
                        MainstreetReferenceKit.GardenTree
                    );
                foreach (float z in new[] { 35f, 47f, 59f, 67f })
                    Place(
                        "StreetLampV2",
                        root.transform,
                        new Vector3(side * 3.45f, .1f, z),
                        MainstreetReferenceKit.StreetLamp
                    );
            }
            return root;
        }

        private static GameObject ExtensionEnd(float width)
        {
            var root = new GameObject("ExtensionEnd");
            Transform p = root.transform;
            Block("GardenWall", p, new Vector3(0, .45f, 0), new Vector3(width, .9f, .6f), Cream);
            Block(
                "Hedge",
                p,
                new Vector3(0, 1.2f, .4f),
                new Vector3(width, 1.6f, 1.4f),
                MainstreetReferenceKit.Foliage,
                .35f
            );
            var boundary = root.AddComponent<BoxCollider>();
            boundary.center = new Vector3(0, 3, .3f);
            boundary.size = new Vector3(width, 6, 1.5f);
            for (float x = -width / 2 + 2; x < width / 2; x += 6)
                Place("GardenTreeV2", p, new Vector3(x, 0, 3), MainstreetReferenceKit.GardenTree);
            return root;
        }
    }
}
