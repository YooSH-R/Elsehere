using NUnit.Framework;

namespace A305.World.Tests
{
    public class PlaceScenesTests
    {
        [TestCase("HOME", "HomeScene")]
        [TestCase("TOWN", "TownScene")]
        [TestCase("CAFE", "CafeScene")]
        [TestCase("HOTEL", "HotelScene")]
        [TestCase("SHOP", "ShopScene")]
        [TestCase("POLICE_STATION", "PoliceStationScene")]
        public void SceneOf_Place_ReturnsItsUnityScene(string placeCode, string unitySceneName)
        {
            Assert.That(PlaceScenes.SceneOf(placeCode), Is.EqualTo(unitySceneName));
            Assert.That(PlaceScenes.CodeOf(unitySceneName), Is.EqualTo(placeCode));
        }

        // 마을 매장도 실내마다 따로 씬이다. 마을 문으로 들어가고 나온다.
        [TestCase("POST_OFFICE", "PostOfficeInterior")]
        [TestCase("DINER", "MapleDinerInterior")]
        [TestCase("BANK", "TownBankInterior")]
        [TestCase("LAUNDROMAT", "SudsLaundromatInterior")]
        [TestCase("FLOWER_SHOP", "BloomFlowersInterior")]
        [TestCase("BARBER", "MainStreetBarberInterior")]
        [TestCase("BAKERY", "MapleBakeryInterior")]
        [TestCase("ARCADE", "LittleArcadeInterior")]
        [TestCase("HOME_STORE", "HomeLivingInterior")]
        public void SceneOf_TownShop_ReturnsItsInteriorScene(
            string placeCode,
            string unitySceneName
        )
        {
            Assert.That(PlaceScenes.SceneOf(placeCode), Is.EqualTo(unitySceneName));
            Assert.That(PlaceScenes.CodeOf(unitySceneName), Is.EqualTo(placeCode));
        }

        // 같은 건물 2층 가게(서점·찻집)는 그 건물의 영업시간을 따른다. 길 안내는 1층 매장 문으로 한다.
        [TestCase("LittleArcadeUpperInterior", "ARCADE", "LittleArcadeInterior")]
        [TestCase("HomeLivingUpperInterior", "HOME_STORE", "HomeLivingInterior")]
        public void CodeOf_UpperFloorShop_UsesItsBuildingPlace(
            string unitySceneName,
            string placeCode,
            string groundFloorScene
        )
        {
            Assert.That(PlaceScenes.CodeOf(unitySceneName), Is.EqualTo(placeCode));
            Assert.That(PlaceScenes.SceneOf(placeCode), Is.EqualTo(groundFloorScene));
        }

        [TestCase("PARK")]
        [TestCase("")]
        [TestCase(null)]
        public void SceneOf_PlaceWithoutScene_ReturnsNull(string placeCode)
        {
            Assert.That(PlaceScenes.SceneOf(placeCode), Is.Null);
        }

        [TestCase("EntryScene")]
        [TestCase("")]
        [TestCase(null)]
        public void CodeOf_SceneThatIsNotAPlace_ReturnsNull(string unitySceneName)
        {
            Assert.That(PlaceScenes.CodeOf(unitySceneName), Is.Null);
        }

        [TestCase("HOME", "집")]
        [TestCase("TOWN", "마을")]
        [TestCase("CAFE", "카페")]
        [TestCase("HOTEL", "호텔")]
        [TestCase("SHOP", "드럭스토어")]
        [TestCase("POLICE_STATION", "경찰서")]
        [TestCase("POST_OFFICE", "우체국")]
        [TestCase("DINER", "메이플 식당")]
        [TestCase("BANK", "타운 은행")]
        [TestCase("LAUNDROMAT", "빨래방")]
        [TestCase("FLOWER_SHOP", "꽃집")]
        [TestCase("BARBER", "이발소")]
        [TestCase("BAKERY", "베이커리")]
        [TestCase("ARCADE", "오락실")]
        [TestCase("HOME_STORE", "생활용품점")]
        public void NameOf_Place_ReturnsTheKoreanNameForNotices(string placeCode, string name)
        {
            Assert.That(PlaceScenes.NameOf(placeCode), Is.EqualTo(name));
        }

        [TestCase("PARK")]
        [TestCase(null)]
        public void NameOf_PlaceWithoutScene_ReturnsNull(string placeCode)
        {
            Assert.That(PlaceScenes.NameOf(placeCode), Is.Null);
        }

        // 서버는 마을 매장을 마을(TOWN)의 일부로 본다. 재접속 위치·사물 카드는 서버 장소 코드로 주고받는다.
        [TestCase("TownScene", "TOWN")]
        [TestCase("CafeScene", "CAFE")]
        [TestCase("PoliceStationScene", "POLICE_STATION")]
        [TestCase("PostOfficeInterior", "TOWN")]
        [TestCase("LittleArcadeUpperInterior", "TOWN")]
        public void ServerCodeOf_PlaceScene_ReturnsTheServerPlace(
            string unitySceneName,
            string code
        )
        {
            Assert.That(PlaceScenes.ServerCodeOf(unitySceneName), Is.EqualTo(code));
        }

        [TestCase("EntryScene")]
        [TestCase(null)]
        public void ServerCodeOf_SceneThatIsNotAPlace_ReturnsNull(string unitySceneName)
        {
            Assert.That(PlaceScenes.ServerCodeOf(unitySceneName), Is.Null);
        }

        // 대화를 시작할 때 보내는, 플레이어가 지금 있는 장소. 마을 매장도 따로 장면이라 장면 이름으로 그 매장 코드를 안다.
        [TestCase("SudsLaundromatInterior", "LAUNDROMAT")]
        [TestCase("TownScene", "TOWN")]
        [TestCase("CafeScene", "CAFE")]
        [TestCase("EntryScene", null)]
        public void CodeOf_SceneThePlayerStandsIn_ReturnsThatPlace(
            string unitySceneName,
            string expected
        )
        {
            Assert.That(PlaceScenes.CodeOf(unitySceneName), Is.EqualTo(expected));
        }
    }
}
