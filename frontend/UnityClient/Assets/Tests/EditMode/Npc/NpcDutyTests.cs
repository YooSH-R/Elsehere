using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class NpcDutyTests
    {
        [TearDown]
        public void ReleaseMissionHold()
        {
            NpcDuty.MissionNpcCode = null;
        }

        [Test]
        public void Staff_AtWork_IsOnDuty()
        {
            Assert.That(NpcDuty.IsOffDuty("CAFE_STAFF", "STAFF", "WORK"), Is.False);
        }

        [TestCase("BREAK")]
        [TestCase("OFF")]
        public void Staff_OnBreakOrAfterTheShift_IsOffDuty(string activity)
        {
            Assert.That(NpcDuty.IsOffDuty("CAFE_STAFF", "STAFF", activity), Is.True);
        }

        [TestCase("FREE")]
        [TestCase("OFF")]
        public void Passerby_IsNeverOffDuty(string activity)
        {
            // 행인은 맡은 근무가 없어 근무 외 대화로 보내지 않는다.
            Assert.That(NpcDuty.IsOffDuty("TOWN_PASSERBY", "PASSERBY", activity), Is.False);
        }

        [Test]
        public void Staff_TheFirstDayMissionSendsThePlayerTo_StaysOnDutyOutsideTheShift()
        {
            NpcDuty.MissionNpcCode = "HOTEL_STAFF";

            Assert.That(NpcDuty.IsOffDuty("HOTEL_STAFF", "STAFF", "OFF"), Is.False);
            Assert.That(NpcDuty.IsOffDuty("HOTEL_RECEPTIONIST", "STAFF", "OFF"), Is.True);
        }

        [Test]
        public void SharedCounter_IsServedByTheOnDutyStaffWithTheNearestPost()
        {
            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost("CAFE_BARISTA", onDuty: true, metersFromCounter: .9f),
                    new CounterPost("CAFE_STAFF", onDuty: true, metersFromCounter: .6f),
                    new CounterPost("CAFE_SERVER", onDuty: false, metersFromCounter: .3f),
                }
            );

            Assert.That(server, Is.EqualTo("CAFE_STAFF"));
        }

        [Test]
        public void SharedCounter_WithPostsAsNearAsEachOther_IsServedByTheFirstCodeInOrder()
        {
            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost("HOTEL_STAFF", onDuty: true, metersFromCounter: .6f),
                    new CounterPost("HOTEL_CONCIERGE", onDuty: true, metersFromCounter: .6f),
                }
            );

            Assert.That(server, Is.EqualTo("HOTEL_CONCIERGE"));
        }

        [Test]
        public void SharedCounter_IsServedByTheMissionNpcBeforeNearerStaff()
        {
            // 미션은 그 NPC와 말해야 이어지므로, 같은 카운터에 더 가까운 직원이 근무 중이어도 미션 NPC가 응대한다.
            NpcDuty.MissionNpcCode = "HOTEL_STAFF";

            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost("HOTEL_RECEPTIONIST", onDuty: true, metersFromCounter: .4f),
                    new CounterPost("HOTEL_STAFF", onDuty: true, metersFromCounter: .9f),
                }
            );

            Assert.That(server, Is.EqualTo("HOTEL_STAFF"));
        }

        [Test]
        public void SharedCounter_WithEveryoneOffDuty_IsNotServed()
        {
            string server = NpcDuty.PickServer(
                new[] { new CounterPost("CAFE_STAFF", onDuty: false, metersFromCounter: .6f) }
            );

            Assert.That(server, Is.Null);
        }
    }
}
