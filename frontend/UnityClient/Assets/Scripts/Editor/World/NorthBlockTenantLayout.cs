using System.Linq;
using A305.EditorTools.Common;
using A305.World;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>복층 매장, 공용 계단 상가, 외부 계단 상가의 출입 동선을 구성한다.</summary>
    internal static class NorthBlockTenantLayout
    {
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

        private static void SharedStairShops(Transform root, Transform interior, Transform layout)
        {
            MoveFrontEntrance(root, false, 2);
            for (int floor = 0; floor < 2; floor++)
            {
                float y = floor * 4;
                Solid(
                    "StairHallWall",
                    layout,
                    new Vector3(0, y + 1.5f, -4.9f),
                    new Vector3(.14f, 3, 6.2f),
                    Cream,
                    "wall"
                );
                Solid(
                    "DoorHeader",
                    layout,
                    new Vector3(0, y + 2.75f, -.9f),
                    new Vector3(.14f, .5f, 1.8f),
                    Cream,
                    "wall"
                );
                Door(
                    layout,
                    floor == 0 ? "ArcadeDoor" : "BookshopDoor",
                    new Vector3(0, y, -1.75f),
                    -90,
                    1.7f
                );
                Text(
                    "TenantDoorSign",
                    floor == 0 ? "1F  LITTLE ARCADE" : "2F  PAPER & LEAF",
                    layout,
                    new Vector3(.12f, y + 2.65f, -.85f),
                    .12f
                ).transform.localRotation = Quaternion.Euler(0, 90, 0);
            }
            Sign(layout, "1F  LITTLE ARCADE", new Vector3(-1.7f, 3.2f, .25f), 3.9f, Navy);
            Sign(layout, "2F  PAPER & LEAF BOOKS", new Vector3(0, 6.5f, .25f), 6.4f, Green);
            Text(
                "Directory",
                "1F  ARCADE\n2F  BOOKSHOP\nSTAIRS  >",
                layout,
                new Vector3(2, 2.9f, .36f),
                .17f
            );
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
            MoveFrontEntrance(root, false, 2.7f);
            MoveFrontEntrance(root, true, 2.7f);
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

        private static void MoveFrontEntrance(Transform root, bool upper, float center)
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
                        child.name.StartsWith("ClosedWindow") && child.localPosition.x > 0
                    )
            )
            {
                window.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(window.gameObject);
            }
            float height = upper ? 3 : 4;
            float left = center - 1.2f;
            float right = center + 1.2f;
            Solid(
                "FrontWall",
                shell,
                new Vector3((left - 4) / 2, height / 2, -.1f),
                new Vector3(left + 4, height, .2f),
                wall,
                "wall"
            );
            if (right < 4)
                Solid(
                    "FrontWall",
                    shell,
                    new Vector3((right + 4) / 2, height / 2, -.1f),
                    new Vector3(4 - right, height, .2f),
                    wall,
                    "wall"
                );
            Solid(
                "DoorHeader",
                shell,
                new Vector3(center, (height + 2.6f) / 2, -.1f),
                new Vector3(2.4f, height - 2.6f, .2f),
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
            if (layout.Find("StairSign") != null)
                return;
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
