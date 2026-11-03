using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.Npc;
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
    /// <summary>같은 건물 프리팹을 편집 원본으로 두고 Play·빌드 시 거리와 실내에 필요한 부분만 남긴다.</summary>
    public sealed class ShopInteriorScenes : IProcessSceneWithReport
    {
        private const string Folder = "Assets/Scenes/ShopInteriors";
        private const string BuildingsPath = "TownMainstreet/BackgroundBuildings";
        public int callbackOrder => 0;

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
                string path = $"{Folder}/{prefab.name}Interior.unity";
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
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (scene.path.StartsWith(Folder + "/", StringComparison.Ordinal))
            {
                Transform building = scene.GetRootGameObjects().Single().transform;
                foreach (Transform child in building.Cast<Transform>().ToArray())
                    if (child.name != "Interior")
                        Object.DestroyImmediate(child.gameObject);
                PartitionInterior(building, keepStructure: false);
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

                PartitionInterior(building, keepStructure: true);
                ShopInterior room = building.gameObject.AddComponent<ShopInterior>();
                var fields = new SerializedObject(room);
                fields.FindProperty("sceneName").stringValue = building.name + "Interior";
                (string placeCode, string placeName) = PlaceOf(building.name);
                fields.FindProperty("placeCode").stringValue = placeCode;
                fields.FindProperty("placeName").stringValue = placeName;
                // 마을 플레이어·대화 참조는 유지하고 자기 매장을 방문할 때만 직원의 생활과 애니메이션을 켠다.
                var bounds = new Bounds(new Vector3(0, 2, -4), new Vector3(8, 4.2f, 8));
                var occupants = staff
                    .Where(npc =>
                        bounds.Contains(building.InverseTransformPoint(npc.transform.position))
                    )
                    .ToArray();
                var list = fields.FindProperty("staff");
                list.arraySize = occupants.Length;
                for (int index = 0; index < occupants.Length; index++)
                {
                    GameObject npc = occupants[index].gameObject;
                    list.GetArrayElementAtIndex(index).objectReferenceValue = npc;
                    npc.SetActive(false);
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
                foreach (ShopSwingDoor swing in swings)
                {
                    GameObject door = swing.gameObject;
                    Object.DestroyImmediate(swing);
                    var sensor = door.GetComponent<BoxCollider>();
                    // 머리 반경 0.68m가 닫힌 문에 닿기 전에 몸통 반경 0.33m가 문턱에 진입한다.
                    sensor.size = new Vector3(sensor.center.x * 2f, sensor.size.y, 1.3f);
                    var config = new SerializedObject(door.AddComponent<ShopDoorway>());
                    config.FindProperty("insideOnForwardSide").boolValue =
                        door.name is "ArcadeDoor" or "BookshopDoor";
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
