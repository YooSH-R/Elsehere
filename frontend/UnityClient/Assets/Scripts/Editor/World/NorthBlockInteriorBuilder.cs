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
            Stairs(interior, name);
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
            {
                BakeryCounters(interior);
                BakerySeating(interior);
            }
            else if (name == "LittleArcade")
                Arcade(interior);
            else
                HomeGoods(interior);
            NorthBlockTenantLayout.Apply(root, name);
            return root;
        }

        // 이어 붙이는 판은 모서리를 1mm만 둥글게 해 이음매에 홈이 보이지 않게 한다.
        private const float FlushEdge = .001f;

        /// <summary>
        /// 오락실 매장 장면(1층 오락실·2층 서점)을 계단실 자리(x 0~3.8)까지 넓혀 건물 폭 전체를 한 매장으로 쓴다.
        /// 계단실 칸막이·서점 문 위 벽·계단실 안내를 없애고, 층 사이 판(2층 바닥이자 1층 천장)과 그 층 뒷벽 마감을 건물 폭 한 장으로 바꾸며,
        /// 오른쪽 벽 마감과 조명을 매장 쪽(<see cref="InteriorFinish"/>)과 같게 둔다.
        /// 1층은 앞벽을 오락실 문 자리만 비우고 이음 없이 다시 세워 거리 쪽 계단실 입구를 막는다.
        /// 2층 서점 문은 마을에서처럼 서점 오른쪽 벽 앞쪽에 두어 같은 방향(서쪽)으로 들어온다.
        /// 마을 건물에는 계단실이 그대로 남으므로 매장 장면에만 쓴다. 다시 실행해도 같은 모습이 된다.
        /// </summary>
        internal static void WidenIntoStairHall(Transform building, bool upper)
        {
            Transform layout = building.Find("TenantEntrances");
            foreach (Transform part in layout.Cast<Transform>().ToArray())
                if (
                    part.name
                    is "StairHallWall"
                        or "DoorHeader"
                        or "TenantDoorSign"
                        or "TenantDoorSignBoard"
                )
                    Object.DestroyImmediate(part.gameObject);
            Transform interior = building.Find("Interior");
            Transform upperFloor = interior.Find("UpperFloor");
            if (upperFloor != null)
                Object.DestroyImmediate(upperFloor.gameObject);
            float y = upper ? 4 : 0;
            float height = upper ? 3 : 4;
            Transform finish = interior.Find("InteriorFinish" + (upper ? 1 : 0));
            foreach (Transform part in finish.Cast<Transform>().ToArray())
                if ((part.name is "InteriorWall" or "Skirting") && part.localPosition.z < -7)
                    Object.DestroyImmediate(part.gameObject);
            Transform old = building.Find("StairHallAnnex");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            Transform annex = Group("StairHallAnnex", building, Vector3.zero);

            Solid(
                "StoreySlab",
                annex,
                new Vector3(-.05f, 3.9f, -4),
                new Vector3(7.9f, .2f, 8),
                Wood,
                "floor",
                radius: FlushEdge
            );
            Material plaster = Color("NorthBlockPlaster", "#ECE3CE");
            Solid(
                "InteriorWall",
                annex,
                new Vector3(3.785f, y + height / 2, -4),
                new Vector3(.025f, height, 7.6f),
                plaster,
                "wall",
                false
            );
            Solid(
                "InteriorWall",
                annex,
                new Vector3(0, y + height / 2, -7.785f),
                new Vector3(7.6f, height, .025f),
                plaster,
                "wall",
                false,
                FlushEdge
            );
            Solid(
                "Skirting",
                annex,
                new Vector3(3.75f, y + .28f, -4),
                new Vector3(.08f, .55f, 7.6f),
                Green,
                "wall",
                false
            );
            Solid(
                "Skirting",
                annex,
                new Vector3(0, y + .28f, -7.75f),
                new Vector3(7.6f, .55f, .08f),
                Green,
                "wall",
                false,
                FlushEdge
            );
            var lamp = new GameObject("InteriorLight").AddComponent<Light>();
            lamp.transform.SetParent(annex, false);
            lamp.transform.localPosition = new Vector3(1.9f, y + 2.2f, -4);
            lamp.type = LightType.Point;
            lamp.range = 8;
            lamp.intensity = .7f;
            lamp.color = new UnityEngine.Color(1, .88f, .69f);
            lamp.shadows = LightShadows.None;

            if (upper)
            {
                // 문짝은 덧댄 문틀·손잡이와 함께 옮긴다. 마을에서와 같은 방향으로 오른쪽 벽 안쪽 면에 붙이고,
                // 앞벽(z -0.2)에 닿지 않게 조금 뒤로 둔다.
                layout
                    .Find("BookshopDoor")
                    .SetLocalPositionAndRotation(
                        new Vector3(3.75f, 4, -1.95f),
                        Quaternion.Euler(0, -90, 0)
                    );
                return;
            }
            Transform ground = building
                .Cast<Transform>()
                .Single(floor => floor.name.Contains("Floor8m") && floor.localPosition.y < 1);
            Material wall = ground.Find("Walls").GetComponent<Renderer>().sharedMaterial;
            Transform shell = ground.Find("HollowWalls");
            foreach (Transform part in shell.Cast<Transform>().ToArray())
                if (part.name is "FrontWall" or "DoorHeader")
                    Object.DestroyImmediate(part.gameObject);
            Transform door = layout.Find("ArcadeDoor");
            float doorWidth = door.Find("Hinge/DoorLeaf").GetComponent<BoxCollider>().size.x;
            float left = door.localPosition.x - .05f;
            float right = door.localPosition.x + doorWidth + .05f;
            Solid(
                "FrontWall",
                annex,
                new Vector3((left - 4) / 2, 2, -.1f),
                new Vector3(left + 4, 4, .2f),
                wall,
                "wall",
                radius: FlushEdge
            );
            Solid(
                "DoorHeader",
                annex,
                new Vector3((left + right) / 2, 3.3f, -.1f),
                new Vector3(right - left, 1.4f, .2f),
                wall,
                "wall",
                radius: FlushEdge
            );
            Solid(
                "FrontWall",
                annex,
                new Vector3((right + 4) / 2, 2, -.1f),
                new Vector3(4 - right, 4, .2f),
                wall,
                "wall",
                radius: FlushEdge
            );
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

        internal static void OpenFloor(Transform floor, bool ground)
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

        /// <summary>
        /// 앞에서 뒤로 오른 뒤 계단참에서 돌아 앞쪽 2층 계단참으로 오르는 실내 계단.
        /// 빵집은 왼쪽으로 돌고, 오락실 공용 계단실은 오른쪽으로 돌아 마지막 단 바로 왼쪽에 2층 서점 문이 온다.
        /// </summary>
        private static void Stairs(Transform parent, string building)
        {
            bool isBakery = building == "MapleBakery";
            bool turnsRight = TurnsRight(building);
            float firstX = FirstFlightX(building);
            float returnX = 4 - firstX;
            float firstWallSideX = turnsRight ? 3.73f : .12f;
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
                    new Vector3(firstX, rise / 2, z),
                    new Vector3(1.8f, rise, run / steps),
                    Wood,
                    "stairs"
                );
                float returnZ = -5.6f + (index + .5f) * run / steps;
                Solid(
                    "Step",
                    stairs,
                    new Vector3(returnX, 2 + rise / 2, returnZ),
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
            Rail(
                stairs,
                new Vector3(firstWallSideX, .95f, -1.6f),
                new Vector3(firstWallSideX, 2.95f, -5.6f)
            );
            Rail(stairs, new Vector3(2, .95f, -1.6f), new Vector3(2, 2.95f, -5.6f));
            Rail(stairs, new Vector3(2, 2.95f, -5.6f), new Vector3(2, 4.95f, -1.6f));
            if (turnsRight)
                // 공용 계단실 벽이 2층 매장 쪽을 막는다. 첫 계단 위로 열린 2층 계단참 가장자리만 난간으로 막는다.
                Rail(stairs, new Vector3(2, 4.95f, -1.6f), new Vector3(3.73f, 4.95f, -1.6f));
            else
                // Guard the upper-floor opening; leave both stair mouths and the turning landing clear.
                Solid(
                    "UpperRailing",
                    stairs,
                    new Vector3(.12f, 4.5f, -4.65f),
                    new Vector3(.08f, 1, 6.1f),
                    Green,
                    "railing"
                );
            if (isBakery)
                BakeryStairDetails(parent, stairs);
        }

        private static bool TurnsRight(string building) => building == "LittleArcade";

        // 입구에서 안쪽(-z)을 보면 +x가 왼쪽이다. 오른쪽으로 돌면 첫 계단이 왼쪽 절반, 돌아오는 계단이 오른쪽 절반이다.
        private static float FirstFlightX(string building) => TurnsRight(building) ? 2.95f : 1.05f;

        /// <summary>
        /// 이미 만든 오락실 프리팹의 공용 계단실을 다시 만든다. 계단을 오른쪽으로 도는 방향으로 바꾸고,
        /// 계단실 벽의 오락실 문을 건물 앞면으로 옮기며 계단실 안내를 맞춘다. 건물 간판·집기 등 나머지 배치는 그대로 둔다.
        /// </summary>
        [MenuItem("Tools/A305/World/Rebuild Little Arcade Stair Hall")]
        public static void RebuildArcadeStairHall()
        {
            if (!BuilderTools.CanBuild())
                return;
            string path = NorthBlockAssets.Folder + "/LittleArcade.prefab";
            BuilderTools.UpdatePrefabIfExists(
                path,
                root =>
                {
                    // 계단 묶음은 단·난간뿐이라 새로 만든다.
                    Transform interior = root.transform.Find("Interior");
                    Object.DestroyImmediate(interior.Find("Stairs").gameObject);
                    Stairs(interior, root.name);
                    NorthBlockTenantLayout.MoveArcadeDoorToFront(root.transform);
                    NorthBlockTenantLayout.ArcadeStairHallSigns(root.transform);
                    // 새 계단·벽·문에도 호버 영역을 프리팹 안에 붙여, 씬마다 따로 붙지 않게 한다.
                    ObjectWordRegistration.ApplyToPrefab(root, path);
                }
            );
            AssetDatabase.SaveAssets();
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

        internal static Transform Solid(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            string word,
            bool collision = true,
            float radius = .015f
        )
        {
            Transform part = Block(name, parent, position, size, material, radius);
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

        private static void BakeryStairDetails(Transform interior, Transform stairs)
        {
            // 2층 바닥의 열린 가장자리만 막아 돌아 올라오는 계단 입구는 비워 둔다.
            Rail(stairs, new Vector3(.12f, 4.9f, -1.57f), new Vector3(2f, 4.9f, -1.57f));
            Rail(stairs, new Vector3(3.73f, 2.95f, -5.6f), new Vector3(3.73f, 4.95f, -1.6f));
            Transform plaque = Solid(
                "StairSign",
                interior,
                new Vector3(3.77f, 1.75f, -1.45f),
                new Vector3(.06f, .38f, 1.25f),
                Green,
                "sign",
                false
            );
            Text(
                "StairDirection",
                "UPSTAIRS",
                plaque,
                new Vector3(-.032f, 0, 0),
                .16f
            ).transform.localRotation = Quaternion.Euler(0, 90, 0);
        }

        private static void BakeryCounters(Transform interior)
        {
            // 1.8m 상판 세 개를 이어 입구부터 계산대, 유리 진열장, 케이크 진열대를 놓는다.
            foreach (float z in new[] { -2.6f, -4.4f, -6.2f })
                Treat("BakeryInterior", "Counter", interior, new Vector3(-3, 0, z), 90);
            Transform register = Treat(
                "BakeryInterior",
                "CashRegister",
                interior,
                new Vector3(-2.85f, .9f, -2.12f),
                90
            );
            register.localScale = Vector3.one * 1.4f;
            Treat("BakeryInterior", "ServingTray", interior, new Vector3(-3, .9f, -2.85f));
            Treat("BakedGoods", "Bread", interior, new Vector3(-3, .945f, -2.85f), 90).localScale =
                Vector3.one * .65f;
            Treat(
                "BakeryInterior",
                "DisplayCase",
                interior,
                new Vector3(-3, .9f, -4.4f),
                90
            ).localScale = new Vector3(2.1f, 1, 1);
            Treat("BakedGoods", "Croissant", interior, new Vector3(-3, 1.319f, -4.4f), -12);
            Transform stand = Treat(
                "BakeryInterior",
                "PastryStand",
                interior,
                new Vector3(-3, .9f, -5.92f)
            );
            if (stand.TryGetComponent(out BoxCollider standBox))
                PropColliderShape.FitToShape(standBox);
            Treat("BakedGoods", "ChocolateCake", interior, new Vector3(-3, 1.25f, -5.92f));
            Treat("BakeryInterior", "CoffeeMachine", interior, new Vector3(-3, .9f, -6.73f), 90);
        }

        private static void BakerySeating(Transform interior)
        {
            for (int index = 0; index < 2; index++)
            {
                float z = -3 - index * 2.5f;
                NorthBlockAssets.Place("Table", interior, new Vector3(-2, 4, z));
                // 이 모델은 등받이 없는 90cm 바 의자이므로 좌판을 식탁용 높이(45cm)에 맞춘다.
                Transform chair = NorthBlockAssets.Place(
                    "Chair",
                    interior,
                    new Vector3(-2, 4, z + .65f)
                );
                chair.localScale = new Vector3(.75f, .5f, .75f);
                Treat("BakeryInterior", "Mug", interior, new Vector3(-2.23f, 4.75f, z));
                Vector3 platePosition = new Vector3(-1.86f, 4.75f, z);
                Treat("BakeryInterior", "Plate", interior, platePosition);
                Vector3 pastryPosition = platePosition + Vector3.up * .016f;
                if (index == 0)
                    Treat("BakedGoods", "Croissant", interior, pastryPosition, -20);
                else
                    PlaceBagel(interior, pastryPosition);
            }
        }

        private static void PlaceBagel(Transform interior, Vector3 position)
        {
            ObjectWordAuthoring.FindOrCreateWord(
                "bagel",
                "bagel",
                "베이글",
                "I'd like a bagel, please.",
                "베이글 하나 주세요."
            );
            Mesh mesh = AssetDatabase
                .LoadAllAssetsAtPath("Assets/Art/World/NorthBlock/SesameBagel.obj")
                .OfType<Mesh>()
                .Single();
            Material palette = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/World/TinyTreats/BakedGoodsPalette.mat"
            );
            Transform bagel = MeshObject("Bagel", interior, position, Vector3.one, mesh, palette);
            NorthBlockAssets.Bind(bagel.gameObject, "bagel");
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
