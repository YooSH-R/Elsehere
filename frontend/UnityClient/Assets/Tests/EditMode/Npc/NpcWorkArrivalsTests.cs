using System.Collections.Generic;
using System.Reflection;
using A305.World;
using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class NpcWorkArrivalsTests
    {
        private static NpcLifeEntry BankClerk =>
            new NpcLifeEntry
            {
                NpcId = "BANK_STAFF",
                Role = "STAFF",
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

        [SetUp]
        public void SetUp() => ResetSharedClock();

        [TearDown]
        public void TearDown() => ResetSharedClock();

        [Test]
        public void AnArrivalCountsOnlyForTheShiftItWasRecordedFor()
        {
            NpcWorkArrivals.MarkArrived("BANK_STAFF", 600);

            Assert.That(NpcWorkArrivals.HasArrived("BANK_STAFF", 600), Is.True);
            Assert.That(NpcWorkArrivals.HasArrived("BANK_STAFF", 1440 + 600), Is.False);
            Assert.That(NpcWorkArrivals.HasArrived("POST_OFFICE_STAFF", 600), Is.False);
        }

        [Test]
        public void ANewGameClockForgetsArrivals()
        {
            NpcWorkArrivals.MarkArrived("BANK_STAFF", 600);
            ResetSharedClock();

            Assert.That(NpcWorkArrivals.HasArrived("BANK_STAFF", 600), Is.False);
        }

        [Test]
        public void StaffWhoWalkedInEarlyAreAtWorkBeforeTheShiftStarts()
        {
            double beforeShift = 9 * 60 + 40;
            NpcDayPlan plan = NpcDayPlan.At(BankClerk, beforeShift, false);
            Assert.That(plan.Phase, Is.EqualTo(NpcDayPhase.GoingToWork), "준비: 출근하는 시각");
            Assert.That(NpcWorkArrivals.IsAtWork(BankClerk, plan, beforeShift), Is.False);

            NpcWorkArrivals.MarkArrived("BANK_STAFF", 600);

            Assert.That(NpcWorkArrivals.IsAtWork(BankClerk, plan, beforeShift), Is.True);
        }

        [Test]
        public void YesterdaysEarlyArrivalDoesNotKeepStaffInsideBeforeTodaysShift()
        {
            NpcWorkArrivals.MarkArrived("BANK_STAFF", 600);
            double nextMorning = 1440 + 9 * 60 + 40;

            Assert.That(
                NpcWorkArrivals.IsAtWork(
                    BankClerk,
                    NpcDayPlan.At(BankClerk, nextMorning, false),
                    nextMorning
                ),
                Is.False
            );
        }

        [Test]
        public void StaffOnShiftAreAtWorkWithoutAnArrival()
        {
            double midShift = 12 * 60;

            Assert.That(
                NpcWorkArrivals.IsAtWork(
                    BankClerk,
                    NpcDayPlan.At(BankClerk, midShift, false),
                    midShift
                ),
                Is.True
            );
        }

        // 일터 장면에 있는 플레이어에게는 직원이 근무 10분 전에 문으로 들어온다.
        [TestCase(9 * 60 + 50, true)]
        [TestCase(9 * 60 + 49, false)]
        [TestCase(12 * 60, true)]
        [TestCase(18 * 60 + 10, false)]
        public void StaffComeInsideTenMinutesBeforeTheShift(int minutes, bool inside)
        {
            Assert.That(
                NpcWorkArrivals.ShouldBeInside(
                    BankClerk,
                    NpcDayPlan.At(BankClerk, minutes, false),
                    minutes
                ),
                Is.EqualTo(inside)
            );
        }

        [Test]
        public void StaffWhoWalkedInEarlierAreInsideBeforeTheTenMinuteWindow()
        {
            double early = 9 * 60 + 20;
            NpcWorkArrivals.MarkArrived("BANK_STAFF", 600);

            Assert.That(
                NpcWorkArrivals.ShouldBeInside(
                    BankClerk,
                    NpcDayPlan.At(BankClerk, early, false),
                    early
                ),
                Is.True
            );
        }

        // 짧은 휴식에서는 일터 앞에서 쉬다가 근무가 다시 시작할 때 들어온다.
        [Test]
        public void StaffOnAShortBreakComeBackWhenTheShiftResumes()
        {
            var npc = new NpcLifeEntry
            {
                NpcId = "DINER_STAFF",
                Role = "STAFF",
                Schedule = new List<NpcScheduleBlock>
                {
                    new NpcScheduleBlock
                    {
                        From = "06:00",
                        To = "12:00",
                        Activity = "WORK",
                    },
                    new NpcScheduleBlock
                    {
                        From = "12:00",
                        To = "13:00",
                        Activity = "OFF",
                    },
                    new NpcScheduleBlock
                    {
                        From = "13:00",
                        To = "18:00",
                        Activity = "WORK",
                    },
                    new NpcScheduleBlock
                    {
                        From = "18:00",
                        To = "06:00",
                        Activity = "OFF",
                    },
                },
            };
            double beforeResume = 12 * 60 + 55;
            NpcDayPlan plan = NpcDayPlan.At(npc, beforeResume, false);
            Assert.That(plan.IsShortBreak, Is.True, "준비: 짧은 휴식");

            Assert.That(NpcWorkArrivals.ShouldBeInside(npc, plan, beforeResume), Is.False);
        }

        private static void ResetSharedClock() =>
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
    }
}
