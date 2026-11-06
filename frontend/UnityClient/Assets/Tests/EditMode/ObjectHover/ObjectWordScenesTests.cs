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

        [Test]
        public void TryGetSceneCode_ReviewScene_HasNoServerScene()
        {
            // 검수용 씬은 서버 장소가 아니므로 단어 카드를 받지 않는다.
            Assert.That(ObjectWordScenes.TryGetSceneCode("ObjectHoverSandbox", out _), Is.False);
        }
    }
}
