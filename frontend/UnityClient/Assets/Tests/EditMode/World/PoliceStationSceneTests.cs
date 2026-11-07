using System.Linq;
using A305.EditorTools.Common;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>
    /// 경찰서는 카페·호텔처럼 따로 장면(PoliceStationScene)이다. 마을의 경찰서 문에 닿으면 그 장면으로 옮기고,
    /// 경찰관과 신고 창구는 경찰서 장면에만 있다. 순찰차는 마을에 남아 경찰서에서 받은 신고로 출동한다.
    /// </summary>
    public sealed class PoliceStationSceneTests
    {
        private const string TownPath = "Assets/Scenes/Production/TownScene.unity";
        private const string StationPath = "Assets/Scenes/Production/PoliceStationScene.unity";
        private const string OldInteriorPath =
            "Assets/Scenes/ShopInteriors/PoliceStationInterior.unity";

        private static readonly string[] OfficerCodes =
        {
            "POLICE_OFFICER",
            "POLICE_SERGEANT",
            "POLICE_ROOKIE",
        };

        private Scene town;
        private Scene station;

        [OneTimeSetUp]
        public void OpenProductionScenes()
        {
            town = EditorSceneManager.OpenScene(TownPath);
            station = EditorSceneManager.OpenScene(StationPath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseProductionScenes()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void TownPoliceStationDoor_LeadsToThePoliceStationScene()
        {
            SpaceDoorway door = Doorway(town, "PoliceStationDoor");

            Assert.That(door.DestinationScene, Is.EqualTo("PoliceStationScene"));
            Assert.That(door.DestinationDoorwayId, Is.EqualTo("TownDoor"));
            Assert.That(
                door.GetComponentsInParent<Transform>(true)
                    .Any(part => part.name == "PoliceStation"),
                Is.True,
                "경찰서 건물의 문에 붙어 있어야 합니다."
            );
        }

        // 같은 장면 안에서 실내로 옮기던 예전 출입구(OutsideDoorway·InsideDoorway)는 남지 않는다.
        [Test]
        public void TownPoliceStation_HasNoSameSceneDoorways()
        {
            string[] oldDoorways = town.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(part => part.name is "OutsideDoorway" or "InsideDoorway")
                .Select(part => part.parent.name)
                .ToArray();

            Assert.That(oldDoorways, Is.Empty);
        }

        [Test]
        public void Town_HasNoPoliceOfficers()
        {
            Assert.That(
                BuilderTools
                    .FindAllInScene<NpcIdentity>(town)
                    .Select(npc => npc.Code)
                    .Intersect(OfficerCodes),
                Is.Empty
            );
        }

        [Test]
        public void PoliceStationScene_IsInTheSceneListInsteadOfTheOldInterior()
        {
            string[] scenes = EditorBuildSettings
                .scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(scenes, Does.Contain(StationPath));
            Assert.That(scenes, Does.Not.Contain(OldInteriorPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(OldInteriorPath), Is.Null);
        }

        [Test]
        public void PoliceStationScene_HasTheDoorBackToTown()
        {
            SpaceDoorway door = Doorway(station, "TownDoor");

            Assert.That(door.DestinationScene, Is.EqualTo("TownScene"));
            Assert.That(door.DestinationDoorwayId, Is.EqualTo("PoliceStationDoor"));
        }

        // 누구에게 신고해도 접수되도록 경찰관마다 창구가 있고, 접수 문구에 쓸 장소 이름(순찰차 출동 장소와 같은 목록)을 안다.
        [TestCase("POLICE_OFFICER")]
        [TestCase("POLICE_SERGEANT")]
        [TestCase("POLICE_ROOKIE")]
        public void Officer_WorksInThePoliceStationSceneWithAReportDesk(string code)
        {
            NpcIdentity officer = BuilderTools
                .FindAllInScene<NpcIdentity>(station)
                .SingleOrDefault(npc => npc.Code == code);

            Assert.That(officer, Is.Not.Null, code);
            var desk = officer.GetComponent<PoliceReportDesk>();
            Assert.That(desk, Is.Not.Null, code + "의 신고 창구");
            SerializedProperty destinations = new SerializedObject(desk).FindProperty(
                "destinations"
            );
            string[] names = Enumerable
                .Range(0, destinations.arraySize)
                .Select(index =>
                    destinations
                        .GetArrayElementAtIndex(index)
                        .FindPropertyRelative("displayName")
                        .stringValue
                )
                .ToArray();
            Assert.That(names, Does.Contain("메이플 식당"));
            Assert.That(names, Does.Not.Contain("경찰서"), "경찰서 자신으로는 출동하지 않습니다.");
        }

        // 신고 접수 문구는 택시와 같은 대화 화면 위쪽 상태 판에 보인다.
        [Test]
        public void PoliceStationDialogue_HasTheStatusPlateForReports()
        {
            var panel = BuilderTools.FindSingleInScene<A305.Dialogue.DialoguePanelView>(station);

            Assert.That(
                new SerializedObject(panel).FindProperty("vehicleStatusText").objectReferenceValue,
                Is.Not.Null
            );
        }

        private static SpaceDoorway Doorway(Scene scene, string doorwayId)
        {
            SpaceDoorway door = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .SingleOrDefault(candidate => candidate.DoorwayId == doorwayId);
            Assert.That(door, Is.Not.Null, $"{scene.name}에 {doorwayId} 출입구가 없습니다.");
            return door;
        }
    }
}
