using System.Linq;
using A305.Npc;
using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class NpcOutingsTests
    {
        [Test]
        public void ReservationsIncludePeopleStillWalkingAndNeverExceedCapacity()
        {
            var outings = new NpcOutings();
            var venues = new[] { Venue("CAFE"), Venue("SHOP"), Venue("BANK"), Venue("WALK") };
            for (int i = 0; i < 8; i++)
                Assert.That(
                    outings.Reserve("NPC" + i, "WORK", 100, 200, venues, _ => true),
                    Is.Not.Null
                );
            Assert.That(
                outings.Trips.GroupBy(trip => trip.Venue.Code).All(group => group.Count() == 2),
                Is.True
            );
            Assert.That(outings.Reserve("EXTRA", "WORK", 100, 200, venues, _ => true), Is.Null);
        }

        [Test]
        public void AFullClosedOrOwnWorkplaceIsNotChosen()
        {
            var outings = new NpcOutings();
            var venues = new[] { Venue("WORK"), Venue("CLOSED"), Venue("CAFE"), Venue("WALK") };
            var chosen = outings.Reserve(
                "Ben",
                "WORK",
                100,
                200,
                venues,
                venue => venue.Code != "CLOSED"
            );
            Assert.That(chosen, Is.Not.Null);
            Assert.That(new[] { "CAFE", "WALK" }, Does.Contain(chosen.Venue.Code));
            Assert.That(
                outings.Reserve("Ben", "WORK", 110, 200, venues, _ => true),
                Is.SameAs(chosen)
            );
            outings.Release("Ben");
            Assert.That(outings.Find("Ben"), Is.Null);
        }

        [Test]
        public void ExistingWorkAndShortBreakSchedulesAreProtected()
        {
            Assert.That(NpcOutings.CanGoOut(new NpcDayPlan(NpcDayPhase.Working)), Is.False);
            Assert.That(NpcOutings.CanGoOut(new NpcDayPlan(NpcDayPhase.GoingToWork)), Is.False);
            Assert.That(
                NpcOutings.CanGoOut(new NpcDayPlan(NpcDayPhase.LeavingWork, .5, true)),
                Is.False
            );
            Assert.That(NpcOutings.CanGoOut(new NpcDayPlan(NpcDayPhase.Leisure, .5)), Is.True);
            Assert.That(
                NpcOutings.UntilHome(new NpcDayPlan(NpcDayPhase.Leisure, .5), 600),
                Is.EqualTo(645)
            );
        }

        private static NpcOutingVenue Venue(string code) =>
            new NpcOutingVenue { Code = code, Capacity = 2 };

        [Test]
        public void AGuestWalkingToTheExitStillCountsTowardsCapacity()
        {
            var outings = new NpcOutings();
            var venues = new[]
            {
                new NpcOutingVenue { Code = "BANK", Capacity = 1 },
            };
            NpcOuting first = outings.Reserve("First", "WORK", 100, 200, venues, _ => true);
            first.Arrived = true;
            Assert.That(outings.Reserve("Second", "WORK", 201, 300, venues, _ => true), Is.Null);
            first.ReadyToLeave = true;
            Assert.That(
                outings.Reserve("Second", "WORK", 201, 300, venues, _ => true),
                Is.Not.Null
            );
        }
    }
}
