using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class FeedbackTranscriptTests
    {
        [Test]
        public void Build_ShowsEveryNpcAndLearnerLineInOrder()
        {
            var lines = new List<DialogueLine>
            {
                DialogueLine.FromNpc("What can I get for you?", "무엇을 드릴까요?"),
                DialogueLine.FromUser("I want latte"),
                DialogueLine.FromNpc("Hot or iced?", "따뜻하게 드릴까요, 차갑게 드릴까요?"),
                DialogueLine.FromUser("Iced please"),
            };

            FeedbackTranscriptDocument transcript = FeedbackTranscript.Build(
                "Mia",
                lines,
                new List<UtteranceReview>(),
                null
            );

            Assert.That(
                transcript.Body.IndexOf("What can I get for you?"),
                Is.LessThan(transcript.Body.IndexOf("I want latte"))
            );
            Assert.That(
                transcript.Body.IndexOf("I want latte"),
                Is.LessThan(transcript.Body.IndexOf("Hot or iced?"))
            );
            Assert.That(
                transcript.Body.IndexOf("Hot or iced?"),
                Is.LessThan(transcript.Body.IndexOf("Iced please"))
            );
            Assert.That(transcript.Body, Does.Contain("Mia"));
            Assert.That(transcript.Body, Does.Contain("나"));
        }

        [Test]
        public void Build_LinksOnlyLearnerLinesThatHaveAReview()
        {
            var lines = new List<DialogueLine>
            {
                DialogueLine.FromNpc("Hello", "안녕하세요"),
                DialogueLine.FromUser("I want latte"),
                DialogueLine.FromUser("Thank you"),
            };
            var reviews = new List<UtteranceReview>
            {
                new UtteranceReview(
                    "I want latte",
                    new ReferenceExpression("I'd like a latte.", "", "더 공손해요.")
                ),
            };

            FeedbackTranscriptDocument transcript = FeedbackTranscript.Build(
                "Mia",
                lines,
                reviews,
                null
            );

            Assert.That(transcript.Body, Does.Contain("<link=\"review:0\">"));
            Assert.That(transcript.Body, Does.Not.Contain("<link=\"review:1\">"));
            Assert.That(transcript.Body, Does.Contain("눌러서 피드백 보기"));
        }

        [Test]
        public void Build_SelectedLearnerLine_ShowsCorrectionReasonAndIdiomInline()
        {
            var lines = new List<DialogueLine> { DialogueLine.FromUser("I'm good") };
            var reviews = new List<UtteranceReview>
            {
                new UtteranceReview(
                    "I'm good",
                    new ReferenceExpression(
                        "That sounds good.",
                        "",
                        "좋다는 의견에는 이렇게 말해요."
                    ),
                    null,
                    new ReferenceExpression("I'm good, thanks.", "괜찮아요, 됐어요.", null)
                ),
            };

            FeedbackTranscriptDocument transcript = FeedbackTranscript.Build(
                "Mia",
                lines,
                reviews,
                0
            );

            Assert.That(transcript.Body, Does.Contain("선택한 문장 피드백"));
            Assert.That(transcript.Body, Does.Contain("That sounds good."));
            Assert.That(transcript.Body, Does.Contain("좋다는 의견에는 이렇게 말해요."));
            Assert.That(transcript.Body, Does.Contain("I'm good, thanks."));
            Assert.That(transcript.Body, Does.Contain("괜찮아요, 됐어요."));
        }

        [Test]
        public void Build_DuplicateLearnerLines_ConnectsReviewsInOccurrenceOrder()
        {
            var lines = new List<DialogueLine>
            {
                DialogueLine.FromUser("Yes"),
                DialogueLine.FromNpc("Anything else?", "더 필요한 건 없나요?"),
                DialogueLine.FromUser("Yes"),
            };
            var reviews = new List<UtteranceReview>
            {
                new UtteranceReview("Yes", null, "첫 번째 답변이에요."),
                new UtteranceReview("Yes", null, "두 번째 답변이에요."),
            };

            FeedbackTranscriptDocument transcript = FeedbackTranscript.Build(
                "Mia",
                lines,
                reviews,
                1
            );

            Assert.That(transcript.Body, Does.Contain("<link=\"review:0\">"));
            Assert.That(transcript.Body, Does.Contain("<link=\"review:1\">"));
            Assert.That(transcript.Body, Does.Contain("두 번째 답변이에요."));
            Assert.That(transcript.Body, Does.Not.Contain("첫 번째 답변이에요."));
            Assert.That(transcript.Entries[0].ReviewIndex, Is.EqualTo(0));
            Assert.That(transcript.Entries[1].IsNpc, Is.True);
            Assert.That(transcript.Entries[2].ReviewIndex, Is.EqualTo(1));
        }

        [Test]
        public void Build_EscapesDialogueTextThatLooksLikeTmpMarkup()
        {
            var lines = new List<DialogueLine>
            {
                DialogueLine.FromUser("I need <size=99>coffee</size> & tea"),
            };

            FeedbackTranscriptDocument transcript = FeedbackTranscript.Build(
                "Mia",
                lines,
                new List<UtteranceReview>(),
                null
            );

            Assert.That(transcript.Body, Does.Contain("&lt;size=99&gt;coffee&lt;/size&gt;"));
            Assert.That(transcript.Body, Does.Contain("&amp; tea"));
        }
    }
}
