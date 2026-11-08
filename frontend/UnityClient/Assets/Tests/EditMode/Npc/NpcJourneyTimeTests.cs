using System.Collections.Generic;
using A305.World;
using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class NpcJourneyTimeTests
    {
        [TestCase(40, 0, 60)]
        [TestCase(80, 0, 120)]
        [TestCase(40, 23, 116)]
        public void RouteLengthAndSignalWaitingDetermineDeparture(double meters, double wait, double minutes) =>
            Assert.That(NpcJourneyTime.LeadMinutes(meters, wait), Is.EqualTo(minutes));

        [Test]
        public void ALongerCommuteStartsEarlierWithoutChangingTheShift()
        {
            var npc = DayShift();
            Assert.That(NpcDayPlan.At(npc, 8 * 60 + 30, false, 120).Phase, Is.EqualTo(NpcDayPhase.GoingToWork));
            Assert.That(NpcDayPlan.At(npc, 8 * 60 + 30, false, 30).Phase, Is.EqualTo(NpcDayPhase.Resting));
            Assert.That(NpcDayPlan.At(npc, 9 * 60 + 45, false, 30).Progress, Is.EqualTo(.5).Within(.001));
            Assert.That(NpcDayPlan.At(npc, 10 * 60, false, 120).Phase, Is.EqualTo(NpcDayPhase.Working));
        }

        [Test]
        public void WaitingAllowanceIsAnArrivalBufferRatherThanSlowerWalking()
        {
            Assert.That(NpcJourneyTime.WalkingProgress(.5, 120, 40), Is.EqualTo(1));
            Assert.That(NpcJourneyTime.WalkingProgress(.25, 120, 40), Is.EqualTo(.5).Within(.001));
        }

        [Test]
        public void AnOutingMustLeaveTimeForActuallyUsingTheFacility()
        {
            Assert.That(NpcJourneyTime.CanVisit(10, 0, 120), Is.True);
            Assert.That(NpcJourneyTime.CanVisit(80, 23, 120), Is.False);
        }

        [TestCase(true, 23)]
        [TestCase(false, 18)]
        public void SignalWaitUsesTheExistingPedestrianStartWindow(bool mainRoad, float seconds) =>
            Assert.That(new TrafficSignalCycle(12, 7, 3).MaximumPedestrianWaitSeconds(mainRoad), Is.EqualTo(seconds));

        private static NpcLifeEntry DayShift() => new NpcLifeEntry
        {
            Schedule = new List<NpcScheduleBlock>
            {
                new NpcScheduleBlock { From = "00:00", To = "10:00", Activity = "OFF" },
                new NpcScheduleBlock { From = "10:00", To = "18:00", Activity = "WORK" },
                new NpcScheduleBlock { From = "18:00", To = "00:00", Activity = "OFF" },
            },
        };
    }
}
