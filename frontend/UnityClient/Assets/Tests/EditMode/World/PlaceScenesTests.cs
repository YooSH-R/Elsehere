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

        // 거리 매장(다이너 등)은 마을 건물 안이라 따로 씬이 없다.
        [TestCase("DINER")]
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
        public void NameOf_Place_ReturnsTheKoreanNameForNotices(string placeCode, string name)
        {
            Assert.That(PlaceScenes.NameOf(placeCode), Is.EqualTo(name));
        }

        [TestCase("DINER")]
        [TestCase(null)]
        public void NameOf_PlaceWithoutScene_ReturnsNull(string placeCode)
        {
            Assert.That(PlaceScenes.NameOf(placeCode), Is.Null);
        }
    }
}
