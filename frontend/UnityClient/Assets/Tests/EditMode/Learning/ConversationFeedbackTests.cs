using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ConversationFeedbackTests
    {
        private static readonly ReferenceExpression SoyLatte = new ReferenceExpression(
            "Could I have a soy latte, please?",
            "두유 카페라테 한 잔 주시겠어요?",
            "Could I have…?는 원하는 것을 정중하게 요청할 때 쓴다."
        );
        private static readonly ReferenceExpression Desserts = new ReferenceExpression(
            "Do you have any desserts?",
            "디저트 있나요?",
            "Do you have any…?로 있는지 자연스럽게 물어볼 수 있다."
        );

        private static readonly List<ReferenceRule> Rules = new List<ReferenceRule>
        {
            new ReferenceRule(new[] { "soy", "latte" }, SoyLatte),
            new ReferenceRule(new[] { "dessert" }, Desserts),
        };

        [Test]
        public void Reviews_UtteranceMatchingRule_GetsReferenceExpression()
        {
            var feedback = new ConversationFeedback("Emma", new[] { "I want soy latte" }, Rules);

            Assert.That(feedback.Reviews, Has.Count.EqualTo(1));
            Assert.That(feedback.Reviews[0].Utterance, Is.EqualTo("I want soy latte"));
            Assert.That(feedback.Reviews[0].Reference, Is.SameAs(SoyLatte));
        }

        [Test]
        public void Reviews_UtteranceWithoutRule_HasNoReference()
        {
            // 참고 표현이 없으면 오류로 보지 않고 그대로 써도 된다고 안내한다.
            var feedback = new ConversationFeedback("Emma", new[] { "Hello!" }, Rules);

            Assert.That(feedback.Reviews[0].Reference, Is.Null);
        }

        [Test]
        public void NextExpression_IsLastReferenceFound()
        {
            var feedback = new ConversationFeedback(
                "Emma",
                new[] { "soy latte please", "Any dessert?", "Thanks!" },
                Rules
            );

            Assert.That(feedback.NextExpression, Is.SameAs(Desserts));
        }

        [Test]
        public void NextExpression_WithoutAnyReference_IsNull()
        {
            var feedback = new ConversationFeedback("Emma", new[] { "Hello!" }, Rules);

            Assert.That(feedback.NextExpression, Is.Null);
        }

        [Test]
        public void Summary_MentionsNpcAndUtteranceCount()
        {
            var feedback = new ConversationFeedback("Emma", new[] { "Hello!", "Thanks!" }, Rules);

            Assert.That(feedback.UtteranceCount, Is.EqualTo(2));
            Assert.That(feedback.Summary, Does.Contain("Emma"));
            Assert.That(feedback.Summary, Does.Contain("2"));
        }

        // 서버 없이 만든 요약도 대화 노트 부제처럼 이름 끝소리에 맞춰 와·과를 고른다.
        [TestCase("Ben", "3개의 문장으로 Ben과 이야기를 나눴어요.")]
        [TestCase("Mia", "3개의 문장으로 Mia와 이야기를 나눴어요.")]
        public void Summary_PicksWaOrGwaByTheLastSoundOfTheName(string npcName, string expected)
        {
            var feedback = new ConversationFeedback(
                npcName,
                new[] { "Hello!", "Thanks!", "Bye!" },
                Rules
            );

            Assert.That(feedback.Summary, Is.EqualTo(expected));
        }

        [Test]
        public void ScriptedFeedback_ShowsTheSituationExpressionsOfTheScene()
        {
            var situations = new List<SituationExpressionData>
            {
                new SituationExpressionData
                {
                    Expression = "I'd like to check in.",
                    MeaningKo = "체크인하고 싶어요.",
                },
            };

            var feedback = new ConversationFeedback("Ben", new[] { "Hello!" }, Rules, situations);

            Assert.That(feedback.SituationExpressions, Is.EqualTo(situations));
        }

        // 대본 대화도 서버 피드백처럼 잘한 점 한 줄을 보여 준다.
        [Test]
        public void ScriptedFeedback_PraisesFinishingTheConversationInEnglish()
        {
            var feedback = new ConversationFeedback("Ben", new[] { "Hello!", "Thanks!" }, Rules);

            Assert.That(feedback.Notes, Has.Count.EqualTo(1));
            Assert.That(feedback.Notes[0], Does.StartWith("잘했어요 · "));
            Assert.That(feedback.Notes[0], Does.Contain("2"));
        }

        [Test]
        public void FromServer_DoneFeedback_ShowsServerSummaryAndNotes()
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(DoneSummary(), null, null)
            );

            Assert.That(feedback.IsFromServer, Is.True);
            Assert.That(feedback.NpcName, Is.EqualTo("Mia"));
            Assert.That(feedback.UtteranceCount, Is.EqualTo(4));
            Assert.That(feedback.Summary, Is.EqualTo("아이스 라떼 주문에 성공했어요"));
            Assert.That(
                feedback.Notes,
                Is.EqualTo(
                    new[]
                    {
                        "잘했어요 · 원하는 메뉴와 사이즈를 분명하게 말했어요.",
                        "아쉬워요 · I want 대신 I'd like를 쓰면 더 공손해요.",
                    }
                )
            );
        }

        [Test]
        public void FromServer_DoneFeedbackWithSuggestion_SuggestsBetterTextForNextConversation()
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(DoneSummary(), null, null)
            );

            Assert.That(
                feedback.NextExpression.English,
                Is.EqualTo("I'd like an iced latte, please.")
            );
            // 서버는 개선 표현의 뜻을 주지 않는다. [표현 저장]이 뜻 자리에 내가 한 말을 저장하지 않도록 뜻은 비우고 이유로 보여 준다.
            Assert.That(feedback.NextExpression.Korean, Is.Empty);
            Assert.That(
                feedback.NextExpression.Reason,
                Is.EqualTo("내가 한 말 · I want 아이스 라떼.")
            );
        }

        [Test]
        public void FromServer_DoneFeedbackWithoutSuggestionOrReviews_HasNoNotesOrNextExpression()
        {
            FeedbackData summary = DoneSummary();
            summary.Reviews = null;
            summary.Suggestion = null;

            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(summary, null, null)
            );

            Assert.That(feedback.Notes, Is.Empty);
            Assert.That(feedback.NextExpression, Is.Null);
        }

        [Test]
        public void FromServer_DoneFeedbackWithOnlyGoodPoints_ShowsOnlyGoodNotes()
        {
            FeedbackData summary = DoneSummary();
            summary.Reviews.Improve = null;

            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(summary, null, null)
            );

            Assert.That(
                feedback.Notes,
                Is.EqualTo(new[] { "잘했어요 · 원하는 메뉴와 사이즈를 분명하게 말했어요." })
            );
        }

        [Test]
        public void FromServer_DoneFeedbackWithoutSummaryText_HasNullSummary()
        {
            FeedbackData summary = DoneSummary();
            summary.Summary = null;

            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(summary, null, null)
            );

            Assert.That(feedback.Summary, Is.Null);
            Assert.That(feedback.Notes, Has.Count.EqualTo(2));
        }

        [Test]
        public void FromServer_DetailedFeedback_BecomesUtteranceReviews()
        {
            var details = new List<DetailedFeedbackItem>
            {
                new DetailedFeedbackItem
                {
                    UserText = "I want 아이스 라떼.",
                    BetterText = "I'd like an iced latte, please.",
                    Reason = "주문할 때는 I'd like가 더 공손해요.",
                },
                new DetailedFeedbackItem
                {
                    UserText = "Medium size, please.",
                    BetterText = "A medium, please.",
                    Reason = "사이즈만 말할 때는 관사와 함께 짧게 말해요.",
                    Idiom = new IdiomData
                    {
                        Text = "for here or to go",
                        MeaningKo = "매장에서 드실 건가요, 가져가실 건가요?",
                    },
                },
                // 적절한 발화는 더 나은 표현이 없다.
                new DetailedFeedbackItem { UserText = "Thank you!", BetterText = null },
            };

            var feedback = ConversationFeedback.FromServer(
                "Mia",
                3,
                new ServerFeedback(DoneSummary(), details, null)
            );

            Assert.That(feedback.Reviews, Has.Count.EqualTo(3));
            Assert.That(feedback.Reviews[0].Utterance, Is.EqualTo("I want 아이스 라떼."));
            Assert.That(
                feedback.Reviews[0].Reference.English,
                Is.EqualTo("I'd like an iced latte, please.")
            );
            Assert.That(feedback.Reviews[0].Reference.Korean, Is.Empty);
            Assert.That(
                feedback.Reviews[0].Reference.Reason,
                Is.EqualTo("주문할 때는 I'd like가 더 공손해요.")
            );
            // 회화 표현은 개선 표현의 뜻 자리에 섞지 않고 따로 둔다(FromServer_Idiom_IsKeptApartFromBetterText).
            Assert.That(feedback.Reviews[1].Reference.Korean, Is.Empty);
            Assert.That(feedback.Reviews[2].Utterance, Is.EqualTo("Thank you!"));
            Assert.That(feedback.Reviews[2].Reference, Is.Null);
        }

        [Test]
        public void FromServer_ReviewWithoutBetterText_KeepsAiReasonAsComment()
        {
            // 고칠 표현이 없어도 AI는 그 발화를 평가한 설명을 준다. 뜻이 분명하지 않은 발화도 여기에 해당한다.
            var details = new List<DetailedFeedbackItem>
            {
                new DetailedFeedbackItem
                {
                    UserText = "That one, the thing.",
                    BetterText = null,
                    Reason =
                        "어떤 것을 가리키는지 분명하지 않아요. 원하는 메뉴 이름을 말해 보세요.",
                },
            };

            var feedback = ConversationFeedback.FromServer(
                "Mia",
                1,
                new ServerFeedback(DoneSummary(), details, null)
            );

            Assert.That(feedback.Reviews[0].Reference, Is.Null);
            Assert.That(
                feedback.Reviews[0].Comment,
                Is.EqualTo("어떤 것을 가리키는지 분명하지 않아요. 원하는 메뉴 이름을 말해 보세요.")
            );
        }

        [Test]
        public void FromServer_Idiom_IsKeptApartFromBetterText()
        {
            // 회화 표현은 고칠 표현이 없는 적절한 발화에도 붙을 수 있다.
            var details = new List<DetailedFeedbackItem>
            {
                new DetailedFeedbackItem
                {
                    UserText = "Medium size, please.",
                    BetterText = "A medium, please.",
                    Reason = "사이즈만 말할 때는 관사와 함께 짧게 말해요.",
                    Idiom = new IdiomData
                    {
                        Text = "for here or to go",
                        MeaningKo = "매장에서 드실 건가요, 가져가실 건가요?",
                    },
                },
                new DetailedFeedbackItem
                {
                    UserText = "I'm good, thanks.",
                    BetterText = null,
                    Reason = "괜찮다는 뜻이 자연스럽게 전달됐어요.",
                    Idiom = new IdiomData { Text = "I'm good", MeaningKo = "괜찮아요, 됐어요" },
                },
            };

            var feedback = ConversationFeedback.FromServer(
                "Mia",
                2,
                new ServerFeedback(DoneSummary(), details, null)
            );

            Assert.That(feedback.Reviews[0].Reference.Korean, Is.Empty);
            Assert.That(feedback.Reviews[0].Idiom.English, Is.EqualTo("for here or to go"));
            Assert.That(
                feedback.Reviews[0].Idiom.Korean,
                Is.EqualTo("매장에서 드실 건가요, 가져가실 건가요?")
            );
            Assert.That(feedback.Reviews[1].Reference, Is.Null);
            Assert.That(feedback.Reviews[1].Idiom.English, Is.EqualTo("I'm good"));
            Assert.That(feedback.Reviews[1].Idiom.Korean, Is.EqualTo("괜찮아요, 됐어요"));
        }

        [Test]
        public void FromServer_SkippedFeedback_ExplainsWhyThereIsNoFeedback()
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                0,
                new ServerFeedback(new FeedbackData { Status = "SKIPPED" }, null, null)
            );

            Assert.That(feedback.IsFromServer, Is.True);
            Assert.That(feedback.Summary, Is.EqualTo("아직 말한 문장이 없어요."));
            Assert.That(feedback.Notes, Is.Empty);
            Assert.That(feedback.Reviews, Is.Empty);
            Assert.That(feedback.NextExpression, Is.Null);
        }

        [Test]
        public void FromServer_FailedFeedback_ShowsFailureInsteadOfFeedback()
        {
            // 만들지 못한 피드백을 가짜 내용으로 채우지 않는다.
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(
                    new FeedbackData { Status = "FAILED", ErrorCode = "AI_TIMEOUT" },
                    null,
                    null
                )
            );

            Assert.That(feedback.IsFromServer, Is.True);
            Assert.That(feedback.Summary, Does.Contain("피드백을 만들지 못했어요"));
            Assert.That(feedback.Notes, Is.Empty);
            Assert.That(feedback.Reviews, Is.Empty);
            Assert.That(feedback.NextExpression, Is.Null);
        }

        [Test]
        public void FromServer_WithoutSummary_ShowsFailureMessage()
        {
            // 종료 요청 자체가 실패한 경우다.
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(null, null, "서버에 연결하지 못했어요.")
            );

            Assert.That(feedback.IsFromServer, Is.True);
            Assert.That(feedback.UtteranceCount, Is.EqualTo(4));
            Assert.That(feedback.Summary, Does.Contain("피드백을 받지 못했어요"));
            Assert.That(feedback.Summary, Does.Contain("서버에 연결하지 못했어요."));
            Assert.That(feedback.Notes, Is.Empty);
            Assert.That(feedback.Reviews, Is.Empty);
            Assert.That(feedback.NextExpression, Is.Null);
        }

        [Test]
        public void FromServer_WithoutSummaryOrFailureMessage_SaysFeedbackWasNotReceived()
        {
            // 종료는 성공했지만 응답에 피드백이 없는 경우다.
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(null, null, null)
            );

            Assert.That(feedback.Summary, Is.EqualTo("피드백을 받지 못했어요."));
            Assert.That(feedback.NextExpression, Is.Null);
        }

        [Test]
        public void FromServer_DoneFeedback_HasEncouragingTitleAndCannotRegenerate()
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                2,
                new ServerFeedback(DoneSummary(), null, null, 58)
            );

            Assert.That(feedback.Title, Is.EqualTo("오늘도 한 걸음 가까워졌어요"));
            Assert.That(feedback.IsFailed, Is.False);
            Assert.That(feedback.CanRegenerate, Is.False);
        }

        [Test]
        public void FromServer_FailedFeedback_SaysSoInTitleAndCanRegenerate()
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(new FeedbackData { Status = "FAILED" }, null, null, 58)
            );

            Assert.That(feedback.Title, Is.EqualTo("피드백을 만들지 못했어요"));
            Assert.That(feedback.IsFailed, Is.True);
            Assert.That(feedback.CanRegenerate, Is.True);
        }

        [Test]
        public void FromServer_EndRequestFailed_IsFailedButCannotRegenerate()
        {
            // 종료 요청이 실패하면 서버에 피드백 기록이 없어 다시 만들 수 없다. 대화 기록만 볼 수 있다.
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(null, null, "서버에 연결하지 못했어요.", 58)
            );

            Assert.That(feedback.Title, Is.EqualTo("피드백을 받지 못했어요"));
            Assert.That(feedback.IsFailed, Is.True);
            Assert.That(feedback.CanRegenerate, Is.False);
        }

        [Test]
        public void FromServer_SkippedFeedback_SaysThereIsNoFeedbackInTitle()
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                0,
                new ServerFeedback(new FeedbackData { Status = "SKIPPED" }, null, null, 58)
            );

            Assert.That(feedback.Title, Is.EqualTo("이번 대화는 피드백이 없어요"));
            Assert.That(feedback.IsFailed, Is.False);
            Assert.That(feedback.CanRegenerate, Is.False);
        }

        [Test]
        public void FromServer_RegenerateRequestFailed_ShowsReasonAndCanTryAgain()
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                4,
                new ServerFeedback(
                    new FeedbackData { Status = "FAILED" },
                    null,
                    "서버에 연결하지 못했어요.",
                    58
                )
            );

            Assert.That(feedback.Summary, Does.Contain("서버에 연결하지 못했어요."));
            Assert.That(feedback.CanRegenerate, Is.True);
        }

        [Test]
        public void ExampleFeedback_HasEncouragingTitle()
        {
            var feedback = new ConversationFeedback("Emma", new[] { "I want soy latte" }, Rules);

            Assert.That(feedback.Title, Is.EqualTo("오늘도 한 걸음 가까워졌어요"));
            Assert.That(feedback.IsFailed, Is.False);
            Assert.That(feedback.CanRegenerate, Is.False);
        }

        private static FeedbackData DoneSummary()
        {
            return new FeedbackData
            {
                Status = "DONE",
                Summary = "아이스 라떼 주문에 성공했어요",
                Reviews = new FeedbackReviews
                {
                    Good = new List<string> { "원하는 메뉴와 사이즈를 분명하게 말했어요." },
                    Improve = new List<string> { "I want 대신 I'd like를 쓰면 더 공손해요." },
                },
                Suggestion = new FeedbackSuggestion
                {
                    UserText = "I want 아이스 라떼.",
                    BetterText = "I'd like an iced latte, please.",
                },
            };
        }
    }
}
