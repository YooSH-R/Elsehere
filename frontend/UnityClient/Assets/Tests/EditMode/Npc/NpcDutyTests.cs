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
        public void SharedCounter_IsServedByTheNearestOnDutyStaffWhoIsNotBusy()
        {
            // 카운터 바로 옆이라도 일하는 중이면, 조금 떨어져 있어도 하는 일이 없는 직원이 응대한다.
            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost("CAFE_STAFF", onDuty: true, busy: true, metersFromCounter: .5f),
                    new CounterPost(
                        "CAFE_BARISTA",
                        onDuty: true,
                        busy: false,
                        metersFromCounter: 3f
                    ),
                    new CounterPost(
                        "CAFE_SERVER",
                        onDuty: true,
                        busy: false,
                        metersFromCounter: 1.5f
                    ),
                },
                currentServer: null
            );

            Assert.That(server, Is.EqualTo("CAFE_SERVER"));
        }

        [Test]
        public void SharedCounter_WithEveryoneBusy_IsServedByTheNearestOnDutyStaff()
        {
            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost("CAFE_STAFF", onDuty: true, busy: true, metersFromCounter: 4f),
                    new CounterPost(
                        "CAFE_BARISTA",
                        onDuty: true,
                        busy: true,
                        metersFromCounter: 2f
                    ),
                    new CounterPost(
                        "CAFE_SERVER",
                        onDuty: false,
                        busy: false,
                        metersFromCounter: .3f
                    ),
                },
                currentServer: null
            );

            Assert.That(server, Is.EqualTo("CAFE_BARISTA"));
        }

        [Test]
        public void SharedCounter_KeepsTheStaffAlreadyServing_EvenIfAnotherIsNearer()
        {
            // 응대하러 걸어오는 동안 다른 직원이 더 가까워져도 손님이 떠날 때까지 같은 직원이 응대한다.
            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost(
                        "CAFE_BARISTA",
                        onDuty: true,
                        busy: false,
                        metersFromCounter: 2.5f
                    ),
                    new CounterPost(
                        "CAFE_SERVER",
                        onDuty: true,
                        busy: false,
                        metersFromCounter: .8f
                    ),
                },
                currentServer: "CAFE_BARISTA"
            );

            Assert.That(server, Is.EqualTo("CAFE_BARISTA"));
        }

        [Test]
        public void SharedCounter_WhenTheServingStaffGoesOffDuty_IsServedByAnother()
        {
            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost(
                        "CAFE_BARISTA",
                        onDuty: false,
                        busy: false,
                        metersFromCounter: .5f
                    ),
                    new CounterPost("CAFE_SERVER", onDuty: true, busy: true, metersFromCounter: 3f),
                },
                currentServer: "CAFE_BARISTA"
            );

            Assert.That(server, Is.EqualTo("CAFE_SERVER"));
        }

        [Test]
        public void SharedCounter_WithStaffAsNearAsEachOther_IsServedByTheFirstCodeInOrder()
        {
            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost(
                        "HOTEL_STAFF",
                        onDuty: true,
                        busy: false,
                        metersFromCounter: .6f
                    ),
                    new CounterPost(
                        "HOTEL_CONCIERGE",
                        onDuty: true,
                        busy: false,
                        metersFromCounter: .6f
                    ),
                },
                currentServer: null
            );

            Assert.That(server, Is.EqualTo("HOTEL_CONCIERGE"));
        }

        [Test]
        public void SharedCounter_IsServedByTheMissionNpcBeforeNearerIdleStaff()
        {
            // 미션은 그 NPC와 말해야 이어지므로, 더 가깝고 한가한 직원이 근무 중이어도 미션 NPC가 응대한다.
            NpcDuty.MissionNpcCode = "HOTEL_STAFF";

            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost(
                        "HOTEL_RECEPTIONIST",
                        onDuty: true,
                        busy: false,
                        metersFromCounter: .4f
                    ),
                    new CounterPost("HOTEL_STAFF", onDuty: true, busy: true, metersFromCounter: 5f),
                },
                currentServer: "HOTEL_RECEPTIONIST"
            );

            Assert.That(server, Is.EqualTo("HOTEL_STAFF"));
        }

        [Test]
        public void SharedCounter_WithEveryoneOffDuty_IsNotServed()
        {
            string server = NpcDuty.PickServer(
                new[]
                {
                    new CounterPost(
                        "CAFE_STAFF",
                        onDuty: false,
                        busy: false,
                        metersFromCounter: .6f
                    ),
                },
                currentServer: "CAFE_STAFF"
            );

            Assert.That(server, Is.Null);
        }
    }
}
