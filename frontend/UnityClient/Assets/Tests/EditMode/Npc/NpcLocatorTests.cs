using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>
    /// [이웃]에서 고른 NPC를 지도 어디에 찍을지. 마을에 있으면 지금 켜진 몸을 그대로 따라가고,
    /// 몸이 없으면 일과로 일터를 고른다. 숙소에서 쉬거나 걸어 다니는 중이라 자리를 모르면 찍지 않는다.
    /// </summary>
    public class NpcLocatorTests
    {
        private const double Hour = 60;
        private static readonly Vector3 Street = new Vector3(3, 0, 12);

        // 10시~18시 근무, 출퇴근은 60분
        private static NpcLifeEntry Staff(string workplace, string role = "STAFF")
        {
            return new NpcLifeEntry
            {
                NpcId = "BAKERY_STAFF",
                Role = role,
                Scene = "TOWN",
                Workplace = workplace,
                Schedule = new List<NpcScheduleBlock>
                {
                    new NpcScheduleBlock
                    {
                        From = "10:00",
                        To = "18:00",
                        Activity = role == "PASSERBY" ? "FREE" : "WORK",
                    },
                    new NpcScheduleBlock
                    {
                        From = "18:00",
                        To = "10:00",
                        Activity = "OFF",
                    },
                },
            };
        }

        [TestCase(12 * Hour, "근무 중")]
        [TestCase(23 * Hour, "숙소에서 쉬는 중이어도 거리에 몸이 있으면")]
        [TestCase(19 * Hour, "산책 중")]
        public void InTownWithABody_FollowsTheBody(double minutes, string when)
        {
            NpcLocation location = NpcLocator.Locate(
                Staff("BAKERY"),
                minutes,
                false,
                playerInTown: true,
                body: Street
            );

            Assert.That(location.Spot, Is.EqualTo(NpcSpot.Body), when);
            Assert.That(location.BodyPosition, Is.EqualTo(Street));
        }

        [TestCase(true, "마을에서 매장 실내를 아직 불러오지 않음")]
        [TestCase(false, "호텔처럼 다른 장면에 있음")]
        public void WorkingWithoutABody_PointsAtTheWorkplace(bool playerInTown, string when)
        {
            NpcLocation location = NpcLocator.Locate(
                Staff("BAKERY"),
                12 * Hour,
                false,
                playerInTown,
                body: null
            );

            Assert.That(location.Spot, Is.EqualTo(NpcSpot.Workplace), when);
            Assert.That(location.Workplace, Is.EqualTo("BAKERY"));
        }

        [Test]
        public void BodyInAnotherScene_PointsAtTheWorkplaceOnTheTownMap()
        {
            // 호텔 안에서 호텔 직원의 몸을 찾았어도 마을 지도에는 호텔 칸에 찍는다.
            NpcLocation location = NpcLocator.Locate(
                Staff("HOTEL"),
                12 * Hour,
                false,
                playerInTown: false,
                body: Street
            );

            Assert.That(location.Spot, Is.EqualTo(NpcSpot.Workplace));
            Assert.That(location.Workplace, Is.EqualTo("HOTEL"));
        }

        [Test]
        public void ShortBreakInFrontOfTheWorkplace_PointsAtTheWorkplace()
        {
            NpcLifeEntry staff = Staff("BAKERY");
            staff.Schedule = new List<NpcScheduleBlock>
            {
                new NpcScheduleBlock
                {
                    From = "10:00",
                    To = "14:00",
                    Activity = "WORK",
                },
                new NpcScheduleBlock
                {
                    From = "14:00",
                    To = "14:30",
                    Activity = "OFF",
                },
                new NpcScheduleBlock
                {
                    From = "14:30",
                    To = "18:00",
                    Activity = "WORK",
                },
                new NpcScheduleBlock
                {
                    From = "18:00",
                    To = "10:00",
                    Activity = "OFF",
                },
            };

            NpcLocation location = NpcLocator.Locate(staff, 14 * Hour + 15, false, false, null);

            Assert.That(location.Spot, Is.EqualTo(NpcSpot.Workplace));
        }

        [Test]
        public void MissionNeedsThemAtWork_PointsAtTheWorkplaceEvenOffShift()
        {
            NpcLocation location = NpcLocator.Locate(Staff("BAKERY"), 23 * Hour, true, false, null);

            Assert.That(location.Spot, Is.EqualTo(NpcSpot.Workplace));
        }

        [Test]
        public void RestingWithoutABody_IsResting()
        {
            NpcLocation location = NpcLocator.Locate(Staff("BAKERY"), 23 * Hour, false, true, null);

            Assert.That(location.Spot, Is.EqualTo(NpcSpot.Resting));
        }

        [TestCase(9.5 * Hour, "출근하는 길")]
        [TestCase(19 * Hour, "마을에서 쉬는 중")]
        public void WalkingWithoutABody_IsUnknown(double minutes, string when)
        {
            NpcLocation location = NpcLocator.Locate(Staff("BAKERY"), minutes, false, false, null);

            Assert.That(location.Spot, Is.EqualTo(NpcSpot.Unknown), when);
        }

        [TestCase("TOWN", "STAFF", "근무지가 마을 거리")]
        [TestCase("TAXI", "STAFF", "택시 기사")]
        [TestCase("TOWN", "PASSERBY", "행인")]
        public void WanderingWithoutABody_IsUnknown(string workplace, string role, string who)
        {
            NpcLocation location = NpcLocator.Locate(
                Staff(workplace, role),
                12 * Hour,
                false,
                false,
                null
            );

            Assert.That(location.Spot, Is.EqualTo(NpcSpot.Unknown), who);
        }

        [Test]
        public void WithoutLifeData_IsUnknownUnlessTheBodyIsInTown()
        {
            Assert.That(
                NpcLocator.Locate(null, 12 * Hour, false, false, null).Spot,
                Is.EqualTo(NpcSpot.Unknown)
            );
            Assert.That(
                NpcLocator.Locate(null, 12 * Hour, false, true, Street).Spot,
                Is.EqualTo(NpcSpot.Body)
            );
        }
    }
}
