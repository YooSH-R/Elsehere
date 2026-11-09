using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ReviewPracticeFeedbackTests
    {
        [Test]
        public void UsedExpressionExplainsItsMeaningAndQuotesOnlyTheActualLearnerSentence()
        {
            var result = new ReviewPracticeResult
            {
                Expressions = new List<ReviewExpressionEvidence>
                {
                    new ReviewExpressionEvidence
                    {
                        Expression = "I think I left my bag here.",
                        Evidence = new List<ReviewUtterance>
                        {
                            new ReviewUtterance
                            {
                                TurnId = 3,
                                Text = "I think I left my bag here, earlier today.",
                            },
                        },
                    },
                },
            };
            string text = ReviewPracticeFeedback.Format(Card(), result);
            Assert.That(text, Does.Contain("여기에 가방을 두고 간 것 같아요"));
            Assert.That(text, Does.Contain("I think I left my bag here, earlier today."));
            Assert.That(text, Does.Contain("분실한 물건을 문의"));
            Assert.That(text, Does.Not.Contain("Is this your bag?"));
        }

        [Test]
        public void UnusedExpressionIsANextPracticeSuggestionAndIsNeverPraised()
        {
            var result = new ReviewPracticeResult
            {
                Expressions = new List<ReviewExpressionEvidence>
                {
                    new ReviewExpressionEvidence { Expression = "I think I left my bag here." },
                },
            };
            string text = ReviewPracticeFeedback.Format(Card(), result);
            Assert.That(text, Does.Contain("다음에 써 볼 표현"));
            Assert.That(text, Does.Not.Contain("직접 사용했어요"));
            Assert.That(text, Does.Not.Contain("자연스러웠어요"));
        }

        [Test]
        public void MissingResultDoesNotFabricateSuccess()
        {
            Assert.That(
                ReviewPracticeFeedback.Format(Card(), null),
                Does.Contain("결과를 받지 못했어요")
            );
        }

        private static ReviewCard Card() =>
            new ReviewCard
            {
                Targets = new List<ReviewTarget>
                {
                    new ReviewTarget
                    {
                        Expression = "I think I left my bag here.",
                        MeaningKo = "여기에 가방을 두고 간 것 같아요.",
                        UsageKo = "분실한 물건을 문의할 때, 두고 간 장소를 설명하는 표현이에요.",
                        ExampleEn = "Is this your bag?",
                    },
                },
            };
    }
}
