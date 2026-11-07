using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class NpcReviewPresenceTests
    {
        [Test]
        public void OverlappingVisits_DoNotReleaseAnotherOwnerOrAnotherNpc()
        {
            using (var first = new NpcReviewPresence("FLOWER_SHOP_STAFF"))
            using (var second = new NpcReviewPresence("FLOWER_SHOP_STAFF"))
            using (var barber = new NpcReviewPresence("BARBER_STAFF"))
            {
                first.Dispose();
                first.Dispose();
                Assert.That(NpcReviewPresence.IsRequested("FLOWER_SHOP_STAFF"), Is.True);
                second.Dispose();
                Assert.That(NpcReviewPresence.IsRequested("FLOWER_SHOP_STAFF"), Is.False);
                Assert.That(NpcReviewPresence.IsRequested("BARBER_STAFF"), Is.True);
            }
            Assert.That(NpcReviewPresence.IsRequested("BARBER_STAFF"), Is.False);
        }

        [Test]
        public void ReviewVisit_ReservesOffDutyStaffWithoutChangingTheMainMission()
        {
            string mainMissionNpc = NpcDuty.MissionNpcCode;
            try
            {
                NpcDuty.MissionNpcCode = "CAFE_STAFF";
                Assert.That(NpcDuty.IsOffDuty("FLOWER_SHOP_STAFF", "STAFF", "OFF"), Is.True);
                using (var visit = new NpcReviewPresence("FLOWER_SHOP_STAFF"))
                {
                    Assert.That(
                        NpcDuty.IsOffDuty("FLOWER_SHOP_STAFF", "STAFF", "OFF"),
                        Is.False,
                        "복습 중에는 저장한 NPC와 근무지에서 연습할 수 있어야 합니다."
                    );
                    Assert.That(NpcDuty.MissionNpcCode, Is.EqualTo("CAFE_STAFF"));
                    Assert.That(NpcDuty.IsOffDuty("BARBER_STAFF", "STAFF", "OFF"), Is.True);
                }
                Assert.That(NpcDuty.IsOffDuty("FLOWER_SHOP_STAFF", "STAFF", "OFF"), Is.True);
            }
            finally
            {
                NpcDuty.MissionNpcCode = mainMissionNpc;
            }
        }
    }
}
