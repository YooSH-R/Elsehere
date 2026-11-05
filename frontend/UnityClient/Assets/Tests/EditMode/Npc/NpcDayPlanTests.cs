using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class NpcDayPlanTests
    {
        private static NpcLifeEntry DayShift =>
            new NpcLifeEntry
            {
                Schedule = new List<NpcScheduleBlock>
                {
                    new NpcScheduleBlock
                    {
                        From = "00:00",
                        To = "10:00",
                        Activity = "OFF",
                    },
                    new NpcScheduleBlock
                    {
                        From = "10:00",
                        To = "18:00",
                        Activity = "WORK",
                    },
                    new NpcScheduleBlock
                    {
                        From = "18:00",
                        To = "00:00",
                        Activity = "OFF",
                    },
                },
            };

        [TestCase(8, 0, NpcDayPhase.Resting)]
        [TestCase(9, 30, NpcDayPhase.GoingToWork)]
        [TestCase(10, 0, NpcDayPhase.Working)]
        [TestCase(18, 0, NpcDayPhase.LeavingWork)]
        [TestCase(19, 0, NpcDayPhase.Leisure)]
        [TestCase(20, 15, NpcDayPhase.GoingHome)]
        [TestCase(21, 0, NpcDayPhase.Resting)]
        public void Shift_DeterminesWorkTravelAndPrivateRest(
            int hour,
            int minute,
            NpcDayPhase phase
        )
        {
            Assert.That(
                NpcDayPlan.At(DayShift, hour * 60 + minute, false).Phase,
                Is.EqualTo(phase)
            );
        }

        [Test]
        public void Passerby_IsInTownDuringFreeTimeAndRestsAtDormAtNight()
        {
            var npc = DayShift;
            npc.Role = "PASSERBY";
            npc.Schedule[1].Activity = "FREE";
            Assert.That(NpcDayPlan.At(npc, 12 * 60, false).Phase, Is.EqualTo(NpcDayPhase.Working));
            Assert.That(NpcDayPlan.At(npc, 2 * 60, false).Phase, Is.EqualTo(NpcDayPhase.Resting));
        }

        [Test]
        public void StartingAnotherDay_KeepsCommuteProgress()
        {
            var first = NpcDayPlan.At(DayShift, 570, false);
            var next = NpcDayPlan.At(DayShift, 1440 + 570, false);
            Assert.That(first.Progress, Is.EqualTo(.5).Within(.02));
            Assert.That(next.Progress, Is.EqualTo(first.Progress));
        }

        [Test]
        public void MissionStaff_StaysAvailableDuringOffHours()
        {
            Assert.That(NpcDayPlan.At(DayShift, 0, true).Phase, Is.EqualTo(NpcDayPhase.Working));
        }

        [Test]
        public void NightShift_GoesToWorkBeforeMidnightAndLeavesInMorning()
        {
            var npc = new NpcLifeEntry
            {
                Schedule = new List<NpcScheduleBlock>
                {
                    new NpcScheduleBlock
                    {
                        From = "22:00",
                        To = "06:00",
                        Activity = "WORK",
                    },
                    new NpcScheduleBlock
                    {
                        From = "06:00",
                        To = "22:00",
                        Activity = "OFF",
                    },
                },
            };
            Assert.That(
                NpcDayPlan.At(npc, 21 * 60 + 30, false).Phase,
                Is.EqualTo(NpcDayPhase.GoingToWork)
            );
            Assert.That(NpcDayPlan.At(npc, 2 * 60, false).Phase, Is.EqualTo(NpcDayPhase.Working));
            Assert.That(
                NpcDayPlan.At(npc, 6 * 60 + 10, false).Phase,
                Is.EqualTo(NpcDayPhase.LeavingWork)
            );
        }
    }
}
