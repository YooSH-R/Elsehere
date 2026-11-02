using NUnit.Framework;

namespace A305.World.Tests
{
    public class PoliceReportRequestTests
    {
        [TestCase("My wallet was stolen at the diner.", "DINER")]
        [TestCase("Someone stole my bag in front of the bank!", "BANK")]
        [TestCase("I lost my phone at the cafe.", "CAFE")]
        [TestCase("There was a car accident near the post office.", "POST_OFFICE")]
        [TestCase("Someone broke into my house.", "HOME")]
        [TestCase("호텔에서 지갑을 도둑맞았어요", "HOTEL")]
        public void ReportWithOnePlace_DispatchesToThatPlace(string text, string expected)
        {
            Assert.That(PoliceReportRequest.TryResolve(text, out string place), Is.True);
            Assert.That(place, Is.EqualTo(expected));
        }

        [TestCase("I lost my wallet.")]
        [TestCase("I think my bike was stolen.")]
        [TestCase("There's a fight on the street!")]
        [TestCase("It's an emergency, please send someone.")]
        [TestCase("Please call the police.")]
        [TestCase("Someone stole my bike right outside the police station.")]
        [TestCase("지갑을 잃어버렸어요")]
        [TestCase("사고가 났어요 신고하려고요")]
        public void ReportWithoutAPlace_DispatchesOnPatrol(string text)
        {
            Assert.That(PoliceReportRequest.TryResolve(text, out string place), Is.True);
            Assert.That(place, Is.Null);
        }

        [Test]
        public void ReportNamingTwoPlaces_PatrolsInsteadOfGuessing()
        {
            Assert.That(
                PoliceReportRequest.TryResolve(
                    "I lost my wallet at the cafe or maybe the bank.",
                    out string place
                ),
                Is.True
            );
            Assert.That(place, Is.Null);
        }

        [TestCase("Hello, officer.")]
        [TestCase("Where is the bank?")]
        [TestCase("Can you help me find the hotel?")]
        [TestCase("Nothing was stolen, I'm fine.")]
        [TestCase("I'm not hurt, thank you.")]
        [TestCase("There was no accident.")]
        [TestCase("What should I do if I lose my passport?")]
        [TestCase("I watched a movie about a robbery.")]
        [TestCase("I'm lost. How do I get to the hotel?")]
        [TestCase("잃어버린 건 없어요")]
        [TestCase("")]
        [TestCase(null)]
        public void WithoutAClearReport_DoesNotDispatch(string text)
        {
            Assert.That(PoliceReportRequest.TryResolve(text, out string place), Is.False);
            Assert.That(place, Is.Null);
        }
    }
}
