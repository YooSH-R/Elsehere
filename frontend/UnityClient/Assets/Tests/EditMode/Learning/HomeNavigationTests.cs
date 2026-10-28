using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class HomeNavigationTests
    {
        [Test]
        public void ReturnFromAnotherPlace_UsesHomeScene()
        {
            string destination = null;
            bool started = HomeNavigation.TryReturn(
                "ShopScene",
                false,
                (scene, doorway) =>
                {
                    destination = scene;
                    Assert.That(doorway, Is.Null);
                    return true;
                }
            );
            Assert.That(started, Is.True);
            Assert.That(destination, Is.EqualTo("HomeScene"));
        }

        [TestCase("HomeScene", false)]
        [TestCase("CafeScene", true)]
        public void BusyOrAlreadyHome_DoesNotLeaveTheScene(string scene, bool busy)
        {
            bool called = false;
            Assert.That(
                HomeNavigation.TryReturn(
                    scene,
                    busy,
                    (_, _) =>
                    {
                        called = true;
                        return true;
                    }
                ),
                Is.False
            );
            Assert.That(called, Is.False);
        }

        [Test]
        public void RejectedTravel_DoesNotReportSuccess()
        {
            Assert.That(HomeNavigation.TryReturn("TownScene", false, (_, _) => false), Is.False);
        }
    }
}
