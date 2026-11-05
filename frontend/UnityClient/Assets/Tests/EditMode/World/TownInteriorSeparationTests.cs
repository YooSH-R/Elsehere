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

        [TestCase("PostOffice", "PostOfficeInterior")]
        [TestCase("MapleDiner", "MapleDinerInterior")]
        [TestCase("TownBank", "TownBankInterior")]
        [TestCase("SudsLaundromat", "LaundromatInterior")]
        [TestCase("BloomFlowers", "FlowerShopInterior")]
        [TestCase("MainStreetBarber", "BarberShopInterior")]
        [TestCase("MapleBakery", "BakeryInterior")]
        [TestCase("MapleBakery", "BakeryLoungeInterior")]
        [TestCase("LittleArcade", "ArcadeInterior")]
        [TestCase("LittleArcade", "BookshopInterior", "Upper")]
        [TestCase("HomeLiving", "HomeGoodsInterior")]
        [TestCase("HomeLiving", "TeaRoomInterior", "Upper")]
        [TestCase("PoliceStation", "InteriorLight")]
        public void FurnishingsRemainInSeparateSceneOnly(
            string buildingName,
            string furnishingName,
            string floor = ""
        )
        {
            Transform building = buildings.Find(buildingName);
            Assert.That(building, Is.Not.Null, buildingName);
            Assert.That(
                building.Find("Interior/" + furnishingName),
                Is.Null,
                buildingName + ": 저장된 마을 씬에 실내 집기가 남아 있습니다."
            );

            Scene interior = EditorSceneManager.OpenScene(
                $"Assets/Scenes/ShopInteriors/{buildingName}{floor}Interior.unity",
                OpenSceneMode.Additive
            );
            try
            {
                Transform furnishing = interior
                    .GetRootGameObjects()
                    .Single()
                    .transform.Find("Interior/" + furnishingName);
                Assert.That(furnishing, Is.Not.Null, buildingName + ": 실내 원본을 잃었습니다.");
            }
            finally
            {
                EditorSceneManager.CloseScene(interior, true);
                SceneManager.SetActiveScene(town);
            }
        }
    }
}
