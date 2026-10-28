using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class FeedbackPagesTests
    {
        [Test]
        public void Summary_ContainsSummaryAndNotes_WithoutRepeatingEveryUtterance()
        {
            var feedback = Sample();
            var pages = FeedbackPages.Create(feedback, FeedbackSection.Summary);
            Assert.That(pages.Count, Is.EqualTo(1));
            Assert.That(pages[0].Body, Does.Contain("카페에서 주문했어요"));
            Assert.That(pages[0].Body, Does.Contain("의도를 잘 전달했어요"));
            Assert.That(pages[0].Body, Does.Not.Contain("I want go"));
        }

        [Test]
        public void Corrections_ShowsOneUtteranceAtATime_PreservingTheAiReason()
        {
            var pages = FeedbackPages.Create(Sample(), FeedbackSection.Corrections);
            Assert.That(pages.Count, Is.EqualTo(2));
            Assert.That(pages[0].Body, Does.Contain("I want go"));
            Assert.That(pages[0].Body, Does.Contain("I want to go"));
            Assert.That(pages[0].Body, Does.Contain("to를 넣어요"));
            Assert.That(pages[0].Body, Does.Not.Contain("I'm good"));
            Assert.That(pages[1].Body, Does.Contain("정중하게 거절했어요"));
        }

        [Test]
        public void Conversation_OnlyIncludesExistingIdioms_WithOriginalContext()
        {
            var pages = FeedbackPages.Create(Sample(), FeedbackSection.Conversation);
            Assert.That(pages.Count, Is.EqualTo(1));
            Assert.That(pages[0].Body, Does.Contain("I'm good"));
            Assert.That(pages[0].Body, Does.Contain("괜찮아요, 됐어요"));
        }

        [Test]
        public void Expressions_OffersEachUniqueExpression_AndKeepsMeaningForSaving()
        {
            var pages = FeedbackPages.Create(Sample(), FeedbackSection.Expressions);
            Assert.That(pages.Count, Is.EqualTo(2));
            Assert.That(pages[0].Expression.English, Is.EqualTo("I want to go"));
            Assert.That(pages[1].Expression.Korean, Is.EqualTo("괜찮아요, 됐어요"));
        }

        [TestCase(FeedbackSection.Corrections)]
        [TestCase(FeedbackSection.Conversation)]
        [TestCase(FeedbackSection.Expressions)]
        public void MissingContent_ShowsAnHonestEmptyPage(FeedbackSection section)
        {
            var feedback = new ConversationFeedback("Mia", new string[0], new ReferenceRule[0]);
            var pages = FeedbackPages.Create(feedback, section);
            Assert.That(pages.Count, Is.EqualTo(1));
            Assert.That(pages[0].Body, Is.Not.Empty);
            Assert.That(pages[0].Expression, Is.Null);
        }

        private static ConversationFeedback Sample()
        {
            return ConversationFeedback.FromServer(
                "Mia",
                2,
                new ServerFeedback(
                    new FeedbackData
                    {
                        Status = "DONE",
                        Summary = "카페에서 주문했어요",
                        Reviews = new FeedbackReviews
                        {
                            Good = new List<string> { "의도를 잘 전달했어요" },
                        },
                        Suggestion = new FeedbackSuggestion
                        {
                            UserText = "I want go",
                            BetterText = "I want to go",
                        },
                    },
                    new List<DetailedFeedbackItem>
                    {
                        new DetailedFeedbackItem
                        {
                            UserText = "I want go",
                            BetterText = "I want to go",
                            Reason = "to를 넣어요",
                        },
                        new DetailedFeedbackItem
                        {
                            UserText = "I'm good, thanks.",
                            Reason = "정중하게 거절했어요",
                            Idiom = new IdiomData
                            {
                                Text = "I'm good",
                                MeaningKo = "괜찮아요, 됐어요",
                            },
                        },
                    },
                    null
                )
            );
        }
    }
}
