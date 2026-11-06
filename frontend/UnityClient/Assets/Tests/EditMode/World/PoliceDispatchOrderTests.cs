using NUnit.Framework;

namespace A305.World.Tests
{
    public class PoliceDispatchOrderTests
    {
        [SetUp]
        [TearDown]
        public void Clear() => PoliceDispatchOrder.Clear();

        [TestCase("DINER")]
        [TestCase(null)]
        public void AcceptedReport_KeepsItsOfficerAndPlace(string place)
        {
            Assert.That(PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", place), Is.True);
            Assert.That(PoliceDispatchOrder.IsAway("POLICE_OFFICER"), Is.True);
            Assert.That(PoliceDispatchOrder.TryBegin(out var report), Is.True);
            Assert.That(report.TurnId, Is.EqualTo(10));
            Assert.That(report.OfficerCode, Is.EqualTo("POLICE_OFFICER"));
            Assert.That(report.PlaceCode, Is.EqualTo(place));
        }

        [Test]
        public void AdditionalReport_WaitsUntilTheCurrentPatrolReturns()
        {
            PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER");
            PoliceDispatchOrder.TryBegin(out var first);
            Assert.That(
                PoliceDispatchOrder.TryRequest(11, "POLICE_PATROL_OFFICER", "BANK"),
                Is.True
            );
            Assert.That(PoliceDispatchOrder.IsPending, Is.True);
            Assert.That(PoliceDispatchOrder.IsAway("POLICE_PATROL_OFFICER"), Is.True);
            PoliceDispatchOrder.TryBegin(out var stillFirst);
            Assert.That(stillFirst, Is.SameAs(first));

            PoliceDispatchOrder.Complete(10);
            Assert.That(PoliceDispatchOrder.IsAway("POLICE_OFFICER"), Is.False);
            PoliceDispatchOrder.TryBegin(out var second);
            Assert.That(second.TurnId, Is.EqualTo(11));
            Assert.That(second.PlaceCode, Is.EqualTo("BANK"));
        }

        [Test]
        public void SameTurnBeforeDuringAndAfterPatrol_IsAcceptedOnlyOnce()
        {
            PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER");
            Assert.That(PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER"), Is.False);
            PoliceDispatchOrder.TryBegin(out _);
            Assert.That(PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER"), Is.False);
            PoliceDispatchOrder.Complete(10);
            Assert.That(PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER"), Is.False);
            Assert.That(PoliceDispatchOrder.TryBegin(out _), Is.False);
        }

        [Test]
        public void BeginningAgainAfterChangingScenes_KeepsTheUnfinishedReport()
        {
            PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER");
            PoliceDispatchOrder.TryBegin(out var before);
            PoliceDispatchOrder.TryBegin(out var after);
            Assert.That(after, Is.SameAs(before));
            Assert.That(PoliceDispatchOrder.IsAway("POLICE_OFFICER"), Is.True);
        }

        [Test]
        public void CompletingAnotherReport_DoesNotReleaseTheCurrentOfficer()
        {
            PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER");
            PoliceDispatchOrder.TryBegin(out _);
            PoliceDispatchOrder.Complete(11);
            Assert.That(PoliceDispatchOrder.Current.TurnId, Is.EqualTo(10));
            Assert.That(PoliceDispatchOrder.IsAway("POLICE_OFFICER"), Is.True);
        }

        [Test]
        public void NewGame_ClearsCurrentPendingAndDuplicateHistory()
        {
            PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER");
            PoliceDispatchOrder.TryBegin(out _);
            PoliceDispatchOrder.TryRequest(11, "POLICE_PATROL_OFFICER", "BANK");
            PoliceDispatchOrder.Clear();
            Assert.That(PoliceDispatchOrder.IsPending, Is.False);
            Assert.That(PoliceDispatchOrder.TryBegin(out _), Is.False);
            Assert.That(PoliceDispatchOrder.IsAway("POLICE_OFFICER"), Is.False);
            Assert.That(PoliceDispatchOrder.TryRequest(10, "POLICE_OFFICER", "DINER"), Is.True);
        }
    }
}
