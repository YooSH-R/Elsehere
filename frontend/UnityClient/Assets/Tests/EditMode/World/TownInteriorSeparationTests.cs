using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    public sealed class TownInteriorSeparationTests
    {
        private Scene town;
        private Transform buildings;

        [OneTimeSetUp]
        public void OpenTown()
        {
            town = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            buildings = town.GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find("TownMainstreet/BackgroundBuildings");
        }

        // 경찰서는 카페·호텔처럼 따로 장면(PoliceStationScene)이라, 실내 집기는 그 장면의 같은 경찰서 건물에만 있다.
        [Test]
        public void PoliceStationFurnishings_RemainInThePoliceStationSceneOnly()
        {
            Transform building = buildings.Find("PoliceStation");
            Assert.That(
                building.Find("Interior/InteriorLight"),
                Is.Null,
                "저장된 마을 씬에 경찰서 실내 집기가 남아 있습니다."
            );

            Scene station = EditorSceneManager.OpenScene(
                "Assets/Scenes/Production/PoliceStationScene.unity",
                OpenSceneMode.Additive
            );
            try
            {
                Transform stationBuilding = station
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(part => part.name == "PoliceStation" && part.Find("Entrance") != null);
                Assert.That(
                    stationBuilding.Find("Interior/InteriorLight"),
                    Is.Not.Null,
                    "경찰서 장면이 실내 원본을 잃었습니다."
                );
            }
            finally
            {
                EditorSceneManager.CloseScene(station, true);
                SceneManager.SetActiveScene(town);
            }
        }
    }
}
