using NUnit.Framework;

namespace A305.Npc.Tests
{
    // 일터 장면(매장·카페·호텔 등)의 직원은 근무하러 올 때 문으로 들어오고, 근무가 끝나면 문으로 걸어 나간다.
    public class NpcWorkplaceDoorTests
    {
        [Test]
        public void ArrivingWhileThePlayerIsInside_EntersThroughTheDoor()
        {
            Assert.That(
                NpcWorkplaceDoor.Decide(true, false, true, NpcDayPhase.GoingToWork, false),
                Is.EqualTo(NpcWorkplaceStep.EnterThroughDoor)
            );
        }

        // 장면을 처음 열 때는 이미 일하던 직원이 자기 자리에 있다.
        [Test]
        public void OpeningTheSceneDuringTheShift_ShowsStaffAtTheirPost()
        {
            Assert.That(
                NpcWorkplaceDoor.Decide(true, false, false, NpcDayPhase.Working, false),
                Is.EqualTo(NpcWorkplaceStep.ShowAtPost)
            );
        }

        [Test]
        public void StaffAtWork_Stay()
        {
            Assert.That(
                NpcWorkplaceDoor.Decide(true, true, true, NpcDayPhase.Working, false),
                Is.EqualTo(NpcWorkplaceStep.Stay)
            );
        }

        [Test]
        public void ShiftEndWhileThePlayerIsInside_WalksOutThroughTheDoor()
        {
            Assert.That(
                NpcWorkplaceDoor.Decide(false, true, true, NpcDayPhase.LeavingWork, false),
                Is.EqualTo(NpcWorkplaceStep.WalkOut)
            );
            Assert.That(
                NpcWorkplaceDoor.Decide(false, true, true, NpcDayPhase.LeavingWork, true),
                Is.EqualTo(NpcWorkplaceStep.Hide),
                "문에 닿으면 사라진다."
            );
        }

        // 시계를 크게 건너뛰었거나 장면을 연 직후에는 걸어 나가지 않고 바로 일과의 자리로 옮긴다.
        [TestCase(false, NpcDayPhase.LeavingWork)]
        [TestCase(true, NpcDayPhase.Resting)]
        public void NoLongerNeededWithoutAContinuousShiftEnd_HidesAtOnce(
            bool continuous,
            NpcDayPhase phase
        )
        {
            Assert.That(
                NpcWorkplaceDoor.Decide(false, true, continuous, phase, false),
                Is.EqualTo(NpcWorkplaceStep.Hide)
            );
        }

        [Test]
        public void OffDutyStaffNotInTheScene_StayHidden()
        {
            Assert.That(
                NpcWorkplaceDoor.Decide(false, false, true, NpcDayPhase.Resting, false),
                Is.EqualTo(NpcWorkplaceStep.Hide)
            );
        }
    }
}
