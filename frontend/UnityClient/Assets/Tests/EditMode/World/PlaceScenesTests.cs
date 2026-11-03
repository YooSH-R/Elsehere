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
        public void SceneOf_Place_ReturnsItsUnityScene(string placeCode, string unitySceneName)
        {
            Assert.That(PlaceScenes.SceneOf(placeCode), Is.EqualTo(unitySceneName));
            Assert.That(PlaceScenes.CodeOf(unitySceneName), Is.EqualTo(placeCode));
        }

        [TestCase("POLICE_STATION")]
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
    }
}
