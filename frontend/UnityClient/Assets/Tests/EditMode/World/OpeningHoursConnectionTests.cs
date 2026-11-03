using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.World;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>배포용 장소 씬과 마을 매장이 영업시간 처리에 연결되어 있는지 확인한다.</summary>
    public sealed class OpeningHoursConnectionTests
    {
        private const string PlaceHoursPath = "Assets/Data/World/PlaceHours.json";

        [TearDown]
        public void DiscardOpenedScene()
        {
            // 매장 실내를 나누는 처리는 열어 둔 씬을 바꾸므로 저장하지 않고 닫는다.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TestCase("HomeScene")]
        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        public void ProductionScene_ReadsPlaceHoursAndShowsNoticesForItsPlayer(string name)
        {
            Scene scene = OpenProductionScene(name);
            var controller = BuilderTools.FindSingleInScene<OpeningHoursController>(scene);

            Assert.That(controller, Is.Not.Null, name);
            Assert.That(
                Reference(controller, "placeHours"),
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<TextAsset>(PlaceHoursPath)),
                name
            );
            Assert.That(Reference(controller, "notice"), Is.InstanceOf<HudNoticeView>(), name);
            Assert.That(
                Reference(controller, "player"),
                Is.EqualTo(BuilderTools.FindSingleInScene<PlayerMover>(scene)),
                name
            );
        }

        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        public void PlaceThatCloses_SendsThePlayerOutThroughItsTownDoor(string name)
        {
            Scene scene = OpenProductionScene(name);
            var controller = BuilderTools.FindSingleInScene<OpeningHoursController>(scene);
            Assert.That(controller, Is.Not.Null, name);

            var exit = Reference(controller, "exitToTown") as SpaceDoorway;
            Assert.That(exit, Is.Not.Null, name);
            Assert.That(exit.gameObject.scene, Is.EqualTo(scene), name);
            Assert.That(exit.DestinationScene, Is.EqualTo("TownScene"), name);
        }

        [Test]
        public void EveryTownShop_KnowsItsPlaceCodeHoursAndName()
        {
            Scene scene = OpenProductionScene("TownScene");
            // Play·빌드에서처럼 건물마다 실내를 나누고 ShopInterior를 붙인다.
            new ShopInteriorScenes().OnProcessScene(scene, null);
            PlaceHours hours = PlaceHours.FromJson(
                AssetDatabase.LoadAssetAtPath<TextAsset>(PlaceHoursPath).text
            );

            ShopInterior[] shops = BuilderTools.FindAllInScene<ShopInterior>(scene);
            Assert.That(
                shops.Select(shop => $"{shop.name}={shop.PlaceCode}"),
                Is.EquivalentTo(
                    new[]
                    {
                        "PostOffice=POST_OFFICE",
                        "MapleDiner=DINER",
                        "TownBank=BANK",
                        "SudsLaundromat=LAUNDROMAT",
                        "BloomFlowers=FLOWER_SHOP",
                        "MainStreetBarber=BARBER",
                        "PoliceStation=POLICE_STATION",
                        "MapleBakery=BAKERY",
                        "LittleArcade=ARCADE",
                        "HomeLiving=HOME_STORE",
                    }
                )
            );
            foreach (ShopInterior shop in shops)
            {
                Assert.That(shop.PlaceName, Is.Not.Empty, shop.name);
                Assert.That(hours.HoursText(shop.PlaceCode), Is.Not.Null, shop.name);
            }
        }

        private static Scene OpenProductionScene(string name) =>
            EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");

        private static Object Reference(Object owner, string field) =>
            new SerializedObject(owner).FindProperty(field)?.objectReferenceValue;
    }
}
