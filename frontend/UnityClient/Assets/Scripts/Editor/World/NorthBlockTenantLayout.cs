using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.ObjectHover;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>복층 매장, 공용 계단 상가, 외부 계단 상가의 출입 동선을 구성한다.</summary>
    internal static class NorthBlockTenantLayout
    {
        [MenuItem("Tools/A305/World/Fit Tea Room Stair Sign")]
        public static void FitTeaRoomStairSign()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                NorthBlockAssets.Folder + "/HomeLiving.prefab",
                root => StairEntranceSign(root.transform.Find("TenantEntrances"))
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>기존 오락실 프리팹의 계단실 안내만 맞춘다(<see cref="ArcadeStairHallSigns"/>).</summary>
        [MenuItem("Tools/A305/World/Fit Arcade Stair Hall Signs")]
        public static void FitArcadeStairHallSigns()
        {
            BuilderTools.ThrowIfPlaying();
            string path = NorthBlockAssets.Folder + "/LittleArcade.prefab";
            BuilderTools.UpdatePrefabIfExists(
                path,
                root =>
                {
                    ArcadeStairHallSigns(root.transform);
                    // 새 안내판에도 호버 영역을 프리팹 안에 붙여, 씬마다 따로 붙지 않게 한다.
                    ObjectWordRegistration.ApplyToPrefab(root, path);
                }
            );
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/World/Fit Tea Room Chairs")]
        public static void FitTeaRoomChairs()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                NorthBlockAssets.Folder + "/HomeLiving.prefab",
                root => FitTeaRoomChairs(root.transform.Find("Interior"))
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 오락실 공용 계단실 안내. 계단실 입구 위에는 2층 서점만 알리는 판을, 계단을 다 올라와 마주 보는 앞벽에는
        /// 2층 서점 간판을 둔다. 첫 계단 앞의 UPSTAIRS 글자는 두지 않는다. 다시 실행해도 같은 모습이 된다.
        /// </summary>
        internal static void ArcadeStairHallSigns(Transform root)
        {
            Transform upstairs = root.Find("Interior/StairDirection");
            if (upstairs != null)
                Object.DestroyImmediate(upstairs.gameObject);
            Transform layout = root.Find("TenantEntrances");
            StairHallDirectory(layout);
            BookshopWallSign(layout);
        }

        private static void FitTeaRoomChairs(Transform interior)
        {
            Transform[] furniture = interior.Cast<Transform>().ToArray();
            Transform[] tables = furniture
                .Where(item => item.name == "Table" && Mathf.Approximately(item.localPosition.y, 4))
                .ToArray();
            foreach (Transform chair in furniture)
            {
                if (chair.name != "Chair" || !Mathf.Approximately(chair.localPosition.y, 4))
                    continue;
                Transform table = tables
                    .OrderBy(item => (item.localPosition - chair.localPosition).sqrMagnitude)
                    .First();
                float side = chair.localPosition.x < table.localPosition.x ? -1 : 1;
                // 90cm 바 스툴을 낮추되 비율과 바닥 접점을 유지하고, 테이블 옆에 여유를 둔다.
                chair.localScale = Vector3.one * .6f;
                chair.localPosition = table.localPosition + Vector3.right * (side * 1.15f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(chair);
            }
        }

        internal static void Apply(GameObject root, string name)
        {
            if (root.transform.Find("TenantEntrances") != null)
            {
                Transform exterior = root.transform.Find("TenantEntrances/ExteriorStairs");
                if (exterior != null)
                {
                    EnsureWalkSurface(exterior);
                    StairEntranceSign(exterior.parent);
                }
                return;
            }
            Transform layout = Group("TenantEntrances", root.transform, Vector3.zero);
            if (name == "MapleBakery")
            {
                Door(layout, "BakeryDoor", new Vector3(-1.1f, 0, .02f), 0, 2.2f);
                return;
            }
            Transform interior = root.transform.Find("Interior");
            foreach (string sign in new[] { "ShopSign", "ShopName", "OpeningNotice" })
                Object.DestroyImmediate(root.transform.Find(sign).gameObject);
            foreach (Transform furnishing in interior.Cast<Transform>().ToArray())
                if (
                    furnishing.localPosition.y >= 4
                    && PrefabUtility
                        .GetPrefabAssetPathOfNearestInstanceRoot(furnishing.gameObject)
                        .StartsWith(NorthBlockAssets.Folder + "/Props/")
                )
                    Object.DestroyImmediate(furnishing.gameObject);
            if (name == "LittleArcade")
                SharedStairShops(root.transform, interior, layout);
            else
                ExternalStairShops(root.transform, interior, layout);
        }

        // 오락실 문은 "1F LITTLE ARCADE" 간판 아래 건물 앞면에 둔다. 안쪽 앞 왼편의 경품 진열(PrizeCorner)을 피해
        // 칸막이 쪽으로 붙인다. 왼쪽 경첩 자리와 문짝 폭.
        private static readonly Vector3 ArcadeDoorHinge = new Vector3(-1.8f, 0, .02f);
        private const float ArcadeDoorWidth = 1.7f;

        private static void SharedStairShops(Transform root, Transform interior, Transform layout)
        {
            GroundFloorStairHall(root, layout);
            Door(layout, "ArcadeDoor", ArcadeDoorHinge, 0, ArcadeDoorWidth);
            Solid(
                "StairHallWall",
                layout,
                new Vector3(0, 5.5f, -4.9f),
                new Vector3(.14f, 3, 6.2f),
                Cream,
                "wall"
            );
            Solid(
                "DoorHeader",
                layout,
                new Vector3(0, 6.75f, -.9f),
                new Vector3(.14f, .5f, 1.8f),
                Cream,
                "wall"
            );
            Door(layout, "BookshopDoor", new Vector3(0, 4, -1.75f), -90, 1.7f);
            Sign(layout, "1F  LITTLE ARCADE", new Vector3(-1.7f, 3.2f, .25f), 3.9f, Navy);
            Sign(layout, "2F  PAPER & LEAF BOOKS", new Vector3(0, 6.5f, .25f), 6.4f, Green);
            ArcadeStairHallSigns(root);
            for (int index = 0; index < 2; index++)
            {
                var shelf = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Home/CozyCottage/OakBookcase.prefab"
                );
                BuilderTools.InstantiatePrefab(
                    shelf,
                    interior,
                    new Vector3(-3.1f, 4, -3.2f - index * 2.8f),
                    Quaternion.Euler(0, 90, 0)
                );
            }
            NorthBlockAssets.Place("Table", interior, new Vector3(-1.6f, 4, -4.5f));
            NorthBlockAssets.Place("Chair", interior, new Vector3(-1.6f, 4, -6));
            for (int index = 0; index < 3; index++)
                NorthBlockAssets.Place(
                    "Book",
                    interior,
                    new Vector3(-1.9f + index * .3f, 4.75f, -4.5f)
                );
        }

        /// <summary>
        /// 이미 만든 오락실 프리팹에서 계단실 1층의 오락실 문을 건물 앞면으로 옮기고, 그 자리는 칸막이 벽으로 막는다.
        /// 문짝은 덧댄 문틀·손잡이(<see cref="ShopEntranceBuilder"/>)와 함께 그대로 옮긴다.
        /// </summary>
        internal static void MoveArcadeDoorToFront(Transform root)
        {
            Transform layout = root.Find("TenantEntrances");
            foreach (Transform part in layout.Cast<Transform>().ToArray())
                if (
                    (part.name is "StairHallWall" or "DoorHeader" or "TenantDoorSign")
                    && part.localPosition.y < 4
                )
                    Object.DestroyImmediate(part.gameObject);
            GroundFloorStairHall(root, layout);
            layout
                .Find("ArcadeDoor")
                .SetLocalPositionAndRotation(ArcadeDoorHinge, Quaternion.identity);
        }

        // 계단실은 인도 쪽 입구(+x)에서 2층 서점 문으로만 이어진다. 1층 칸막이는 문 없이 앞벽부터 뒷벽까지,
        // 2층 바닥 높이까지 막고, 건물 앞면에는 오락실 문과 계단실 입구를 낸다.
        private static void GroundFloorStairHall(Transform root, Transform layout)
        {
            FrontEntrances(
                root,
                false,
                (ArcadeDoorHinge.x + ArcadeDoorWidth / 2, ArcadeDoorWidth + .1f),
                (2, 2.4f)
            );
            Solid(
                "StairHallWall",
                layout,
                new Vector3(0, 2, -4.05f),
                new Vector3(.14f, 4, 7.7f),
                Cream,
                "wall"
            );
        }

        /// <summary>
        /// 계단실 입구 위 안내판. 이 입구는 2층 서점으로만 이어지므로 서점만 알린다. 글자만 두면 벽 앞 공중에 떠 보이므로
        /// 2층 간판과 같은 초록 판을 입구 폭으로 붙이고 그 위에 글자를 둔다. 기존 글자는 바꾼 글꼴을 지키도록 다시 쓴다.
        /// </summary>
        private static void StairHallDirectory(Transform layout)
        {
            var board = new Vector3(2, 3.2f, .25f);
            if (layout.Find("DirectoryBoard") == null)
                Solid(
                    "DirectoryBoard",
                    layout,
                    board,
                    new Vector3(2.4f, .6f, .14f),
                    Green,
                    "sign",
                    false
                );
            Transform directory = layout.Find("Directory");
            TextMeshPro label =
                directory != null
                    ? directory.GetComponent<TextMeshPro>()
                    : Text("Directory", "", layout, Vector3.zero, .23f);
            label.text = "2F  BOOKSHOP";
            label.transform.localPosition = board + Vector3.forward * .09f;
            // 공용 글자 영역(8×3m) 대신 판(2.4×0.6m) 안쪽에 여백을 두고 한 줄로 맞춘다.
            label.rectTransform.sizeDelta = new Vector2(2.1f, .4f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 1;
            label.fontSizeMax = 2.3f;
        }

        /// <summary>
        /// 2층 서점 간판. 돌아오는 계단(x 0.15~1.95)을 다 올라오면 바로 마주 보는 앞벽 안쪽(z -0.2)에, 그 계단 폭 가운데
        /// 눈높이보다 조금 위로 초록 판을 붙이고 글자를 둔다. 서점 문은 그 바로 왼쪽이다.
        /// 문 위에 있던 기존 글자는 바꾼 글꼴을 지키도록 옮겨 다시 쓴다.
        /// </summary>
        private static void BookshopWallSign(Transform layout)
        {
            var board = new Vector3(1.05f, 5.75f, -.23f);
            if (layout.Find("TenantDoorSignBoard") == null)
                Solid(
                    "TenantDoorSignBoard",
                    layout,
                    board,
                    new Vector3(1.7f, .45f, .06f),
                    Green,
                    "sign",
                    false
                );
            Transform sign = layout
                .Cast<Transform>()
                .SingleOrDefault(part => part.name == "TenantDoorSign");
            TextMeshPro label =
                sign != null
                    ? sign.GetComponent<TextMeshPro>()
                    : Text("TenantDoorSign", "", layout, Vector3.zero, .2f);
            label.text = "2F  PAPER & LEAF";
            // 글자 앞면이 계단을 올라오는 쪽(-z)을 본다.
            label.transform.SetLocalPositionAndRotation(
                board + Vector3.back * .04f,
                Quaternion.identity
            );
            label.rectTransform.sizeDelta = new Vector2(1.5f, .32f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 1;
            label.fontSizeMax = 2.2f;
        }

        private static void ExternalStairShops(Transform root, Transform interior, Transform layout)
        {
            foreach (
                string part in new[] { "Stairs", "UpperFloor", "UpperLanding", "StairDirection" }
            )
            {
                Transform old = interior.Find(part);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }
            Solid(
                "FullUpperFloor",
                layout,
                new Vector3(0, 3.9f, -4),
                new Vector3(8, .2f, 8),
                Wood,
                "floor"
            );
            FrontEntrances(root, false, (2.7f, 2.4f));
            FrontEntrances(root, true, (2.7f, 2.4f));
            Door(layout, "HomeGoodsDoor", new Vector3(1.6f, 0, .02f), 0, 2.2f);
            Door(layout, "TeaRoomDoor", new Vector3(1.6f, 4, .02f), 0, 2.2f);
            Transform stairs = Group("ExteriorStairs", layout, Vector3.zero);
            NorthBlockAssets.Bind(stairs.gameObject, "stairs");
            const int steps = 30;
            for (int index = 0; index < steps; index++)
            {
                float x = -3.7f + (index + .5f) * 5.2f / steps;
                float y = (index + 1) * 4f / steps;
                Solid(
                    "Tread",
                    stairs,
                    new Vector3(x, y - .06f, 1.2f),
                    new Vector3(5.2f / steps, .12f, 2.2f),
                    Wood,
                    "stairs"
                );
            }
            Solid(
                "UpperLanding",
                stairs,
                new Vector3(2.7f, 3.9f, 1.2f),
                new Vector3(2.4f, .2f, 2.4f),
                Wood,
                "floor"
            );
            foreach (float z in new[] { .13f, 2.27f })
                Handrail(stairs, new Vector3(-3.7f, .95f, z), new Vector3(1.5f, 4.95f, z));
            Solid(
                "LandingGuard",
                stairs,
                new Vector3(2.7f, 4.5f, 2.36f),
                new Vector3(2.4f, 1, .08f),
                Green,
                "railing"
            );
            Solid(
                "LandingGuard",
                stairs,
                new Vector3(3.86f, 4.5f, 1.2f),
                new Vector3(.08f, 1, 2.4f),
                Green,
                "railing"
            );
            foreach (float x in new[] { 1.5f, 3.85f })
                Solid(
                    "StairSupport",
                    stairs,
                    new Vector3(x, 1.9f, 2.25f),
                    new Vector3(.12f, 3.8f, .12f),
                    Green,
                    "railing"
                );
            Sign(layout, "1F  HOME & LIVING", new Vector3(-1.4f, 2.9f, .25f), 4.1f, Green);
            Sign(layout, "2F  LEAF TEA ROOM", new Vector3(0, 6.5f, .25f), 6.1f, Green);
            StairEntranceSign(layout);
            NorthBlockAssets.Place("Bed", interior, new Vector3(1.6f, 0, -5.9f));
            NorthBlockAssets.Place("Sofa", interior, new Vector3(.5f, 0, -3.5f));
            for (int index = 0; index < 4; index++)
            {
                float x = index % 2 == 0 ? -2 : 1.7f;
                float z = index < 2 ? -3 : -6;
                NorthBlockAssets.Place("Table", interior, new Vector3(x, 4, z));
                NorthBlockAssets.Place("Chair", interior, new Vector3(x - .95f, 4, z), 90);
                NorthBlockAssets.Place("Chair", interior, new Vector3(x + .95f, 4, z), -90);
                var mug = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TinyTreatsPropsBuilder.Prefabs + "BakeryInterior/Mug.prefab"
                );
                BuilderTools.InstantiatePrefab(
                    mug,
                    interior,
                    new Vector3(x, 4.75f, z),
                    Quaternion.identity
                );
            }
            FitTeaRoomChairs(interior);
            NorthBlockAssets.Place("Plant", interior, new Vector3(-3.2f, 4, -1));
            EnsureWalkSurface(stairs);
        }

        private static void EnsureWalkSurface(Transform stairs)
        {
            // Closely spaced treads catch a wide capsule. Keep the visible steps and use their slope for walking.
            foreach (
                Transform step in stairs.Cast<Transform>().Where(child => child.name == "Tread")
            )
                step.GetComponent<BoxCollider>().enabled = false;
            stairs.Find("UpperLanding").GetComponent<BoxCollider>().enabled = false;
            string path = "Assets/Art/World/NorthBlock/ExteriorStairWalkSurface.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "ExteriorStairWalkSurface" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.vertices = new[]
            {
                new Vector3(-3.7f, 0, .1f),
                new Vector3(-3.7f, 0, 2.3f),
                new Vector3(1.5f, 4, .1f),
                new Vector3(1.5f, 4, 2.3f),
                new Vector3(1.5f, 4, 0),
                new Vector3(1.5f, 4, 2.4f),
                new Vector3(3.9f, 4, 0),
                new Vector3(3.9f, 4, 2.4f),
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3, 4, 5, 6, 6, 5, 7 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            Transform surface = stairs.Find("WalkSurface");
            if (surface == null)
                surface = Group("WalkSurface", stairs, Vector3.zero);
            BuilderTools.GetOrAddComponent<MeshCollider>(surface.gameObject).sharedMesh = mesh;
        }

        /// <summary>
        /// 층 앞벽을 다시 세우며 출입구(가운데 x, 폭)마다 2.6m 높이로 비운다.
        /// 계단·입구가 있는 +x 쪽 창과 출입구에 걸리는 앞 창은 끈다.
        /// </summary>
        private static void FrontEntrances(
            Transform root,
            bool upper,
            params (float center, float width)[] openings
        )
        {
            Transform floor = root.Cast<Transform>()
                .Single(child =>
                    child.name.Contains("Floor8m") && (child.localPosition.y > 1) == upper
                );
            Transform shell = floor.Find("HollowWalls");
            Material wall = floor.Find("Walls").GetComponent<Renderer>().sharedMaterial;
            foreach (Transform part in shell.Cast<Transform>().ToArray())
                if (part.name is "FrontWall" or "DoorHeader" or "DoorTrim" or "DoorLintel")
                    Object.DestroyImmediate(part.gameObject);
            foreach (
                Transform window in floor
                    .Cast<Transform>()
                    .Where(child =>
                        child.name.StartsWith("ClosedWindow")
                        && (
                            child.localPosition.x > 0
                            || openings.Any(opening =>
                                Mathf.Abs(child.localPosition.x - opening.center)
                                < opening.width / 2 + .8f
                            )
                        )
                    )
            )
            {
                window.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(window.gameObject);
            }
            float height = upper ? 3 : 4;
            float wallStart = -4;
            foreach (var opening in openings.OrderBy(opening => opening.center))
            {
                FrontWallSegment(
                    shell,
                    wall,
                    wallStart,
                    opening.center - opening.width / 2,
                    height
                );
                Solid(
                    "DoorHeader",
                    shell,
                    new Vector3(opening.center, (height + 2.6f) / 2, -.1f),
                    new Vector3(opening.width, height - 2.6f, .2f),
                    wall,
                    "wall"
                );
                wallStart = opening.center + opening.width / 2;
            }
            FrontWallSegment(shell, wall, wallStart, 4, height);
        }

        private static void FrontWallSegment(
            Transform shell,
            Material wall,
            float from,
            float to,
            float height
        )
        {
            if (to - from < .01f)
                return;
            Solid(
                "FrontWall",
                shell,
                new Vector3((from + to) / 2, height / 2, -.1f),
                new Vector3(to - from, height, .2f),
                wall,
                "wall"
            );
        }

        internal static void Door(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            float width
        )
        {
            Transform door = Group(name, parent, position);
            door.localRotation = Quaternion.Euler(0, yaw, 0);
            Transform hinge = Group("Hinge", door, Vector3.zero);
            Transform leaf = Solid(
                "DoorLeaf",
                hinge,
                new Vector3(width / 2, 1.25f, 0),
                new Vector3(width, 2.5f, .08f),
                Green,
                "door"
            );
            Solid(
                "DoorGlass",
                hinge,
                new Vector3(width / 2, 1.55f, .05f),
                new Vector3(width - .22f, 1.45f, .025f),
                Glass,
                "window",
                false
            );
            Solid(
                "Handle",
                hinge,
                new Vector3(width - .15f, 1.1f, .1f),
                new Vector3(.06f, .28f, .07f),
                Cream,
                "handle",
                false
            );
            var sensor = door.gameObject.AddComponent<BoxCollider>();
            sensor.isTrigger = true;
            sensor.center = new Vector3(width / 2, 1.2f, 0);
            sensor.size = new Vector3(width + .4f, 2.5f, 3.5f);
            var behaviour = door.gameObject.AddComponent<ShopSwingDoor>();
            BuilderTools.SetReference(behaviour, "hinge", hinge);
            BuilderTools.SetReference(behaviour, "leafCollider", leaf.GetComponent<Collider>());
        }

        private static void Sign(
            Transform parent,
            string text,
            Vector3 position,
            float width,
            Material material
        )
        {
            Solid(
                "TenantSign",
                parent,
                position,
                new Vector3(width, .6f, .14f),
                material,
                "sign",
                false
            );
            Text("TenantName", text, parent, position + Vector3.forward * .09f, .23f);
        }

        /// <summary>
        /// 바깥 계단 입구의 찻집 안내. 글자만 두면 난간 앞 공중에 떠 보이므로 계단 첫 단 옆 보도에 기둥과 판을 세워 붙인다.
        /// 예전에 글자만 둔 건물은 그 글자를 지우고 안내판으로 바꾼다.
        /// </summary>
        private static void StairEntranceSign(Transform layout)
        {
            Transform floating = layout.Find("StairEntranceSign");
            if (floating != null)
                Object.DestroyImmediate(floating.gameObject);
            Transform existing = layout.Find("StairSign");
            if (existing != null)
            {
                FitStairEntranceText(existing);
                return;
            }
            Transform sign = Group("StairSign", layout, new Vector3(-4.3f, 0, 2.55f));
            Solid(
                "StairSignPost",
                sign,
                new Vector3(0, .65f, 0),
                new Vector3(.08f, 1.3f, .08f),
                Green,
                "sign"
            );
            Solid(
                "StairSignBoard",
                sign,
                new Vector3(0, 1.45f, 0),
                new Vector3(1.25f, .4f, .08f),
                Green,
                "sign",
                false
            );
            Text("StairEntranceSign", "TEA ROOM  2F  >", sign, new Vector3(0, 1.45f, .05f), .19f);
            FitStairEntranceText(sign);
        }

        private static void FitStairEntranceText(Transform sign)
        {
            var label = sign.Find("StairEntranceSign").GetComponent<TextMeshPro>();
            label.text = "TEA ROOM 2F >";
            // 공용 글자 영역(8×3m) 대신 안내판(1.25×0.4m) 안쪽에 여백을 둔다.
            label.rectTransform.sizeDelta = new Vector2(1.05f, .24f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 1;
            label.fontSizeMax = 1.9f;
        }

        private static void Handrail(Transform parent, Vector3 from, Vector3 to)
        {
            Transform rail = Solid(
                "Handrail",
                parent,
                (from + to) / 2,
                new Vector3(.08f, .08f, Vector3.Distance(from, to)),
                Green,
                "railing"
            );
            rail.localRotation = Quaternion.LookRotation(to - from);
            for (int index = 0; index <= 8; index++)
                Solid(
                    "Baluster",
                    parent,
                    Vector3.Lerp(from, to, index / 8f) - Vector3.up * .45f,
                    new Vector3(.06f, .9f, .06f),
                    Green,
                    "railing"
                );
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
    }
}
