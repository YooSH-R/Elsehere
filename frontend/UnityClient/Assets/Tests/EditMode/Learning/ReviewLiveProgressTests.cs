using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ReviewLiveProgressTests
    {
        [Test]
        public async Task ClosingDuringTheRequest_DoesNotReopenOrUpdateTheCard()
        {
            var api = new ProgressApi();
            var delayed = new TaskCompletionSource<ReviewPracticeProgress>();
            api.Pending.Enqueue(delayed.Task);
            var state = new ReviewLiveProgress(api);
            state.Begin(Card(), 77);
            Task<bool> request = state.RefreshAsync();
            state.Close();
            delayed.SetResult(api.Response());
            Assert.That(await request, Is.False);
            Assert.That(state.Items, Is.Empty);
            Assert.That(state.Message, Is.Null);
        }

        [Test]
        public async Task AnOlderResponseCannotUndoTheLatestProgress()
        {
            var api = new ProgressApi();
            var older = new TaskCompletionSource<ReviewPracticeProgress>();
            var latest = new TaskCompletionSource<ReviewPracticeProgress>();
            api.Pending.Enqueue(older.Task);
            api.Pending.Enqueue(latest.Task);
            var state = new ReviewLiveProgress(api);
            state.Begin(Card(), 77);
            Task<bool> first = state.RefreshAsync();
            Task<bool> second = state.RefreshAsync();
            latest.SetResult(api.Response());
            Assert.That(await second, Is.True);
            var empty = api.Response();
            empty.Expressions.Clear();
            empty.VerifiedExpressionCount = 0;
            older.SetResult(empty);
            Assert.That(await first, Is.False);
            Assert.That(state.Items[0].Used, Is.True);
            Assert.That(state.VerifiedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task FailedOrMismatchedResponsesKeepTheLastConfirmedCheck()
        {
            var api = new ProgressApi();
            var state = new ReviewLiveProgress(api);
            state.Begin(Card(), 77);
            Assert.That(await state.RefreshAsync(), Is.True);
            api.Pending.Enqueue(
                Task.FromException<ReviewPracticeProgress>(
                    new ApiException("OFFLINE", 0, "offline")
                )
            );
            Assert.That(await state.RefreshAsync(), Is.False);
            Assert.That(state.Items[0].Used, Is.True);
            Assert.That(state.ErrorCode, Is.EqualTo("OFFLINE"));
            var other = api.Response();
            other.ConversationId = 99;
            api.Pending.Enqueue(Task.FromResult(other));
            Assert.That(await state.RefreshAsync(), Is.False);
            Assert.That(state.Items[0].Used, Is.True);
            Assert.That(state.ErrorCode, Is.EqualTo("PROGRESS_MISMATCH"));
        }

        [Test]
        public async Task AcceptedEvidence_ChecksOnlyTheCorrespondingReferenceExpression()
        {
            var api = new ProgressApi();
            var state = new ReviewLiveProgress(api);
            state.Begin(Card(), 77);
            Assert.That(state.Items[0].Used, Is.False);
            Assert.That(await state.RefreshAsync(), Is.True);
            Assert.That(state.VerifiedCount, Is.EqualTo(1));
            Assert.That(state.Items[0].Used, Is.True);
            Assert.That(state.Items[1].Used, Is.False);
            Assert.That(api.RequestedCard, Is.EqualTo(42));
            Assert.That(api.RequestedConversation, Is.EqualTo(77));
        }

        // 서버가 판정한 조건은 왼쪽 위 카드의 같은 차례 조건에 체크된다.
        [Test]
        public async Task SaidConditionsAreCheckedInTheCardOrder()
        {
            var api = new ProgressApi();
            var state = new ReviewLiveProgress(api);
            ReviewCard card = Card();
            card.Scenario = new ReviewScenarioInfo
            {
                Key = "showcase_ORDER",
                Goals = new List<ReviewGoalInfo>
                {
                    new ReviewGoalInfo { Id = "order_burger" },
                    new ReviewGoalInfo { Id = "choose_dining" },
                },
            };
            state.Begin(card, 77);
            Assert.That(state.GoalsDone, Is.EqualTo(new[] { false, false }));

            ReviewPracticeProgress response = api.Response();
            response.CompletedGoals = new List<string> { "choose_dining" };
            api.Pending.Enqueue(Task.FromResult(response));
            Assert.That(await state.RefreshAsync(), Is.True);

            Assert.That(state.GoalsDone, Is.EqualTo(new[] { false, true }));
        }

        private static ReviewCard Card() =>
            new ReviewCard
            {
                Id = 42,
                Targets = new List<ReviewTarget>
                {
                    new ReviewTarget
                    {
                        Expression = "Can I get that to go?",
                        MeaningKo = "포장해 주세요.",
                    },
                    new ReviewTarget
                    {
                        Expression = "Could I get less ice?",
                        MeaningKo = "얼음을 줄여 주세요.",
                    },
                },
            };

        private sealed class ProgressApi : IReviewProgressApi
        {
            public Queue<Task<ReviewPracticeProgress>> Pending =
                new Queue<Task<ReviewPracticeProgress>>();
            public long RequestedCard;
            public long RequestedConversation;

            public Task<ReviewPracticeProgress> GetProgressAsync(long cardId, long conversationId)
            {
                RequestedCard = cardId;
                RequestedConversation = conversationId;
                return Pending.Count > 0 ? Pending.Dequeue() : Task.FromResult(Response());
            }

            public ReviewPracticeProgress Response() =>
                new ReviewPracticeProgress
                {
                    ConversationId = 77,
                    ReviewCardId = 42,
                    VerifiedExpressionCount = 1,
                    Expressions = new List<ReviewExpressionEvidence>
                    {
                        new ReviewExpressionEvidence
                        {
                            Expression = "Can I get that to go?",
                            Evidence = new List<ReviewUtterance>
                            {
                                new ReviewUtterance
                                {
                                    TurnId = 1,
                                    Text = "Could I get that to go, please?",
                                },
                            },
                        },
                    },
                };
        }
    }
}
