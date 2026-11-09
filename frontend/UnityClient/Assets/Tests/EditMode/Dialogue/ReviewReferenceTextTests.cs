using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public sealed class ReviewReferenceTextTests
    {
        [Test]
        public void ScenarioNotesDoNotShiftUsageBadgesToTheWrongLine()
        {
            var formatted = ReviewReferenceText.Format(
                new[] { new ReviewReferenceItem("That's the one!", "제 가방이에요!", true) },
                1,
                "<b>분실 가방 찾기</b>\n가방 안: 여권 지갑과 책\n"
            );
            Assert.That(
                formatted.Text.Substring(formatted.Markers[0].SourceIndex),
                Does.StartWith("That's the one!")
            );
            Assert.That(formatted.Markers[0].Used, Is.True);
        }

        [Test]
        public void ExpressionsAndMeaningsRemainLiteralWhileBadgesAreSeparateFromText()
        {
            var formatted = ReviewReferenceText.Format(
                new[]
                {
                    new ReviewReferenceItem(
                        "Can I get that to go?",
                        "포장해 주실 수 있나요?",
                        true
                    ),
                    new ReviewReferenceItem(
                        "<size=99>less ice</size>",
                        "얼음을 줄여 주세요.",
                        false
                    ),
                },
                1
            );
            Assert.That(formatted.Text, Does.Contain("1 / 2"));
            Assert.That(formatted.Text, Does.Contain("Can I get that to go?"));
            Assert.That(formatted.Text, Does.Contain("포장해 주실 수 있나요?"));
            Assert.That(formatted.Text, Does.Not.Contain("<size=99>"));
            Assert.That(formatted.Text, Does.Not.Contain("✓"));
            Assert.That(formatted.Markers.Count, Is.EqualTo(2));
            Assert.That(formatted.Markers[0].Used, Is.True);
            Assert.That(formatted.Markers[1].Used, Is.False);
            Assert.That(
                formatted.Text.Substring(formatted.Markers[0].SourceIndex),
                Does.StartWith("Can I get that to go?")
            );
        }
    }
}
