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

        private static void ResetSharedClock() =>
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
    }
}
