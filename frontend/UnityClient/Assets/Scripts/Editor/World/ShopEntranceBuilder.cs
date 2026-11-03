using A305.EditorTools.Common;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.World
{
    /// <summary>기존 문짝과 출입 동작을 유지하면서 양면 손잡이와 문틀을 덧댄다.</summary>
    public static class ShopEntranceBuilder
    {
        private const float LeafWidth = 2.2f;
        private const string DoorPrefab = "Assets/Prefabs/World/MainstreetArt/ShopEntrance.prefab";

        [MenuItem("Tools/A305/World/Fit Shop Entrances")]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;

            foreach (string name in new[] { "LittleArcade", "HomeLiving" })
                BuilderTools.UpdatePrefabIfExists(
                    NorthBlockAssets.Folder + "/" + name + ".prefab",
                    FitEntrances
                );
            foreach (var shop in TownStorefrontBuilder.Storefronts)
                BuilderTools.UpdatePrefabIfExists(
                    TownStorefrontBuilder.Folder + "/" + shop.Name + ".prefab",
                    FitEntrances
                );
            BuilderTools.UpdatePrefabIfExists(SpaceSceneBuilder.DoorwayPrefabPath, FitSpaceDoor);
            AssetDatabase.SaveAssets();

            var drugstore = EditorSceneManager.OpenScene(DrugstoreInteriorBuilder.ScenePath);
            DrugstoreInteriorBuilder.DressEntranceDoor(drugstore);
            EditorSceneManager.SaveScene(drugstore);
            var town = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            foreach (var doorway in BuilderTools.FindAllInScene<SpaceDoorway>(town))
                FitStreetFacade(doorway);
            // 베이커리 실내 프리팹은 별도로 편집하므로 거리의 인스턴스에만 문 장식을 적용한다.
            foreach (var door in BuilderTools.FindAllInScene<ShopSwingDoor>(town))
            {
                Transform ancestor = door.transform;
                while (ancestor != null && ancestor.name != "MapleBakery")
                    ancestor = ancestor.parent;
                if (ancestor != null)
                    FitDoor(door.transform);
            }
            EditorSceneManager.SaveScene(town);
        }

        internal static void FitSpaceDoor(GameObject doorway)
        {
            Transform visual = doorway.transform.Find("Visual");
            if (visual.Find("ShopEntrance") != null)
                return;
            Transform panel = visual.Find("DoorPanel");
            panel.localScale = new Vector3(1.32f, 2.2f, .12f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
            var model = BuilderTools.InstantiatePrefab(
                prefab,
                visual,
                new Vector3(0, 0, panel.localPosition.z),
                Quaternion.identity
            );
            model.transform.localScale = new Vector3(1.32f / LeafWidth, 2.2f / 2.5f, 1);
            DisablePart(model.transform.Find("DoorLeaf"));
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        }

        internal static void FitStreetFacade(SpaceDoorway door)
        {
            var visual = door.transform.Find("Visual");
            visual.localScale = new Vector3(1.5f, 1f, 1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }

        private static void FitEntrances(GameObject building)
        {
            foreach (var door in building.GetComponentsInChildren<ShopSwingDoor>(true))
                FitDoor(door.transform);
            Transform policeDoor = building.transform.Find("Entrance/PoliceStationDoor");
            if (policeDoor != null)
                FitDoor(policeDoor);
        }

        private static void FitDoor(Transform door)
        {
            Transform hinge = door.Find("Hinge");
            if (hinge == null || door.Find("EntranceFrame") != null)
                return;
            Transform leaf = hinge.Find("DoorLeaf");
            float width = leaf.GetComponent<BoxCollider>().size.x;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
            // 문틀은 고정하고 손잡이는 기존 힌지 아래에 둬서 여닫힐 때 문짝과 함께 움직인다.
            // 기존 문짝과 충돌체는 출입 동작에서 참조하므로 그대로 유지한다.
            var frame = BuilderTools.InstantiatePrefab(
                prefab,
                door,
                new Vector3(width / 2, 0, 0),
                Quaternion.identity
            );
            frame.name = "EntranceFrame";
            frame.transform.localScale = new Vector3(width / LeafWidth, 1, 1);
            DisablePart(frame.transform.Find("DoorLeaf"));
            DisablePart(frame.transform.Find("Front"));
            DisablePart(frame.transform.Find("Back"));
            PrefabUtility.RecordPrefabInstancePropertyModifications(frame.transform);

            var trim = BuilderTools.InstantiatePrefab(
                prefab,
                hinge,
                new Vector3(width / 2, 0, 0),
                Quaternion.identity
            );
            trim.name = "EntranceTrim";
            trim.transform.localScale = new Vector3(width / LeafWidth, 1, 1);
            foreach (Transform part in trim.transform)
                if (part.name != "Front" && part.name != "Back")
                    DisablePart(part);
            PrefabUtility.RecordPrefabInstancePropertyModifications(trim.transform);
            DisablePart(hinge.Find("DoorGlass"));
            DisablePart(hinge.Find("Handle"));
        }

        private static void DisablePart(Transform part)
        {
            if (part == null)
                return;
            part.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
        }
    }
}
