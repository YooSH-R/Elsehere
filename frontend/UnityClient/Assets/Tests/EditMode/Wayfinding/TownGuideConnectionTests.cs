using A305.EditorTools.Common;
using A305.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace A305.Wayfinding.Tests
{
    /// <summary>새 경찰서 장소의 길 안내가 경찰서 출입문 앞을 찾는지 확인한다.</summary>
    public sealed class TownGuideConnectionTests
    {
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";

        // 모든 검사가 마을 씬을 읽기만 하므로 한 번만 연다.
        private Scene scene;

        [OneTimeSetUp]
        public void OpenTown()
        {
            scene = EditorSceneManager.OpenScene(TownScenePath);
        }

        [TestCase("POLICE_STATION", "PoliceStationDoor")]
        public void EveryTownPlace_HasItsDoorFrontAsTheEntrance(string placeCode, string doorwayId)
        {
            SpaceDoorway door = System.Array.Find(
                BuilderTools.FindAllInScene<SpaceDoorway>(scene),
                doorway => doorway.DoorwayId == doorwayId
            );

            Assert.That(
                DestinationGuide.FindEntrance(scene, placeCode),
                Is.SameAs(door.ArrivalPoint)
            );
        }
    }
}
