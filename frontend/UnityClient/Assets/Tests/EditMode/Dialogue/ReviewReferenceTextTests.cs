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
        public void CorrectedSentences_ComeFirstUnderTheirOwnHeadingWithWhatTheLearnerSaid()
        {
            var formatted = ReviewReferenceText.Format(
                new[]
                {
                    new ReviewReferenceItem(
                        "Can I get that to go?",
                        "포장해 주세요",
                        true,
                        "포장해 주세요"
                    ),
                    new ReviewReferenceItem(
                        "I'd like a coffee, please.",
                        "주문할 때는 I'd like가 자연스러워요.",
                        false,
                        "I want <b>coffee"
                    ),
                    new ReviewReferenceItem("Could I get less ice?", "얼음을 조금만요", false),
                },
                1
            );

            string text = formatted.Text;
            Assert.That(text, Does.Contain("다시 말해 볼 문장"));
            Assert.That(text, Does.Contain("이 상황에서 자주 써요"));
            Assert.That(
                text.IndexOf("다시 말해 볼 문장"),
                Is.LessThan(text.IndexOf("Can I get that to go?"))
            );
            Assert.That(
                text.IndexOf("이 상황에서 자주 써요"),
                Is.GreaterThan(text.IndexOf("I'd like a coffee, please."))
                    .And.LessThan(text.IndexOf("Could I get less ice?"))
            );
            // 한국어로 한 말은 틀린 말이 아니라 그대로, 고친 영어 문장은 줄을 그어 보인다. 고친 이유는 카드에 싣지 않는다.
            Assert.That(text, Does.Contain("내가 한 말 · 포장해 주세요"));
            Assert.That(text, Does.Contain("내가 한 말 · <s>I want ‹b›coffee</s>"));
            Assert.That(text, Does.Not.Contain("I'd like가 자연스러워요"));
            Assert.That(formatted.Markers.Count, Is.EqualTo(3));
            Assert.That(
                text.Substring(formatted.Markers[1].SourceIndex),
                Does.StartWith("I'd like a coffee, please.")
            );
            Assert.That(
                text.Substring(formatted.Markers[2].SourceIndex),
                Does.StartWith("Could I get less ice?")
            );
        }

        [Test]
        public void ListFirst_PutsTheSentencesAboveTheSituationSoTheyShowWithoutScrolling()
        {
            var formatted = ReviewReferenceText.Format(
                new[]
                {
                    new ReviewReferenceItem(
                        "Can I get that to go?",
                        "포장해 주세요",
                        false,
                        "포장해 주세요"
                    ),
                    new ReviewReferenceItem("Could I get less ice?", "얼음을 조금만요", true),
                },
                1,
                "<b>음료 주문하기</b>\n카페 · 손님\n\n카페에서 음료를 주문해요.\n",
                listFirst: true
            );

            string text = formatted.Text;
            Assert.That(text, Does.StartWith("<color=#C9794A><b>1 / 2</b>"));
            Assert.That(
                text.IndexOf("Could I get less ice?"),
                Is.LessThan(text.IndexOf("음료 주문하기"))
            );
            Assert.That(text, Does.EndWith("카페에서 음료를 주문해요."));
            Assert.That(
                text.Substring(formatted.Markers[0].SourceIndex),
                Does.StartWith("Can I get that to go?")
            );
            Assert.That(
                text.Substring(formatted.Markers[1].SourceIndex),
                Does.StartWith("Could I get less ice?")
            );
        }

        [Test]
        public void WithoutCorrectedSentences_NoSectionHeadingsAreAdded()
        {
            string text = ReviewReferenceText
                .Format(
                    new[] { new ReviewReferenceItem("Could I get less ice?", "얼음", false) },
                    0
                )
                .Text;

            Assert.That(text, Does.Not.Contain("다시 말해 볼 문장"));
            Assert.That(text, Does.Not.Contain("이 상황에서 자주 써요"));
        }

        // 말해 볼 문장은 그 아래 설명보다 크게 보여 한눈에 찾는다.
        [Test]
        public void SentencesToSay_AreLargerThanTheirNotes()
        {
            var formatted = ReviewReferenceText.Format(
                new[]
                {
                    new ReviewReferenceItem("Could I get less ice?", "얼음을 조금만요", false),
                },
                0
            );

            Assert.That(
                formatted.Text,
                Does.Contain("<size=117%><b>Could I get less ice?</b></size>")
            );
            Assert.That(formatted.Text, Does.Contain("<size=85%>얼음을 조금만요</size>"));
            Assert.That(
                formatted.Text.Substring(formatted.Markers[0].SourceIndex),
                Does.StartWith("Could I get less ice?")
            );
        }

        [Test]
        public void PracticeGoals_CountWhatWasDoneWithTheirOwnWording()
        {
            var formatted = ReviewReferenceText.Format(
                new[]
                {
                    new ReviewReferenceItem("인원 말하고 창가 자리 부탁하기", "Two, please.", true),
                    new ReviewReferenceItem("기다리는 시간 묻기", "How long is the wait?", false),
                },
                1,
                "<b>자리 잡기</b>\n",
                "해냈어요"
            );
            Assert.That(formatted.Text, Does.Contain("1 / 2</b>  해냈어요"));
            Assert.That(formatted.Text, Does.Not.Contain("말해봤어요"));
            Assert.That(formatted.Markers[0].Used, Is.True);
            Assert.That(formatted.Markers[1].Used, Is.False);
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
