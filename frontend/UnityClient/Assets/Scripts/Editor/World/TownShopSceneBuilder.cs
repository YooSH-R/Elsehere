using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.EditorTools.Weather;
using A305.Npc;
using A305.Player;
using A305.Weather;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 마을 매장 실내를 카페·경찰서처럼 따로 장소 장면으로 만든다. 매장 문에 닿으면 마을을 내리고 그 매장 장면만 남는다.
    /// 매장 장면에는 건물 프리팹을 마을과 같은 자리에 두어 직원 자리·여가 방문 지점 좌표를 그대로 쓰고,
    /// 플레이어·실내 카메라·HUD·영업시간·마을로 나가는 문과 그 매장 직원을 둔다.
    /// 마을에는 매장 안 구조(바닥·천장·2층 바닥)와 직원을 남기지 않고, 매장 문마다 매장 장면으로 가는 출입구를 둔다.
    /// 오락실 공용 계단실과 생활용품점 바깥 계단은 2층 가게 문으로 가는 마을 통로라 남긴다.
    /// 이미 만든 장면은 디자인을 두고 연결만 다시 맞춘다.
    /// </summary>
    public static class TownShopSceneBuilder
    {
        public const string MenuPath = "Tools/A305/World/Build Town Shop Scenes";
        public const string Folder = "Assets/Scenes/ShopInteriors";
        private const string BuildingsPath = "TownMainstreet/BackgroundBuildings";
        private const string LifePath = "Assets/Data/Npc/NpcLife.json";
        private const string ShopDoorId = "TownDoor";

        // 건물 바닥 기준 높이. 2층 바닥(4m) 바로 아래라 1층 조명(2.2m)·진열대보다 높고 2층 집기는 모두 이 위에 있다.
        private const float UpperFloorBottom = 3.5f;

        // 매장 안에서 마을로 나가는 문으로 들어오면 문 안쪽 2m에서 매장 안을 보고 선다.
        private static readonly Vector3 InsideArrival = new Vector3(0, 0, 2f);

        // 건물의 바닥·천장·계단. 매장 장면에서는 층과 상관없이 두고, 마을에서는 지운다.
        private static readonly string[] StructureNames =
        {
            "Floor",
            "FloorSurface",
            "UpperFloor",
            "FullUpperFloor",
            "UpperLanding",
            "Ceiling",
            "Stairs",
            "StairDirection",
        };

        // 오락실 공용 계단실. 인도에서 걸어 들어가 2층 서점 문으로 가므로 마을에 남긴다.
        private static readonly string[] StairHallNames =
        {
            "Floor",
            "Ceiling",
            "Stairs",
            "UpperLanding",
            "StairDirection",
        };

        private sealed class Shop
        {
            public Shop(Transform building, Transform door, bool upper)
            {
                Building = building.name;
                Door = door;
                Upper = upper;
                Scene = building.name + (upper ? "UpperInterior" : "Interior");
            }

            public string Building { get; }

            /// <summary>마을 건물에 있는 그 층 매장의 문(여닫이 문짝)</summary>
            public Transform Door { get; }

            public bool Upper { get; }

            public string Scene { get; }

            public string Path => Folder + "/" + Scene + ".unity";

            /// <summary>마을에서 이 매장으로 들어가는 출입구 이름. 매장 장면 이름의 Interior를 Door로 바꾼다.</summary>
            public string TownDoorId => Scene.Replace("Interior", "Door");

            // 2층 서점 문은 공용 계단실 벽에 있어 건물 왼쪽(-x)이 매장이다. 다른 문은 건물 앞면이라 뒤쪽(-z)이 매장이다.
            public Vector3 InwardLocal => Door.name == "BookshopDoor" ? Vector3.left : Vector3.back;
        }

        /// <summary>마을 매장 장면(2층 가게 포함). 매장이 늘어도 빠지지 않게 폴더에서 찾는다.</summary>
        public static string[] ScenePaths() =>
            AssetDatabase
                .FindAssets("t:Scene", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

        /// <summary>건물 1층 매장 장면. 매장 직원은 모두 1층에서 일한다.</summary>
        public static string GroundFloorScenePath(string building) =>
            Folder + "/" + building + "Interior.unity";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;

            NpcLifeData life = NpcLifeData.Parse(
                BuilderTools.LoadRequiredAsset<TextAsset>(LifePath, MenuPath).text
            );
            Scene town = EditorSceneManager.OpenScene(
                TownProductionBuilder.ScenePath,
                OpenSceneMode.Single
            );
            Shop[] shops = FindShops(town);
            foreach (Shop shop in shops)
            {
                BuildShopScene(town, shop, life);
                ConnectTownDoor(town, shop);
            }
            ForgetMovedWorkers(town);
            CommuteThroughShopDoors(town, life);
            BuildingStaffBuilder.RefreshTutorialPassersby(town);
            RemoveTownInteriors(town);
            DaylightBuilder.ConnectTown(
                BuilderTools.FindSingleInScene<WeatherPresenter>(town),
                town
            );
            NpcLifeBuilder.RebakeWalkArea(town);
            WarnMissingReferences(town);
            BuilderTools.SaveScene(town, TownProductionBuilder.ScenePath);

            // 직원에 생활을 붙이고 매장 바닥을 구운 뒤, 직원마다 자기 건물 1층 안에서만 지내게 한다.
            foreach (Shop shop in shops.Where(shop => !shop.Upper))
            {
                NpcLifeBuilder.BuildScene(shop.Path);
                Scene scene = EditorSceneManager.OpenScene(shop.Path, OpenSceneMode.Single);
                foreach (NpcIdentity worker in BuilderTools.FindAllInScene<NpcIdentity>(scene))
                    BuildingStaffBuilder.LimitToGroundFloor(scene, worker.Code, shop.Building);
                BuilderTools.SaveScene(scene, shop.Path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"마을 매장 {shops.Length}곳을 따로 장면으로 만들고 마을 문을 이었습니다.");
        }

        /// <summary>마을 거리 매장과 북쪽 상가의 층별 매장. 문(여닫이 문짝)마다 하나이며 2층 문은 2층 가게다.</summary>
        private static Shop[] FindShops(Scene town)
        {
            var shops = new List<Shop>();
            foreach (Transform building in Buildings(town))
            {
                if (!building.gameObject.activeInHierarchy)
                    continue;
                foreach (
                    ShopSwingDoor door in building.GetComponentsInChildren<ShopSwingDoor>(true)
                )
                {
                    var shop = new Shop(
                        building,
                        door.transform,
                        IsUpperFloor(building, door.transform.position)
                    );
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(shop.Path) == null)
                        throw new InvalidOperationException(
                            $"{shop.Path}가 없습니다. 매장 실내 프리팹(Build Town Shop Interiors)부터 준비합니다."
                        );
                    shops.Add(shop);
                }
            }
            return shops.ToArray();
        }

        private static IEnumerable<Transform> Buildings(Scene town) =>
            town.GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find(BuildingsPath)
                .Cast<Transform>();

        // ---------- 매장 장면 ----------

        private static void BuildShopScene(Scene town, Shop shop, NpcLifeData life)
        {
            Scene scene = EditorSceneManager.OpenScene(shop.Path, OpenSceneMode.Additive);
            // 부모 없이 만드는 조명·환경광은 활성 장면을 따른다.
            SceneManager.SetActiveScene(scene);
            try
            {
                Transform townBuilding = Buildings(town).Single(item => item.name == shop.Building);
                SceneGroups groups = BuilderTools.EnsureGroups(scene);
                Transform building = PlaceBuilding(scene, groups, townBuilding);
                KeepShopFloor(building, shop.Upper);
                SpaceDoorway exit = ConnectShopDoor(building, shop);
                EnsureLight(groups);
                MoveWorkers(town, groups, shop, life);
                SpaceSceneSetup.Apply(scene);
                FirstDayShopBuilder.UseInteriorCamera(scene);
                Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
                player.SetPositionAndRotation(
                    exit.ArrivalPoint.position,
                    exit.ArrivalPoint.rotation
                );
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                // 식당 부스처럼 매장 안 자리에 앉는다. 마을 플레이어와 같게 둔다.
                BuilderTools.GetOrAddComponent<PlayerSeatPose>(player.gameObject);
                NpcIdentity[] workers = BuilderTools.FindAllInScene<NpcIdentity>(scene);
                if (workers.Length > 0)
                    NpcPopulationBuilder.ConnectWorkers(scene, workers);
                BuilderTools.SaveScene(scene, shop.Path);
            }
            finally
            {
                SceneManager.SetActiveScene(town);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // 실내 씬에는 건물 프리팹 하나만 있었다. Environment 아래로 옮기고 마을의 그 건물과 같은 자리에 둔다.
        private static Transform PlaceBuilding(
            Scene scene,
            SceneGroups groups,
            Transform townBuilding
        )
        {
            Transform building = BuilderTools
                .FindAllInScene<Transform>(scene)
                .FirstOrDefault(item =>
                    item.name == townBuilding.name && item.Find("Interior") != null
                );
            if (building == null)
                throw new InvalidOperationException(
                    $"{scene.path}에 {townBuilding.name} 건물이 없습니다."
                );
            if (building.parent != groups.Environment)
                building.SetParent(groups.Environment, false);
            building.SetPositionAndRotation(townBuilding.position, townBuilding.rotation);
            building.localScale = townBuilding.lossyScale;
            return building;
        }

        // 층마다 다른 가게면 그 층 집기만 남긴다. 바닥·천장·계단 같은 구조는 두 층 모두 둔다.
        // 집기는 가장 낮은 곳의 높이로 층을 정하고, 그림이 없는 조명은 자리로 정한다.
        private static void KeepShopFloor(Transform building, bool upper)
        {
            bool hasUpperShop = building
                .GetComponentsInChildren<ShopSwingDoor>(true)
                .Any(door => IsUpperFloor(building, door.transform.position));
            if (!hasUpperShop)
                return;
            foreach (Transform part in building.Find("Interior").Cast<Transform>().ToArray())
            {
                if (StructureNames.Contains(part.name))
                    continue;
                Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
                float bottom =
                    renderers.Length == 0
                        ? building.InverseTransformPoint(part.position).y
                        : renderers.Min(item => building.InverseTransformPoint(item.bounds.min).y);
                if ((bottom >= UpperFloorBottom) != upper)
                    Object.DestroyImmediate(part.gameObject);
            }
        }

        // 매장 안 문턱에 마을로 나가는 출입구를 둔다. 건물의 여닫이 문은 그대로 보이게 하고 출입구 프리팹의 문 그림은 끈다.
        private static SpaceDoorway ConnectShopDoor(Transform building, Shop shop)
        {
            Transform door = FindDoor(building, shop.Door.name);
            SpaceDoorway exit = PlaceDoorway(
                building,
                door,
                "TownDoorway",
                shop.InwardLocal,
                ShopDoorId,
                "TownScene",
                shop.TownDoorId
            );
            exit.ArrivalPoint.localPosition = InsideArrival;
            exit.ArrivalPoint.localRotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(exit.ArrivalPoint);
            return exit;
        }

        private static void EnsureLight(SceneGroups groups)
        {
            if (
                BuilderTools
                    .FindAllInScene<Light>(groups.Scene)
                    .Any(light => light.type == LightType.Directional)
            )
                return;
            // 경찰서 장면과 같은 창빛과 환경광. 밤낮은 InteriorDaylight가 이 빛을 줄이고 늘린다.
            var sun = new GameObject("ShopSun").AddComponent<Light>();
            sun.transform.SetParent(groups.Lighting, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            RenderSettings.ambientLight = new Color(.65f, .65f, .6f);
        }

        // 그 매장(1층)에서 일하는 직원을 마을에서 매장 장면으로 옮긴다. 마을과 같은 자리에 두므로 위치는 그대로다.
        private static void MoveWorkers(Scene town, SceneGroups groups, Shop shop, NpcLifeData life)
        {
            if (shop.Upper)
                return;
            NpcIdentity[] workers = BuilderTools
                .FindAllInScene<NpcIdentity>(town)
                .Where(npc => npc.GetComponent<NpcTownWalker>() == null)
                .Where(npc => PlaceScenes.SceneOf(life.FindNpc(npc.Code)?.Workplace) == shop.Scene)
                .ToArray();
            foreach (NpcIdentity worker in workers)
            {
                worker.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(worker.gameObject, groups.Scene);
                worker.transform.SetParent(groups.Characters, true);
            }
        }

        // ---------- 마을 ----------

        // 마을 매장 문 바깥에 매장 장면으로 가는 출입구를 둔다. 다른 마을 문처럼 문 앞 1m에서 문을 등지고 나온다.
        private static void ConnectTownDoor(Scene town, Shop shop)
        {
            Transform building = Buildings(town).Single(item => item.name == shop.Building);
            SpaceDoorway entrance = PlaceDoorway(
                building,
                shop.Door,
                shop.TownDoorId + "way",
                -shop.InwardLocal,
                shop.TownDoorId,
                shop.Scene,
                ShopDoorId
            );
            TownProductionBuilder.FitDoorToSidewalk(entrance);
        }

        // 옮긴 직원을 가리키던 마을 출퇴근 연결을 비운다. 마을에는 출퇴근하는 모습만 남는다.
        private static void ForgetMovedWorkers(Scene town)
        {
            var population = BuilderTools.FindSingleInScene<NpcPopulation>(town);
            var fields = new SerializedObject(population);
            SerializedProperty residents = fields.FindProperty("residents");
            for (int index = 0; index < residents.arraySize; index++)
            {
                SerializedProperty worker = residents
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("Worker");
                if (worker.objectReferenceValue == null)
                    worker.objectReferenceValue = null;
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // 매장 직원은 출퇴근할 때 실제 매장 문 앞(씬 전환 도착 지점)에서 들어가고 나온다.
        private static void CommuteThroughShopDoors(Scene town, NpcLifeData life)
        {
            SpaceDoorway[] doors = BuilderTools.FindAllInScene<SpaceDoorway>(town);
            var fields = new SerializedObject(BuilderTools.FindSingleInScene<NpcPopulation>(town));
            SerializedProperty residents = fields.FindProperty("residents");
            for (int index = 0; index < residents.arraySize; index++)
            {
                SerializedProperty resident = residents.GetArrayElementAtIndex(index);
                var commuter = (GameObject)
                    resident.FindPropertyRelative("TownPrefab").objectReferenceValue;
                if (commuter == null)
                    continue;
                string workplace = PlaceScenes.SceneOf(
                    life.FindNpc(commuter.GetComponent<NpcIdentity>().Code)?.Workplace
                );
                SpaceDoorway door = doors.FirstOrDefault(item =>
                    PlaceScenes.IsTownShop(item.DestinationScene)
                    && item.DestinationScene == workplace
                );
                if (door != null)
                    resident.FindPropertyRelative("WorkplaceDoor").vector3Value =
                        door.ArrivalPoint.position;
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // 손님이 걷는 마을에는 매장 안 바닥·천장·2층 바닥과 집기를 남기지 않는다. 따로 장면인 경찰서도 같다.
        private static void RemoveTownInteriors(Scene town)
        {
            foreach (Transform building in Buildings(town))
            {
                Transform interior = building.Find("Interior");
                if (interior == null || building.GetComponentInChildren<SpaceDoorway>(true) == null)
                    continue;
                bool stairHall = building.Find("TenantEntrances/StairHallWall") != null;
                foreach (Transform part in interior.Cast<Transform>().ToArray())
                    if (!stairHall || !StairHallNames.Contains(part.name))
                        Object.DestroyImmediate(part.gameObject);
                if (interior.childCount == 0)
                    Object.DestroyImmediate(interior.gameObject);
                Transform upperFloor = building.Find("TenantEntrances/FullUpperFloor");
                if (upperFloor != null)
                    Object.DestroyImmediate(upperFloor.gameObject);
            }
        }

        // 매장 장면으로 옮긴 직원을 가리키던 마을 연결이 남았는지 알린다. 저장하면 빈 참조가 된다.
        private static void WarnMissingReferences(Scene town)
        {
            foreach (Component component in BuilderTools.FindAllInScene<Component>(town))
            {
                if (component == null)
                    continue;
                SerializedProperty property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                {
                    if (
                        property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue == null
                        && property.objectReferenceInstanceIDValue != 0
                    )
                        Debug.LogWarning(
                            $"마을에 사라진 참조가 남았습니다: {component.GetType().Name}.{property.propertyPath} ({component.name})",
                            component
                        );
                }
            }
        }

        // ---------- 공용 ----------

        /// <summary>
        /// 문(여닫이 문짝) 가운데 바닥에 출입구를 둔다. 출입구 앞쪽(+z, 도착 지점 방향)이 <paramref name="facingLocal"/>을 본다.
        /// 문 그림은 끄고 지나가지 못하게 막는 충돌만 남긴다. 이미 있으면 자리와 연결만 맞춘다.
        /// </summary>
        private static SpaceDoorway PlaceDoorway(
            Transform building,
            Transform door,
            string objectName,
            Vector3 facingLocal,
            string doorwayId,
            string destinationScene,
            string destinationDoorwayId
        )
        {
            Transform parent = door.parent;
            Transform existing = parent.Find(objectName);
            GameObject root =
                existing != null
                    ? existing.gameObject
                    : (GameObject)
                        PrefabUtility.InstantiatePrefab(
                            BuilderTools.LoadRequiredAsset<GameObject>(
                                SpaceSceneBuilder.DoorwayPrefabPath,
                                MenuPath
                            ),
                            parent
                        );
            root.name = objectName;
            Vector3 centre = DoorCentreLocal(building, door) + facingLocal * .07f;
            root.transform.SetPositionAndRotation(
                building.TransformPoint(centre),
                Quaternion.LookRotation(building.TransformDirection(facingLocal), Vector3.up)
            );
            var doorway = root.GetComponent<SpaceDoorway>();
            BuilderTools.SetText(doorway, "doorwayId", doorwayId);
            BuilderTools.SetText(doorway, "destinationScene", destinationScene);
            BuilderTools.SetText(doorway, "destinationDoorwayId", destinationDoorwayId);
            foreach (
                Renderer picture in root
                    .transform.Find("Visual")
                    .GetComponentsInChildren<Renderer>(true)
            )
            {
                picture.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(picture);
            }
            return doorway;
        }

        // 문짝 그림의 가운데(건물 기준). 높이는 그 층 바닥이다.
        private static Vector3 DoorCentreLocal(Transform building, Transform door)
        {
            Renderer[] renderers = door.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            Vector3 centre = building.InverseTransformPoint(bounds.center);
            centre.y = building.InverseTransformPoint(door.position).y;
            return centre;
        }

        private static Transform FindDoor(Transform building, string doorName) =>
            building
                .GetComponentsInChildren<ShopSwingDoor>(true)
                .Select(door => door.transform)
                .Single(door => door.name == doorName);

        private static bool IsUpperFloor(Transform building, Vector3 point) =>
            building.InverseTransformPoint(point).y >= UpperFloorBottom;
    }
}
