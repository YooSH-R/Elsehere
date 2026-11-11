using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordScenesTests
    {
        [TestCase("HomeScene", "HOME")]
        [TestCase("TownScene", "TOWN")]
        [TestCase("CafeScene", "CAFE")]
        [TestCase("HotelScene", "HOTEL")]
        [TestCase("ShopScene", "SHOP")]
        [TestCase("PoliceStationScene", "POLICE_STATION")]
        public void TryGetSceneCode_PlaceScene_ReturnsServerSceneCode(
            string unitySceneName,
            string expected
        )
        {
            Assert.That(
                ObjectWordScenes.TryGetSceneCode(unitySceneName, out string sceneCode),
                Is.True
            );
            Assert.That(sceneCode, Is.EqualTo(expected));
        }

        // 마을 매장 실내의 사물 단어는 서버에서 마을(TOWN) 장소로 내보낸다.
        [TestCase("PostOfficeInterior")]
        [TestCase("MapleDinerInterior")]
        [TestCase("TownBankInterior")]
        [TestCase("SudsLaundromatInterior")]
        [TestCase("BloomFlowersInterior")]
        [TestCase("MainStreetBarberInterior")]
        [TestCase("MapleBakeryInterior")]
        [TestCase("LittleArcadeInterior")]
        [TestCase("LittleArcadeUpperInterior")]
        [TestCase("HomeLivingInterior")]
        [TestCase("HomeLivingUpperInterior")]
        public void TryGetSceneCode_TownShopScene_UsesTheTownWords(string unitySceneName)
        {
            Assert.That(
                ObjectWordScenes.TryGetSceneCode(unitySceneName, out string sceneCode),
                Is.True
            );
            Assert.That(sceneCode, Is.EqualTo("TOWN"));
        }

        [Test]
        public void TryGetSceneCode_ReviewScene_HasNoServerScene()
        {
            // 검수용 씬은 서버 장소가 아니므로 단어 카드를 받지 않는다.
            Assert.That(ObjectWordScenes.TryGetSceneCode("ObjectHoverSandbox", out _), Is.False);
        }
    }
}
