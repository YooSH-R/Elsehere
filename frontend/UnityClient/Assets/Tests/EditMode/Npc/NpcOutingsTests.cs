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
                NpcOutings.UntilOutingEnds(new NpcDayPlan(NpcDayPhase.Leisure, .5), 600),
                Is.EqualTo(645)
            );
        }

        [Test]
        public void AnErrandEndsBeforeTheWayBack()
        {
            Assert.That(
                NpcOutings.UntilOutingEnds(
                    new NpcDayPlan(NpcDayPhase.Leisure, .5, errand: true),
                    600
                ),
                Is.EqualTo(622.5)
            );
        }

        [Test]
        public void ErrandsOnlyGoIntoShops_ButLeisureMayBeAStreetWalk()
        {
            var shop = Venue("BAKERY");
            var walk = new NpcOutingVenue
            {
                Code = "WALK",
                Capacity = 2,
                IsOutdoor = true,
            };
            var errand = new NpcDayPlan(NpcDayPhase.Leisure, 0, errand: true);
            var leisure = new NpcDayPlan(NpcDayPhase.Leisure, 0);

            Assert.That(NpcOutings.Suits(errand, shop), Is.True);
            Assert.That(NpcOutings.Suits(errand, walk), Is.False);
            Assert.That(NpcOutings.Suits(leisure, walk), Is.True);
        }

        [Test]
        public void ArrivingStaysAtMostFortyFiveMinutes()
        {
            var outings = new NpcOutings();
            var venues = new[] { Venue("CAFE") };
            NpcOuting longVisit = outings.Reserve("Ben", "WORK", 100, 200, venues, _ => true);
            NpcOuting shortVisit = outings.Reserve("Mia", "WORK", 100, 120, venues, _ => true);

            longVisit.Arrive(110);
            longVisit.Arrive(150);
            shortVisit.Arrive(110);

            Assert.That(longVisit.Arrived, Is.True);
            Assert.That(longVisit.Until, Is.EqualTo(155), "두 번 도착해도 처음 도착부터 셉니다.");
            Assert.That(shortVisit.Until, Is.EqualTo(120), "남은 여가보다 오래 머물지 않습니다.");
        }

        [Test]
        public void TheNextVisitTheSameDayGoesSomewhereNew()
        {
            var outings = new NpcOutings();
            var venues = new[] { Venue("CAFE"), Venue("BANK"), Venue("BAKERY") };
            var visited = new System.Collections.Generic.List<string>();
            for (int visit = 0; visit < 3; visit++)
            {
                NpcOuting trip = outings.Reserve(
                    "Ben",
                    "WORK",
                    100 + visit * 50,
                    140 + visit * 50,
                    venues,
                    _ => true
                );
                Assert.That(trip, Is.Not.Null, "방문 " + visit);
                Assert.That(visited, Does.Not.Contain(trip.Venue.Code));
                visited.Add(trip.Venue.Code);
                trip.Arrive(110 + visit * 50);
                if (visit < 2)
                    outings.Finish("Ben");
                else
                    trip.ReadyToLeave = true;
            }

            NpcOuting again = outings.Reserve("Ben", "WORK", 300, 340, venues, _ => true);
            Assert.That(
                again.Venue.Code,
                Is.Not.EqualTo(visited[2]),
                "다 들렀으면 방금 걸어 나온 곳만 빼고 다시 고릅니다."
            );
            outings.Release("Ben");
            Assert.That(
                outings.Reserve("Ben", "WORK", 350, 390, venues, venue => venue.Code == visited[2]),
                Is.Null,
                "방금 나온 곳에는 바로 다시 들어가지 않습니다."
            );
        }

        [Test]
        public void APlaceGivenUpOnTheWay_CanBeChosenAgain()
        {
            var outings = new NpcOutings();
            var venues = new[] { Venue("CAFE"), Venue("BANK") };
            NpcOuting first = outings.Reserve("Ben", "WORK", 100, 140, venues, _ => true);
            outings.Release("Ben");

            NpcOuting again = outings.Reserve("Ben", "WORK", 150, 190, venues, _ => true);

            Assert.That(again.Venue.Code, Is.EqualTo(first.Venue.Code));
        }

        [Test]
        public void AShopClosedToVisitors_IsNotChosenButStaysOpenForStaffAndThePlayer()
        {
            // 시연 퀘스트 동안 햄버거 가게에는 직원과 John만 둔다. 여가로 들르는 손님만 막고 가게 자체는 열어 둔다.
            var outings = new NpcOutings();
            var venues = new[] { Venue("DINER"), Venue("CAFE") };
            outings.SetClosedToVisitors("DINER", true);

            for (int i = 0; i < 4; i++)
                Assert.That(
                    outings.Reserve("NPC" + i, "WORK", 100, 200, venues, _ => true)?.Venue.Code,
                    Is.Not.EqualTo("DINER")
                );
            Assert.That(outings.Welcomes("DINER"), Is.False);
            Assert.That(outings.Welcomes("CAFE"), Is.True);
            Assert.That(A305.World.OpeningHours.IsOpenNow("DINER"), Is.True);

            outings.SetClosedToVisitors("DINER", false);
            Assert.That(outings.Welcomes("DINER"), Is.True);
            Assert.That(
                outings.Reserve("Ben", "WORK", 100, 200, new[] { Venue("DINER") }, _ => true),
                Is.Not.Null
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
