using NUnit.Framework;
using UnityEngine;

namespace A305.Wayfinding.Tests
{
    public class GuideRouteClockTests
    {
        private GuideRouteClock clock;

        [SetUp]
        public void CreateClock()
        {
            clock = new GuideRouteClock(replanMeters: 1f, replanSeconds: .5f, revealSeconds: .6f);
        }

        [Test]
        public void FirstDestination_NeedsAPath()
        {
            Assert.That(clock.NeedsPath("HOTEL", Vector3.zero, 0f), Is.True);
        }

        [Test]
        public void PlayerBarelyMoved_KeepsThePath()
        {
            clock.PathFound("HOTEL", Vector3.zero, 0f);

            Assert.That(clock.NeedsPath("HOTEL", new Vector3(.2f, 0, 0), .1f), Is.False);
        }

        [Test]
        public void PlayerWalkedAMetre_NeedsANewPath()
        {
            clock.PathFound("HOTEL", Vector3.zero, 0f);

            Assert.That(clock.NeedsPath("HOTEL", new Vector3(1f, 0, 0), .1f), Is.True);
        }

        [Test]
        public void HalfASecondPassed_NeedsANewPath()
        {
            clock.PathFound("HOTEL", Vector3.zero, 0f);

            Assert.That(clock.NeedsPath("HOTEL", Vector3.zero, .5f), Is.True);
        }

        [Test]
        public void DestinationChanged_NeedsANewPath()
        {
            clock.PathFound("HOTEL", Vector3.zero, 0f);

            Assert.That(clock.NeedsPath("CAFE", Vector3.zero, .1f), Is.True);
        }

        [Test]
        public void NewRoute_IsLaidFromThePlayerOverRevealSeconds()
        {
            clock.PathFound("HOTEL", Vector3.zero, 1f);

            Assert.That(clock.Revealed(1f), Is.EqualTo(0f));
            Assert.That(clock.Revealed(1.3f), Is.EqualTo(.5f).Within(.001f));
            Assert.That(clock.Revealed(2f), Is.EqualTo(1f));
        }

        [Test]
        public void PathRefreshedOnTheWay_KeepsTheRouteFullyLaid()
        {
            clock.PathFound("HOTEL", Vector3.zero, 0f);
            clock.PathFound("HOTEL", new Vector3(1f, 0, 0), .7f);

            Assert.That(clock.Revealed(.7f), Is.EqualTo(1f));
        }

        [Test]
        public void DestinationChanged_LaysTheRouteAgain()
        {
            clock.PathFound("HOTEL", Vector3.zero, 0f);
            clock.PathFound("CAFE", Vector3.zero, 2f);

            Assert.That(clock.Revealed(2f), Is.EqualTo(0f));
            Assert.That(clock.Revealed(2.3f), Is.EqualTo(.5f).Within(.001f));
        }

        [Test]
        public void RouteWasHidden_LaysTheRouteAgain()
        {
            clock.PathFound("HOTEL", Vector3.zero, 0f);
            clock.Hide();

            Assert.That(clock.NeedsPath("HOTEL", Vector3.zero, .1f), Is.True);
            clock.PathFound("HOTEL", Vector3.zero, 3f);
            Assert.That(clock.Revealed(3f), Is.EqualTo(0f));
        }
    }
}
