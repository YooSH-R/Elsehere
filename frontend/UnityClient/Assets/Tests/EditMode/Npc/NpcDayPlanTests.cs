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

        private static NpcLifeEntry FreeFromTenToSix
        {
            get
            {
                var npc = DayShift;
                npc.Role = "PASSERBY";
                npc.Schedule[1].Activity = "FREE";
                return npc;
            }
        }

        // 자유 시간 10~18시: 홀수 시 정각부터 한 시간은 가게에 들렀다가 끝 15분에 제자리로 돌아온다.
        [TestCase(10, 30, NpcDayPhase.Working, false)]
        [TestCase(11, 10, NpcDayPhase.Leisure, true)]
        [TestCase(11, 50, NpcDayPhase.GoingToWork, true)]
        [TestCase(12, 30, NpcDayPhase.Working, false)]
        [TestCase(17, 20, NpcDayPhase.Leisure, true)]
        [TestCase(18, 10, NpcDayPhase.LeavingWork, false)]
        public void Passerby_DropsIntoAShopEveryOtherHourOfFreeTime(
            int hour,
            int minute,
            NpcDayPhase phase,
            bool errand
        )
        {
            NpcDayPlan plan = NpcDayPlan.At(FreeFromTenToSix, hour * 60 + minute, false);

            Assert.That(plan.Phase, Is.EqualTo(phase));
            Assert.That(plan.IsErrand, Is.EqualTo(errand));
        }

        // Ben처럼 홀수 시에 자유 시간이 시작하면 첫 한 시간은 제자리 근처를 걷는다.
        [TestCase(9, 30, NpcDayPhase.Working, false)]
        [TestCase(10, 0, NpcDayPhase.Working, false)]
        [TestCase(11, 10, NpcDayPhase.Leisure, true)]
        [TestCase(12, 15, NpcDayPhase.Working, false)]
        public void PasserbyStartingOnAnOddHour_StrollsBeforeTheFirstShop(
            int hour,
            int minute,
            NpcDayPhase phase,
            bool errand
        )
        {
            var npc = FreeFromTenToSix;
            npc.Schedule[0].To = "09:00";
            npc.Schedule[1].From = "09:00";

            NpcDayPlan plan = NpcDayPlan.At(npc, hour * 60 + minute, false);

            Assert.That(plan.Phase, Is.EqualTo(phase));
            Assert.That(plan.IsErrand, Is.EqualTo(errand));
        }

        [Test]
        public void PasserbyErrand_ProgressesThroughTheShopVisitAndTheWayBack()
        {
            Assert.That(
                NpcDayPlan.At(FreeFromTenToSix, 11 * 60 + 15, false).Progress,
                Is.EqualTo(1d / 3).Within(.001)
            );
            Assert.That(
                NpcDayPlan.At(FreeFromTenToSix, 11 * 60 + 50, false).Progress,
                Is.EqualTo(1d / 3).Within(.001)
            );
        }

        [Test]
        public void Passerby_SkipsAnErrandThatWouldRunPastFreeTime()
        {
            var npc = FreeFromTenToSix;
            npc.Schedule[1].To = "17:30";
            npc.Schedule[2].From = "17:30";

            NpcDayPlan plan = NpcDayPlan.At(npc, 17 * 60 + 10, false);

            Assert.That(plan.Phase, Is.EqualTo(NpcDayPhase.Working));
            Assert.That(plan.IsErrand, Is.False);
        }

        [Test]
        public void PasserbyNeededForMission_StaysOutOfShops()
        {
            NpcDayPlan plan = NpcDayPlan.At(FreeFromTenToSix, 11 * 60 + 10, true);

            Assert.That(plan.Phase, Is.EqualTo(NpcDayPhase.Working));
            Assert.That(plan.IsErrand, Is.False);
        }

        [Test]
        public void StaffLeisureAfterWork_IsNotAnErrand()
        {
            Assert.That(NpcDayPlan.At(DayShift, 19 * 60, false).IsErrand, Is.False);
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

        [TestCase(9 * 60 + 30, 10 * 60)]
        [TestCase(12 * 60, 1440 + 10 * 60)]
        [TestCase(1440 + 9 * 60, 1440 + 10 * 60)]
        public void NextWorkStart_IsTheStartOfTheShiftAhead(int minutes, int expected)
        {
            Assert.That(NpcDayPlan.NextWorkStart(DayShift, minutes), Is.EqualTo((double)expected));
        }

        [Test]
        public void NextWorkStart_CrossesMidnightForANightShift()
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
            Assert.That(NpcDayPlan.NextWorkStart(npc, 21 * 60 + 30), Is.EqualTo(22.0 * 60));
            Assert.That(NpcDayPlan.NextWorkStart(npc, 23 * 60), Is.EqualTo(1440.0 + 22 * 60));
        }

        [Test]
        public void NextWorkStart_IsNullForAPasserbyWithoutWork()
        {
            var npc = DayShift;
            npc.Role = "PASSERBY";
            npc.Schedule[1].Activity = "FREE";
            Assert.That(NpcDayPlan.NextWorkStart(npc, 9 * 60), Is.Null);
        }
    }
}
