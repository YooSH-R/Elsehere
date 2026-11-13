using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    public class ShowcaseStreetRouteTests
    {
        [TestCase("HOTEL_STAFF", true)]
        [TestCase("HOTEL_RECEPTIONIST", true)]
        [TestCase("HOTEL_BELLHOP", true)]
        [TestCase("HOTEL_CONCIERGE", false)]
        [TestCase("HOTEL_NIGHT_STAFF", false)]
        [TestCase("DINER_STAFF", false)]
        public void ReceptionistAndTwoSideStaffStayInTheShowcaseLobby(string code, bool expected)
        {
            Assert.That(ShowcaseStreetRoute.KeepsHotelWorker(code), Is.EqualTo(expected));
        }

        [TestCase("DINER_STAFF", true)]
        [TestCase("TOWN_JOHN_PARK", true)]
        [TestCase("FIRE_CAPTAIN", false)]
        public void ShowcaseRestaurantUsesOneFixedServerAndJohn(string code, bool expected)
        {
            Assert.That(ShowcaseStreetRoute.KeepsRestaurantWorker(code), Is.EqualTo(expected));
        }

        [TestCase("TOWN_GRANDFATHER")]
        [TestCase("TOWN_GRANDMOTHER")]
        public void OnlyTwoResidentsWalkOnTheOppositeSidewalk(string code)
        {
            Assert.That(
                ShowcaseStreetRoute.TryGet(code, out Vector3 start, out Vector3 end),
                Is.True
            );
            Assert.That(start.x, Is.LessThan(-4));
            Assert.That(end.x, Is.EqualTo(start.x));
            Assert.That(Vector3.Distance(start, end), Is.InRange(3, 6));
        }

        [TestCase("TOWN_PASSERBY")]
        [TestCase("HOTEL_STAFF")]
        [TestCase("DINER_STAFF")]
        [TestCase("TAXI_DRIVER")]
        public void OtherResidentsDoNotJoinTheShowcaseStreetWalk(string code)
        {
            Assert.That(ShowcaseStreetRoute.TryGet(code, out _, out _), Is.False);
        }

        [TestCase(true, "TownScene", true)]
        [TestCase(false, "TownScene", false)]
        [TestCase(true, "HotelScene", false)]
        [TestCase(true, "MapleDinerInterior", false)]
        public void OnlyShowcaseOutdoorPopulationIsReduced(
            bool showcase,
            string scene,
            bool expected
        )
        {
            Assert.That(ShowcaseStreetRoute.AppliesTo(showcase, scene), Is.EqualTo(expected));
        }
    }
}
