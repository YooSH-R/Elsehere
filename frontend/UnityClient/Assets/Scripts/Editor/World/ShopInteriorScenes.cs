using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Weather;
using A305.Npc;
using A305.Weather;
using A305.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>건물 프리팹을 편집 원본으로 두고 마을의 집기를 정리하며 Play·빌드 시 실내와 출입구를 구성한다.</summary>
    public sealed class ShopInteriorScenes : IProcessSceneWithReport
    {
        private const string Folder = "Assets/Scenes/ShopInteriors";
        private const string BuildingsPath = "TownMainstreet/BackgroundBuildings";
        private const string UpperSuffix = "UpperInterior";

        // 건물 바닥 기준 높이. 2층 바닥(4m) 바로 아래라 1층 조명(2.2m)·진열대보다 높고 2층 집기는 모두 이 위에 있다.
        private const float UpperFloorBottom = 3.5f;

        public int callbackOrder => 0;

        [MenuItem("Tools/A305/World/Remove Town Interior Furnishings")]
        public static void RemoveTownFurnishings()
        {
            if (!BuilderTools.CanBuild())
                return;

            Scene scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Transform buildings = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find(BuildingsPath);
            int removedObjects = 0;
            foreach (Transform building in buildings)
            {
                // 별도 실내 씬이 있는 건물만 정리하고, 원본 프리팹과 실내 씬은 그대로 둔다.
                string interiorPath = $"{Folder}/{building.name}Interior.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(interiorPath) == null)
                    continue;

                int before = building.GetComponentsInChildren<Transform>(true).Length;
                PartitionInterior(building, keepStructure: true);
                removedObjects += before - building.GetComponentsInChildren<Transform>(true).Length;
            }

            // 실내 조명이 빠진 뒤에는 남은 거리 조명만 밤낮 제어 목록에 연결한다.
            DaylightBuilder.ConnectTown(
                BuilderTools.FindSingleInScene<WeatherPresenter>(scene),
                scene
            );
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("마을 실내 집기 정리를 저장하지 못했습니다.");
            Debug.Log($"Removed {removedObjects} interior objects from the saved TownScene.");
        }

        [MenuItem("Tools/A305/World/Register Shop Interior Scenes")]
        public static void Register()
        {
            if (!BuilderTools.CanBuild())
                return;
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            BuilderTools.EnsureFolder(Folder);
            foreach (
                var prefabPath in AssetDatabase
                    .FindAssets(
                        "t:Prefab",
                        new[] { NorthBlockAssets.Folder, TownStorefrontBuilder.Folder }
                    )
                    .Select(AssetDatabase.GUIDToAssetPath)
            )
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (
                    prefab.GetComponentInChildren<ShopSwingDoor>(true) == null
                    && prefab.GetComponentInChildren<InteriorDoorway>(true) == null
                )
                    continue;
                RegisterScene(prefab, $"{Folder}/{prefab.name}Interior.unity");
                // 2층이 다른 가게면 그 층만 담은 씬을 따로 둔다.
                if (HasUpperShop(prefab.transform))
                    RegisterScene(prefab, $"{Folder}/{prefab.name}{UpperSuffix}.unity");
            }
        }

        private static void RegisterScene(GameObject prefab, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive
                );
                try
                {
                    PrefabUtility.InstantiatePrefab(prefab, scene);
                    if (!EditorSceneManager.SaveScene(scene, path))
                        throw new InvalidOperationException(
                            $"실내 씬을 저장하지 못했습니다: {path}"
                        );
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            BuilderTools.AddToSceneList(path);
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (scene.path.StartsWith(Folder + "/", StringComparison.Ordinal))
            {
                Transform building = scene.GetRootGameObjects().Single().transform;
                // 문은 실내 밖에 있으므로 지우기 전에 층별 가게인지 본다.
                bool hasUpperShop = HasUpperShop(building);
                foreach (Transform child in building.Cast<Transform>().ToArray())
                    if (child.name != "Interior")
                        Object.DestroyImmediate(child.gameObject);
                PartitionInterior(building, keepStructure: false);
                if (hasUpperShop)
                    KeepFloor(
                        building,
                        upper: scene.name.EndsWith(UpperSuffix, StringComparison.Ordinal)
                    );
                return;
            }
            if (scene.path != TownProductionBuilder.ScenePath)
                return;

            Transform buildings = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find(BuildingsPath);
            var staff = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>(true))
                .Where(npc =>
                    npc.Code != null && npc.Code.EndsWith("_STAFF", StringComparison.Ordinal)
                )
                .ToArray();
            foreach (Transform building in buildings)
            {
                if (!building.gameObject.activeInHierarchy)
                    continue;
                var swings = building.GetComponentsInChildren<ShopSwingDoor>(true);
                var pairedDoor = building.Find("Entrance/OutsideDoorway");
                if (
                    swings.Length == 0
                    && (pairedDoor == null || !pairedDoor.GetComponent<InteriorDoorway>())
                )
                    continue;

                bool hasUpperShop = HasUpperShop(building);
                PartitionInterior(building, keepStructure: true);
                // 마을 플레이어·대화 참조는 유지하고 자기 매장을 방문할 때만 직원의 생활과 애니메이션을 켠다.
                // 직원은 모두 1층에 있으므로 2층 가게를 방문할 때는 켜지 않는다.
                var bounds = new Bounds(new Vector3(0, 2, -4), new Vector3(8, 4.2f, 8));
                var occupants = staff
                    .Where(npc =>
                        bounds.Contains(building.InverseTransformPoint(npc.transform.position))
                    )
                    .Select(npc => npc.gameObject)
                    .ToArray();
                ShopInterior room = AddRoom(building, building.name + "Interior", occupants);
                ShopInterior upperRoom = hasUpperShop
                    ? AddRoom(building, building.name + UpperSuffix, Array.Empty<GameObject>())
                    : null;
                foreach (ShopSwingDoor swing in swings)
                {
                    GameObject door = swing.gameObject;
                    bool upperDoor = IsUpperFloor(building, door.transform.position);
                    Object.DestroyImmediate(swing);
                    var sensor = door.GetComponent<BoxCollider>();
                    // 머리 반경 0.68m가 닫힌 문에 닿기 전에 몸통 반경 0.33m가 문턱에 진입한다.
                    sensor.size = new Vector3(sensor.center.x * 2f, sensor.size.y, 1.3f);
                    var config = new SerializedObject(door.AddComponent<ShopDoorway>());
                    config.FindProperty("insideOnForwardSide").boolValue =
                        door.name is "ArcadeDoor" or "BookshopDoor";
                    config.FindProperty("room").objectReferenceValue =
                        upperDoor && upperRoom != null ? upperRoom : room;
                    config.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>
        /// 건물의 장소 코드와 영업시간 안내에 쓰는 이름. 거리 매장은 <see cref="TownStorefrontBuilder.Storefronts"/>를,
        /// 북쪽 상가는 1층 매장을 따른다(2층 서점·찻집은 같은 건물의 영업시간을 쓴다).
        /// </summary>
        private static (string Code, string Name) PlaceOf(string buildingName)
        {
            TownStorefrontBuilder.Storefront storefront =
                TownStorefrontBuilder.Storefronts.FirstOrDefault(shop => shop.Name == buildingName);
            if (storefront != null)
                return (storefront.PlaceCode, storefront.KoreanName);
            switch (buildingName)
            {
                case "MapleBakery":
                    return ("BAKERY", "베이커리");
                case "LittleArcade":
                    return ("ARCADE", "오락실");
                case "HomeLiving":
                    return ("HOME_STORE", "생활용품점");
                default:
                    // 코드가 없는 장소는 늘 열려 있다. Play는 막지 않고 알린다.
                    Debug.LogError(
                        $"{buildingName}의 장소 코드가 없어 늘 열어 둡니다. 영업시간을 찾을 수 있게 ShopInteriorScenes에 더합니다."
                    );
                    return (null, buildingName);
            }
        }

        // 먼저 붙인 실내가 건물의 기본 실내다(GetComponentInParent로 찾는 곳).
        private static ShopInterior AddRoom(
            Transform building,
            string sceneName,
            GameObject[] staff
        )
        {
            ShopInterior room = building.gameObject.AddComponent<ShopInterior>();
            var fields = new SerializedObject(room);
            fields.FindProperty("sceneName").stringValue = sceneName;
            (string placeCode, string placeName) = PlaceOf(building.name);
            fields.FindProperty("placeCode").stringValue = placeCode;
            fields.FindProperty("placeName").stringValue = placeName;
            var list = fields.FindProperty("staff");
            list.arraySize = staff.Length;
            for (int index = 0; index < staff.Length; index++)
            {
                list.GetArrayElementAtIndex(index).objectReferenceValue = staff[index];
                staff[index].SetActive(false);
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            return room;
        }

        // 1층과 2층이 다른 가게면(2층에 따로 문이 있음) 층마다 실내 씬을 나눈다. 매장 안 계단으로 오르는 복층(베이커리)은 나누지 않는다.
        private static bool HasUpperShop(Transform building) =>
            building
                .GetComponentsInChildren<ShopSwingDoor>(true)
                .Any(door => IsUpperFloor(building, door.transform.position));

        private static bool IsUpperFloor(Transform building, Vector3 point) =>
            building.InverseTransformPoint(point).y >= UpperFloorBottom;

        // 층별 실내 씬에는 그 층 집기만 남긴다. 집기는 가장 낮은 곳의 높이로 층을 정하고, 그림이 없는 조명은 자리로 정한다.
        private static void KeepFloor(Transform building, bool upper)
        {
            foreach (Transform part in building.Find("Interior").Cast<Transform>().ToArray())
            {
                Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
                float bottom =
                    renderers.Length == 0
                        ? building.InverseTransformPoint(part.position).y
                        : renderers.Min(renderer =>
                            building.InverseTransformPoint(renderer.bounds.min).y
                        );
                if ((bottom >= UpperFloorBottom) != upper)
                    Object.DestroyImmediate(part.gameObject);
            }
        }

        private static void PartitionInterior(Transform building, bool keepStructure)
        {
            Transform interior = building.Find("Interior");
            foreach (Transform part in interior.Cast<Transform>().ToArray())
            {
                // 거리와 공용 계단에서 밟거나 카메라가 부딪히는 구조물은 마을 씬이 소유한다.
                bool structure =
                    part.name
                    is "Floor"
                        or "FloorSurface"
                        or "UpperFloor"
                        or "FullUpperFloor"
                        or "UpperLanding"
                        or "Ceiling"
                        or "Stairs"
                        or "StairDirection";
                // 경찰관과 순찰차는 거리에서도 신고를 처리하므로 경찰관의 근무 자리도 같은 씬에 둔다.
                bool policeDesk =
                    building.name == "PoliceStation" && part.name is "FrontDesk" or "FrontDeskTop";
                if ((structure || policeDesk) != keepStructure)
                    Object.DestroyImmediate(part.gameObject);
            }
        }
    }
}
