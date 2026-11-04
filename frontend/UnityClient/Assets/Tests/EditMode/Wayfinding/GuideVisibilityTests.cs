using NUnit.Framework;

namespace A305.Wayfinding.Tests
{
    public class GuideVisibilityTests
    {
        [Test]
        public void HeadingToTheDestination_ShowsEntranceAndRoute()
        {
            GuideVisibility view = Decide();

            Assert.That(view.ShowsEntrance, Is.True);
            Assert.That(view.ShowsRoute, Is.True);
        }

        [Test]
        public void NoEntranceToTheDestinationInThisScene_ShowsNothing()
        {
            GuideVisibility view = Decide(hasEntrance: false);

            Assert.That(view.ShowsEntrance, Is.False);
            Assert.That(view.ShowsRoute, Is.False);
        }

        [Test]
        public void TalkingOrTravelling_ShowsNothing()
        {
            GuideVisibility view = Decide(paused: true);

            Assert.That(view.ShowsEntrance, Is.False);
            Assert.That(view.ShowsRoute, Is.False);
        }

        [Test]
        public void RouteTurnedOff_OnlyTheEntranceGlows()
        {
            GuideVisibility view = Decide(routeWanted: false);

            Assert.That(view.ShowsEntrance, Is.True);
            Assert.That(view.ShowsRoute, Is.False);
        }

        // 도착하면 안내가 할 일을 마쳤으므로 길과 출입문 빛을 모두 거둔다.
        [Test]
        public void Arrived_ShowsNothing()
        {
            GuideVisibility view = Decide(arrived: true);

            Assert.That(view.ShowsEntrance, Is.False);
            Assert.That(view.ShowsRoute, Is.False);
        }

        private static GuideVisibility Decide(
            bool hasEntrance = true,
            bool arrived = false,
            bool paused = false,
            bool routeWanted = true
        ) => GuideVisibility.Decide(hasEntrance, arrived, paused, routeWanted);
    }
}
