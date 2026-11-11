using NUnit.Framework;
using UnityEngine;

namespace A305.Learning.Tests
{
    public class ReviewReturnJourneyTests
    {
        private static ReviewReturnLocation Location(string scene = "SudsLaundromatInterior") =>
            new ReviewReturnLocation(
                scene,
                new Pose(new Vector3(2, .1f, 4), Quaternion.Euler(0, 130, 0)),
                210
            );

        [Test]
        public void PracticeKeepsTheFirstOriginInsteadOfOverwritingItAtTheNpc()
        {
            var journey = new ReviewReturnJourney();
            ReviewReturnLocation origin = Location();
            Assert.That(journey.TryBegin(origin), Is.True);
            Assert.That(journey.TryBegin(Location("CafeScene")), Is.False);
            Assert.That(journey.Origin, Is.SameAs(origin));
            Assert.That(journey.Origin.SceneName, Is.EqualTo("SudsLaundromatInterior"));
            Assert.That(journey.Origin.Pose.position, Is.EqualTo(new Vector3(2, .1f, 4)));
            Assert.That(journey.Origin.ViewYaw, Is.EqualTo(210));
        }

        [Test]
        public void ReturningOnlyClearsTheOriginAfterArrivingInTheSavedScene()
        {
            var journey = new ReviewReturnJourney();
            Assert.That(journey.RequestReturn(), Is.False);
            Assert.That(journey.TryBegin(Location("HomeScene")), Is.True);
            Assert.That(journey.RequestReturn(), Is.True);
            Assert.That(journey.IsReturning, Is.True);
            Assert.That(journey.CompleteReturn("CafeScene"), Is.False);
            Assert.That(journey.Origin, Is.Not.Null);
            Assert.That(journey.CompleteReturn("HomeScene"), Is.True);
            Assert.That(journey.Origin, Is.Null);
            Assert.That(journey.IsReturning, Is.False);
            Assert.That(journey.TryBegin(Location("HotelScene")), Is.True);
        }

        [Test]
        public void RefusedStartCanClearTheSnapshotWithoutLeavingAReturnRequest()
        {
            var journey = new ReviewReturnJourney();
            Assert.That(journey.TryBegin(Location()), Is.True);
            journey.Clear();
            Assert.That(journey.Origin, Is.Null);
            Assert.That(journey.RequestReturn(), Is.False);
            Assert.That(journey.TryBegin(null), Is.False);
        }
    }
}
