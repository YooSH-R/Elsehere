using NUnit.Framework;

namespace A305.World.Tests
{
    /// <summary>
    /// 경찰관과 대화하는 동안 신고를 접수하고, 대화를 마치면 순찰차가 출동할 장소를 정한다.
    /// 신고 뒤에 장소를 따로 말해도 그 장소로 출동한다.
    /// </summary>
    public class PoliceReportIntakeTests
    {
        private PoliceReportIntake intake;

        [SetUp]
        public void Create()
        {
            intake = new PoliceReportIntake();
        }

        [Test]
        public void WithoutAReport_NothingIsDispatchedWhenTheTalkEnds()
        {
            intake.Hear("Hello, officer.");
            intake.Hear("Where is the cafe?");

            Assert.That(intake.HasReport, Is.False);
            Assert.That(intake.TryTakeDispatch(out _), Is.False);
        }

        [Test]
        public void ReportWithAPlace_DispatchesThereWhenTheTalkEnds()
        {
            intake.Hear("My wallet was stolen at the diner.");

            Assert.That(intake.HasReport, Is.True);
            Assert.That(intake.TryTakeDispatch(out string place), Is.True);
            Assert.That(place, Is.EqualTo("DINER"));
        }

        [Test]
        public void PlaceSaidAfterTheReport_IsWhereThePatrolCarGoes()
        {
            intake.Hear("My bag was stolen.");
            Assert.That(intake.Place, Is.Null);

            intake.Hear("There is only one cafe in town. Please go there.");

            Assert.That(intake.TryTakeDispatch(out string place), Is.True);
            Assert.That(place, Is.EqualTo("CAFE"));
        }

        [Test]
        public void PlaceSaidBeforeAnyReport_IsNotRemembered()
        {
            intake.Hear("I just came from the bank.");
            intake.Hear("I lost my wallet.");

            Assert.That(intake.TryTakeDispatch(out string place), Is.True);
            Assert.That(place, Is.Null);
        }

        [Test]
        public void ReportWithoutAPlace_PatrolsWhenTheTalkEnds()
        {
            intake.Hear("I lost my wallet.");
            intake.Hear("Red, with a zipper.");

            Assert.That(intake.TryTakeDispatch(out string place), Is.True);
            Assert.That(place, Is.Null);
        }

        [Test]
        public void TakingTheDispatch_StartsTheNextTalkEmpty()
        {
            intake.Hear("My wallet was stolen at the diner.");
            intake.TryTakeDispatch(out _);

            Assert.That(intake.HasReport, Is.False);
            Assert.That(intake.TryTakeDispatch(out _), Is.False);
        }

        [Test]
        public void MisspelledCafe_IsStillTheCafe()
        {
            intake.Hear("my bag was stolen in caffee please go together");

            Assert.That(intake.TryTakeDispatch(out string place), Is.True);
            Assert.That(place, Is.EqualTo("CAFE"));
        }
    }
}
