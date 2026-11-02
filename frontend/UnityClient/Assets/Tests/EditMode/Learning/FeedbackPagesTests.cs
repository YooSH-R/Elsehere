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

        [Test]
        public void Conversation_ShowsSituationExpressionsFirst_WithMeaningUsageAndExample()
        {
            var pages = FeedbackPages.Create(
                Sample(
                    new List<SituationExpressionData>
                    {
                        new SituationExpressionData
                        {
                            Expression = "Can I get that to go?",
                            MeaningKo = "포장해 주실 수 있나요?",
                            UsageKo = "음료를 가지고 나가고 싶을 때 써요.",
                            ExampleEn = "Can I get that to go? I'm in a bit of a hurry.",
                            ExampleKo = "포장해 주실 수 있나요? 좀 급해서요.",
                        },
                    }
                ),
                FeedbackSection.Conversation
            );

            Assert.That(pages.Count, Is.EqualTo(2));
            Assert.That(pages[0].Title, Is.EqualTo("이 상황에서 자주 써요"));
            // 대화 노트는 페이지 제목을 따로 보여 주지 않으므로 다른 회화 카드와 구분되게 본문 첫 줄에 쓴다.
            Assert.That(pages[0].Body, Does.StartWith("<b>이 상황에서 자주 써요</b>"));
            Assert.That(
                pages[0].Body,
                Does.Contain("Can I get that to go?")
                    .And.Contain("포장해 주실 수 있나요?")
                    .And.Contain("음료를 가지고 나가고 싶을 때 써요.")
                    .And.Contain("I'm in a bit of a hurry.")
                    .And.Contain("좀 급해서요.")
            );
            // [표현 저장]이 표현과 뜻을 함께 저장한다.
            Assert.That(pages[0].Expression.English, Is.EqualTo("Can I get that to go?"));
            Assert.That(pages[0].Expression.Korean, Is.EqualTo("포장해 주실 수 있나요?"));
            Assert.That(pages[1].Body, Does.Contain("I'm good"));
        }

        // 한 장씩 넘기지 않고 이 상황 표현과 예문을 한 화면에 모은다. 표현마다 담기는 요약 탭의 표현 줄에서 한다.
        [Test]
        public void Conversation_PutsEverySituationExpressionOnOnePage()
        {
            var expressions = new List<SituationExpressionData>
            {
                new SituationExpressionData
                {
                    Expression = "Could you hold my luggage?",
                    MeaningKo = "짐 좀 맡아 주시겠어요?",
                    ExampleEn = "Could you hold my luggage until three?",
                },
                new SituationExpressionData
                {
                    Expression = "Is there a cafe nearby?",
                    MeaningKo = "근처에 카페가 있나요?",
                    ExampleEn = "Is there a cafe nearby where I can wait?",
                },
            };

            var pages = FeedbackPages.Create(Sample(expressions), FeedbackSection.Conversation);

            Assert.That(pages.Count, Is.EqualTo(2));
            Assert.That(
                pages[0].Body,
                Does.Contain("Could you hold my luggage?")
                    .And.Contain("짐 좀 맡아 주시겠어요?")
                    .And.Contain("until three?")
                    .And.Contain("Is there a cafe nearby?")
                    .And.Contain("where I can wait?")
            );
            Assert.That(pages[0].Expression, Is.Null);
            Assert.That(pages[1].Body, Does.Contain("I'm good"));
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

        private static ConversationFeedback Sample(
            List<SituationExpressionData> situationExpressions = null
        )
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
                        SituationExpressions = situationExpressions,
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
