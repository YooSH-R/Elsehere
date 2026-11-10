using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>
    /// [이웃]에서 NPC를 눌렀을 때 보이는 지금 위치. 같은 게임 시각이면 장면과 상관없이 같은 상태가 나오도록 일과(<see cref="NpcDayPlan"/>)로 정한다.
    /// </summary>
    public class NpcWhereaboutsTests
    {
        private const double Hour = 60;

        // 10시~18시 근무, 출퇴근은 60분
        private static NpcLifeEntry Staff(string workplace)
        {
            return new NpcLifeEntry
            {
                NpcId = "BAKERY_STAFF",
                Role = "STAFF",
                Scene = "TOWN",
                Workplace = workplace,
                Schedule = new List<NpcScheduleBlock>
                {
                    new NpcScheduleBlock
                    {
                        From = "10:00",
                        To = "18:00",
                        Activity = "WORK",
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

        [TestCase(12 * Hour, "빵집에서 일하고 있어요")]
        [TestCase(9.5 * Hour, "빵집으로 출근하는 길이에요")]
        [TestCase(18 * Hour + 10, "퇴근하는 길이에요")]
        [TestCase(19 * Hour, "마을에서 쉬고 있어요")]
        [TestCase(23 * Hour, "숙소에서 쉬고 있어요")]
        public void Staff_SaysWhatTheyAreDoingByTheSchedule(double minutes, string expected)
        {
            Assert.That(
                NpcWhereabouts.Describe(Staff("BAKERY"), minutes, false),
                Is.EqualTo(expected)
            );
        }

        [Test]
        public void GoingToAPlaceEndingInRieul_UsesRo()
        {
            Assert.That(
                NpcWhereabouts.Describe(Staff("ARCADE"), 9.5 * Hour, false),
                Is.EqualTo("오락실로 출근하는 길이에요")
            );
        }

        [Test]
        public void ShortBreakBetweenShifts_RestsInFrontOfTheWorkplace()
        {
            NpcLifeEntry staff = Staff("CAFE");
            staff.Schedule = new List<NpcScheduleBlock>
            {
                new NpcScheduleBlock
                {
                    From = "08:00",
                    To = "12:00",
                    Activity = "WORK",
                },
                new NpcScheduleBlock
                {
                    From = "12:00",
                    To = "13:00",
                    Activity = "BREAK",
                },
                new NpcScheduleBlock
                {
                    From = "13:00",
                    To = "08:00",
                    Activity = "WORK",
                },
            };

            Assert.That(
                NpcWhereabouts.Describe(staff, 12 * Hour + 20, false),
                Is.EqualTo("모퉁이 카페 앞에서 잠깐 쉬고 있어요")
            );
        }

        [Test]
        public void MissionNeedsTheNpc_StaysAtWorkOutsideTheShift()
        {
            Assert.That(
                NpcWhereabouts.Describe(Staff("BAKERY"), 23 * Hour, true),
                Is.EqualTo("빵집에서 일하고 있어요")
            );
        }

        [TestCase(12 * Hour, "마을을 돌아다니고 있어요")]
        [TestCase(23 * Hour, "집에서 쉬고 있어요")]
        public void Passerby_WandersTheTownOrRestsAtHome(double minutes, string expected)
        {
            NpcLifeEntry passerby = Staff("TOWN");
            passerby.Role = "PASSERBY";
            passerby.Schedule[0].Activity = "FREE";

            Assert.That(NpcWhereabouts.Describe(passerby, minutes, false), Is.EqualTo(expected));
        }

        [TestCase(11 * Hour + 10, "가게에 들르는 중이에요")]
        [TestCase(11 * Hour + 50, "가게에 들렀다 돌아오는 길이에요")]
        public void Passerby_SaysWhenTheyDropIntoShops(double minutes, string expected)
        {
            NpcLifeEntry passerby = Staff("TOWN");
            passerby.Role = "PASSERBY";
            passerby.Schedule[0].Activity = "FREE";

            Assert.That(NpcWhereabouts.Describe(passerby, minutes, false), Is.EqualTo(expected));
        }

        [Test]
        public void TaxiDriverAtWork_IsDriving()
        {
            Assert.That(
                NpcWhereabouts.Describe(Staff("TAXI"), 12 * Hour, false),
                Is.EqualTo("택시를 몰고 있어요")
            );
        }

        [Test]
        public void UnknownWorkplace_SaysWorkplace()
        {
            Assert.That(
                NpcWhereabouts.Describe(Staff("MOON_BASE"), 12 * Hour, false),
                Is.EqualTo("일터에서 일하고 있어요")
            );
        }

        [Test]
        public void WithoutLifeData_SaysItIsUnknown()
        {
            Assert.That(
                NpcWhereabouts.Describe(null, 12 * Hour, false),
                Is.EqualTo("어디 있는지 알 수 없어요")
            );
        }
    }
}
