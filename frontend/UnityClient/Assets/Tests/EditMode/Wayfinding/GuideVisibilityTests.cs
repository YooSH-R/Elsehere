using NUnit.Framework;

namespace A305.Wayfinding.Tests
{
    public class GuideVisibilityTests
    {
        private const float ArrivedMeters = 1.5f;

        [Test]
        public void HeadingToTheDestination_ShowsEntranceAndRoute()
        {
            GuideVisibility view = Decide(metersToEntrance: 20f);

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

        [TestCase(1f, false)]
        [TestCase(ArrivedMeters, false)]
        [TestCase(1.6f, true)]
        public void NearTheEntrance_RouteDisappearsButEntranceKeepsGlowing(
            float metersToEntrance,
            bool showsRoute
        )
        {
            GuideVisibility view = Decide(metersToEntrance: metersToEntrance);

            Assert.That(view.ShowsEntrance, Is.True);
            Assert.That(view.ShowsRoute, Is.EqualTo(showsRoute));
        }

        private static GuideVisibility Decide(
            bool hasEntrance = true,
            bool paused = false,
            bool routeWanted = true,
            float metersToEntrance = 20f
        ) =>
            GuideVisibility.Decide(
                hasEntrance,
                paused,
                routeWanted,
                metersToEntrance,
                ArrivedMeters
            );
    }
}
